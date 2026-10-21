using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CameraCaptureApp.Models;
using CameraCaptureApp.Native;
using CameraCaptureApp.Services;

namespace CameraCaptureApp.Forms
{
    public partial class MainForm : Form, ICameraImageSource
    {
        private const int MaxConcurrentSnapshotSaves = 5;
        private readonly ICameraService _cameraService;
        private readonly ISettingsService _settingsService;
        private readonly Controls.CameraDisplayControl _cameraDisplayControl;
        private readonly FrameRecorder _frameRecorder;
        private readonly System.Windows.Forms.Timer _statusRefreshTimer;
        private readonly System.Windows.Forms.Timer _meterWheelAutoConnectTimer;
        private readonly Lsi8181MeterWheelService _meterWheelService;
        private CameraSettings _settings;
        private CancellationTokenSource _imageLoadTokenSource;
        private CancellationTokenSource _previewFrameTokenSource;
        private readonly object _pendingPreviewFramesLock = new object();
        private readonly Queue<Bitmap> _pendingRollingPreviewFrames = new Queue<Bitmap>();
        private readonly SemaphoreSlim _snapshotSaveGate = new SemaphoreSlim(MaxConcurrentSnapshotSaves, MaxConcurrentSnapshotSaves);
        private readonly object _snapshotProgressLock = new object();
        private bool _autoConnectAttempted;
        private int _pendingSnapshotSaveCount;
        private int _snapshotSaveQueueCount;
        private int _snapshotSaveActiveCount;
        private int _snapshotSaveCompletedCount;
        private int _snapshotSaveFailedCount;
        private SaveProgressForm _snapshotProgressForm;
        private MeterWheelControlForm _meterWheelControlForm;
        private Bitmap _pendingPreviewFrame;
        private int _previewFrameUiUpdateQueued;
        private bool _isClosing;
        private readonly object _softwareTriggerMonitorLock = new object();
        private CancellationTokenSource _softwareTriggerMonitorTokenSource;
        private Task _softwareTriggerMonitorTask;
        private int _softwareTriggerCaptureQueued;
        private EventHandler<CameraImageAcquiredEventArgs> _imageAcquiredHandlers;

        public event EventHandler ImageProcessingEnabledChanged;

        public bool IsImageProcessingEnabled
        {
            get { return _settings != null && _settings.ForwardAcquiredImageToInspection; }
        }

        public bool IsCameraConnected
        {
            get
            {
                CameraStatus status = _cameraService == null ? null : _cameraService.Status;
                return status != null && status.IsConnected;
            }
        }

        public bool IsCameraPreviewing
        {
            get
            {
                CameraStatus status = _cameraService == null ? null : _cameraService.Status;
                return status != null && status.IsPreviewing;
            }
        }

        public bool IsSoftwareTriggerMode
        {
            get { return _settings != null && _settings.TriggerMode == TriggerMode.SoftwareTrigger; }
        }

        public bool ConnectCameraFromHost(out string message)
        {
            if (IsCameraConnected)
            {
                message = _cameraService.Status.LastMessage;
                return true;
            }

            bool connected = ConnectCameraCore();
            UpdateStatus();
            message = _cameraService.Status == null
                ? "Camera status is unavailable."
                : _cameraService.Status.LastMessage;
            return connected;
        }

        public bool StartPreviewFromHost(out string message)
        {
            if (!IsCameraConnected)
            {
                message = "Connect the camera before starting preview.";
                return false;
            }

            if (IsCameraPreviewing)
            {
                message = _cameraService.Status.LastMessage;
                return true;
            }

            bool started = StartPreviewCore();
            message = _cameraService.Status == null
                ? "Camera status is unavailable."
                : _cameraService.Status.LastMessage;
            return started;
        }

        public void StopPreviewFromHost()
        {
            StopPreviewCore();
        }

        public void DisconnectCameraFromHost()
        {
            DisconnectCameraCore();
        }

