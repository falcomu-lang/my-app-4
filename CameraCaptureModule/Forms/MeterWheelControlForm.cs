using System;
using System.Globalization;
using System.Windows.Forms;
using CameraCaptureApp.Models;
using CameraCaptureApp.Native;
using CameraCaptureApp.Services;

namespace CameraCaptureApp.Forms
{
    public partial class MeterWheelControlForm : Form
    {
        private readonly Lsi8181MeterWheelService _meterWheelService;
        private readonly ISettingsService _settingsService;
        private CameraSettings _settings;
        private readonly bool _ownsMeterWheelService;
        private bool _loadingSettingsToUi;
        private MeterWheelExtensionCompareForm _extensionCompareForm;

        public MeterWheelControlForm()
            : this(CameraSettings.CreateDefault(), null, new Lsi8181MeterWheelService())
        {
            _ownsMeterWheelService = true;
        }

        internal MeterWheelControlForm(
            CameraSettings settings,
            ISettingsService settingsService,
            Lsi8181MeterWheelService meterWheelService)
        {
            _settings = settings == null ? CameraSettings.CreateDefault() : settings;
            _settingsService = settingsService;
            _meterWheelService = meterWheelService ?? throw new ArgumentNullException("meterWheelService");
            InitializeComponent();
            LoadMeterWheelSettingsToUi();
            UpdateControlStates();
            if (_meterWheelService.IsInitialized)
            {
                labelStatus.Text = "Connected";
                timerRefresh.Start();
            }

            RefreshValues();
        }

        internal void UpdateSettings(CameraSettings settings)
        {
            SaveMeterWheelSettingsFromUi();
            _settings = settings == null ? CameraSettings.CreateDefault() : settings;
            SaveMeterWheelSettingsFromUi();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timerRefresh.Stop();
            timerRefresh.Dispose();
            if (_extensionCompareForm != null && !_extensionCompareForm.IsDisposed)
            {
                _extensionCompareForm.Close();
            }

            if (_ownsMeterWheelService)
            {
                _meterWheelService.Dispose();
            }

            base.OnFormClosed(e);
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (_meterWheelService.IsInitialized)
                {
                    timerRefresh.Stop();
                    _meterWheelService.Close();
                    labelStatus.Text = "Offline";
                }
                else
                {
                    _meterWheelService.Open(
                        (byte)comboCardId.SelectedIndex,
                        GetSelectedMultipleRate(),
                        (int)numericIncrement.Value,
                        (ushort)numericCmpOutWidth.Value,
                        checkReverseDirection.Checked,
                        CreateExtensionCompareChannelsFromSettings(_settings));
                    SaveMeterWheelSettingsFromUi();
                    timerRefresh.Start();
                    labelStatus.Text = "Connected";
                }

                UpdateControlStates();
                RefreshValues();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void buttonClearEncoder_Click(object sender, EventArgs e)
        {
            SetEncoderValue(0);
        }

        private void numericEncoder_ValueChanged(object sender, EventArgs e)
        {
            SaveMeterWheelSettingsFromUi();
        }

        private void buttonSetEncoder_Click(object sender, EventArgs e)
        {
            SetEncoderValue((int)numericEncoder.Value);
        }

        private void buttonClearCompare_Click(object sender, EventArgs e)
        {
            SetCompareValue(0);
        }

        private void numericCompare_ValueChanged(object sender, EventArgs e)
        {
            SaveMeterWheelSettingsFromUi();
        }

        private void buttonSetCompare_Click(object sender, EventArgs e)
        {
            SetCompareValue((int)numericCompare.Value);
        }

        private void buttonApplyIncrement_Click(object sender, EventArgs e)
        {
            SetCompareIncrement((int)numericIncrement.Value);
            SaveMeterWheelSettingsFromUi();
        }

        private void buttonSetMultipleRate_Click(object sender, EventArgs e)
        {
            SetMultipleRate(GetSelectedMultipleRate());
            SaveMeterWheelSettingsFromUi();
        }

        private void comboMultipleRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveMeterWheelSettingsFromUi();
        }

        private void checkReverseDirection_CheckedChanged(object sender, EventArgs e)
        {
            if (_meterWheelService.IsInitialized)
            {
                SetReverseDirection(checkReverseDirection.Checked);
            }

            SaveMeterWheelSettingsFromUi();
        }

