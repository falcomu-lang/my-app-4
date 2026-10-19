namespace CameraCaptureApp.Forms
{
    partial class MeterWheelControlForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox comboCardId;
        private System.Windows.Forms.Label labelCard;
        private System.Windows.Forms.Button buttonConnect;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Label labelEncoderTitle;
        private System.Windows.Forms.Label labelEncoderValue;
        private System.Windows.Forms.Button buttonClearEncoder;
        private System.Windows.Forms.NumericUpDown numericEncoder;
        private System.Windows.Forms.Button buttonSetEncoder;
        private System.Windows.Forms.Label labelCompareTitle;
        private System.Windows.Forms.Label labelCompareValue;
        private System.Windows.Forms.Button buttonClearCompare;
        private System.Windows.Forms.NumericUpDown numericCompare;
        private System.Windows.Forms.Button buttonSetCompare;
        private System.Windows.Forms.Label labelIncrementTitle;
        private System.Windows.Forms.NumericUpDown numericIncrement;
        private System.Windows.Forms.Button buttonApplyIncrement;
        private System.Windows.Forms.Label labelMultipleRateTitle;
        private System.Windows.Forms.ComboBox comboMultipleRate;
        private System.Windows.Forms.Button buttonSetMultipleRate;
        private System.Windows.Forms.CheckBox checkReverseDirection;
        private System.Windows.Forms.Label labelCmpOutWidthTitle;
        private System.Windows.Forms.NumericUpDown numericCmpOutWidth;
        private System.Windows.Forms.Button buttonSetCmpOutWidth;
        private System.Windows.Forms.Button buttonExtensionCompare;
        private System.Windows.Forms.Timer timerRefresh;

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
            this.components = new System.ComponentModel.Container();
            this.comboCardId = new System.Windows.Forms.ComboBox();
            this.labelCard = new System.Windows.Forms.Label();
            this.buttonConnect = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelEncoderTitle = new System.Windows.Forms.Label();
            this.labelEncoderValue = new System.Windows.Forms.Label();
            this.buttonClearEncoder = new System.Windows.Forms.Button();
            this.numericEncoder = new System.Windows.Forms.NumericUpDown();
            this.buttonSetEncoder = new System.Windows.Forms.Button();
            this.labelCompareTitle = new System.Windows.Forms.Label();
            this.labelCompareValue = new System.Windows.Forms.Label();
            this.buttonClearCompare = new System.Windows.Forms.Button();
            this.numericCompare = new System.Windows.Forms.NumericUpDown();
            this.buttonSetCompare = new System.Windows.Forms.Button();
            this.labelIncrementTitle = new System.Windows.Forms.Label();
            this.numericIncrement = new System.Windows.Forms.NumericUpDown();
            this.buttonApplyIncrement = new System.Windows.Forms.Button();
            this.labelMultipleRateTitle = new System.Windows.Forms.Label();
            this.comboMultipleRate = new System.Windows.Forms.ComboBox();
            this.buttonSetMultipleRate = new System.Windows.Forms.Button();
            this.checkReverseDirection = new System.Windows.Forms.CheckBox();
            this.labelCmpOutWidthTitle = new System.Windows.Forms.Label();
            this.numericCmpOutWidth = new System.Windows.Forms.NumericUpDown();
            this.buttonSetCmpOutWidth = new System.Windows.Forms.Button();
            this.buttonExtensionCompare = new System.Windows.Forms.Button();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.numericEncoder)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericCompare)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericIncrement)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericCmpOutWidth)).BeginInit();
            this.SuspendLayout();
            // 
            // comboCardId
            // 
            this.comboCardId.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCardId.FormattingEnabled = true;
            this.comboCardId.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14",
            "15"});
            this.comboCardId.Location = new System.Drawing.Point(68, 14);
            this.comboCardId.Name = "comboCardId";
            this.comboCardId.Size = new System.Drawing.Size(72, 25);
            this.comboCardId.TabIndex = 1;
            // 
            // labelCard
            // 
            this.labelCard.AutoSize = true;
            this.labelCard.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelCard.ForeColor = System.Drawing.Color.White;
            this.labelCard.Location = new System.Drawing.Point(16, 17);
            this.labelCard.Name = "labelCard";
            this.labelCard.Size = new System.Drawing.Size(38, 19);
            this.labelCard.TabIndex = 0;
            this.labelCard.Text = "Card";
            // 
            // buttonConnect
            // 
            this.buttonConnect.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonConnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonConnect.ForeColor = System.Drawing.Color.White;
            this.buttonConnect.Location = new System.Drawing.Point(152, 12);
            this.buttonConnect.Name = "buttonConnect";
            this.buttonConnect.Size = new System.Drawing.Size(96, 30);
            this.buttonConnect.TabIndex = 2;
            this.buttonConnect.Text = "Connect";
            this.buttonConnect.UseVisualStyleBackColor = false;
            this.buttonConnect.Click += new System.EventHandler(this.buttonConnect_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.ForeColor = System.Drawing.Color.FromArgb(210, 220, 240);
            this.labelStatus.Location = new System.Drawing.Point(264, 18);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(49, 17);
            this.labelStatus.TabIndex = 3;
            this.labelStatus.Text = "Offline";
            // 
            // labelEncoderTitle
            // 
            this.labelEncoderTitle.AutoSize = true;
            this.labelEncoderTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelEncoderTitle.ForeColor = System.Drawing.Color.White;
            this.labelEncoderTitle.Location = new System.Drawing.Point(16, 64);
            this.labelEncoderTitle.Name = "labelEncoderTitle";
            this.labelEncoderTitle.Size = new System.Drawing.Size(67, 19);
            this.labelEncoderTitle.TabIndex = 4;
            this.labelEncoderTitle.Text = "Encoder";
            // 
            // labelEncoderValue
            // 
            this.labelEncoderValue.BackColor = System.Drawing.Color.FromArgb(8, 12, 20);
            this.labelEncoderValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelEncoderValue.Font = new System.Drawing.Font("Consolas", 13.8F, System.Drawing.FontStyle.Bold);
            this.labelEncoderValue.ForeColor = System.Drawing.Color.White;
            this.labelEncoderValue.Location = new System.Drawing.Point(92, 56);
            this.labelEncoderValue.Name = "labelEncoderValue";
            this.labelEncoderValue.Size = new System.Drawing.Size(170, 34);
            this.labelEncoderValue.TabIndex = 5;
            this.labelEncoderValue.Text = "0";
            this.labelEncoderValue.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // buttonClearEncoder
            // 
            this.buttonClearEncoder.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonClearEncoder.Enabled = false;
            this.buttonClearEncoder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonClearEncoder.ForeColor = System.Drawing.Color.White;
            this.buttonClearEncoder.Location = new System.Drawing.Point(272, 56);
            this.buttonClearEncoder.Name = "buttonClearEncoder";
            this.buttonClearEncoder.Size = new System.Drawing.Size(64, 34);
            this.buttonClearEncoder.TabIndex = 6;
            this.buttonClearEncoder.Text = "Clear";
            this.buttonClearEncoder.UseVisualStyleBackColor = false;
            this.buttonClearEncoder.Click += new System.EventHandler(this.buttonClearEncoder_Click);
            // 
            // numericEncoder
            // 
            this.numericEncoder.Location = new System.Drawing.Point(92, 96);
            this.numericEncoder.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.numericEncoder.Minimum = -2147483648M;
            this.numericEncoder.Name = "numericEncoder";
            this.numericEncoder.Size = new System.Drawing.Size(170, 25);
            this.numericEncoder.TabIndex = 7;
            this.numericEncoder.ThousandsSeparator = true;
            this.numericEncoder.ValueChanged += new System.EventHandler(this.numericEncoder_ValueChanged);
            // 
            // buttonSetEncoder
            // 
            this.buttonSetEncoder.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonSetEncoder.Enabled = false;
            this.buttonSetEncoder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSetEncoder.ForeColor = System.Drawing.Color.White;
            this.buttonSetEncoder.Location = new System.Drawing.Point(272, 94);
            this.buttonSetEncoder.Name = "buttonSetEncoder";
            this.buttonSetEncoder.Size = new System.Drawing.Size(64, 30);
            this.buttonSetEncoder.TabIndex = 8;
            this.buttonSetEncoder.Text = "Set";
            this.buttonSetEncoder.UseVisualStyleBackColor = false;
            this.buttonSetEncoder.Click += new System.EventHandler(this.buttonSetEncoder_Click);
            // 
            // labelCompareTitle
            // 
            this.labelCompareTitle.AutoSize = true;
            this.labelCompareTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelCompareTitle.ForeColor = System.Drawing.Color.White;
            this.labelCompareTitle.Location = new System.Drawing.Point(16, 150);
            this.labelCompareTitle.Name = "labelCompareTitle";
            this.labelCompareTitle.Size = new System.Drawing.Size(74, 19);
            this.labelCompareTitle.TabIndex = 9;
            this.labelCompareTitle.Text = "Compare";
            // 
            // labelCompareValue
            // 
            this.labelCompareValue.BackColor = System.Drawing.Color.FromArgb(8, 12, 20);
            this.labelCompareValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelCompareValue.Font = new System.Drawing.Font("Consolas", 13.8F, System.Drawing.FontStyle.Bold);
            this.labelCompareValue.ForeColor = System.Drawing.Color.White;
            this.labelCompareValue.Location = new System.Drawing.Point(92, 142);
            this.labelCompareValue.Name = "labelCompareValue";
            this.labelCompareValue.Size = new System.Drawing.Size(170, 34);
            this.labelCompareValue.TabIndex = 10;
            this.labelCompareValue.Text = "0";
            this.labelCompareValue.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // buttonClearCompare
            // 
            this.buttonClearCompare.BackColor = System.Drawing.Color.FromArgb(36, 51, 84);
            this.buttonClearCompare.Enabled = false;
            this.buttonClearCompare.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonClearCompare.ForeColor = System.Drawing.Color.White;
            this.buttonClearCompare.Location = new System.Drawing.Point(272, 142);
            this.buttonClearCompare.Name = "buttonClearCompare";
            this.buttonClearCompare.Size = new System.Drawing.Size(64, 34);
            this.buttonClearCompare.TabIndex = 11;
            this.buttonClearCompare.Text = "Clear";
            this.buttonClearCompare.UseVisualStyleBackColor = false;
            this.buttonClearCompare.Click += new System.EventHandler(this.buttonClearCompare_Click);
            // 
            // numericCompare
            // 
            this.numericCompare.Location = new System.Drawing.Point(92, 182);
            this.numericCompare.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.numericCompare.Minimum = -2147483648M;
            this.numericCompare.Name = "numericCompare";
            this.numericCompare.Size = new System.Drawing.Size(170, 25);
            this.numericCompare.TabIndex = 12;
            this.numericCompare.ThousandsSeparator = true;
            this.numericCompare.ValueChanged += new System.EventHandler(this.numericCompare_ValueChanged);
            // 
            // buttonSetCompare
            // 
            this.buttonSetCompare.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonSetCompare.Enabled = false;
            this.buttonSetCompare.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSetCompare.ForeColor = System.Drawing.Color.White;
            this.buttonSetCompare.Location = new System.Drawing.Point(272, 180);
            this.buttonSetCompare.Name = "buttonSetCompare";
            this.buttonSetCompare.Size = new System.Drawing.Size(64, 30);
            this.buttonSetCompare.TabIndex = 13;
            this.buttonSetCompare.Text = "Set";
            this.buttonSetCompare.UseVisualStyleBackColor = false;
            this.buttonSetCompare.Click += new System.EventHandler(this.buttonSetCompare_Click);
            // 
            // labelIncrementTitle
            // 
            this.labelIncrementTitle.AutoSize = true;
            this.labelIncrementTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelIncrementTitle.ForeColor = System.Drawing.Color.White;
            this.labelIncrementTitle.Location = new System.Drawing.Point(16, 224);
            this.labelIncrementTitle.Name = "labelIncrementTitle";
            this.labelIncrementTitle.Size = new System.Drawing.Size(77, 19);
            this.labelIncrementTitle.TabIndex = 14;
            this.labelIncrementTitle.Text = "Increment";
            // 
            // numericIncrement
            // 
            this.numericIncrement.Location = new System.Drawing.Point(92, 220);
            this.numericIncrement.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.numericIncrement.Minimum = -2147483648M;
            this.numericIncrement.Name = "numericIncrement";
            this.numericIncrement.Size = new System.Drawing.Size(170, 25);
            this.numericIncrement.TabIndex = 15;
            this.numericIncrement.ThousandsSeparator = true;
            // 
            // buttonApplyIncrement
            // 
            this.buttonApplyIncrement.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonApplyIncrement.Enabled = false;
            this.buttonApplyIncrement.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonApplyIncrement.ForeColor = System.Drawing.Color.White;
            this.buttonApplyIncrement.Location = new System.Drawing.Point(272, 218);
            this.buttonApplyIncrement.Name = "buttonApplyIncrement";
            this.buttonApplyIncrement.Size = new System.Drawing.Size(64, 30);
            this.buttonApplyIncrement.TabIndex = 16;
            this.buttonApplyIncrement.Text = "Apply";
            this.buttonApplyIncrement.UseVisualStyleBackColor = false;
            this.buttonApplyIncrement.Click += new System.EventHandler(this.buttonApplyIncrement_Click);
            // 
            // labelMultipleRateTitle
            // 
            this.labelMultipleRateTitle.AutoSize = true;
            this.labelMultipleRateTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelMultipleRateTitle.ForeColor = System.Drawing.Color.White;
            this.labelMultipleRateTitle.Location = new System.Drawing.Point(16, 264);
            this.labelMultipleRateTitle.Name = "labelMultipleRateTitle";
            this.labelMultipleRateTitle.Size = new System.Drawing.Size(100, 19);
            this.labelMultipleRateTitle.TabIndex = 17;
            this.labelMultipleRateTitle.Text = "Multiple Rate";
            // 
            // comboMultipleRate
            // 
            this.comboMultipleRate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMultipleRate.FormattingEnabled = true;
            this.comboMultipleRate.Items.AddRange(new object[] {
            "X4",
            "X2",
            "X1"});
            this.comboMultipleRate.Location = new System.Drawing.Point(122, 260);
            this.comboMultipleRate.Name = "comboMultipleRate";
            this.comboMultipleRate.Size = new System.Drawing.Size(140, 25);
            this.comboMultipleRate.TabIndex = 18;
            this.comboMultipleRate.SelectedIndexChanged += new System.EventHandler(this.comboMultipleRate_SelectedIndexChanged);
            // 
            // buttonSetMultipleRate
            // 
            this.buttonSetMultipleRate.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonSetMultipleRate.Enabled = false;
            this.buttonSetMultipleRate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSetMultipleRate.ForeColor = System.Drawing.Color.White;
            this.buttonSetMultipleRate.Location = new System.Drawing.Point(272, 258);
            this.buttonSetMultipleRate.Name = "buttonSetMultipleRate";
            this.buttonSetMultipleRate.Size = new System.Drawing.Size(64, 30);
            this.buttonSetMultipleRate.TabIndex = 19;
            this.buttonSetMultipleRate.Text = "Set";
            this.buttonSetMultipleRate.UseVisualStyleBackColor = false;
            this.buttonSetMultipleRate.Click += new System.EventHandler(this.buttonSetMultipleRate_Click);
            // 
            // checkReverseDirection
            // 
            this.checkReverseDirection.AutoSize = true;
            this.checkReverseDirection.Enabled = false;
            this.checkReverseDirection.ForeColor = System.Drawing.Color.White;
            this.checkReverseDirection.Location = new System.Drawing.Point(122, 302);
            this.checkReverseDirection.Name = "checkReverseDirection";
            this.checkReverseDirection.Size = new System.Drawing.Size(136, 23);
            this.checkReverseDirection.TabIndex = 20;
            this.checkReverseDirection.Text = "Reverse Direction";
            this.checkReverseDirection.UseVisualStyleBackColor = true;
            this.checkReverseDirection.CheckedChanged += new System.EventHandler(this.checkReverseDirection_CheckedChanged);
            // 
            // labelCmpOutWidthTitle
            // 
            this.labelCmpOutWidthTitle.AutoSize = true;
            this.labelCmpOutWidthTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelCmpOutWidthTitle.ForeColor = System.Drawing.Color.White;
            this.labelCmpOutWidthTitle.Location = new System.Drawing.Point(16, 344);
            this.labelCmpOutWidthTitle.Name = "labelCmpOutWidthTitle";
            this.labelCmpOutWidthTitle.Size = new System.Drawing.Size(115, 19);
            this.labelCmpOutWidthTitle.TabIndex = 21;
            this.labelCmpOutWidthTitle.Text = "CMP Out Width";
            // 
            // numericCmpOutWidth
            // 
            this.numericCmpOutWidth.Location = new System.Drawing.Point(122, 340);
            this.numericCmpOutWidth.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numericCmpOutWidth.Name = "numericCmpOutWidth";
            this.numericCmpOutWidth.Size = new System.Drawing.Size(140, 25);
            this.numericCmpOutWidth.TabIndex = 22;
            this.numericCmpOutWidth.ThousandsSeparator = true;
            this.numericCmpOutWidth.ValueChanged += new System.EventHandler(this.numericCmpOutWidth_ValueChanged);
            // 
            // buttonSetCmpOutWidth
            // 
            this.buttonSetCmpOutWidth.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonSetCmpOutWidth.Enabled = false;
            this.buttonSetCmpOutWidth.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSetCmpOutWidth.ForeColor = System.Drawing.Color.White;
            this.buttonSetCmpOutWidth.Location = new System.Drawing.Point(272, 338);
            this.buttonSetCmpOutWidth.Name = "buttonSetCmpOutWidth";
            this.buttonSetCmpOutWidth.Size = new System.Drawing.Size(64, 30);
            this.buttonSetCmpOutWidth.TabIndex = 23;
            this.buttonSetCmpOutWidth.Text = "Set";
            this.buttonSetCmpOutWidth.UseVisualStyleBackColor = false;
            this.buttonSetCmpOutWidth.Click += new System.EventHandler(this.buttonSetCmpOutWidth_Click);
            // 
            // buttonExtensionCompare
            // 
            this.buttonExtensionCompare.BackColor = System.Drawing.Color.FromArgb(84, 120, 196);
            this.buttonExtensionCompare.Enabled = false;
            this.buttonExtensionCompare.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonExtensionCompare.ForeColor = System.Drawing.Color.White;
            this.buttonExtensionCompare.Location = new System.Drawing.Point(122, 376);
            this.buttonExtensionCompare.Name = "buttonExtensionCompare";
            this.buttonExtensionCompare.Size = new System.Drawing.Size(214, 30);
            this.buttonExtensionCompare.TabIndex = 24;
            this.buttonExtensionCompare.Text = "Extension";
            this.buttonExtensionCompare.UseVisualStyleBackColor = false;
            this.buttonExtensionCompare.Click += new System.EventHandler(this.buttonExtensionCompare_Click);
            // 
            // timerRefresh
            // 
            this.timerRefresh.Interval = 200;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            // 
            // MeterWheelControlForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(18, 23, 34);
            this.ClientSize = new System.Drawing.Size(368, 428);
            this.Controls.Add(this.buttonExtensionCompare);
            this.Controls.Add(this.buttonSetCmpOutWidth);
            this.Controls.Add(this.numericCmpOutWidth);
            this.Controls.Add(this.labelCmpOutWidthTitle);
            this.Controls.Add(this.checkReverseDirection);
            this.Controls.Add(this.buttonSetMultipleRate);
            this.Controls.Add(this.comboMultipleRate);
            this.Controls.Add(this.labelMultipleRateTitle);
            this.Controls.Add(this.buttonApplyIncrement);
            this.Controls.Add(this.numericIncrement);
            this.Controls.Add(this.labelIncrementTitle);
            this.Controls.Add(this.buttonSetCompare);
            this.Controls.Add(this.numericCompare);
            this.Controls.Add(this.buttonClearCompare);
            this.Controls.Add(this.labelCompareValue);
            this.Controls.Add(this.labelCompareTitle);
            this.Controls.Add(this.buttonSetEncoder);
            this.Controls.Add(this.numericEncoder);
            this.Controls.Add(this.buttonClearEncoder);
            this.Controls.Add(this.labelEncoderValue);
            this.Controls.Add(this.labelEncoderTitle);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonConnect);
            this.Controls.Add(this.labelCard);
            this.Controls.Add(this.comboCardId);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimumSize = new System.Drawing.Size(386, 475);
            this.Name = "MeterWheelControlForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Meter Wheel Control";
            ((System.ComponentModel.ISupportInitialize)(this.numericEncoder)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericCompare)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericIncrement)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericCmpOutWidth)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}