        public event EventHandler<CameraImageAcquiredEventArgs> ImageAcquired
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (_imageAcquiredHandlers == null)
                {
                    _cameraService.ImageAcquired += CameraService_ImageAcquired;
                }
                _imageAcquiredHandlers += value;
            }
            remove
            {
                if (value == null)
                {
                    return;
                }

                _imageAcquiredHandlers -= value;
                if (_imageAcquiredHandlers == null)
                {
                    _cameraService.ImageAcquired -= CameraService_ImageAcquired;
                }
            }
        }

        public MainForm(ICameraService cameraService, ISettingsService settingsService)
        {
            _cameraService = cameraService;
            _settingsService = settingsService;
            _settings = _settingsService.Load() ?? CameraSettings.CreateDefault();
            _meterWheelService = new Lsi8181MeterWheelService();

            InitializeComponent();

            _frameRecorder = new FrameRecorder();
            _cameraDisplayControl = new Controls.CameraDisplayControl();
            _cameraDisplayControl.Dock = DockStyle.Fill;
            _cameraDisplayControl.SaveSnapshotRequested += CameraDisplayControl_SaveSnapshotRequested;
            _cameraDisplayControl.GrayWaveformRequested += CameraDisplayControl_GrayWaveformRequested;
            _cameraDisplayControl.GrayWaveformSelectionCompleted += CameraDisplayControl_GrayWaveformSelectionCompleted;
            panelViewerHost.Controls.Add(_cameraDisplayControl);

            _cameraService.FrameReady += CameraService_FrameReady;
            _cameraService.ExternalTriggerReceived += CameraService_ExternalTriggerReceived;

            ApplySettingsToUi();
            UpdateStatus();

            _statusRefreshTimer = new System.Windows.Forms.Timer();
            _statusRefreshTimer.Interval = 300;
            _statusRefreshTimer.Tick += StatusRefreshTimer_Tick;
            _statusRefreshTimer.Start();

            _meterWheelAutoConnectTimer = new System.Windows.Forms.Timer();
            _meterWheelAutoConnectTimer.Interval = 1000;
            _meterWheelAutoConnectTimer.Tick += MeterWheelAutoConnectTimer_Tick;

            this.Shown += MainForm_Shown;
        }

        private void buttonCameraSettings_Click(object sender, EventArgs e)
        {
            var dialogSettings = _settings ?? CameraSettings.CreateDefault();
            CameraSettingsForm form = null;
            try
            {
                form = new CameraSettingsForm(dialogSettings, _cameraService);
                bool settingsCommittedWhileOpen = false;
                form.SettingsCommitted += delegate
                {
                    CommitCameraSettings(form.Settings);
                    settingsCommittedWhileOpen = true;
                };
                DialogResult dialogResult;
                dialogResult = form.ShowDialog(this);

                if (dialogResult != DialogResult.OK && !form.SettingsApplied)
                {
                    return;
                }

                if (!settingsCommittedWhileOpen)
                {
                    CommitCameraSettings(form.Settings);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("Camera Settings dialog open failed.", ex);
                MessageBox.Show(
                    this,
                    "Camera Settings could not be opened.\r\n" + ex.Message + "\r\n\r\nLog: " + AppLogger.GetLogPath(),
                    "Camera Settings Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                labelFooterMessageValue.Text = "Camera Settings open failed: " + ex.Message;
            }
            finally
            {
                if (form != null)
                {
                    form.Dispose();
                }
            }
        }

        private void CommitCameraSettings(CameraSettings settings)
        {
            bool previousImageProcessingEnabled = IsImageProcessingEnabled;
            _settings = (settings ?? CameraSettings.CreateDefault()).Clone();
            _settingsService.Save(_settings);
            _cameraService.ApplySettings(_settings);
            if (_meterWheelControlForm != null && !_meterWheelControlForm.IsDisposed)
            {
                _meterWheelControlForm.UpdateSettings(_settings);
            }
            ApplySettingsToUi();
            UpdateStatus();

            if (previousImageProcessingEnabled != IsImageProcessingEnabled)
            {
                EventHandler handler = ImageProcessingEnabledChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        private void buttonMeterWheel_Click(object sender, EventArgs e)
        {
            if (_meterWheelControlForm == null || _meterWheelControlForm.IsDisposed)
            {
                _meterWheelControlForm = new MeterWheelControlForm(_settings, _settingsService, _meterWheelService);
                _meterWheelControlForm.FormClosed += MeterWheelControlForm_FormClosed;
                _meterWheelControlForm.Show(this);
                return;
            }

            _meterWheelControlForm.UpdateSettings(_settings);
            _meterWheelControlForm.Activate();
        }

        private void MeterWheelControlForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (ReferenceEquals(_meterWheelControlForm, sender))
            {
                _meterWheelControlForm = null;
            }
        }

        private void MeterWheelAutoConnectTimer_Tick(object sender, EventArgs e)
        {
            _meterWheelAutoConnectTimer.Stop();
            if (_isClosing || IsDisposed)
            {
                return;
            }

            AutoConnectMeterWheel();
        }

        private void AutoConnectMeterWheel()
        {
            if (_isClosing || IsDisposed)
            {
                return;
            }

            if (_meterWheelService.IsInitialized)
            {
                return;
            }

            try
            {
                var cardId = (byte)Math.Max(0, Math.Min(Lsi8181Native.CardIdMax, _settings.MeterWheelCardId));
                _meterWheelService.Open(
                    cardId,
                    GetMeterWheelMultipleRate(_settings.MeterWheelMultipleRate),
                    _settings.MeterWheelCompareIncrement,
                    (ushort)Math.Max(0, Math.Min(ushort.MaxValue, _settings.MeterWheelCmpOutWidth)),
                    _settings.MeterWheelReverseDirection,
                    MeterWheelControlForm.CreateExtensionCompareChannelsFromSettings(_settings));
                labelFooterMessageValue.Text = "Meter wheel connected: Card " + cardId;
            }
            catch (Exception ex)
            {
                AppLogger.Log("Meter wheel auto-connect failed.", ex);
                labelFooterMessageValue.Text = "Meter wheel auto-connect failed: " + ex.Message;
            }
        }

        private static byte GetMeterWheelMultipleRate(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 1:
                    return Lsi8181Native.Multiple2;
                case 2:
                    return Lsi8181Native.Multiple1;
                default:
                    return Lsi8181Native.Multiple4;
            }
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            ConnectCameraCore();
            UpdateStatus();
        }

        private bool ConnectCameraCore()
        {
            _cameraService.ApplySettings(_settings);
            if (!_cameraService.Connect())
            {
                return false;
            }

            _settings = _cameraService.CurrentSettings;
            _settingsService.Save(_settings);
            if (_meterWheelControlForm != null && !_meterWheelControlForm.IsDisposed)
            {
                _meterWheelControlForm.UpdateSettings(_settings);
            }
            ApplySettingsToUi();
            return true;
        }

        private void buttonStartPreview_Click(object sender, EventArgs e)
        {
            StartPreviewCore();
        }

        private bool StartPreviewCore()
        {
            _frameRecorder.ClearRolling();
            if (_settings != null && _settings.TriggerMode == TriggerMode.SoftwareTrigger)
            {
                StartSoftwareTriggerMeterWheelMonitorIfNeeded();
                UpdateStatus();
                return true;
            }

            bool started = _cameraService.StartPreview();
            if (started)
            {
                StartSoftwareTriggerMeterWheelMonitorIfNeeded();
            }
            UpdateStatus();
            return started;
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            DisconnectCameraCore();
        }

        private void DisconnectCameraCore()
        {
            StopSoftwareTriggerMeterWheelMonitor();
            _cameraService.Disconnect();
            UpdateStatus();
        }

        private void StopPreviewCore()
        {
            StopSoftwareTriggerMeterWheelMonitor();
            if (_cameraService.Status != null &&
                (_cameraService.Status.IsPreviewing || _cameraService.Status.IsCaptureInProgress))
            {
                _cameraService.StopPreview();
            }
            UpdateStatus();
        }

        private async void buttonStop_Click(object sender, EventArgs e)
        {
            StopPreviewCore();
            if (_settings.RollingCaptureEnabled)
            {
                await ShowRollingSnapshotForReviewAsync();
            }
        }

        private void buttonCapture_Click(object sender, EventArgs e)
        {
            if (_cameraService.CaptureFrame())
            {
                labelFooterMessageValue.Text = "Capture requested.";
            }

            UpdateStatus();
        }

        private async void SaveLatestRecordedFrameIfRequestedAsync()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(SaveLatestRecordedFrameIfRequestedAsync));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            if (Interlocked.CompareExchange(ref _pendingSnapshotSaveCount, 0, 0) <= 0)
            {
                return;
            }

            if (Interlocked.Decrement(ref _pendingSnapshotSaveCount) < 0)
            {
                Interlocked.Exchange(ref _pendingSnapshotSaveCount, 0);
                return;
            }

            if (_settings.RollingCaptureEnabled)
            {
                try
                {
                    var progressForm = EnsureSaveProgressForm();
                    var settings = _settings.Clone();
                    var savedPath = await RunQueuedSnapshotSaveAsync(
                        progressForm,
                        () => SaveRollingCaptureSnapshot(settings));
                    SetFooterMessage("Captured image saved: " + Path.GetFileName(savedPath));
                }
                catch (Exception ex)
                {
                    SetFooterMessage("Captured image save failed: " + ex.Message);
                }
                finally
                {
                    CompleteQueuedSnapshotSave();
                }

                return;
            }

            using (var snapshot = _frameRecorder.SnapshotLatest())
            {
                if (snapshot == null)
                {
                    return;
                }

                try
                {
                    var progressForm = EnsureSaveProgressForm();
                    var settings = _settings.Clone();
                    var savedPath = await RunQueuedSnapshotSaveAsync(
                        progressForm,
                        () => SaveSnapshotBitmap(snapshot, settings));
                    SetFooterMessage("Captured image saved: " + Path.GetFileName(savedPath));
                }
                catch (Exception ex)
                {
                    SetFooterMessage("Captured image save failed: " + ex.Message);
                }
                finally
                {
                    CompleteQueuedSnapshotSave();
                }
            }
        }

        private async void CameraDisplayControl_SaveSnapshotRequested(object sender, EventArgs e)
        {
            var status = _cameraService.Status;
            if (status == null || (status.IsPreviewing && !_settings.RollingCaptureEnabled))
            {
                labelFooterMessageValue.Text = "Stop preview before saving a snapshot.";
                return;
            }

            if (_settings.RollingCaptureEnabled)
            {
                var rollingSnapshot = _frameRecorder.SnapshotRollingFrames();
                if (rollingSnapshot == null)
                {
                    labelFooterMessageValue.Text = "No rolling image is available to save.";
                    return;
                }

                try
                {
                    var progressForm = EnsureSaveProgressForm();
                    var direction = _settings.RollingCaptureDirection;
                    var saveFormat = _settings.ImageSaveFormat;
                    var savedPath = await RunQueuedSnapshotSaveAsync(
                        progressForm,
                        () => SaveManualRollingSnapshot(rollingSnapshot, saveFormat, direction, progressForm.Report));
                    labelFooterMessageValue.Text = "Snapshot saved: " + Path.GetFileName(savedPath);
                }
                catch (Exception ex)
                {
                    labelFooterMessageValue.Text = "Snapshot save failed: " + ex.Message;
                }
                finally
                {
                    CompleteQueuedSnapshotSave();
                    rollingSnapshot.Dispose();
                }

                return;
            }

            using (var snapshot = _frameRecorder.SnapshotLatest())
            {
                if (snapshot == null)
                {
                    labelFooterMessageValue.Text = "No recorded image is available to save.";
                    return;
                }

                try
                {
                    var progressForm = EnsureSaveProgressForm();
                    var saveFormat = _settings.ImageSaveFormat;
                    progressForm.Report(15, "Saving image...");
                    var savedPath = await RunQueuedSnapshotSaveAsync(
                        progressForm,
                        () => SaveManualSnapshotBitmap(snapshot, saveFormat));
                    progressForm.Report(100, "Image saved.");
                    labelFooterMessageValue.Text = "Snapshot saved: " + Path.GetFileName(savedPath);
                }
                catch (Exception ex)
                {
                    labelFooterMessageValue.Text = "Snapshot save failed: " + ex.Message;
                }
                finally
                {
                    CompleteQueuedSnapshotSave();
                }
            }
        }

        private void CameraDisplayControl_GrayWaveformRequested(object sender, EventArgs e)
        {
            var status = _cameraService.Status;
            if (status != null && status.IsPreviewing)
            {
                labelFooterMessageValue.Text = "Stop preview before getting gray waveform.";
                return;
            }

            Bitmap cameraPixelSource = null;
            if (_cameraDisplayControl.IsDisplayingPreviewBitmap)
            {
                cameraPixelSource = _frameRecorder.SnapshotLatest();
            }

            if (!_cameraDisplayControl.BeginGrayWaveformSelection(cameraPixelSource))
            {
                labelFooterMessageValue.Text = "No image is available for waveform selection.";
            }
        }

        private void CameraDisplayControl_GrayWaveformSelectionCompleted(object sender, GrayWaveformSelectionEventArgs e)
        {
            if (e == null || e.PixelSource == null || e.LinePoints == null || e.LinePoints.Length < 2)
            {
                if (e != null && e.PixelSource != null)
                {
                    e.PixelSource.Dispose();
                }

                labelFooterMessageValue.Text = "Gray waveform selection was invalid.";
                return;
            }

            using (var confirm = new GrayWaveformConfirmForm(
                "Start: " + e.StartPoint.X + ", " + e.StartPoint.Y + "\r\n" +
                "End: " + e.EndPoint.X + ", " + e.EndPoint.Y + "\r\n" +
                "Length: " + e.LinePoints.Length + " pixels"))
            {
                var result = confirm.ShowDialog(this);
                if (result == DialogResult.Retry)
                {
                    _cameraDisplayControl.BeginGrayWaveformSelection();
                    return;
                }

                if (result != DialogResult.OK || !confirm.IsConfirmSelected)
                {
                    _cameraDisplayControl.CancelGrayWaveformSelection();
                    labelFooterMessageValue.Text = "Gray waveform selection cancelled.";
                    return;
                }

                using (var waveform = new GrayScaleWaveformForm(e.PixelSource, e.LinePoints))
                {
                    waveform.ShowDialog(this);
                }
            }

            _cameraDisplayControl.CancelGrayWaveformSelection();
        }

        private async Task<string> RunQueuedSnapshotSaveAsync(SaveProgressForm progressForm, Func<string> saveAction)
        {
            ReportSnapshotSaveCounts(progressForm);
            progressForm.Report(0, "Waiting for a save slot...");
            await _snapshotSaveGate.WaitAsync();
            Interlocked.Increment(ref _snapshotSaveActiveCount);
            ReportSnapshotSaveCounts(progressForm);
            try
            {
                progressForm.Report(5, "Saving image...");
                var savedPath = await Task.Run(saveAction);
                Interlocked.Increment(ref _snapshotSaveCompletedCount);
                ReportSnapshotSaveCounts(progressForm);
                return savedPath;
            }
            catch
            {
                Interlocked.Increment(ref _snapshotSaveFailedCount);
                ReportSnapshotSaveCounts(progressForm);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _snapshotSaveActiveCount);
                _snapshotSaveGate.Release();
                ReportSnapshotSaveCounts(progressForm);
            }
        }

        private SaveProgressForm EnsureSaveProgressForm()
        {
            var queuedCount = Interlocked.Increment(ref _snapshotSaveQueueCount);
            lock (_snapshotProgressLock)
            {
                if (_snapshotProgressForm == null || _snapshotProgressForm.IsDisposed)
                {
                    Interlocked.Exchange(ref _snapshotSaveCompletedCount, 0);
                    Interlocked.Exchange(ref _snapshotSaveFailedCount, 0);
                    _snapshotProgressForm = new SaveProgressForm();
                    _snapshotProgressForm.FormClosed += SnapshotProgressForm_FormClosed;
                    _snapshotProgressForm.Show(this);
                }

                _snapshotProgressForm.Report(0, "Queued save jobs: " + queuedCount + ".");
                ReportSnapshotSaveCounts(_snapshotProgressForm);
                return _snapshotProgressForm;
            }
        }

        private void CompleteQueuedSnapshotSave()
        {
            var remaining = Interlocked.Decrement(ref _snapshotSaveQueueCount);
            if (remaining > 0)
            {
                return;
            }

            lock (_snapshotProgressLock)
            {
                if (_snapshotProgressForm == null || _snapshotProgressForm.IsDisposed)
                {
                    return;
                }

                _snapshotProgressForm.Report(100, "All save jobs completed.");
                ReportSnapshotSaveCounts(_snapshotProgressForm);
                _snapshotProgressForm.Close();
                _snapshotProgressForm = null;
            }
        }

        private void ReportSnapshotSaveCounts(SaveProgressForm progressForm)
        {
            if (progressForm == null || progressForm.IsDisposed)
            {
                return;
            }

            var outstandingCount = Interlocked.CompareExchange(ref _snapshotSaveQueueCount, 0, 0);
            var activeCount = Interlocked.CompareExchange(ref _snapshotSaveActiveCount, 0, 0);
            var completedCount = Interlocked.CompareExchange(ref _snapshotSaveCompletedCount, 0, 0);
            var failedCount = Interlocked.CompareExchange(ref _snapshotSaveFailedCount, 0, 0);
            var waitingCount = Math.Max(0, outstandingCount - activeCount);
            progressForm.ReportCounts(activeCount, waitingCount, completedCount, failedCount);
        }

        private void SnapshotProgressForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            lock (_snapshotProgressLock)
            {
                if (ReferenceEquals(_snapshotProgressForm, sender))
                {
                    _snapshotProgressForm = null;
                }
            }
        }

        private async Task ShowRollingSnapshotForReviewAsync()
        {
            labelFooterMessageValue.Text = "Preparing rolling image for review...";
            try
            {
                var filePath = Path.Combine(
                    Path.GetTempPath(),
                    "CameraCaptureApp_rolling_review_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".png");

                await Task.Run(
                    () =>
                    {
                        _frameRecorder.SaveRollingPng(filePath, _settings.RollingCaptureDirection, null, null);
                    });

                if (!File.Exists(filePath))
                {
                    labelFooterMessageValue.Text = "No rolling image is available for review.";
                    return;
                }

                await LoadImageForReviewAsync(filePath);
                labelFooterMessageValue.Text = "Rolling image ready for review.";
            }
            catch (OperationCanceledException)
            {
                labelFooterMessageValue.Text = "Rolling image review cancelled.";
            }
            catch (Exception ex)
            {
                labelFooterMessageValue.Text = "Rolling image review failed: " + ex.Message;
            }
        }

        private async Task LoadImageForReviewAsync(string filePath)
        {
            CancelPendingImageLoad();
            _imageLoadTokenSource = new CancellationTokenSource();
            await _cameraDisplayControl.LoadImageFromFileAsync(filePath, _imageLoadTokenSource.Token);
        }

        private async void buttonLoadImage_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|All Files|*.*";
                dialog.Title = "Load preview image";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                CancelPendingImageLoad();
                _imageLoadTokenSource = new CancellationTokenSource();
                var token = _imageLoadTokenSource.Token;
                labelFooterMessageValue.Text = "Loading image...";

                try
                {
                    await _cameraDisplayControl.LoadImageFromFileAsync(dialog.FileName, token);
                    labelFooterMessageValue.Text = "Loaded image: " + Path.GetFileName(dialog.FileName);
                }
                catch (OperationCanceledException)
                {
                    labelFooterMessageValue.Text = "Image load cancelled.";
                }
                catch (Exception ex)
                {
                    labelFooterMessageValue.Text = "Image load failed: " + ex.Message;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;
            StopSoftwareTriggerMeterWheelMonitor();
            _statusRefreshTimer.Stop();
            _meterWheelAutoConnectTimer.Stop();
            CancelPendingImageLoad();
            CancelPendingPreviewFrame();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _isClosing = true;
            _cameraService.FrameReady -= CameraService_FrameReady;
            _cameraService.ExternalTriggerReceived -= CameraService_ExternalTriggerReceived;
            _cameraService.ImageAcquired -= CameraService_ImageAcquired;
            _cameraDisplayControl.SaveSnapshotRequested -= CameraDisplayControl_SaveSnapshotRequested;
            _statusRefreshTimer.Dispose();
            _meterWheelAutoConnectTimer.Dispose();
            if (_meterWheelControlForm != null && !_meterWheelControlForm.IsDisposed)
            {
                _meterWheelControlForm.Close();
            }

            CancelPendingImageLoad();
            CancelPendingPreviewFrame();
            _frameRecorder.Dispose();
            ClearPendingRollingPreviewFrames();
            var pendingFrame = Interlocked.Exchange(ref _pendingPreviewFrame, null);
            if (pendingFrame != null)
            {
                pendingFrame.Dispose();
            }

            try
            {
                _cameraService.Disconnect();
            }
            catch (Exception ex)
            {
                AppLogger.Log("Camera disconnect during shutdown failed.", ex);
            }

            try
            {
                _meterWheelService.Dispose();
            }
            catch (Exception ex)
            {
                AppLogger.Log("Meter wheel dispose during shutdown failed.", ex);
            }

            base.OnFormClosed(e);
        }

        private void CameraService_FrameReady(object sender, CameraFrameEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                e.Frame.Dispose();
                return;
            }

            _frameRecorder.StoreLatest(e.Frame);
            if (_settings.RollingCaptureEnabled)
            {
                _frameRecorder.StoreRolling(e.Frame, _settings.RollingCaptureFrameCount);
            }
            else
            {
                _frameRecorder.ClearRolling();
            }

            QueueSoftwareTriggerAutoSave();
            SaveLatestRecordedFrameIfRequestedAsync();
            if (!_settings.ShowCameraImage)
            {
                e.Frame.Dispose();
                return;
            }

            if (_settings.RollingCaptureEnabled)
            {
                lock (_pendingPreviewFramesLock)
                {
                    _pendingRollingPreviewFrames.Enqueue(e.Frame);
                }
            }
            else
            {
                ClearPendingRollingPreviewFrames();
                var previousFrame = Interlocked.Exchange(ref _pendingPreviewFrame, e.Frame);
                if (previousFrame != null)
                {
                    previousFrame.Dispose();
                }
            }

            if (Interlocked.Exchange(ref _previewFrameUiUpdateQueued, 1) == 0)
            {
                BeginInvoke(new Action(ProcessPendingPreviewFrameAsync));
            }
        }

        private void CameraService_ImageAcquired(object sender, CameraImageAcquiredEventArgs e)
        {
            var handler = _imageAcquiredHandlers;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void CameraService_ExternalTriggerReceived(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                QueueExternalTriggerAutoSave();
                BeginInvoke(new Action(ApplyMeterWheelActionsOnExternalTrigger));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void StartSoftwareTriggerMeterWheelMonitorIfNeeded()
        {
            if (_settings == null || _settings.TriggerMode != TriggerMode.SoftwareTrigger)
            {
                StopSoftwareTriggerMeterWheelMonitor();
                return;
            }

            if (_cameraService.IsTransferStartBlocked)
            {
                SetFooterMessageFromAnyThread("Software trigger monitor was not started: camera transfer is still busy.");
                return;
            }

            if (!_meterWheelService.IsInitialized)
            {
                SetFooterMessageFromAnyThread("Software trigger monitor was not started: meter wheel is not connected.");
                return;
            }

            lock (_softwareTriggerMonitorLock)
            {
                StopSoftwareTriggerMeterWheelMonitorLocked();

                _softwareTriggerMonitorTokenSource = new CancellationTokenSource();
                var token = _softwareTriggerMonitorTokenSource.Token;
                var compareValue = _settings.MeterWheelCompareValue;
                _softwareTriggerMonitorTask = Task.Run(() => RunSoftwareTriggerMeterWheelMonitor(compareValue, token), token);
            }

            SetFooterMessageFromAnyThread("Software trigger monitor started. Waiting for meter wheel compare crossing.");
        }

        private void StopSoftwareTriggerMeterWheelMonitor()
        {
            lock (_softwareTriggerMonitorLock)
            {
                StopSoftwareTriggerMeterWheelMonitorLocked();
            }
        }

        private void StopSoftwareTriggerMeterWheelMonitorLocked()
        {
            if (_softwareTriggerMonitorTokenSource == null)
            {
                return;
            }

            _softwareTriggerMonitorTokenSource.Cancel();
            _softwareTriggerMonitorTokenSource.Dispose();
            _softwareTriggerMonitorTokenSource = null;
            _softwareTriggerMonitorTask = null;
            Interlocked.Exchange(ref _softwareTriggerCaptureQueued, 0);
        }

        private bool IsSoftwareTriggerMeterWheelMonitorRunning()
        {
            lock (_softwareTriggerMonitorLock)
            {
                return _softwareTriggerMonitorTokenSource != null;
            }
        }

        private async Task RunSoftwareTriggerMeterWheelMonitor(int compareValue, CancellationToken token)
        {
            var waitingForBelowCompare = true;
            try
            {
                var encoderValue = _meterWheelService.ReadEncoder();
                if (encoderValue < compareValue)
                {
                    _meterWheelService.SetCompare(compareValue);
                    QueueSoftwareTriggerCapture(compareValue, encoderValue);
                    waitingForBelowCompare = false;
                    SetFooterMessageFromAnyThread("Software trigger armed Compare Set: " + compareValue + ", Encoder: " + encoderValue);
                }
                else
                {
                    SetFooterMessageFromAnyThread("Software trigger monitor waiting below Compare: " + compareValue + ", Encoder: " + encoderValue);
                }

                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(50, token);
                    encoderValue = _meterWheelService.ReadEncoder();

                    if (waitingForBelowCompare)
                    {
                        if (encoderValue < compareValue)
                        {
                            _meterWheelService.SetCompare(compareValue);
                            QueueSoftwareTriggerCapture(compareValue, encoderValue);
                            waitingForBelowCompare = false;
                            SetFooterMessageFromAnyThread("Software trigger armed Compare Set: " + compareValue + ", Encoder: " + encoderValue);
                        }

                        continue;
                    }

                    if (encoderValue > compareValue)
                    {
                        waitingForBelowCompare = true;
                        SetFooterMessageFromAnyThread("Software trigger monitor waiting below Compare: " + compareValue + ", Encoder: " + encoderValue);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                AppLogger.Log("Software trigger meter wheel monitor failed.", ex);
                SetFooterMessageFromAnyThread("Software trigger monitor failed: " + ex.Message);
            }
        }

        private void QueueSoftwareTriggerCapture(int compareValue, int encoderValue)
        {
            if (Interlocked.Exchange(ref _softwareTriggerCaptureQueued, 1) == 1)
            {
                return;
            }

            if (_isClosing || IsDisposed || !IsHandleCreated)
            {
                Interlocked.Exchange(ref _softwareTriggerCaptureQueued, 0);
                return;
            }

            try
            {
                BeginInvoke(new Action(() => ExecuteSoftwareTriggerCapture(compareValue, encoderValue)));
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Exchange(ref _softwareTriggerCaptureQueued, 0);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref _softwareTriggerCaptureQueued, 0);
            }
        }

        private void ExecuteSoftwareTriggerCapture(int compareValue, int encoderValue)
        {
            try
            {
                if (_isClosing || IsDisposed || _settings == null || _settings.TriggerMode != TriggerMode.SoftwareTrigger)
                {
                    return;
                }

                if (_cameraService.CaptureFrame())
                {
                    labelFooterMessageValue.Text = "Software trigger capture requested after Compare Set: "
                        + compareValue
                        + ", Encoder: "
                        + encoderValue;
                    return;
                }

                labelFooterMessageValue.Text = "Software trigger Capture could not be started after Compare Set: "
                    + compareValue
                    + ", Encoder: "
                    + encoderValue;
            }
            finally
            {
                Interlocked.Exchange(ref _softwareTriggerCaptureQueued, 0);
            }
        }

        private void SetFooterMessageFromAnyThread(string message)
        {
            if (_isClosing || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(SetFooterMessageFromAnyThread), message);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            labelFooterMessageValue.Text = message;
        }

        private void QueueExternalTriggerAutoSave()
        {
            if (_settings == null ||
                _settings.TriggerMode != TriggerMode.ExternalTrigger ||
                !_settings.ExternalFrameTriggerOneFrame ||
                !_settings.AutoSaveOnExternalTriggerOneFrame)
            {
                return;
            }

            Interlocked.Increment(ref _pendingSnapshotSaveCount);
        }

        private void QueueSoftwareTriggerAutoSave()
        {
            if (_settings == null ||
                _settings.TriggerMode != TriggerMode.SoftwareTrigger ||
                !_settings.AutoSaveOnSoftwareTriggerFrame)
            {
                return;
            }

            Interlocked.Increment(ref _pendingSnapshotSaveCount);
        }

        private void ApplyMeterWheelActionsOnExternalTrigger()
        {
            if (_settings == null ||
                _settings.TriggerMode != TriggerMode.ExternalTrigger ||
                !_settings.ExternalFrameTriggerOneFrame ||
                !_settings.ExternalFrameTriggerOneFrameCompareFromEncoder)
            {
                return;
            }

            if (!_meterWheelService.IsInitialized)
            {
                labelFooterMessageValue.Text = "External trigger received, but meter wheel is not connected.";
                return;
            }

            try
            {
                _meterWheelService.SetCompare(_settings.MeterWheelCompareValue);
                if (_settings.ExternalFrameTriggerOneFrameSetEncoderOnTrigger)
                {
                    _meterWheelService.SetEncoder(_settings.MeterWheelEncoderValue);
                    labelFooterMessageValue.Text = "External trigger applied Compare Set: "
                        + _settings.MeterWheelCompareValue
                        + ", Encoder Set: "
                        + _settings.MeterWheelEncoderValue;
                    return;
                }

                labelFooterMessageValue.Text = "External trigger applied Compare Set: " + _settings.MeterWheelCompareValue;
            }
            catch (Exception ex)
            {
                AppLogger.Log("External trigger meter wheel action failed.", ex);
                labelFooterMessageValue.Text = "External trigger meter wheel action failed: " + ex.Message;
            }
        }

        private async void ProcessPendingPreviewFrameAsync()
        {
            Bitmap frame = null;
            try
            {
                frame = Interlocked.Exchange(ref _pendingPreviewFrame, null);
                if (frame == null)
                {
                    frame = DequeuePendingRollingPreviewFrame();
                }

                if (frame == null || IsDisposed)
                {
                    return;
                }
                if (!_settings.ShowCameraImage)
                {
                    frame.Dispose();
                    frame = null;
                    return;
                }

                await DisplayPreviewFrameAsync(frame);
                frame = null;
            }
            finally
            {
                if (frame != null)
                {
                    frame.Dispose();
                }

                Interlocked.Exchange(ref _previewFrameUiUpdateQueued, 0);
                if ((Interlocked.CompareExchange(ref _pendingPreviewFrame, null, null) != null || HasPendingRollingPreviewFrames()) &&
                    Interlocked.Exchange(ref _previewFrameUiUpdateQueued, 1) == 0 &&
                    !IsDisposed &&
                    IsHandleCreated)
                {
                    BeginInvoke(new Action(ProcessPendingPreviewFrameAsync));
                }
            }
        }

        private Bitmap DequeuePendingRollingPreviewFrame()
        {
            lock (_pendingPreviewFramesLock)
            {
                return _pendingRollingPreviewFrames.Count > 0 ? _pendingRollingPreviewFrames.Dequeue() : null;
            }
        }

        private bool HasPendingRollingPreviewFrames()
        {
            lock (_pendingPreviewFramesLock)
            {
                return _pendingRollingPreviewFrames.Count > 0;
            }
        }

        private void ClearPendingRollingPreviewFrames()
        {
            lock (_pendingPreviewFramesLock)
            {
                while (_pendingRollingPreviewFrames.Count > 0)
                {
                    _pendingRollingPreviewFrames.Dequeue().Dispose();
                }
            }
        }

        private async Task DisplayPreviewFrameAsync(Bitmap frame)
        {
            CancelPendingPreviewFrame();
            _previewFrameTokenSource = new CancellationTokenSource();
            var token = _previewFrameTokenSource.Token;
            var displayOwnsFrame = false;

            try
            {
                await _cameraDisplayControl.ShowFrameAsync(
                    frame,
                    token,
                    _settings.RollingCaptureEnabled,
                    _settings.RollingCaptureFrameCount,
                    _settings.RollingCaptureDirection);
                displayOwnsFrame = true;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!displayOwnsFrame)
                {
                    frame.Dispose();
                }
            }
        }

        private void StatusRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_isClosing || IsDisposed)
            {
                return;
            }

            UpdateStatus();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (_isClosing || IsDisposed)
            {
                return;
            }

            TryAutoConnectOnStart();
            _meterWheelAutoConnectTimer.Start();
        }

        private void ApplySettingsToUi()
        {
            labelHeaderResolutionValue.Text = _settings.Width + " x " + _settings.Height;
            labelHeaderTriggerValue.Text = GetTriggerModeDisplay(_settings.TriggerMode);
            labelHeaderCameraValue.Text = _settings.CameraName;
            _cameraDisplayControl.ResolutionText = _settings.Width + " x " + _settings.Height;
            if (_settings.ShowCameraImage)
            {
                _cameraDisplayControl.OverlayText = "Ready for preview";
                return;
            }

            CancelPendingPreviewFrame();
            ClearPendingRollingPreviewFrames();
            var pendingFrame = Interlocked.Exchange(ref _pendingPreviewFrame, null);
            if (pendingFrame != null)
            {
                pendingFrame.Dispose();
            }
            _cameraDisplayControl.ClearLivePreview("Camera image display is disabled.");
        }

        private void UpdateStatus()
        {
            if (_isClosing || IsDisposed)
            {
                return;
            }

            var status = _cameraService.Status;

            labelHeaderConnectionValue.Text = status.IsConnected ? "Connected" : "Offline";
            labelHeaderSignalValue.Text = status.HasSignal ? "Detected" : "Missing";
            labelFooterLinesValue.Text = status.ScannedLineCount.ToString();
            labelFooterPreviewValue.Text = status.IsPreviewing ? "Running" : "Stopped";
            labelFooterMessageValue.Text = status.LastMessage;
            labelFooterScanStateValue.Text = status.ScanStateText;
            if (!string.IsNullOrWhiteSpace(status.CameraName))
            {
                labelHeaderCameraValue.Text = status.CameraName;
            }

            UpdateCommandStates(status);
        }

        private void SetFooterMessage(string message)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(SetFooterMessage), message);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            labelFooterMessageValue.Text = message;
        }

        private void UpdateCommandStates(CameraStatus status)
        {
            var isConnected = status != null && status.IsConnected;
            var isPreviewing = status != null && status.IsPreviewing;
            var isCaptureInProgress = status != null && status.IsCaptureInProgress;
            var isTransferStartBlocked = _cameraService.IsTransferStartBlocked;
            var isSoftwareTriggerMonitorRunning = IsSoftwareTriggerMeterWheelMonitorRunning();
            var isBusyPreviewing = isPreviewing || isCaptureInProgress || isTransferStartBlocked || isSoftwareTriggerMonitorRunning;

            buttonCameraSettings.Enabled = true;
            buttonMeterWheel.Enabled = true;
            buttonConnect.Enabled = !isConnected;
            buttonDisconnect.Enabled = isConnected;
            buttonStartPreview.Enabled = isConnected && !isBusyPreviewing;
            buttonStop.Enabled = isConnected && isBusyPreviewing;
            buttonCapture.Enabled = isConnected && !isBusyPreviewing;
            buttonLoadImage.Enabled = !isBusyPreviewing;
            _cameraDisplayControl.SaveSnapshotButtonEnabled = isConnected && (!isBusyPreviewing || _settings.RollingCaptureEnabled);
            _cameraDisplayControl.GrayWaveformButtonEnabled = !isBusyPreviewing;
        }

        private void CancelPendingImageLoad()
        {
            if (_imageLoadTokenSource == null)
            {
                return;
            }

            _imageLoadTokenSource.Cancel();
            _imageLoadTokenSource.Dispose();
            _imageLoadTokenSource = null;
        }

        private void CancelPendingPreviewFrame()
        {
            if (_previewFrameTokenSource == null)
            {
                return;
            }

            _previewFrameTokenSource.Cancel();
            _previewFrameTokenSource.Dispose();
            _previewFrameTokenSource = null;
        }

        private static string GetTriggerModeDisplay(TriggerMode mode)
        {
            switch (mode)
            {
                case TriggerMode.SingleFrame:
                    return "Single";
                case TriggerMode.SoftwareTrigger:
                    return "Software";
                case TriggerMode.ExternalTrigger:
                    return "External";
                default:
                    return "Free Run";
            }
        }

        private void TryAutoConnectOnStart()
        {
            if (_isClosing || IsDisposed || _autoConnectAttempted || !_settings.AutoConnect)
            {
                return;
            }

            _autoConnectAttempted = true;
            _cameraService.ApplySettings(_settings);
            if (_cameraService.Connect())
            {
                _settings = _cameraService.CurrentSettings;
                _settingsService.Save(_settings);
                ApplySettingsToUi();
            }

            UpdateStatus();
        }

        private static string SaveSnapshotBitmap(Bitmap bitmap, CameraSettings settings)
        {
            var outputFolder = ResolveSnapshotFolder(settings);
            Directory.CreateDirectory(outputFolder);

            var filePath = BuildSnapshotPath(outputFolder, settings);
            FrameRecorder.SaveBitmapImage(bitmap, filePath, settings.ImageSaveFormat, null);
            return filePath;
        }

        private string SaveRollingCaptureSnapshot(CameraSettings settings)
        {
            var outputFolder = ResolveSnapshotFolder(settings);
            Directory.CreateDirectory(outputFolder);

            var filePath = BuildSnapshotPath(outputFolder, settings);
            if (!_frameRecorder.SaveRollingImage(filePath, settings.ImageSaveFormat, settings.RollingCaptureDirection, null, null))
            {
                throw new InvalidOperationException("No rolling image is available to save.");
            }

            return filePath;
        }

        private string SaveManualRollingSnapshot(
            FrameRecorder.RollingFrameSnapshot rollingSnapshot,
            ImageSaveFormat saveFormat,
            RollingCaptureDirection direction,
            Action<int, string> reportProgress)
        {
            var outputFolder = Path.Combine(Application.StartupPath, "snapshot");
            Directory.CreateDirectory(outputFolder);

            var baseName = "rolling_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff");
            var filePath = BuildManualSnapshotPath(outputFolder, baseName, saveFormat);
            rollingSnapshot.SaveImage(filePath, saveFormat, direction, reportProgress, null);

            if (!File.Exists(filePath))
            {
                throw new IOException("Snapshot file was not created: " + filePath);
            }

            return filePath;
        }

        private static string SaveManualSnapshotBitmap(Bitmap bitmap, ImageSaveFormat saveFormat)
        {
            var outputFolder = Path.Combine(Application.StartupPath, "snapshot");
            Directory.CreateDirectory(outputFolder);

            var baseName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff");
            var filePath = BuildManualSnapshotPath(outputFolder, baseName, saveFormat);
            FrameRecorder.SaveBitmapImage(bitmap, filePath, saveFormat, null);
            if (!File.Exists(filePath))
            {
                throw new IOException("Snapshot file was not created: " + filePath);
            }

            return filePath;
        }

        private static string BuildManualSnapshotPath(string outputFolder, string baseName, ImageSaveFormat saveFormat)
        {
            var extension = GetImageExtension(saveFormat);
            var candidatePath = Path.Combine(outputFolder, baseName + extension);
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            var suffix = 1;
            while (true)
            {
                var nextPath = Path.Combine(outputFolder, baseName + "_" + suffix.ToString("000") + extension);
                if (!File.Exists(nextPath))
                {
                    return nextPath;
                }

                suffix++;
            }
        }

        private static string ResolveSnapshotFolder(CameraSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.SaveFolder))
            {
                return settings.SaveFolder.Trim();
            }

            return Path.Combine(Application.StartupPath, "CameraCapture", "Captures");
        }

        private static string BuildSnapshotPath(string outputFolder, CameraSettings settings)
        {
            var baseName = FormatFileNamePattern(settings.FileNamePattern);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            baseName = SanitizeFileName(baseName);
            var extension = GetImageExtension(settings.ImageSaveFormat);
            var candidatePath = Path.Combine(outputFolder, baseName + extension);
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            var suffix = 1;
            while (true)
            {
                var nextPath = Path.Combine(outputFolder, baseName + "_" + suffix.ToString("000") + extension);
                if (!File.Exists(nextPath))
                {
                    return nextPath;
                }

                suffix++;
            }
        }

        private static string FormatFileNamePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return string.Empty;
            }

            var result = pattern;
            var startIndex = result.IndexOf('{');
            while (startIndex >= 0)
            {
                var endIndex = result.IndexOf('}', startIndex + 1);
                if (endIndex <= startIndex)
                {
                    break;
                }

                var format = result.Substring(startIndex + 1, endIndex - startIndex - 1);
                string replacement;
                try
                {
                    replacement = DateTime.Now.ToString(format);
                }
                catch (FormatException)
                {
                    replacement = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }

                result = result.Substring(0, startIndex) + replacement + result.Substring(endIndex + 1);
                startIndex = result.IndexOf('{', startIndex + replacement.Length);
            }

            return result;
        }

        private static string GetImageExtension(ImageSaveFormat saveFormat)
        {
            switch (saveFormat)
            {
                case ImageSaveFormat.Tif:
                case ImageSaveFormat.UncompressedTif:
                    return ".tif";
                default:
                    return ".png";
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var sanitized = fileName;
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            return sanitized.Trim();
        }
    }
}
