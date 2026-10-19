namespace CameraCaptureApp.Forms
{
    partial class GrayWaveformConfirmForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelSummary;
        private System.Windows.Forms.Button buttonConfirm;
        private System.Windows.Forms.Button buttonReselect;
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
            this.labelSummary = new System.Windows.Forms.Label();
            this.buttonConfirm = new System.Windows.Forms.Button();
            this.buttonReselect = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelTitle.Location = new System.Drawing.Point(16, 15);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(168, 25);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "確認灰階波形線段";
            // 
            // labelSummary
            // 
            this.labelSummary.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelSummary.Location = new System.Drawing.Point(21, 54);
            this.labelSummary.Name = "labelSummary";
            this.labelSummary.Size = new System.Drawing.Size(462, 68);
            this.labelSummary.TabIndex = 1;
            this.labelSummary.Text = "請確認你剛剛選取的線段是否正確。";
            // 
            // buttonConfirm
            // 
            this.buttonConfirm.Location = new System.Drawing.Point(279, 137);
            this.buttonConfirm.Name = "buttonConfirm";
            this.buttonConfirm.Size = new System.Drawing.Size(86, 32);
            this.buttonConfirm.TabIndex = 2;
            this.buttonConfirm.Text = "確認";
            this.buttonConfirm.UseVisualStyleBackColor = true;
            this.buttonConfirm.Click += new System.EventHandler(this.buttonConfirm_Click);
            // 
            // buttonReselect
            // 
            this.buttonReselect.Location = new System.Drawing.Point(171, 137);
            this.buttonReselect.Name = "buttonReselect";
            this.buttonReselect.Size = new System.Drawing.Size(102, 32);
            this.buttonReselect.TabIndex = 3;
            this.buttonReselect.Text = "重新指定";
            this.buttonReselect.UseVisualStyleBackColor = true;
            this.buttonReselect.Click += new System.EventHandler(this.buttonReselect_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(377, 137);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(86, 32);
            this.buttonCancel.TabIndex = 4;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // GrayWaveformConfirmForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(504, 184);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonReselect);
            this.Controls.Add(this.buttonConfirm);
            this.Controls.Add(this.labelSummary);
            this.Controls.Add(this.labelTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GrayWaveformConfirmForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "確認線段";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
