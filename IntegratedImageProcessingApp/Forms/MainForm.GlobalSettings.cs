using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private readonly CameraDeviceSettingsIniService cameraDeviceSettingsIniService;
        private CameraDeviceSettings cameraDeviceSettings;
        private TabControl globalSettingsTabControl;
        private CameraCaptureHostControl cameraCaptureHostControl;
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
            cameraCaptureHostControl = new CameraCaptureHostControl
            {
                Dock = DockStyle.Fill,
                Name = "cameraCaptureHostControl"
            };
            cameraCaptureHostControl.ImageAcquired += CameraCaptureHostControl_ImageAcquired;
            lineScanPage.Controls.Add(cameraCaptureHostControl);

            var internalParameterPage = new TabPage("內部參數")
            {
                AutoScroll = true,
                Padding = new Padding(12)
            };
            internalParameterPage.Controls.Add(CreateInternalParametersPanel());
            tabs.TabPages.Add(lineScanPage);
            tabs.TabPages.Add(internalParameterPage);
            return tabs;
        }

        private Control CreateInternalParametersPanel()
        {
            var cameraPrecisionGroup = new GroupBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Width = 480,
                Height = 220,
                Text = "攝影機精度",
                Padding = new Padding(10)
            };
            var precisionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(4)
            };
            precisionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            precisionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            precisionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            precisionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            precisionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            precisionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

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
            var precisionApplyButton = new Button
            {
                Text = "套用內部參數",
                Width = 150,
                Height = 28,
                Anchor = AnchorStyles.Right
            };
            precisionLayout.Controls.Add(CreateCameraPrecisionLabel("攝影機 X 向精度 (mm/pixel)"), 0, 0);
            precisionLayout.Controls.Add(xPrecisionBox, 1, 0);
            precisionLayout.Controls.Add(CreateCameraPrecisionLabel("攝影機 Y 向精度 (mm/pixel)"), 0, 1);
            precisionLayout.Controls.Add(yPrecisionBox, 1, 1);
            precisionLayout.Controls.Add(conversionEnabled, 0, 2);
            precisionLayout.SetColumnSpan(conversionEnabled, 2);
            precisionLayout.Controls.Add(precisionApplyButton, 0, 3);
            precisionLayout.SetColumnSpan(precisionApplyButton, 2);
            cameraPrecisionGroup.Controls.Add(precisionLayout);

            var programNameGroup = new GroupBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Left = 0,
                Top = 228,
                Width = 480,
                Height = 116,
                Text = "取像程式名稱",
                Padding = new Padding(10)
            };
            var programNameLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(4)
            };
            programNameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            programNameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            programNameLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            programNameLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var programNameTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 48,
                Text = cameraDeviceSettings == null ? string.Empty : cameraDeviceSettings.ProgramName
            };
            var programNameApplyButton = new Button
            {
                Text = "套用程式名稱",
                Width = 128,
                Height = 28,
                Anchor = AnchorStyles.Right
            };
            programNameLayout.Controls.Add(CreateCameraPrecisionLabel("程式名稱"), 0, 0);
            programNameLayout.Controls.Add(programNameTextBox, 1, 0);
            programNameLayout.Controls.Add(programNameApplyButton, 1, 1);
            programNameGroup.Controls.Add(programNameLayout);

            var connectionSettingsGroup = new GroupBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Left = 0,
                Top = 352,
                Width = 480,
                Height = 82,
                Text = "連線設定",
                Padding = new Padding(10)
            };
            var pathLabel = new Label
            {
                AutoSize = true,
                Text = "設定檔：config/device-settings.ini",
                ForeColor = Color.FromArgb(92, 101, 116),
                Padding = new Padding(0, 4, 0, 0)
            };
            var container = new Panel { Dock = DockStyle.Top, Height = 466 };
            container.Controls.Add(cameraPrecisionGroup);
            container.Controls.Add(programNameGroup);
            container.Controls.Add(connectionSettingsGroup);
            container.Controls.Add(pathLabel);
            Action updateGroupWidth = delegate
            {
                int availableWidth = container.ClientSize.Width;
                if (availableWidth > 0)
                {
                    int groupWidth = Math.Min(availableWidth, Math.Max(410, availableWidth / 3));
                    cameraPrecisionGroup.Width = groupWidth;
                    programNameGroup.Width = groupWidth;
                    connectionSettingsGroup.Width = groupWidth;
                }
                pathLabel.Location = new Point(4, connectionSettingsGroup.Bottom + 2);
            };
            container.Resize += delegate { updateGroupWidth(); };
            updateGroupWidth();

            precisionApplyButton.Click += delegate
            {
                CameraDeviceSettings updated = CreateCameraDeviceSettingsCopy();
                updated.XMillimetersPerPixel = Decimal.ToDouble(xPrecisionBox.Value);
                updated.YMillimetersPerPixel = Decimal.ToDouble(yPrecisionBox.Value);
                updated.MillimeterConversionEnabled = conversionEnabled.Checked;
                try
                {
                    SaveCameraDeviceSettings(updated);
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

            programNameApplyButton.Click += delegate
            {
                CameraDeviceSettings updated = CreateCameraDeviceSettingsCopy();
                updated.ProgramName = (programNameTextBox.Text ?? string.Empty).Trim();
                try
                {
                    SaveCameraDeviceSettings(updated);
                    statusLabel.Text = string.IsNullOrWhiteSpace(updated.ProgramName)
                        ? "已清除自訂取像程式名稱，使用預設名稱"
                        : "已套用取像程式名稱：" + updated.ProgramName;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法保存取像程式名稱：" + exception.Message,
                        "全域設定",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };
            return container;
        }

        private CameraDeviceSettings CreateCameraDeviceSettingsCopy()
        {
            return new CameraDeviceSettings
            {
                XMillimetersPerPixel = GetCameraXMillimetersPerPixel(),
                YMillimetersPerPixel = GetCameraYMillimetersPerPixel(),
                MillimeterConversionEnabled = IsCameraMillimeterConversionEnabled(),
                ProgramName = cameraDeviceSettings == null
                    ? string.Empty
                    : cameraDeviceSettings.ProgramName
            };
        }

        private void SaveCameraDeviceSettings(CameraDeviceSettings settings)
        {
            cameraDeviceSettingsIniService.Save(settings);
            cameraDeviceSettings = settings;
            UpdateProgramNameHeader();
        }

        private void UpdateProgramNameHeader()
        {
            if (projectLabel == null || topBarPanel == null)
            {
                return;
            }

            string programName = cameraDeviceSettings == null
                ? string.Empty
                : cameraDeviceSettings.ProgramName;
            projectLabel.Text = string.IsNullOrWhiteSpace(programName)
                ? "影像處理介面框架"
                : programName.Trim();
            projectLabel.AutoSize = false;
            projectLabel.AutoEllipsis = true;
            projectLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            projectLabel.TextAlign = ContentAlignment.MiddleLeft;
            projectLabel.Height = 20;
            projectLabel.Width = Math.Max(0, topBarPanel.ClientSize.Width - projectLabel.Left - 16);
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
