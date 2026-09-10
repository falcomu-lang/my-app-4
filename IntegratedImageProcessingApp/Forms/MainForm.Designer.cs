namespace IntegratedImageProcessingApp.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label subtitleLabel;
        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.GroupBox toolGroupBox;
        private System.Windows.Forms.Button openImageButton;
        private System.Windows.Forms.Button saveResultButton;
        private System.Windows.Forms.PictureBox imagePreviewBox;
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
            this.headerPanel = new System.Windows.Forms.Panel();
            this.subtitleLabel = new System.Windows.Forms.Label();
            this.titleLabel = new System.Windows.Forms.Label();
            this.mainSplitContainer = new System.Windows.Forms.SplitContainer();
            this.toolGroupBox = new System.Windows.Forms.GroupBox();
            this.saveResultButton = new System.Windows.Forms.Button();
            this.openImageButton = new System.Windows.Forms.Button();
            this.imagePreviewBox = new System.Windows.Forms.PictureBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).BeginInit();
            this.mainSplitContainer.Panel1.SuspendLayout();
            this.mainSplitContainer.Panel2.SuspendLayout();
            this.mainSplitContainer.SuspendLayout();
            this.toolGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imagePreviewBox)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // headerPanel
            // 
            this.headerPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(45)))), ((int)(((byte)(56)))));
            this.headerPanel.Controls.Add(this.subtitleLabel);
            this.headerPanel.Controls.Add(this.titleLabel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new System.Drawing.Size(984, 82);
            this.headerPanel.TabIndex = 0;
            // 
            // subtitleLabel
            // 
            this.subtitleLabel.AutoSize = true;
            this.subtitleLabel.ForeColor = System.Drawing.Color.Gainsboro;
            this.subtitleLabel.Location = new System.Drawing.Point(24, 49);
            this.subtitleLabel.Name = "subtitleLabel";
            this.subtitleLabel.Size = new System.Drawing.Size(317, 15);
            this.subtitleLabel.TabIndex = 1;
            this.subtitleLabel.Text = "C# .NET Framework 4.7.2 Windows Forms 專案骨架";
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.titleLabel.ForeColor = System.Drawing.Color.White;
            this.titleLabel.Location = new System.Drawing.Point(21, 17);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(232, 28);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "整合式影像處理軟件";
            // 
            // mainSplitContainer
            // 
            this.mainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.mainSplitContainer.Location = new System.Drawing.Point(0, 82);
            this.mainSplitContainer.Name = "mainSplitContainer";
            // 
            // mainSplitContainer.Panel1
            // 
            this.mainSplitContainer.Panel1.Controls.Add(this.toolGroupBox);
            this.mainSplitContainer.Panel1MinSize = 220;
            // 
            // mainSplitContainer.Panel2
            // 
            this.mainSplitContainer.Panel2.Controls.Add(this.imagePreviewBox);
            this.mainSplitContainer.Size = new System.Drawing.Size(984, 557);
            this.mainSplitContainer.SplitterDistance = 260;
            this.mainSplitContainer.TabIndex = 1;
            // 
            // toolGroupBox
            // 
            this.toolGroupBox.Controls.Add(this.saveResultButton);
            this.toolGroupBox.Controls.Add(this.openImageButton);
            this.toolGroupBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolGroupBox.Location = new System.Drawing.Point(0, 0);
            this.toolGroupBox.Name = "toolGroupBox";
            this.toolGroupBox.Padding = new System.Windows.Forms.Padding(16);
            this.toolGroupBox.Size = new System.Drawing.Size(260, 142);
            this.toolGroupBox.TabIndex = 0;
            this.toolGroupBox.TabStop = false;
            this.toolGroupBox.Text = "基本工具";
            // 
            // saveResultButton
            // 
            this.saveResultButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.saveResultButton.Location = new System.Drawing.Point(19, 82);
            this.saveResultButton.Name = "saveResultButton";
            this.saveResultButton.Size = new System.Drawing.Size(222, 34);
            this.saveResultButton.TabIndex = 1;
            this.saveResultButton.Text = "儲存結果";
            this.saveResultButton.UseVisualStyleBackColor = true;
            // 
            // openImageButton
            // 
            this.openImageButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.openImageButton.Location = new System.Drawing.Point(19, 34);
            this.openImageButton.Name = "openImageButton";
            this.openImageButton.Size = new System.Drawing.Size(222, 34);
            this.openImageButton.TabIndex = 0;
            this.openImageButton.Text = "開啟影像";
            this.openImageButton.UseVisualStyleBackColor = true;
            // 
            // imagePreviewBox
            // 
            this.imagePreviewBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.imagePreviewBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.imagePreviewBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imagePreviewBox.Location = new System.Drawing.Point(0, 0);
            this.imagePreviewBox.Name = "imagePreviewBox";
            this.imagePreviewBox.Size = new System.Drawing.Size(720, 557);
            this.imagePreviewBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.imagePreviewBox.TabIndex = 0;
            this.imagePreviewBox.TabStop = false;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 639);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(984, 22);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "statusStrip";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(31, 17);
            this.statusLabel.Text = "就緒";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 661);
            this.Controls.Add(this.mainSplitContainer);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.headerPanel);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "整合式影像處理軟件";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.mainSplitContainer.Panel1.ResumeLayout(false);
            this.mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).EndInit();
            this.mainSplitContainer.ResumeLayout(false);
            this.toolGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imagePreviewBox)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