        private void buttonSetCmpOutWidth_Click(object sender, EventArgs e)
        {
            SetCmpOutWidth((ushort)numericCmpOutWidth.Value);
            SaveMeterWheelSettingsFromUi();
        }

        private void numericCmpOutWidth_ValueChanged(object sender, EventArgs e)
        {
            SaveMeterWheelSettingsFromUi();
        }

        private void buttonExtensionCompare_Click(object sender, EventArgs e)
        {
            if (!_meterWheelService.IsInitialized)
            {
                return;
            }

            if (_extensionCompareForm != null && !_extensionCompareForm.IsDisposed)
            {
                _extensionCompareForm.Focus();
                return;
            }

            _extensionCompareForm = new MeterWheelExtensionCompareForm(_meterWheelService, _settings, _settingsService);
            _extensionCompareForm.FormClosed += extensionCompareForm_FormClosed;
            _extensionCompareForm.Show(this);
        }

        private void extensionCompareForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _extensionCompareForm = null;
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            RefreshValues();
        }

        private void RefreshValues()
        {
            if (!_meterWheelService.IsInitialized)
            {
                labelEncoderValue.Text = "0";
                labelCompareValue.Text = "0";
                return;
            }

            try
            {
                labelEncoderValue.Text = _meterWheelService.ReadEncoder().ToString();
                labelCompareValue.Text = _meterWheelService.ReadCompare().ToString();
            }
            catch (Exception ex)
            {
                timerRefresh.Stop();
                ShowError(ex);
            }
        }

