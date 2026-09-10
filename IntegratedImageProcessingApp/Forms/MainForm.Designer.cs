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
        private System.Windows.Forms.Panel sourceImagePanel;
        private System.Windows.Forms.Panel resultImagePanel;
        private System.Windows.Forms.Label sourceImageTitleLabel;
        private System.Windows.Forms.Label resultImageTitleLabel;
        private System.Windows.Forms.PictureBox sourcePictureBox;
        private System.Windows.Forms.PictureBox resultPictureBox;
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
            this.sourceImagePanel = new System.Windows.Forms.Panel();
            this.sourcePictureBox = new System.Windows.Forms.PictureBox();
            this.sourceImageTitleLabel = new System.Windows.Forms.Label();
            this.resultImagePanel = new System.Windows.Forms.Panel();
            this.resultPictureBox = new System.Windows.Forms.PictureBox();
            this.resultImageTitleLabel = new System.Windows.Forms.Label();
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
            this.sourceImagePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sourcePictureBox)).BeginInit();
            this.resultImagePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.resultPictureBox)).BeginInit();
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
            this.imageLayoutPanel.Controls.Add(this.sourceImagePanel, 0, 0);
            this.imageLayoutPanel.Controls.Add(this.resultImagePanel, 1, 0);
            this.imageLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageLayoutPanel.Location = new System.Drawing.Point(16, 14);
            this.imageLayoutPanel.Name = "imageLayoutPanel";
            this.imageLayoutPanel.RowCount = 1;
            this.imageLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.imageLayoutPanel.Size = new System.Drawing.Size(736, 622);
            this.imageLayoutPanel.TabIndex = 0;
            // 
            // sourceImagePanel
            // 
            this.sourceImagePanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.sourceImagePanel.Controls.Add(this.sourcePictureBox);
            this.sourceImagePanel.Controls.Add(this.sourceImageTitleLabel);
            this.sourceImagePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sourceImagePanel.Location = new System.Drawing.Point(0, 0);
            this.sourceImagePanel.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.sourceImagePanel.Name = "sourceImagePanel";
            this.sourceImagePanel.Padding = new System.Windows.Forms.Padding(12);
            this.sourceImagePanel.Size = new System.Drawing.Size(360, 622);
            this.sourceImagePanel.TabIndex = 0;
            // 
            // sourcePictureBox
            // 
            this.sourcePictureBox.BackColor = System.Drawing.Color.White;
            this.sourcePictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.sourcePictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sourcePictureBox.Location = new System.Drawing.Point(12, 46);
            this.sourcePictureBox.Name = "sourcePictureBox";
            this.sourcePictureBox.Size = new System.Drawing.Size(336, 564);
            this.sourcePictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.sourcePictureBox.TabIndex = 1;
            this.sourcePictureBox.TabStop = false;
            // 
            // sourceImageTitleLabel
            // 
            this.sourceImageTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.sourceImageTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.sourceImageTitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(39)))), ((int)(((byte)(46)))));
            this.sourceImageTitleLabel.Location = new System.Drawing.Point(12, 12);
            this.sourceImageTitleLabel.Name = "sourceImageTitleLabel";
            this.sourceImageTitleLabel.Size = new System.Drawing.Size(336, 34);
            this.sourceImageTitleLabel.TabIndex = 0;
            this.sourceImageTitleLabel.Text = "原始圖片";
            this.sourceImageTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // resultImagePanel
            // 
            this.resultImagePanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.resultImagePanel.Controls.Add(this.resultPictureBox);
            this.resultImagePanel.Controls.Add(this.resultImageTitleLabel);
            this.resultImagePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultImagePanel.Location = new System.Drawing.Point(376, 0);
            this.resultImagePanel.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.resultImagePanel.Name = "resultImagePanel";
            this.resultImagePanel.Padding = new System.Windows.Forms.Padding(12);
            this.resultImagePanel.Size = new System.Drawing.Size(360, 622);
            this.resultImagePanel.TabIndex = 1;
            // 
            // resultPictureBox
            // 
            this.resultPictureBox.BackColor = System.Drawing.Color.White;
            this.resultPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.resultPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultPictureBox.Location = new System.Drawing.Point(12, 46);
            this.resultPictureBox.Name = "resultPictureBox";
            this.resultPictureBox.Size = new System.Drawing.Size(336, 564);
            this.resultPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.resultPictureBox.TabIndex = 1;
            this.resultPictureBox.TabStop = false;
            // 
            // resultImageTitleLabel
            // 
            this.resultImageTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.resultImageTitleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.resultImageTitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(39)))), ((int)(((byte)(46)))));
            this.resultImageTitleLabel.Location = new System.Drawing.Point(12, 12);
            this.resultImageTitleLabel.Name = "resultImageTitleLabel";
            this.resultImageTitleLabel.Size = new System.Drawing.Size(336, 34);
            this.resultImageTitleLabel.TabIndex = 0;
            this.resultImageTitleLabel.Text = "影像處理結果";
            this.resultImageTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
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
            this.sourceImagePanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sourcePictureBox)).EndInit();
            this.resultImagePanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.resultPictureBox)).EndInit();
            this.rightPanel.ResumeLayout(false);
            this.parameterPanel.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
