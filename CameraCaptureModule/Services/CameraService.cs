using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CameraCaptureApp.Models;
using DALSA.SaperaLT.SapClassBasic;
using DALSA.SaperaLT.SapClassGui;

namespace CameraCaptureApp.Services
{
    public class CameraService : ICameraService
    {
        private const int PreviewIntervalMilliseconds = 200;

        private readonly CameraStatus _status;
        private readonly object _frameSync = new object();
        private CameraSettings _settings;
        private SapLocation _serverLocation;
        private string _configFileName;
        private SapAcqDevice _acqDevice;
        private SapAcquisition _acquisition;
        private SapBuffer _buffers;
        private SapAcqToBuf _transfer;
        private DateTime _lastPreviewFrameUtc;
        private DateTime _lastAcquisitionStopUtc = DateTime.MinValue;
        private int _pendingExternalTriggerEvents;
        private long _acquiredImageSequence;
        private bool _stopRequestedDuringCapture;
        private bool _deviceFeaturesAvailable;
        private string _acqDevicePathSummary;
        private string _acqDeviceProbeSummary;

        public CameraService()
        {
            _settings = CameraSettings.CreateDefault();
            _status = new CameraStatus
            {
                FrameWidth = _settings.Width,
                FrameHeight = _settings.Height,
                CameraName = _settings.CameraName,
                UpdateRateHz = 5,
                FollowLatestLine = true,
                ScanStateText = "Idle",
                LastMessage = "Sapera camera service is ready."
            };
        }

        public event EventHandler<CameraFrameEventArgs> FrameReady;

        public event EventHandler<CameraImageAcquiredEventArgs> ImageAcquired;

        public event EventHandler ExternalTriggerReceived;

        public CameraSettings CurrentSettings
        {
            get { return _settings.Clone(); }
        }

        public CameraStatus Status
        {
            get { return _status; }
        }

        public bool IsTransferStartBlocked
        {
            get { return IsTransferStartBlockedCore(); }
        }

        public void ApplySettings(CameraSettings settings)
        {
            _settings = settings.Clone();
            _status.FrameWidth = _settings.Width;
            _status.FrameHeight = _settings.Length > 0 ? _settings.Length : _settings.Height;
            _status.CameraName = _settings.CameraName;

            var requestedSettingsPath = WriteRequestedSettingsReport("ApplySettings");
            if (_status.IsConnected)
            {
                _status.LastMessage = "Camera settings saved only. Disconnect first; acquisition parameters will be written on the next connect. Requested: " + requestedSettingsPath;
                return;
            }

            _status.LastMessage = HasStoredConnectionSettings()
                ? "Camera settings saved locally. Acquisition parameters will be written on the next connect. Requested: " + requestedSettingsPath
                : "Camera settings saved locally. Select connection settings before applying to hardware. Requested: " + requestedSettingsPath;
        }

        public bool Connect()
        {
            if (_status.IsConnected)
            {
                _status.LastMessage = "Camera is still online. Disconnect first, then connect again to write saved acquisition parameters.";
                return false;
            }

            var attemptedStoredSettings = false;
            try
            {
                attemptedStoredSettings = HasStoredConnectionSettings();
                if (!TryPrepareConnectionSettings())
                {
                    _status.LastMessage = "Camera connection was cancelled.";
                    return false;
                }

                return OpenCurrentConnection();
            }
            catch (Exception ex)
            {
                if (attemptedStoredSettings && SelectConnectionSettings(null))
                {
                    try
                    {
                        return OpenCurrentConnection();
                    }
                    catch (Exception retryEx)
                    {
                        ex = retryEx;
                    }
                }

                SafeCleanupSdkObjects("connect failure");
                _status.IsConnected = false;
                _status.HasSignal = false;
                _status.ScanStateText = "Error";
                _status.LastMessage = "Camera connect failed: " + ex.Message;
                return false;
            }
        }

        public void Disconnect()
        {
            if (!_status.IsConnected && !HasSdkObjects())
            {
                _status.IsPreviewing = false;
                _status.IsCaptureInProgress = false;
                _stopRequestedDuringCapture = false;
                _lastAcquisitionStopUtc = DateTime.MinValue;
                _status.IsConnected = false;
                _status.HasSignal = false;
                _status.ScanStateText = "Disconnected";
                _status.LastMessage = "Camera disconnected.";
                return;
            }

            SafeCleanupSdkObjects("disconnect");

            _status.IsPreviewing = false;
            _status.IsCaptureInProgress = false;
            _stopRequestedDuringCapture = false;
            _lastAcquisitionStopUtc = DateTime.MinValue;
            _status.IsConnected = false;
            _status.HasSignal = false;
            _status.ScanStateText = "Disconnected";
            _status.LastMessage = "Camera disconnected.";
        }

        public bool StartPreview()
        {
            if (!_status.IsConnected || _transfer == null)
            {
                _status.LastMessage = "Connect the camera before starting preview.";
                return false;
            }

            if (!CanStartTransfer("preview"))
            {
                return false;
            }

            var externalTriggerArmStatus = GetExternalTriggerArmStatus();
            if (_settings.TriggerMode == TriggerMode.ExternalTrigger && !externalTriggerArmStatus.IsArmed)
            {
                _status.ScanStateText = "Trigger not armed";
                _status.LastMessage = externalTriggerArmStatus.Message;
                return false;
            }

            _pendingExternalTriggerEvents = 0;
            if (_transfer.Grab())
            {
                _status.IsPreviewing = true;
                _status.ScanStateText = "Preview";
                _status.LastMessage = "Preview started.";
                return true;
            }

            _status.LastMessage = "Preview could not be started.";
            return false;
        }

        private ExternalTriggerArmStatus GetExternalTriggerArmStatus()
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return new ExternalTriggerArmStatus(
                    false,
                    "External Trigger is not armed: acquisition is not initialized. Preview was not started to avoid free-run acquisition.");
            }