        private void SetEncoderValue(int value)
        {
            try
            {
                _meterWheelService.SetEncoder(value);
                RefreshValues();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SetCompareValue(int value)
        {
            try
            {
                _meterWheelService.SetCompare(value);
                RefreshValues();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void UpdateControlStates()
        {
            var isConnected = _meterWheelService.IsInitialized;
            comboCardId.Enabled = !isConnected;
            buttonConnect.Text = isConnected ? "Disconnect" : "Connect";
            buttonClearEncoder.Enabled = isConnected;
            buttonSetEncoder.Enabled = isConnected;
            buttonClearCompare.Enabled = isConnected;
            buttonSetCompare.Enabled = isConnected;
            buttonApplyIncrement.Enabled = isConnected;
            buttonSetMultipleRate.Enabled = isConnected;
            checkReverseDirection.Enabled = isConnected;
            buttonSetCmpOutWidth.Enabled = isConnected;
            buttonExtensionCompare.Enabled = isConnected;

            if (!isConnected && _extensionCompareForm != null && !_extensionCompareForm.IsDisposed)
            {
                _extensionCompareForm.Close();
            }
        }

        private void SetCompareIncrement(int value)
        {
            try
            {
                _meterWheelService.SetCompareIncrement(value);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SetMultipleRate(byte multipleRate)
        {
            try
            {
                _meterWheelService.SetMultipleRate(multipleRate);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SetReverseDirection(bool reverseDirection)
        {
            try
            {
                _meterWheelService.SetReverseDirection(reverseDirection);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SetCmpOutWidth(ushort outWidth)
        {
            try
            {
                _meterWheelService.SetCmpOutWidth(outWidth);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private byte GetSelectedMultipleRate()
        {
            switch (comboMultipleRate.SelectedIndex)
            {
                case 1:
                    return Lsi8181Native.Multiple2;
                case 2:
                    return Lsi8181Native.Multiple1;
                default:
                    return Lsi8181Native.Multiple4;
            }
        }

        private void LoadMeterWheelSettingsToUi()
        {
            _loadingSettingsToUi = true;
            try
            {
                comboCardId.SelectedIndex = NormalizeCardId(_settings.MeterWheelCardId);
                numericIncrement.Value = ClampToNumericRange(numericIncrement, _settings.MeterWheelCompareIncrement);
                numericEncoder.Value = ClampToNumericRange(numericEncoder, _settings.MeterWheelEncoderValue);
                numericCompare.Value = ClampToNumericRange(numericCompare, _settings.MeterWheelCompareValue);
                numericCmpOutWidth.Value = ClampToNumericRange(numericCmpOutWidth, _settings.MeterWheelCmpOutWidth);
                comboMultipleRate.SelectedIndex = NormalizeMultipleRateIndex(_settings.MeterWheelMultipleRate);
                checkReverseDirection.Checked = _settings.MeterWheelReverseDirection;
            }
            finally
            {
                _loadingSettingsToUi = false;
            }
        }

        private void SaveMeterWheelSettingsFromUi()
        {
            if (_loadingSettingsToUi)
            {
                return;
            }

            _settings.MeterWheelCardId = comboCardId.SelectedIndex;
            _settings.MeterWheelCompareIncrement = (int)numericIncrement.Value;
            _settings.MeterWheelEncoderValue = (int)numericEncoder.Value;
            _settings.MeterWheelCompareValue = (int)numericCompare.Value;
            _settings.MeterWheelMultipleRate = comboMultipleRate.SelectedIndex;
            _settings.MeterWheelReverseDirection = checkReverseDirection.Checked;
            _settings.MeterWheelCmpOutWidth = (int)numericCmpOutWidth.Value;

            if (_settingsService != null)
            {
                _settingsService.Save(_settings);
            }
        }

        private static int NormalizeMultipleRateIndex(int value)
        {
            return value >= 0 && value <= 2 ? value : 0;
        }

        private static int NormalizeCardId(int value)
        {
            return value >= 0 && value <= Lsi8181Native.CardIdMax ? value : 0;
        }

        private static decimal ClampToNumericRange(NumericUpDown numeric, int value)
        {
            if (value < numeric.Minimum)
            {
                return numeric.Minimum;
            }

            if (value > numeric.Maximum)
            {
                return numeric.Maximum;
            }

            return value;
        }

        internal static Lsi8181ExtensionCompareChannel[] CreateExtensionCompareChannelsFromSettings(CameraSettings settings)
        {
            var channels = new Lsi8181ExtensionCompareChannel[8];
            var offsets = ParseIntList(settings.MeterWheelExtensionCompareOffsets, -32768, 32767);
            var pulseWidths = ParseIntList(settings.MeterWheelExtensionComparePulseWidths, 0, 65535);

            for (var index = 0; index < channels.Length; index++)
            {
                channels[index] = new Lsi8181ExtensionCompareChannel
                {
                    Channel = (byte)index,
                    Masked = (settings.MeterWheelExtensionCompareMask & (1 << index)) != 0,
                    OffsetCompare = (short)offsets[index],
                    PulseWidth = (ushort)pulseWidths[index],
                    OutputState = (settings.MeterWheelExtensionCompareOutputStates & (1 << index)) != 0
                };
            }

            return channels;
        }

        internal static void SaveExtensionCompareChannelsToSettings(
            CameraSettings settings,
            Lsi8181ExtensionCompareChannel[] channels,
            ISettingsService settingsService)
        {
            var mask = 0;
            var outputStates = 0;
            var offsets = new string[8];
            var pulseWidths = new string[8];

            for (var index = 0; index < 8; index++)
            {
                if (channels[index].Masked)
                {
                    mask |= 1 << index;
                }

                if (channels[index].OutputState)
                {
                    outputStates |= 1 << index;
                }

                offsets[index] = channels[index].OffsetCompare.ToString(CultureInfo.InvariantCulture);
                pulseWidths[index] = channels[index].PulseWidth.ToString(CultureInfo.InvariantCulture);
            }

            settings.MeterWheelExtensionCompareMask = mask;
            settings.MeterWheelExtensionCompareOffsets = string.Join(",", offsets);
            settings.MeterWheelExtensionComparePulseWidths = string.Join(",", pulseWidths);
            settings.MeterWheelExtensionCompareOutputStates = outputStates;

            if (settingsService != null)
            {
                settingsService.Save(settings);
            }
        }

        private static int[] ParseIntList(string value, int minimum, int maximum)
        {
            var result = new int[8];
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            var parts = value.Split(',');
            for (var index = 0; index < result.Length && index < parts.Length; index++)
            {
                int parsed;
                if (int.TryParse(parts[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    if (parsed < minimum)
                    {
                        parsed = minimum;
                    }
                    else if (parsed > maximum)
                    {
                        parsed = maximum;
                    }

                    result[index] = parsed;
                }
            }

            return result;
        }

        private void ShowError(Exception ex)
        {
            AppLogger.Log("Meter wheel control failed.", ex);
            labelStatus.Text = "Error";
            UpdateControlStates();
            MessageBox.Show(
                this,
                "Meter wheel operation failed.\r\n" + ex.Message + "\r\n\r\nMake sure LSI8181_64.dll and the driver are installed.",
                "Meter Wheel Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
