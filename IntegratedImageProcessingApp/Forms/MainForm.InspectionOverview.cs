using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private enum InspectionOverviewFilter
        {
            All,
            Ng,
            Pending
        }

        private sealed class InspectionOverviewItem
        {
            public int ObjectNumber { get; set; }
            public int GridRow { get; set; }
            public int GridColumn { get; set; }
            public Rectangle Bounds { get; set; }
            public ResultReviewGoodJudgementGrade SizeGrade { get; set; }
            public ResultReviewDefectGrade DefectGrade { get; set; }
            public ResultReviewGoodJudgementGrade CombinedGrade { get; set; }
        }

        private sealed class InspectionOverviewTile : Control
        {
            public InspectionOverviewItem Item { get; set; }
            public bool IsSelected { get; set; }

            public InspectionOverviewTile()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint |
                    ControlStyles.StandardClick |
                    ControlStyles.StandardDoubleClick, true);
                BackColor = Color.White;
                Cursor = Cursors.Hand;
                Margin = new Padding(2);
                TabStop = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = ClientRectangle;
                bounds.Width--;
                bounds.Height--;
                using (var background = new SolidBrush(IsSelected
                    ? Color.FromArgb(239, 247, 255)
                    : BackColor))
                using (var border = new Pen(IsSelected
                    ? Color.FromArgb(0, 105, 210)
                    : Color.FromArgb(205, 213, 223), IsSelected ? 2f : 1f))
                {
                    graphics.FillRectangle(background, bounds);
                    graphics.DrawRectangle(border, bounds);
                }

                if (Item == null)
                {
                    return;
                }

                string number = Item.ObjectNumber.ToString(CultureInfo.CurrentCulture);
                TextRenderer.DrawText(
                    graphics,
                    number,
                    Font,
                    new Rectangle(2, 4, Math.Max(0, Width - 4), Math.Max(16, Height / 2)),
                    Color.FromArgb(30, 41, 59),
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding);

                int chipWidth = Math.Min(13, Math.Max(8, (Width - 16) / 3));
                int chipHeight = Math.Min(13, Math.Max(8, Height / 5));
                int gap = Math.Min(4, Math.Max(1, (Width - chipWidth * 3) / 4));
                int totalWidth = chipWidth * 3 + gap * 2;
                int x = Math.Max(2, (Width - totalWidth) / 2);
                int y = Height - chipHeight - 8;
                Color[] colors =
                {
                    GetGradeColor(Item.SizeGrade),
                    GetGradeColor(Item.DefectGrade),
                    GetGradeColor(Item.CombinedGrade)
                };
                foreach (Color color in colors)
                {
                    Rectangle chip = new Rectangle(x, y, chipWidth, chipHeight);
                    using (var brush = new SolidBrush(color))
                    using (var border = new Pen(Color.FromArgb(85, 50, 50, 50)))
                    {
                        graphics.FillRectangle(brush, chip);
                        graphics.DrawRectangle(border, chip);
                    }
                    x += chipWidth + gap;
                }
            }
        }

        private Panel inspectionOverviewPanel;
        private TableLayoutPanel inspectionOverviewMatrix;
        private Panel inspectionOverviewMatrixHost;
        private ImageDisplayControl inspectionOverviewImageDisplay;
        private Label inspectionOverviewNoImageLabel;
        private Label inspectionOverviewSelectedNumberLabel;
        private Label[] inspectionOverviewGradeLabels;
        private Label inspectionOverviewCountLabel;
        private Label inspectionOverviewRecipeLabel;
        private Label inspectionOverviewCameraLabel;
        private Label inspectionOverviewTcpLabel;
        private Panel inspectionOverviewCameraDot;
        private Panel inspectionOverviewTcpDot;
        private Button inspectionOverviewAllFilterButton;
        private Button inspectionOverviewNgFilterButton;
        private Button inspectionOverviewPendingFilterButton;
        private Button inspectionOverviewSendButton;
        private Button inspectionOverviewConnectCameraButton;
        private Button inspectionOverviewContinuousCaptureButton;
        private Button inspectionOverviewRunInspectionButton;
        private Timer inspectionOverviewCameraTimer;
        private List<InspectionOverviewItem> inspectionOverviewItems =
            new List<InspectionOverviewItem>();
        private int? inspectionOverviewSelectedObjectNumber;
        private InspectionOverviewFilter inspectionOverviewFilter;
        private volatile bool isInspectionOverviewActive;
        private volatile bool isInspectionOverviewContinuousCaptureActive;
        private volatile bool isInspectionOverviewInspectionModeActive;
        private bool inspectionOverviewInspectionStartedPreview;
        private bool inspectionOverviewHasUninspectedCameraImage;
        private bool inspectionOverviewCameraImageCapturedSubscribed;
        private int inspectionOverviewAcceptNextCameraFrame;
        private long inspectionOverviewCameraImageSequence;
        private int inspectionOverviewCameraImageUpdateInFlight;
        private CameraCaptureApp.Services.CameraGrayscaleFrame inspectionOverviewPendingGrayscaleFrame;
        private SizeType[] inspectionOverviewPreviousColumnTypes;
        private float[] inspectionOverviewPreviousColumnWidths;

        private void ShowInspectionOverviewPanel()
        {
            if (inspectionOverviewPanel == null || inspectionOverviewPanel.IsDisposed)
            {
                inspectionOverviewPanel = CreateInspectionOverviewPanel();
                centerPanel.Controls.Add(inspectionOverviewPanel);
            }

            if (!isInspectionOverviewActive)
            {
                inspectionOverviewPreviousColumnTypes = mainLayoutPanel.ColumnStyles
                    .Cast<ColumnStyle>().Select(style => style.SizeType).ToArray();
                inspectionOverviewPreviousColumnWidths = mainLayoutPanel.ColumnStyles
                    .Cast<ColumnStyle>().Select(style => style.Width).ToArray();
                isInspectionOverviewActive = true;
            }
            if (cameraCaptureHostControl != null)
            {
                cameraCaptureHostControl.SuppressAutomaticInspectionForwarding = true;
                cameraCaptureHostControl.OverviewCaptureForwardingEnabled = true;
                Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 1);
                if (!inspectionOverviewCameraImageCapturedSubscribed)
                {
                    cameraCaptureHostControl.OverviewImageCaptured +=
                        CameraCaptureHostControl_OverviewImageCaptured;
                    inspectionOverviewCameraImageCapturedSubscribed = true;
                }
            }

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
                centerPanel.Visible = true;
                inspectionOverviewPanel.Visible = true;
                inspectionOverviewPanel.BringToFront();
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }

            RefreshInspectionOverview();
            UpdateInspectionOverviewCameraStatus();
            if (inspectionOverviewCameraTimer == null)
            {
                inspectionOverviewCameraTimer = new Timer { Interval = 500 };
                inspectionOverviewCameraTimer.Tick += delegate
                {
                    UpdateInspectionOverviewCameraStatus();
                };
                if (components != null)
                {
                    components.Add(inspectionOverviewCameraTimer);
                }
            }
            inspectionOverviewCameraTimer.Start();
        }

        private void ExitInspectionOverviewMode()
        {
            if (!isInspectionOverviewActive)
            {
                return;
            }

            bool inspectionModeRemainsActive = isInspectionOverviewInspectionModeActive;
            bool continuousCaptureRemainsActive = isInspectionOverviewContinuousCaptureActive;
            bool captureModeRemainsActive = inspectionModeRemainsActive ||
                continuousCaptureRemainsActive;
            isInspectionOverviewActive = false;
            if (cameraCaptureHostControl != null)
            {
                cameraCaptureHostControl.OverviewCaptureForwardingEnabled = false;
                cameraCaptureHostControl.SuppressAutomaticInspectionForwarding =
                    captureModeRemainsActive || remoteContinuousCaptureActive ||
                    Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) != 0;
                if (inspectionOverviewCameraImageCapturedSubscribed)
                {
                    cameraCaptureHostControl.OverviewImageCaptured -=
                        CameraCaptureHostControl_OverviewImageCaptured;
                    inspectionOverviewCameraImageCapturedSubscribed = false;
                }
            }
            if (inspectionOverviewCameraTimer != null)
            {
                inspectionOverviewCameraTimer.Stop();
            }
            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
            if (inspectionOverviewPanel != null)
            {
                inspectionOverviewPanel.Visible = false;
            }
            if (!captureModeRemainsActive && !inspectionOverviewHasUninspectedCameraImage &&
                inspectionOverviewImageDisplay != null &&
                !inspectionOverviewImageDisplay.IsDisposed)
            {
                inspectionOverviewImageDisplay.ClearImage();
            }

            mainLayoutPanel.SuspendLayout();
            try
            {
                imageLayoutPanel.Visible = true;
                rightPanel.Visible = true;
                centerPanel.Visible = !IsImageDisplayUpdateSuppressed;
                if (inspectionOverviewPreviousColumnTypes != null &&
                    inspectionOverviewPreviousColumnWidths != null)
                {
                    for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count &&
                        index < inspectionOverviewPreviousColumnTypes.Length &&
                        index < inspectionOverviewPreviousColumnWidths.Length; index++)
                    {
                        mainLayoutPanel.ColumnStyles[index].SizeType =
                            inspectionOverviewPreviousColumnTypes[index];
                        mainLayoutPanel.ColumnStyles[index].Width =
                            inspectionOverviewPreviousColumnWidths[index];
                    }
                }
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }
        }

        private Panel CreateInspectionOverviewPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(245, 247, 250),
                Name = "inspectionOverviewPanel",
                Visible = false
            };
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(20, 53, 86),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(12, 2, 12, 2)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
            var identity = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            identity.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            identity.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            var title = new Label
            {
                Text = "檢測總覽",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            inspectionOverviewRecipeLabel = new Label
            {
                Text = "最近一次完成結果",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(214, 225, 237),
                Font = new Font("Microsoft JhengHei UI", 8F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            identity.Controls.Add(title, 0, 0);
            identity.Controls.Add(inspectionOverviewRecipeLabel, 0, 1);
            var connectionStatus = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 7, 0, 0),
                Margin = Padding.Empty
            };
            inspectionOverviewTcpLabel = CreateOverviewConnectionLabel("尚未啟用");
            inspectionOverviewTcpDot = CreateOverviewStatusDot(Color.FromArgb(145, 153, 163));
            var tcpTitle = CreateOverviewConnectionLabel("TCP/IP");
            inspectionOverviewCameraLabel = CreateOverviewConnectionLabel("未連線");
            inspectionOverviewCameraDot = CreateOverviewStatusDot(Color.FromArgb(145, 153, 163));
            var cameraTitle = CreateOverviewConnectionLabel("相機");
            connectionStatus.Controls.Add(inspectionOverviewTcpLabel);
            connectionStatus.Controls.Add(inspectionOverviewTcpDot);
            connectionStatus.Controls.Add(tcpTitle);
            connectionStatus.Controls.Add(CreateOverviewSeparator());
            connectionStatus.Controls.Add(inspectionOverviewCameraLabel);
            connectionStatus.Controls.Add(inspectionOverviewCameraDot);
            connectionStatus.Controls.Add(cameraTitle);
            header.Controls.Add(identity, 0, 0);
            header.Controls.Add(connectionStatus, 1, 0);

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = Color.White,
                Padding = new Padding(8)
            };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            var imageHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 251, 253),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 8)
            };
            inspectionOverviewImageDisplay = new ImageDisplayControl
            {
                Dock = DockStyle.Fill,
                TitleText = "處理後影像"
            };
            inspectionOverviewNoImageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "尚無已完成的檢測結果",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(105, 115, 128),
                BackColor = Color.FromArgb(250, 251, 253),
                Visible = true
            };
            imageHost.Controls.Add(inspectionOverviewImageDisplay);
            imageHost.Controls.Add(inspectionOverviewNoImageLabel);
            inspectionOverviewNoImageLabel.BringToFront();
            left.Controls.Add(imageHost, 0, 0);

            var selectionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            selectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            selectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            selectionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inspectionOverviewSelectedNumberLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "未選取物件",
                Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 45, 58),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };
            var grades = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty
            };
            for (int column = 0; column < 3; column++)
            {
                grades.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            grades.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            grades.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inspectionOverviewGradeLabels = new Label[3];
            string[] gradeTitles = { "尺寸", "缺陷", "綜合" };
            for (int index = 0; index < gradeTitles.Length; index++)
            {
                var cell = new Panel
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(index == 0 ? 0 : 3, 0, 3, 0),
                    BackColor = Color.FromArgb(248, 249, 251),
                    BorderStyle = BorderStyle.FixedSingle
                };
                var titleLabel = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 18,
                    Text = gradeTitles[index],
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(92, 101, 116),
                    Font = new Font("Microsoft JhengHei UI", 8.5F)
                };
                inspectionOverviewGradeLabels[index] = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "待確認",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    BackColor = GetGradeColor(ResultReviewGoodJudgementGrade.Pending),
                    Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold)
                };
                cell.Controls.Add(inspectionOverviewGradeLabels[index]);
                cell.Controls.Add(titleLabel);
                grades.Controls.Add(cell, index, 1);
            }
            selectionLayout.Controls.Add(inspectionOverviewSelectedNumberLabel, 0, 0);
            selectionLayout.Controls.Add(grades, 0, 1);
            left.Controls.Add(selectionLayout, 0, 1);

            inspectionOverviewSendButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "送至參數結果確認",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(22, 112, 196),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                Enabled = false,
                Margin = Padding.Empty
            };
            inspectionOverviewSendButton.FlatAppearance.BorderSize = 0;
            inspectionOverviewSendButton.Click += InspectionOverviewSendButton_Click;
            left.Controls.Add(inspectionOverviewSendButton, 0, 2);

            var cameraActions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 0)
            };
            for (int column = 0; column < 3; column++)
            {
                cameraActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            inspectionOverviewConnectCameraButton = CreateInspectionOverviewActionButton("連結攝影機");
            inspectionOverviewConnectCameraButton.Click += InspectionOverviewConnectCameraButton_Click;
            inspectionOverviewContinuousCaptureButton = CreateInspectionOverviewActionButton("連續取像");
            inspectionOverviewContinuousCaptureButton.Click +=
                InspectionOverviewContinuousCaptureButton_Click;
            inspectionOverviewRunInspectionButton = CreateInspectionOverviewActionButton("檢測");
            inspectionOverviewRunInspectionButton.Click += InspectionOverviewRunInspectionButton_Click;
            cameraActions.Controls.Add(inspectionOverviewConnectCameraButton, 0, 0);
            cameraActions.Controls.Add(inspectionOverviewContinuousCaptureButton, 1, 0);
            cameraActions.Controls.Add(inspectionOverviewRunInspectionButton, 2, 0);
            left.Controls.Add(cameraActions, 0, 3);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                BackColor = Color.White,
                Padding = new Padding(8)
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            inspectionOverviewCountLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "物件 0　NG 0　待確認 0",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 45, 58),
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                AutoEllipsis = true
            };
            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(0, 4, 0, 0)
            };
            inspectionOverviewPendingFilterButton = CreateInspectionOverviewFilterButton("待確認");
            inspectionOverviewPendingFilterButton.Click += delegate
            {
                SetInspectionOverviewFilter(InspectionOverviewFilter.Pending);
            };
            inspectionOverviewNgFilterButton = CreateInspectionOverviewFilterButton("NG");
            inspectionOverviewNgFilterButton.Click += delegate
            {
                SetInspectionOverviewFilter(InspectionOverviewFilter.Ng);
            };
            inspectionOverviewAllFilterButton = CreateInspectionOverviewFilterButton("全部");
            inspectionOverviewAllFilterButton.Click += delegate
            {
                SetInspectionOverviewFilter(InspectionOverviewFilter.All);
            };
            filters.Controls.Add(inspectionOverviewPendingFilterButton);
            filters.Controls.Add(inspectionOverviewNgFilterButton);
            filters.Controls.Add(inspectionOverviewAllFilterButton);
            toolbar.Controls.Add(inspectionOverviewCountLabel, 0, 0);
            toolbar.Controls.Add(filters, 1, 0);
            right.Controls.Add(toolbar, 0, 0);

            var legend = new Label
            {
                Dock = DockStyle.Fill,
                Text = "每格色標順序：尺寸 / 缺陷 / 綜合　　綠 A規　黃 B規　紅 NG　灰 待確認",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                AutoEllipsis = true,
                Padding = new Padding(2, 0, 0, 0)
            };
            right.Controls.Add(legend, 0, 1);

            inspectionOverviewMatrixHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(241, 244, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty
            };
            inspectionOverviewMatrix = new TableLayoutPanel
            {
                Location = Point.Empty,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = new Padding(2),
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            inspectionOverviewMatrixHost.Controls.Add(inspectionOverviewMatrix);
            inspectionOverviewMatrixHost.SizeChanged += delegate
            {
                LayoutInspectionOverviewMatrix();
            };
            right.Controls.Add(inspectionOverviewMatrixHost, 0, 2);

            body.Controls.Add(left, 0, 0);
            body.Controls.Add(right, 1, 0);
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(body, 0, 1);
            panel.Controls.Add(root);
            return panel;
        }

        private static Label CreateOverviewConnectionLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                Margin = new Padding(4, 0, 4, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private static Panel CreateOverviewStatusDot(Color color)
        {
            return new Panel
            {
                Width = 12,
                Height = 12,
                BackColor = color,
                Margin = new Padding(2, 4, 2, 0)
            };
        }

        private static Control CreateOverviewSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 20,
                BackColor = Color.FromArgb(150, 170, 190),
                Margin = new Padding(9, 0, 9, 0)
            };
        }

        private static Button CreateInspectionOverviewFilterButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Width = text == "待確認" ? 76 : 60,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(3, 0, 0, 0),
                Font = new Font("Microsoft JhengHei UI", 8.5F)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(199, 207, 217);
            return button;
        }

        private static Button CreateInspectionOverviewActionButton(string text)
        {
            var button = new Button
            {
                Dock = DockStyle.Fill,
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 243, 248),
                ForeColor = Color.FromArgb(35, 45, 58),
                Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Bold),
                Margin = new Padding(2, 0, 2, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 214);
            return button;
        }

        private void RefreshInspectionOverview()
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            inspectionOverviewItems = new List<InspectionOverviewItem>();
            inspectionOverviewSelectedNumberLabel.Text = "未選取物件";
            SetInspectionOverviewGradeLabel(0, "待確認", ResultReviewGoodJudgementGrade.Pending);
            SetInspectionOverviewGradeLabel(1, "待確認", ResultReviewGoodJudgementGrade.Pending);
            SetInspectionOverviewGradeLabel(2, "待確認", ResultReviewGoodJudgementGrade.Pending);
            inspectionOverviewSendButton.Enabled = false;
            inspectionOverviewRecipeLabel.Text = "最近一次完成結果";

            bool hasSnapshotImage = snapshot != null &&
                (snapshot.OriginalLargeSource != null || snapshot.OriginalBitmap != null);
            if (snapshot == null || (!inspectionOverviewHasUninspectedCameraImage &&
                snapshot.Parameter == null && !hasSnapshotImage))
            {
                inspectionOverviewCountLabel.Text = "尚無已完成的檢測結果";
                inspectionOverviewNoImageLabel.Text = "尚無已完成的檢測結果\r\n請先到「參數結果確認」執行確認";
                inspectionOverviewNoImageLabel.Visible = true;
                inspectionOverviewImageDisplay.ClearImage();
                RenderInspectionOverviewMatrix();
                UpdateInspectionOverviewFilterButtons();
                return;
            }

            SetInspectionOverviewImage(snapshot);
            inspectionOverviewRecipeLabel.Text = snapshot.JudgementComplete && snapshot.Parameter != null
                ? "檢測參數：" +
                    (string.IsNullOrWhiteSpace(snapshot.Parameter.DisplayName)
                        ? snapshot.Parameter.Id
                        : snapshot.Parameter.DisplayName)
                : inspectionOverviewHasUninspectedCameraImage
                    ? "相機影像 #" + inspectionOverviewCameraImageSequence.ToString(CultureInfo.CurrentCulture) +
                        "，尚未執行檢測"
                : string.Equals(
                    snapshot.UndeterminedReason,
                    "正在執行尺寸與缺陷判定",
                    StringComparison.Ordinal)
                    ? "新影像檢測中，結果暫列待確認"
                    : "未判定影像（待確認）";
            Dictionary<int, ResultReviewGoodJudgementGrade> sizeGrades =
                snapshot.SizeGrades ??
                    new Dictionary<int, ResultReviewGoodJudgementGrade>();
            Dictionary<int, ResultReviewDefectGrade> defectGrades = snapshot.Parameter == null
                ? new Dictionary<int, ResultReviewDefectGrade>()
                : GetInspectionOverviewDefectGrades(snapshot);
            Dictionary<int, Point> gridPositions = snapshot.ObjectGridPositions ??
                CreateObjectDetectionResultReviewObjectGridPositions(snapshot.Objects);

            foreach (ObjectDefinitionDetectedObject detectedObject in
                snapshot.Objects ?? new List<ObjectDefinitionDetectedObject>())
            {
                ResultReviewGoodJudgementGrade sizeGrade;
                if (!sizeGrades.TryGetValue(detectedObject.Number, out sizeGrade))
                {
                    sizeGrade = ResultReviewGoodJudgementGrade.Pending;
                }
                Point position;
                if (!gridPositions.TryGetValue(detectedObject.Number, out position))
                {
                    position = new Point(inspectionOverviewItems.Count % 3,
                        inspectionOverviewItems.Count / 3);
                }

                ResultReviewDefectGrade defectGrade;
                if (!defectGrades.TryGetValue(detectedObject.Number, out defectGrade))
                {
                    defectGrade = ResultReviewDefectGrade.Pending;
                }
                inspectionOverviewItems.Add(new InspectionOverviewItem
                {
                    ObjectNumber = detectedObject.Number,
                    GridColumn = position.X,
                    GridRow = position.Y,
                    Bounds = detectedObject.Bounds,
                    SizeGrade = sizeGrade,
                    DefectGrade = defectGrade,
                    CombinedGrade = GetInspectionOverviewCombinedGrade(sizeGrade, defectGrade)
                });
            }

            if (!snapshot.JudgementComplete && inspectionOverviewItems.Count == 0 &&
                snapshot.Parameter != null && snapshot.Parameter.ColumnCount > 0 &&
                snapshot.Parameter.RowCount > 0)
            {
                long expectedCount = (long)snapshot.Parameter.ColumnCount * snapshot.Parameter.RowCount;
                if (expectedCount <= 10000)
                {
                    for (int number = 1; number <= expectedCount; number++)
                    {
                        inspectionOverviewItems.Add(new InspectionOverviewItem
                        {
                            ObjectNumber = number,
                            GridColumn = (number - 1) % snapshot.Parameter.ColumnCount,
                            GridRow = (number - 1) / snapshot.Parameter.ColumnCount,
                            Bounds = Rectangle.Empty,
                            SizeGrade = ResultReviewGoodJudgementGrade.Pending,
                            DefectGrade = ResultReviewDefectGrade.Pending,
                            CombinedGrade = ResultReviewGoodJudgementGrade.Pending
                        });
                    }
                }
            }

            int aCount = inspectionOverviewItems.Count(item =>
                item.CombinedGrade == ResultReviewGoodJudgementGrade.A);
            int bCount = inspectionOverviewItems.Count(item =>
                item.CombinedGrade == ResultReviewGoodJudgementGrade.B);
            int ngCount = inspectionOverviewItems.Count(item =>
                item.CombinedGrade == ResultReviewGoodJudgementGrade.Ng);
            int pendingCount = inspectionOverviewItems.Count(item =>
                item.CombinedGrade == ResultReviewGoodJudgementGrade.Pending);
            inspectionOverviewCountLabel.Text =
                "物件 " + inspectionOverviewItems.Count.ToString("N0", CultureInfo.CurrentCulture) +
                "　A規 " + aCount.ToString("N0", CultureInfo.CurrentCulture) +
                "　B規 " + bCount.ToString("N0", CultureInfo.CurrentCulture) +
                "　NG " + ngCount.ToString("N0", CultureInfo.CurrentCulture) +
                "　待確認 " + pendingCount.ToString("N0", CultureInfo.CurrentCulture);
            inspectionOverviewSendButton.Enabled = snapshot.JudgementComplete &&
                inspectionOverviewItems.Count > 0;
            if (inspectionOverviewSelectedObjectNumber.HasValue &&
                !inspectionOverviewItems.Any(item =>
                    item.ObjectNumber == inspectionOverviewSelectedObjectNumber.Value))
            {
                inspectionOverviewSelectedObjectNumber = null;
            }
            UpdateInspectionOverviewSelectedItem();
            RenderInspectionOverviewMatrix();
            UpdateInspectionOverviewFilterButtons();
            UpdateInspectionOverviewCameraButtons();
        }

        private void SetInspectionOverviewImage(ObjectDetectionResultReviewSnapshot snapshot)
        {
            if (inspectionOverviewHasUninspectedCameraImage &&
                inspectionOverviewImageDisplay != null &&
                inspectionOverviewImageDisplay.HasImage)
            {
                inspectionOverviewImageDisplay.TitleText = "原始影像";
                inspectionOverviewNoImageLabel.Visible = false;
                return;
            }

            inspectionOverviewImageDisplay.ClearImage();
            if (snapshot.OriginalLargeSource != null)
            {
                inspectionOverviewImageDisplay.TitleText = "原始影像";
                inspectionOverviewImageDisplay.SetSharedLargeImageSource(snapshot.OriginalLargeSource);
            }
            else if (snapshot.OriginalBitmap != null)
            {
                inspectionOverviewImageDisplay.TitleText = "原始影像";
                inspectionOverviewImageDisplay.SetDisplayImage(
                    new Bitmap(snapshot.OriginalBitmap), false);
            }
            else
            {
                inspectionOverviewImageDisplay.TitleText = "原始影像";
            }

            bool hasImage = inspectionOverviewImageDisplay.HasImage;
            inspectionOverviewNoImageLabel.Text = hasImage
                ? string.Empty
                : "目前結果沒有可顯示的影像";
            inspectionOverviewNoImageLabel.Visible = !hasImage;
            if (!hasImage)
            {
                inspectionOverviewNoImageLabel.BringToFront();
            }
        }

        private Dictionary<int, ResultReviewDefectGrade> GetInspectionOverviewDefectGrades(
            ObjectDetectionResultReviewSnapshot snapshot)
        {
            var grades = (snapshot.Objects ?? new List<ObjectDefinitionDetectedObject>())
                .Where(item => item != null)
                .GroupBy(item => item.Number)
                .ToDictionary(group => group.Key, group => ResultReviewDefectGrade.Pending);
            ObjectDetectionParameterSettings parameter = snapshot.Parameter;
            if (parameter.DefectInspectionRegions == null ||
                !parameter.DefectInspectionRegions.Any(region => region != null &&
                    string.Equals(region.ObjectDefinitionId, parameter.ObjectDefinitionId,
                        StringComparison.Ordinal) &&
                    region.Right > region.Left && region.Bottom > region.Top))
            {
                return grades;
            }

            bool hasEnabledAnalysis = false;
            foreach (ObjectDetectionDefectCoreSettings core in
                (parameter.DefectDetectionCores ?? new List<ObjectDetectionDefectCoreSettings>())
                    .Take(ObjectDetectionDefectCoreKeys.Length))
            {
                if (core == null || !core.Enabled)
                {
                    continue;
                }

                hasEnabledAnalysis = true;
                if ((!core.DarkThresholdEnabled && !core.BrightThresholdEnabled) ||
                    snapshot.CoreResults == null ||
                    !snapshot.CoreResults.ContainsKey(core.CoreKey))
                {
                    return grades;
                }
            }

            if (IsObjectDetectionDefectFrequencyIntegrationRequired(parameter, 0))
            {
                hasEnabledAnalysis = true;
                if (snapshot.FrequencyResult == null)
                {
                    return grades;
                }
            }
            if (IsObjectDetectionDefectFrequencyIntegrationRequired(parameter, 1))
            {
                hasEnabledAnalysis = true;
                if (snapshot.Frequency2Result == null)
                {
                    return grades;
                }
            }
            if (!hasEnabledAnalysis)
            {
                return grades;
            }

            HashSet<int> ngObjects = new HashSet<int>(
                (snapshot.IntegrationGroups ?? new List<ObjectDetectionDefectIntegrationGroup>())
                    .Where(group => group != null)
                    .Select(group => group.ObjectNumber));
            foreach (int objectNumber in grades.Keys.ToList())
            {
                grades[objectNumber] = ngObjects.Contains(objectNumber)
                    ? ResultReviewDefectGrade.Ng
                    : ResultReviewDefectGrade.A;
            }
            return grades;
        }

        private static ResultReviewGoodJudgementGrade GetInspectionOverviewCombinedGrade(
            ResultReviewGoodJudgementGrade sizeGrade,
            ResultReviewDefectGrade defectGrade)
        {
            if (sizeGrade == ResultReviewGoodJudgementGrade.Ng ||
                defectGrade == ResultReviewDefectGrade.Ng)
            {
                return ResultReviewGoodJudgementGrade.Ng;
            }
            if (sizeGrade == ResultReviewGoodJudgementGrade.Pending ||
                defectGrade == ResultReviewDefectGrade.Pending)
            {
                return ResultReviewGoodJudgementGrade.Pending;
            }
            return sizeGrade == ResultReviewGoodJudgementGrade.B
                ? ResultReviewGoodJudgementGrade.B
                : ResultReviewGoodJudgementGrade.A;
        }

        private void RenderInspectionOverviewMatrix()
        {
            inspectionOverviewMatrix.SuspendLayout();
            try
            {
                foreach (Control control in inspectionOverviewMatrix.Controls.Cast<Control>().ToList())
                {
                    inspectionOverviewMatrix.Controls.Remove(control);
                    control.Dispose();
                }
                inspectionOverviewMatrix.ColumnStyles.Clear();
                inspectionOverviewMatrix.RowStyles.Clear();
                int columns = Math.Max(1, inspectionOverviewItems
                    .Select(item => item.GridColumn).DefaultIfEmpty(0).Max() + 1);
                int rows = Math.Max(1, inspectionOverviewItems
                    .Select(item => item.GridRow).DefaultIfEmpty(0).Max() + 1);
                inspectionOverviewMatrix.ColumnCount = columns + 1;
                inspectionOverviewMatrix.RowCount = rows + 1;
                inspectionOverviewMatrix.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, 38F));
                for (int column = 0; column < columns; column++)
                {
                    inspectionOverviewMatrix.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100F / columns));
                }
                inspectionOverviewMatrix.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
                for (int row = 0; row < rows; row++)
                {
                    inspectionOverviewMatrix.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
                }

                inspectionOverviewMatrix.Controls.Add(CreateInspectionOverviewAxisLabel(""), 0, 0);
                for (int column = 0; column < columns; column++)
                {
                    inspectionOverviewMatrix.Controls.Add(
                        CreateInspectionOverviewAxisLabel(
                            (column + 1).ToString("D2", CultureInfo.CurrentCulture)),
                        column + 1,
                        0);
                }
                for (int row = 0; row < rows; row++)
                {
                    inspectionOverviewMatrix.Controls.Add(
                        CreateInspectionOverviewAxisLabel(GetInspectionOverviewRowLabel(row)),
                        0,
                        row + 1);
                }

                foreach (InspectionOverviewItem item in inspectionOverviewItems)
                {
                    var tile = new InspectionOverviewTile
                    {
                        Dock = DockStyle.Fill,
                        Item = item,
                        IsSelected = inspectionOverviewSelectedObjectNumber == item.ObjectNumber,
                        Visible = IsInspectionOverviewItemVisible(item),
                        AccessibleName = "物件 " + item.ObjectNumber.ToString(CultureInfo.CurrentCulture),
                        AccessibleDescription =
                            "尺寸 " + GetInspectionOverviewGradeText(item.SizeGrade) +
                            "；缺陷 " + GetInspectionOverviewGradeText(item.DefectGrade) +
                            "；綜合 " + GetInspectionOverviewGradeText(item.CombinedGrade),
                        MinimumSize = new Size(48, 58)
                    };
                    tile.Click += InspectionOverviewTile_Click;
                    tile.DoubleClick += InspectionOverviewTile_DoubleClick;
                    inspectionOverviewMatrix.Controls.Add(
                        tile,
                        item.GridColumn + 1,
                        item.GridRow + 1);
                }
                LayoutInspectionOverviewMatrix();
            }
            finally
            {
                inspectionOverviewMatrix.ResumeLayout(true);
            }
        }

        private void LayoutInspectionOverviewMatrix()
        {
            if (inspectionOverviewMatrix == null || inspectionOverviewMatrixHost == null ||
                inspectionOverviewMatrix.IsDisposed || inspectionOverviewMatrixHost.IsDisposed)
            {
                return;
            }
            int columns = Math.Max(1, inspectionOverviewMatrix.ColumnCount - 1);
            int availableWidth = Math.Max(1, inspectionOverviewMatrixHost.ClientSize.Width - 2);
            const int minimumCellWidth = 58;
            inspectionOverviewMatrix.Width = Math.Max(
                availableWidth,
                columns * minimumCellWidth + 42);
            inspectionOverviewMatrix.Height = Math.Max(92,
                (inspectionOverviewMatrix.RowCount - 1) * 66 + 30);
            if (inspectionOverviewMatrix.ColumnStyles.Count > 0)
            {
                inspectionOverviewMatrix.ColumnStyles[0].SizeType = SizeType.Absolute;
                inspectionOverviewMatrix.ColumnStyles[0].Width = 38F;
            }
            for (int column = 1; column < inspectionOverviewMatrix.ColumnStyles.Count; column++)
            {
                inspectionOverviewMatrix.ColumnStyles[column].SizeType = SizeType.Percent;
                inspectionOverviewMatrix.ColumnStyles[column].Width = 100F / columns;
            }
            inspectionOverviewMatrixHost.AutoScrollMinSize = inspectionOverviewMatrix.Size;
        }

        private static Label CreateInspectionOverviewAxisLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(232, 237, 243),
                ForeColor = Color.FromArgb(57, 69, 84),
                Font = new Font("Microsoft JhengHei UI", 8F, FontStyle.Bold),
                Margin = new Padding(1)
            };
        }

        private static string GetInspectionOverviewRowLabel(int row)
        {
            int value = Math.Max(0, row);
            string label = string.Empty;
            do
            {
                label = (char)('A' + value % 26) + label;
                value = value / 26 - 1;
            }
            while (value >= 0);
            return label;
        }

        private bool IsInspectionOverviewItemVisible(InspectionOverviewItem item)
        {
            if (inspectionOverviewFilter == InspectionOverviewFilter.Ng)
            {
                return item.CombinedGrade == ResultReviewGoodJudgementGrade.Ng;
            }
            if (inspectionOverviewFilter == InspectionOverviewFilter.Pending)
            {
                return item.CombinedGrade == ResultReviewGoodJudgementGrade.Pending;
            }
            return true;
        }

        private void InspectionOverviewTile_Click(object sender, EventArgs e)
        {
            var tile = sender as InspectionOverviewTile;
            if (tile == null || tile.Item == null)
            {
                return;
            }
            inspectionOverviewSelectedObjectNumber = tile.Item.ObjectNumber;
            UpdateInspectionOverviewSelectedItem();
            foreach (InspectionOverviewTile other in inspectionOverviewMatrix.Controls
                .OfType<InspectionOverviewTile>())
            {
                other.IsSelected = other.Item.ObjectNumber == tile.Item.ObjectNumber;
                other.Invalidate();
            }
            if (inspectionOverviewImageDisplay != null && inspectionOverviewImageDisplay.HasImage &&
                tile.Item.Bounds.Width > 0 && tile.Item.Bounds.Height > 0)
            {
                inspectionOverviewImageDisplay.FocusOnSourceRectangle(tile.Item.Bounds);
            }
        }

        private void InspectionOverviewTile_DoubleClick(object sender, EventArgs e)
        {
            var tile = sender as InspectionOverviewTile;
            if (tile == null || tile.Item == null ||
                inspectionOverviewSelectedObjectNumber != tile.Item.ObjectNumber)
            {
                return;
            }

            inspectionOverviewSelectedObjectNumber = null;
            UpdateInspectionOverviewSelectedItem();
            foreach (InspectionOverviewTile other in inspectionOverviewMatrix.Controls
                .OfType<InspectionOverviewTile>())
            {
                other.IsSelected = false;
                other.Invalidate();
            }
            if (inspectionOverviewImageDisplay != null && inspectionOverviewImageDisplay.HasImage)
            {
                inspectionOverviewImageDisplay.ResetViewToFit(false);
            }
        }

        private void UpdateInspectionOverviewSelectedItem()
        {
            InspectionOverviewItem selected = inspectionOverviewSelectedObjectNumber.HasValue
                ? inspectionOverviewItems.FirstOrDefault(item =>
                    item.ObjectNumber == inspectionOverviewSelectedObjectNumber.Value)
                : null;
            if (selected == null)
            {
                inspectionOverviewSelectedNumberLabel.Text = "未選取物件";
                SetInspectionOverviewGradeLabel(0, "待確認", ResultReviewGoodJudgementGrade.Pending);
                SetInspectionOverviewGradeLabel(1, "待確認", ResultReviewGoodJudgementGrade.Pending);
                SetInspectionOverviewGradeLabel(2, "待確認", ResultReviewGoodJudgementGrade.Pending);
                return;
            }
            inspectionOverviewSelectedNumberLabel.Text = "選取物件結果（物件 " +
                selected.ObjectNumber.ToString(CultureInfo.CurrentCulture) + ")";
            SetInspectionOverviewGradeLabel(0, GetInspectionOverviewGradeText(selected.SizeGrade),
                selected.SizeGrade);
            SetInspectionOverviewGradeLabel(1, GetInspectionOverviewGradeText(selected.DefectGrade),
                selected.DefectGrade == ResultReviewDefectGrade.A
                    ? ResultReviewGoodJudgementGrade.A
                    : selected.DefectGrade == ResultReviewDefectGrade.Ng
                        ? ResultReviewGoodJudgementGrade.Ng
                        : ResultReviewGoodJudgementGrade.Pending);
            SetInspectionOverviewGradeLabel(2, GetInspectionOverviewGradeText(selected.CombinedGrade),
                selected.CombinedGrade);
        }

        private void SetInspectionOverviewGradeLabel(
            int index,
            string text,
            ResultReviewGoodJudgementGrade grade)
        {
            if (inspectionOverviewGradeLabels == null || index < 0 ||
                index >= inspectionOverviewGradeLabels.Length ||
                inspectionOverviewGradeLabels[index] == null)
            {
                return;
            }
            inspectionOverviewGradeLabels[index].Text = text;
            inspectionOverviewGradeLabels[index].BackColor = GetGradeColor(grade);
            inspectionOverviewGradeLabels[index].ForeColor =
                grade == ResultReviewGoodJudgementGrade.B
                    ? Color.FromArgb(35, 35, 35)
                    : Color.White;
        }

        private static Color GetGradeColor(ResultReviewGoodJudgementGrade grade)
        {
            return GetObjectDetectionResultReviewGoodJudgementColor(grade);
        }

        private static Color GetGradeColor(ResultReviewDefectGrade grade)
        {
            switch (grade)
            {
                case ResultReviewDefectGrade.A:
                    return GetObjectDetectionResultReviewGoodJudgementColor(
                        ResultReviewGoodJudgementGrade.A);
                case ResultReviewDefectGrade.Ng:
                    return GetObjectDetectionResultReviewGoodJudgementColor(
                        ResultReviewGoodJudgementGrade.Ng);
                default:
                    return GetObjectDetectionResultReviewGoodJudgementColor(
                        ResultReviewGoodJudgementGrade.Pending);
            }
        }

        private static string GetInspectionOverviewGradeText(ResultReviewGoodJudgementGrade grade)
        {
            switch (grade)
            {
                case ResultReviewGoodJudgementGrade.A:
                    return "A規";
                case ResultReviewGoodJudgementGrade.B:
                    return "B規";
                case ResultReviewGoodJudgementGrade.Ng:
                    return "NG";
                default:
                    return "待確認";
            }
        }

        private static string GetInspectionOverviewGradeText(ResultReviewDefectGrade grade)
        {
            switch (grade)
            {
                case ResultReviewDefectGrade.A:
                    return "A規";
                case ResultReviewDefectGrade.Ng:
                    return "NG";
                default:
                    return "待確認";
            }
        }

        private void SetInspectionOverviewFilter(InspectionOverviewFilter filter)
        {
            inspectionOverviewFilter = filter;
            foreach (InspectionOverviewTile tile in inspectionOverviewMatrix.Controls
                .OfType<InspectionOverviewTile>())
            {
                tile.Visible = IsInspectionOverviewItemVisible(tile.Item);
            }
            UpdateInspectionOverviewFilterButtons();
        }

        private void UpdateInspectionOverviewFilterButtons()
        {
            SetInspectionOverviewFilterButtonStyle(inspectionOverviewAllFilterButton,
                inspectionOverviewFilter == InspectionOverviewFilter.All);
            SetInspectionOverviewFilterButtonStyle(inspectionOverviewNgFilterButton,
                inspectionOverviewFilter == InspectionOverviewFilter.Ng);
            SetInspectionOverviewFilterButtonStyle(inspectionOverviewPendingFilterButton,
                inspectionOverviewFilter == InspectionOverviewFilter.Pending);
        }

        private static void SetInspectionOverviewFilterButtonStyle(Button button, bool selected)
        {
            if (button == null || button.IsDisposed)
            {
                return;
            }
            button.BackColor = selected
                ? Color.FromArgb(22, 112, 196)
                : Color.FromArgb(245, 247, 250);
            button.ForeColor = selected ? Color.White : Color.FromArgb(40, 50, 65);
            button.FlatAppearance.BorderColor = selected
                ? Color.FromArgb(22, 112, 196)
                : Color.FromArgb(199, 207, 217);
        }

        private void CameraCaptureHostControl_OverviewImageCaptured(
            object sender,
            CameraCaptureApp.Services.CameraImageAcquiredEventArgs e)
        {
            if (e == null || !isInspectionOverviewActive ||
                (!isInspectionOverviewContinuousCaptureActive && !isInspectionOverviewInspectionModeActive &&
                    Interlocked.CompareExchange(ref inspectionOverviewAcceptNextCameraFrame, 0, 0) == 0) ||
                IsDisposed || !IsHandleCreated ||
                Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 0, 0) != 0 ||
                Interlocked.CompareExchange(ref inspectionOverviewCameraImageUpdateInFlight, 1, 0) != 0)
            {
                return;
            }
            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);

            Bitmap image = null;
            Bitmap pendingImage = null;
            try
            {
                CameraCaptureApp.Services.CameraGrayscaleFrame frame = e.GrayscaleFrame;
                if (frame == null)
                {
                    throw new InvalidOperationException("相機沒有提供可用的灰階影像資料。");
                }
                image = e.CreateImageCopy();
                long sequenceNumber = e.SequenceNumber;
                pendingImage = image;
                image = null;
                Bitmap capturedImage = pendingImage;
                CameraCaptureApp.Services.CameraGrayscaleFrame capturedFrame = frame;
                BeginInvoke(new Action(async delegate
                {
                    bool transferred = false;
                    try
                    {
                        if (isInspectionOverviewActive &&
                            inspectionOverviewImageDisplay != null &&
                            !inspectionOverviewImageDisplay.IsDisposed)
                        {
                            transferred = ApplyInspectionOverviewCameraImage(
                                capturedImage,
                                sequenceNumber,
                                capturedFrame);
                            if (transferred && isInspectionOverviewInspectionModeActive)
                            {
                                await RunInspectionOverviewPendingFrameAsync(true);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        SetCameraInspectionStatus("相機影像顯示失敗：" + exception.Message);
                    }
                    finally
                    {
                        if (!transferred && capturedImage != null)
                        {
                            capturedImage.Dispose();
                        }
                        Interlocked.Exchange(ref inspectionOverviewCameraImageUpdateInFlight, 0);
                        UpdateInspectionOverviewCameraButtons();
                    }
                }));
                pendingImage = null;
            }
            catch (Exception exception)
            {
                if (image != null)
                {
                    image.Dispose();
                }
                if (pendingImage != null)
                {
                    pendingImage.Dispose();
                }
                Interlocked.Exchange(ref inspectionOverviewCameraImageUpdateInFlight, 0);
                SetCameraInspectionStatus("相機影像擷取失敗：" + exception.Message);
            }
        }

        private bool ApplyInspectionOverviewCameraImage(
            Bitmap image,
            long sequenceNumber,
            CameraCaptureApp.Services.CameraGrayscaleFrame grayscaleFrame)
        {
            if (image == null || grayscaleFrame == null)
            {
                return false;
            }

            bool imageTransferred = false;
            try
            {
                int width = image.Width;
                int height = image.Height;
                EnsureInspectionOverviewResultReviewParametersInitialized();
                ResultReviewParameterChoice choice = objectDetectionResultReviewParameterComboBox == null
                    ? null
                    : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
                ObjectDetectionParameterSettings parameter = choice == null || choice.Parameter == null
                    ? null
                    : (ObjectDetectionParameterSettings)CloneObjectDetectionResultReviewModel(choice.Parameter);
                var snapshot = new ObjectDetectionResultReviewSnapshot
                {
                    ParameterId = parameter == null ? null : parameter.Id,
                    Parameter = parameter,
                    Objects = new List<ObjectDefinitionDetectedObject>(),
                    SizeGrades = new Dictionary<int, ResultReviewGoodJudgementGrade>(),
                    ObjectGridPositions = new Dictionary<int, Point>(),
                    JudgementComplete = false,
                    UndeterminedReason = "相機影像尚未執行檢測",
                    ImageWidth = width,
                    ImageHeight = height
                };
                inspectionOverviewImageDisplay.TitleText = "原始影像 - 相機 #" +
                    sequenceNumber.ToString(CultureInfo.CurrentCulture);
                inspectionOverviewImageDisplay.SetDisplayImage(image, true);
                imageTransferred = true;
                ClearObjectDetectionResultReviewSnapshot();
                objectDetectionResultReviewSnapshot = snapshot;
                inspectionOverviewSelectedObjectNumber = null;
                inspectionOverviewFilter = InspectionOverviewFilter.All;
                inspectionOverviewHasUninspectedCameraImage = true;
                inspectionOverviewCameraImageSequence = sequenceNumber;
                inspectionOverviewPendingGrayscaleFrame = grayscaleFrame;
                RefreshInspectionOverview();
                SetCameraInspectionStatus("相機影像 #" +
                    sequenceNumber.ToString(CultureInfo.CurrentCulture) +
                    " 已取得，尚未執行檢測；按下「檢測」才會進入尺寸與缺陷判定。");
                QueueStationStatusPublish(false);
            }
            catch (Exception exception)
            {
                SetCameraInspectionStatus("相機影像更新失敗：" + exception.Message);
            }
            return imageTransferred;
        }

        private void InspectionOverviewConnectCameraButton_Click(object sender, EventArgs e)
        {
            string initializationMessage;
            if (!EnsureCameraCaptureProgramInitialized(out initializationMessage))
            {
                SetCameraInspectionStatus("線掃描攝影機程式初始化失敗：" + initializationMessage);
                return;
            }

            if (cameraCaptureHostControl.IsCameraConnected)
            {
                if (isInspectionOverviewInspectionModeActive)
                {
                    StopInspectionOverviewInspectionMode(false);
                }
                if (isInspectionOverviewContinuousCaptureActive)
                {
                    StopInspectionOverviewContinuousCapture(false);
                }
                Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
                cameraCaptureHostControl.DisconnectCamera();
                UpdateInspectionOverviewCameraStatus();
                SetCameraInspectionStatus("攝影機已斷開連線。");
                return;
            }

            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 1);
            if (cameraCaptureHostControl.IsSoftwareTriggerMode)
            {
                DialogResult originConfirmation = MessageBox.Show(
                    this,
                    "目前 Trigger Mode 為 Software Trigger。\r\n\r\n" +
                        "請確認機台位置已回到原點。選擇「是」才會繼續連線。",
                    "確認機台原點",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (originConfirmation != DialogResult.Yes)
                {
                    Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
                    SetCameraInspectionStatus("已取消攝影機連線；請確認機台已回到原點後再試。");
                    return;
                }

                string encoderMessage;
                if (!cameraCaptureHostControl.ClearMeterWheelEncoder(out encoderMessage))
                {
                    Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
                    string failureMessage = "米輪 Encoder Clear 失敗，已取消攝影機連線。\r\n\r\n" +
                        encoderMessage;
                    SetCameraInspectionStatus(failureMessage.Replace("\r\n\r\n", " "));
                    MessageBox.Show(
                        this,
                        failureMessage,
                        "米輪歸零失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            string message;
            bool started = cameraCaptureHostControl.ConnectCameraAndStartPreview(out message);
            if (!started)
            {
                Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
            }
            UpdateInspectionOverviewCameraStatus();
            SetCameraInspectionStatus(started
                ? "攝影機已連線並啟動 Start Preview。"
                : cameraCaptureHostControl.IsCameraConnected
                    ? "攝影機已連線，但預覽啟動失敗：" + message
                    : "攝影機連線失敗：" + message);
        }

        private void InspectionOverviewContinuousCaptureButton_Click(object sender, EventArgs e)
        {
            if (isInspectionOverviewContinuousCaptureActive)
            {
                StopInspectionOverviewContinuousCapture(true);
                SetCameraInspectionStatus("已停止連續取像；最後一張影像仍保留，尚未檢測的影像可按「檢測」。");
                return;
            }

            if (cameraCaptureHostControl == null || !cameraCaptureHostControl.IsCameraConnected)
            {
                SetCameraInspectionStatus("請先連結攝影機。");
                return;
            }

            isInspectionOverviewContinuousCaptureActive = true;
            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
            string message;
            if (!cameraCaptureHostControl.IsCameraPreviewing &&
                !cameraCaptureHostControl.StartPreview(out message))
            {
                isInspectionOverviewContinuousCaptureActive = false;
                SetCameraInspectionStatus("無法開始連續取像：" + message);
                UpdateInspectionOverviewCameraStatus();
                UpdateInspectionOverviewCameraButtons();
                return;
            }

            inspectionOverviewContinuousCaptureButton.Text = "停止取像";
            inspectionOverviewContinuousCaptureButton.BackColor = Color.FromArgb(190, 65, 59);
            inspectionOverviewContinuousCaptureButton.ForeColor = Color.White;
            inspectionOverviewContinuousCaptureButton.FlatAppearance.BorderColor =
                Color.FromArgb(165, 51, 46);
            UpdateInspectionOverviewCameraButtons();
            SetCameraInspectionStatus("連續取像中；只更新原始影像，不會執行檢測。");
        }

        private void StopInspectionOverviewContinuousCapture(bool stopCameraPreview)
        {
            isInspectionOverviewContinuousCaptureActive = false;
            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
            if (cameraCaptureHostControl != null)
            {
                if (stopCameraPreview)
                {
                    if (!remoteContinuousCaptureActive &&
                        Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) == 0)
                    {
                        cameraCaptureHostControl.StopPreview();
                    }
                }
            }
            if (inspectionOverviewContinuousCaptureButton != null &&
                !inspectionOverviewContinuousCaptureButton.IsDisposed)
            {
                inspectionOverviewContinuousCaptureButton.Text = "連續取像";
                inspectionOverviewContinuousCaptureButton.BackColor = Color.FromArgb(239, 243, 248);
                inspectionOverviewContinuousCaptureButton.ForeColor = Color.FromArgb(35, 45, 58);
                inspectionOverviewContinuousCaptureButton.FlatAppearance.BorderColor =
                    Color.FromArgb(190, 201, 214);
            }
            UpdateInspectionOverviewCameraButtons();
            QueueStationStatusPublish(false);
        }

        private async void InspectionOverviewRunInspectionButton_Click(object sender, EventArgs e)
        {
            if (isInspectionOverviewInspectionModeActive)
            {
                StopInspectionOverviewInspectionMode(true);
                SetCameraInspectionStatus("已停止持續檢測；正在處理的影像會完成，之後不再自動檢測。");
                return;
            }

            EnsureInspectionOverviewResultReviewParametersInitialized();
            ResultReviewParameterChoice choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (choice == null || choice.Parameter == null)
            {
                SetCameraInspectionStatus("目前沒有可執行的檢測參數，請先選擇參數。");
                return;
            }

            bool hasCameraImage = inspectionOverviewHasUninspectedCameraImage &&
                inspectionOverviewPendingGrayscaleFrame != null &&
                inspectionOverviewImageDisplay != null && inspectionOverviewImageDisplay.HasImage;
            bool connected = cameraCaptureHostControl != null &&
                cameraCaptureHostControl.IsCameraConnected;
            if (!connected)
            {
                if (hasCameraImage)
                {
                    await RunInspectionOverviewPendingFrameAsync(false);
                    return;
                }

                SetCameraInspectionStatus("請先連結攝影機，或先取得一張相機影像。");
                return;
            }

            bool wasContinuousCaptureActive = isInspectionOverviewContinuousCaptureActive;
            if (wasContinuousCaptureActive)
            {
                StopInspectionOverviewContinuousCapture(false);
            }

            isInspectionOverviewInspectionModeActive = true;
            inspectionOverviewInspectionStartedPreview = wasContinuousCaptureActive ||
                !cameraCaptureHostControl.IsCameraPreviewing;
            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 1);
            cameraCaptureHostControl.OverviewCaptureForwardingEnabled = true;
            if (!wasContinuousCaptureActive && !cameraCaptureHostControl.IsCameraPreviewing)
            {
                string previewMessage;
                if (!cameraCaptureHostControl.StartPreview(out previewMessage))
                {
                    StopInspectionOverviewInspectionMode(false);
                    SetCameraInspectionStatus("無法啟動檢測取像：" + previewMessage);
                    return;
                }
            }

            UpdateInspectionOverviewCameraButtons();
            if (hasCameraImage)
            {
                await RunInspectionOverviewPendingFrameAsync(true);
                return;
            }

            SetCameraInspectionStatus(cameraCaptureHostControl.IsSoftwareTriggerMode
                ? "持續檢測中，等待米輪觸發影像。再按一次「停止檢測」即可停止。"
                : "持續檢測中，等待攝影機影像。再按一次「停止檢測」即可停止。");
        }

        private void StopInspectionOverviewInspectionMode(bool stopCameraPreview)
        {
            bool shouldStopPreview = stopCameraPreview && inspectionOverviewInspectionStartedPreview &&
                !isInspectionOverviewContinuousCaptureActive && !remoteContinuousCaptureActive &&
                Interlocked.CompareExchange(ref remoteInspectionRequested, 0, 0) == 0;
            isInspectionOverviewInspectionModeActive = false;
            inspectionOverviewInspectionStartedPreview = false;
            Interlocked.Exchange(ref inspectionOverviewAcceptNextCameraFrame, 0);
            if (shouldStopPreview && cameraCaptureHostControl != null)
            {
                cameraCaptureHostControl.StopPreview();
            }
            UpdateInspectionOverviewCameraButtons();
        }

        private async Task RunInspectionOverviewPendingFrameAsync(bool keepCameraRunning)
        {
            if (!inspectionOverviewHasUninspectedCameraImage ||
                inspectionOverviewImageDisplay == null || !inspectionOverviewImageDisplay.HasImage)
            {
                return;
            }

            string busyReason;
            if (TryGetCameraInspectionBusyReason(out busyReason) ||
                Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 1, 0) != 0)
            {
                SetCameraInspectionStatus("目前無法開始檢測：" +
                    (string.IsNullOrWhiteSpace(busyReason) ? "其他影像流程尚未完成" : busyReason));
                return;
            }

            try
            {
                CameraCaptureApp.Services.CameraGrayscaleFrame frame =
                    inspectionOverviewPendingGrayscaleFrame;
                if (frame == null)
                {
                    Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                    SetCameraInspectionStatus("目前影像沒有可用的灰階記憶體來源，請重新取像。");
                    return;
                }

                long sequenceNumber = inspectionOverviewCameraImageSequence;
                if (!keepCameraRunning && isInspectionOverviewContinuousCaptureActive)
                {
                    StopInspectionOverviewContinuousCapture(true);
                }
                else if (!keepCameraRunning && cameraCaptureHostControl != null &&
                    cameraCaptureHostControl.IsCameraPreviewing)
                {
                    cameraCaptureHostControl.StopPreview();
                }

                SetCameraInspectionStatus("開始使用目前檢測參數判定相機影像 #" +
                    sequenceNumber.ToString(CultureInfo.CurrentCulture) + "...");
                string previousImagePath = latestCameraInspectionFramePath;
                inspectionOverviewPendingGrayscaleFrame = null;
                await ProcessCameraAcquiredImageAsync(frame, sequenceNumber);
                if (!string.Equals(previousImagePath, latestCameraInspectionFramePath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    inspectionOverviewHasUninspectedCameraImage = false;
                    if (isInspectionOverviewActive)
                    {
                        RefreshInspectionOverview();
                    }
                }
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                SetCameraInspectionStatus("檢測啟動失敗：" + exception.Message);
            }
            finally
            {
                UpdateInspectionOverviewCameraButtons();
            }
        }

        private void UpdateInspectionOverviewCameraButtons()
        {
            bool connected = cameraCaptureHostControl != null &&
                cameraCaptureHostControl.IsCameraConnected;
            bool isBusy = isLoadingImage || isObjectDetectionResultReviewRunning ||
                Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 0, 0) != 0;
            if (inspectionOverviewConnectCameraButton != null &&
                !inspectionOverviewConnectCameraButton.IsDisposed)
            {
                inspectionOverviewConnectCameraButton.Enabled = !isBusy;
                inspectionOverviewConnectCameraButton.Text = connected ? "斷開連線" : "連結攝影機";
                inspectionOverviewConnectCameraButton.BackColor = connected
                    ? Color.FromArgb(190, 65, 59)
                    : Color.FromArgb(239, 243, 248);
                inspectionOverviewConnectCameraButton.ForeColor = connected
                    ? Color.White
                    : Color.FromArgb(35, 45, 58);
                inspectionOverviewConnectCameraButton.FlatAppearance.BorderColor = connected
                    ? Color.FromArgb(165, 51, 46)
                    : Color.FromArgb(190, 201, 214);
            }
            if (inspectionOverviewContinuousCaptureButton != null &&
                !inspectionOverviewContinuousCaptureButton.IsDisposed)
            {
                inspectionOverviewContinuousCaptureButton.Enabled = connected && !isBusy &&
                    !isInspectionOverviewInspectionModeActive;
            }
            if (inspectionOverviewRunInspectionButton != null &&
                !inspectionOverviewRunInspectionButton.IsDisposed)
            {
                inspectionOverviewRunInspectionButton.Enabled =
                    isInspectionOverviewInspectionModeActive ||
                    ((inspectionOverviewHasUninspectedCameraImage || connected) && !isBusy);
                inspectionOverviewRunInspectionButton.Text = isInspectionOverviewInspectionModeActive
                    ? "停止檢測"
                    : "檢測";
                inspectionOverviewRunInspectionButton.BackColor = isInspectionOverviewInspectionModeActive
                    ? Color.FromArgb(190, 65, 59)
                    : Color.FromArgb(239, 243, 248);
                inspectionOverviewRunInspectionButton.ForeColor = isInspectionOverviewInspectionModeActive
                    ? Color.White
                    : Color.FromArgb(35, 45, 58);
                inspectionOverviewRunInspectionButton.FlatAppearance.BorderColor =
                    isInspectionOverviewInspectionModeActive
                        ? Color.FromArgb(165, 51, 46)
                        : Color.FromArgb(190, 201, 214);
            }
        }

        private void EnsureInspectionOverviewResultReviewParametersInitialized()
        {
            if (objectDetectionResultReviewParameterComboBox == null ||
                objectDetectionResultReviewParameterComboBox.IsDisposed)
            {
                if (objectDetectionResultReviewPanel != null &&
                    !objectDetectionResultReviewPanel.IsDisposed)
                {
                    objectDetectionResultReviewPanel.Dispose();
                }
                objectDetectionResultReviewPanel = CreateObjectDetectionResultReviewPanel();
            }

            if (objectDetectionResultReviewParameterComboBox.Items.Count == 0)
            {
                PopulateObjectDetectionResultReviewParameters();
            }
        }

        private void UpdateInspectionOverviewCameraStatus()
        {
            if (inspectionOverviewCameraLabel == null || inspectionOverviewCameraDot == null)
            {
                return;
            }
            bool connected = cameraCaptureHostControl != null &&
                cameraCaptureHostControl.IsCameraConnected;
            inspectionOverviewCameraLabel.Text = connected ? "已連線" : "未連線";
            inspectionOverviewCameraDot.BackColor = connected
                ? Color.FromArgb(77, 190, 105)
                : Color.FromArgb(145, 153, 163);
            UpdateInspectionOverviewTcpStatus();
            UpdateInspectionOverviewCameraButtons();
        }

        private void UpdateInspectionOverviewTcpStatus()
        {
            if (inspectionOverviewTcpLabel != null)
            {
                bool connected = inspectionTcpTransport != null &&
                    inspectionTcpTransport.ConnectedPeerCount > 0;
                inspectionOverviewTcpLabel.Text = connected ? "已連線" : "未連線";
                if (inspectionOverviewTcpDot != null)
                {
                    inspectionOverviewTcpDot.BackColor = connected
                        ? Color.FromArgb(77, 190, 105)
                        : Color.FromArgb(145, 153, 163);
                }
            }
        }

        private void InspectionOverviewSendButton_Click(object sender, EventArgs e)
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            if (snapshot == null || inspectionOverviewItems.Count == 0)
            {
                return;
            }
            activeObjectDetectionParameterId = snapshot.ParameterId;
            objectDetectionResultReviewSelectedParameterId = snapshot.ParameterId;
            if (inspectionOverviewSelectedObjectNumber.HasValue)
            {
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber =
                    inspectionOverviewSelectedObjectNumber.Value;
                objectDetectionResultReviewSelectedDefectObjectNumber =
                    inspectionOverviewSelectedObjectNumber.Value;
                selectedObjectDetectionNumber = inspectionOverviewSelectedObjectNumber.Value;
            }
            else
            {
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber = null;
                objectDetectionResultReviewSelectedDefectObjectNumber = null;
                selectedObjectDetectionNumber = -1;
            }

            int resultReviewIndex = functionListBox.Items.IndexOf(ObjectDetectionResultReviewMenuText);
            if (resultReviewIndex >= 0)
            {
                functionListBox.SelectedIndex = resultReviewIndex;
            }
            if (inspectionOverviewSelectedObjectNumber.HasValue)
            {
                RefreshObjectDetectionResultReviewGoodJudgementView();
                RefreshObjectDetectionResultReviewDefectObjectButtons();
                FocusObjectDetectionResultReviewObject(
                    inspectionOverviewSelectedObjectNumber.Value);
            }
        }
    }
}
