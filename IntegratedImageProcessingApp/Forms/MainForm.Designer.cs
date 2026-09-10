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
        private System.Windows.Forms.PictureBox leftOriginalPictureBox;
        private System.Windows.Forms.PictureBox leftProcessedPictureBox;
        private System.Windows.Forms.PictureBox leftObjectsPictureBox;
        private System.Windows.Forms.PictureBox leftDebugPictureBox;
        private System.Windows.Forms.PictureBox rightOriginalPictureBox;
        private System.Windows.Forms.PictureBox rightProcessedPictureBox;
        private System.Windows.Forms.PictureBox rightObjectsPictureBox;
        private System.Windows.Forms.PictureBox rightDebugPictureBox;
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
            this.leftOriginalPictureBox = new System.Windows.Forms.PictureBox();
            this.leftProcessedTabPage = new System.Windows.Forms.TabPage();
            this.leftProcessedPictureBox = new System.Windows.Forms.PictureBox();
            this.leftObjectsTabPage = new System.Windows.Forms.TabPage();
            this.leftObjectsPictureBox = new System.Windows.Forms.PictureBox();
            this.leftDebugTabPage = new System.Windows.Forms.TabPage();
            this.leftDebugPictureBox = new System.Windows.Forms.PictureBox();
            this.rightImageTabControl = new System.Windows.Forms.TabControl();
            this.rightOriginalTabPage = new System.Windows.Forms.TabPage();
            this.rightOriginalPictureBox = new System.Windows.Forms.PictureBox();
            this.rightProcessedTabPage = new System.Windows.Forms.TabPage();
            this.rightProcessedPictureBox = new System.Windows.Forms.PictureBox();
            this.rightObjectsTabPage = new System.Windows.Forms.TabPage();
            this.rightObjectsPictureBox = new System.Windows.Forms.PictureBox();
            this.rightDebugTabPage = new System.Windows.Forms.TabPage();
            this.rightDebugPictureBox = new System.Windows.Forms.PictureBox();
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
            ((System.ComponentModel.ISupportInitialize)(this.leftOriginalPictureBox)).BeginInit();
            this.leftProcessedTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.leftProcessedPictureBox)).BeginInit();
            this.leftObjectsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.leftObjectsPictureBox)).BeginInit();
            this.leftDebugTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.leftDebugPictureBox)).BeginInit();
            this.rightImageTabControl.SuspendLayout();
            this.rightOriginalTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rightOriginalPictureBox)).BeginInit();
            this.rightProcessedTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rightProcessedPictureBox)).BeginInit();
            this.rightObjectsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rightObjectsPictureBox)).BeginInit();
            this.rightDebugTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rightDebugPictureBox)).BeginInit();
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
            this.leftOriginalTabPage.Controls.Add(this.leftOriginalPictureBox);
            this.leftOriginalTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftOriginalTabPage.Name = "leftOriginalTabPage";
            this.leftOriginalTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftOriginalTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftOriginalTabPage.TabIndex = 0;
            this.leftOriginalTabPage.Text = "原圖";
            // 
            // leftOriginalPictureBox
            // 
            this.leftOriginalPictureBox.BackColor = System.Drawing.Color.White;
            this.leftOriginalPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftOriginalPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftOriginalPictureBox.Location = new System.Drawing.Point(12, 12);
            this.leftOriginalPictureBox.Name = "leftOriginalPictureBox";
            this.leftOriginalPictureBox.Size = new System.Drawing.Size(328, 570);
            this.leftOriginalPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.leftOriginalPictureBox.TabIndex = 0;
            this.leftOriginalPictureBox.TabStop = false;
            // 
            // leftProcessedTabPage
            // 
            this.leftProcessedTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftProcessedTabPage.Controls.Add(this.leftProcessedPictureBox);
            this.leftProcessedTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftProcessedTabPage.Name = "leftProcessedTabPage";
            this.leftProcessedTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftProcessedTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftProcessedTabPage.TabIndex = 1;
            this.leftProcessedTabPage.Text = "處理後";
            // 
            // leftProcessedPictureBox
            // 
            this.leftProcessedPictureBox.BackColor = System.Drawing.Color.White;
            this.leftProcessedPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftProcessedPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftProcessedPictureBox.Location = new System.Drawing.Point(12, 12);
            this.leftProcessedPictureBox.Name = "leftProcessedPictureBox";
            this.leftProcessedPictureBox.Size = new System.Drawing.Size(328, 570);
            this.leftProcessedPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.leftProcessedPictureBox.TabIndex = 0;
            this.leftProcessedPictureBox.TabStop = false;
            // 
            // leftObjectsTabPage
            // 
            this.leftObjectsTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftObjectsTabPage.Controls.Add(this.leftObjectsPictureBox);
            this.leftObjectsTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftObjectsTabPage.Name = "leftObjectsTabPage";
            this.leftObjectsTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftObjectsTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftObjectsTabPage.TabIndex = 2;
            this.leftObjectsTabPage.Text = "物件結果";
            // 
            // leftObjectsPictureBox
            // 
            this.leftObjectsPictureBox.BackColor = System.Drawing.Color.White;
            this.leftObjectsPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftObjectsPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftObjectsPictureBox.Location = new System.Drawing.Point(12, 12);
            this.leftObjectsPictureBox.Name = "leftObjectsPictureBox";
            this.leftObjectsPictureBox.Size = new System.Drawing.Size(328, 570);
            this.leftObjectsPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.leftObjectsPictureBox.TabIndex = 0;
            this.leftObjectsPictureBox.TabStop = false;
            // 
            // leftDebugTabPage
            // 
            this.leftDebugTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.leftDebugTabPage.Controls.Add(this.leftDebugPictureBox);
            this.leftDebugTabPage.Location = new System.Drawing.Point(4, 24);
            this.leftDebugTabPage.Name = "leftDebugTabPage";
            this.leftDebugTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.leftDebugTabPage.Size = new System.Drawing.Size(352, 594);
            this.leftDebugTabPage.TabIndex = 3;
            this.leftDebugTabPage.Text = "debug";
            // 
            // leftDebugPictureBox
            // 
            this.leftDebugPictureBox.BackColor = System.Drawing.Color.White;
            this.leftDebugPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftDebugPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftDebugPictureBox.Location = new System.Drawing.Point(12, 12);
            this.leftDebugPictureBox.Name = "leftDebugPictureBox";
            this.leftDebugPictureBox.Size = new System.Drawing.Size(328, 570);
            this.leftDebugPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.leftDebugPictureBox.TabIndex = 0;
            this.leftDebugPictureBox.TabStop = false;
            // 
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
            this.rightOriginalTabPage.Controls.Add(this.rightOriginalPictureBox);
            this.rightOriginalTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightOriginalTabPage.Name = "rightOriginalTabPage";
            this.rightOriginalTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightOriginalTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightOriginalTabPage.TabIndex = 0;
            this.rightOriginalTabPage.Text = "原圖";
            // 
            // rightOriginalPictureBox
            // 
            this.rightOriginalPictureBox.BackColor = System.Drawing.Color.White;
            this.rightOriginalPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightOriginalPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightOriginalPictureBox.Location = new System.Drawing.Point(12, 12);
            this.rightOriginalPictureBox.Name = "rightOriginalPictureBox";
            this.rightOriginalPictureBox.Size = new System.Drawing.Size(328, 570);
            this.rightOriginalPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.rightOriginalPictureBox.TabIndex = 0;
            this.rightOriginalPictureBox.TabStop = false;
            // 
            // rightProcessedTabPage
            // 
            this.rightProcessedTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightProcessedTabPage.Controls.Add(this.rightProcessedPictureBox);
            this.rightProcessedTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightProcessedTabPage.Name = "rightProcessedTabPage";
            this.rightProcessedTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightProcessedTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightProcessedTabPage.TabIndex = 1;
            this.rightProcessedTabPage.Text = "處理後";
            // 
            // rightProcessedPictureBox
            // 
            this.rightProcessedPictureBox.BackColor = System.Drawing.Color.White;
            this.rightProcessedPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightProcessedPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightProcessedPictureBox.Location = new System.Drawing.Point(12, 12);
            this.rightProcessedPictureBox.Name = "rightProcessedPictureBox";
            this.rightProcessedPictureBox.Size = new System.Drawing.Size(328, 570);
            this.rightProcessedPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.rightProcessedPictureBox.TabIndex = 0;
            this.rightProcessedPictureBox.TabStop = false;
            // 
            // rightObjectsTabPage
            // 
            this.rightObjectsTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightObjectsTabPage.Controls.Add(this.rightObjectsPictureBox);
            this.rightObjectsTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightObjectsTabPage.Name = "rightObjectsTabPage";
            this.rightObjectsTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightObjectsTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightObjectsTabPage.TabIndex = 2;
            this.rightObjectsTabPage.Text = "物件結果";
            // 
            // rightObjectsPictureBox
            // 
            this.rightObjectsPictureBox.BackColor = System.Drawing.Color.White;
            this.rightObjectsPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightObjectsPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightObjectsPictureBox.Location = new System.Drawing.Point(12, 12);
            this.rightObjectsPictureBox.Name = "rightObjectsPictureBox";
            this.rightObjectsPictureBox.Size = new System.Drawing.Size(328, 570);
            this.rightObjectsPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.rightObjectsPictureBox.TabIndex = 0;
            this.rightObjectsPictureBox.TabStop = false;
            // 
            // rightDebugTabPage
            // 
            this.rightDebugTabPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.rightDebugTabPage.Controls.Add(this.rightDebugPictureBox);
            this.rightDebugTabPage.Location = new System.Drawing.Point(4, 24);
            this.rightDebugTabPage.Name = "rightDebugTabPage";
            this.rightDebugTabPage.Padding = new System.Windows.Forms.Padding(12);
            this.rightDebugTabPage.Size = new System.Drawing.Size(352, 594);
            this.rightDebugTabPage.TabIndex = 3;
            this.rightDebugTabPage.Text = "debug";
            // 
            // rightDebugPictureBox
            // 
            this.rightDebugPictureBox.BackColor = System.Drawing.Color.White;
            this.rightDebugPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightDebugPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightDebugPictureBox.Location = new System.Drawing.Point(12, 12);
            this.rightDebugPictureBox.Name = "rightDebugPictureBox";
            this.rightDebugPictureBox.Size = new System.Drawing.Size(328, 570);
            this.rightDebugPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.rightDebugPictureBox.TabIndex = 0;
            this.rightDebugPictureBox.TabStop = false;
            // 
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
            ((System.ComponentModel.ISupportInitialize)(this.leftOriginalPictureBox)).EndInit();
            this.leftProcessedTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.leftProcessedPictureBox)).EndInit();
            this.leftObjectsTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.leftObjectsPictureBox)).EndInit();
            this.leftDebugTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.leftDebugPictureBox)).EndInit();
            this.rightImageTabControl.ResumeLayout(false);
            this.rightOriginalTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rightOriginalPictureBox)).EndInit();
            this.rightProcessedTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rightProcessedPictureBox)).EndInit();
            this.rightObjectsTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rightObjectsPictureBox)).EndInit();
            this.rightDebugTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rightDebugPictureBox)).EndInit();
            this.rightPanel.ResumeLayout(false);
            this.parameterPanel.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
