using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private readonly CameraDeviceSettingsIniService cameraDeviceSettingsIniService;
        private CameraDeviceSettings cameraDeviceSettings;
        private TabControl globalSettingsTabControl;
        private bool isGlobalSettingsActive;
        private SizeType[] globalSettingsPreviousColumnTypes;
        private float[] globalSettingsPreviousColumnWidths;

        private double GetCameraXMillimetersPerPixel()
        {
            return cameraDeviceSettings == null || cameraDeviceSettings.XMillimetersPerPixel <= 0.0
                ? 1.0
                : cameraDeviceSettings.XMillimetersPerPixel;
        }

        private double GetCameraYMillimetersPerPixel()
        {
            return cameraDeviceSettings == null || cameraDeviceSettings.YMillimetersPerPixel <= 0.0
                ? 1.0
                : cameraDeviceSettings.YMillimetersPerPixel;
        }

        private bool IsCameraMillimeterConversionEnabled()
        {
            return cameraDeviceSettings != null && cameraDeviceSettings.MillimeterConversionEnabled;
        }

        private void ShowGlobalSettingsPanel()
        {
            if (globalSettingsTabControl == null || globalSettingsTabControl.IsDisposed)
            {
                globalSettingsTabControl = CreateGlobalSettingsTabControl();
                centerPanel.Controls.Add(globalSettingsTabControl);
            }

            if (!isGlobalSettingsActive)
            {
                globalSettingsPreviousColumnTypes = new SizeType[mainLayoutPanel.ColumnStyles.Count];
                globalSettingsPreviousColumnWidths = new float[mainLayoutPanel.ColumnStyles.Count];
                for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count; index++)
                {
                    globalSettingsPreviousColumnTypes[index] = mainLayoutPanel.ColumnStyles[index].SizeType;
                    globalSettingsPreviousColumnWidths[index] = mainLayoutPanel.ColumnStyles[index].Width;
                }
            }

            isGlobalSettingsActive = true;
            mainLayoutPanel.SuspendLayout();
            try
            {
                mainLayoutPanel.ColumnStyles[0].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[0].Width = 20F;
                mainLayoutPanel.ColumnStyles[1].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[1].Width = 80F;
                mainLayoutPanel.ColumnStyles[2].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[2].Width = 0F;
                rightPanel.Visible = false;
                imageLayoutPanel.Visible = false;
                globalSettingsTabControl.Visible = true;
                globalSettingsTabControl.BringToFront();
                centerPanel.Visible = true;
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }
        }

        private void HideGlobalSettingsPanel()
        {
            if (!isGlobalSettingsActive)
            {
                return;
            }

            isGlobalSettingsActive = false;
            mainLayoutPanel.SuspendLayout();
            try
            {
                if (globalSettingsTabControl != null)
                {
                    globalSettingsTabControl.Visible = false;
                }

                imageLayoutPanel.Visible = true;
                rightPanel.Visible = true;
                if (globalSettingsPreviousColumnTypes != null &&
                    globalSettingsPreviousColumnWidths != null)
                {
                    for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count &&
                        index < globalSettingsPreviousColumnTypes.Length &&
                        index < globalSettingsPreviousColumnWidths.Length; index++)
                    {
                        mainLayoutPanel.ColumnStyles[index].SizeType = globalSettingsPreviousColumnTypes[index];
                        mainLayoutPanel.ColumnStyles[index].Width = globalSettingsPreviousColumnWidths[index];
                    }
                }

                centerPanel.Visible = !IsImageDisplayUpdateSuppressed;
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }
        }

        private TabControl CreateGlobalSettingsTabControl()
        {
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Name = "globalSettingsTabControl"
            };
            var lineScanPage = new TabPage("與線掃描取像結合");
            var lineScanStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "尚未設定線掃描取像設備",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(92, 101, 116)
            };
            lineScanPage.Controls.Add(lineScanStatus);

            var internalParameterPage = new TabPage("內部參數")
            {
                AutoScroll = true,
                Padding = new Padding(12)
            };
            internalParameterPage.Controls.Add(CreateCameraPrecisionSettingsPanel());
            tabs.TabPages.Add(lineScanPage);
            tabs.TabPages.Add(internalParameterPage);
            return tabs;
        }

        private Control CreateCameraPrecisionSettingsPanel()
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 220,
                Text = "攝影機精度",
                Padding = new Padding(10)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(4)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            NumericUpDown xPrecisionBox = CreateCameraPrecisionInput(
                cameraDeviceSettings == null ? 1.0 : cameraDeviceSettings.XMillimetersPerPixel);
            NumericUpDown yPrecisionBox = CreateCameraPrecisionInput(
                cameraDeviceSettings == null ? 1.0 : cameraDeviceSettings.YMillimetersPerPixel);
            var conversionEnabled = new CheckBox
            {
                AutoSize = true,
                Text = "啟用 mm 換算（未啟用時以 pixel 計算）",
                Checked = cameraDeviceSettings != null && cameraDeviceSettings.MillimeterConversionEnabled,
                Anchor = AnchorStyles.Left
            };
            var applyButton = new Button
            {
                Text = "套用內部參數",
                Width = 150,
                Height = 28,
                Anchor = AnchorStyles.Right
            };
            var pathLabel = new Label
            {
                AutoSize = true,
                Text = "設定檔：config/device-settings.ini",
                ForeColor = Color.FromArgb(92, 101, 116),
                Dock = DockStyle.Top,
                Padding = new Padding(4, 8, 0, 0)
            };

            layout.Controls.Add(CreateCameraPrecisionLabel("攝影機 X 向精度 (mm/pixel)"), 0, 0);
            layout.Controls.Add(xPrecisionBox, 1, 0);
            layout.Controls.Add(CreateCameraPrecisionLabel("攝影機 Y 向精度 (mm/pixel)"), 0, 1);
            layout.Controls.Add(yPrecisionBox, 1, 1);
            layout.Controls.Add(conversionEnabled, 0, 2);
            layout.SetColumnSpan(conversionEnabled, 2);
            layout.Controls.Add(applyButton, 0, 3);
            layout.SetColumnSpan(applyButton, 2);
            group.Controls.Add(layout);

            applyButton.Click += delegate
            {
                var updated = new CameraDeviceSettings
                {
                    XMillimetersPerPixel = Decimal.ToDouble(xPrecisionBox.Value),
                    YMillimetersPerPixel = Decimal.ToDouble(yPrecisionBox.Value),
                    MillimeterConversionEnabled = conversionEnabled.Checked
                };
                try
                {
                    cameraDeviceSettingsIniService.Save(updated);
                    cameraDeviceSettings = updated;
                    statusLabel.Text = "已套用全域攝影機精度：X " +
                        updated.XMillimetersPerPixel.ToString("0.######", CultureInfo.InvariantCulture) +
                        "、Y " +
                        updated.YMillimetersPerPixel.ToString("0.######", CultureInfo.InvariantCulture) +
                        " mm/pixel；尺寸量測以 " +
                        (updated.MillimeterConversionEnabled ? "mm" : "pixel") + " 計算";
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法保存全域攝影機設定：" + exception.Message,
                        "全域設定",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };

            var container = new Panel { Dock = DockStyle.Top, Height = 255 };
            container.Controls.Add(pathLabel);
            container.Controls.Add(group);
            return container;
        }

        private static Label CreateCameraPrecisionLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
        }

        private static NumericUpDown CreateCameraPrecisionInput(double value)
        {
            decimal numericValue = (decimal)Math.Max(0.000001, Math.Min(1000.0, value));
            return new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 220,
                Minimum = 0.000001m,
                Maximum = 1000m,
                DecimalPlaces = 6,
                Increment = 0.000001m,
                Value = numericValue
            };
        }
    }
}
