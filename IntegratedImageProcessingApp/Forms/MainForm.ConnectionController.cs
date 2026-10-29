using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ConnectionControllerStationViewState
        {
            public ConnectionControllerStation Configuration;
            public bool TcpConnected;
            public bool CameraConnected;
            public bool IsCapturing;
            public bool IsInspecting;
            public bool JudgementComplete;
            public string Status;
            public string RecipeName;
            public DateTime? ResultAtUtc;
            public List<InspectionTcpObjectResult> Objects = new List<InspectionTcpObjectResult>();
            public Panel Card;
            public Label ConnectionLabel;
            public Label CameraLabel;
            public Label ResultLabel;
            public Label RecipeLabel;
            public Label StatusLabel;
            public Panel ObjectGridHost;
        }

        private sealed class ConnectionControllerPendingCommand
        {
            public int StationId;
            public string Command;
        }

        private InspectionTcpTransport inspectionTcpTransport;
        private System.Windows.Forms.Timer connectionControllerReconnectTimer;
        private Panel connectionControllerPanel;
        private Label connectionControllerSummaryLabel;
        private Label connectionControllerNetworkStatusLabel;
        private FlowLayoutPanel connectionControllerCardsHost;
        private Button connectionControllerConnectButton;
        private Button connectionControllerContinuousCaptureButton;
        private Button connectionControllerInspectButton;
        private readonly Dictionary<int, ConnectionControllerStationViewState>
            connectionControllerStationViews = new Dictionary<int, ConnectionControllerStationViewState>();
        private SizeType[] connectionControllerPreviousColumnTypes;
        private float[] connectionControllerPreviousColumnWidths;
        private bool isConnectionControllerPanelActive;
        private bool connectionControllerSettingsLoaded;
        private bool connectionControllerConnectRequested;
        private bool isConnectionControllerContinuousCaptureRequested;
        private bool isConnectionControllerInspectionRequested;
        private bool connectionControllerCommandInFlight;
        private readonly Dictionary<string, ConnectionControllerPendingCommand>
            connectionControllerPendingCommands =
                new Dictionary<string, ConnectionControllerPendingCommand>();
        private readonly Dictionary<int, string> connectionControllerLatestModeCommandIds =
            new Dictionary<int, string>();
        private readonly HashSet<int> connectionControllerCaptureStations = new HashSet<int>();
        private readonly HashSet<int> connectionControllerInspectionStations = new HashSet<int>();
        private bool remoteContinuousCaptureActive;
        private volatile bool remoteContinuousInspectionActive;
        private bool isRemoteControllerInspectionActive;
        private bool connectionControllerRecipeMetadataResolved;
        private string connectionControllerRecipeMetadataId;
        private string connectionControllerRecipeMetadataName;
        private int remoteInspectionRequested;
        private int connectionControllerConnectInFlight;
        private long lastStationStatusPublishUtcTicks;
        private long lastRemoteCameraSequence;
        private DateTime? lastCameraInspectionCompletedUtc;
        private bool isParameterResultReviewSnapshotForTcp;
        private DateTime? parameterResultReviewTcpResultAtUtc;
        private IPAddress inspectionTcpListenerAddress;
        private int inspectionTcpListenerPort;
        private bool inspectionTcpListenerActive;

        private void EnsureConnectionControllerSettingsLoaded()
        {
            if (connectionControllerSettingsLoaded)
            {
                return;
            }

            connectionControllerSettingsLoaded = true;
            connectionControllerSettingsService = new ConnectionControllerSettingsService(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "config", "connection-controller-settings.xml"));
            try
            {
                connectionControllerSettings = connectionControllerSettingsService.Load();
            }
            catch (Exception exception)
            {
                connectionControllerSettings = new ConnectionControllerSettings();
                if (!IsDisposed && IsHandleCreated)
                {
                    MessageBox.Show(this,
                        "無法讀取連線總控設定，將以預設值顯示；原設定檔不會被自動覆寫。\r\n" + exception.Message,
                        "全域設定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            if (connectionControllerSettings.Stations == null)
            {
                connectionControllerSettings.Stations = new List<ConnectionControllerStation>();
            }
        }

        private void InitializeInspectionTcpTransport()
        {
            EnsureConnectionControllerSettingsLoaded();
            inspectionTcpTransport = new InspectionTcpTransport();
            inspectionTcpTransport.MessageReceived += InspectionTcpTransport_MessageReceived;
            inspectionTcpTransport.PeerStateChanged += InspectionTcpTransport_PeerStateChanged;
            inspectionTcpTransport.SetHeartbeatTimeout(
                connectionControllerSettings.HeartbeatTimeoutSeconds);
            connectionControllerReconnectTimer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(1000, connectionControllerSettings.ReconnectIntervalSeconds * 1000)
            };
            connectionControllerReconnectTimer.Tick += ConnectionControllerReconnectTimer_Tick;
            if (components != null)
            {
                components.Add(connectionControllerReconnectTimer);
            }
            FormClosed += MainForm_ConnectionControllerFormClosed;
            ConfigureInspectionTcpListener(cameraDeviceSettings);
        }

        private void MainForm_ConnectionControllerFormClosed(object sender, FormClosedEventArgs e)
        {
            if (cameraCaptureHostControl != null && !cameraCaptureHostControl.IsDisposed)
            {
                cameraCaptureHostControl.RemoteImageCaptured -= CameraCaptureHostControl_RemoteImageCaptured;
            }
            if (inspectionTcpTransport != null)
            {
                inspectionTcpTransport.Dispose();
                inspectionTcpTransport = null;
            }
        }

        private void ConfigureInspectionTcpListener(CameraDeviceSettings settings)
        {
            if (inspectionTcpTransport == null)
            {
                return;
            }
            if (settings == null || !settings.AllowConnections)
            {
                if (inspectionTcpListenerActive)
                {
                    inspectionTcpTransport.StopListener();
                    inspectionTcpListenerActive = false;
                    inspectionTcpListenerAddress = null;
                    inspectionTcpListenerPort = 0;
                }
                QueueStationStatusPublish(true);
                return;
            }

            IPAddress address;
            string addressText = string.IsNullOrWhiteSpace(settings.ListenIpAddress)
                ? "0.0.0.0"
                : settings.ListenIpAddress.Trim();
            if (addressText == "0.0.0.0")
            {
                address = IPAddress.Any;
            }
            else if (!IPAddress.TryParse(addressText, out address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (inspectionTcpListenerActive)
                {
                    inspectionTcpTransport.StopListener();
                    inspectionTcpListenerActive = false;
                    inspectionTcpListenerAddress = null;
                    inspectionTcpListenerPort = 0;
                }
                SetConnectionControllerStatus("允許連線未啟動：本機監聽 IP 不是有效的 IPv4 位址。");
                return;
            }

            if (inspectionTcpListenerActive && inspectionTcpListenerPort == settings.ListenPort &&
                inspectionTcpListenerAddress.Equals(address))
            {
                return;
            }

            try
            {
                inspectionTcpTransport.StartListener(address, settings.ListenPort);
                inspectionTcpListenerAddress = address;
                inspectionTcpListenerPort = settings.ListenPort;
                inspectionTcpListenerActive = true;
                SetConnectionControllerStatus("已允許總控端連線：" + addressText + ":" +
                    settings.ListenPort.ToString());
            }
            catch (Exception exception)
            {
                inspectionTcpListenerActive = false;
                inspectionTcpListenerAddress = null;
                inspectionTcpListenerPort = 0;
                SetConnectionControllerStatus("無法啟動 TCP/IP 監聽：" + exception.Message);
                if (statusLabel != null && !statusLabel.IsDisposed)
                {
                    statusLabel.Text = "TCP/IP 監聽失敗：" + exception.Message;
                }
            }
        }

        private void ShowConnectionControllerPanel()
        {
            EnsureConnectionControllerSettingsLoaded();
            if (isInspectionOverviewActive)
            {
                ExitInspectionOverviewMode();
            }
            if (connectionControllerPanel == null || connectionControllerPanel.IsDisposed)
            {
                connectionControllerPanel = CreateConnectionControllerPanel();
                centerPanel.Controls.Add(connectionControllerPanel);
            }
            if (!isConnectionControllerPanelActive)
            {
                connectionControllerPreviousColumnTypes = mainLayoutPanel.ColumnStyles
                    .Cast<ColumnStyle>().Select(style => style.SizeType).ToArray();
                connectionControllerPreviousColumnWidths = mainLayoutPanel.ColumnStyles
                    .Cast<ColumnStyle>().Select(style => style.Width).ToArray();
                isConnectionControllerPanelActive = true;
            }

            mainLayoutPanel.SuspendLayout();
            try
            {
                SetMainLayoutColumnPercentages(false);
                rightPanel.Visible = false;
                imageLayoutPanel.Visible = false;
                centerPanel.Visible = true;
                connectionControllerPanel.Visible = true;
                connectionControllerPanel.BringToFront();
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }

            RefreshConnectionControllerCards();
            UpdateConnectionControllerNetworkStatus();
            UpdateConnectionControllerMonitoringControls();
            if (!connectionControllerSettings.MonitorCamera)
            {
                connectionControllerConnectRequested = false;
                if (connectionControllerReconnectTimer != null)
                {
                    connectionControllerReconnectTimer.Stop();
                }
                SetConnectionControllerStatus("攝影機監控未啟用；不會連線或監控被控站點。");
                return;
            }
            if (connectionControllerSettings.ConnectWhenOpened)
            {
                connectionControllerConnectRequested = true;
            }
            if (connectionControllerReconnectTimer != null)
            {
                connectionControllerReconnectTimer.Start();
                ConnectionControllerReconnectTimer_Tick(this, EventArgs.Empty);
            }
        }

        private void ExitConnectionControllerPanel()
        {
            if (!isConnectionControllerPanelActive)
            {
                return;
            }
            isConnectionControllerPanelActive = false;
            if (connectionControllerPanel != null)
            {
                connectionControllerPanel.Visible = false;
            }
            mainLayoutPanel.SuspendLayout();
            try
            {
                imageLayoutPanel.Visible = true;
                rightPanel.Visible = true;
                if (connectionControllerPreviousColumnTypes != null &&
                    connectionControllerPreviousColumnWidths != null)
                {
                    for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count &&
                        index < connectionControllerPreviousColumnTypes.Length &&
                        index < connectionControllerPreviousColumnWidths.Length; index++)
                    {
                        mainLayoutPanel.ColumnStyles[index].SizeType =
                            connectionControllerPreviousColumnTypes[index];
                        mainLayoutPanel.ColumnStyles[index].Width =
                            connectionControllerPreviousColumnWidths[index];
                    }
                }
                centerPanel.Visible = !IsImageDisplayUpdateSuppressed;
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }
        }

        private Panel CreateConnectionControllerPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(245, 247, 250),
                Visible = false,
                Name = "connectionControllerPanel"
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 53, 86),
                Margin = new Padding(0, 0, 0, 6)
            };
            header.Controls.Add(new Label
            {
                Text = "連線總控角色",
                Dock = DockStyle.Left,
                Width = 270,
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            });
            connectionControllerNetworkStatusLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 320,
                ForeColor = Color.FromArgb(220, 230, 240),
                Font = new Font("Microsoft JhengHei UI", 9F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 12, 0),
                AutoEllipsis = true
            };
            header.Controls.Add(connectionControllerNetworkStatusLabel);

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 0, 0, 6)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            connectionControllerSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(40, 50, 65),
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                AutoEllipsis = true
            };
            connectionControllerConnectButton = CreateControllerActionButton("連線已啟用站點");
            connectionControllerConnectButton.Click += ConnectionControllerConnectButton_Click;
            toolbar.Controls.Add(connectionControllerSummaryLabel, 0, 0);
            toolbar.Controls.Add(connectionControllerConnectButton, 1, 0);

            var cardsScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 247, 250),
                Margin = Padding.Empty
            };
            connectionControllerCardsHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(2),
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(245, 247, 250)
            };
            cardsScroll.Controls.Add(connectionControllerCardsHost);
            cardsScroll.Resize += delegate { LayoutConnectionControllerCards(); };

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 6, 0, 0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            connectionControllerContinuousCaptureButton = CreateControllerActionButton("連續取像");
            connectionControllerContinuousCaptureButton.BackColor = Color.FromArgb(22, 112, 196);
            connectionControllerContinuousCaptureButton.ForeColor = Color.White;
            connectionControllerContinuousCaptureButton.FlatAppearance.BorderColor = Color.FromArgb(22, 112, 196);
            connectionControllerContinuousCaptureButton.Click +=
                ConnectionControllerContinuousCaptureButton_Click;
            connectionControllerInspectButton = CreateControllerActionButton("檢測");
            connectionControllerInspectButton.BackColor = Color.FromArgb(34, 139, 75);
            connectionControllerInspectButton.ForeColor = Color.White;
            connectionControllerInspectButton.FlatAppearance.BorderColor = Color.FromArgb(34, 139, 75);
            connectionControllerInspectButton.Click += ConnectionControllerInspectButton_Click;
            actions.Controls.Add(connectionControllerContinuousCaptureButton, 0, 0);
            actions.Controls.Add(connectionControllerInspectButton, 1, 0);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(toolbar, 0, 1);
            layout.Controls.Add(cardsScroll, 0, 2);
            layout.Controls.Add(actions, 0, 3);
            panel.Controls.Add(layout);
            panel.Resize += delegate { LayoutConnectionControllerCards(); };
            RefreshConnectionControllerCards();
            return panel;
        }

        private static Button CreateControllerActionButton(string text)
        {
            var button = new Button
            {
                Dock = DockStyle.Fill,
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 243, 248),
                ForeColor = Color.FromArgb(35, 45, 58),
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                Margin = new Padding(4),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 214);
            return button;
        }

        private void RefreshConnectionControllerCards()
        {
            if (connectionControllerCardsHost == null || connectionControllerCardsHost.IsDisposed)
            {
                return;
            }
            connectionControllerCardsHost.SuspendLayout();
            try
            {
                var previousStates = new Dictionary<int, ConnectionControllerStationViewState>(
                    connectionControllerStationViews);
                connectionControllerCardsHost.Controls.Clear();
                connectionControllerStationViews.Clear();
                List<ConnectionControllerStation> configuredStations =
                    connectionControllerSettings == null || connectionControllerSettings.Stations == null
                        ? new List<ConnectionControllerStation>()
                        : connectionControllerSettings.Stations.Where(station => station != null).ToList();
                foreach (ConnectionControllerStation station in configuredStations)
                {
                    ConnectionControllerStationViewState state;
                    if (!previousStates.TryGetValue(station.SoftwareId, out state))
                    {
                        state = new ConnectionControllerStationViewState
                        {
                            Status = station.Enabled ? "等待連線" : "未啟用"
                        };
                    }
                    state.Configuration = station;
                    state.TcpConnected = inspectionTcpTransport != null &&
                        inspectionTcpTransport.IsStationConnected(station.SoftwareId);
                    state.Card = CreateConnectionControllerStationCard(state);
                    connectionControllerStationViews[station.SoftwareId] = state;
                    connectionControllerCardsHost.Controls.Add(state.Card);
                    UpdateConnectionControllerStationCard(state);
                }
                if (configuredStations.Count == 0)
                {
                    connectionControllerCardsHost.Controls.Add(new Label
                    {
                        AutoSize = false,
                        Height = 100,
                        Width = Math.Max(350, connectionControllerCardsHost.ClientSize.Width - 20),
                        Text = "尚未設定被控站點。請至「全域設定 > 連線總控參數」新增站點，並填入對方設定的站點 ID、IP 與埠號。",
                        TextAlign = ContentAlignment.MiddleCenter,
                        ForeColor = Color.FromArgb(95, 105, 118),
                        Font = new Font("Microsoft JhengHei UI", 10F),
                        Margin = new Padding(8)
                    });
                }
                LayoutConnectionControllerCards();
            }
            finally
            {
                connectionControllerCardsHost.ResumeLayout(true);
            }
            UpdateConnectionControllerSummary();
        }

        private Panel CreateConnectionControllerStationCard(ConnectionControllerStationViewState state)
        {
            var card = new Panel
            {
                Height = 326,
                Width = 540,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(6),
                Tag = state
            };
            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(235, 240, 246),
                ForeColor = Color.FromArgb(30, 45, 62),
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 4, 0)
            };
            card.Controls.Add(title);
            state.ConnectionLabel = new Label
            {
                Location = new Point(10, 42),
                Size = new Size(210, 24),
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            state.CameraLabel = new Label
            {
                Location = new Point(230, 42),
                Size = new Size(220, 24),
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            state.ResultLabel = new Label
            {
                Location = new Point(10, 70),
                Size = new Size(300, 24),
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            state.RecipeLabel = new Label
            {
                Location = new Point(310, 70),
                Size = new Size(220, 24),
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            };
            state.StatusLabel = new Label
            {
                Location = new Point(10, 96),
                Size = new Size(510, 22),
                ForeColor = Color.FromArgb(92, 101, 116),
                Font = new Font("Microsoft JhengHei UI", 8F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            state.ObjectGridHost = new Panel
            {
                Location = new Point(8, 124),
                Size = new Size(520, 190),
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(state.ConnectionLabel);
            card.Controls.Add(state.CameraLabel);
            card.Controls.Add(state.ResultLabel);
            card.Controls.Add(state.RecipeLabel);
            card.Controls.Add(state.StatusLabel);
            card.Controls.Add(state.ObjectGridHost);
            card.Resize += delegate
            {
                int width = card.ClientSize.Width;
                state.CameraLabel.Left = Math.Max(220, width / 2);
                state.CameraLabel.Width = Math.Max(120, width - state.CameraLabel.Left - 10);
                state.ResultLabel.Width = Math.Max(120, width / 2 - 15);
                state.RecipeLabel.Left = width / 2;
                state.RecipeLabel.Width = Math.Max(120, width / 2 - 20);
                state.StatusLabel.Width = Math.Max(150, width - 20);
                state.ObjectGridHost.Width = Math.Max(150, width - 18);
                state.ObjectGridHost.Height = Math.Max(80, card.ClientSize.Height - 134);
            };
            return card;
        }

        private void UpdateConnectionControllerStationCard(ConnectionControllerStationViewState state)
        {
            if (state == null || state.Card == null || state.Card.IsDisposed)
            {
                return;
            }
            string name = string.IsNullOrWhiteSpace(state.Configuration.Name)
                ? "站點 " + state.Configuration.SoftwareId.ToString()
                : state.Configuration.Name;
            Control title = state.Card.Controls.Cast<Control>().FirstOrDefault(control =>
                control.Dock == DockStyle.Top);
            if (title != null)
            {
                title.Text = name + "  (ID " + state.Configuration.SoftwareId.ToString() + ")";
            }
            state.ConnectionLabel.Text = "TCP/IP　" + (state.TcpConnected ? "已連線" : "未連線");
            state.ConnectionLabel.ForeColor = state.TcpConnected
                ? Color.FromArgb(31, 132, 73)
                : Color.FromArgb(120, 128, 138);
            state.CameraLabel.Text = "相機　" + (state.CameraConnected ? "已連線" : "未連線");
            UpdateConnectionControllerCameraLabel(state);
            state.ResultLabel.Text = state.ResultAtUtc.HasValue
                ? "最新結果　" + state.ResultAtUtc.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss")
                : "最新結果　尚無判定回報";
            state.RecipeLabel.Text = string.IsNullOrWhiteSpace(state.RecipeName)
                ? "配方　--"
                : "配方　" + state.RecipeName;
            state.StatusLabel.Text = string.IsNullOrWhiteSpace(state.Status)
                ? (state.IsInspecting ? "持續檢測中" :
                    state.IsCapturing ? "連續取像中" : "等待站點回報")
                : state.Status;
            RenderConnectionControllerObjectGrid(state);
        }

        private void UpdateConnectionControllerCameraLabel(ConnectionControllerStationViewState state)
        {
            if (state == null || state.CameraLabel == null || state.CameraLabel.IsDisposed)
            {
                return;
            }

            bool modeRequested = isConnectionControllerContinuousCaptureRequested ||
                isConnectionControllerInspectionRequested;
            bool shouldAlert = !state.CameraConnected && modeRequested &&
                state.Configuration != null && state.Configuration.Enabled;
            state.CameraLabel.ForeColor = state.CameraConnected
                ? Color.FromArgb(31, 132, 73)
                : (shouldAlert
                    ? Color.FromArgb(198, 45, 48)
                    : Color.FromArgb(120, 128, 138));
        }

        private void RenderConnectionControllerObjectGrid(ConnectionControllerStationViewState state)
        {
            Panel host = state.ObjectGridHost;
            if (host == null || host.IsDisposed)
            {
                return;
            }
            host.SuspendLayout();
            try
            {
                host.Controls.Clear();
                if (state.Objects == null || state.Objects.Count == 0)
                {
                    host.Controls.Add(new Label
                    {
                        Dock = DockStyle.Fill,
                        Text = state.JudgementComplete ? "本次檢測沒有找到物件" : "尚無物件判定結果",
                        TextAlign = ContentAlignment.MiddleCenter,
                        ForeColor = Color.FromArgb(105, 115, 128)
                    });
                    return;
                }

                int maxColumn = state.Objects.Max(item => Math.Max(0, item.Column));
                int maxRow = state.Objects.Max(item => Math.Max(0, item.Row));
                int columnCount = Math.Max(1, Math.Min(32, maxColumn + 1));
                int rowCount = Math.Max(1, maxRow + 1);
                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = columnCount,
                    RowCount = rowCount,
                    Padding = new Padding(3),
                    Margin = Padding.Empty,
                    BackColor = Color.FromArgb(248, 250, 252)
                };
                for (int column = 0; column < columnCount; column++)
                {
                    grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columnCount));
                }
                for (int row = 0; row < rowCount; row++)
                {
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
                }
                foreach (InspectionTcpObjectResult result in state.Objects.OrderBy(item => item.Row)
                    .ThenBy(item => item.Column).ThenBy(item => item.Number))
                {
                    int column = Math.Max(0, Math.Min(columnCount - 1, result.Column));
                    int row = Math.Max(0, result.Row);
                    while (row >= grid.RowCount)
                    {
                        grid.RowCount++;
                        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
                    }
                    var item = new InspectionOverviewItem
                    {
                        ObjectNumber = result.Number,
                        SizeGrade = ParseOverviewSizeGrade(result.SizeGrade),
                        DefectGrade = ParseOverviewDefectGrade(result.DefectGrade),
                        CombinedGrade = ParseOverviewSizeGrade(result.CombinedGrade)
                    };
                    grid.Controls.Add(new InspectionOverviewTile
                    {
                        Item = item,
                        Font = new Font("Microsoft JhengHei UI", 8F),
                        Margin = new Padding(2),
                        Dock = DockStyle.Fill
                    }, column, row);
                }
                host.Controls.Add(grid);
            }
            finally
            {
                host.ResumeLayout(true);
            }
        }

        private static ResultReviewGoodJudgementGrade ParseOverviewSizeGrade(string value)
        {
            switch ((value ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A": return ResultReviewGoodJudgementGrade.A;
                case "B": return ResultReviewGoodJudgementGrade.B;
                case "NG": return ResultReviewGoodJudgementGrade.Ng;
                default: return ResultReviewGoodJudgementGrade.Pending;
            }
        }

        private static ResultReviewDefectGrade ParseOverviewDefectGrade(string value)
        {
            switch ((value ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A": return ResultReviewDefectGrade.A;
                case "NG": return ResultReviewDefectGrade.Ng;
                default: return ResultReviewDefectGrade.Pending;
            }
        }

        private void LayoutConnectionControllerCards()
        {
            if (connectionControllerCardsHost == null || connectionControllerCardsHost.IsDisposed)
            {
                return;
            }
            int availableWidth = Math.Max(400,
                (connectionControllerCardsHost.Parent == null
                    ? connectionControllerCardsHost.ClientSize.Width
                    : connectionControllerCardsHost.Parent.ClientSize.Width) - 26);
            connectionControllerCardsHost.Width = availableWidth;
            int cardsPerRow = availableWidth >= 760 ? 2 : 1;
            int cardWidth = Math.Max(360, (availableWidth - 18) / cardsPerRow);
            foreach (Control card in connectionControllerCardsHost.Controls)
            {
                card.Width = cardWidth;
            }
            int cardCount = connectionControllerCardsHost.Controls.Count;
            int rowCount = Math.Max(1, (cardCount + cardsPerRow - 1) / cardsPerRow);
            connectionControllerCardsHost.Height = rowCount * 338 + 8;
        }

        private void UpdateConnectionControllerSummary()
        {
            if (connectionControllerSummaryLabel == null || connectionControllerSummaryLabel.IsDisposed)
            {
                return;
            }
            int total = connectionControllerStationViews.Count;
            int connected = connectionControllerStationViews.Values.Count(state => state.TcpConnected);
            int ng = connectionControllerStationViews.Values.Sum(state =>
                state.Objects == null ? 0 : state.Objects.Count(item =>
                    string.Equals(item.CombinedGrade, "NG", StringComparison.OrdinalIgnoreCase)));
            int pending = connectionControllerStationViews.Values.Sum(state =>
                state.Objects == null ? 0 : state.Objects.Count(item =>
                    string.Equals(item.CombinedGrade, "Pending", StringComparison.OrdinalIgnoreCase)));
            connectionControllerSummaryLabel.Text = "站點 " + total +
                "　已連線 " + connected + "　物件 NG " + ng + "　待確認 " + pending;
            if (connectionControllerConnectButton != null && !connectionControllerConnectButton.IsDisposed)
            {
                connectionControllerConnectButton.Text = connected > 0
                    ? "中斷已連線站點"
                    : "連線已啟用站點";
            }
            bool anyConnected = connected > 0 && connectionControllerSettings != null &&
                connectionControllerSettings.MonitorCamera;
            if (connected == 0)
            {
                isConnectionControllerContinuousCaptureRequested = false;
                isConnectionControllerInspectionRequested = false;
                connectionControllerCaptureStations.Clear();
                connectionControllerInspectionStations.Clear();
                connectionControllerPendingCommands.Clear();
                connectionControllerLatestModeCommandIds.Clear();
            }
            foreach (ConnectionControllerStationViewState state in connectionControllerStationViews.Values)
            {
                UpdateConnectionControllerCameraLabel(state);
            }
            if (connectionControllerContinuousCaptureButton != null &&
                !connectionControllerContinuousCaptureButton.IsDisposed)
            {
                connectionControllerContinuousCaptureButton.Enabled = anyConnected &&
                    !connectionControllerCommandInFlight && !isConnectionControllerInspectionRequested;
                connectionControllerContinuousCaptureButton.Text =
                    isConnectionControllerContinuousCaptureRequested ? "停止取像" : "連續取像";
                SetConnectionControllerModeButtonStyle(connectionControllerContinuousCaptureButton,
                    isConnectionControllerContinuousCaptureRequested, Color.FromArgb(22, 112, 196));
            }
            if (connectionControllerInspectButton != null && !connectionControllerInspectButton.IsDisposed)
            {
                connectionControllerInspectButton.Enabled = anyConnected &&
                    !connectionControllerCommandInFlight &&
                    !isConnectionControllerContinuousCaptureRequested;
                connectionControllerInspectButton.Text = isConnectionControllerInspectionRequested
                    ? "停止檢測" : "檢測";
                SetConnectionControllerModeButtonStyle(connectionControllerInspectButton,
                    isConnectionControllerInspectionRequested, Color.FromArgb(34, 139, 75));
            }
        }

        private static void SetConnectionControllerModeButtonStyle(
            Button button, bool active, Color idleColor)
        {
            Color color = active ? Color.FromArgb(190, 65, 59) : idleColor;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = color;
        }

        private void UpdateConnectionControllerNetworkStatus()
        {
            if (connectionControllerNetworkStatusLabel == null ||
                connectionControllerNetworkStatusLabel.IsDisposed)
            {
                return;
            }
            int connected = connectionControllerStationViews.Values.Count(state => state.TcpConnected);
            connectionControllerNetworkStatusLabel.Text = "TCP/IP　" + connected + " / " +
                connectionControllerStationViews.Count + " 個站點已連線";
        }

        private void SetConnectionControllerStatus(string text)
        {
            if (connectionControllerNetworkStatusLabel != null &&
                !connectionControllerNetworkStatusLabel.IsDisposed)
            {
                connectionControllerNetworkStatusLabel.Text = text ?? string.Empty;
            }
        }

        private async void ConnectionControllerConnectButton_Click(object sender, EventArgs e)
        {
            if (connectionControllerSettings == null || !connectionControllerSettings.MonitorCamera)
            {
                SetConnectionControllerStatus("攝影機監控未啟用；請先到全域設定開啟監控。");
                return;
            }
            if (connectionControllerStationViews.Values.Any(state => state.TcpConnected))
            {
                connectionControllerConnectRequested = false;
                foreach (ConnectionControllerStationViewState state in
                    connectionControllerStationViews.Values.ToList())
                {
                    inspectionTcpTransport.DisconnectStation(state.Configuration.SoftwareId);
                }
                SetConnectionControllerStatus("已中斷總控端的站點連線。");
                return;
            }
            connectionControllerConnectRequested = true;
            await ConnectConfiguredStationsAsync();
        }

        private async void ConnectionControllerContinuousCaptureButton_Click(object sender, EventArgs e)
        {
            if (connectionControllerCommandInFlight || isConnectionControllerInspectionRequested)
            {
                return;
            }
            bool wasActive = isConnectionControllerContinuousCaptureRequested;
            string command = wasActive ? "stop-capture" : "start-capture";
            isConnectionControllerContinuousCaptureRequested = !wasActive;
            if (!wasActive) connectionControllerCaptureStations.Clear();
            connectionControllerCommandInFlight = true;
            UpdateConnectionControllerSummary();
            int sent = 0;
            try
            {
                sent = await SendControllerCommandToConnectedStationsAsync(command);
            }
            finally
            {
                if (sent == 0) isConnectionControllerContinuousCaptureRequested = wasActive;
                connectionControllerCommandInFlight = false;
                if (sent > 0) ReconcileConnectionControllerMode(command);
                UpdateConnectionControllerSummary();
            }
            SetConnectionControllerStatus(sent > 0
                ? (wasActive ? "已傳送停止取像命令。" : "已傳送連續取像命令。")
                : "沒有已連線的被控站點可接收命令。");
        }

        private async void ConnectionControllerInspectButton_Click(object sender, EventArgs e)
        {
            if (connectionControllerCommandInFlight ||
                isConnectionControllerContinuousCaptureRequested)
            {
                return;
            }
            bool wasActive = isConnectionControllerInspectionRequested;
            string command = wasActive ? "stop-inspection" : "start-inspection";
            isConnectionControllerInspectionRequested = !wasActive;
            if (!wasActive) connectionControllerInspectionStations.Clear();
            connectionControllerCommandInFlight = true;
            UpdateConnectionControllerSummary();
            int sent = 0;
            try
            {
                sent = await SendControllerCommandToConnectedStationsAsync(command);
            }
            finally
            {
                if (sent == 0) isConnectionControllerInspectionRequested = wasActive;
                connectionControllerCommandInFlight = false;
                if (sent > 0) ReconcileConnectionControllerMode(command);
                UpdateConnectionControllerSummary();
            }
            SetConnectionControllerStatus(sent > 0
                ? (wasActive ? "已傳送停止檢測命令。" :
                    "已傳送持續檢測命令；各站會以目前 Recipe 判定並回傳結果。")
                : "沒有已連線的被控站點可接收檢測命令。");
        }

        private async Task<int> SendControllerCommandToConnectedStationsAsync(string command)
        {
            if (inspectionTcpTransport == null || connectionControllerSettings == null ||
                !connectionControllerSettings.MonitorCamera)
            {
                return 0;
            }
            var targets = GetEnabledConnectionControllerStations()
                .Where(station => inspectionTcpTransport.IsStationConnected(station.SoftwareId)).ToList();
            int sent = 0;
            foreach (ConnectionControllerStation station in targets)
            {
                string commandId = Guid.NewGuid().ToString("N");
                string previousCommandId;
                connectionControllerLatestModeCommandIds.TryGetValue(
                    station.SoftwareId, out previousCommandId);
                connectionControllerPendingCommands[commandId] =
                    new ConnectionControllerPendingCommand
                    {
                        StationId = station.SoftwareId,
                        Command = command
                    };
                connectionControllerLatestModeCommandIds[station.SoftwareId] = commandId;
                bool success = await inspectionTcpTransport.SendToStationAsync(station.SoftwareId,
                    new InspectionTcpMessage
                    {
                        MessageType = "command",
                        Command = command,
                        CommandId = commandId,
                        StationId = cameraDeviceSettings == null ? 1 : cameraDeviceSettings.SoftwareId,
                        StationName = GetLocalStationName()
                    });
                if (success)
                {
                    sent++;
                    foreach (string oldCommandId in connectionControllerPendingCommands
                        .Where(item => item.Value.StationId == station.SoftwareId &&
                            item.Key != commandId)
                        .Select(item => item.Key).ToList())
                    {
                        connectionControllerPendingCommands.Remove(oldCommandId);
                    }
                }
                else
                {
                    connectionControllerPendingCommands.Remove(commandId);
                    string latestId;
                    if (connectionControllerLatestModeCommandIds.TryGetValue(
                            station.SoftwareId, out latestId) && latestId == commandId)
                    {
                        if (!string.IsNullOrEmpty(previousCommandId) &&
                            connectionControllerPendingCommands.ContainsKey(previousCommandId))
                        {
                            connectionControllerLatestModeCommandIds[station.SoftwareId] =
                                previousCommandId;
                        }
                        else
                        {
                            connectionControllerLatestModeCommandIds.Remove(station.SoftwareId);
                        }
                    }
                }
            }
            return sent;
        }

        private void ReconcileConnectionControllerMode(string command)
        {
            if (connectionControllerCommandInFlight ||
                connectionControllerPendingCommands.Values.Any(item => item.Command == command))
            {
                return;
            }
            if (command == "stop-capture")
            {
                isConnectionControllerContinuousCaptureRequested =
                    connectionControllerCaptureStations.Count > 0;
            }
            else if (command == "stop-inspection")
            {
                isConnectionControllerInspectionRequested =
                    connectionControllerInspectionStations.Count > 0;
            }
        }

        private async Task ConnectConfiguredStationsAsync()
        {
            if (inspectionTcpTransport == null || connectionControllerSettings == null ||
                !connectionControllerSettings.MonitorCamera ||
                Interlocked.CompareExchange(ref connectionControllerConnectInFlight, 1, 0) != 0)
            {
                return;
            }
            try
            {
                ConnectionControllerStation[] stations = GetEnabledConnectionControllerStations().ToArray();
                if (stations.Length == 0)
                {
                    SetConnectionControllerStatus("尚未啟用任何被控站點；請先到全域設定加入並啟用站點。");
                    return;
                }
                var attempts = stations.Select(async station =>
                {
                    if (inspectionTcpTransport.IsStationConnected(station.SoftwareId))
                    {
                        return true;
                    }
                    bool connected = await inspectionTcpTransport.ConnectToStationAsync(
                        station.SoftwareId, station.IpAddress, station.Port,
                        connectionControllerSettings.ConnectTimeoutSeconds);
                    if (connected)
                    {
                        await inspectionTcpTransport.SendToStationAsync(station.SoftwareId,
                            CreateLocalStationStatusMessage("hello"));
                    }
                    return connected;
                }).ToArray();
                bool[] results = await Task.WhenAll(attempts);
                int connectedCount = results.Count(result => result);
                SetConnectionControllerStatus("站點連線完成：成功 " + connectedCount + " / " + stations.Length + "。" );
            }
            finally
            {
                Interlocked.Exchange(ref connectionControllerConnectInFlight, 0);
                UpdateConnectionControllerSummary();
            }
        }

        private IEnumerable<ConnectionControllerStation> GetEnabledConnectionControllerStations()
        {
            return connectionControllerSettings == null || connectionControllerSettings.Stations == null
                ? Enumerable.Empty<ConnectionControllerStation>()
                : connectionControllerSettings.Stations.Where(station => station != null && station.Enabled);
        }

        private void ConnectionControllerReconnectTimer_Tick(object sender, EventArgs e)
        {
            if (!connectionControllerConnectRequested || connectionControllerSettings == null ||
                !connectionControllerSettings.MonitorCamera ||
                !connectionControllerSettings.AutoReconnect ||
                Interlocked.CompareExchange(ref connectionControllerConnectInFlight, 0, 0) != 0)
            {
                return;
            }
            _ = ConnectConfiguredStationsAsync();
        }

        private void InspectionTcpTransport_PeerStateChanged(object sender, InspectionTcpPeerEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (e.StationId.HasValue)
                    {
                        ConnectionControllerStationViewState state;
                        if (connectionControllerStationViews.TryGetValue(e.StationId.Value, out state))
                        {
                            state.TcpConnected = e.Connected;
                            if (e.Connected)
                            {
                                state.CameraConnected = false;
                                state.IsCapturing = false;
                                state.IsInspecting = false;
                                state.Status = "TCP/IP 已連線，等待設備狀態";
                            }
                            else
                            {
                                state.CameraConnected = false;
                                state.IsCapturing = false;
                                state.IsInspecting = false;
                                state.Status = "TCP/IP 連線中斷";
                                connectionControllerCaptureStations.Remove(e.StationId.Value);
                                connectionControllerInspectionStations.Remove(e.StationId.Value);
                                connectionControllerLatestModeCommandIds.Remove(e.StationId.Value);
                                foreach (string commandId in connectionControllerPendingCommands
                                    .Where(item => item.Value.StationId == e.StationId.Value)
                                    .Select(item => item.Key).ToList())
                                {
                                    connectionControllerPendingCommands.Remove(commandId);
                                }
                                ReconcileConnectionControllerMode("start-capture");
                                ReconcileConnectionControllerMode("start-inspection");
                            }
                            UpdateConnectionControllerStationCard(state);
                        }
                    }
                    UpdateConnectionControllerSummary();
                    UpdateConnectionControllerNetworkStatus();
                    UpdateInspectionOverviewTcpStatus();
                    QueueStationStatusPublish(true);
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void InspectionTcpTransport_MessageReceived(object sender, InspectionTcpMessageEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke(new Action(delegate { HandleInspectionTcpMessage(e); }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void HandleInspectionTcpMessage(InspectionTcpMessageEventArgs e)
        {
            InspectionTcpMessage message = e.Message;
            if (message == null)
            {
                return;
            }
            if (e.ConfiguredStationId.HasValue && message.StationId > 0 &&
                e.ConfiguredStationId.Value != message.StationId)
            {
                ConnectionControllerStationViewState mismatchState;
                if (connectionControllerStationViews.TryGetValue(e.ConfiguredStationId.Value, out mismatchState))
                {
                    mismatchState.Status = "站點 ID 不符：設定 " + e.ConfiguredStationId.Value +
                        "，對方回報 " + message.StationId;
                    UpdateConnectionControllerStationCard(mismatchState);
                }
                inspectionTcpTransport.DisconnectStation(e.ConfiguredStationId.Value);
                return;
            }

            if (string.Equals(message.MessageType, "command", StringComparison.OrdinalIgnoreCase))
            {
                _ = HandleRemoteInspectionCommandAsync(e, message);
                return;
            }
            if (string.Equals(message.MessageType, "hello", StringComparison.OrdinalIgnoreCase))
            {
                SendLocalStationStatusToPeer(e.PeerKey, "已連線至總控端");
                return;
            }
            if (string.Equals(message.MessageType, "command-ack", StringComparison.OrdinalIgnoreCase))
            {
                int stationId = e.ConfiguredStationId ?? message.StationId;
                ConnectionControllerStationViewState ackState;
                if (connectionControllerStationViews.TryGetValue(stationId, out ackState))
                {
                    ackState.Status = message.Status ?? (message.Accepted ? "命令已接受" : "命令遭拒絕");
                    UpdateConnectionControllerStationCard(ackState);
                }
                ConnectionControllerPendingCommand pending;
                if (!string.IsNullOrEmpty(message.CommandId) &&
                    connectionControllerPendingCommands.TryGetValue(message.CommandId, out pending) &&
                    pending.StationId == stationId)
                {
                    connectionControllerPendingCommands.Remove(message.CommandId);
                    string latestId;
                    bool isLatest = connectionControllerLatestModeCommandIds.TryGetValue(
                        stationId, out latestId) && latestId == message.CommandId;
                    if (message.Accepted && isLatest)
                    {
                        switch (pending.Command)
                        {
                            case "start-capture":
                                connectionControllerCaptureStations.Add(stationId);
                                break;
                            case "stop-capture":
                                connectionControllerCaptureStations.Remove(stationId);
                                break;
                            case "start-inspection":
                                connectionControllerInspectionStations.Add(stationId);
                                break;
                            case "stop-inspection":
                                connectionControllerInspectionStations.Remove(stationId);
                                break;
                        }
                    }
                    if (isLatest)
                    {
                        connectionControllerLatestModeCommandIds.Remove(stationId);
                    }
                    ReconcileConnectionControllerMode(pending.Command);
                    UpdateConnectionControllerSummary();
                }
                return;
            }
            if (string.Equals(message.MessageType, "station-status", StringComparison.OrdinalIgnoreCase))
            {
                UpdateConnectionControllerStationFromMessage(e, message);
            }
        }

        private void UpdateConnectionControllerStationFromMessage(
            InspectionTcpMessageEventArgs e,
            InspectionTcpMessage message)
        {
            int stationId = e.ConfiguredStationId ?? message.StationId;
            if (stationId < 1 || stationId > 10)
            {
                return;
            }
            ConnectionControllerStationViewState state;
            if (!connectionControllerStationViews.TryGetValue(stationId, out state))
            {
                ConnectionControllerStation configuration = connectionControllerSettings.Stations
                    .FirstOrDefault(station => station != null && station.SoftwareId == stationId);
                if (configuration == null)
                {
                    configuration = new ConnectionControllerStation
                    {
                        SoftwareId = stationId,
                        Name = message.StationName ?? "站點 " + stationId,
                        Enabled = true
                    };
                }
                state = new ConnectionControllerStationViewState { Configuration = configuration };
                state.Card = CreateConnectionControllerStationCard(state);
                connectionControllerStationViews[stationId] = state;
                connectionControllerCardsHost.Controls.Add(state.Card);
                LayoutConnectionControllerCards();
            }
            state.TcpConnected = true;
            state.CameraConnected = message.CameraConnected;
            state.IsCapturing = message.IsCapturing;
            state.IsInspecting = message.IsInspecting;
            bool hasPendingModeCommand = connectionControllerPendingCommands.Values.Any(item =>
                item.StationId == stationId);
            if (!hasPendingModeCommand)
            {
                if (isConnectionControllerContinuousCaptureRequested)
                {
                    if (message.IsCapturing) connectionControllerCaptureStations.Add(stationId);
                    else connectionControllerCaptureStations.Remove(stationId);
                    ReconcileConnectionControllerMode("start-capture");
                }
                if (isConnectionControllerInspectionRequested)
                {
                    if (message.IsInspecting) connectionControllerInspectionStations.Add(stationId);
                    else connectionControllerInspectionStations.Remove(stationId);
                    ReconcileConnectionControllerMode("start-inspection");
                }
            }
            state.JudgementComplete = message.JudgementComplete;
            state.Status = message.Status;
            state.RecipeName = string.IsNullOrWhiteSpace(message.RecipeName)
                ? message.RecipeId
                : message.RecipeName;
            DateTime resultAt;
            if (!string.IsNullOrWhiteSpace(message.ResultAtUtc) &&
                DateTime.TryParse(message.ResultAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out resultAt))
            {
                state.ResultAtUtc = resultAt;
            }
            state.Objects = message.Objects ?? new List<InspectionTcpObjectResult>();
            UpdateConnectionControllerStationCard(state);
            UpdateConnectionControllerSummary();
            UpdateConnectionControllerNetworkStatus();
        }

        private async Task HandleRemoteInspectionCommandAsync(
            InspectionTcpMessageEventArgs e,
            InspectionTcpMessage message)
        {
            string command = (message.Command ?? string.Empty).Trim().ToLowerInvariant();
            bool accepted = false;
            string response;
            switch (command)
            {
                case "start-capture":
                    response = StartRemoteContinuousCapture(out accepted);
                    break;
                case "stop-capture":
                    response = StopRemoteContinuousCapture(out accepted);
                    break;
                case "inspect":
                    response = RequestRemoteInspection(out accepted);
                    break;
                case "start-inspection":
                    response = StartRemoteContinuousInspection(out accepted);
                    break;
                case "stop-inspection":
                    response = StopRemoteContinuousInspection(out accepted);
                    break;
                default:
                    response = "不支援的命令：" + command;
                    break;
            }
            await inspectionTcpTransport.SendToPeerAsync(e.PeerKey, new InspectionTcpMessage
            {
                MessageType = "command-ack",
                Command = command,
                CommandId = message.CommandId,
                StationId = cameraDeviceSettings == null ? 1 : cameraDeviceSettings.SoftwareId,
                StationName = GetLocalStationName(),
                Accepted = accepted,
                Status = response
            });
            QueueStationStatusPublish(true);
        }

        private string StartRemoteContinuousCapture(out bool accepted)
        {
            accepted = false;
            if (isInspectionOverviewInspectionModeActive ||
                isInspectionOverviewContinuousCaptureActive)
            {
                return "無法連續取像：本機取像或檢測模式正在運行。";
            }
            if (remoteContinuousInspectionActive ||
                Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) != 0)
            {
                return "無法連續取像：目前正在檢測。";
            }
            if (!IsRemoteCameraReady())
            {
                return "無法連續取像：攝影機程式尚未開啟或相機尚未連線。";
            }
            string previewMessage;
            if (!cameraCaptureHostControl.IsCameraPreviewing &&
                !cameraCaptureHostControl.StartPreview(out previewMessage))
            {
                return "無法啟動相機預覽：" + previewMessage;
            }
            remoteContinuousCaptureActive = true;
            cameraCaptureHostControl.SuppressAutomaticInspectionForwarding = true;
            cameraCaptureHostControl.RemoteCaptureForwardingEnabled = true;
            accepted = true;
            return "已接受連續取像；不會自動進入檢測。";
        }

        private string StopRemoteContinuousCapture(out bool accepted)
        {
            accepted = true;
            remoteContinuousCaptureActive = false;
            if (cameraCaptureHostControl != null && !cameraCaptureHostControl.IsDisposed)
            {
                if (!remoteContinuousInspectionActive &&
                    Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) == 0)
                {
                    cameraCaptureHostControl.RemoteCaptureForwardingEnabled = false;
                }
                if (!isInspectionOverviewActive && !remoteContinuousInspectionActive)
                {
                    cameraCaptureHostControl.SuppressAutomaticInspectionForwarding = false;
                }
                if (!isInspectionOverviewContinuousCaptureActive &&
                    !remoteContinuousInspectionActive &&
                    cameraCaptureHostControl.IsCameraPreviewing)
                {
                    cameraCaptureHostControl.StopPreview();
                }
            }
            return "已停止遠端連續取像。";
        }

        private string RequestRemoteInspection(out bool accepted)
        {
            accepted = false;
            if (isInspectionOverviewInspectionModeActive ||
                isInspectionOverviewContinuousCaptureActive)
            {
                return "檢測未啟動：本機取像或檢測模式正在運行。";
            }
            if (remoteContinuousCaptureActive)
            {
                return "檢測未啟動：目前正在連續取像。";
            }
            if (!IsRemoteCameraReady())
            {
                return "檢測未啟動：攝影機程式尚未開啟或相機尚未連線。";
            }
            string busyReason;
            if (TryGetCameraInspectionBusyReason(out busyReason) ||
                Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 0, 0) != 0 ||
                Interlocked.CompareExchange(ref remoteInspectionRequested, 1, 0) != 0)
            {
                return "檢測未接受：" + (string.IsNullOrWhiteSpace(busyReason)
                    ? "另一張影像仍在處理中。"
                    : busyReason);
            }

            EnsureInspectionOverviewResultReviewParametersInitialized();
            ResultReviewParameterChoice choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (choice == null || choice.Parameter == null)
            {
                Interlocked.Exchange(ref remoteInspectionRequested, 0);
                return "檢測未接受：目前沒有可用的檢測參數。";
            }
            cameraCaptureHostControl.SuppressAutomaticInspectionForwarding = true;
            cameraCaptureHostControl.RemoteCaptureForwardingEnabled = true;
            if (!cameraCaptureHostControl.IsCameraPreviewing)
            {
                string previewMessage;
                if (!cameraCaptureHostControl.StartPreview(out previewMessage))
                {
                    Interlocked.Exchange(ref remoteInspectionRequested, 0);
                    cameraCaptureHostControl.RemoteCaptureForwardingEnabled = remoteContinuousCaptureActive;
                    if (!isInspectionOverviewActive)
                    {
                        cameraCaptureHostControl.SuppressAutomaticInspectionForwarding = false;
                    }
                    return "檢測未啟動：無法開始相機預覽。" + previewMessage;
                }
            }
            accepted = true;
            return "已接受檢測命令，將以目前 Recipe 處理下一張相機影像。";
        }

        private string StartRemoteContinuousInspection(out bool accepted)
        {
            if (remoteContinuousInspectionActive)
            {
                accepted = true;
                return "持續檢測已在運行。";
            }
            remoteContinuousInspectionActive = true;
            string response = RequestRemoteInspection(out accepted);
            if (!accepted)
            {
                remoteContinuousInspectionActive = false;
                return response;
            }
            return "已啟動持續檢測；上一張處理完成後才接收下一張影像。";
        }

        private string StopRemoteContinuousInspection(out bool accepted)
        {
            accepted = true;
            remoteContinuousInspectionActive = false;
            Interlocked.Exchange(ref remoteInspectionRequested, 0);
            if (Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 0, 0) == 0)
            {
                FinishRemoteInspectionCapture();
            }
            return "已停止持續檢測；正在處理的影像會完成。";
        }

        private void FinishRemoteInspectionCapture()
        {
            if (remoteContinuousInspectionActive || remoteContinuousCaptureActive ||
                Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) != 0 ||
                cameraCaptureHostControl == null || cameraCaptureHostControl.IsDisposed)
            {
                return;
            }
            cameraCaptureHostControl.RemoteCaptureForwardingEnabled = false;
            if (!isInspectionOverviewContinuousCaptureActive &&
                !isInspectionOverviewInspectionModeActive &&
                cameraCaptureHostControl.IsCameraPreviewing)
            {
                cameraCaptureHostControl.StopPreview();
            }
            if (!isInspectionOverviewActive)
            {
                cameraCaptureHostControl.SuppressAutomaticInspectionForwarding = false;
            }
        }

        private bool IsRemoteCameraReady()
        {
            return cameraCaptureHostControl != null && !cameraCaptureHostControl.IsDisposed &&
                cameraCaptureHostControl.HasCameraProgramBeenOpened &&
                cameraCaptureHostControl.IsCameraConnected;
        }

        private void CameraCaptureHostControl_RemoteImageCaptured(
            object sender,
            CameraCaptureApp.Services.CameraImageAcquiredEventArgs e)
        {
            if (e == null || IsDisposed)
            {
                return;
            }
            Interlocked.Exchange(ref lastRemoteCameraSequence, e.SequenceNumber);
            if (Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) != 1 ||
                Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 1, 0) != 0)
            {
                QueueStationStatusPublish(false);
                return;
            }

            CameraCaptureApp.Services.CameraGrayscaleFrame frame = null;
            try
            {
                frame = e.GrayscaleFrame;
                if (frame == null)
                {
                    Interlocked.Exchange(ref remoteInspectionRequested,
                        remoteContinuousInspectionActive ? 1 : 0);
                    Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                    SetCameraInspectionStatus("遠端相機沒有提供可用的灰階影像資料。");
                    return;
                }
                long sequenceNumber = e.SequenceNumber;
                CameraCaptureApp.Services.CameraGrayscaleFrame capturedFrame = frame;
                frame = null;
                BeginInvoke(new Action(async delegate
                {
                    try
                    {
                        Interlocked.Exchange(ref remoteInspectionRequested, 0);
                        isRemoteControllerInspectionActive = true;
                        await ProcessCameraAcquiredImageAsync(capturedFrame, sequenceNumber);
                        capturedFrame = null;
                    }
                    catch (Exception exception)
                    {
                        Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                        SetCameraInspectionStatus("遠端檢測失敗：" + exception.Message);
                    }
                    finally
                    {
                        isRemoteControllerInspectionActive = false;
                        if (remoteContinuousInspectionActive && IsRemoteCameraReady())
                        {
                            Interlocked.Exchange(ref remoteInspectionRequested, 1);
                        }
                        else
                        {
                            remoteContinuousInspectionActive = false;
                            Interlocked.Exchange(ref remoteInspectionRequested, 0);
                            FinishRemoteInspectionCapture();
                        }
                        if (isConnectionControllerPanelActive)
                        {
                            ShowConnectionControllerPanel();
                        }
                        QueueStationStatusPublish(true);
                    }
                }));
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref remoteInspectionRequested,
                    remoteContinuousInspectionActive ? 1 : 0);
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                SetCameraInspectionStatus("遠端灰階影像交接失敗：" + exception.Message);
            }
        }

        private void QueueStationStatusPublish(bool force)
        {
            if (inspectionTcpTransport == null || IsDisposed || !IsHandleCreated)
            {
                return;
            }
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<bool>(QueueStationStatusPublish), force); }
                catch (InvalidOperationException) { }
                return;
            }
            long nowTicks = DateTime.UtcNow.Ticks;
            long lastTicks = Interlocked.Read(ref lastStationStatusPublishUtcTicks);
            if (!force && nowTicks - lastTicks < TimeSpan.TicksPerSecond)
            {
                return;
            }
            Interlocked.Exchange(ref lastStationStatusPublishUtcTicks, nowTicks);
            InspectionTcpMessage message = CreateLocalStationStatusMessage("station-status");
            _ = inspectionTcpTransport.BroadcastAsync(message);
        }

        private void UpdateConnectionControllerMonitoringControls()
        {
            bool enabled = connectionControllerSettings != null &&
                connectionControllerSettings.MonitorCamera;
            if (connectionControllerConnectButton != null)
            {
                connectionControllerConnectButton.Enabled = enabled;
            }
            UpdateConnectionControllerSummary();
        }

        private void InvalidateConnectionControllerRecipeMetadata()
        {
            connectionControllerRecipeMetadataResolved = false;
        }

        private void ResolveConnectionControllerRecipeMetadata()
        {
            string recipeId = systemParameters == null
                ? null
                : systemParameters.ActiveDetectionRecipeId;
            if (connectionControllerRecipeMetadataResolved &&
                string.Equals(connectionControllerRecipeMetadataId, recipeId, StringComparison.Ordinal))
            {
                return;
            }

            connectionControllerRecipeMetadataResolved = true;
            connectionControllerRecipeMetadataId = recipeId;
            connectionControllerRecipeMetadataName = null;
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            try
            {
                var catalog = new DetectionRecipeCatalogService(AppDomain.CurrentDomain.BaseDirectory);
                DetectionRecipeCatalogEntry entry = catalog.LoadParameterList().FirstOrDefault(item =>
                    string.Equals(item.Id, recipeId, StringComparison.Ordinal));
                connectionControllerRecipeMetadataName = entry == null
                    ? null
                    : entry.DisplayName;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Unable to resolve active detection recipe metadata: " + exception.Message);
            }
        }

        private void PublishParameterResultReviewSnapshotToTcp()
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            if (snapshot == null || !snapshot.JudgementComplete)
            {
                return;
            }

            isParameterResultReviewSnapshotForTcp = true;
            parameterResultReviewTcpResultAtUtc = DateTime.UtcNow;
            QueueStationStatusPublish(true);
        }

        private void SendLocalStationStatusToPeer(string peerKey, string status)
        {
            if (inspectionTcpTransport == null)
            {
                return;
            }
            _ = inspectionTcpTransport.SendToPeerAsync(peerKey,
                CreateLocalStationStatusMessage("station-status", status));
        }

        private InspectionTcpMessage CreateLocalStationStatusMessage(
            string messageType,
            string statusOverride = null)
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            ResolveConnectionControllerRecipeMetadata();
            bool currentSnapshot = snapshot != null &&
                (isParameterResultReviewSnapshotForTcp ||
                    (string.IsNullOrWhiteSpace(snapshot.ImagePath)
                    ? snapshot.Parameter != null
                    : string.Equals(snapshot.ImagePath, latestCameraInspectionFramePath,
                        StringComparison.OrdinalIgnoreCase)));
            var message = new InspectionTcpMessage
            {
                MessageType = messageType,
                StationId = cameraDeviceSettings == null ? 1 : cameraDeviceSettings.SoftwareId,
                StationName = GetLocalStationName(),
                SentAtUtc = DateTime.UtcNow.ToString("o"),
                Status = statusOverride ?? (statusLabel == null ? string.Empty : statusLabel.Text),
                CameraConnected = cameraCaptureHostControl != null && cameraCaptureHostControl.IsCameraConnected,
                IsCapturing = remoteContinuousCaptureActive || isInspectionOverviewContinuousCaptureActive,
                IsInspecting = remoteContinuousInspectionActive,
                JudgementComplete = currentSnapshot && snapshot.JudgementComplete,
                RecipeId = string.IsNullOrWhiteSpace(connectionControllerRecipeMetadataId)
                    ? null
                    : connectionControllerRecipeMetadataId,
                RecipeName = string.IsNullOrWhiteSpace(connectionControllerRecipeMetadataName)
                    ? null
                    : connectionControllerRecipeMetadataName,
                Objects = new List<InspectionTcpObjectResult>()
            };

            if (currentSnapshot)
            {
                if (snapshot.JudgementComplete)
                {
                    DateTime resultAt = isParameterResultReviewSnapshotForTcp &&
                        parameterResultReviewTcpResultAtUtc.HasValue
                        ? parameterResultReviewTcpResultAtUtc.Value
                        : lastCameraInspectionCompletedUtc ?? DateTime.UtcNow;
                    message.ResultAtUtc = resultAt.ToString("o");
                }
                Dictionary<int, ResultReviewDefectGrade> defectGrades = snapshot.JudgementComplete
                    ? GetInspectionOverviewDefectGrades(snapshot)
                    : new Dictionary<int, ResultReviewDefectGrade>();
                Dictionary<int, ResultReviewGoodJudgementGrade> sizeGrades = snapshot.SizeGrades ??
                    new Dictionary<int, ResultReviewGoodJudgementGrade>();
                Dictionary<int, Point> positions = snapshot.ObjectGridPositions ??
                    CreateObjectDetectionResultReviewObjectGridPositions(snapshot.Objects);
                foreach (ObjectDefinitionDetectedObject detectedObject in
                    snapshot.Objects ?? new List<ObjectDefinitionDetectedObject>())
                {
                    ResultReviewGoodJudgementGrade sizeGrade;
                    if (!sizeGrades.TryGetValue(detectedObject.Number, out sizeGrade))
                    {
                        sizeGrade = ResultReviewGoodJudgementGrade.Pending;
                    }
                    ResultReviewDefectGrade defectGrade;
                    if (!defectGrades.TryGetValue(detectedObject.Number, out defectGrade))
                    {
                        defectGrade = ResultReviewDefectGrade.Pending;
                    }
                    Point position;
                    if (!positions.TryGetValue(detectedObject.Number, out position))
                    {
                        position = new Point(message.Objects.Count, 0);
                    }
                    ResultReviewGoodJudgementGrade combined = GetInspectionOverviewCombinedGrade(
                        sizeGrade, defectGrade);
                    message.Objects.Add(new InspectionTcpObjectResult
                    {
                        Number = detectedObject.Number,
                        Row = Math.Max(0, position.Y),
                        Column = Math.Max(0, position.X),
                        SizeGrade = GetWireSizeGrade(sizeGrade),
                        DefectGrade = GetWireDefectGrade(defectGrade),
                        CombinedGrade = GetWireSizeGrade(combined)
                    });
                }
                if (!snapshot.JudgementComplete && message.Objects.Count == 0 &&
                    snapshot.Parameter != null && snapshot.Parameter.ColumnCount > 0 &&
                    snapshot.Parameter.RowCount > 0)
                {
                    long expectedCount = (long)snapshot.Parameter.ColumnCount * snapshot.Parameter.RowCount;
                    if (expectedCount <= 10000)
                    {
                        for (int number = 1; number <= expectedCount; number++)
                        {
                            message.Objects.Add(new InspectionTcpObjectResult
                            {
                                Number = number,
                                Row = (number - 1) / snapshot.Parameter.ColumnCount,
                                Column = (number - 1) % snapshot.Parameter.ColumnCount,
                                SizeGrade = "Pending",
                                DefectGrade = "Pending",
                                CombinedGrade = "Pending"
                            });
                        }
                    }
                }
                message.ObjectCount = message.Objects.Count;
                message.ACount = message.Objects.Count(item => item.CombinedGrade == "A");
                message.BCount = message.Objects.Count(item => item.CombinedGrade == "B");
                message.NgCount = message.Objects.Count(item => item.CombinedGrade == "NG");
                message.PendingCount = message.Objects.Count(item => item.CombinedGrade == "Pending");
            }
            return message;
        }

        private static string GetWireSizeGrade(ResultReviewGoodJudgementGrade grade)
        {
            switch (grade)
            {
                case ResultReviewGoodJudgementGrade.A: return "A";
                case ResultReviewGoodJudgementGrade.B: return "B";
                case ResultReviewGoodJudgementGrade.Ng: return "NG";
                default: return "Pending";
            }
        }

        private static string GetWireDefectGrade(ResultReviewDefectGrade grade)
        {
            switch (grade)
            {
                case ResultReviewDefectGrade.A: return "A";
                case ResultReviewDefectGrade.Ng: return "NG";
                default: return "Pending";
            }
        }

        private string GetLocalStationName()
        {
            if (cameraDeviceSettings != null && !string.IsNullOrWhiteSpace(cameraDeviceSettings.ProgramName))
            {
                return cameraDeviceSettings.ProgramName.Trim();
            }
            return "攝影機 " + (cameraDeviceSettings == null ? 1 : cameraDeviceSettings.SoftwareId).ToString();
        }

        private void ConnectControllerStationViewsChanged()
        {
            RefreshConnectionControllerCards();
            UpdateConnectionControllerNetworkStatus();
        }
    }
}
