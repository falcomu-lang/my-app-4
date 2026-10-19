using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CameraCaptureApp.Models;
using CameraCaptureApp.Services;

namespace CameraCaptureApp.Forms
{
    public partial class CameraSettingsForm : Form
    {
        private readonly ICameraService _cameraService;

        public CameraSettingsForm(CameraSettings settings, ICameraService cameraService)
        {
            try
            {
                _cameraService = cameraService;
                Settings = (settings ?? CameraSettings.CreateDefault()).Clone();
                InitializeComponent();
                BindSettings();
            }
            catch (Exception ex)
            {
                AppLogger.Log("CameraSettingsForm constructor failed.", ex);
                throw;
            }
        }

        public CameraSettings Settings { get; private set; }

        public bool SettingsApplied { get; private set; }

        private void BindSettings()
        {
            comboBoxTriggerMode.Items.Clear();
            comboBoxTriggerMode.Items.AddRange(new object[]
            {
                "Continuous",
                "Single Frame",
                "Software Trigger",
                "External Trigger"
            });
            comboBoxRollingCaptureDirection.Items.Clear();
            comboBoxRollingCaptureDirection.Items.AddRange(new object[]
            {
                "由上而下",
                "由下而上"
            });
            comboBoxImageSaveFormat.Items.Clear();
            comboBoxImageSaveFormat.Items.AddRange(new object[]
            {
                "PNG",
                "TIF",
                "TIF (未壓縮)"
            });

            textBoxCameraName.Text = Settings.CameraName;
            textBoxConfigFile.Text = Settings.ConfigFilePath;
            textBoxServerName.Text = Settings.ServerName;
            textBoxServerIndex.Text = Settings.ServerIndex >= 0 ? Settings.ServerIndex.ToString() : string.Empty;
            textBoxResourceIndex.Text = Settings.ResourceIndex.ToString();
            numericExposure.Value = Settings.ExposureTime;
            numericGain.Value = Settings.Gain;
            numericLength.Value = Settings.Length > 0 ? Settings.Length : 1;
            checkBoxRollingCaptureEnabled.Checked = Settings.RollingCaptureEnabled;
            numericRollingCaptureFrameCount.Value = ClampToNumericRange(
                numericRollingCaptureFrameCount,
                Settings.RollingCaptureFrameCount > 0 ? Settings.RollingCaptureFrameCount : 1);
            comboBoxRollingCaptureDirection.SelectedIndex = (int)Settings.RollingCaptureDirection >= 0 &&
                (int)Settings.RollingCaptureDirection < comboBoxRollingCaptureDirection.Items.Count
                    ? (int)Settings.RollingCaptureDirection
                    : 0;
            numericInternalLineRate.Value = Settings.InternalLineRate > 0 ? Settings.InternalLineRate : 1;
            var triggerIndex = (int)Settings.TriggerMode;
            comboBoxTriggerMode.SelectedIndex = triggerIndex >= 0 && triggerIndex < comboBoxTriggerMode.Items.Count ? triggerIndex : 0;
            checkBoxExternalFrameTriggerOneFrame.Checked = Settings.ExternalFrameTriggerOneFrame;
            checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Checked = Settings.ExternalFrameTriggerOneFrameCompareFromEncoder;
            checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Checked = Settings.ExternalFrameTriggerOneFrameSetEncoderOnTrigger;
            UpdateTriggerModeControls();
            checkBoxAutoConnect.Checked = Settings.AutoConnect;
            checkBoxAutoSaveOnExternalTriggerOneFrame.Checked = Settings.AutoSaveOnExternalTriggerOneFrame;
            checkBoxAutoSaveOnSoftwareTriggerFrame.Checked = Settings.AutoSaveOnSoftwareTriggerFrame;
            comboBoxImageSaveFormat.SelectedIndex = GetImageSaveFormatIndex(Settings.ImageSaveFormat);
            labelReadResult.Text = "Load Sapera settings first, then read supported CCF values into the fields.";
        }

