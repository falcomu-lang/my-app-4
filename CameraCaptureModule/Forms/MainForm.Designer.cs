namespace CameraCaptureApp.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.Panel panelFooter;
        private System.Windows.Forms.Panel panelViewerHost;
        private System.Windows.Forms.Button buttonCameraSettings;
        private System.Windows.Forms.Button buttonMeterWheel;
        private System.Windows.Forms.Button buttonConnect;
        private System.Windows.Forms.Button buttonDisconnect;
        private System.Windows.Forms.Button buttonStartPreview;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.Button buttonCapture;
        private System.Windows.Forms.Button buttonLoadImage;
        private System.Windows.Forms.Label labelHeaderConnection;
        private System.Windows.Forms.Label labelHeaderConnectionValue;
        private System.Windows.Forms.Label labelHeaderCamera;
        private System.Windows.Forms.Label labelHeaderCameraValue;
        private System.Windows.Forms.Label labelHeaderResolution;
        private System.Windows.Forms.Label labelHeaderResolutionValue;
        private System.Windows.Forms.Label labelHeaderTrigger;
        private System.Windows.Forms.Label labelHeaderTriggerValue;
        private System.Windows.Forms.Label labelHeaderSignal;
        private System.Windows.Forms.Label labelHeaderSignalValue;
        private System.Windows.Forms.Label labelFooterLines;
        private System.Windows.Forms.Label labelFooterLinesValue;
        private System.Windows.Forms.Label labelFooterPreview;
        private System.Windows.Forms.Label labelFooterPreviewValue;
        private System.Windows.Forms.Label labelFooterScanState;
        private System.Windows.Forms.Label labelFooterScanStateValue;
        private System.Windows.Forms.Label labelFooterMessage;
        private System.Windows.Forms.Label labelFooterMessageValue;

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
            this.panelHeader = new System.Windows.Forms.Panel();
            this.labelHeaderSignalValue = new System.Windows.Forms.Label();
            this.labelHeaderSignal = new System.Windows.Forms.Label();
            this.labelHeaderTriggerValue = new System.Windows.Forms.Label();
            this.labelHeaderTrigger = new System.Windows.Forms.Label();
            this.labelHeaderResolutionValue = new System.Windows.Forms.Label();
            this.labelHeaderResolution = new System.Windows.Forms.Label();
            this.labelHeaderCameraValue = new System.Windows.Forms.Label();
            this.labelHeaderCamera = new System.Windows.Forms.Label();
            this.labelHeaderConnectionValue = new System.Windows.Forms.Label();
            this.labelHeaderConnection = new System.Windows.Forms.Label();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.buttonLoadImage = new System.Windows.Forms.Button();
            this.buttonCapture = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.buttonStartPreview = new System.Windows.Forms.Button();
            this.buttonDisconnect = new System.Windows.Forms.Button();
            this.buttonConnect = new System.Windows.Forms.Button();
            this.buttonMeterWheel = new System.Windows.Forms.Button();
            this.buttonCameraSettings = new System.Windows.Forms.Button();
            this.panelContent = new System.Windows.Forms.Panel();
            this.panelViewerHost = new System.Windows.Forms.Panel();
            this.panelFooter = new System.Windows.Forms.Panel();
            this.labelFooterMessageValue = new System.Windows.Forms.Label();
            this.labelFooterMessage = new System.Windows.Forms.Label();
            this.labelFooterScanStateValue = new System.Windows.Forms.Label();
            this.labelFooterScanState = new System.Windows.Forms.Label();
            this.labelFooterPreviewValue = new System.Windows.Forms.Label();
            this.labelFooterPreview = new System.Windows.Forms.Label();
            this.labelFooterLinesValue = new System.Windows.Forms.Label();
            this.labelFooterLines = new System.Windows.Forms.Label();
            this.panelHeader.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.panelContent.SuspendLayout();
            this.panelFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.panelHeader.Controls.Add(this.labelHeaderSignalValue);
            this.panelHeader.Controls.Add(this.labelHeaderSignal);
            this.panelHeader.Controls.Add(this.labelHeaderTriggerValue);
            this.panelHeader.Controls.Add(this.labelHeaderTrigger);
            this.panelHeader.Controls.Add(this.labelHeaderResolutionValue);
            this.panelHeader.Controls.Add(this.labelHeaderResolution);
            this.panelHeader.Controls.Add(this.labelHeaderCameraValue);
            this.panelHeader.Controls.Add(this.labelHeaderCamera);
            this.panelHeader.Controls.Add(this.labelHeaderConnectionValue);
            this.panelHeader.Controls.Add(this.labelHeaderConnection);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(1382, 44);
            // 
            // header labels
            // 
            this.labelHeaderConnection.AutoSize = true;
            this.labelHeaderConnection.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelHeaderConnection.ForeColor = System.Drawing.Color.White;
            this.labelHeaderConnection.Location = new System.Drawing.Point(18, 11);
            this.labelHeaderConnection.Text = "Connection";
            this.labelHeaderConnectionValue.AutoSize = true;
            this.labelHeaderConnectionValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelHeaderConnectionValue.ForeColor = System.Drawing.Color.White;
            this.labelHeaderConnectionValue.Location = new System.Drawing.Point(104, 11);
            this.labelHeaderConnectionValue.Text = "Offline";
            this.labelHeaderCamera.AutoSize = true;
            this.labelHeaderCamera.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelHeaderCamera.ForeColor = System.Drawing.Color.White;
            this.labelHeaderCamera.Location = new System.Drawing.Point(220, 11);
            this.labelHeaderCamera.Text = "Camera";
            this.labelHeaderCameraValue.AutoSize = true;
            this.labelHeaderCameraValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelHeaderCameraValue.ForeColor = System.Drawing.Color.White;
            this.labelHeaderCameraValue.Location = new System.Drawing.Point(294, 11);
            this.labelHeaderCameraValue.Text = "Default Camera";
            this.labelHeaderResolution.AutoSize = true;
            this.labelHeaderResolution.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelHeaderResolution.ForeColor = System.Drawing.Color.White;
            this.labelHeaderResolution.Location = new System.Drawing.Point(500, 11);
            this.labelHeaderResolution.Text = "Resolution";
            this.labelHeaderResolutionValue.AutoSize = true;
            this.labelHeaderResolutionValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelHeaderResolutionValue.ForeColor = System.Drawing.Color.White;
            this.labelHeaderResolutionValue.Location = new System.Drawing.Point(595, 11);
            this.labelHeaderResolutionValue.Text = "1280 x 720";
            this.labelHeaderTrigger.AutoSize = true;
            this.labelHeaderTrigger.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelHeaderTrigger.ForeColor = System.Drawing.Color.White;
            this.labelHeaderTrigger.Location = new System.Drawing.Point(760, 11);
            this.labelHeaderTrigger.Text = "Trigger";
            this.labelHeaderTriggerValue.AutoSize = true;
            this.labelHeaderTriggerValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelHeaderTriggerValue.ForeColor = System.Drawing.Color.White;
            this.labelHeaderTriggerValue.Location = new System.Drawing.Point(831, 11);
            this.labelHeaderTriggerValue.Text = "Free Run";
            this.labelHeaderSignal.AutoSize = true;
            this.labelHeaderSignal.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelHeaderSignal.ForeColor = System.Drawing.Color.White;
            this.labelHeaderSignal.Location = new System.Drawing.Point(985, 11);
            this.labelHeaderSignal.Text = "Signal";
            this.labelHeaderSignalValue.AutoSize = true;
            this.labelHeaderSignalValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelHeaderSignalValue.ForeColor = System.Drawing.Color.White;
            this.labelHeaderSignalValue.Location = new System.Drawing.Point(1052, 11);
            this.labelHeaderSignalValue.Text = "Missing";
            // 
            // panelLeft
            // 
            this.panelLeft.BackColor = System.Drawing.Color.FromArgb(10, 14, 24);
            this.panelLeft.Controls.Add(this.buttonLoadImage);
            this.panelLeft.Controls.Add(this.buttonCapture);
            this.panelLeft.Controls.Add(this.buttonStop);
            this.panelLeft.Controls.Add(this.buttonStartPreview);
            this.panelLeft.Controls.Add(this.buttonDisconnect);
            this.panelLeft.Controls.Add(this.buttonConnect);
            this.panelLeft.Controls.Add(this.buttonMeterWheel);
            this.panelLeft.Controls.Add(this.buttonCameraSettings);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeft.Location = new System.Drawing.Point(0, 44);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Size = new System.Drawing.Size(290, 697);
            // 
            // buttons
            // 
            this.buttonCameraSettings.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonCameraSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonCameraSettings.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonCameraSettings.ForeColor = System.Drawing.Color.White;
            this.buttonCameraSettings.Location = new System.Drawing.Point(19, 20);
            this.buttonCameraSettings.Size = new System.Drawing.Size(252, 54);
            this.buttonCameraSettings.Text = "Camera Settings";
            this.buttonCameraSettings.Click += new System.EventHandler(this.buttonCameraSettings_Click);
            this.buttonMeterWheel.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonMeterWheel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonMeterWheel.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonMeterWheel.ForeColor = System.Drawing.Color.White;
            this.buttonMeterWheel.Location = new System.Drawing.Point(19, 86);
            this.buttonMeterWheel.Size = new System.Drawing.Size(252, 48);
            this.buttonMeterWheel.Text = "Meter Wheel";
            this.buttonMeterWheel.Click += new System.EventHandler(this.buttonMeterWheel_Click);
            this.buttonConnect.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonConnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonConnect.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonConnect.ForeColor = System.Drawing.Color.White;
            this.buttonConnect.Location = new System.Drawing.Point(19, 146);
            this.buttonConnect.Size = new System.Drawing.Size(120, 48);
            this.buttonConnect.Text = "Connect";
            this.buttonConnect.Click += new System.EventHandler(this.buttonConnect_Click);
            this.buttonDisconnect.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonDisconnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonDisconnect.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonDisconnect.ForeColor = System.Drawing.Color.White;
            this.buttonDisconnect.Location = new System.Drawing.Point(151, 146);
            this.buttonDisconnect.Size = new System.Drawing.Size(120, 48);
            this.buttonDisconnect.Text = "Disconnect";
            this.buttonDisconnect.Click += new System.EventHandler(this.buttonDisconnect_Click);
            this.buttonStartPreview.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonStartPreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonStartPreview.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonStartPreview.ForeColor = System.Drawing.Color.White;
            this.buttonStartPreview.Location = new System.Drawing.Point(19, 206);
            this.buttonStartPreview.Size = new System.Drawing.Size(252, 48);
            this.buttonStartPreview.Text = "Start Preview";
            this.buttonStartPreview.Click += new System.EventHandler(this.buttonStartPreview_Click);
            this.buttonStop.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonStop.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonStop.ForeColor = System.Drawing.Color.White;
            this.buttonStop.Location = new System.Drawing.Point(19, 266);
            this.buttonStop.Size = new System.Drawing.Size(252, 48);
            this.buttonStop.Text = "Stop";
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            this.buttonCapture.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonCapture.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonCapture.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonCapture.ForeColor = System.Drawing.Color.White;
            this.buttonCapture.Location = new System.Drawing.Point(19, 326);
            this.buttonCapture.Size = new System.Drawing.Size(120, 42);
            this.buttonCapture.Text = "Capture";
            this.buttonCapture.Click += new System.EventHandler(this.buttonCapture_Click);
            this.buttonLoadImage.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonLoadImage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonLoadImage.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.buttonLoadImage.ForeColor = System.Drawing.Color.White;
            this.buttonLoadImage.Location = new System.Drawing.Point(151, 326);
            this.buttonLoadImage.Size = new System.Drawing.Size(120, 42);
            this.buttonLoadImage.Text = "Load Image";
            this.buttonLoadImage.Click += new System.EventHandler(this.buttonLoadImage_Click);
            // panelContent
            // 
            this.panelContent.BackColor = System.Drawing.Color.Black;
            this.panelContent.Controls.Add(this.panelViewerHost);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelContent.Location = new System.Drawing.Point(290, 44);
            this.panelContent.Padding = new System.Windows.Forms.Padding(24);
            this.panelContent.Size = new System.Drawing.Size(1092, 581);
            // 
            // panelViewerHost
            // 
            this.panelViewerHost.BackColor = System.Drawing.Color.Black;
            this.panelViewerHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelViewerHost.Location = new System.Drawing.Point(24, 24);
            this.panelViewerHost.Name = "panelViewerHost";
            this.panelViewerHost.Size = new System.Drawing.Size(1044, 533);
            // 
            // panelFooter
            // 
            this.panelFooter.BackColor = System.Drawing.Color.FromArgb(10, 14, 24);
            this.panelFooter.Controls.Add(this.labelFooterMessageValue);
            this.panelFooter.Controls.Add(this.labelFooterMessage);
            this.panelFooter.Controls.Add(this.labelFooterScanStateValue);
            this.panelFooter.Controls.Add(this.labelFooterScanState);
            this.panelFooter.Controls.Add(this.labelFooterPreviewValue);
            this.panelFooter.Controls.Add(this.labelFooterPreview);
            this.panelFooter.Controls.Add(this.labelFooterLinesValue);
            this.panelFooter.Controls.Add(this.labelFooterLines);
            this.panelFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelFooter.Location = new System.Drawing.Point(290, 645);
            this.panelFooter.Name = "panelFooter";
            this.panelFooter.Size = new System.Drawing.Size(1092, 116);
            // 
            // footer labels
            // 
            this.labelFooterLines.AutoSize = true;
            this.labelFooterLines.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelFooterLines.ForeColor = System.Drawing.Color.White;
            this.labelFooterLines.Location = new System.Drawing.Point(27, 22);
            this.labelFooterLines.Text = "Lines";
            this.labelFooterLinesValue.AutoSize = true;
            this.labelFooterLinesValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelFooterLinesValue.ForeColor = System.Drawing.Color.White;
            this.labelFooterLinesValue.Location = new System.Drawing.Point(115, 22);
            this.labelFooterLinesValue.Text = "0";
            this.labelFooterPreview.AutoSize = true;
            this.labelFooterPreview.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelFooterPreview.ForeColor = System.Drawing.Color.White;
            this.labelFooterPreview.Location = new System.Drawing.Point(210, 22);
            this.labelFooterPreview.Text = "Preview";
            this.labelFooterPreviewValue.AutoSize = true;
            this.labelFooterPreviewValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelFooterPreviewValue.ForeColor = System.Drawing.Color.White;
            this.labelFooterPreviewValue.Location = new System.Drawing.Point(298, 22);
            this.labelFooterPreviewValue.Text = "Stopped";
            this.labelFooterScanState.AutoSize = true;
            this.labelFooterScanState.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelFooterScanState.ForeColor = System.Drawing.Color.White;
            this.labelFooterScanState.Location = new System.Drawing.Point(430, 22);
            this.labelFooterScanState.Text = "State";
            this.labelFooterScanStateValue.AutoSize = true;
            this.labelFooterScanStateValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelFooterScanStateValue.ForeColor = System.Drawing.Color.White;
            this.labelFooterScanStateValue.Location = new System.Drawing.Point(518, 22);
            this.labelFooterScanStateValue.Text = "Idle";
            this.labelFooterMessage.AutoSize = true;
            this.labelFooterMessage.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelFooterMessage.ForeColor = System.Drawing.Color.White;
            this.labelFooterMessage.Location = new System.Drawing.Point(27, 68);
            this.labelFooterMessage.Text = "Message";
            this.labelFooterMessageValue.AutoSize = true;
            this.labelFooterMessageValue.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.labelFooterMessageValue.ForeColor = System.Drawing.Color.White;
            this.labelFooterMessageValue.Location = new System.Drawing.Point(115, 68);
            this.labelFooterMessageValue.Text = "Ready";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1382, 761);
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelFooter);
            this.Controls.Add(this.panelLeft);
            this.Controls.Add(this.panelHeader);
            this.MinimumSize = new System.Drawing.Size(1200, 700);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Camera Capture App - Line Scan";
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelLeft.ResumeLayout(false);
            this.panelContent.ResumeLayout(false);
            this.panelFooter.ResumeLayout(false);
            this.panelFooter.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
