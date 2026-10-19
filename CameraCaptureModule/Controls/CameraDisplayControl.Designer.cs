namespace CameraCaptureApp.Controls
{
    partial class CameraDisplayControl
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Label overlayLabel;
        private System.Windows.Forms.Label resolutionLabel;
        private CameraCaptureApp.Controls.BufferedRenderPanel viewerPanel;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button buttonSaveSnapshot;
        private System.Windows.Forms.Button buttonGrayWaveform;
        private System.Windows.Forms.Button buttonFitToWindow;

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
            this.topPanel = new System.Windows.Forms.Panel();
            this.resolutionLabel = new System.Windows.Forms.Label();
            this.overlayLabel = new System.Windows.Forms.Label();
            this.viewerPanel = new CameraCaptureApp.Controls.BufferedRenderPanel();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.buttonSaveSnapshot = new System.Windows.Forms.Button();
            this.buttonGrayWaveform = new System.Windows.Forms.Button();
            this.buttonFitToWindow = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            this.topPanel.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // topPanel
            // 
            this.topPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(22)))), ((int)(((byte)(34)))));
            this.topPanel.Controls.Add(this.resolutionLabel);
            this.topPanel.Controls.Add(this.overlayLabel);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            this.topPanel.Size = new System.Drawing.Size(900, 50);
            this.topPanel.TabIndex = 0;
            // 
            // resolutionLabel
            // 
            this.resolutionLabel.AutoSize = true;
            this.resolutionLabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.resolutionLabel.ForeColor = System.Drawing.Color.Gainsboro;
            this.resolutionLabel.Location = new System.Drawing.Point(800, 10);
            this.resolutionLabel.Name = "resolutionLabel";
            this.resolutionLabel.Size = new System.Drawing.Size(88, 16);
            this.resolutionLabel.TabIndex = 1;
            this.resolutionLabel.Text = "1280 x 720";
            // 
            // overlayLabel
            // 
            this.overlayLabel.AutoSize = true;
            this.overlayLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.overlayLabel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.overlayLabel.ForeColor = System.Drawing.Color.White;
            this.overlayLabel.Location = new System.Drawing.Point(12, 10);
            this.overlayLabel.Name = "overlayLabel";
            this.overlayLabel.Size = new System.Drawing.Size(136, 22);
            this.overlayLabel.TabIndex = 0;
            this.overlayLabel.Text = "1280 x 720 預覽區";
            // 
            // viewerPanel
            // 
            this.viewerPanel.BackColor = System.Drawing.Color.Black;
            this.viewerPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.viewerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.viewerPanel.Location = new System.Drawing.Point(0, 50);
            this.viewerPanel.Name = "viewerPanel";
            this.viewerPanel.Size = new System.Drawing.Size(900, 470);
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
            this.bottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(22)))), ((int)(((byte)(34)))));
            this.bottomPanel.Controls.Add(this.buttonSaveSnapshot);
            this.bottomPanel.Controls.Add(this.buttonGrayWaveform);
            this.bottomPanel.Controls.Add(this.buttonFitToWindow);
            this.bottomPanel.Controls.Add(this.statusLabel);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 520);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            this.bottomPanel.Size = new System.Drawing.Size(900, 50);
            this.bottomPanel.TabIndex = 2;
            // 
            // buttonSaveSnapshot
            // 
            this.buttonSaveSnapshot.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonSaveSnapshot.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSaveSnapshot.ForeColor = System.Drawing.Color.White;
            this.buttonSaveSnapshot.Location = new System.Drawing.Point(664, 10);
            this.buttonSaveSnapshot.Name = "buttonSaveSnapshot";
            this.buttonSaveSnapshot.Size = new System.Drawing.Size(112, 30);
            this.buttonSaveSnapshot.TabIndex = 2;
            this.buttonSaveSnapshot.Text = "保留影像";
            this.buttonSaveSnapshot.UseVisualStyleBackColor = true;
            this.buttonSaveSnapshot.Click += new System.EventHandler(this.buttonSaveSnapshot_Click);
            // 
            // buttonGrayWaveform
            // 
            this.buttonGrayWaveform.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonGrayWaveform.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonGrayWaveform.ForeColor = System.Drawing.Color.White;
            this.buttonGrayWaveform.Location = new System.Drawing.Point(520, 10);
            this.buttonGrayWaveform.Name = "buttonGrayWaveform";
            this.buttonGrayWaveform.Size = new System.Drawing.Size(144, 30);
            this.buttonGrayWaveform.TabIndex = 3;
            this.buttonGrayWaveform.Text = "取得灰階波形";
            this.buttonGrayWaveform.UseVisualStyleBackColor = true;
            this.buttonGrayWaveform.Click += new System.EventHandler(this.buttonGrayWaveform_Click);
            // 
            // buttonFitToWindow
            // 
            this.buttonFitToWindow.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonFitToWindow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFitToWindow.ForeColor = System.Drawing.Color.White;
            this.buttonFitToWindow.Location = new System.Drawing.Point(776, 10);
            this.buttonFitToWindow.Name = "buttonFitToWindow";
            this.buttonFitToWindow.Size = new System.Drawing.Size(112, 30);
            this.buttonFitToWindow.TabIndex = 1;
            this.buttonFitToWindow.Text = "重設視圖";
            this.buttonFitToWindow.UseVisualStyleBackColor = true;
            this.buttonFitToWindow.Click += new System.EventHandler(this.buttonFitToWindow_Click);
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this.statusLabel.ForeColor = System.Drawing.Color.Gainsboro;
            this.statusLabel.Location = new System.Drawing.Point(12, 10);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(98, 16);
            this.statusLabel.TabIndex = 0;
            this.statusLabel.Text = "尚未載入圖片";
            // 
            // CameraDisplayControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.viewerPanel);
            this.Controls.Add(this.bottomPanel);
            this.Controls.Add(this.topPanel);
            this.Name = "CameraDisplayControl";
            this.Size = new System.Drawing.Size(900, 570);
            this.SizeChanged += new System.EventHandler(this.CameraDisplayControl_SizeChanged);
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.ResumeLayout(false);

        }
    }
}