        private void buttonBrowseSapera_Click(object sender, EventArgs e)
        {
            SaveSettings();
            _cameraService.ApplySettings(Settings);

            if (!_cameraService.SelectConnectionSettings(this))
            {
                return;
            }

            Settings = _cameraService.CurrentSettings;
            textBoxConfigFile.Text = Settings.ConfigFilePath;
            textBoxServerName.Text = Settings.ServerName;
            textBoxServerIndex.Text = Settings.ServerIndex >= 0 ? Settings.ServerIndex.ToString() : string.Empty;
            textBoxResourceIndex.Text = Settings.ResourceIndex.ToString();
            textBoxCameraName.Text = Settings.CameraName;
            labelReadResult.Text = "Sapera acquisition settings loaded.";
        }

        private void buttonReadCcfToFields_Click(object sender, EventArgs e)
        {
            SaveSettings();

            if (string.IsNullOrWhiteSpace(Settings.ConfigFilePath))
            {
                labelReadResult.Text = "Please choose a CCF file first.";
                return;
            }

            if (!File.Exists(Settings.ConfigFilePath))
            {
                labelReadResult.Text = "The selected CCF file could not be found.";
                return;
            }

            try
            {
                var values = LoadCcfValues(Settings.ConfigFilePath);
                var appliedFields = new List<string>();
                decimal decimalValue;
                int intValue;
                TriggerMode triggerMode;

                if (TryReadDecimal(
                    values,
                    out decimalValue,
                    "ExposureTime",
                    "ExposureTimeAbs",
                    "Exposure",
                    "ExposureTimeRaw",
                    "LineIntegrateDuration",
                    "LineIntegrationDuration",
                    "LINE_INTEGRATE_DURATION",
                    "TimeIntegrateDuration",
                    "TimeIntegrationDuration",
                    "TIME_INTEGRATE_DURATION",
                    "CamTriggerDuration",
                    "CAM_TRIGGER_DURATION"))
                {
                    numericExposure.Value = ClampToNumericRange(numericExposure, decimalValue);
                    appliedFields.Add("Exposure");
                }

                if (TryReadDecimal(values, out decimalValue, "Gain", "GainRaw", "AnalogGain"))
                {
                    numericGain.Value = ClampToNumericRange(numericGain, decimalValue);
                    appliedFields.Add("Gain");
                }

                if (TryReadInt(values, out intValue, "AcquisitionLineCount", "FrameLength", "ImageHeight", "ROIHeight", "LineCount", "Height"))
                {
                    numericLength.Value = ClampToNumericRange(numericLength, intValue);
                    appliedFields.Add("Length");
                }

                if (TryReadDecimal(values, out decimalValue, "AcquisitionLineRate", "LineRate", "DeviceLineRate", "InternalLineRate", "INT_LINE_TRIGGER_FREQ"))
                {
                    numericInternalLineRate.Value = ClampToNumericRange(numericInternalLineRate, decimalValue);
                    appliedFields.Add("Internal Line Rate");
                }

                if (TryReadTriggerMode(values, out triggerMode))
                {
                    comboBoxTriggerMode.SelectedIndex = (int)triggerMode;
                    appliedFields.Add("Trigger Mode");
                }

                labelReadResult.Text = appliedFields.Count > 0
                    ? "Mapped CCF file values to: " + string.Join(", ", appliedFields.ToArray())
                    : "No supported CCF fields were found. Connection settings were kept unchanged.";
                SaveSettings();
            }
            catch (Exception ex)
            {
                AppLogger.Log("Read CCF to fields failed.", ex);
                labelReadResult.Text = "Read CCF failed: " + ex.Message;
            }
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            SaveSettings();
            _cameraService.ApplySettings(Settings);
            SettingsApplied = true;
            ShowApplyResult();
        }

        private void buttonProbeLiveFeatures_Click(object sender, EventArgs e)
        {
            try
            {
                var filePath = _cameraService.ExportLiveFeatureReport();
                labelReadResult.Text = Path.GetFileName(filePath).StartsWith("live_features_failed_", StringComparison.OrdinalIgnoreCase)
                    ? "Live features unavailable. Diagnostic report exported: " + filePath
                    : "Live feature report exported: " + filePath;
            }
            catch (Exception ex)
            {
                AppLogger.Log("Probe Live Features failed.", ex);
                labelReadResult.Text = "Probe Live Features failed: " + ex.Message;
            }
        }

