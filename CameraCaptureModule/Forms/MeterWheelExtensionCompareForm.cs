using System;
using System.Windows.Forms;
using CameraCaptureApp.Models;
using CameraCaptureApp.Services;

namespace CameraCaptureApp.Forms
{
    public partial class MeterWheelExtensionCompareForm : Form
    {
        private readonly Lsi8181MeterWheelService _meterWheelService;
        private readonly CameraSettings _settings;
        private readonly ISettingsService _settingsService;
        private CheckBox[] _maskChecks;
        private NumericUpDown[] _offsetValues;
        private NumericUpDown[] _pulseWidthValues;
        private CheckBox[] _outputStateChecks;
        private CheckBox[] _statusChecks;
        private bool _loading;

        public MeterWheelExtensionCompareForm()
        {
            InitializeComponent();
            InitializeControlArrays();
        }

        internal MeterWheelExtensionCompareForm(
            Lsi8181MeterWheelService meterWheelService,
            CameraSettings settings,
            ISettingsService settingsService)
        {
            _meterWheelService = meterWheelService ?? throw new ArgumentNullException("meterWheelService");
            _settings = settings ?? CameraSettings.CreateDefault();
            _settingsService = settingsService;

            InitializeComponent();
            InitializeControlArrays();
            LoadFromSettings();
            timerRefresh.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timerRefresh.Stop();
            base.OnFormClosed(e);
        }

        private void InitializeControlArrays()
        {
            _maskChecks = new[]
            {
                checkMask0, checkMask1, checkMask2, checkMask3,
                checkMask4, checkMask5, checkMask6, checkMask7
            };

            _offsetValues = new[]
            {
                numericOffset0, numericOffset1, numericOffset2, numericOffset3,
                numericOffset4, numericOffset5, numericOffset6, numericOffset7
            };

            _pulseWidthValues = new[]
            {
                numericPulseWidth0, numericPulseWidth1, numericPulseWidth2, numericPulseWidth3,
                numericPulseWidth4, numericPulseWidth5, numericPulseWidth6, numericPulseWidth7
            };

            _outputStateChecks = new[]
            {
                checkOutput0, checkOutput1, checkOutput2, checkOutput3,
                checkOutput4, checkOutput5, checkOutput6, checkOutput7
            };

            _statusChecks = new[]
            {
                checkStatus0, checkStatus1, checkStatus2, checkStatus3,
                checkStatus4, checkStatus5, checkStatus6, checkStatus7
            };
        }

        private void LoadFromSettings()
        {
            try
            {
                _loading = true;
                var channels = MeterWheelControlForm.CreateExtensionCompareChannelsFromSettings(_settings);
                for (var index = 0; index < channels.Length; index++)
                {
                    _maskChecks[index].Checked = channels[index].Masked;
                    _offsetValues[index].Value = channels[index].OffsetCompare;
                    _pulseWidthValues[index].Value = channels[index].PulseWidth;
                    _outputStateChecks[index].Checked = channels[index].OutputState;
                    UpdateOutputStateEnabled(index);
                }

                RefreshStatus();
                labelStatus.Text = "Loaded from settings";
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                _loading = false;
            }
        }

        private void ApplyToHardware()
        {
            var channels = new Lsi8181ExtensionCompareChannel[8];
            for (var index = 0; index < channels.Length; index++)
            {
                channels[index] = new Lsi8181ExtensionCompareChannel
                {
                    Channel = (byte)index,
                    Masked = _maskChecks[index].Checked,
                    OffsetCompare = (short)_offsetValues[index].Value,
                    PulseWidth = (ushort)_pulseWidthValues[index].Value,
                    OutputState = !_maskChecks[index].Checked && _outputStateChecks[index].Checked
                };
            }

            _meterWheelService.ApplyExtensionCompareChannels(channels);
            MeterWheelControlForm.SaveExtensionCompareChannelsToSettings(_settings, channels, _settingsService);
            labelStatus.Text = "Applied " + DateTime.Now.ToString("HH:mm:ss");
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            try
            {
                var values = _meterWheelService.ReadExtensionCompareStatus();
                for (var index = 0; index < values.Length; index++)
                {
                    _statusChecks[index].Checked = values[index];
                }
            }
            catch (Exception ex)
            {
                timerRefresh.Stop();
                ShowError(ex);
            }
        }

        private void UpdateOutputStateEnabled(int index)
        {
            var masked = _maskChecks[index].Checked;
            if (masked)
            {
                _outputStateChecks[index].Checked = false;
            }

            _outputStateChecks[index].Enabled = !masked;
        }

        private void checkMask_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading)
            {
                return;
            }

            for (var index = 0; index < _maskChecks.Length; index++)
            {
                if (ReferenceEquals(sender, _maskChecks[index]))
                {
                    UpdateOutputStateEnabled(index);
                    break;
                }
            }
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            RefreshStatus();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyToHardware();
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyToHardware();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowError(Exception ex)
        {
            AppLogger.Log("Extension compare control failed.", ex);
            labelStatus.Text = "Error";
            MessageBox.Show(
                this,
                "Extension compare operation failed.\r\n" + ex.Message,
                "Extension Compare Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
