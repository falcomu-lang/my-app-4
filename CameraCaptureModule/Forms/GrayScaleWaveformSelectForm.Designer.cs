namespace CameraCaptureApp.Forms
{
    partial class GrayScaleWaveformSelectForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelHint;
        private System.Windows.Forms.Panel panelImage;
        private System.Windows.Forms.Button buttonOk;
        private System.Windows.Forms.Button buttonReset;
        private System.Windows.Forms.Button buttonCancel;

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
            this.labelHint = new System.Windows.Forms.Label();
            this.panelImage = new System.Windows.Forms.Panel();
            this.buttonOk = new System.Windows.Forms.Button();
            this.buttonReset = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelTitle.Location = new System.Drawing.Point(16, 15);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(150, 25);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "請指定取樣線段";
            // 
            // labelHint
            // 
            this.labelHint.AutoSize = true;
            this.labelHint.Location = new System.Drawing.Point(18, 48);
            this.labelHint.Name = "labelHint";
            this.labelHint.Size = new System.Drawing.Size(644, 16);
            this.labelHint.TabIndex = 1;
            this.labelHint.Text = "請在圖像中拖曳一條線，可由左到右、右到左、上到下、下到上。按確認後會產生灰階波形。";
            // 
            // panelImage
            // 
            this.panelImage.BackColor = System.Drawing.Color.Black;
            this.panelImage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelImage.Location = new System.Drawing.Point(21, 76);
            this.panelImage.Name = "panelImage";
            this.panelImage.Size = new System.Drawing.Size(960, 540);
            this.panelImage.TabIndex = 2;
            this.panelImage.Paint += new System.Windows.Forms.PaintEventHandler(this.panelImage_Paint);
            this.panelImage.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelImage_MouseDown);
            this.panelImage.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelImage_MouseMove);
            this.panelImage.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelImage_MouseUp);
            // 
            // buttonOk
            // 
            this.buttonOk.Location = new System.Drawing.Point(802, 632);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Size = new System.Drawing.Size(86, 32);
            this.buttonOk.TabIndex = 3;
            this.buttonOk.Text = "確認";
            this.buttonOk.UseVisualStyleBackColor = true;
            this.buttonOk.Click += new System.EventHandler(this.buttonOk_Click);
            // 
            // buttonReset
            // 
            this.buttonReset.Location = new System.Drawing.Point(694, 632);
            this.buttonReset.Name = "buttonReset";
            this.buttonReset.Size = new System.Drawing.Size(102, 32);
            this.buttonReset.TabIndex = 4;
            this.buttonReset.Text = "重新指定";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(894, 632);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(86, 32);
            this.buttonCancel.TabIndex = 5;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // GrayScaleWaveformSelectForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(1004, 681);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonReset);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.panelImage);
            this.Controls.Add(this.labelHint);
            this.Controls.Add(this.labelTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GrayScaleWaveformSelectForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "灰階波形 - 線段選取";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
