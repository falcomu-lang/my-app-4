namespace CameraCaptureApp.Forms
{
    partial class GrayScaleWaveformForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelInfo;
        private DoubleBufferedPanel panelChart;
        private System.Windows.Forms.Button buttonClose;

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
            this.labelTitle = new System.Windows.Forms.Label();
            this.labelInfo = new System.Windows.Forms.Label();
            this.panelChart = new CameraCaptureApp.Forms.GrayScaleWaveformForm.DoubleBufferedPanel();
            this.buttonClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelTitle.Location = new System.Drawing.Point(16, 14);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(150, 25);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "灰階波形結果";
            // 
            // labelInfo
            // 
            this.labelInfo.AutoSize = true;
            this.labelInfo.Location = new System.Drawing.Point(18, 49);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Size = new System.Drawing.Size(44, 16);
            this.labelInfo.TabIndex = 1;
            this.labelInfo.Text = "Ready";
            // 
            // panelChart
            // 
            this.panelChart.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelChart.BackColor = System.Drawing.Color.FromArgb(20, 24, 35);
            this.panelChart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelChart.Location = new System.Drawing.Point(21, 74);
            this.panelChart.Name = "panelChart";
            this.panelChart.Size = new System.Drawing.Size(960, 480);
            this.panelChart.TabIndex = 2;
            this.panelChart.TabStop = true;
            this.panelChart.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelChart_MouseDown);
            this.panelChart.MouseEnter += new System.EventHandler(this.panelChart_MouseEnter);
            this.panelChart.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelChart_MouseMove);
            this.panelChart.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelChart_MouseUp);
            this.panelChart.Paint += new System.Windows.Forms.PaintEventHandler(this.panelChart_Paint);
            // 
            // buttonClose
            // 
            this.buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonClose.Location = new System.Drawing.Point(895, 570);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(86, 32);
            this.buttonClose.TabIndex = 3;
            this.buttonClose.Text = "關閉";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            // 
            // GrayScaleWaveformForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1004, 621);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.panelChart);
            this.Controls.Add(this.labelInfo);
            this.Controls.Add(this.labelTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new System.Drawing.Size(760, 420);
            this.MinimizeBox = false;
            this.Name = "GrayScaleWaveformForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "灰階波形";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private class DoubleBufferedPanel : System.Windows.Forms.Panel
        {
            public DoubleBufferedPanel()
            {
                this.DoubleBuffered = true;
                this.ResizeRedraw = true;
            }
        }
    }
}
