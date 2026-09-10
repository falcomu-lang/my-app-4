namespace IntegratedImageProcessingApp.Controls
{
    partial class ImageDisplayControl
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label resolutionLabel;
        private IntegratedImageProcessingApp.Controls.BufferedRenderPanel viewerPanel;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button buttonFitToWindow;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCurrentImage();
                if (components != null)
                {
                    components.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.topPanel = new System.Windows.Forms.Panel();
            this.resolutionLabel = new System.Windows.Forms.Label();
            this.titleLabel = new System.Windows.Forms.Label();
            this.viewerPanel = new IntegratedImageProcessingApp.Controls.BufferedRenderPanel();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.buttonFitToWindow = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            this.topPanel.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // topPanel
            // 
            this.topPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.topPanel.Controls.Add(this.resolutionLabel);
            this.topPanel.Controls.Add(this.titleLabel);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.topPanel.Size = new System.Drawing.Size(340, 42);
            this.topPanel.TabIndex = 0;
            // 
            // resolutionLabel
            // 
            this.resolutionLabel.AutoSize = true;
            this.resolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.resolutionLabel.ForeColor = System.Drawing.Color.Black;
            this.resolutionLabel.Location = new System.Drawing.Point(275, 8);
            this.resolutionLabel.Name = "resolutionLabel";
            this.resolutionLabel.Size = new System.Drawing.Size(55, 15);
            this.resolutionLabel.TabIndex = 1;
            this.resolutionLabel.Text = "0 x 0";
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.titleLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.titleLabel.ForeColor = System.Drawing.Color.Black;
            this.titleLabel.Location = new System.Drawing.Point(10, 8);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(56, 15);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "圖片顯示";
            // 
            // viewerPanel
            // 
            this.viewerPanel.BackColor = System.Drawing.Color.White;
            this.viewerPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.viewerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.viewerPanel.Location = new System.Drawing.Point(0, 42);
            this.viewerPanel.Name = "viewerPanel";
            this.viewerPanel.Size = new System.Drawing.Size(340, 248);
            this.viewerPanel.TabIndex = 1;
            this.viewerPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.viewerPanel_Paint);
            this.viewerPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.viewerPanel_MouseDown);
            this.viewerPanel.MouseEnter += new System.EventHandler(this.viewerPanel_MouseEnter);
            this.viewerPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.viewerPanel_MouseMove);
            this.viewerPanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.viewerPanel_MouseUp);
            this.viewerPanel.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.viewerPanel_MouseWheel);
            // 
            // bottomPanel
            // 
            this.bottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.bottomPanel.Controls.Add(this.buttonFitToWindow);
            this.bottomPanel.Controls.Add(this.statusLabel);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 290);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.bottomPanel.Size = new System.Drawing.Size(340, 42);
            this.bottomPanel.TabIndex = 2;
            // 
            // buttonFitToWindow
            // 
            this.buttonFitToWindow.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonFitToWindow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFitToWindow.ForeColor = System.Drawing.Color.Black;
            this.buttonFitToWindow.Location = new System.Drawing.Point(240, 7);
            this.buttonFitToWindow.Name = "buttonFitToWindow";
            this.buttonFitToWindow.Size = new System.Drawing.Size(90, 28);
            this.buttonFitToWindow.TabIndex = 1;
            this.buttonFitToWindow.Text = "重設視圖";
            this.buttonFitToWindow.UseVisualStyleBackColor = true;
            this.buttonFitToWindow.Click += new System.EventHandler(this.buttonFitToWindow_Click);
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.statusLabel.ForeColor = System.Drawing.Color.Black;
            this.statusLabel.Location = new System.Drawing.Point(10, 7);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(79, 15);
            this.statusLabel.TabIndex = 0;
            this.statusLabel.Text = "尚未載入圖片";
            // 
            // ImageDisplayControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.viewerPanel);
            this.Controls.Add(this.bottomPanel);
            this.Controls.Add(this.topPanel);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.Name = "ImageDisplayControl";
            this.Size = new System.Drawing.Size(340, 332);
            this.SizeChanged += new System.EventHandler(this.ImageDisplayControl_SizeChanged);
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
