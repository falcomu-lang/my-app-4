namespace IntegratedImageProcessingApp.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel topBarPanel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label projectLabel;
        private System.Windows.Forms.TableLayoutPanel mainLayoutPanel;
        private System.Windows.Forms.Panel leftPanel;
        private System.Windows.Forms.Panel centerPanel;
        private System.Windows.Forms.Panel rightPanel;
        private System.Windows.Forms.Label leftPanelTitleLabel;
        private System.Windows.Forms.ListBox functionListBox;
        private System.Windows.Forms.Label rightPanelTitleLabel;
        private System.Windows.Forms.Panel parameterPanel;
        private System.Windows.Forms.Label parameterPlaceholderLabel;
        private System.Windows.Forms.TableLayoutPanel imageLayoutPanel;
        private System.Windows.Forms.TabControl leftImageTabControl;
        private System.Windows.Forms.TabPage leftOriginalTabPage;
        private System.Windows.Forms.TabPage leftProcessedTabPage;
        private System.Windows.Forms.TabPage leftObjectsTabPage;
        private System.Windows.Forms.TabPage leftDebugTabPage;
        private System.Windows.Forms.TabControl rightImageTabControl;
        private System.Windows.Forms.TabPage rightOriginalTabPage;
        private System.Windows.Forms.TabPage rightProcessedTabPage;
        private System.Windows.Forms.TabPage rightObjectsTabPage;
        private System.Windows.Forms.TabPage rightDebugTabPage;
        private System.Windows.Forms.Panel leftOriginalDisplayHostPanel;
        private System.Windows.Forms.Panel leftProcessedDisplayHostPanel;
        private System.Windows.Forms.Panel leftObjectsDisplayHostPanel;
        private System.Windows.Forms.Panel leftDebugDisplayHostPanel;
        private System.Windows.Forms.Panel rightOriginalDisplayHostPanel;
        private System.Windows.Forms.Panel rightProcessedDisplayHostPanel;
        private System.Windows.Forms.Panel rightObjectsDisplayHostPanel;
        private System.Windows.Forms.Panel rightDebugDisplayHostPanel;
        private System.Windows.Forms.Panel leftOriginalPreviewTopPanel;
        private System.Windows.Forms.Label leftOriginalPreviewTitleLabel;
        private System.Windows.Forms.Label leftOriginalPreviewResolutionLabel;
        private System.Windows.Forms.Panel leftOriginalPreviewViewPanel;
        private System.Windows.Forms.Panel leftOriginalPreviewBottomPanel;
        private System.Windows.Forms.Label leftOriginalPreviewStatusLabel;
        private System.Windows.Forms.Button leftOriginalPreviewFitButton;
        private System.Windows.Forms.Panel leftProcessedPreviewTopPanel;
        private System.Windows.Forms.Label leftProcessedPreviewTitleLabel;
        private System.Windows.Forms.Label leftProcessedPreviewResolutionLabel;
        private System.Windows.Forms.Panel leftProcessedPreviewViewPanel;
        private System.Windows.Forms.Panel leftProcessedPreviewBottomPanel;
        private System.Windows.Forms.Label leftProcessedPreviewStatusLabel;
        private System.Windows.Forms.Button leftProcessedPreviewFitButton;
        private System.Windows.Forms.Panel leftObjectsPreviewTopPanel;
        private System.Windows.Forms.Label leftObjectsPreviewTitleLabel;
        private System.Windows.Forms.Label leftObjectsPreviewResolutionLabel;
        private System.Windows.Forms.Panel leftObjectsPreviewViewPanel;
        private System.Windows.Forms.Panel leftObjectsPreviewBottomPanel;
        private System.Windows.Forms.Label leftObjectsPreviewStatusLabel;
        private System.Windows.Forms.Button leftObjectsPreviewFitButton;
        private System.Windows.Forms.Panel leftDebugPreviewTopPanel;
        private System.Windows.Forms.Label leftDebugPreviewTitleLabel;
        private System.Windows.Forms.Label leftDebugPreviewResolutionLabel;
        private System.Windows.Forms.Panel leftDebugPreviewViewPanel;
        private System.Windows.Forms.Panel leftDebugPreviewBottomPanel;
        private System.Windows.Forms.Label leftDebugPreviewStatusLabel;
        private System.Windows.Forms.Button leftDebugPreviewFitButton;
        private System.Windows.Forms.Panel rightOriginalPreviewTopPanel;
        private System.Windows.Forms.Label rightOriginalPreviewTitleLabel;
        private System.Windows.Forms.Label rightOriginalPreviewResolutionLabel;
        private System.Windows.Forms.Panel rightOriginalPreviewViewPanel;
        private System.Windows.Forms.Panel rightOriginalPreviewBottomPanel;
        private System.Windows.Forms.Label rightOriginalPreviewStatusLabel;
        private System.Windows.Forms.Button rightOriginalPreviewFitButton;
        private System.Windows.Forms.Panel rightProcessedPreviewTopPanel;
        private System.Windows.Forms.Label rightProcessedPreviewTitleLabel;
        private System.Windows.Forms.Label rightProcessedPreviewResolutionLabel;
        private System.Windows.Forms.Panel rightProcessedPreviewViewPanel;
        private System.Windows.Forms.Panel rightProcessedPreviewBottomPanel;
        private System.Windows.Forms.Label rightProcessedPreviewStatusLabel;
        private System.Windows.Forms.Button rightProcessedPreviewFitButton;
        private System.Windows.Forms.Panel rightObjectsPreviewTopPanel;
        private System.Windows.Forms.Label rightObjectsPreviewTitleLabel;
        private System.Windows.Forms.Label rightObjectsPreviewResolutionLabel;
        private System.Windows.Forms.Panel rightObjectsPreviewViewPanel;
        private System.Windows.Forms.Panel rightObjectsPreviewBottomPanel;
        private System.Windows.Forms.Label rightObjectsPreviewStatusLabel;
        private System.Windows.Forms.Button rightObjectsPreviewFitButton;
        private System.Windows.Forms.Panel rightDebugPreviewTopPanel;
        private System.Windows.Forms.Label rightDebugPreviewTitleLabel;
        private System.Windows.Forms.Label rightDebugPreviewResolutionLabel;
        private System.Windows.Forms.Panel rightDebugPreviewViewPanel;
        private System.Windows.Forms.Panel rightDebugPreviewBottomPanel;
        private System.Windows.Forms.Label rightDebugPreviewStatusLabel;
        private System.Windows.Forms.Button rightDebugPreviewFitButton;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.topBarPanel = new System.Windows.Forms.Panel();
            this.projectLabel = new System.Windows.Forms.Label();
            this.titleLabel = new System.Windows.Forms.Label();
            this.mainLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.leftPanel = new System.Windows.Forms.Panel();
            this.functionListBox = new System.Windows.Forms.ListBox();
            this.leftPanelTitleLabel = new System.Windows.Forms.Label();
            this.centerPanel = new System.Windows.Forms.Panel();
            this.imageLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.leftImageTabControl = new System.Windows.Forms.TabControl();
            this.leftOriginalTabPage = new System.Windows.Forms.TabPage();
            this.leftOriginalDisplayHostPanel = new System.Windows.Forms.Panel();
            this.leftProcessedTabPage = new System.Windows.Forms.TabPage();
            this.leftProcessedDisplayHostPanel = new System.Windows.Forms.Panel();
            this.leftObjectsTabPage = new System.Windows.Forms.TabPage();
            this.leftObjectsDisplayHostPanel = new System.Windows.Forms.Panel();
            this.leftDebugTabPage = new System.Windows.Forms.TabPage();
            this.leftDebugDisplayHostPanel = new System.Windows.Forms.Panel();
            this.rightImageTabControl = new System.Windows.Forms.TabControl();
            this.rightOriginalTabPage = new System.Windows.Forms.TabPage();
            this.rightOriginalDisplayHostPanel = new System.Windows.Forms.Panel();
            this.rightProcessedTabPage = new System.Windows.Forms.TabPage();
            this.rightProcessedDisplayHostPanel = new System.Windows.Forms.Panel();
            this.rightObjectsTabPage = new System.Windows.Forms.TabPage();
            this.rightObjectsDisplayHostPanel = new System.Windows.Forms.Panel();
            this.rightDebugTabPage = new System.Windows.Forms.TabPage();
            this.rightDebugDisplayHostPanel = new System.Windows.Forms.Panel();
            this.leftOriginalPreviewTopPanel = new System.Windows.Forms.Panel();
            this.leftOriginalPreviewTitleLabel = new System.Windows.Forms.Label();
            this.leftOriginalPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.leftOriginalPreviewViewPanel = new System.Windows.Forms.Panel();
            this.leftOriginalPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.leftOriginalPreviewStatusLabel = new System.Windows.Forms.Label();
            this.leftOriginalPreviewFitButton = new System.Windows.Forms.Button();
            this.leftProcessedPreviewTopPanel = new System.Windows.Forms.Panel();
            this.leftProcessedPreviewTitleLabel = new System.Windows.Forms.Label();
            this.leftProcessedPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.leftProcessedPreviewViewPanel = new System.Windows.Forms.Panel();
            this.leftProcessedPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.leftProcessedPreviewStatusLabel = new System.Windows.Forms.Label();
            this.leftProcessedPreviewFitButton = new System.Windows.Forms.Button();
            this.leftObjectsPreviewTopPanel = new System.Windows.Forms.Panel();
            this.leftObjectsPreviewTitleLabel = new System.Windows.Forms.Label();
            this.leftObjectsPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.leftObjectsPreviewViewPanel = new System.Windows.Forms.Panel();
            this.leftObjectsPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.leftObjectsPreviewStatusLabel = new System.Windows.Forms.Label();
            this.leftObjectsPreviewFitButton = new System.Windows.Forms.Button();
            this.leftDebugPreviewTopPanel = new System.Windows.Forms.Panel();
            this.leftDebugPreviewTitleLabel = new System.Windows.Forms.Label();
            this.leftDebugPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.leftDebugPreviewViewPanel = new System.Windows.Forms.Panel();
            this.leftDebugPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.leftDebugPreviewStatusLabel = new System.Windows.Forms.Label();
            this.leftDebugPreviewFitButton = new System.Windows.Forms.Button();
            this.rightOriginalPreviewTopPanel = new System.Windows.Forms.Panel();
            this.rightOriginalPreviewTitleLabel = new System.Windows.Forms.Label();
            this.rightOriginalPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.rightOriginalPreviewViewPanel = new System.Windows.Forms.Panel();
            this.rightOriginalPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.rightOriginalPreviewStatusLabel = new System.Windows.Forms.Label();
            this.rightOriginalPreviewFitButton = new System.Windows.Forms.Button();
            this.rightProcessedPreviewTopPanel = new System.Windows.Forms.Panel();
            this.rightProcessedPreviewTitleLabel = new System.Windows.Forms.Label();
            this.rightProcessedPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.rightProcessedPreviewViewPanel = new System.Windows.Forms.Panel();
            this.rightProcessedPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.rightProcessedPreviewStatusLabel = new System.Windows.Forms.Label();
            this.rightProcessedPreviewFitButton = new System.Windows.Forms.Button();
            this.rightObjectsPreviewTopPanel = new System.Windows.Forms.Panel();
            this.rightObjectsPreviewTitleLabel = new System.Windows.Forms.Label();
            this.rightObjectsPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.rightObjectsPreviewViewPanel = new System.Windows.Forms.Panel();
            this.rightObjectsPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.rightObjectsPreviewStatusLabel = new System.Windows.Forms.Label();
            this.rightObjectsPreviewFitButton = new System.Windows.Forms.Button();
            this.rightDebugPreviewTopPanel = new System.Windows.Forms.Panel();
            this.rightDebugPreviewTitleLabel = new System.Windows.Forms.Label();
            this.rightDebugPreviewResolutionLabel = new System.Windows.Forms.Label();
            this.rightDebugPreviewViewPanel = new System.Windows.Forms.Panel();
            this.rightDebugPreviewBottomPanel = new System.Windows.Forms.Panel();
            this.rightDebugPreviewStatusLabel = new System.Windows.Forms.Label();
            this.rightDebugPreviewFitButton = new System.Windows.Forms.Button();
            this.rightPanel = new System.Windows.Forms.Panel();
            this.parameterPanel = new System.Windows.Forms.Panel();
            this.parameterPlaceholderLabel = new System.Windows.Forms.Label();
            this.rightPanelTitleLabel = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.topBarPanel.SuspendLayout();
            this.mainLayoutPanel.SuspendLayout();
            this.leftPanel.SuspendLayout();
            this.centerPanel.SuspendLayout();
            this.imageLayoutPanel.SuspendLayout();
            this.leftImageTabControl.SuspendLayout();
            this.leftOriginalTabPage.SuspendLayout();
            this.leftProcessedTabPage.SuspendLayout();
            this.leftObjectsTabPage.SuspendLayout();
            this.leftDebugTabPage.SuspendLayout();
            this.rightImageTabControl.SuspendLayout();
            this.rightOriginalTabPage.SuspendLayout();
            this.rightProcessedTabPage.SuspendLayout();
            this.rightObjectsTabPage.SuspendLayout();
            this.rightDebugTabPage.SuspendLayout();
            this.leftOriginalDisplayHostPanel.SuspendLayout();
            this.leftOriginalPreviewTopPanel.SuspendLayout();
            this.leftOriginalPreviewBottomPanel.SuspendLayout();
            this.leftProcessedDisplayHostPanel.SuspendLayout();
            this.leftProcessedPreviewTopPanel.SuspendLayout();
            this.leftProcessedPreviewBottomPanel.SuspendLayout();
            this.leftObjectsDisplayHostPanel.SuspendLayout();
            this.leftObjectsPreviewTopPanel.SuspendLayout();
            this.leftObjectsPreviewBottomPanel.SuspendLayout();
            this.leftDebugDisplayHostPanel.SuspendLayout();
            this.leftDebugPreviewTopPanel.SuspendLayout();
            this.leftDebugPreviewBottomPanel.SuspendLayout();
            this.rightOriginalDisplayHostPanel.SuspendLayout();
            this.rightOriginalPreviewTopPanel.SuspendLayout();
            this.rightOriginalPreviewBottomPanel.SuspendLayout();
            this.rightProcessedDisplayHostPanel.SuspendLayout();
            this.rightProcessedPreviewTopPanel.SuspendLayout();
            this.rightProcessedPreviewBottomPanel.SuspendLayout();
            this.rightObjectsDisplayHostPanel.SuspendLayout();
            this.rightObjectsPreviewTopPanel.SuspendLayout();
            this.rightObjectsPreviewBottomPanel.SuspendLayout();
            this.rightDebugDisplayHostPanel.SuspendLayout();
            this.rightDebugPreviewTopPanel.SuspendLayout();
            this.rightDebugPreviewBottomPanel.SuspendLayout();
            this.rightPanel.SuspendLayout();
            this.parameterPanel.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // topBarPanel
            // 
            this.topBarPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(247)))), ((int)(((byte)(249)))), ((int)(((byte)(252)))));
            this.topBarPanel.Controls.Add(this.projectLabel);
            this.topBarPanel.Controls.Add(this.titleLabel);
            this.topBarPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topBarPanel.Location = new System.Drawing.Point(0, 0);
            this.topBarPanel.Name = "topBarPanel";
            this.topBarPanel.Size = new System.Drawing.Size(1280, 48);
            this.topBarPanel.TabIndex = 0;
            // 
            // projectLabel
            // 
            this.projectLabel.AutoSize = true;
            this.projectLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(92)))), ((int)(((byte)(101)))), ((int)(((byte)(116)))));
            this.projectLabel.Location = new System.Drawing.Point(198, 17);
            this.projectLabel.Name = "projectLabel";
            this.projectLabel.Size = new System.Drawing.Size(127, 15);
            this.projectLabel.TabIndex = 1;
            this.projectLabel.Text = "影像處理介面框架";
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(39)))), ((int)(((byte)(46)))));
            this.titleLabel.Location = new System.Drawing.Point(16, 14);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(176, 19);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "整合式影像處理軟件";
            // 
            // mainLayoutPanel
            // 
            this.mainLayoutPanel.BackColor = System.Drawing.Color.White;
            this.mainLayoutPanel.ColumnCount = 3;
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.mainLayoutPanel.Controls.Add(this.leftPanel, 0, 0);
            this.mainLayoutPanel.Controls.Add(this.centerPanel, 1, 0);
            this.mainLayoutPanel.Controls.Add(this.rightPanel, 2, 0);
            this.mainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayoutPanel.Location = new System.Drawing.Point(0, 48);
            this.mainLayoutPanel.Name = "mainLayoutPanel";
            this.mainLayoutPanel.RowCount = 1;
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayoutPanel.Size = new System.Drawing.Size(1280, 650);
            this.mainLayoutPanel.TabIndex = 1;
            // 
            // leftPanel
            // 
            this.leftPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.leftPanel.Controls.Add(this.functionListBox);
            this.leftPanel.Controls.Add(this.leftPanelTitleLabel);
            this.leftPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftPanel.Location = new System.Drawing.Point(0, 0);
            this.leftPanel.Margin = new System.Windows.Forms.Padding(0);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Padding = new System.Windows.Forms.Padding(14, 12, 14, 12);
            this.leftPanel.Size = new System.Drawing.Size(256, 650);
            this.leftPanel.TabIndex = 0;
            // 
            // functionListBox
            // 
            this.functionListBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.functionListBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.functionListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.functionListBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.functionListBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(46)))), ((int)(((byte)(56)))));
            this.functionListBox.FormattingEnabled = true;
            this.functionListBox.IntegralHeight = false;
            this.functionListBox.ItemHeight = 38;
            this.functionListBox.Items.AddRange(new object[] {
            "檔案與影像",
            "亮度 / 對比",
            "濾波與銳化",
            "邊緣偵測",
            "幾何校正",
            "量測工具",
            "輸出設定"});
            this.functionListBox.Location = new System.Drawing.Point(14, 46);
            this.functionListBox.Name = "functionListBox";
            this.functionListBox.Size = new System.Drawing.Size(228, 592);
            this.functionListBox.TabIndex = 1;
            this.functionListBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.FunctionListBox_DrawItem);
            this.functionListBox.SelectedIndexChanged += new System.EventHandler(this.FunctionListBox_SelectedIndexChanged);
            // 
            // leftPanelTitleLabel
            // 
            this.leftPanelTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.leftPanelTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.leftPanelTitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(39)))), ((int)(((byte)(46)))));
            this.leftPanelTitleLabel.Location = new System.Drawing.Point(14, 12);
            this.leftPanelTitleLabel.Name = "leftPanelTitleLabel";
            this.leftPanelTitleLabel.Size = new System.Drawing.Size(228, 34);
            this.leftPanelTitleLabel.TabIndex = 0;
            this.leftPanelTitleLabel.Text = "功能選項";
            this.leftPanelTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // centerPanel
            // 
            this.centerPanel.BackColor = System.Drawing.Color.White;
            this.centerPanel.Controls.Add(this.imageLayoutPanel);
            this.centerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.centerPanel.Location = new System.Drawing.Point(256, 0);
            this.centerPanel.Margin = new System.Windows.Forms.Padding(0);
            this.centerPanel.Name = "centerPanel";
            this.centerPanel.Padding = new System.Windows.Forms.Padding(16, 14, 16, 14);
            this.centerPanel.Size = new System.Drawing.Size(768, 650);
            this.centerPanel.TabIndex = 1;
            // 
            // imageLayoutPanel
            // 
            this.imageLayoutPanel.ColumnCount = 2;
            this.imageLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.imageLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.imageLayoutPanel.Controls.Add(this.leftImageTabControl, 0, 0);
            this.imageLayoutPanel.Controls.Add(this.rightImageTabControl, 1, 0);
            this.imageLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageLayoutPanel.Location = new System.Drawing.Point(16, 14);
            this.imageLayoutPanel.Name = "imageLayoutPanel";
            this.imageLayoutPanel.RowCount = 1;
            this.imageLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.imageLayoutPanel.Size = new System.Drawing.Size(736, 622);
            this.imageLayoutPanel.TabIndex = 0;
            // 
            // leftImageTabControl
            // 
            this.leftImageTabControl.Controls.Add(this.leftOriginalTabPage);
            this.leftImageTabControl.Controls.Add(this.leftProcessedTabPage);
            this.leftImageTabControl.Controls.Add(this.leftObjectsTabPage);
            this.leftImageTabControl.Controls.Add(this.leftDebugTabPage);
            this.leftImageTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftImageTabControl.Location = new System.Drawing.Point(0, 0);
            this.leftImageTabControl.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.leftImageTabControl.Name = "leftImageTabControl";
            this.leftImageTabControl.SelectedIndex = 0;
            this.leftImageTabControl.Size = new System.Drawing.Size(360, 622);
            this.leftImageTabControl.TabIndex = 0;
            // 
            // leftOriginalTabPage
            // 
            this.leftOriginalTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftOriginalTabPage.Controls.Add(this.leftOriginalDisplayHostPanel);
            this.leftOriginalTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftOriginalTabPage.Name = "leftOriginalTabPage";
            this.leftOriginalTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftOriginalTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftOriginalTabPage.TabIndex = 0;
            this.leftOriginalTabPage.Text = "原圖";
            // 
            // leftOriginalDisplayHostPanel
            // 
            this.leftOriginalDisplayHostPanel.Controls.Add(this.leftOriginalPreviewViewPanel);
            this.leftOriginalDisplayHostPanel.Controls.Add(this.leftOriginalPreviewBottomPanel);
            this.leftOriginalDisplayHostPanel.Controls.Add(this.leftOriginalPreviewTopPanel);
            this.leftOriginalDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftOriginalDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.leftOriginalDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftOriginalDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.leftOriginalDisplayHostPanel.Name = "leftOriginalDisplayHostPanel";
            this.leftOriginalDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.leftOriginalDisplayHostPanel.TabIndex = 0;
            // 
            // leftOriginalPreviewTopPanel
            // 
            this.leftOriginalPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftOriginalPreviewTopPanel.Controls.Add(this.leftOriginalPreviewResolutionLabel);
            this.leftOriginalPreviewTopPanel.Controls.Add(this.leftOriginalPreviewTitleLabel);
            this.leftOriginalPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.leftOriginalPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.leftOriginalPreviewTopPanel.Name = "leftOriginalPreviewTopPanel";
            this.leftOriginalPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.leftOriginalPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.leftOriginalPreviewTopPanel.TabIndex = 2;
            // 
            // leftOriginalPreviewResolutionLabel
            // 
            this.leftOriginalPreviewResolutionLabel.AutoSize = true;
            this.leftOriginalPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftOriginalPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.leftOriginalPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.leftOriginalPreviewResolutionLabel.Name = "leftOriginalPreviewResolutionLabel";
            this.leftOriginalPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.leftOriginalPreviewResolutionLabel.TabIndex = 1;
            this.leftOriginalPreviewResolutionLabel.Text = "0 x 0";
            // 
            // leftOriginalPreviewTitleLabel
            // 
            this.leftOriginalPreviewTitleLabel.AutoSize = true;
            this.leftOriginalPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftOriginalPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.leftOriginalPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.leftOriginalPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.leftOriginalPreviewTitleLabel.Name = "leftOriginalPreviewTitleLabel";
            this.leftOriginalPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.leftOriginalPreviewTitleLabel.TabIndex = 0;
            this.leftOriginalPreviewTitleLabel.Text = "左側 原圖";
            // 
            // leftOriginalPreviewViewPanel
            // 
            this.leftOriginalPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.leftOriginalPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftOriginalPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftOriginalPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.leftOriginalPreviewViewPanel.Name = "leftOriginalPreviewViewPanel";
            this.leftOriginalPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.leftOriginalPreviewViewPanel.TabIndex = 1;
            // 
            // leftOriginalPreviewBottomPanel
            // 
            this.leftOriginalPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftOriginalPreviewBottomPanel.Controls.Add(this.leftOriginalPreviewFitButton);
            this.leftOriginalPreviewBottomPanel.Controls.Add(this.leftOriginalPreviewStatusLabel);
            this.leftOriginalPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.leftOriginalPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.leftOriginalPreviewBottomPanel.Name = "leftOriginalPreviewBottomPanel";
            this.leftOriginalPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.leftOriginalPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.leftOriginalPreviewBottomPanel.TabIndex = 0;
            // 
            // leftOriginalPreviewFitButton
            // 
            this.leftOriginalPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftOriginalPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.leftOriginalPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.leftOriginalPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.leftOriginalPreviewFitButton.Name = "leftOriginalPreviewFitButton";
            this.leftOriginalPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.leftOriginalPreviewFitButton.TabIndex = 1;
            this.leftOriginalPreviewFitButton.Text = "重設視圖";
            this.leftOriginalPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // leftOriginalPreviewStatusLabel
            // 
            this.leftOriginalPreviewStatusLabel.AutoSize = true;
            this.leftOriginalPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftOriginalPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.leftOriginalPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.leftOriginalPreviewStatusLabel.Name = "leftOriginalPreviewStatusLabel";
            this.leftOriginalPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.leftOriginalPreviewStatusLabel.TabIndex = 0;
            this.leftOriginalPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // leftProcessedTabPage
            // 
            this.leftProcessedTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftProcessedTabPage.Controls.Add(this.leftProcessedDisplayHostPanel);
            this.leftProcessedTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftProcessedTabPage.Name = "leftProcessedTabPage";
            this.leftProcessedTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftProcessedTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftProcessedTabPage.TabIndex = 1;
            this.leftProcessedTabPage.Text = "處理後";
            // 
            // leftProcessedDisplayHostPanel
            // 
            this.leftProcessedDisplayHostPanel.Controls.Add(this.leftProcessedPreviewViewPanel);
            this.leftProcessedDisplayHostPanel.Controls.Add(this.leftProcessedPreviewBottomPanel);
            this.leftProcessedDisplayHostPanel.Controls.Add(this.leftProcessedPreviewTopPanel);
            this.leftProcessedDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftProcessedDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.leftProcessedDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftProcessedDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.leftProcessedDisplayHostPanel.Name = "leftProcessedDisplayHostPanel";
            this.leftProcessedDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.leftProcessedDisplayHostPanel.TabIndex = 0;
            // 
            // leftProcessedPreviewTopPanel
            // 
            this.leftProcessedPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftProcessedPreviewTopPanel.Controls.Add(this.leftProcessedPreviewResolutionLabel);
            this.leftProcessedPreviewTopPanel.Controls.Add(this.leftProcessedPreviewTitleLabel);
            this.leftProcessedPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.leftProcessedPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.leftProcessedPreviewTopPanel.Name = "leftProcessedPreviewTopPanel";
            this.leftProcessedPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.leftProcessedPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.leftProcessedPreviewTopPanel.TabIndex = 2;
            // 
            // leftProcessedPreviewResolutionLabel
            // 
            this.leftProcessedPreviewResolutionLabel.AutoSize = true;
            this.leftProcessedPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftProcessedPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.leftProcessedPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.leftProcessedPreviewResolutionLabel.Name = "leftProcessedPreviewResolutionLabel";
            this.leftProcessedPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.leftProcessedPreviewResolutionLabel.TabIndex = 1;
            this.leftProcessedPreviewResolutionLabel.Text = "0 x 0";
            // 
            // leftProcessedPreviewTitleLabel
            // 
            this.leftProcessedPreviewTitleLabel.AutoSize = true;
            this.leftProcessedPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftProcessedPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.leftProcessedPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.leftProcessedPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.leftProcessedPreviewTitleLabel.Name = "leftProcessedPreviewTitleLabel";
            this.leftProcessedPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.leftProcessedPreviewTitleLabel.TabIndex = 0;
            this.leftProcessedPreviewTitleLabel.Text = "左側 處理後";
            // 
            // leftProcessedPreviewViewPanel
            // 
            this.leftProcessedPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.leftProcessedPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftProcessedPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftProcessedPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.leftProcessedPreviewViewPanel.Name = "leftProcessedPreviewViewPanel";
            this.leftProcessedPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.leftProcessedPreviewViewPanel.TabIndex = 1;
            // 
            // leftProcessedPreviewBottomPanel
            // 
            this.leftProcessedPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftProcessedPreviewBottomPanel.Controls.Add(this.leftProcessedPreviewFitButton);
            this.leftProcessedPreviewBottomPanel.Controls.Add(this.leftProcessedPreviewStatusLabel);
            this.leftProcessedPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.leftProcessedPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.leftProcessedPreviewBottomPanel.Name = "leftProcessedPreviewBottomPanel";
            this.leftProcessedPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.leftProcessedPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.leftProcessedPreviewBottomPanel.TabIndex = 0;
            // 
            // leftProcessedPreviewFitButton
            // 
            this.leftProcessedPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftProcessedPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.leftProcessedPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.leftProcessedPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.leftProcessedPreviewFitButton.Name = "leftProcessedPreviewFitButton";
            this.leftProcessedPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.leftProcessedPreviewFitButton.TabIndex = 1;
            this.leftProcessedPreviewFitButton.Text = "重設視圖";
            this.leftProcessedPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // leftProcessedPreviewStatusLabel
            // 
            this.leftProcessedPreviewStatusLabel.AutoSize = true;
            this.leftProcessedPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftProcessedPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.leftProcessedPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.leftProcessedPreviewStatusLabel.Name = "leftProcessedPreviewStatusLabel";
            this.leftProcessedPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.leftProcessedPreviewStatusLabel.TabIndex = 0;
            this.leftProcessedPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // leftObjectsTabPage
            // 
            this.leftObjectsTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftObjectsTabPage.Controls.Add(this.leftObjectsDisplayHostPanel);
            this.leftObjectsTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftObjectsTabPage.Name = "leftObjectsTabPage";
            this.leftObjectsTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftObjectsTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftObjectsTabPage.TabIndex = 2;
            this.leftObjectsTabPage.Text = "物件結果";
            // 
            // leftObjectsDisplayHostPanel
            // 
            this.leftObjectsDisplayHostPanel.Controls.Add(this.leftObjectsPreviewViewPanel);
            this.leftObjectsDisplayHostPanel.Controls.Add(this.leftObjectsPreviewBottomPanel);
            this.leftObjectsDisplayHostPanel.Controls.Add(this.leftObjectsPreviewTopPanel);
            this.leftObjectsDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftObjectsDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.leftObjectsDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftObjectsDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.leftObjectsDisplayHostPanel.Name = "leftObjectsDisplayHostPanel";
            this.leftObjectsDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.leftObjectsDisplayHostPanel.TabIndex = 0;
            // 
            // leftObjectsPreviewTopPanel
            // 
            this.leftObjectsPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftObjectsPreviewTopPanel.Controls.Add(this.leftObjectsPreviewResolutionLabel);
            this.leftObjectsPreviewTopPanel.Controls.Add(this.leftObjectsPreviewTitleLabel);
            this.leftObjectsPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.leftObjectsPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.leftObjectsPreviewTopPanel.Name = "leftObjectsPreviewTopPanel";
            this.leftObjectsPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.leftObjectsPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.leftObjectsPreviewTopPanel.TabIndex = 2;
            // 
            // leftObjectsPreviewResolutionLabel
            // 
            this.leftObjectsPreviewResolutionLabel.AutoSize = true;
            this.leftObjectsPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftObjectsPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.leftObjectsPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.leftObjectsPreviewResolutionLabel.Name = "leftObjectsPreviewResolutionLabel";
            this.leftObjectsPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.leftObjectsPreviewResolutionLabel.TabIndex = 1;
            this.leftObjectsPreviewResolutionLabel.Text = "0 x 0";
            // 
            // leftObjectsPreviewTitleLabel
            // 
            this.leftObjectsPreviewTitleLabel.AutoSize = true;
            this.leftObjectsPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftObjectsPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.leftObjectsPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.leftObjectsPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.leftObjectsPreviewTitleLabel.Name = "leftObjectsPreviewTitleLabel";
            this.leftObjectsPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.leftObjectsPreviewTitleLabel.TabIndex = 0;
            this.leftObjectsPreviewTitleLabel.Text = "左側 物件結果";
            // 
            // leftObjectsPreviewViewPanel
            // 
            this.leftObjectsPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.leftObjectsPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftObjectsPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftObjectsPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.leftObjectsPreviewViewPanel.Name = "leftObjectsPreviewViewPanel";
            this.leftObjectsPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.leftObjectsPreviewViewPanel.TabIndex = 1;
            // 
            // leftObjectsPreviewBottomPanel
            // 
            this.leftObjectsPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftObjectsPreviewBottomPanel.Controls.Add(this.leftObjectsPreviewFitButton);
            this.leftObjectsPreviewBottomPanel.Controls.Add(this.leftObjectsPreviewStatusLabel);
            this.leftObjectsPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.leftObjectsPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.leftObjectsPreviewBottomPanel.Name = "leftObjectsPreviewBottomPanel";
            this.leftObjectsPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.leftObjectsPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.leftObjectsPreviewBottomPanel.TabIndex = 0;
            // 
            // leftObjectsPreviewFitButton
            // 
            this.leftObjectsPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftObjectsPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.leftObjectsPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.leftObjectsPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.leftObjectsPreviewFitButton.Name = "leftObjectsPreviewFitButton";
            this.leftObjectsPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.leftObjectsPreviewFitButton.TabIndex = 1;
            this.leftObjectsPreviewFitButton.Text = "重設視圖";
            this.leftObjectsPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // leftObjectsPreviewStatusLabel
            // 
            this.leftObjectsPreviewStatusLabel.AutoSize = true;
            this.leftObjectsPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftObjectsPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.leftObjectsPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.leftObjectsPreviewStatusLabel.Name = "leftObjectsPreviewStatusLabel";
            this.leftObjectsPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.leftObjectsPreviewStatusLabel.TabIndex = 0;
            this.leftObjectsPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // leftDebugTabPage
            // 
            this.leftDebugTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftDebugTabPage.Controls.Add(this.leftDebugDisplayHostPanel);
            this.leftDebugTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftDebugTabPage.Name = "leftDebugTabPage";
            this.leftDebugTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftDebugTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftDebugTabPage.TabIndex = 3;
            this.leftDebugTabPage.Text = "debug";
            // 
            // leftDebugDisplayHostPanel
            // 
            this.leftDebugDisplayHostPanel.Controls.Add(this.leftDebugPreviewViewPanel);
            this.leftDebugDisplayHostPanel.Controls.Add(this.leftDebugPreviewBottomPanel);
            this.leftDebugDisplayHostPanel.Controls.Add(this.leftDebugPreviewTopPanel);
            this.leftDebugDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftDebugDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.leftDebugDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftDebugDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.leftDebugDisplayHostPanel.Name = "leftDebugDisplayHostPanel";
            this.leftDebugDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.leftDebugDisplayHostPanel.TabIndex = 0;
            // 
            // leftDebugPreviewTopPanel
            // 
            this.leftDebugPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftDebugPreviewTopPanel.Controls.Add(this.leftDebugPreviewResolutionLabel);
            this.leftDebugPreviewTopPanel.Controls.Add(this.leftDebugPreviewTitleLabel);
            this.leftDebugPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.leftDebugPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.leftDebugPreviewTopPanel.Name = "leftDebugPreviewTopPanel";
            this.leftDebugPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.leftDebugPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.leftDebugPreviewTopPanel.TabIndex = 2;
            // 
            // leftDebugPreviewResolutionLabel
            // 
            this.leftDebugPreviewResolutionLabel.AutoSize = true;
            this.leftDebugPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftDebugPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.leftDebugPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.leftDebugPreviewResolutionLabel.Name = "leftDebugPreviewResolutionLabel";
            this.leftDebugPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.leftDebugPreviewResolutionLabel.TabIndex = 1;
            this.leftDebugPreviewResolutionLabel.Text = "0 x 0";
            // 
            // leftDebugPreviewTitleLabel
            // 
            this.leftDebugPreviewTitleLabel.AutoSize = true;
            this.leftDebugPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftDebugPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.leftDebugPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.leftDebugPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.leftDebugPreviewTitleLabel.Name = "leftDebugPreviewTitleLabel";
            this.leftDebugPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.leftDebugPreviewTitleLabel.TabIndex = 0;
            this.leftDebugPreviewTitleLabel.Text = "左側 debug";
            // 
            // leftDebugPreviewViewPanel
            // 
            this.leftDebugPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.leftDebugPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftDebugPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftDebugPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.leftDebugPreviewViewPanel.Name = "leftDebugPreviewViewPanel";
            this.leftDebugPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.leftDebugPreviewViewPanel.TabIndex = 1;
            // 
            // leftDebugPreviewBottomPanel
            // 
            this.leftDebugPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.leftDebugPreviewBottomPanel.Controls.Add(this.leftDebugPreviewFitButton);
            this.leftDebugPreviewBottomPanel.Controls.Add(this.leftDebugPreviewStatusLabel);
            this.leftDebugPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.leftDebugPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.leftDebugPreviewBottomPanel.Name = "leftDebugPreviewBottomPanel";
            this.leftDebugPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.leftDebugPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.leftDebugPreviewBottomPanel.TabIndex = 0;
            // 
            // leftDebugPreviewFitButton
            // 
            this.leftDebugPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.leftDebugPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.leftDebugPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.leftDebugPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.leftDebugPreviewFitButton.Name = "leftDebugPreviewFitButton";
            this.leftDebugPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.leftDebugPreviewFitButton.TabIndex = 1;
            this.leftDebugPreviewFitButton.Text = "重設視圖";
            this.leftDebugPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // leftDebugPreviewStatusLabel
            // 
            this.leftDebugPreviewStatusLabel.AutoSize = true;
            this.leftDebugPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftDebugPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.leftDebugPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.leftDebugPreviewStatusLabel.Name = "leftDebugPreviewStatusLabel";
            this.leftDebugPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.leftDebugPreviewStatusLabel.TabIndex = 0;
            this.leftDebugPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // rightImageTabControl
            // 
            this.rightImageTabControl.Controls.Add(this.rightOriginalTabPage);
            this.rightImageTabControl.Controls.Add(this.rightProcessedTabPage);
            this.rightImageTabControl.Controls.Add(this.rightObjectsTabPage);
            this.rightImageTabControl.Controls.Add(this.rightDebugTabPage);
            this.rightImageTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightImageTabControl.Location = new System.Drawing.Point(376, 0);
            this.rightImageTabControl.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.rightImageTabControl.Name = "rightImageTabControl";
            this.rightImageTabControl.SelectedIndex = 0;
            this.rightImageTabControl.Size = new System.Drawing.Size(360, 622);
            this.rightImageTabControl.TabIndex = 1;
            // 
            // rightOriginalTabPage
            // 
            this.rightOriginalTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightOriginalTabPage.Controls.Add(this.rightOriginalDisplayHostPanel);
            this.rightOriginalTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightOriginalTabPage.Name = "rightOriginalTabPage";
            this.rightOriginalTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightOriginalTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightOriginalTabPage.TabIndex = 0;
            this.rightOriginalTabPage.Text = "原圖";
            // 
            // rightOriginalDisplayHostPanel
            // 
            this.rightOriginalDisplayHostPanel.Controls.Add(this.rightOriginalPreviewViewPanel);
            this.rightOriginalDisplayHostPanel.Controls.Add(this.rightOriginalPreviewBottomPanel);
            this.rightOriginalDisplayHostPanel.Controls.Add(this.rightOriginalPreviewTopPanel);
            this.rightOriginalDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightOriginalDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.rightOriginalDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightOriginalDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.rightOriginalDisplayHostPanel.Name = "rightOriginalDisplayHostPanel";
            this.rightOriginalDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.rightOriginalDisplayHostPanel.TabIndex = 0;
            // 
            // rightOriginalPreviewTopPanel
            // 
            this.rightOriginalPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightOriginalPreviewTopPanel.Controls.Add(this.rightOriginalPreviewResolutionLabel);
            this.rightOriginalPreviewTopPanel.Controls.Add(this.rightOriginalPreviewTitleLabel);
            this.rightOriginalPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.rightOriginalPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.rightOriginalPreviewTopPanel.Name = "rightOriginalPreviewTopPanel";
            this.rightOriginalPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.rightOriginalPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.rightOriginalPreviewTopPanel.TabIndex = 2;
            // 
            // rightOriginalPreviewResolutionLabel
            // 
            this.rightOriginalPreviewResolutionLabel.AutoSize = true;
            this.rightOriginalPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightOriginalPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.rightOriginalPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.rightOriginalPreviewResolutionLabel.Name = "rightOriginalPreviewResolutionLabel";
            this.rightOriginalPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.rightOriginalPreviewResolutionLabel.TabIndex = 1;
            this.rightOriginalPreviewResolutionLabel.Text = "0 x 0";
            // 
            // rightOriginalPreviewTitleLabel
            // 
            this.rightOriginalPreviewTitleLabel.AutoSize = true;
            this.rightOriginalPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightOriginalPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.rightOriginalPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.rightOriginalPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.rightOriginalPreviewTitleLabel.Name = "rightOriginalPreviewTitleLabel";
            this.rightOriginalPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.rightOriginalPreviewTitleLabel.TabIndex = 0;
            this.rightOriginalPreviewTitleLabel.Text = "右側 原圖";
            // 
            // rightOriginalPreviewViewPanel
            // 
            this.rightOriginalPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.rightOriginalPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightOriginalPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightOriginalPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.rightOriginalPreviewViewPanel.Name = "rightOriginalPreviewViewPanel";
            this.rightOriginalPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.rightOriginalPreviewViewPanel.TabIndex = 1;
            // 
            // rightOriginalPreviewBottomPanel
            // 
            this.rightOriginalPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightOriginalPreviewBottomPanel.Controls.Add(this.rightOriginalPreviewFitButton);
            this.rightOriginalPreviewBottomPanel.Controls.Add(this.rightOriginalPreviewStatusLabel);
            this.rightOriginalPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.rightOriginalPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.rightOriginalPreviewBottomPanel.Name = "rightOriginalPreviewBottomPanel";
            this.rightOriginalPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.rightOriginalPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.rightOriginalPreviewBottomPanel.TabIndex = 0;
            // 
            // rightOriginalPreviewFitButton
            // 
            this.rightOriginalPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightOriginalPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rightOriginalPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.rightOriginalPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.rightOriginalPreviewFitButton.Name = "rightOriginalPreviewFitButton";
            this.rightOriginalPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.rightOriginalPreviewFitButton.TabIndex = 1;
            this.rightOriginalPreviewFitButton.Text = "重設視圖";
            this.rightOriginalPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // rightOriginalPreviewStatusLabel
            // 
            this.rightOriginalPreviewStatusLabel.AutoSize = true;
            this.rightOriginalPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightOriginalPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.rightOriginalPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.rightOriginalPreviewStatusLabel.Name = "rightOriginalPreviewStatusLabel";
            this.rightOriginalPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.rightOriginalPreviewStatusLabel.TabIndex = 0;
            this.rightOriginalPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // rightProcessedTabPage
            // 
            this.rightProcessedTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightProcessedTabPage.Controls.Add(this.rightProcessedDisplayHostPanel);
            this.rightProcessedTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightProcessedTabPage.Name = "rightProcessedTabPage";
            this.rightProcessedTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightProcessedTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightProcessedTabPage.TabIndex = 1;
            this.rightProcessedTabPage.Text = "處理後";
            // 
            // rightProcessedDisplayHostPanel
            // 
            this.rightProcessedDisplayHostPanel.Controls.Add(this.rightProcessedPreviewViewPanel);
            this.rightProcessedDisplayHostPanel.Controls.Add(this.rightProcessedPreviewBottomPanel);
            this.rightProcessedDisplayHostPanel.Controls.Add(this.rightProcessedPreviewTopPanel);
            this.rightProcessedDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightProcessedDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.rightProcessedDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightProcessedDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.rightProcessedDisplayHostPanel.Name = "rightProcessedDisplayHostPanel";
            this.rightProcessedDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.rightProcessedDisplayHostPanel.TabIndex = 0;
            // 
            // rightProcessedPreviewTopPanel
            // 
            this.rightProcessedPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightProcessedPreviewTopPanel.Controls.Add(this.rightProcessedPreviewResolutionLabel);
            this.rightProcessedPreviewTopPanel.Controls.Add(this.rightProcessedPreviewTitleLabel);
            this.rightProcessedPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.rightProcessedPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.rightProcessedPreviewTopPanel.Name = "rightProcessedPreviewTopPanel";
            this.rightProcessedPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.rightProcessedPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.rightProcessedPreviewTopPanel.TabIndex = 2;
            // 
            // rightProcessedPreviewResolutionLabel
            // 
            this.rightProcessedPreviewResolutionLabel.AutoSize = true;
            this.rightProcessedPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightProcessedPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.rightProcessedPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.rightProcessedPreviewResolutionLabel.Name = "rightProcessedPreviewResolutionLabel";
            this.rightProcessedPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.rightProcessedPreviewResolutionLabel.TabIndex = 1;
            this.rightProcessedPreviewResolutionLabel.Text = "0 x 0";
            // 
            // rightProcessedPreviewTitleLabel
            // 
            this.rightProcessedPreviewTitleLabel.AutoSize = true;
            this.rightProcessedPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightProcessedPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.rightProcessedPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.rightProcessedPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.rightProcessedPreviewTitleLabel.Name = "rightProcessedPreviewTitleLabel";
            this.rightProcessedPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.rightProcessedPreviewTitleLabel.TabIndex = 0;
            this.rightProcessedPreviewTitleLabel.Text = "右側 處理後";
            // 
            // rightProcessedPreviewViewPanel
            // 
            this.rightProcessedPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.rightProcessedPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightProcessedPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightProcessedPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.rightProcessedPreviewViewPanel.Name = "rightProcessedPreviewViewPanel";
            this.rightProcessedPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.rightProcessedPreviewViewPanel.TabIndex = 1;
            // 
            // rightProcessedPreviewBottomPanel
            // 
            this.rightProcessedPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightProcessedPreviewBottomPanel.Controls.Add(this.rightProcessedPreviewFitButton);
            this.rightProcessedPreviewBottomPanel.Controls.Add(this.rightProcessedPreviewStatusLabel);
            this.rightProcessedPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.rightProcessedPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.rightProcessedPreviewBottomPanel.Name = "rightProcessedPreviewBottomPanel";
            this.rightProcessedPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.rightProcessedPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.rightProcessedPreviewBottomPanel.TabIndex = 0;
            // 
            // rightProcessedPreviewFitButton
            // 
            this.rightProcessedPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightProcessedPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rightProcessedPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.rightProcessedPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.rightProcessedPreviewFitButton.Name = "rightProcessedPreviewFitButton";
            this.rightProcessedPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.rightProcessedPreviewFitButton.TabIndex = 1;
            this.rightProcessedPreviewFitButton.Text = "重設視圖";
            this.rightProcessedPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // rightProcessedPreviewStatusLabel
            // 
            this.rightProcessedPreviewStatusLabel.AutoSize = true;
            this.rightProcessedPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightProcessedPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.rightProcessedPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.rightProcessedPreviewStatusLabel.Name = "rightProcessedPreviewStatusLabel";
            this.rightProcessedPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.rightProcessedPreviewStatusLabel.TabIndex = 0;
            this.rightProcessedPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // rightObjectsTabPage
            // 
            this.rightObjectsTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightObjectsTabPage.Controls.Add(this.rightObjectsDisplayHostPanel);
            this.rightObjectsTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightObjectsTabPage.Name = "rightObjectsTabPage";
            this.rightObjectsTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightObjectsTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightObjectsTabPage.TabIndex = 2;
            this.rightObjectsTabPage.Text = "物件結果";
            // 
            // rightObjectsDisplayHostPanel
            // 
            this.rightObjectsDisplayHostPanel.Controls.Add(this.rightObjectsPreviewViewPanel);
            this.rightObjectsDisplayHostPanel.Controls.Add(this.rightObjectsPreviewBottomPanel);
            this.rightObjectsDisplayHostPanel.Controls.Add(this.rightObjectsPreviewTopPanel);
            this.rightObjectsDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightObjectsDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.rightObjectsDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightObjectsDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.rightObjectsDisplayHostPanel.Name = "rightObjectsDisplayHostPanel";
            this.rightObjectsDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.rightObjectsDisplayHostPanel.TabIndex = 0;
            // 
            // rightObjectsPreviewTopPanel
            // 
            this.rightObjectsPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightObjectsPreviewTopPanel.Controls.Add(this.rightObjectsPreviewResolutionLabel);
            this.rightObjectsPreviewTopPanel.Controls.Add(this.rightObjectsPreviewTitleLabel);
            this.rightObjectsPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.rightObjectsPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.rightObjectsPreviewTopPanel.Name = "rightObjectsPreviewTopPanel";
            this.rightObjectsPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.rightObjectsPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.rightObjectsPreviewTopPanel.TabIndex = 2;
            // 
            // rightObjectsPreviewResolutionLabel
            // 
            this.rightObjectsPreviewResolutionLabel.AutoSize = true;
            this.rightObjectsPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightObjectsPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.rightObjectsPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.rightObjectsPreviewResolutionLabel.Name = "rightObjectsPreviewResolutionLabel";
            this.rightObjectsPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.rightObjectsPreviewResolutionLabel.TabIndex = 1;
            this.rightObjectsPreviewResolutionLabel.Text = "0 x 0";
            // 
            // rightObjectsPreviewTitleLabel
            // 
            this.rightObjectsPreviewTitleLabel.AutoSize = true;
            this.rightObjectsPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightObjectsPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.rightObjectsPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.rightObjectsPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.rightObjectsPreviewTitleLabel.Name = "rightObjectsPreviewTitleLabel";
            this.rightObjectsPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.rightObjectsPreviewTitleLabel.TabIndex = 0;
            this.rightObjectsPreviewTitleLabel.Text = "右側 物件結果";
            // 
            // rightObjectsPreviewViewPanel
            // 
            this.rightObjectsPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.rightObjectsPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightObjectsPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightObjectsPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.rightObjectsPreviewViewPanel.Name = "rightObjectsPreviewViewPanel";
            this.rightObjectsPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.rightObjectsPreviewViewPanel.TabIndex = 1;
            // 
            // rightObjectsPreviewBottomPanel
            // 
            this.rightObjectsPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightObjectsPreviewBottomPanel.Controls.Add(this.rightObjectsPreviewFitButton);
            this.rightObjectsPreviewBottomPanel.Controls.Add(this.rightObjectsPreviewStatusLabel);
            this.rightObjectsPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.rightObjectsPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.rightObjectsPreviewBottomPanel.Name = "rightObjectsPreviewBottomPanel";
            this.rightObjectsPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.rightObjectsPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.rightObjectsPreviewBottomPanel.TabIndex = 0;
            // 
            // rightObjectsPreviewFitButton
            // 
            this.rightObjectsPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightObjectsPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rightObjectsPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.rightObjectsPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.rightObjectsPreviewFitButton.Name = "rightObjectsPreviewFitButton";
            this.rightObjectsPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.rightObjectsPreviewFitButton.TabIndex = 1;
            this.rightObjectsPreviewFitButton.Text = "重設視圖";
            this.rightObjectsPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // rightObjectsPreviewStatusLabel
            // 
            this.rightObjectsPreviewStatusLabel.AutoSize = true;
            this.rightObjectsPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightObjectsPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.rightObjectsPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.rightObjectsPreviewStatusLabel.Name = "rightObjectsPreviewStatusLabel";
            this.rightObjectsPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.rightObjectsPreviewStatusLabel.TabIndex = 0;
            this.rightObjectsPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // rightDebugTabPage
            // 
            this.rightDebugTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightDebugTabPage.Controls.Add(this.rightDebugDisplayHostPanel);
            this.rightDebugTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightDebugTabPage.Name = "rightDebugTabPage";
            this.rightDebugTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightDebugTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightDebugTabPage.TabIndex = 3;
            this.rightDebugTabPage.Text = "debug";
            // 
            // rightDebugDisplayHostPanel
            // 
            this.rightDebugDisplayHostPanel.Controls.Add(this.rightDebugPreviewViewPanel);
            this.rightDebugDisplayHostPanel.Controls.Add(this.rightDebugPreviewBottomPanel);
            this.rightDebugDisplayHostPanel.Controls.Add(this.rightDebugPreviewTopPanel);
            this.rightDebugDisplayHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightDebugDisplayHostPanel.BackColor = System.Drawing.Color.White;
            this.rightDebugDisplayHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightDebugDisplayHostPanel.Location = new System.Drawing.Point(12, 12);
            this.rightDebugDisplayHostPanel.Name = "rightDebugDisplayHostPanel";
            this.rightDebugDisplayHostPanel.Size = new System.Drawing.Size(328, 570);
            this.rightDebugDisplayHostPanel.TabIndex = 0;
            // 
            // rightDebugPreviewTopPanel
            // 
            this.rightDebugPreviewTopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightDebugPreviewTopPanel.Controls.Add(this.rightDebugPreviewResolutionLabel);
            this.rightDebugPreviewTopPanel.Controls.Add(this.rightDebugPreviewTitleLabel);
            this.rightDebugPreviewTopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.rightDebugPreviewTopPanel.Location = new System.Drawing.Point(0, 0);
            this.rightDebugPreviewTopPanel.Name = "rightDebugPreviewTopPanel";
            this.rightDebugPreviewTopPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.rightDebugPreviewTopPanel.Size = new System.Drawing.Size(328, 42);
            this.rightDebugPreviewTopPanel.TabIndex = 2;
            // 
            // rightDebugPreviewResolutionLabel
            // 
            this.rightDebugPreviewResolutionLabel.AutoSize = true;
            this.rightDebugPreviewResolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightDebugPreviewResolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.rightDebugPreviewResolutionLabel.Location = new System.Drawing.Point(287, 8);
            this.rightDebugPreviewResolutionLabel.Name = "rightDebugPreviewResolutionLabel";
            this.rightDebugPreviewResolutionLabel.Size = new System.Drawing.Size(31, 15);
            this.rightDebugPreviewResolutionLabel.TabIndex = 1;
            this.rightDebugPreviewResolutionLabel.Text = "0 x 0";
            // 
            // rightDebugPreviewTitleLabel
            // 
            this.rightDebugPreviewTitleLabel.AutoSize = true;
            this.rightDebugPreviewTitleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightDebugPreviewTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.rightDebugPreviewTitleLabel.ForeColor = System.Drawing.Color.Black;
            this.rightDebugPreviewTitleLabel.Location = new System.Drawing.Point(10, 8);
            this.rightDebugPreviewTitleLabel.Name = "rightDebugPreviewTitleLabel";
            this.rightDebugPreviewTitleLabel.Size = new System.Drawing.Size(56, 15);
            this.rightDebugPreviewTitleLabel.TabIndex = 0;
            this.rightDebugPreviewTitleLabel.Text = "右側 debug";
            // 
            // rightDebugPreviewViewPanel
            // 
            this.rightDebugPreviewViewPanel.BackColor = System.Drawing.Color.White;
            this.rightDebugPreviewViewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightDebugPreviewViewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightDebugPreviewViewPanel.Location = new System.Drawing.Point(0, 42);
            this.rightDebugPreviewViewPanel.Name = "rightDebugPreviewViewPanel";
            this.rightDebugPreviewViewPanel.Size = new System.Drawing.Size(328, 486);
            this.rightDebugPreviewViewPanel.TabIndex = 1;
            // 
            // rightDebugPreviewBottomPanel
            // 
            this.rightDebugPreviewBottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.rightDebugPreviewBottomPanel.Controls.Add(this.rightDebugPreviewFitButton);
            this.rightDebugPreviewBottomPanel.Controls.Add(this.rightDebugPreviewStatusLabel);
            this.rightDebugPreviewBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.rightDebugPreviewBottomPanel.Location = new System.Drawing.Point(0, 528);
            this.rightDebugPreviewBottomPanel.Name = "rightDebugPreviewBottomPanel";
            this.rightDebugPreviewBottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.rightDebugPreviewBottomPanel.Size = new System.Drawing.Size(328, 42);
            this.rightDebugPreviewBottomPanel.TabIndex = 0;
            // 
            // rightDebugPreviewFitButton
            // 
            this.rightDebugPreviewFitButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightDebugPreviewFitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rightDebugPreviewFitButton.ForeColor = System.Drawing.Color.Black;
            this.rightDebugPreviewFitButton.Location = new System.Drawing.Point(228, 7);
            this.rightDebugPreviewFitButton.Name = "rightDebugPreviewFitButton";
            this.rightDebugPreviewFitButton.Size = new System.Drawing.Size(90, 28);
            this.rightDebugPreviewFitButton.TabIndex = 1;
            this.rightDebugPreviewFitButton.Text = "重設視圖";
            this.rightDebugPreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // rightDebugPreviewStatusLabel
            // 
            this.rightDebugPreviewStatusLabel.AutoSize = true;
            this.rightDebugPreviewStatusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.rightDebugPreviewStatusLabel.ForeColor = System.Drawing.Color.Black;
            this.rightDebugPreviewStatusLabel.Location = new System.Drawing.Point(10, 7);
            this.rightDebugPreviewStatusLabel.Name = "rightDebugPreviewStatusLabel";
            this.rightDebugPreviewStatusLabel.Size = new System.Drawing.Size(79, 15);
            this.rightDebugPreviewStatusLabel.TabIndex = 0;
            this.rightDebugPreviewStatusLabel.Text = "尚未載入圖片";            // 
            // rightPanel
            // 
            this.rightPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.rightPanel.Controls.Add(this.parameterPanel);
            this.rightPanel.Controls.Add(this.rightPanelTitleLabel);
            this.rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightPanel.Location = new System.Drawing.Point(1024, 0);
            this.rightPanel.Margin = new System.Windows.Forms.Padding(0);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Padding = new System.Windows.Forms.Padding(14, 12, 14, 12);
            this.rightPanel.Size = new System.Drawing.Size(256, 650);
            this.rightPanel.TabIndex = 2;
            // 
            // parameterPanel
            // 
            this.parameterPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.parameterPanel.Controls.Add(this.parameterPlaceholderLabel);
            this.parameterPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.parameterPanel.Location = new System.Drawing.Point(14, 46);
            this.parameterPanel.Name = "parameterPanel";
            this.parameterPanel.Size = new System.Drawing.Size(228, 592);
            this.parameterPanel.TabIndex = 1;
            // 
            // parameterPlaceholderLabel
            // 
            this.parameterPlaceholderLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.parameterPlaceholderLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(92)))), ((int)(((byte)(101)))), ((int)(((byte)(116)))));
            this.parameterPlaceholderLabel.Location = new System.Drawing.Point(0, 0);
            this.parameterPlaceholderLabel.Name = "parameterPlaceholderLabel";
            this.parameterPlaceholderLabel.Padding = new System.Windows.Forms.Padding(0, 12, 0, 0);
            this.parameterPlaceholderLabel.Size = new System.Drawing.Size(228, 88);
            this.parameterPlaceholderLabel.TabIndex = 0;
            this.parameterPlaceholderLabel.Text = "這裡會顯示目前功能的參數設定與選項。";
            // 
            // rightPanelTitleLabel
            // 
            this.rightPanelTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.rightPanelTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.rightPanelTitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(39)))), ((int)(((byte)(46)))));
            this.rightPanelTitleLabel.Location = new System.Drawing.Point(14, 12);
            this.rightPanelTitleLabel.Name = "rightPanelTitleLabel";
            this.rightPanelTitleLabel.Size = new System.Drawing.Size(228, 34);
            this.rightPanelTitleLabel.TabIndex = 0;
            this.rightPanelTitleLabel.Text = "參數設定";
            this.rightPanelTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // statusStrip
            // 
            this.statusStrip.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(247)))), ((int)(((byte)(249)))), ((int)(((byte)(252)))));
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 698);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1280, 22);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "statusStrip";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(67, 17);
            this.statusLabel.Text = "框架準備中";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1280, 720);
            this.Controls.Add(this.mainLayoutPanel);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.topBarPanel);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.MinimumSize = new System.Drawing.Size(1024, 640);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "整合式影像處理軟件";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.topBarPanel.ResumeLayout(false);
            this.topBarPanel.PerformLayout();
            this.mainLayoutPanel.ResumeLayout(false);
            this.leftPanel.ResumeLayout(false);
            this.centerPanel.ResumeLayout(false);
            this.imageLayoutPanel.ResumeLayout(false);
            this.leftImageTabControl.ResumeLayout(false);
            this.leftOriginalTabPage.ResumeLayout(false);
            this.leftProcessedTabPage.ResumeLayout(false);
            this.leftObjectsTabPage.ResumeLayout(false);
            this.leftDebugTabPage.ResumeLayout(false);
            this.rightImageTabControl.ResumeLayout(false);
            this.rightOriginalTabPage.ResumeLayout(false);
            this.rightProcessedTabPage.ResumeLayout(false);
            this.rightObjectsTabPage.ResumeLayout(false);
            this.rightDebugTabPage.ResumeLayout(false);
            this.leftOriginalPreviewTopPanel.ResumeLayout(false);
            this.leftOriginalPreviewTopPanel.PerformLayout();
            this.leftOriginalPreviewBottomPanel.ResumeLayout(false);
            this.leftOriginalPreviewBottomPanel.PerformLayout();
            this.leftOriginalDisplayHostPanel.ResumeLayout(false);
            this.leftProcessedPreviewTopPanel.ResumeLayout(false);
            this.leftProcessedPreviewTopPanel.PerformLayout();
            this.leftProcessedPreviewBottomPanel.ResumeLayout(false);
            this.leftProcessedPreviewBottomPanel.PerformLayout();
            this.leftProcessedDisplayHostPanel.ResumeLayout(false);
            this.leftObjectsPreviewTopPanel.ResumeLayout(false);
            this.leftObjectsPreviewTopPanel.PerformLayout();
            this.leftObjectsPreviewBottomPanel.ResumeLayout(false);
            this.leftObjectsPreviewBottomPanel.PerformLayout();
            this.leftObjectsDisplayHostPanel.ResumeLayout(false);
            this.leftDebugPreviewTopPanel.ResumeLayout(false);
            this.leftDebugPreviewTopPanel.PerformLayout();
            this.leftDebugPreviewBottomPanel.ResumeLayout(false);
            this.leftDebugPreviewBottomPanel.PerformLayout();
            this.leftDebugDisplayHostPanel.ResumeLayout(false);
            this.rightOriginalPreviewTopPanel.ResumeLayout(false);
            this.rightOriginalPreviewTopPanel.PerformLayout();
            this.rightOriginalPreviewBottomPanel.ResumeLayout(false);
            this.rightOriginalPreviewBottomPanel.PerformLayout();
            this.rightOriginalDisplayHostPanel.ResumeLayout(false);
            this.rightProcessedPreviewTopPanel.ResumeLayout(false);
            this.rightProcessedPreviewTopPanel.PerformLayout();
            this.rightProcessedPreviewBottomPanel.ResumeLayout(false);
            this.rightProcessedPreviewBottomPanel.PerformLayout();
            this.rightProcessedDisplayHostPanel.ResumeLayout(false);
            this.rightObjectsPreviewTopPanel.ResumeLayout(false);
            this.rightObjectsPreviewTopPanel.PerformLayout();
            this.rightObjectsPreviewBottomPanel.ResumeLayout(false);
            this.rightObjectsPreviewBottomPanel.PerformLayout();
            this.rightObjectsDisplayHostPanel.ResumeLayout(false);
            this.rightDebugPreviewTopPanel.ResumeLayout(false);
            this.rightDebugPreviewTopPanel.PerformLayout();
            this.rightDebugPreviewBottomPanel.ResumeLayout(false);
            this.rightDebugPreviewBottomPanel.PerformLayout();
            this.rightDebugDisplayHostPanel.ResumeLayout(false);
            this.rightPanel.ResumeLayout(false);
            this.parameterPanel.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}







