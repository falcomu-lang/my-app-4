using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private readonly CameraDeviceSettingsIniService cameraDeviceSettingsIniService;
        private CameraDeviceSettings cameraDeviceSettings;
        private ConnectionControllerSettingsService connectionControllerSettingsService;
        private ConnectionControllerSettings connectionControllerSettings;
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
            var connectionControllerPage = new TabPage("連線總控參數")
            {
                AutoScroll = true,
                Padding = new Padding(12)
            };
            connectionControllerSettingsService = new ConnectionControllerSettingsService(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "connection-controller-settings.xml"));
            try
            {
                connectionControllerSettings = connectionControllerSettingsService.Load();
            }
            catch (Exception exception)
            {
                connectionControllerSettings = new ConnectionControllerSettings();
                MessageBox.Show(
                    this,
                    "無法讀取連線總控設定，將以預設值顯示；原設定檔不會被自動覆寫。\r\n" + exception.Message,
                    "全域設定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            connectionControllerPage.Controls.Add(CreateConnectionControllerSettingsPanel());
            tabs.TabPages.Add(lineScanPage);
            tabs.TabPages.Add(internalParameterPage);
            tabs.TabPages.Add(connectionControllerPage);
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
                Height = 168,
                Text = "連線設定",
                Padding = new Padding(10)
            };
            var localConnectionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(4)
            };
            localConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            localConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            localConnectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            localConnectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            localConnectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            localConnectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var listenIpComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Width = 240,
                Anchor = AnchorStyles.Left
            };
            PopulateLocalIpv4Addresses(listenIpComboBox);
            listenIpComboBox.Text = cameraDeviceSettings == null ||
                string.IsNullOrWhiteSpace(cameraDeviceSettings.ListenIpAddress)
                ? "0.0.0.0"
                : cameraDeviceSettings.ListenIpAddress;
            var listenPortBox = new NumericUpDown
            {
                Minimum = 1024,
                Maximum = 65535,
                Value = cameraDeviceSettings == null
                    ? 5000
                    : Math.Max(1024, Math.Min(65535, cameraDeviceSettings.ListenPort)),
                Width = 130,
                Anchor = AnchorStyles.Left
            };
            var localConnectionApplyButton = new Button
            {
                Text = "套用本機連線設定",
                Width = 160,
                Height = 28,
                Anchor = AnchorStyles.Right
            };
            var localConnectionNote = new Label
            {
                AutoSize = true,
                Text = "只保存程式監聽位址，不會修改 Windows 網卡 IP。",
                ForeColor = Color.FromArgb(92, 101, 116),
                Anchor = AnchorStyles.Left
            };
            localConnectionLayout.Controls.Add(CreateCameraPrecisionLabel("本機監聽 IP"), 0, 0);
            localConnectionLayout.Controls.Add(listenIpComboBox, 1, 0);
            localConnectionLayout.Controls.Add(CreateCameraPrecisionLabel("監聽埠"), 0, 1);
            localConnectionLayout.Controls.Add(listenPortBox, 1, 1);
            localConnectionLayout.Controls.Add(localConnectionApplyButton, 1, 2);
            localConnectionLayout.Controls.Add(localConnectionNote, 0, 3);
            localConnectionLayout.SetColumnSpan(localConnectionNote, 2);
            connectionSettingsGroup.Controls.Add(localConnectionLayout);
            var stationIdentityGroup = new GroupBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Left = 0,
                Top = 528,
                Width = 480,
                Height = 94,
                Text = "站點識別",
                Padding = new Padding(10)
            };
            var stationIdentityLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(4)
            };
            stationIdentityLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            stationIdentityLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            stationIdentityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            stationIdentityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var softwareIdComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            for (int softwareId = 1; softwareId <= 10; softwareId++)
            {
                softwareIdComboBox.Items.Add(softwareId.ToString(CultureInfo.InvariantCulture));
            }
            int selectedSoftwareId = cameraDeviceSettings == null
                ? 1
                : Math.Max(1, Math.Min(10, cameraDeviceSettings.SoftwareId));
            softwareIdComboBox.SelectedIndex = selectedSoftwareId - 1;
            var softwareIdApplyButton = new Button
            {
                Text = "套用軟體 ID",
                Width = 128,
                Height = 28,
                Anchor = AnchorStyles.Right
            };
            stationIdentityLayout.Controls.Add(CreateCameraPrecisionLabel("軟體 ID (1-10)"), 0, 0);
            stationIdentityLayout.Controls.Add(softwareIdComboBox, 1, 0);
            stationIdentityLayout.Controls.Add(softwareIdApplyButton, 1, 1);
            stationIdentityGroup.Controls.Add(stationIdentityLayout);

            var pathLabel = new Label
            {
                AutoSize = true,
                Text = "設定檔：config/device-settings.ini",
                ForeColor = Color.FromArgb(92, 101, 116),
                Padding = new Padding(0, 4, 0, 0)
            };
            var container = new Panel { Dock = DockStyle.Top, Height = 648 };
            container.Controls.Add(cameraPrecisionGroup);
            container.Controls.Add(programNameGroup);
            container.Controls.Add(connectionSettingsGroup);
            container.Controls.Add(stationIdentityGroup);
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
                    stationIdentityGroup.Width = groupWidth;
                }
                pathLabel.Location = new Point(4, stationIdentityGroup.Bottom + 2);
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

            localConnectionApplyButton.Click += delegate
            {
                IPAddress parsedAddress;
                if (!IPAddress.TryParse(listenIpComboBox.Text.Trim(), out parsedAddress) ||
                    parsedAddress.AddressFamily != AddressFamily.InterNetwork)
                {
                    MessageBox.Show(
                        this,
                        "請輸入有效的 IPv4 位址，例如 192.168.1.20，或使用 0.0.0.0 監聽所有網卡。",
                        "本機連線設定",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                CameraDeviceSettings updated = CreateCameraDeviceSettingsCopy();
                updated.ListenIpAddress = parsedAddress.ToString();
                updated.ListenPort = Decimal.ToInt32(listenPortBox.Value);
                try
                {
                    SaveCameraDeviceSettings(updated);
                    statusLabel.Text = "已保存本機監聽端點：" + updated.ListenIpAddress + ":" +
                        updated.ListenPort.ToString(CultureInfo.InvariantCulture) +
                        "（目前尚未啟用網路服務）";
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法保存本機連線設定：" + exception.Message,
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

            softwareIdApplyButton.Click += delegate
            {
                CameraDeviceSettings updated = CreateCameraDeviceSettingsCopy();
                updated.SoftwareId = softwareIdComboBox.SelectedIndex + 1;
                try
                {
                    SaveCameraDeviceSettings(updated);
                    statusLabel.Text = "已套用站點軟體 ID：" +
                        updated.SoftwareId.ToString(CultureInfo.InvariantCulture);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法保存軟體 ID：" + exception.Message,
                        "全域設定",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };
            return container;
        }

        private Control CreateConnectionControllerSettingsPanel()
        {
            if (connectionControllerSettings == null)
            {
                connectionControllerSettings = new ConnectionControllerSettings();
            }

            var container = new Panel { Dock = DockStyle.Top, Height = 610 };
            var descriptionLabel = new Label
            {
                AutoSize = false,
                Location = new Point(4, 2),
                Height = 38,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = "總控端主動連線至被控站；此頁目前只保存參數，尚未啟動 TCP/IP 通訊。"
            };
            var behaviorGroup = new GroupBox
            {
                Text = "連線行為",
                Location = new Point(0, 42),
                Size = new Size(900, 154),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(10)
            };
            var behaviorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(4)
            };
            behaviorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            behaviorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            behaviorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            behaviorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            behaviorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            behaviorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            behaviorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            var connectWhenOpenedCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "進入總控頁面時自動連線",
                Checked = connectionControllerSettings.ConnectWhenOpened,
                Anchor = AnchorStyles.Left
            };
            var autoReconnectCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "斷線後自動重連",
                Checked = connectionControllerSettings.AutoReconnect,
                Anchor = AnchorStyles.Left
            };
            NumericUpDown connectTimeoutBox = CreateControllerIntegerInput(
                connectionControllerSettings.ConnectTimeoutSeconds, 1, 60);
            NumericUpDown reconnectIntervalBox = CreateControllerIntegerInput(
                connectionControllerSettings.ReconnectIntervalSeconds, 1, 300);
            NumericUpDown heartbeatTimeoutBox = CreateControllerIntegerInput(
                connectionControllerSettings.HeartbeatTimeoutSeconds, 3, 300);
            behaviorLayout.Controls.Add(connectWhenOpenedCheckBox, 0, 0);
            behaviorLayout.SetColumnSpan(connectWhenOpenedCheckBox, 2);
            behaviorLayout.Controls.Add(autoReconnectCheckBox, 2, 0);
            behaviorLayout.SetColumnSpan(autoReconnectCheckBox, 2);
            behaviorLayout.Controls.Add(CreateCameraPrecisionLabel("連線逾時 (秒)"), 0, 1);
            behaviorLayout.Controls.Add(connectTimeoutBox, 1, 1);
            behaviorLayout.Controls.Add(CreateCameraPrecisionLabel("重連間隔 (秒)"), 2, 1);
            behaviorLayout.Controls.Add(reconnectIntervalBox, 3, 1);
            behaviorLayout.Controls.Add(CreateCameraPrecisionLabel("心跳逾時 (秒)"), 0, 2);
            behaviorLayout.Controls.Add(heartbeatTimeoutBox, 1, 2);
            behaviorGroup.Controls.Add(behaviorLayout);

            var stationsGroup = new GroupBox
            {
                Text = "被控站點 (最多 10 個；ID 不可重複)",
                Location = new Point(0, 204),
                Size = new Size(900, 350),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(8)
            };
            var stationContentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(2)
            };
            stationContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            stationContentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var stationGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersHeight = 30,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            stationGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Enabled",
                HeaderText = "啟用",
                FillWeight = 45,
                MinimumWidth = 52
            });
            var stationIdColumn = new DataGridViewComboBoxColumn
            {
                Name = "SoftwareId",
                HeaderText = "站點 ID",
                FillWeight = 55,
                MinimumWidth = 72,
                FlatStyle = FlatStyle.Flat
            };
            for (int softwareId = 1; softwareId <= 10; softwareId++)
            {
                stationIdColumn.Items.Add(softwareId);
            }
            stationGrid.Columns.Add(stationIdColumn);
            stationGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "站點名稱",
                FillWeight = 120,
                MinimumWidth = 110
            });
            stationGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IpAddress",
                HeaderText = "被控站 IP",
                FillWeight = 120,
                MinimumWidth = 130
            });
            stationGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Port",
                HeaderText = "通訊埠",
                FillWeight = 60,
                MinimumWidth = 75
            });
            stationGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            foreach (ConnectionControllerStation station in connectionControllerSettings.Stations)
            {
                AddConnectionControllerStationRow(stationGrid, station);
            }

            var stationActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var addStationButton = new Button { Text = "新增站點", Width = 100, Height = 28 };
            var removeStationButton = new Button { Text = "移除站點", Width = 100, Height = 28 };
            stationActions.Controls.Add(removeStationButton);
            stationActions.Controls.Add(addStationButton);
            stationContentLayout.Controls.Add(stationGrid, 0, 0);
            stationContentLayout.Controls.Add(stationActions, 0, 1);
            stationsGroup.Controls.Add(stationContentLayout);

            var settingsStatusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(4, 568),
                Size = new Size(560, 30),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            var saveControllerSettingsButton = new Button
            {
                Text = "保存連線總控參數",
                Location = new Point(680, 566),
                Size = new Size(180, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };

            container.Controls.Add(descriptionLabel);
            container.Controls.Add(behaviorGroup);
            container.Controls.Add(stationsGroup);
            container.Controls.Add(settingsStatusLabel);
            container.Controls.Add(saveControllerSettingsButton);
            Action updateWidths = delegate
            {
                int width = Math.Max(500, container.ClientSize.Width - 8);
                behaviorGroup.Width = width;
                stationsGroup.Width = width;
                saveControllerSettingsButton.Left = Math.Max(4, width - saveControllerSettingsButton.Width);
                settingsStatusLabel.Width = Math.Max(100, saveControllerSettingsButton.Left - 16);
                descriptionLabel.Width = width;
            };
            container.Resize += delegate { updateWidths(); };
            updateWidths();

            addStationButton.Click += delegate
            {
                if (stationGrid.Rows.Count >= 10)
                {
                    MessageBox.Show(this, "最多可設定 10 個被控站點。", "連線總控參數",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int nextId = FindAvailableControllerStationId(stationGrid);
                if (nextId < 1)
                {
                    MessageBox.Show(this, "1-10 的站點 ID 已用完，請先移除或修改既有站點。", "連線總控參數",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                stationGrid.Rows.Add(false, nextId, "站點 " + nextId, string.Empty, "5000");
            };
            removeStationButton.Click += delegate
            {
                if (stationGrid.SelectedRows.Count > 0)
                {
                    stationGrid.Rows.Remove(stationGrid.SelectedRows[0]);
                }
            };
            saveControllerSettingsButton.Click += delegate
            {
                stationGrid.EndEdit();
                if (stationGrid.Rows.Count > 10)
                {
                    ShowControllerSettingsValidationError("最多只能設定 10 個被控站點。");
                    return;
                }

                var updated = new ConnectionControllerSettings
                {
                    ConnectWhenOpened = connectWhenOpenedCheckBox.Checked,
                    AutoReconnect = autoReconnectCheckBox.Checked,
                    ConnectTimeoutSeconds = Decimal.ToInt32(connectTimeoutBox.Value),
                    ReconnectIntervalSeconds = Decimal.ToInt32(reconnectIntervalBox.Value),
                    HeartbeatTimeoutSeconds = Decimal.ToInt32(heartbeatTimeoutBox.Value)
                };
                var configuredIds = new HashSet<int>();
                var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int rowIndex = 0; rowIndex < stationGrid.Rows.Count; rowIndex++)
                {
                    DataGridViewRow row = stationGrid.Rows[rowIndex];
                    bool enabled = Convert.ToBoolean(row.Cells["Enabled"].Value ?? false);
                    int id;
                    if (!int.TryParse(Convert.ToString(row.Cells["SoftwareId"].Value, CultureInfo.InvariantCulture),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || id < 1 || id > 10)
                    {
                        ShowControllerSettingsValidationError("每個站點都必須指定 1-10 的站點 ID。");
                        return;
                    }
                    if (!configuredIds.Add(id))
                    {
                        ShowControllerSettingsValidationError("被控站點 ID 不可重複：" + id + "。");
                        return;
                    }

                    string name = Convert.ToString(row.Cells["Name"].Value) ?? string.Empty;
                    string ipText = (Convert.ToString(row.Cells["IpAddress"].Value) ?? string.Empty).Trim();
                    int port;
                    if (!int.TryParse(Convert.ToString(row.Cells["Port"].Value), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out port))
                    {
                        ShowControllerSettingsValidationError("站點「" + id + "」的通訊埠必須是數字。");
                        return;
                    }
                    if (port < 1024 || port > 65535)
                    {
                        ShowControllerSettingsValidationError("站點「" + id + "」的通訊埠需介於 1024-65535。");
                        return;
                    }
                    if (enabled)
                    {
                        IPAddress ipAddress;
                        if (!IPAddress.TryParse(ipText, out ipAddress) ||
                            ipAddress.AddressFamily != AddressFamily.InterNetwork ||
                            IPAddress.IsLoopback(ipAddress) || ipAddress.Equals(IPAddress.Any))
                        {
                            ShowControllerSettingsValidationError("啟用中的站點「" + id + "」需填入可連線的 IPv4 位址。");
                            return;
                        }
                        string endpoint = ipAddress + ":" + port.ToString(CultureInfo.InvariantCulture);
                        if (!endpoints.Add(endpoint))
                        {
                            ShowControllerSettingsValidationError("被控站點 IP 與通訊埠不可重複：" + endpoint + "。");
                            return;
                        }
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = "站點 " + id;
                        }
                    }
                    updated.Stations.Add(new ConnectionControllerStation
                    {
                        Enabled = enabled,
                        SoftwareId = id,
                        Name = name.Trim(),
                        IpAddress = ipText,
                        Port = port
                    });
                }

                try
                {
                    connectionControllerSettingsService.Save(updated);
                    connectionControllerSettings = updated;
                    settingsStatusLabel.Text = "參數已保存；目前尚未建立任何網路連線。";
                    statusLabel.Text = "已保存連線總控參數。";
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "無法保存連線總控參數：" + exception.Message,
                        "全域設定", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            return container;
        }

        private void ShowControllerSettingsValidationError(string message)
        {
            MessageBox.Show(this, message, "連線總控參數", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private int FindAvailableControllerStationId(DataGridView stationGrid)
        {
            var usedIds = new HashSet<int>();
            for (int rowIndex = 0; rowIndex < stationGrid.Rows.Count; rowIndex++)
            {
                int id;
                if (int.TryParse(Convert.ToString(stationGrid.Rows[rowIndex].Cells["SoftwareId"].Value), out id))
                {
                    usedIds.Add(id);
                }
            }

            int localId = cameraDeviceSettings == null ? 1 : cameraDeviceSettings.SoftwareId;
            for (int id = 1; id <= 10; id++)
            {
                if (id != localId && !usedIds.Contains(id))
                {
                    return id;
                }
            }
            return -1;
        }

        private static void AddConnectionControllerStationRow(
            DataGridView stationGrid,
            ConnectionControllerStation station)
        {
            if (station == null)
            {
                return;
            }

            int id = Math.Max(1, Math.Min(10, station.SoftwareId));
            int port = Math.Max(1024, Math.Min(65535, station.Port));
            stationGrid.Rows.Add(
                station.Enabled,
                id,
                station.Name ?? string.Empty,
                station.IpAddress ?? string.Empty,
                port.ToString(CultureInfo.InvariantCulture));
        }

        private static NumericUpDown CreateControllerIntegerInput(int value, int minimum, int maximum)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Max(minimum, Math.Min(maximum, value)),
                Width = 100,
                Anchor = AnchorStyles.Left
            };
        }

        private static void PopulateLocalIpv4Addresses(ComboBox comboBox)
        {
            comboBox.Items.Add("0.0.0.0");
            try
            {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation addressInfo in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress address = addressInfo.Address;
                        if (address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(address) && !comboBox.Items.Contains(address.ToString()))
                        {
                            comboBox.Items.Add(address.ToString());
                        }
                    }
                }
            }
            catch (NetworkInformationException)
            {
            }
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
                    : cameraDeviceSettings.ProgramName,
                SoftwareId = cameraDeviceSettings == null
                    ? 1
                    : cameraDeviceSettings.SoftwareId,
                ListenIpAddress = cameraDeviceSettings == null
                    ? "0.0.0.0"
                    : cameraDeviceSettings.ListenIpAddress,
                ListenPort = cameraDeviceSettings == null
                    ? 5000
                    : cameraDeviceSettings.ListenPort
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