        private void buttonLoadDeviceFeatures_Click(object sender, EventArgs e)
        {
            SaveSettings();
            _cameraService.ApplySettings(Settings);

            if (!_cameraService.SelectDeviceFeatureSettings(this))
            {
                return;
            }

            Settings = _cameraService.CurrentSettings;
            SettingsApplied = true;
            labelReadResult.Text = string.IsNullOrWhiteSpace(Settings.DeviceFeatureConfigFilePath)
                ? "Sapera device feature settings loaded without a config file. Use Live Features after connecting."
                : "Sapera device feature settings loaded: " + Settings.DeviceFeatureConfigFilePath;
        }

        private void buttonProbeAcquisitionParameters_Click(object sender, EventArgs e)
        {
            try
            {
                var filePath = _cameraService.ExportAcquisitionParameterReport();
                labelReadResult.Text = "Acquisition parameter report exported: " + filePath;
            }
            catch (Exception ex)
            {
                AppLogger.Log("Probe Acquisition Parameters failed.", ex);
                labelReadResult.Text = "Probe Acquisition Parameters failed: " + ex.Message;
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            SaveSettings();
            _cameraService.ApplySettings(Settings);
            SettingsApplied = true;
            ShowApplyResult();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowApplyResult()
        {
            var message = _cameraService.Status.LastMessage;
            labelReadResult.Text = message;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void SaveSettings()
        {
            Settings.CameraName = textBoxCameraName.Text.Trim();
            Settings.ConfigFilePath = textBoxConfigFile.Text.Trim();
            Settings.ServerName = textBoxServerName.Text.Trim();
            Settings.ServerIndex = ParseInt(textBoxServerIndex.Text, Settings.ServerIndex);
            Settings.ResourceIndex = ParseInt(textBoxResourceIndex.Text, Settings.ResourceIndex);
            Settings.ExposureTime = numericExposure.Value;
            Settings.Gain = numericGain.Value;
            Settings.Length = decimal.ToInt32(numericLength.Value);
            Settings.RollingCaptureEnabled = checkBoxRollingCaptureEnabled.Checked;
            Settings.RollingCaptureFrameCount = decimal.ToInt32(numericRollingCaptureFrameCount.Value);
            Settings.RollingCaptureDirection = (RollingCaptureDirection)Math.Max(0, comboBoxRollingCaptureDirection.SelectedIndex);
            Settings.InternalLineRate = numericInternalLineRate.Value;
            Settings.TriggerMode = (TriggerMode)Math.Max(0, comboBoxTriggerMode.SelectedIndex);
            Settings.ExternalFrameTriggerOneFrame = Settings.TriggerMode != TriggerMode.SoftwareTrigger &&
                checkBoxExternalFrameTriggerOneFrame.Checked;
            Settings.ExternalFrameTriggerOneFrameCompareFromEncoder = checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Enabled &&
                checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Checked;
            Settings.ExternalFrameTriggerOneFrameSetEncoderOnTrigger = checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Enabled &&
                checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Checked;
            Settings.AutoConnect = checkBoxAutoConnect.Checked;
            Settings.AutoSaveOnExternalTriggerOneFrame = checkBoxAutoSaveOnExternalTriggerOneFrame.Checked;
            Settings.AutoSaveOnSoftwareTriggerFrame = checkBoxAutoSaveOnSoftwareTriggerFrame.Checked;
            Settings.ImageSaveFormat = GetImageSaveFormatFromIndex(comboBoxImageSaveFormat.SelectedIndex);
        }

        private void comboBoxTriggerMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTriggerModeControls();
        }

        private void checkBoxExternalFrameTriggerOneFrame_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTriggerModeControls();
        }

        private void checkBoxExternalFrameTriggerOneFrameCompareFromEncoder_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTriggerModeControls();
        }