            var extLineTriggerEnabled = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.EXT_LINE_TRIGGER_ENABLE);
            var extFrameTriggerEnabled = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE);
            var lineArmed = extLineTriggerEnabled.HasValue && extLineTriggerEnabled.Value == 1;
            var frameArmed = extFrameTriggerEnabled.HasValue && extFrameTriggerEnabled.Value == 1;

            if (_settings.ExternalFrameTriggerOneFrame)
            {
                return new ExternalTriggerArmStatus(
                    lineArmed && frameArmed,
                    "External Trigger is not armed: External Trigger One Frame is enabled, so both EXT_LINE_TRIGGER_ENABLE and EXT_FRAME_TRIGGER_ENABLE must read back as 1. Readback EXT_LINE_TRIGGER_ENABLE="
                    + FormatNullableInt(extLineTriggerEnabled)
                    + ", EXT_FRAME_TRIGGER_ENABLE="
                    + FormatNullableInt(extFrameTriggerEnabled)
                    + ". Preview was not started to avoid free-run acquisition.");
            }

            return new ExternalTriggerArmStatus(
                lineArmed,
                "External Trigger is not armed: External Trigger One Frame is disabled, so external line triggering must be armed while EXT_FRAME_TRIGGER_ENABLE may remain 0. Readback EXT_LINE_TRIGGER_ENABLE="
                + FormatNullableInt(extLineTriggerEnabled)
                + ", EXT_FRAME_TRIGGER_ENABLE="
                + FormatNullableInt(extFrameTriggerEnabled)
                + ". Preview was not started to avoid free-run acquisition.");
        }

        private sealed class ExternalTriggerArmStatus
        {
            public ExternalTriggerArmStatus(bool isArmed, string message)
            {
                IsArmed = isArmed;
                Message = message;
            }

            public bool IsArmed { get; private set; }

            public string Message { get; private set; }
        }

        private void ConfigureExternalTriggerEvents()
        {
            if (_acquisition == null)
            {
                return;
            }

            try
            {
                _acquisition.EventType =
                    SapAcquisition.AcqEventType.ExternalTrigger |
                    SapAcquisition.AcqEventType.ExternalTrigger2 |
                    SapAcquisition.AcqEventType.ExternalTriggerIgnored |
                    SapAcquisition.AcqEventType.ExternalTriggerTooSlow |
                    SapAcquisition.AcqEventType.ExtLineTriggerTooSlow |
                    SapAcquisition.AcqEventType.LineTriggerTooFast;
            }
            catch
            {
            }
        }

        public void StopPreview()
        {
            if (_status.IsCaptureInProgress)
            {
                _status.IsPreviewing = false;
                _stopRequestedDuringCapture = true;
                _status.ScanStateText = "Stopping";
                _status.LastMessage = "Stop requested. Current capture will keep waiting for meter wheel pulses until the frame is complete.";
                return;
            }

            if (_transfer != null && _transfer.Initialized)
            {
                _transfer.Freeze();
            }

            _status.IsPreviewing = false;
            _status.IsCaptureInProgress = false;
            _stopRequestedDuringCapture = false;
            _lastAcquisitionStopUtc = DateTime.UtcNow;
            _status.ScanStateText = "Stopped";
            _status.LastMessage = "Preview stopped.";
        }

        public bool CaptureFrame()
        {
            if (!_status.IsConnected || _transfer == null)
            {
                _status.LastMessage = "Connect the camera before grabbing a frame.";
                return false;
            }

            if (!CanStartTransfer("capture"))
            {
                return false;
            }

            if (_transfer.Snap())
            {
                _status.IsCaptureInProgress = true;
                _status.ScanStateText = "Snap";
                _status.LastMessage = "Single frame capture requested.";
                return true;
            }

            _status.LastMessage = "Single frame capture could not be started.";
            return false;
        }

        private bool CanStartTransfer(string actionName)
        {
            if (IsTransferStartBlockedCore())
            {
                _status.LastMessage = BuildTransferStartBlockedMessage(actionName);
                return false;
            }

            return true;
        }

        private bool IsTransferStartBlockedCore()
        {
            return _status.IsCaptureInProgress || GetStopCooldownRemaining() > TimeSpan.Zero;
        }

        private string BuildTransferStartBlockedMessage(string actionName)
        {
            if (_status.IsCaptureInProgress)
            {
                return "Cannot start " + actionName + ": a capture request is still in progress. Wait for the current frame to complete or disconnect/reconnect if Sapera is still holding the resource.";
            }

            var stopCooldownRemaining = GetStopCooldownRemaining();
            return "Cannot start " + actionName + ": acquisition was just stopped. Wait " + Math.Ceiling(stopCooldownRemaining.TotalMilliseconds) + " ms for Sapera to release the resource.";
        }

        private TimeSpan GetStopCooldownRemaining()
        {
            var remaining = TimeSpan.FromMilliseconds(750) - (DateTime.UtcNow - _lastAcquisitionStopUtc);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public bool SelectConnectionSettings(System.Windows.Forms.IWin32Window owner)
        {
            using (var dialog = new AcqConfigDlg(null, _settings.ConfigFilePath ?? string.Empty, AcqConfigDlg.ServerCategory.ServerAcq))
            {
                if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK)
                {
                    return false;
                }

                ApplyDialogSelection(dialog);
                _status.LastMessage = "Connection settings updated from Sapera.";
                return true;
            }
        }

        public bool SelectDeviceFeatureSettings(System.Windows.Forms.IWin32Window owner)
        {
            using (var dialog = new AcqConfigDlg(null, string.Empty, AcqConfigDlg.ServerCategory.ServerAcqDevice))
            {
                if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK)
                {
                    return false;
                }

                var location = dialog.ServerLocation;
                _settings.DeviceFeatureServerName = location.ServerName;
                _settings.DeviceFeatureResourceIndex = location.ResourceIndex;
                _settings.DeviceFeatureConfigFilePath = dialog.ConfigFile;
                _status.LastMessage = "Device feature settings updated from Sapera: " + location.ServerName + "#" + location.ResourceIndex;
                DisposeAcqDeviceOnly();
                _deviceFeaturesAvailable = false;
                _acqDevicePathSummary = string.Empty;
                return true;
            }
        }

        public string ExportLiveFeatureReport()
        {
            if (!_status.IsConnected)
            {
                throw new InvalidOperationException("Connect the camera before probing live features.");
            }

            EnsureAcqDeviceAvailable();
            if (!_deviceFeaturesAvailable || _acqDevice == null || !_acqDevice.Initialized)
            {
                return ExportLiveFeatureFailureReport("SapAcqDevice is not available for this connection path. This camera/frame-grabber path may expose only SapAcquisition parameters.");
            }

            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("CameraCaptureApp Live Feature Report");
            reportBuilder.AppendLine("GeneratedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            reportBuilder.AppendLine("CameraName=" + _status.CameraName);
            reportBuilder.AppendLine("Server=" + _serverLocation.ServerName);
            reportBuilder.AppendLine("ResourceIndex=" + _serverLocation.ResourceIndex);
            reportBuilder.AppendLine("ConfigFile=" + _configFileName);
            reportBuilder.AppendLine("DeviceFeatureServer=" + _settings.DeviceFeatureServerName);
            reportBuilder.AppendLine("DeviceFeatureResourceIndex=" + _settings.DeviceFeatureResourceIndex);
            reportBuilder.AppendLine("DeviceFeatureConfigFile=" + _settings.DeviceFeatureConfigFilePath);
            reportBuilder.AppendLine();

            var featureSummaries = new List<string>();
            var allFeatureBuilder = new StringBuilder();

            for (var i = 0; i < _acqDevice.FeatureCount; i++)
            {
                var feature = new SapFeature();
                if (!_acqDevice.GetFeatureInfo(i, feature))
                {
                    continue;
                }

                var name = feature.Name ?? string.Empty;
                if (name.Length == 0)
                {
                    continue;
                }

                if (IsLineScanCandidateFeature(name, feature))
                {
                    featureSummaries.Add(
                        name
                        + " | DisplayName=" + SafeString(feature.DisplayName)
                        + " | Type=" + feature.DataType
                        + " | AccessMode=" + feature.DataAccessMode
                        + " | Value=" + ReadFeatureValue(name, feature.DataType));
                }

                allFeatureBuilder.AppendLine("[Feature] " + name);
                allFeatureBuilder.AppendLine("DisplayName=" + SafeString(feature.DisplayName));
                allFeatureBuilder.AppendLine("Category=" + SafeString(feature.Category));
                allFeatureBuilder.AppendLine("Type=" + feature.DataType);
                allFeatureBuilder.AppendLine("AccessMode=" + feature.DataAccessMode);
                allFeatureBuilder.AppendLine("Visibility=" + feature.UserVisibility);
                allFeatureBuilder.AppendLine("Description=" + SafeString(feature.Description));
                allFeatureBuilder.AppendLine("Value=" + ReadFeatureValue(name, feature.DataType));
                allFeatureBuilder.AppendLine();
            }

            reportBuilder.AppendLine("[Line Scan Candidate Features]");
            if (featureSummaries.Count == 0)
            {
                reportBuilder.AppendLine("No exposure/integration/line/gain/trigger candidate features were found.");
            }
            else
            {
                foreach (var summary in featureSummaries)
                {
                    reportBuilder.AppendLine(summary);
                }
            }
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("[All Features]");
            reportBuilder.Append(allFeatureBuilder.ToString());

            var filePath = Path.Combine(
                Path.GetDirectoryName(AppLogger.GetLogPath()),
                "live_features_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(filePath, reportBuilder.ToString(), Encoding.UTF8);
            _status.LastMessage = "Live feature report exported: " + Path.GetFileName(filePath);
            return filePath;
        }

        private string ExportLiveFeatureFailureReport(string reason)
        {
            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("CameraCaptureApp Live Feature Failure Report");
            reportBuilder.AppendLine("GeneratedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            reportBuilder.AppendLine("Reason=" + reason);
            reportBuilder.AppendLine("LastMessage=" + _status.LastMessage);
            reportBuilder.AppendLine("CameraName=" + _status.CameraName);
            reportBuilder.AppendLine("Connected=" + _status.IsConnected);
            reportBuilder.AppendLine("AcquisitionServer=" + (_serverLocation == null ? string.Empty : _serverLocation.ServerName));
            reportBuilder.AppendLine("AcquisitionResourceIndex=" + (_serverLocation == null ? -1 : _serverLocation.ResourceIndex));
            reportBuilder.AppendLine("AcquisitionConfigFile=" + _configFileName);
            reportBuilder.AppendLine("DeviceFeatureServer=" + _settings.DeviceFeatureServerName);
            reportBuilder.AppendLine("DeviceFeatureResourceIndex=" + _settings.DeviceFeatureResourceIndex);
            reportBuilder.AppendLine("DeviceFeatureConfigFile=" + _settings.DeviceFeatureConfigFilePath);
            reportBuilder.AppendLine("AcqDeviceProbeSummary=" + FormatAcqDeviceProbeSummary());
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("[Sapera Servers]");
            AppendSaperaResourceReport(reportBuilder);

            var filePath = Path.Combine(
                Path.GetDirectoryName(AppLogger.GetLogPath()),
                "live_features_failed_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(filePath, reportBuilder.ToString(), Encoding.UTF8);
            _status.LastMessage = "Live features unavailable. Diagnostic report exported: " + Path.GetFileName(filePath);
            return filePath;
        }

        private static void AppendSaperaResourceReport(StringBuilder reportBuilder)
        {
            try
            {
                for (var serverIndex = 0; serverIndex < SapManager.GetServerCount(); serverIndex++)
                {
                    var serverName = SapManager.GetServerName(serverIndex);
                    reportBuilder.AppendLine("Server[" + serverIndex + "]=" + serverName);
                    AppendSaperaResourceTypeReport(reportBuilder, serverName, SapManager.ResourceType.Acq, "Acq");
                    AppendSaperaResourceTypeReport(reportBuilder, serverName, SapManager.ResourceType.AcqDevice, "AcqDevice");
                }
            }
            catch (Exception ex)
            {
                reportBuilder.AppendLine("Sapera server enumeration failed: " + ex.Message);
            }
        }

        private static void AppendSaperaResourceTypeReport(StringBuilder reportBuilder, string serverName, SapManager.ResourceType resourceType, string label)
        {
            try
            {
                var count = SapManager.GetResourceCount(serverName, resourceType);
                reportBuilder.AppendLine("  " + label + "Count=" + count);
                for (var resourceIndex = 0; resourceIndex < count; resourceIndex++)
                {
                    var resourceName = SapManager.GetResourceName(serverName, resourceType, resourceIndex);
                    reportBuilder.AppendLine("  " + label + "[" + resourceIndex + "]=" + resourceName);
                }
            }
            catch (Exception ex)
            {
                reportBuilder.AppendLine("  " + label + " enumeration failed: " + ex.Message);
            }
        }

        public string ExportAcquisitionParameterReport()
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                throw new InvalidOperationException("Connect the camera before probing acquisition parameters.");
            }

            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("CameraCaptureApp Acquisition Parameter Report");
            reportBuilder.AppendLine("GeneratedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            reportBuilder.AppendLine("CameraName=" + _status.CameraName);
            reportBuilder.AppendLine("Server=" + _serverLocation.ServerName);
            reportBuilder.AppendLine("ResourceIndex=" + _serverLocation.ResourceIndex);
            reportBuilder.AppendLine("ConfigFile=" + _configFileName);
            reportBuilder.AppendLine();

            foreach (SapAcquisition.Prm parameter in Enum.GetValues(typeof(SapAcquisition.Prm)))
            {
                try
                {
                    if (!_acquisition.IsParameterAvailable(parameter))
                    {
                        continue;
                    }

                    reportBuilder.AppendLine("[Parameter] " + parameter);
                    reportBuilder.AppendLine("Type=" + SafeGetAcquisitionParameterType(parameter));
                    reportBuilder.AppendLine("Value=" + ReadAcquisitionParameterValue(parameter));
                    reportBuilder.AppendLine();
                }
                catch (Exception ex)
                {
                    reportBuilder.AppendLine("[Parameter] " + parameter);
                    reportBuilder.AppendLine("Value=<error: " + ex.Message + ">");
                    reportBuilder.AppendLine();
                }
            }

            var logPath = AppLogger.GetLogPath();
            var logDirectory = Path.GetDirectoryName(logPath);
            var filePath = Path.Combine(
                logDirectory,
                "acquisition_params_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(filePath, reportBuilder.ToString(), Encoding.UTF8);
            _status.LastMessage = "Acquisition parameter report exported: " + Path.GetFileName(filePath);
            return filePath;
        }

        private bool TryPrepareConnectionSettings()
        {
            if (HasStoredConnectionSettings())
            {
                BuildLocationFromSettings();
                return true;
            }

            return SelectConnectionSettings(null);
        }

        private bool HasStoredConnectionSettings()
        {
            return !string.IsNullOrWhiteSpace(_settings.ConfigFilePath)
                && File.Exists(_settings.ConfigFilePath)
                && (!string.IsNullOrWhiteSpace(_settings.ServerName) || _settings.ServerIndex >= 0)
                && _settings.ResourceIndex >= 0;
        }

        private void BuildLocationFromSettings()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ServerName))
            {
                _serverLocation = new SapLocation(_settings.ServerName, _settings.ResourceIndex);
            }
            else
            {
                _serverLocation = new SapLocation(_settings.ServerIndex, _settings.ResourceIndex);
            }

            _configFileName = _settings.ConfigFilePath;
        }

        private void ApplyDialogSelection(AcqConfigDlg dialog)
        {
            _serverLocation = dialog.ServerLocation;
            _configFileName = dialog.ConfigFile;
            _settings.ServerName = _serverLocation.ServerName;
            _settings.ServerIndex = _serverLocation.ServerIndex;
            _settings.ResourceIndex = _serverLocation.ResourceIndex;
            _settings.ConfigFilePath = _configFileName;
            _settings.CameraName = _serverLocation.ServerName;
        }

        private bool CreateSdkObjects()
        {
            if (_acqDevice != null && !_acqDevice.Initialized && !TryCreateAcqDevice())
            {
                _acqDevice.Dispose();
                _acqDevice = null;
            }

            if (_acquisition != null && !_acquisition.Initialized && !_acquisition.Create())
            {
                return false;
            }

            if (_buffers != null && !_buffers.Initialized)
            {
                if (!_buffers.Create())
                {
                    return false;
                }

                _buffers.Clear();
            }

            if (_transfer != null && !_transfer.Initialized && !_transfer.Create())
            {
                return false;
            }

            _acquisition.SignalNotifyEnable = true;
            return true;
        }

        private void DestroySdkObjects()
        {
            if (_transfer != null && _transfer.Initialized)
            {
                _transfer.Destroy();
            }

            if (_buffers != null && _buffers.Initialized)
            {
                _buffers.Destroy();
            }

            if (_acquisition != null && _acquisition.Initialized)
            {
                _acquisition.Destroy();
            }
        }

        private void DisposeSdkObjects()
        {
            _deviceFeaturesAvailable = false;
            _acqDevicePathSummary = string.Empty;
            if (_acqDevice != null)
            {
                _acqDevice.Dispose();
                _acqDevice = null;
            }

            if (_transfer != null)
            {
                _transfer.XferNotify -= OnTransferNotify;
                _transfer.Dispose();
                _transfer = null;
            }

            if (_buffers != null)
            {
                _buffers.Dispose();
                _buffers = null;
            }

            if (_acquisition != null)
            {
                _acquisition.SignalNotify -= OnSignalNotify;
                _acquisition.AcqNotify -= OnAcqNotify;
                _acquisition.Dispose();
                _acquisition = null;
            }
        }

        private void SafeCleanupSdkObjects(string reason)
        {
            SafeSaperaCleanup(reason, "transfer destroy", () =>
            {
                if (_transfer != null && _transfer.Initialized)
                {
                    _transfer.Destroy();
                }
            });
            SafeSaperaCleanup(reason, "buffer destroy", () =>
            {
                if (_buffers != null && _buffers.Initialized)
                {
                    _buffers.Destroy();
                }
            });
            SafeSaperaCleanup(reason, "acquisition destroy", () =>
            {
                if (_acquisition != null && _acquisition.Initialized)
                {
                    _acquisition.Destroy();
                }
            });
            SafeSaperaCleanup(reason, "acq device dispose", () =>
            {
                if (_acqDevice != null)
                {
                    _acqDevice.Dispose();
                    _acqDevice = null;
                }
            });
            SafeSaperaCleanup(reason, "transfer dispose", () =>
            {
                if (_transfer != null)
                {
                    _transfer.XferNotify -= OnTransferNotify;
                    _transfer.Dispose();
                    _transfer = null;
                }
            });
            SafeSaperaCleanup(reason, "buffer dispose", () =>
            {
                if (_buffers != null)
                {
                    _buffers.Dispose();
                    _buffers = null;
                }
            });
            SafeSaperaCleanup(reason, "acquisition dispose", () =>
            {
                if (_acquisition != null)
                {
                    _acquisition.SignalNotify -= OnSignalNotify;
                    _acquisition.AcqNotify -= OnAcqNotify;
                    _acquisition.Dispose();
                    _acquisition = null;
                }
            });

            _deviceFeaturesAvailable = false;
            _acqDevicePathSummary = string.Empty;
        }

        private static void SafeSaperaCleanup(string reason, string actionName, Action cleanupAction)
        {
            try
            {
                cleanupAction();
            }
            catch (Exception ex)
            {
                AppLogger.Log("Sapera " + actionName + " failed during " + reason + ".", ex);
            }
        }

        private bool HasSdkObjects()
        {
            return _acqDevice != null ||
                _acquisition != null ||
                _buffers != null ||
                _transfer != null;
        }

        private void OnTransferNotify(object sender, SapXferNotifyEventArgs argsNotify)
        {
            _status.ScanStateText = argsNotify.Trash ? "Trash" : "Receiving";
            _status.LastMessage = argsNotify.Trash
                ? "Frame landed in trash buffer."
                : "Frame received from Sapera.";

            if (!argsNotify.Trash)
            {
                _status.IsCaptureInProgress = false;
                if (_stopRequestedDuringCapture)
                {
                    _stopRequestedDuringCapture = false;
                    _status.IsPreviewing = false;
                    _lastAcquisitionStopUtc = DateTime.UtcNow;
                    _status.ScanStateText = "Stopped";
                    _status.LastMessage = "Capture completed after stop request.";
                }

                _status.ScannedLineCount++;
                long imageSequence = Interlocked.Increment(ref _acquiredImageSequence);
                if (_settings.TriggerMode == TriggerMode.ExternalTrigger && _pendingExternalTriggerEvents > 0)
                {
                    _pendingExternalTriggerEvents--;
                }

                PublishPreviewFrame();
                PublishAcquiredImage(imageSequence);
            }
        }

        private void OnAcqNotify(object sender, SapAcqNotifyEventArgs argsNotify)
        {
            if (argsNotify.EventType == SapAcquisition.AcqEventType.ExternalTrigger ||
                argsNotify.EventType == SapAcquisition.AcqEventType.ExternalTrigger2)
            {
                _pendingExternalTriggerEvents++;
                _status.LastMessage = "External trigger event received from Sapera.";
                var handler = ExternalTriggerReceived;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }

                return;
            }

            if (argsNotify.EventType == SapAcquisition.AcqEventType.ExternalTriggerIgnored ||
                argsNotify.EventType == SapAcquisition.AcqEventType.ExternalTriggerTooSlow ||
                argsNotify.EventType == SapAcquisition.AcqEventType.ExtLineTriggerTooSlow ||
                argsNotify.EventType == SapAcquisition.AcqEventType.LineTriggerTooFast)
            {
                _status.LastMessage = "External trigger timing event: " + argsNotify.EventType;
            }
        }

        private void OnSignalNotify(object sender, SapSignalNotifyEventArgs argsSignal)
        {
            _status.HasSignal = argsSignal.SignalStatus != SapAcquisition.AcqSignalStatus.None;
            if (!_status.HasSignal)
            {
                _status.IsConnected = false;
                _status.LastMessage = "No camera signal detected.";
            }
            else
            {
                _status.IsConnected = true;
            }
        }

        private void PublishPreviewFrame()
        {
            lock (_frameSync)
            {
                var elapsed = DateTime.UtcNow - _lastPreviewFrameUtc;
                if (elapsed.TotalMilliseconds < PreviewIntervalMilliseconds)
                {
                    return;
                }

                _lastPreviewFrameUtc = DateTime.UtcNow;
            }

            Bitmap previewFrame = null;
            try
            {
                previewFrame = TryCreatePreviewBitmap();
                if (previewFrame == null)
                {
                    return;
                }

                var handler = FrameReady;
                if (handler != null)
                {
                    handler(this, new CameraFrameEventArgs(previewFrame));
                    previewFrame = null;
                }
            }
            catch (Exception ex)
            {
                _status.LastMessage = "Preview conversion failed: " + ex.Message;
            }
            finally
            {
                if (previewFrame != null)
                {
                    previewFrame.Dispose();
                }
            }
        }

        private void PublishAcquiredImage(long sequenceNumber)
        {
            var handler = ImageAcquired;
            if (handler == null)
            {
                return;
            }

            Bitmap image = null;
            try
            {
                image = TryCreatePreviewBitmap();
                if (image == null)
                {
                    return;
                }

                handler(this, new CameraImageAcquiredEventArgs(image, sequenceNumber, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _status.LastMessage = "Acquired image handoff failed: " + ex.Message;
                AppLogger.Log("Acquired image handoff failed.", ex);
            }
            finally
            {
                if (image != null)
                {
                    image.Dispose();
                }
            }
        }

        private Bitmap TryCreatePreviewBitmap()
        {
            if (_buffers == null || !_buffers.Initialized)
            {
                return null;
            }

            var width = _buffers.Width;
            var height = _buffers.Height;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int pixelDepth;
            int pitch;
            if (!_buffers.GetParameter(SapBuffer.Prm.PIXEL_DEPTH, out pixelDepth) ||
                !_buffers.GetParameter(SapBuffer.Prm.PITCH, out pitch))
            {
                return null;
            }

            var bytesPerPixel = Math.Max(1, (pixelDepth + 7) / 8);
            if (bytesPerPixel != 1 && bytesPerPixel != 3)
            {
                bytesPerPixel = 1;
            }

            var expectedStride = width * bytesPerPixel;
            if (pitch < expectedStride)
            {
                pitch = expectedStride;
            }

            var rawBytes = new byte[pitch * height];
            var handle = GCHandle.Alloc(rawBytes, GCHandleType.Pinned);
            try
            {
                if (!_buffers.ReadRect(0, 0, width, height, handle.AddrOfPinnedObject()))
                {
                    return null;
                }
            }
            finally
            {
                handle.Free();
            }

            return bytesPerPixel == 3
                ? CreateRgbBitmap(rawBytes, width, height, pitch)
                : CreateMonoBitmap(rawBytes, width, height, pitch);
        }

        private static Bitmap CreateMonoBitmap(byte[] rawBytes, int width, int height, int sourceStride)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            var palette = bitmap.Palette;
            for (var i = 0; i < 256; i++)
            {
                palette.Entries[i] = Color.FromArgb(i, i, i);
            }

            bitmap.Palette = palette;
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(rawBytes, y * sourceStride, data.Scan0 + (y * data.Stride), width);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private static Bitmap CreateRgbBitmap(byte[] rawBytes, int width, int height, int sourceStride)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                var rowLength = width * 3;
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(rawBytes, y * sourceStride, data.Scan0 + (y * data.Stride), rowLength);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private bool OpenCurrentConnection()
        {
            var connectTimer = System.Diagnostics.Stopwatch.StartNew();
            var stageTimer = System.Diagnostics.Stopwatch.StartNew();
            var timingNotes = new System.Collections.Generic.List<string>();

            DestroySdkObjects();
            DisposeSdkObjects();
            timingNotes.Add("cleanupMs=" + stageTimer.ElapsedMilliseconds);

            var offlineNotes = new System.Collections.Generic.List<string>();
            stageTimer.Restart();
            TryWriteOfflineConfigExposure(offlineNotes);
            timingNotes.Add("offlineConfigMs=" + stageTimer.ElapsedMilliseconds);

            stageTimer.Restart();
            TryApplyNotebookDeviceFeatures(offlineNotes);
            timingNotes.Add("acqDeviceAndCameraFeaturesMs=" + stageTimer.ElapsedMilliseconds);

            stageTimer.Restart();
            _acquisition = new SapAcquisition(_serverLocation, _configFileName);
            _acquisition.SignalNotify += OnSignalNotify;
            _acquisition.SignalNotifyContext = this;
            _acquisition.AcqNotify += OnAcqNotify;
            _acquisition.AcqNotifyContext = this;
            ConfigureExternalTriggerEvents();

            if (!_acquisition.Create())
            {
                throw new InvalidOperationException("Sapera objects could not be created.");
            }
            timingNotes.Add("acquisitionCreateMs=" + stageTimer.ElapsedMilliseconds);

            stageTimer.Restart();
            ApplyWritableCameraSettings(false, offlineNotes, true);
            timingNotes.Add("acquisitionSettingsMs=" + stageTimer.ElapsedMilliseconds);

            stageTimer.Restart();
            if (SapBuffer.IsBufferTypeSupported(_serverLocation, SapBuffer.MemoryType.ScatterGather))
            {
                _buffers = new SapBufferWithTrash(2, _acquisition, SapBuffer.MemoryType.ScatterGather);
            }
            else
            {
                _buffers = new SapBufferWithTrash(2, _acquisition, SapBuffer.MemoryType.ScatterGatherPhysical);
            }

            _transfer = new SapAcqToBuf(_acquisition, _buffers);
            _transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            _transfer.XferNotify += OnTransferNotify;
            _transfer.XferNotifyContext = this;

            if (!CreateSdkObjects())
            {
                throw new InvalidOperationException("Sapera buffer or transfer objects could not be created.");
            }
            timingNotes.Add("bufferTransferCreateMs=" + stageTimer.ElapsedMilliseconds);

            _status.IsConnected = true;
            _status.IsCaptureInProgress = false;
            _stopRequestedDuringCapture = false;
            _lastAcquisitionStopUtc = DateTime.MinValue;
            _status.HasSignal = _acquisition.SignalStatus != SapAcquisition.AcqSignalStatus.None;
            _status.CameraName = _serverLocation.ServerName;
            _status.FrameWidth = _buffers.Width;
            _status.FrameHeight = _buffers.Height;
            if (!_status.HasSignal)
            {
                _status.IsConnected = false;
                _status.LastMessage = "Connected objects were created, but no camera signal was detected.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_status.LastMessage) || _status.LastMessage == "Camera parameters written to Sapera device.")
                {
                    _status.LastMessage = "Camera connected successfully.";
                }
            }
            _status.ScanStateText = "Connected";
            timingNotes.Add("totalMs=" + connectTimer.ElapsedMilliseconds);
            AppLogger.Log(
                "Connect timing server=" + _serverLocation.ServerName
                + " acqDevice=" + (_acqDevicePathSummary ?? "<none>")
                + " " + string.Join(" ", timingNotes.ToArray()));
            return true;
        }

        private void ApplyWritableCameraSettings(bool includeDeviceFeatures)
        {
            ApplyWritableCameraSettings(includeDeviceFeatures, null, true);
        }

        private void ApplyWritableCameraSettings(bool includeDeviceFeatures, System.Collections.Generic.List<string> initialNotes)
        {
            ApplyWritableCameraSettings(includeDeviceFeatures, initialNotes, true);
        }

        private void ApplyWritableCameraSettings(bool includeDeviceFeatures, System.Collections.Generic.List<string> initialNotes, bool applyInternalLineRate)
        {
            var applied = false;
            var notes = initialNotes ?? new System.Collections.Generic.List<string>();

            var shouldApplyInternalLineRate = applyInternalLineRate && _settings.TriggerMode == TriggerMode.Continuous;
            if (shouldApplyInternalLineRate && TrySetInternalLineRate(notes))
            {
                applied = true;
            }
            else if (applyInternalLineRate)
            {
                notes.Add("InternalLineRate acquisition trigger skipped for " + _settings.TriggerMode + " mode");
            }

            if (TrySetLengthParameters(notes))
            {
                _status.FrameHeight = _settings.Length;
                applied = true;
            }

            if (_settings.TriggerMode == TriggerMode.Continuous)
            {
                notes.Add("FreeRun line sync source kept at None; acquisition trigger parameters were not changed.");
            }
            else if (TryApplyAcquisitionTriggerMode(notes))
            {
                applied = true;
            }

            if (TrySetExposureParameters(notes))
            {
                applied = true;
            }

            if (TrySetExternalFrameTriggerOneFrame(notes))
            {
                applied = true;
            }

            if (includeDeviceFeatures)
            {
                notes.Add("Gain not applied: Sapera AcqDevice path is not available for this acquisition connection.");
            }

            if (notes.Count > 0)
            {
                var reportPath = WriteApplyParameterReport(notes);
                _status.LastMessage = string.IsNullOrWhiteSpace(reportPath)
                    ? string.Join(" | ", notes.ToArray())
                    : "Camera parameters applied. Details: " + reportPath;
            }
            else if (applied)
            {
                _status.LastMessage = "Camera parameters written to Sapera device.";
            }
        }

        private string WriteApplyParameterReport(System.Collections.Generic.List<string> notes)
        {
            try
            {
                var logPath = AppLogger.GetLogPath();
                var logDirectory = Path.GetDirectoryName(logPath);
                var reportPath = Path.Combine(logDirectory, "last_apply_params.txt");
                var builder = new StringBuilder();

                builder.AppendLine("CameraCaptureApp Apply Parameter Report");
                builder.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.AppendLine("Server=" + SafeString(_settings.ServerName));
                builder.AppendLine("ConfigFile=" + SafeString(_settings.ConfigFilePath));
                builder.AppendLine("DeviceFeatureServer=" + SafeString(_settings.DeviceFeatureServerName));
                builder.AppendLine("DeviceFeatureResourceIndex=" + _settings.DeviceFeatureResourceIndex);
                builder.AppendLine("DeviceFeatureConfigFile=" + SafeString(_settings.DeviceFeatureConfigFilePath));
                builder.AppendLine("RequestedExposureTime=" + _settings.ExposureTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedGain=" + _settings.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedInternalLineRate=" + _settings.InternalLineRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine();
                builder.AppendLine("[Apply Notes]");

                foreach (var note in notes)
                {
                    builder.AppendLine(note);
                }

                AppendAcquisitionTimingParameterSnapshot(builder);
                AppendAcquisitionTimingCapabilitySnapshot(builder);
                AppendConfigTimingKeySnapshot(builder);
                AppendLiveFeatureSnapshot(builder);

                File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
                return reportPath;
            }
            catch (Exception ex)
            {
                AppLogger.Log("Apply parameter report write failed.", ex);
                return string.Empty;
            }
        }

        private void AppendLiveFeatureSnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("[Live Device Feature Snapshot]");

            try
            {
                if (!_deviceFeaturesAvailable || _acqDevice == null || !_acqDevice.Initialized)
                {
                    builder.AppendLine("SapAcqDevice unavailable or not loaded. ProbeSummary=" + FormatAcqDeviceProbeSummary());
                    builder.AppendLine("Live feature snapshot skipped to avoid SapAcqDevice.LoadFeatures message boxes. Use Load Features / Live Features manually if this camera exposes an AcqDevice path.");
                    return;
                }

                var matched = false;
                for (var i = 0; i < _acqDevice.FeatureCount; i++)
                {
                    var feature = new SapFeature();
                    if (!_acqDevice.GetFeatureInfo(i, feature))
                    {
                        continue;
                    }

                    var name = feature.Name ?? string.Empty;
                    if (name.Length == 0 || !IsLineScanCandidateFeature(name, feature))
                    {
                        continue;
                    }

                    matched = true;
                    builder.AppendLine(
                        name
                        + " type=" + feature.DataType
                        + " access=" + feature.DataAccessMode
                        + " value=" + ReadFeatureValue(name, feature.DataType)
                        + " display=" + SafeString(feature.DisplayName));
                }

                if (!matched)
                {
                    builder.AppendLine("No line/trigger/rate/exposure candidate features were found.");
                }
            }
            catch (Exception ex)
            {
                builder.AppendLine("Live feature snapshot failed: " + ex.Message);
            }
        }

        private void AppendConfigTimingKeySnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("[CCF Timing/Trigger Keys]");

            if (string.IsNullOrWhiteSpace(_configFileName) || !File.Exists(_configFileName))
            {
                builder.AppendLine("Config file unavailable: " + SafeString(_configFileName));
                return;
            }

            try
            {
                foreach (var rawLine in File.ReadAllLines(_configFileName, Encoding.Default))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, separatorIndex).Trim();
                    if (IsTimingOrTriggerParameterName(key) || key.IndexOf("CC", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        builder.AppendLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                builder.AppendLine("CCF timing key snapshot failed: " + ex.Message);
            }
        }

        private void AppendAcquisitionTimingParameterSnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("[Acquisition Timing/Trigger Parameters]");

            if (_acquisition == null || !_acquisition.Initialized)
            {
                builder.AppendLine("<acquisition unavailable>");
                return;
            }

            foreach (SapAcquisition.Prm parameter in Enum.GetValues(typeof(SapAcquisition.Prm)))
            {
                var name = parameter.ToString();
                if (!IsTimingOrTriggerParameterName(name))
                {
                    continue;
                }

                try
                {
                    if (!_acquisition.IsParameterAvailable(parameter))
                    {
                        continue;
                    }

                    builder.AppendLine(
                        name
                        + " type=" + SafeGetAcquisitionParameterType(parameter)
                        + " value=" + ReadAcquisitionParameterValue(parameter));
                }
                catch (Exception ex)
                {
                    builder.AppendLine(name + " value=<error: " + ex.Message + ">");
                }
            }
        }

        private void AppendAcquisitionTimingCapabilitySnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("[Acquisition Timing/Trigger Capabilities]");

            if (_acquisition == null || !_acquisition.Initialized)
            {
                builder.AppendLine("<acquisition unavailable>");
                return;
            }

            foreach (SapAcquisition.Cap capability in Enum.GetValues(typeof(SapAcquisition.Cap)))
            {
                var name = capability.ToString();
                if (!IsTimingOrTriggerParameterName(name))
                {
                    continue;
                }

                try
                {
                    int value;
                    builder.AppendLine(
                        name
                        + " value="
                        + (_acquisition.GetCapability(capability, out value) ? value.ToString() : "<unreadable>"));
                }
                catch (Exception ex)
                {
                    builder.AppendLine(name + " value=<error: " + ex.Message + ">");
                }
            }
        }

        private static bool IsTimingOrTriggerParameterName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var upperName = name.ToUpperInvariant();
            return upperName.Contains("LINE")
                || upperName.Contains("TRIGGER")
                || upperName.Contains("RATE")
                || upperName.Contains("FREQ")
                || upperName.Contains("TIME")
                || upperName.Contains("DURATION")
                || upperName.Contains("PERIOD")
                || upperName.Contains("INTEGRATE")
                || upperName.Contains("CONNECTOR")
                || upperName.Contains("CAMLINK")
                || upperName.Contains("CONTROL")
                || upperName.Contains("SIGNAL")
                || upperName.Contains("SYNC")
                || upperName.Contains("SHAFT")
                || upperName.Contains("ENCODER");
        }

        private string WriteRequestedSettingsReport(string source)
        {
            try
            {
                var logPath = AppLogger.GetLogPath();
                var logDirectory = Path.GetDirectoryName(logPath);
                var reportPath = Path.Combine(logDirectory, "last_requested_settings.txt");
                var builder = new StringBuilder();

                builder.AppendLine("CameraCaptureApp Requested Settings Report");
                builder.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.AppendLine("Source=" + SafeString(source));
                builder.AppendLine("IsConnected=" + _status.IsConnected);
                builder.AppendLine("CameraName=" + SafeString(_settings.CameraName));
                builder.AppendLine("Server=" + SafeString(_settings.ServerName));
                builder.AppendLine("ServerIndex=" + _settings.ServerIndex);
                builder.AppendLine("ResourceIndex=" + _settings.ResourceIndex);
                builder.AppendLine("ConfigFile=" + SafeString(_settings.ConfigFilePath));
                builder.AppendLine("DeviceFeatureServer=" + SafeString(_settings.DeviceFeatureServerName));
                builder.AppendLine("DeviceFeatureResourceIndex=" + _settings.DeviceFeatureResourceIndex);
                builder.AppendLine("DeviceFeatureConfigFile=" + SafeString(_settings.DeviceFeatureConfigFilePath));
                builder.AppendLine("RequestedExposureTime=" + _settings.ExposureTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedExposureTimeString=" + decimal.ToInt32(decimal.Truncate(_settings.ExposureTime)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedGain=" + _settings.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedGainIntegerString=" + decimal.ToInt32(decimal.Truncate(_settings.Gain)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedLength=" + _settings.Length);
                builder.AppendLine("RequestedInternalLineRate=" + _settings.InternalLineRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("RequestedTriggerMode=" + _settings.TriggerMode);

                File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
                return reportPath;
            }
            catch (Exception ex)
            {
                AppLogger.Log("Requested settings report write failed.", ex);
                return "<requested report unavailable>";
            }
        }

        private void TryWriteOfflineConfigExposure(System.Collections.Generic.List<string> notes)
        {
            if (string.IsNullOrWhiteSpace(_configFileName) || !File.Exists(_configFileName))
            {
                notes.Add("CCF exposure not updated: config file not found");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_configFileName, Encoding.Default);
                var exposureText = _settings.ExposureTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var updated = false;
                var matchedKeys = new System.Collections.Generic.List<string>();

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.TrimStart();
                    if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.StartsWith("["))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, separatorIndex).Trim();
                    if (!IsExposureConfigKey(key))
                    {
                        continue;
                    }

                    lines[i] = line.Substring(0, separatorIndex + 1) + exposureText;
                    matchedKeys.Add(key);
                    updated = true;
                }

                if (!updated)
                {
                    notes.Add("CCF exposure key not found; acquisition parameter write will be tried");
                    return;
                }

                var backupPath = _configFileName + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
                File.Copy(_configFileName, backupPath, false);
                File.WriteAllLines(_configFileName, lines, Encoding.Default);
                notes.Add("CCF exposure saved requested=" + exposureText + " keys=" + string.Join(",", matchedKeys.ToArray()));
            }
            catch (Exception ex)
            {
                AppLogger.Log("Offline CCF exposure update failed.", ex);
                notes.Add("CCF exposure update failed: " + ex.Message);
            }
        }

        private static bool IsExposureConfigKey(string key)
        {
            return string.Equals(key, "ExposureTime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "ExposureTimeAbs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Exposure", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "ExposureTimeRaw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "LineIntegrateDuration", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "LineIntegrationDuration", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "LINE_INTEGRATE_DURATION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "TimeIntegrateDuration", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "TimeIntegrationDuration", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "TIME_INTEGRATE_DURATION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "CamTriggerDuration", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "CAM_TRIGGER_DURATION", StringComparison.OrdinalIgnoreCase);
        }

        private void TryApplyNotebookDeviceFeatures(System.Collections.Generic.List<string> notes)
        {
            var timingTimer = System.Diagnostics.Stopwatch.StartNew();
            var stageTimer = System.Diagnostics.Stopwatch.StartNew();
            var timingNotes = new System.Collections.Generic.List<string>();
            try
            {
                EnsureAcqDeviceAvailable();
                timingNotes.Add("ensureAcqDeviceMs=" + stageTimer.ElapsedMilliseconds);
                if (!_deviceFeaturesAvailable || _acqDevice == null || !_acqDevice.Initialized)
                {
                    notes.Add(
                        "Notebook features skipped: no AcqDevice could be associated with selected acquisition server "
                        + FormatSapLocation(_serverLocation)
                        + FormatAcqDeviceProbeSummary()
                        + (CountAcquisitionServers() > 1
                            ? ". Saved global feature path was not used because multiple Acquisition servers are present."
                            : string.Empty));
                    timingNotes.Add("totalMs=" + timingTimer.ElapsedMilliseconds);
                    AppLogger.Log("Notebook feature timing target=<none> " + string.Join(" ", timingNotes.ToArray()));
                    return;
                }

                var notebookDevice = _acqDevice;
                var exposureText = decimal.ToInt32(decimal.Truncate(_settings.ExposureTime)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var gainText = decimal.ToInt32(decimal.Truncate(_settings.Gain)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lineRateText = _settings.InternalLineRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lineRateIntegerText = decimal.ToInt32(decimal.Truncate(_settings.InternalLineRate)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                stageTimer.Restart();
                var lineRateResult = _settings.TriggerMode == TriggerMode.Continuous
                    ? TrySetNotebookInternalLineRateFeatures(notebookDevice, lineRateText, lineRateIntegerText, true)
                    : new NotebookApplyResult
                    {
                        Applied = false,
                        Message = "InternalLineRate early skipped for " + _settings.TriggerMode + " mode"
                    };
                timingNotes.Add("lineRateMs=" + stageTimer.ElapsedMilliseconds);

                stageTimer.Restart();
                var exposureResult = TrySetNotebookExposureFeatures(notebookDevice, exposureText);
                timingNotes.Add("exposureMs=" + stageTimer.ElapsedMilliseconds);

                stageTimer.Restart();
                var gainApplied = TrySetNotebookFeatureValue(notebookDevice, "Gain", gainText);
                timingNotes.Add("gainMs=" + stageTimer.ElapsedMilliseconds);

                stageTimer.Restart();
                var triggerResult = TrySetNotebookTriggerModeFeatures(notebookDevice, _settings.TriggerMode);
                timingNotes.Add("triggerMs=" + stageTimer.ElapsedMilliseconds);

                if (exposureResult.Applied || gainApplied || lineRateResult.Applied || triggerResult.Applied)
                {
                    stageTimer.Restart();
                    TryUpdateNotebookFeaturesToDevice(notebookDevice);
                    timingNotes.Add("updateFeaturesMs=" + stageTimer.ElapsedMilliseconds);
                }
                else
                {
                    timingNotes.Add("updateFeaturesMs=skipped");
                }

                timingNotes.Add("totalMs=" + timingTimer.ElapsedMilliseconds);
                AppLogger.Log(
                    "Notebook feature timing target=" + _acqDevicePathSummary
                    + " " + string.Join(" ", timingNotes.ToArray()));
                notes.Add(
                    "Notebook features target=" + _acqDevicePathSummary + " "
                    + "targetSource=" + GetAcqDeviceTargetSource() + " "
                    + exposureResult.Message
                    + " Gain=" + FormatApplyResult(gainApplied, gainText)
                    + " " + lineRateResult.Message
                    + " " + triggerResult.Message);
            }
            catch (Exception ex)
            {
                timingNotes.Add("totalMs=" + timingTimer.ElapsedMilliseconds);
                AppLogger.Log(
                    "Notebook feature timing target=" + (_acqDevicePathSummary ?? "<none>")
                    + " result=exception " + string.Join(" ", timingNotes.ToArray()));
                AppLogger.Log("Notebook feature write failed.", ex);
                notes.Add("Notebook features unavailable: " + ex.Message);
            }
        }

        private bool IsSelectedAcqDevicePath()
        {
            return _serverLocation != null
                && string.Equals(_acqDevicePathSummary, FormatSapLocation(_serverLocation), StringComparison.OrdinalIgnoreCase);
        }

        private string GetAcqDeviceTargetSource()
        {
            if (IsSelectedAcqDevicePath())
            {
                return "selected-acquisition-server";
            }

            if (_acqDevice != null && IsAcqDeviceLocationAssociatedWithAcquisition(_serverLocation == null ? null : _serverLocation.ServerName, _acqDevice.Location))
            {
                return "resource-name-matched";
            }

            return IsConfiguredAcqDeviceLocation(_acqDevice == null ? null : _acqDevice.Location)
                ? "configured-compatible-path"
                : "related-server";
        }

        private static bool IsAcqDeviceLocationAssociatedWithAcquisition(string acquisitionServerName, SapLocation deviceLocation)
        {
            if (string.IsNullOrWhiteSpace(acquisitionServerName) || deviceLocation == null)
            {
                return false;
            }

            try
            {
                var resourceName = SapManager.GetResourceName(
                    deviceLocation.ServerName,
                    SapManager.ResourceType.AcqDevice,
                    deviceLocation.ResourceIndex);
                return IsAcqDeviceResourceForAcquisition(acquisitionServerName, resourceName);
            }
            catch
            {
                return false;
            }
        }

        private static string FormatSapLocation(SapLocation location)
        {
            if (location == null)
            {
                return "<none>";
            }

            return location.ServerName + "#" + location.ResourceIndex;
        }

        private static bool TrySetNotebookFeatureValue(SapAcqDevice device, string featureName, string value)
        {
            try
            {
                if (!IsNotebookFeatureAvailable(device, featureName))
                {
                    return false;
                }

                return device.SetFeatureValue(featureName, value);
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetNotebookFeatureValue(SapAcqDevice device, string featureName, long value)
        {
            try
            {
                if (!IsNotebookFeatureAvailable(device, featureName))
                {
                    return false;
                }

                return device.SetFeatureValue(featureName, value);
            }
            catch
            {
                return false;
            }
        }

        private NotebookApplyResult TrySetNotebookInternalLineRateFeatures(SapAcqDevice notebookDevice, string lineRateText, string lineRateIntegerText, bool earlyApply)
        {
            var details = new System.Collections.Generic.List<string>();
            var applied = false;
            long lineRateInt64;
            var hasLineRateInt64 = long.TryParse(lineRateIntegerText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out lineRateInt64);

            const string featureName = "AcquisitionLineRate";
            if (!IsNotebookFeatureAvailable(notebookDevice, featureName))
            {
                details.Add(featureName + "=missing");
            }
            else
            {
                var readbackBefore = ReadNotebookFeatureValue(notebookDevice, featureName);
                if ((hasLineRateInt64 && TrySetNotebookFeatureValue(notebookDevice, featureName, lineRateInt64))
                    || TrySetNotebookFeatureValue(notebookDevice, featureName, lineRateText)
                    || TrySetNotebookFeatureValue(notebookDevice, featureName, lineRateIntegerText))
                {
                    applied = true;
                    details.Add(featureName + "=ok(" + lineRateText + ") before=" + readbackBefore + " readback=" + ReadNotebookFeatureValue(notebookDevice, featureName));
                }
                else
                {
                    details.Add(featureName + "=failed(" + lineRateText + ") before=" + readbackBefore + " access=" + ReadNotebookFeatureAccessMode(notebookDevice, featureName));
                }
            }

            return new NotebookApplyResult
            {
                Applied = applied,
                Message = "InternalLineRate " + (earlyApply ? "early " : string.Empty) + "Features[" + string.Join(",", details.ToArray()) + "]"
            };
        }

        private static bool TrySetNotebookEnumFeatureValue(SapAcqDevice device, string featureName, string[] values)
        {
            if (!CanWriteNotebookFeatureStrict(device, featureName))
            {
                return false;
            }

            foreach (var value in values)
            {
                if (TrySetNotebookFeatureValue(device, featureName, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetNotebookLineTriggerInput(SapAcqDevice device)
        {
            var applied = false;
            var selectors = new[] { "LineStart", "LineTrigger", "AcquisitionLine", "ExposureStart", "FrameStart" };
            var sources = new[] { "CC1", "Line1", "Input1", "CameraControl1", "CameraLinkCC1", "CL_CC1", "External", "ExternalLine", "LineTrigger" };

            foreach (var selector in selectors)
            {
                var selectorApplied = TrySetNotebookEnumFeatureValue(device, "TriggerSelector", new[] { selector });
                var modeApplied = TrySetNotebookEnumFeatureValue(device, "TriggerMode", new[] { "On" });
                var sourceApplied = TrySetNotebookEnumFeatureValue(device, "TriggerSource", sources);
                applied = applied || (selectorApplied && modeApplied && sourceApplied);
            }

            return applied;
        }

        private static NotebookApplyResult TrySetNotebookTriggerModeFeatures(SapAcqDevice device, TriggerMode triggerMode)
        {
            var details = new System.Collections.Generic.List<string>();
            var applied = false;

            switch (triggerMode)
            {
                case TriggerMode.Continuous:
                    applied = TrySetNotebookTriggerSelectors(
                        device,
                        details,
                        false,
                        null,
                        new[] { "FrameStart", "LineStart", "AcquisitionStart", "ExposureStart" });
                    break;

                case TriggerMode.ExternalTrigger:
                case TriggerMode.SoftwareTrigger:
                    var disabledFrameSelectors = TrySetNotebookTriggerSelectors(
                        device,
                        details,
                        false,
                        null,
                        new[] { "FrameStart", "AcquisitionStart", "ExposureStart" });
                    var enabledLineSelectors = TrySetNotebookTriggerSelectors(
                        device,
                        details,
                        true,
                        new[] { "Line1", "Input1", "CC1", "CameraControl1", "CameraLinkCC1", "CL_CC1", "External", "ExternalLine", "LineTrigger" },
                        new[] { "LineStart", "LineTrigger", "AcquisitionLine" });
                    applied = disabledFrameSelectors || enabledLineSelectors;
                    break;

                case TriggerMode.SingleFrame:
                    applied = TrySetNotebookTriggerSelectors(
                        device,
                        details,
                        true,
                        new[] { "Software", "SoftwareTrigger" },
                        new[] { "FrameStart", "AcquisitionStart" });
                    break;
            }

            return new NotebookApplyResult
            {
                Applied = applied,
                Message = "TriggerMode " + triggerMode + " Features[" + string.Join(",", details.ToArray()) + "]"
            };
        }

        private static bool TrySetNotebookTriggerSelectors(SapAcqDevice device, System.Collections.Generic.List<string> details, bool enabled, string[] sources, string[] selectors)
        {
            var applied = false;
            foreach (var selector in selectors)
            {
                var selectorApplied = TrySetNotebookEnumFeatureValue(device, "TriggerSelector", new[] { selector });
                var modeApplied = TrySetNotebookEnumFeatureValue(device, "TriggerMode", new[] { enabled ? "On" : "Off" });
                var sourceApplied = !enabled || TrySetNotebookEnumFeatureValue(device, "TriggerSource", sources);

                details.Add(
                    selector
                    + ":selector=" + FormatApplyResult(selectorApplied, selector)
                    + " mode=" + FormatApplyResult(modeApplied, enabled ? "On" : "Off")
                    + (enabled ? " source=" + FormatApplyResult(sourceApplied, sources != null && sources.Length > 0 ? string.Join("/", sources) : "<none>") : string.Empty));

                applied = applied || (selectorApplied && modeApplied && sourceApplied);
            }

            if (!applied)
            {
                details.Add("no writable TriggerSelector/TriggerMode/TriggerSource combination");
            }

            return applied;
        }

        private static NotebookApplyResult TrySetNotebookExposureFeatures(SapAcqDevice device, string exposureText)
        {
            var featureNames = new[]
            {
                "ExposureTime",
                "ExposureTimeAbs",
                "ExposureTimeRaw",
                "Exposure",
                "LineExposureTime",
                "AcquisitionExposureTime",
                "ShutterTime",
                "ShutterDuration"
            };

            var details = new System.Collections.Generic.List<string>();
            var applied = false;

            foreach (var featureName in featureNames)
            {
                if (!IsNotebookFeatureAvailable(device, featureName))
                {
                    details.Add(featureName + "=missing");
                    continue;
                }

                if (!CanWriteNotebookFeature(device, featureName))
                {
                    details.Add(featureName + "=readonly-at-check readback=" + ReadNotebookFeatureValue(device, featureName));
                }

                var setOk = TrySetNotebookFeatureValue(device, featureName, exposureText);
                if (!setOk)
                {
                    details.Add(featureName + "=failed(" + exposureText + ") access=" + ReadNotebookFeatureAccessMode(device, featureName));
                    continue;
                }

                applied = true;
                details.Add(featureName + "=ok(" + exposureText + ") readback=" + ReadNotebookFeatureValue(device, featureName));
                break;
            }

            return new NotebookApplyResult
            {
                Applied = applied,
                Message = "ExposureFeatures[" + string.Join(",", details.ToArray()) + "]"
            };
        }

        private static bool CanWriteNotebookFeatureStrict(SapAcqDevice device, string featureName)
        {
            if (device == null || string.IsNullOrWhiteSpace(featureName))
            {
                return false;
            }

            try
            {
                if (!device.IsFeatureAvailable(featureName))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            SapFeature feature = null;
            try
            {
                feature = new SapFeature(device.Location);
                feature.Create();
                if (device.GetFeatureInfo(featureName, feature))
                {
                    var accessMode = feature.DataAccessMode.ToString();
                    return accessMode.IndexOf("Write", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
            }
            finally
            {
                if (feature != null)
                {
                    try
                    {
                        if (feature.Initialized)
                        {
                            feature.Destroy();
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        feature.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private static string ReadNotebookFeatureAccessMode(SapAcqDevice device, string featureName)
        {
            if (device == null || string.IsNullOrWhiteSpace(featureName))
            {
                return "<unavailable>";
            }

            SapFeature feature = null;
            try
            {
                feature = new SapFeature(device.Location);
                feature.Create();
                if (device.GetFeatureInfo(featureName, feature))
                {
                    return feature.DataAccessMode.ToString();
                }
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
            finally
            {
                if (feature != null)
                {
                    try
                    {
                        if (feature.Initialized)
                        {
                            feature.Destroy();
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        feature.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            return "<unknown>";
        }

        private static bool TrySetNotebookNumericFeatureValue(SapAcqDevice device, string featureName, string value)
        {
            if (!CanWriteNotebookFeatureStrict(device, featureName))
            {
                return false;
            }

            if (TrySetNotebookFeatureValue(device, featureName, value))
            {
                return true;
            }

            int intValue;
            if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out intValue))
            {
                try
                {
                    if (device.SetFeatureValue(featureName, intValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            double doubleValue;
            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
            {
                try
                {
                    if (device.SetFeatureValue(featureName, doubleValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool CanWriteNotebookFeature(SapAcqDevice device, string featureName)
        {
            if (device == null || string.IsNullOrWhiteSpace(featureName))
            {
                return false;
            }

            try
            {
                if (!device.IsFeatureAvailable(featureName))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            SapFeature feature = null;
            try
            {
                feature = new SapFeature(device.Location);
                feature.Create();
                if (device.GetFeatureInfo(featureName, feature))
                {
                    var accessMode = feature.DataAccessMode.ToString();
                    if (accessMode.IndexOf("ReadOnly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        string.Equals(accessMode, "Read", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return true;
                }
            }
            catch
            {
            }
            finally
            {
                if (feature != null)
                {
                    try
                    {
                        if (feature.Initialized)
                        {
                            feature.Destroy();
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        feature.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            return true;
        }

        private static bool IsNotebookFeatureAvailable(SapAcqDevice device, string featureName)
        {
            try
            {
                return device.IsFeatureAvailable(featureName);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadNotebookFeatureValue(SapAcqDevice device, string featureName)
        {
            try
            {
                string stringValue;
                if (device.GetFeatureValue(featureName, out stringValue))
                {
                    return SafeString(stringValue);
                }
            }
            catch
            {
            }

            try
            {
                int intValue;
                if (device.GetFeatureValue(featureName, out intValue))
                {
                    return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            try
            {
                double doubleValue;
                if (device.GetFeatureValue(featureName, out doubleValue))
                {
                    return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            try
            {
                float floatValue;
                if (device.GetFeatureValue(featureName, out floatValue))
                {
                    return floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            return "<unreadable>";
        }

        private static void TryUpdateNotebookFeaturesToDevice(SapAcqDevice device)
        {
            try
            {
                device.UpdateFeaturesToDevice();
            }
            catch
            {
            }
        }

        private static string FormatApplyResult(bool applied, string value)
        {
            return applied ? "ok(" + value + ")" : "failed(" + value + ")";
        }

        private static string FormatNullableInt(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "<unavailable>";
        }

        private sealed class NotebookApplyResult
        {
            public bool Applied { get; set; }

            public string Message { get; set; }
        }

        private bool TrySetNumericFeature(decimal value, System.Collections.Generic.List<string> notes, params string[] featureNames)
        {
            var appliedFeature = string.Empty;
            if (TrySetDeviceFeature(featureNames, (double)value, out appliedFeature))
            {
                notes.Add(appliedFeature + " applied requested=" + value + " readback=" + ReadNumericFeatureValue(appliedFeature));
                return true;
            }

            notes.Add(featureNames[0] + " not supported");
            return false;
        }

        private void TryInitializeAcqDevice()
        {
            _deviceFeaturesAvailable = false;
            _acqDevicePathSummary = string.Empty;
            _acqDeviceProbeSummary = string.Empty;
            DisposeAcqDeviceOnly();

            foreach (var candidate in BuildCandidateAcqDeviceLocations())
            {
                AppendAcqDeviceProbe(candidate);
                var candidateTimer = System.Diagnostics.Stopwatch.StartNew();
                var isResourceNameMatched = IsAcqDeviceLocationAssociatedWithAcquisition(
                    _serverLocation == null ? null : _serverLocation.ServerName,
                    candidate);
                var isConfiguredLocation = IsConfiguredAcqDeviceLocation(candidate);
                SapAcqDevice createdDevice = null;
                if (isResourceNameMatched || !isConfiguredLocation)
                {
                    createdDevice = TryBuildAndCreateAcqDevice(candidate, isResourceNameMatched);
                }
                if (createdDevice == null && isConfiguredLocation)
                {
                    createdDevice = TryBuildAndCreateConfiguredAcqDevice(candidate);
                }
                AppLogger.Log(
                    "AcqDevice probe location=" + FormatSapLocation(candidate)
                    + " elapsedMs=" + candidateTimer.ElapsedMilliseconds
                    + " created=" + (createdDevice != null));
                if (createdDevice != null)
                {
                    _acqDevice = createdDevice;
                    _deviceFeaturesAvailable = true;
                    _acqDevicePathSummary = candidate.ServerName + "#" + candidate.ResourceIndex;
                    return;
                }
            }
        }

        private bool IsConfiguredAcqDeviceLocation(SapLocation location)
        {
            return location != null
                && !string.IsNullOrWhiteSpace(_settings.DeviceFeatureServerName)
                && _settings.DeviceFeatureResourceIndex >= 0
                && string.Equals(location.ServerName, _settings.DeviceFeatureServerName, StringComparison.OrdinalIgnoreCase)
                && location.ResourceIndex == _settings.DeviceFeatureResourceIndex;
        }

        private void EnsureAcqDeviceAvailable()
        {
            if (_deviceFeaturesAvailable)
            {
                return;
            }

            if (_acqDevice == null)
            {
                TryInitializeAcqDevice();
            }

            if (_deviceFeaturesAvailable)
            {
                return;
            }

            if (_acqDevice != null && !_acqDevice.Initialized)
            {
                if (!TryCreateAcqDevice())
                {
                    _acqDevice.Dispose();
                    _acqDevice = null;
                }
            }
        }

        private bool TryCreateAcqDevice()
        {
            if (_acqDevice == null)
            {
                return false;
            }

            try
            {
                _deviceFeaturesAvailable = _acqDevice.Create();
                return _deviceFeaturesAvailable;
            }
            catch
            {
                _deviceFeaturesAvailable = false;
                return false;
            }
        }

        private void DisposeAcqDeviceOnly()
        {
            if (_acqDevice != null)
            {
                try
                {
                    _acqDevice.Dispose();
                }
                catch
                {
                }

                _acqDevice = null;
            }
        }

        private IEnumerable<SapLocation> BuildCandidateAcqDeviceLocations()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selectedServerName = _serverLocation == null ? null : _serverLocation.ServerName;
            var hasConfiguredLocation = !string.IsNullOrWhiteSpace(_settings.DeviceFeatureServerName)
                && _settings.DeviceFeatureResourceIndex >= 0;
            var configuredLocation = hasConfiguredLocation
                ? new SapLocation(_settings.DeviceFeatureServerName, _settings.DeviceFeatureResourceIndex)
                : null;
            var configuredServerMatchesSelection = configuredLocation != null
                && IsConfiguredServerForSelectedAcquisition(selectedServerName, configuredLocation.ServerName);
            var allowLegacyGlobalFallback = CountAcquisitionServers() <= 1;

            if (configuredServerMatchesSelection)
            {
                var configuredKey = configuredLocation.ServerName + "|" + configuredLocation.ResourceIndex.ToString();
                if (seen.Add(configuredKey))
                {
                    yield return configuredLocation;
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedServerName))
            {
                foreach (var location in EnumerateDirectAcqDeviceLocations(selectedServerName))
                {
                    var key = location.ServerName + "|" + location.ResourceIndex.ToString();
                    if (seen.Add(key))
                    {
                        yield return location;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedServerName))
            {
                foreach (var location in EnumerateAcqDeviceLocationsForAcquisition(selectedServerName))
                {
                    var key = location.ServerName + "|" + location.ResourceIndex.ToString();
                    if (seen.Add(key))
                    {
                        yield return location;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedServerName))
            {
                for (var serverIndex = 0; serverIndex < SapManager.GetServerCount(); serverIndex++)
                {
                    var serverName = SapManager.GetServerName(serverIndex);
                    if (!IsRelatedServerName(selectedServerName, serverName)
                        || string.Equals(selectedServerName, serverName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var location in EnumerateDirectAcqDeviceLocations(serverName))
                    {
                        var key = location.ServerName + "|" + location.ResourceIndex.ToString();
                        if (seen.Add(key))
                        {
                            yield return location;
                        }
                    }
                }
            }

            if (configuredLocation != null && !configuredServerMatchesSelection && allowLegacyGlobalFallback)
            {
                var configuredKey = configuredLocation.ServerName + "|" + configuredLocation.ResourceIndex.ToString();
                if (seen.Add(configuredKey))
                {
                    yield return configuredLocation;
                }
            }
        }

        private static IEnumerable<SapLocation> EnumerateAcqDeviceLocationsForAcquisition(string acquisitionServerName)
        {
            if (string.IsNullOrWhiteSpace(acquisitionServerName))
            {
                yield break;
            }

            for (var serverIndex = 0; serverIndex < SapManager.GetServerCount(); serverIndex++)
            {
                var serverName = SapManager.GetServerName(serverIndex);
                var resourceCount = SapManager.GetResourceCount(serverName, SapManager.ResourceType.AcqDevice);
                for (var resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
                {
                    var resourceName = SapManager.GetResourceName(serverName, SapManager.ResourceType.AcqDevice, resourceIndex);
                    if (IsAcqDeviceResourceForAcquisition(acquisitionServerName, resourceName))
                    {
                        yield return new SapLocation(serverName, resourceIndex);
                    }
                }
            }
        }

        private static bool IsAcqDeviceResourceForAcquisition(string acquisitionServerName, string acqDeviceResourceName)
        {
            if (string.IsNullOrWhiteSpace(acquisitionServerName) || string.IsNullOrWhiteSpace(acqDeviceResourceName))
            {
                return false;
            }

            return string.Equals(acquisitionServerName, acqDeviceResourceName, StringComparison.OrdinalIgnoreCase)
                || acqDeviceResourceName.StartsWith(acquisitionServerName + "_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsConfiguredServerForSelectedAcquisition(string acquisitionServerName, string featureServerName)
        {
            if (IsRelatedServerName(acquisitionServerName, featureServerName))
            {
                return true;
            }

            int acquisitionInstance;
            int featureInstance;
            return TryGetServerInstanceNumber(acquisitionServerName, out acquisitionInstance)
                && TryGetServerInstanceNumber(featureServerName, out featureInstance)
                && acquisitionInstance == featureInstance;
        }

        private static bool TryGetServerInstanceNumber(string serverName, out int instanceNumber)
        {
            instanceNumber = -1;
            if (string.IsNullOrWhiteSpace(serverName))
            {
                return false;
            }

            var separatorIndex = serverName.LastIndexOf('_');
            return separatorIndex >= 0
                && separatorIndex < serverName.Length - 1
                && int.TryParse(
                    serverName.Substring(separatorIndex + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out instanceNumber);
        }

        private static int CountAcquisitionServers()
        {
            var count = 0;
            for (var serverIndex = 0; serverIndex < SapManager.GetServerCount(); serverIndex++)
            {
                if (SapManager.GetResourceCount(serverIndex, SapManager.ResourceType.Acq) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private void AppendAcqDeviceProbe(SapLocation location)
        {
            if (location == null)
            {
                return;
            }

            if (_acqDeviceProbeSummary.Length > 0)
            {
                _acqDeviceProbeSummary += ", ";
            }

            _acqDeviceProbeSummary += location.ServerName + "#" + location.ResourceIndex;
        }

        private string FormatAcqDeviceProbeSummary()
        {
            return string.IsNullOrWhiteSpace(_acqDeviceProbeSummary)
                ? " (tried: none)"
                : " (tried: " + _acqDeviceProbeSummary + ")";
        }

        private static IEnumerable<SapLocation> EnumerateDirectAcqDeviceLocations(string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
            {
                yield break;
            }

            var count = SapManager.GetResourceCount(serverName, SapManager.ResourceType.AcqDevice);
            for (var resourceIndex = 0; resourceIndex < count; resourceIndex++)
            {
                yield return new SapLocation(serverName, resourceIndex);
            }
        }

        private SapAcqDevice TryBuildAndCreateAcqDevice(SapLocation location)
        {
            return TryBuildAndCreateAcqDevice(location, false);
        }

        private SapAcqDevice TryBuildAndCreateAcqDevice(SapLocation location, bool preferNoConfig)
        {
            SapAcqDevice device;
            if (preferNoConfig)
            {
                device = TryBuildAndCreateAcqDeviceVariant(
                    location,
                    "no-config",
                    () => new SapAcqDevice(location));
                if (device != null)
                {
                    return device;
                }
            }

            device = TryBuildAndCreateAcqDeviceVariant(
                location,
                "config-file",
                () => new SapAcqDevice(location, _configFileName));
            if (device != null)
            {
                return device;
            }

            if (!preferNoConfig)
            {
                device = TryBuildAndCreateAcqDeviceVariant(
                    location,
                    "no-config",
                    () => new SapAcqDevice(location));
                if (device != null)
                {
                    return device;
                }
            }

            device = TryBuildAndCreateAcqDeviceVariant(
                location,
                "boolean-true",
                () => new SapAcqDevice(location, true));
            if (device != null)
            {
                return device;
            }

            return TryBuildAndCreateAcqDeviceVariant(
                location,
                "boolean-false",
                () => new SapAcqDevice(location, false));
        }

        private SapAcqDevice TryBuildAndCreateConfiguredAcqDevice(SapLocation location)
        {
            var deviceConfigFile = string.IsNullOrWhiteSpace(_settings.DeviceFeatureConfigFilePath)
                ? _configFileName
                : _settings.DeviceFeatureConfigFilePath;
            var device = TryBuildAndCreateAcqDeviceVariant(
                location,
                "configured-file",
                () => new SapAcqDevice(location, deviceConfigFile));
            if (device != null)
            {
                return device;
            }

            return TryBuildAndCreateAcqDevice(location);
        }

        private SapAcqDevice TryBuildAndCreateAcqDeviceVariant(
            SapLocation location,
            string variantName,
            Func<SapAcqDevice> factory)
        {
            SapAcqDevice device = null;
            var constructorError = string.Empty;
            var constructorTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                device = factory();
            }
            catch (Exception exception)
            {
                constructorError = exception.GetType().Name;
            }

            var constructorMs = constructorTimer.ElapsedMilliseconds;
            var initialized = false;
            var created = false;
            var createError = string.Empty;
            long? createMs = null;
            if (device != null)
            {
                var createTimer = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    initialized = device.Initialized;
                    created = initialized || device.Create();
                }
                catch (Exception exception)
                {
                    createError = exception.GetType().Name;
                }
                createMs = createTimer.ElapsedMilliseconds;
            }

            AppLogger.Log(
                "AcqDevice variant location=" + FormatSapLocation(location)
                + " name=" + variantName
                + " constructorMs=" + constructorMs
                + " createMs=" + (createMs.HasValue ? createMs.Value.ToString() : "not-run")
                + " initialized=" + initialized
                + " created=" + created
                + " constructorError=" + (string.IsNullOrEmpty(constructorError) ? "none" : constructorError)
                + " createError=" + (string.IsNullOrEmpty(createError) ? "none" : createError));

            if (created)
            {
                return device;
            }

            if (device != null)
            {
                try
                {
                    device.Dispose();
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool IsRelatedServerName(string primaryServerName, string candidateServerName)
        {
            if (string.IsNullOrWhiteSpace(primaryServerName) || string.IsNullOrWhiteSpace(candidateServerName))
            {
                return false;
            }

            if (string.Equals(primaryServerName, candidateServerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedPrimary = NormalizeServerName(primaryServerName);
            var normalizedCandidate = NormalizeServerName(candidateServerName);
            return string.Equals(normalizedPrimary, normalizedCandidate, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeServerName(string serverName)
        {
            var trimmed = serverName.Trim();
            if (trimmed.Length > 2 && char.IsWhiteSpace(trimmed[trimmed.Length - 2]) && char.IsDigit(trimmed[trimmed.Length - 1]))
            {
                return trimmed.Substring(0, trimmed.Length - 2).TrimEnd();
            }

            return trimmed;
        }

        private bool TrySetIntegralFeature(int value, System.Collections.Generic.List<string> notes, params string[] featureNames)
        {
            var appliedFeature = string.Empty;
            if (TrySetDeviceFeature(featureNames, value, out appliedFeature))
            {
                notes.Add(appliedFeature + " applied requested=" + value + " readback=" + ReadNumericFeatureValue(appliedFeature));
                return true;
            }

            notes.Add(featureNames[0] + " not supported");
            return false;
        }

        private bool ApplyTriggerMode(System.Collections.Generic.List<string> notes)
        {
            switch (_settings.TriggerMode)
            {
                case TriggerMode.Continuous:
                    if (TryConfigureTriggerSelector("FrameStart", false, null) |
                        TryConfigureTriggerSelector("LineStart", false, null) |
                        TrySetDeviceFeature(new[] { "TriggerMode" }, "Off"))
                    {
                        notes.Add("TriggerMode continuous applied");
                        return true;
                    }

                    notes.Add("TriggerMode continuous not supported");
                    return false;

                case TriggerMode.ExternalTrigger:
                case TriggerMode.SoftwareTrigger:
                    if (TryConfigureTriggerSelector("LineStart", true, "Line1") |
                        TryConfigureTriggerSelector("LineTrigger", true, "Line1") |
                        TryConfigureTriggerSelector("AcquisitionLine", true, "Line1") |
                        TryConfigureTriggerSelector("LineStart", true, "Input1") |
                        TryConfigureTriggerSelector("LineStart", true, "CC1"))
                    {
                        notes.Add("TriggerMode external line applied for " + _settings.TriggerMode + " mode");
                        return true;
                    }

                    notes.Add("TriggerMode external line not supported for " + _settings.TriggerMode + " mode");
                    return false;

                case TriggerMode.SingleFrame:
                    if (TryConfigureTriggerSelector("FrameStart", true, "Software") |
                        TryConfigureTriggerSelector("AcquisitionStart", true, "Software"))
                    {
                        notes.Add("TriggerMode single-frame applied");
                        return true;
                    }

                    notes.Add("TriggerMode single-frame not supported");
                    return false;

                default:
                    return false;
            }
        }

        private bool TrySetInternalLineRate(System.Collections.Generic.List<string> notes)
        {
            var acquisitionRate = decimal.ToInt32(decimal.Truncate(_settings.InternalLineRate));
            if (acquisitionRate <= 0)
            {
                notes.Add("InternalLineRate skipped: requested value must be greater than 0");
                return false;
            }

            var applied = false;

            var disabledLineIntegrate = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE, 0);
            notes.Add("LINE_INTEGRATE_ENABLE disabled before internal line trigger " + FormatApplyResult(disabledLineIntegrate, "0") + " readback=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE));
            TryEnableLineTriggerWhenSupported(notes);

            if (IsAcquisitionParameterAvailable(SapAcquisition.Prm.INT_LINE_TRIGGER_ENABLE) &&
                IsAcquisitionParameterAvailable(SapAcquisition.Prm.INT_LINE_TRIGGER_FREQ))
            {
                var minimumRate = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.INT_LINE_TRIGGER_FREQ_MIN);
                var maximumRate = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.INT_LINE_TRIGGER_FREQ_MAX);
                var cameraMinimumRate = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.CAM_LINE_TRIGGER_FREQ_MIN);
                var cameraMaximumRate = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.CAM_LINE_TRIGGER_FREQ_MAX);
                var requestedRate = ClampInternalLineRate(acquisitionRate, minimumRate, maximumRate, cameraMinimumRate, cameraMaximumRate);

                notes.Add(
                    "InternalLineRate range requested=" + acquisitionRate
                    + " appliedRequest=" + requestedRate
                    + " intMin=" + FormatNullableInt(minimumRate)
                    + " intMax=" + FormatNullableInt(maximumRate)
                    + " camMin=" + FormatNullableInt(cameraMinimumRate)
                    + " camMax=" + FormatNullableInt(cameraMaximumRate));

                var disabledExternalLine = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.EXT_LINE_TRIGGER_ENABLE, 0);
                var disabledShaftEncoder = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.SHAFT_ENCODER_ENABLE, 0);
                var disabledExternalFrame = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE, 0);
                var disabledInternalFrame = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.INT_FRAME_TRIGGER_ENABLE, 0);
                var enabledInternalLine = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.INT_LINE_TRIGGER_ENABLE, 1);
                var frequencyApplied = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.INT_LINE_TRIGGER_FREQ, requestedRate);

                notes.Add(
                    "InternalLineRate acquisition "
                    + "extLineOff=" + FormatApplyResult(disabledExternalLine, "0")
                    + " shaftEncoderOff=" + FormatApplyResult(disabledShaftEncoder, "0")
                    + " extFrameOff=" + FormatApplyResult(disabledExternalFrame, "0")
                    + " intFrameOff=" + FormatApplyResult(disabledInternalFrame, "0")
                    + " intLineEnableWrite=" + FormatApplyResult(enabledInternalLine, "1")
                    + " intLineEnable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.INT_LINE_TRIGGER_ENABLE)
                    + " freqWrite=" + FormatApplyResult(frequencyApplied, requestedRate.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    + " freq=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.INT_LINE_TRIGGER_FREQ));

                if (enabledInternalLine && frequencyApplied)
                {
                    applied = true;
                }
            }
            else
            {
                notes.Add("InternalLineRate acquisition parameters unavailable");
            }

            if (applied)
            {
                applied = true;
            }

            if (applied)
            {
                return true;
            }

            notes.Add("InternalLineRate not supported");
            return false;
        }

        private bool TryEnableLineTriggerWhenSupported(System.Collections.Generic.List<string> notes)
        {
            var method = ReadAcquisitionIntParameterValue(SapAcquisition.Prm.LINE_TRIGGER_METHOD).GetValueOrDefault();
            if (method <= 0)
            {
                var supportedMethod = ReadFirstSupportedLineTriggerMethod();
                if (supportedMethod > 0)
                {
                    if (TrySetAcquisitionIntParameter(notes, supportedMethod, SapAcquisition.Prm.LINE_TRIGGER_METHOD))
                    {
                        method = supportedMethod;
                    }
                }
            }

            if (method <= 0)
            {
                notes.Add("LINE_TRIGGER_ENABLE skipped: no supported LINE_TRIGGER_METHOD was available");
                return false;
            }

            return TrySetAcquisitionIntParameter(notes, 1, SapAcquisition.Prm.LINE_TRIGGER_ENABLE);
        }

        private int ReadFirstSupportedLineTriggerMethod()
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return 0;
            }

            try
            {
                int capability;
                if (!_acquisition.GetCapability(SapAcquisition.Cap.LINE_TRIGGER_METHOD, out capability))
                {
                    return 0;
                }

                for (var bit = 1; bit != 0 && bit > 0; bit <<= 1)
                {
                    if ((capability & bit) != 0)
                    {
                        return bit;
                    }
                }
            }
            catch
            {
            }

            return 0;
        }

        private int ClampInternalLineRate(int requestedRate, int? minimumRate, int? maximumRate, int? cameraMinimumRate, int? cameraMaximumRate)
        {
            var clampedRate = requestedRate;
            if (minimumRate.HasValue && clampedRate < minimumRate.Value)
            {
                clampedRate = minimumRate.Value;
            }

            if (cameraMinimumRate.HasValue && clampedRate < cameraMinimumRate.Value)
            {
                clampedRate = cameraMinimumRate.Value;
            }

            if (maximumRate.HasValue && clampedRate > maximumRate.Value)
            {
                clampedRate = maximumRate.Value;
            }

            if (cameraMaximumRate.HasValue && clampedRate > cameraMaximumRate.Value)
            {
                clampedRate = cameraMaximumRate.Value;
            }

            return clampedRate;
        }

        private bool TrySetExposureParameters(System.Collections.Generic.List<string> notes)
        {
            var requestedExposureValue = decimal.ToInt32(decimal.Truncate(_settings.ExposureTime));
            if (_settings.TriggerMode == TriggerMode.Continuous)
            {
                notes.Add(
                    "LineIntegrate exposure skipped for continuous mode "
                    + "requested=" + requestedExposureValue
                    + " note=free-run keeps exposure on camera-side features/CCF and does not write line integration parameters");
                return false;
            }

            if (_settings.TriggerMode == TriggerMode.ExternalTrigger ||
                _settings.TriggerMode == TriggerMode.SoftwareTrigger)
            {
                notes.Add(
                    "LineIntegrate exposure skipped for external-line trigger mode; official line-integration method 3 keeps duration=40 "
                    + "enable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE)
                    + " method=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_METHOD)
                    + " requested=" + requestedExposureValue
                    + " duration=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_DURATION));
                return false;
            }

            if (_settings.InternalLineRate > 0)
            {
                var disabledLineIntegrate = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE, 0);
                notes.Add(
                    "LineIntegrate exposure skipped for internal line rate "
                    + "disableWrite=" + FormatApplyResult(disabledLineIntegrate, "0")
                    + " enable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE)
                    + " method=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_METHOD)
                    + " requested=" + requestedExposureValue
                    + " duration=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_DURATION)
                    + " note=LINE_INTEGRATE_ENABLE is mutually exclusive with LINE_TRIGGER_ENABLE; camera-side ExposureTime remains the exposure path");
                return disabledLineIntegrate;
            }

            var methodApplied = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_METHOD, 1);
            var enableApplied = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE, 1);
            var durationApplied = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_DURATION, requestedExposureValue);

            notes.Add(
                "LineIntegrate exposure "
                + "methodWrite=" + FormatApplyResult(methodApplied, "1")
                + "enableWrite=" + FormatApplyResult(enableApplied, "1")
                + "enable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE)
                + " method=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_METHOD)
                + " requested=" + requestedExposureValue
                + " duration=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_DURATION)
                + " note=method 1 is requested before enable/duration");

            if (methodApplied || enableApplied || durationApplied)
            {
                return true;
            }

            notes.Add("LINE_INTEGRATE_METHOD/ENABLE/DURATION not supported or locked");
            return false;
        }

        private bool TrySetLengthParameters(System.Collections.Generic.List<string> notes)
        {
            if (TrySetAcquisitionIntParameter(
                notes,
                _settings.Length,
                SapAcquisition.Prm.CROP_HEIGHT))
            {
                return true;
            }

            notes.Add("Acquisition length parameter not supported");
            return false;
        }

        private bool TryApplyAcquisitionTriggerMode(System.Collections.Generic.List<string> notes)
        {
            switch (_settings.TriggerMode)
            {
                case TriggerMode.Continuous:
                    return TryApplyFreerunLineSyncSource(notes);

                case TriggerMode.SoftwareTrigger:
                case TriggerMode.ExternalTrigger:
                    if (TryApplyExternalLineTrigger(notes))
                    {
                        notes.Add("Acquisition trigger external applied for " + _settings.TriggerMode + " mode");
                        return true;
                    }
                    break;

                case TriggerMode.SingleFrame:
                    if (TrySetAcquisitionBoolPattern(
                        notes,
                        new[]
                        {
                            new ParameterWrite(SapAcquisition.Prm.EXT_TRIGGER_ENABLE, 0),
                            new ParameterWrite(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE, 0),
                            new ParameterWrite(SapAcquisition.Prm.EXT_LINE_TRIGGER_ENABLE, 0),
                            new ParameterWrite(SapAcquisition.Prm.INT_LINE_TRIGGER_ENABLE, 0),
                            new ParameterWrite(SapAcquisition.Prm.INT_FRAME_TRIGGER_ENABLE, 1),
                            new ParameterWrite(SapAcquisition.Prm.CAM_TRIGGER_ENABLE, 1)
                        }))
                    {
                        notes.Add("Acquisition trigger single-frame applied");
                        return true;
                    }
                    break;
            }

            notes.Add("Acquisition trigger parameter not supported");
            return false;
        }

        private bool TryApplyFreerunLineSyncSource(System.Collections.Generic.List<string> notes)
        {
            notes.Add("FreeRun line sync source set to None");
            return true;
        }

        private bool TrySetExternalFrameTriggerOneFrame(System.Collections.Generic.List<string> notes)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            var allowExternalFrameTrigger = _settings.TriggerMode == TriggerMode.Continuous
                || _settings.TriggerMode == TriggerMode.ExternalTrigger
                || _settings.TriggerMode == TriggerMode.SoftwareTrigger;

            var requestedValue = allowExternalFrameTrigger && _settings.ExternalFrameTriggerOneFrame ? 1 : 0;
            var applied = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE, requestedValue);
            notes.Add(
                "EXT_FRAME_TRIGGER_ENABLE "
                + (requestedValue == 1 ? "enabled" : "disabled")
                + " for " + _settings.TriggerMode + " mode "
                + FormatApplyResult(applied, requestedValue.ToString(System.Globalization.CultureInfo.InvariantCulture))
                + " readback=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE));
            return applied;
        }

        private bool TryApplyExternalLineTrigger(System.Collections.Generic.List<string> notes)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            var disabledInternalLine = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.INT_LINE_TRIGGER_ENABLE, 0);
            var disabledInternalFrame = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.INT_FRAME_TRIGGER_ENABLE, 0);
            var disabledShaftEncoder = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.SHAFT_ENCODER_ENABLE, 0);
            var disabledCameraTrigger = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.CAM_TRIGGER_ENABLE, 0);
            var disabledExternalTrigger = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.EXT_TRIGGER_ENABLE, 0);
            var disabledLineTrigger = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_TRIGGER_ENABLE, 0);
            var lineIntegrateMethodApplied = TrySetAcquisitionValParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_METHOD, SapAcquisition.Val.LINE_INTEGRATE_METHOD_3);
            var lineIntegrateDurationApplied = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_DURATION, 40);
            var pulse0HighApplied = TrySetAcquisitionValParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_PULSE0_POLARITY, SapAcquisition.Val.ACTIVE_HIGH);
            var pulse1LowApplied = TrySetAcquisitionValParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_PULSE1_POLARITY, SapAcquisition.Val.ACTIVE_LOW);
            var cc1Pulse1Applied = TrySetCc1ToPulse1();
            var lineIntegrateEnabled = TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE, 1);
            var externalFrameTriggerEnabled = TrySetAcquisitionIntParameterQuiet(
                SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE,
                _settings.ExternalFrameTriggerOneFrame ? 1 : 0);
            var enabledExternalLine = TrySetAcquisitionIntParameter(notes, 1, SapAcquisition.Prm.EXT_LINE_TRIGGER_ENABLE);

            notes.Add(
                "ExternalLineTrigger board "
                + "mode=one-pulse-one-line "
                + "targetLength=" + _settings.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " "
                + "intLineOff=" + FormatApplyResult(disabledInternalLine, "0")
                + " intFrameOff=" + FormatApplyResult(disabledInternalFrame, "0")
                + " shaftEncoderOff=" + FormatApplyResult(disabledShaftEncoder, "0")
                + " camTriggerOff=" + FormatApplyResult(disabledCameraTrigger, "0")
                + " extTriggerOff=" + FormatApplyResult(disabledExternalTrigger, "0")
                + " lineTriggerOff=" + FormatApplyResult(disabledLineTrigger, "0")
                + " lineIntegrateMethod3=" + FormatApplyResult(lineIntegrateMethodApplied, "LINE_INTEGRATE_METHOD_3")
                + " lineIntegrateDuration40=" + FormatApplyResult(lineIntegrateDurationApplied, "40")
                + " pulse0High=" + FormatApplyResult(pulse0HighApplied, "ACTIVE_HIGH")
                + " pulse1Low=" + FormatApplyResult(pulse1LowApplied, "ACTIVE_LOW")
                + " cc1Pulse1=" + FormatApplyResult(cc1Pulse1Applied, "SIGNAL_NAME_PULSE1")
                + " lineIntegrateOn=" + FormatApplyResult(lineIntegrateEnabled, "1")
                + " extFrameEnableWrite=" + FormatApplyResult(externalFrameTriggerEnabled, _settings.ExternalFrameTriggerOneFrame ? "1" : "0")
                + " sourceWrite=skipped"
                + " detectionWrite=skipped"
                + " camTrigger=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.CAM_TRIGGER_ENABLE)
                + " lineTrigger=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_TRIGGER_ENABLE)
                + " lineTriggerMethod=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_TRIGGER_METHOD)
                + " lineIntegrate=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_ENABLE)
                + " extFrameEnable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE)
                + " lineIntegrateMethod=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_METHOD)
                + " lineIntegrateDuration=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_DURATION)
                + " pulse0Polarity=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_PULSE0_POLARITY)
                + " pulse1Polarity=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.LINE_INTEGRATE_PULSE1_POLARITY)
                + " cc1Control=" + ReadCc1Control()
                + " extLineEnable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.EXT_LINE_TRIGGER_ENABLE)
                + " extLineSource=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.EXT_LINE_TRIGGER_SOURCE)
                + " extLineDetection=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.EXT_LINE_TRIGGER_DETECTION)
                + " shaftEncoder=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.SHAFT_ENCODER_ENABLE)
                + " extFrameEnable=" + ReadAcquisitionIntParameter(SapAcquisition.Prm.EXT_FRAME_TRIGGER_ENABLE));

            return enabledExternalLine;
        }

        private bool TrySetCc1ToPulse1()
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            try
            {
                var controls = _acquisition.CamIoControl;
                if (controls == null || controls.Length == 0 || controls[0] == null)
                {
                    return false;
                }

                controls[0].Value = Convert.ToInt32(SapAcquisition.Val.SIGNAL_NAME_PULSE1);
                _acquisition.CamIoControl = controls;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string ReadCc1Control()
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return "<unavailable>";
            }

            try
            {
                var controls = _acquisition.CamIoControl;
                if (controls == null || controls.Length == 0 || controls[0] == null)
                {
                    return "<unavailable>";
                }

                return controls[0].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
        }

        private bool TrySetAcquisitionValParameterQuiet(SapAcquisition.Prm parameter, SapAcquisition.Val value)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            if (!IsAcquisitionParameterAvailable(parameter))
            {
                return false;
            }

            try
            {
                return _acquisition.SetParameter(parameter, value, true);
            }
            catch
            {
                return false;
            }
        }

        private bool TrySetAcquisitionSourceZero(System.Collections.Generic.List<string> notes, SapAcquisition.Prm parameter, SapAcquisition.Cap capability)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            try
            {
                int sourceCount;
                if (_acquisition.GetCapability(capability, out sourceCount) && sourceCount > 0)
                {
                    return TrySetAcquisitionIntParameter(notes, 0, parameter);
                }
            }
            catch
            {
            }

            return false;
        }

        private bool TrySetAcquisitionFirstCapabilityBit(System.Collections.Generic.List<string> notes, SapAcquisition.Prm parameter, SapAcquisition.Cap capability)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            try
            {
                int capabilityBits;
                if (!_acquisition.GetCapability(capability, out capabilityBits))
                {
                    return false;
                }

                for (var bit = 1; bit != 0 && bit > 0; bit <<= 1)
                {
                    if ((capabilityBits & bit) != 0)
                    {
                        return TrySetAcquisitionIntParameter(notes, bit, parameter);
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private bool TryConfigureTriggerSelector(string selector, bool enabled, string source)
        {
            var selectorApplied = TrySetDeviceFeature(new[] { "TriggerSelector" }, selector);
            var modeApplied = TrySetDeviceFeature(new[] { "TriggerMode" }, enabled ? "On" : "Off");
            var sourceApplied = !enabled || string.IsNullOrWhiteSpace(source) || TrySetDeviceFeature(new[] { "TriggerSource" }, source);
            return selectorApplied && modeApplied && sourceApplied;
        }

        private bool TrySetDeviceFeature(string[] featureNames, double value, out string appliedFeature)
        {
            appliedFeature = string.Empty;
            if (!_deviceFeaturesAvailable || _acqDevice == null || !_acqDevice.Initialized)
            {
                return false;
            }

            foreach (var featureName in featureNames)
            {
                if (!_acqDevice.IsFeatureAvailable(featureName))
                {
                    continue;
                }

                if (TrySetFeatureValue(featureName, value))
                {
                    appliedFeature = featureName;
                    return true;
                }
            }

            return false;
        }

        private bool TrySetDeviceFeature(string[] featureNames, string value, out string appliedFeature)
        {
            appliedFeature = string.Empty;
            if (!_deviceFeaturesAvailable || _acqDevice == null || !_acqDevice.Initialized)
            {
                return false;
            }

            foreach (var featureName in featureNames)
            {
                if (!CanWriteDeviceFeature(featureName))
                {
                    continue;
                }

                if (TrySetFeatureValue(featureName, value))
                {
                    appliedFeature = featureName;
                    return true;
                }
            }

            return false;
        }

        private bool TrySetAcquisitionIntParameter(System.Collections.Generic.List<string> notes, int value, params SapAcquisition.Prm[] parameters)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            var wasGrabbing = _transfer != null && _transfer.Initialized && _status.IsPreviewing;
            try
            {
                if (wasGrabbing)
                {
                    try
                    {
                        _transfer.Freeze();
                    }
                    catch
                    {
                    }
                }

                foreach (var parameter in parameters)
                {
                    if (!IsAcquisitionParameterAvailable(parameter))
                    {
                        continue;
                    }

                    try
                    {
                        if (_acquisition.SetParameter(parameter, value, true))
                        {
                            notes.Add(parameter + " applied requested=" + value + " readback=" + ReadAcquisitionIntParameter(parameter));
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }

                return false;
            }
            finally
            {
                if (wasGrabbing)
                {
                    try
                    {
                        _transfer.Grab();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private bool TrySetAcquisitionIntParameterQuiet(SapAcquisition.Prm parameter, int value)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            if (!IsAcquisitionParameterAvailable(parameter))
            {
                return false;
            }

            try
            {
                return _acquisition.SetParameter(parameter, value, true);
            }
            catch
            {
                return false;
            }
        }

        private bool TrySetAcquisitionBoolPattern(System.Collections.Generic.List<string> notes, ParameterWrite[] writes)
        {
            var applied = false;
            foreach (var write in writes)
            {
                if (TrySetAcquisitionIntParameter(notes, write.Value, write.Parameter))
                {
                    applied = true;
                }
            }

            return applied;
        }

        private bool TrySetDeviceFeature(string[] featureNames, string value)
        {
            if (!_deviceFeaturesAvailable || _acqDevice == null || !_acqDevice.Initialized)
            {
                return false;
            }

            foreach (var featureName in featureNames)
            {
                if (!CanWriteDeviceFeature(featureName))
                {
                    continue;
                }

                if (TrySetFeatureValue(featureName, value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TrySetFeatureValue(string featureName, double value)
        {
            try
            {
                if (_acqDevice.SetFeatureValue(featureName, value))
                {
                    _acqDevice.UpdateFeaturesToDevice();
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                if (_acqDevice.SetFeatureValue(featureName, (float)value))
                {
                    _acqDevice.UpdateFeaturesToDevice();
                    return true;
                }
            }
            catch
            {
            }

            if (Math.Abs(value - Math.Round(value)) < 0.000001)
            {
                var integralValue = Convert.ToInt64(Math.Round(value));
                if (integralValue >= int.MinValue && integralValue <= int.MaxValue)
                {
                    try
                    {
                        if (_acqDevice.SetFeatureValue(featureName, Convert.ToInt32(integralValue)))
                        {
                            _acqDevice.UpdateFeaturesToDevice();
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private bool TrySetFeatureValue(string featureName, string value)
        {
            try
            {
                if (_acqDevice.SetFeatureValue(featureName, value))
                {
                    _acqDevice.UpdateFeaturesToDevice();
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool CanWriteDeviceFeature(string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName) || !_acqDevice.IsFeatureAvailable(featureName))
            {
                return false;
            }

            SapFeature feature = null;
            try
            {
                feature = new SapFeature(_acqDevice.Location);
                feature.Create();
                if (_acqDevice.GetFeatureInfo(featureName, feature))
                {
                    var accessMode = feature.DataAccessMode.ToString();
                    if (accessMode.IndexOf("Write", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }

                    return true;
                }
            }
            catch
            {
            }
            finally
            {
                if (feature != null)
                {
                    try
                    {
                        if (feature.Initialized)
                        {
                            feature.Destroy();
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        feature.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private string ReadNumericFeatureValue(string featureName)
        {
            if (_acqDevice == null || !_acqDevice.Initialized || string.IsNullOrWhiteSpace(featureName))
            {
                return "<unavailable>";
            }

            try
            {
                double doubleValue;
                if (_acqDevice.GetFeatureValue(featureName, out doubleValue))
                {
                    return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            try
            {
                float floatValue;
                if (_acqDevice.GetFeatureValue(featureName, out floatValue))
                {
                    return floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            try
            {
                long longValue;
                if (_acqDevice.GetFeatureValue(featureName, out longValue))
                {
                    return longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            try
            {
                int intValue;
                if (_acqDevice.GetFeatureValue(featureName, out intValue))
                {
                    return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            return "<unreadable>";
        }

        private string ReadAcquisitionIntParameter(SapAcquisition.Prm parameter)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return "<unavailable>";
            }

            try
            {
                int intValue;
                if (_acquisition.GetParameter(parameter, out intValue))
                {
                    return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            return "<unreadable>";
        }

        private bool IsAcquisitionParameterAvailable(SapAcquisition.Prm parameter)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return false;
            }

            try
            {
                return _acquisition.IsParameterAvailable(parameter);
            }
            catch
            {
                return false;
            }
        }

        private int? ReadAcquisitionIntParameterValue(SapAcquisition.Prm parameter)
        {
            if (_acquisition == null || !_acquisition.Initialized)
            {
                return null;
            }

            try
            {
                int intValue;
                if (_acquisition.GetParameter(parameter, out intValue))
                {
                    return intValue;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string SafeGetAcquisitionParameterType(SapAcquisition.Prm parameter)
        {
            try
            {
                return SapAcquisition.GetParameterType(parameter).ToString();
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
        }

        private static bool IsLineScanCandidateFeature(string name, SapFeature feature)
        {
            var searchable = (
                (name ?? string.Empty)
                + " "
                + (feature.DisplayName ?? string.Empty)
                + " "
                + (feature.Category ?? string.Empty)
                + " "
                + (feature.Description ?? string.Empty)).ToLowerInvariant();

            return searchable.Contains("exposure")
                || searchable.Contains("integration")
                || searchable.Contains("integrate")
                || searchable.Contains("shutter")
                || searchable.Contains("strobe")
                || searchable.Contains("pulse")
                || searchable.Contains("width")
                || searchable.Contains("duration")
                || searchable.Contains("line")
                || searchable.Contains("rate")
                || searchable.Contains("gain")
                || searchable.Contains("trigger");
        }

        private string ReadFeatureValue(string featureName, SapFeature.Type dataType)
        {
            try
            {
                switch (dataType)
                {
                    case SapFeature.Type.Bool:
                        bool boolValue;
                        return _acqDevice.GetFeatureValue(featureName, out boolValue) ? boolValue.ToString() : "<unreadable>";
                    case SapFeature.Type.Int32:
                        int int32Value;
                        return _acqDevice.GetFeatureValue(featureName, out int32Value) ? int32Value.ToString() : "<unreadable>";
                    case SapFeature.Type.Int64:
                        long intValue;
                        return _acqDevice.GetFeatureValue(featureName, out intValue) ? intValue.ToString() : "<unreadable>";
                    case SapFeature.Type.Float:
                        float floatValue;
                        return _acqDevice.GetFeatureValue(featureName, out floatValue) ? floatValue.ToString() : "<unreadable>";
                    case SapFeature.Type.Double:
                        double doubleValue;
                        return _acqDevice.GetFeatureValue(featureName, out doubleValue) ? doubleValue.ToString() : "<unreadable>";
                    case SapFeature.Type.String:
                    case SapFeature.Type.Enum:
                        string textValue;
                        return _acqDevice.GetFeatureValue(featureName, out textValue) ? SafeString(textValue) : "<unreadable>";
                    default:
                        string fallbackValue;
                        return _acqDevice.GetFeatureValue(featureName, out fallbackValue) ? SafeString(fallbackValue) : "<unsupported>";
                }
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
        }

        private static string SafeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("\r", " ").Replace("\n", " ");
        }

        private string ReadAcquisitionParameterValue(SapAcquisition.Prm parameter)
        {
            try
            {
                var parameterType = SapAcquisition.GetParameterType(parameter);
                switch (parameterType.ToString())
                {
                    case "Int32":
                    case "RangeInt32":
                    case "Index":
                    case "Enum":
                        int intValue;
                        return _acquisition.GetParameter(parameter, out intValue) ? intValue.ToString() : "<unreadable>";
                    case "Int64":
                    case "RangeInt64":
                        long longValue;
                        return _acquisition.GetParameter(parameter, out longValue) ? longValue.ToString() : "<unreadable>";
                    case "String":
                        string stringValue;
                        return _acquisition.GetParameter(parameter, out stringValue) ? SafeString(stringValue) : "<unreadable>";
                    default:
                        return "<unsupported type: " + parameterType + ">";
                }
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
        }

        private struct ParameterWrite
        {
            public ParameterWrite(SapAcquisition.Prm parameter, int value)
            {
                Parameter = parameter;
                Value = value;
            }

            public SapAcquisition.Prm Parameter { get; private set; }

            public int Value { get; private set; }
        }
    }
}