        private void UpdateTriggerModeControls()
        {
            var triggerMode = (TriggerMode)Math.Max(0, comboBoxTriggerMode.SelectedIndex);
            var isSoftwareTrigger = triggerMode == TriggerMode.SoftwareTrigger;
            var canUseExternalFrameTrigger = triggerMode == TriggerMode.Continuous ||
                triggerMode == TriggerMode.ExternalTrigger;
            var canUseCompareSetOnTrigger = triggerMode == TriggerMode.ExternalTrigger;

            checkBoxExternalFrameTriggerOneFrame.Enabled = canUseExternalFrameTrigger;
            if (isSoftwareTrigger)
            {
                checkBoxExternalFrameTriggerOneFrame.Checked = false;
            }
            else if (!canUseExternalFrameTrigger)
            {
                checkBoxExternalFrameTriggerOneFrame.Checked = false;
            }

            checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Enabled =
                canUseCompareSetOnTrigger && checkBoxExternalFrameTriggerOneFrame.Checked;
            if (!checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Enabled)
            {
                checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Checked = false;
            }

            checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Enabled =
                checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Enabled &&
                checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Checked;
            if (!checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Enabled)
            {
                checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Checked = false;
            }

            checkBoxAutoSaveOnSoftwareTriggerFrame.Enabled = isSoftwareTrigger;
        }

        private static int GetImageSaveFormatIndex(ImageSaveFormat saveFormat)
        {
            switch (saveFormat)
            {
                case ImageSaveFormat.Tif:
                    return 1;
                case ImageSaveFormat.UncompressedTif:
                    return 2;
                default:
                    return 0;
            }
        }

        private static ImageSaveFormat GetImageSaveFormatFromIndex(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 1:
                    return ImageSaveFormat.Tif;
                case 2:
                    return ImageSaveFormat.UncompressedTif;
                default:
                    return ImageSaveFormat.Png;
            }
        }

        private static Dictionary<string, string> LoadCcfValues(string filePath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                {
                    continue;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim();
                if (key.Length > 0)
                {
                    values[key] = value;
                }
            }

            return values;
        }

        private static decimal ClampToNumericRange(NumericUpDown control, decimal value)
        {
            if (value < control.Minimum)
            {
                return control.Minimum;
            }

            if (value > control.Maximum)
            {
                return control.Maximum;
            }

            return value;
        }

        private static bool TryReadDecimal(Dictionary<string, string> values, out decimal result, params string[] keys)
        {
            foreach (var value in EnumerateCandidateValues(values, keys))
            {
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
                    decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
                {
                    return true;
                }
            }

            result = 0;
            return false;
        }

        private static bool TryReadInt(Dictionary<string, string> values, out int result, params string[] keys)
        {
            foreach (var value in EnumerateCandidateValues(values, keys))
            {
                if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
                    int.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
                {
                    return true;
                }
            }

            result = 0;
            return false;
        }

        private static bool TryReadTriggerMode(Dictionary<string, string> values, out TriggerMode mode)
        {
            foreach (var value in EnumerateCandidateValues(values, "TriggerMode", "TriggerSource", "LineTriggerMode", "FrameTriggerMode"))
            {
                var normalized = value.Trim().ToLowerInvariant();
                if (normalized.Contains("line1") || normalized.Contains("input1") || normalized.Contains("external") || normalized.Contains("hardware"))
                {
                    mode = TriggerMode.ExternalTrigger;
                    return true;
                }

                if (normalized.Contains("software"))
                {
                    mode = TriggerMode.SoftwareTrigger;
                    return true;
                }

                if (normalized == "off" || normalized.Contains("continuous") || normalized.Contains("free"))
                {
                    mode = TriggerMode.Continuous;
                    return true;
                }
            }

            mode = TriggerMode.Continuous;
            return false;
        }

        private static IEnumerable<string> EnumerateCandidateValues(Dictionary<string, string> values, params string[] keys)
        {
            foreach (var key in keys)
            {
                string exactValue;
                if (values.TryGetValue(key, out exactValue))
                {
                    yield return exactValue;
                }

                foreach (var pair in values.Where(pair => pair.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    yield return pair.Value;
                }
            }
        }

        private static int ParseInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) ? parsed : fallback;
        }
    }
}
