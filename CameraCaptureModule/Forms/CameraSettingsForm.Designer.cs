namespace CameraCaptureApp.Forms
{
    partial class CameraSettingsForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TabControl tabControlSettings;
        private System.Windows.Forms.TabPage tabPageConnection;
        private System.Windows.Forms.TabPage tabPageImage;
        private System.Windows.Forms.TabPage tabPageTrigger;
        private System.Windows.Forms.TabPage tabPageSaving;
        private System.Windows.Forms.TextBox textBoxCameraName;
        private System.Windows.Forms.TextBox textBoxConfigFile;
        private System.Windows.Forms.TextBox textBoxServerName;
        private System.Windows.Forms.TextBox textBoxServerIndex;
        private System.Windows.Forms.TextBox textBoxResourceIndex;
        private System.Windows.Forms.CheckBox checkBoxAutoConnect;
        private System.Windows.Forms.Button buttonBrowseSapera;
        private System.Windows.Forms.Button buttonReadCcfToFields;
        private System.Windows.Forms.Button buttonLoadDeviceFeatures;
        private System.Windows.Forms.Button buttonProbeLiveFeatures;
        private System.Windows.Forms.Button buttonProbeAcquisitionParameters;
        private System.Windows.Forms.Label labelReadResult;
        private System.Windows.Forms.Label labelCameraName;
        private System.Windows.Forms.Label labelCcfFile;
        private System.Windows.Forms.Label labelAcquisitionServer;
        private System.Windows.Forms.Label labelServerIndex;
        private System.Windows.Forms.Label labelResourceIndex;
        private System.Windows.Forms.Label labelExposure;
        private System.Windows.Forms.Label labelGain;
        private System.Windows.Forms.Label labelLength;
        private System.Windows.Forms.Label labelInternalLineRate;
        private System.Windows.Forms.Label labelImageNote;
        private System.Windows.Forms.GroupBox groupBoxRollingCapture;
        private System.Windows.Forms.CheckBox checkBoxRollingCaptureEnabled;
        private System.Windows.Forms.Label labelRollingCaptureFrameCount;
        private System.Windows.Forms.NumericUpDown numericRollingCaptureFrameCount;
        private System.Windows.Forms.Label labelRollingCaptureDirection;
        private System.Windows.Forms.ComboBox comboBoxRollingCaptureDirection;
        private System.Windows.Forms.Label labelTriggerMode;
        private System.Windows.Forms.Label labelTriggerNote;
        private System.Windows.Forms.CheckBox checkBoxExternalFrameTriggerOneFrame;
        private System.Windows.Forms.CheckBox checkBoxExternalFrameTriggerOneFrameCompareFromEncoder;
        private System.Windows.Forms.CheckBox checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger;
        private System.Windows.Forms.Label labelImageSaveFormat;
        private System.Windows.Forms.CheckBox checkBoxAutoSaveOnExternalTriggerOneFrame;
        private System.Windows.Forms.CheckBox checkBoxAutoSaveOnSoftwareTriggerFrame;
        private System.Windows.Forms.NumericUpDown numericExposure;
        private System.Windows.Forms.NumericUpDown numericGain;
        private System.Windows.Forms.NumericUpDown numericLength;
        private System.Windows.Forms.NumericUpDown numericInternalLineRate;
        private System.Windows.Forms.ComboBox comboBoxTriggerMode;
        private System.Windows.Forms.ComboBox comboBoxImageSaveFormat;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.Button buttonOk;
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
            this.tabControlSettings = new System.Windows.Forms.TabControl();
            this.tabPageConnection = new System.Windows.Forms.TabPage();
            this.labelReadResult = new System.Windows.Forms.Label();
            this.buttonProbeLiveFeatures = new System.Windows.Forms.Button();
            this.buttonProbeAcquisitionParameters = new System.Windows.Forms.Button();
            this.buttonLoadDeviceFeatures = new System.Windows.Forms.Button();
            this.buttonReadCcfToFields = new System.Windows.Forms.Button();
            this.buttonBrowseSapera = new System.Windows.Forms.Button();
            this.checkBoxAutoConnect = new System.Windows.Forms.CheckBox();
            this.textBoxResourceIndex = new System.Windows.Forms.TextBox();
            this.textBoxServerIndex = new System.Windows.Forms.TextBox();
            this.textBoxServerName = new System.Windows.Forms.TextBox();
            this.textBoxConfigFile = new System.Windows.Forms.TextBox();
            this.textBoxCameraName = new System.Windows.Forms.TextBox();
            this.labelCameraName = new System.Windows.Forms.Label();
            this.labelCcfFile = new System.Windows.Forms.Label();
            this.labelAcquisitionServer = new System.Windows.Forms.Label();
            this.labelServerIndex = new System.Windows.Forms.Label();
            this.labelResourceIndex = new System.Windows.Forms.Label();
            this.tabPageImage = new System.Windows.Forms.TabPage();
            this.groupBoxRollingCapture = new System.Windows.Forms.GroupBox();
            this.checkBoxRollingCaptureEnabled = new System.Windows.Forms.CheckBox();
            this.labelRollingCaptureFrameCount = new System.Windows.Forms.Label();
            this.numericRollingCaptureFrameCount = new System.Windows.Forms.NumericUpDown();
            this.labelRollingCaptureDirection = new System.Windows.Forms.Label();
            this.comboBoxRollingCaptureDirection = new System.Windows.Forms.ComboBox();
            this.labelExposure = new System.Windows.Forms.Label();
            this.labelGain = new System.Windows.Forms.Label();
            this.labelLength = new System.Windows.Forms.Label();
            this.labelInternalLineRate = new System.Windows.Forms.Label();
            this.labelImageNote = new System.Windows.Forms.Label();
            this.numericInternalLineRate = new System.Windows.Forms.NumericUpDown();
            this.numericLength = new System.Windows.Forms.NumericUpDown();
            this.numericGain = new System.Windows.Forms.NumericUpDown();
            this.numericExposure = new System.Windows.Forms.NumericUpDown();
            this.tabPageTrigger = new System.Windows.Forms.TabPage();
            this.labelTriggerMode = new System.Windows.Forms.Label();
            this.labelTriggerNote = new System.Windows.Forms.Label();
            this.checkBoxExternalFrameTriggerOneFrame = new System.Windows.Forms.CheckBox();
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder = new System.Windows.Forms.CheckBox();
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger = new System.Windows.Forms.CheckBox();
            this.comboBoxTriggerMode = new System.Windows.Forms.ComboBox();
            this.tabPageSaving = new System.Windows.Forms.TabPage();
            this.labelImageSaveFormat = new System.Windows.Forms.Label();
            this.checkBoxAutoSaveOnExternalTriggerOneFrame = new System.Windows.Forms.CheckBox();
            this.checkBoxAutoSaveOnSoftwareTriggerFrame = new System.Windows.Forms.CheckBox();
            this.comboBoxImageSaveFormat = new System.Windows.Forms.ComboBox();
            this.buttonApply = new System.Windows.Forms.Button();
            this.buttonOk = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.tabControlSettings.SuspendLayout();
            this.tabPageConnection.SuspendLayout();
            this.tabPageImage.SuspendLayout();
            this.groupBoxRollingCapture.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericRollingCaptureFrameCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericInternalLineRate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericGain)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericExposure)).BeginInit();
            this.tabPageTrigger.SuspendLayout();
            this.tabPageSaving.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControlSettings
            // 
            this.tabControlSettings.Controls.Add(this.tabPageConnection);
            this.tabControlSettings.Controls.Add(this.tabPageImage);
            this.tabControlSettings.Controls.Add(this.tabPageTrigger);
            this.tabControlSettings.Controls.Add(this.tabPageSaving);
            this.tabControlSettings.Location = new System.Drawing.Point(12, 12);
            this.tabControlSettings.Name = "tabControlSettings";
            this.tabControlSettings.SelectedIndex = 0;
            this.tabControlSettings.Size = new System.Drawing.Size(760, 380);
            this.tabControlSettings.TabIndex = 0;
            // 
            // tabPageConnection
            // 
            this.tabPageConnection.Controls.Add(this.labelCameraName);
            this.tabPageConnection.Controls.Add(this.textBoxCameraName);
            this.tabPageConnection.Controls.Add(this.labelCcfFile);
            this.tabPageConnection.Controls.Add(this.textBoxConfigFile);
            this.tabPageConnection.Controls.Add(this.labelAcquisitionServer);
            this.tabPageConnection.Controls.Add(this.textBoxServerName);
            this.tabPageConnection.Controls.Add(this.labelServerIndex);
            this.tabPageConnection.Controls.Add(this.textBoxServerIndex);
            this.tabPageConnection.Controls.Add(this.labelResourceIndex);
            this.tabPageConnection.Controls.Add(this.textBoxResourceIndex);
            this.tabPageConnection.Controls.Add(this.checkBoxAutoConnect);
            this.tabPageConnection.Controls.Add(this.buttonBrowseSapera);
            this.tabPageConnection.Controls.Add(this.buttonReadCcfToFields);
            this.tabPageConnection.Controls.Add(this.buttonLoadDeviceFeatures);
            this.tabPageConnection.Controls.Add(this.buttonProbeAcquisitionParameters);
            this.tabPageConnection.Controls.Add(this.buttonProbeLiveFeatures);
            this.tabPageConnection.Controls.Add(this.labelReadResult);
            this.tabPageConnection.Location = new System.Drawing.Point(4, 26);
            this.tabPageConnection.Name = "tabPageConnection";
            this.tabPageConnection.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageConnection.Size = new System.Drawing.Size(752, 350);
            this.tabPageConnection.TabIndex = 0;
            this.tabPageConnection.Text = "Basic Connection";
            this.tabPageConnection.UseVisualStyleBackColor = true;
            // 
            // textBoxCameraName
            // 
            this.textBoxCameraName.Location = new System.Drawing.Point(35, 47);
            this.textBoxCameraName.Name = "textBoxCameraName";
            this.textBoxCameraName.Size = new System.Drawing.Size(280, 23);
            this.textBoxCameraName.TabIndex = 0;
            // 
            // labelCameraName
            // 
            this.labelCameraName.AutoSize = true;
            this.labelCameraName.Location = new System.Drawing.Point(32, 25);
            this.labelCameraName.Name = "labelCameraName";
            this.labelCameraName.Size = new System.Drawing.Size(98, 16);
            this.labelCameraName.TabIndex = 11;
            this.labelCameraName.Text = "Camera Name";
            // 
            // labelCcfFile
            // 
            this.labelCcfFile.AutoSize = true;
            this.labelCcfFile.Location = new System.Drawing.Point(32, 78);
            this.labelCcfFile.Name = "labelCcfFile";
            this.labelCcfFile.Size = new System.Drawing.Size(57, 16);
            this.labelCcfFile.TabIndex = 12;
            this.labelCcfFile.Text = "CCF File";
            // 
            // labelAcquisitionServer
            // 
            this.labelAcquisitionServer.AutoSize = true;
            this.labelAcquisitionServer.Location = new System.Drawing.Point(32, 164);
            this.labelAcquisitionServer.Name = "labelAcquisitionServer";
            this.labelAcquisitionServer.Size = new System.Drawing.Size(122, 16);
            this.labelAcquisitionServer.TabIndex = 13;
            this.labelAcquisitionServer.Text = "Acquisition Server";
            // 
            // labelServerIndex
            // 
            this.labelServerIndex.AutoSize = true;
            this.labelServerIndex.Location = new System.Drawing.Point(32, 217);
            this.labelServerIndex.Name = "labelServerIndex";
            this.labelServerIndex.Size = new System.Drawing.Size(85, 16);
            this.labelServerIndex.TabIndex = 14;
            this.labelServerIndex.Text = "Server Index";
            // 
            // labelResourceIndex
            // 
            this.labelResourceIndex.AutoSize = true;
            this.labelResourceIndex.Location = new System.Drawing.Point(196, 217);
            this.labelResourceIndex.Name = "labelResourceIndex";
            this.labelResourceIndex.Size = new System.Drawing.Size(105, 16);
            this.labelResourceIndex.TabIndex = 15;
            this.labelResourceIndex.Text = "Resource Index";
            // 
            // textBoxConfigFile
            // 
            this.textBoxConfigFile.Location = new System.Drawing.Point(35, 100);
            this.textBoxConfigFile.Multiline = true;
            this.textBoxConfigFile.Name = "textBoxConfigFile";
            this.textBoxConfigFile.Size = new System.Drawing.Size(680, 52);
            this.textBoxConfigFile.TabIndex = 1;
            // 
            // textBoxServerName
            // 
            this.textBoxServerName.Location = new System.Drawing.Point(35, 186);
            this.textBoxServerName.Name = "textBoxServerName";
            this.textBoxServerName.ReadOnly = true;
            this.textBoxServerName.Size = new System.Drawing.Size(420, 23);
            this.textBoxServerName.TabIndex = 2;
            // 
            // textBoxServerIndex
            // 
            this.textBoxServerIndex.Location = new System.Drawing.Point(35, 239);
            this.textBoxServerIndex.Name = "textBoxServerIndex";
            this.textBoxServerIndex.ReadOnly = true;
            this.textBoxServerIndex.Size = new System.Drawing.Size(120, 23);
            this.textBoxServerIndex.TabIndex = 3;
            // 
            // textBoxResourceIndex
            // 
            this.textBoxResourceIndex.Location = new System.Drawing.Point(199, 239);
            this.textBoxResourceIndex.Name = "textBoxResourceIndex";
            this.textBoxResourceIndex.ReadOnly = true;
            this.textBoxResourceIndex.Size = new System.Drawing.Size(120, 23);
            this.textBoxResourceIndex.TabIndex = 4;
            // 
            // checkBoxAutoConnect
            // 
            this.checkBoxAutoConnect.AutoSize = true;
            this.checkBoxAutoConnect.Location = new System.Drawing.Point(35, 276);
            this.checkBoxAutoConnect.Name = "checkBoxAutoConnect";
            this.checkBoxAutoConnect.Size = new System.Drawing.Size(143, 20);
            this.checkBoxAutoConnect.TabIndex = 5;
            this.checkBoxAutoConnect.Text = "Auto Connect at Start";
            this.checkBoxAutoConnect.UseVisualStyleBackColor = true;
            // 
            // buttonBrowseSapera
            // 
            this.buttonBrowseSapera.Location = new System.Drawing.Point(430, 235);
            this.buttonBrowseSapera.Name = "buttonBrowseSapera";
            this.buttonBrowseSapera.Size = new System.Drawing.Size(138, 32);
            this.buttonBrowseSapera.TabIndex = 8;
            this.buttonBrowseSapera.Text = "Load From Sapera";
            this.buttonBrowseSapera.UseVisualStyleBackColor = true;
            this.buttonBrowseSapera.Click += new System.EventHandler(this.buttonBrowseSapera_Click);
            // 
            // buttonReadCcfToFields
            // 
            this.buttonReadCcfToFields.Location = new System.Drawing.Point(577, 235);
            this.buttonReadCcfToFields.Name = "buttonReadCcfToFields";
            this.buttonReadCcfToFields.Size = new System.Drawing.Size(138, 32);
            this.buttonReadCcfToFields.TabIndex = 10;
            this.buttonReadCcfToFields.Text = "Read CCF To Fields";
            this.buttonReadCcfToFields.UseVisualStyleBackColor = true;
            this.buttonReadCcfToFields.Click += new System.EventHandler(this.buttonReadCcfToFields_Click);
            // 
            // buttonLoadDeviceFeatures
            // 
            this.buttonLoadDeviceFeatures.Location = new System.Drawing.Point(283, 276);
            this.buttonLoadDeviceFeatures.Name = "buttonLoadDeviceFeatures";
            this.buttonLoadDeviceFeatures.Size = new System.Drawing.Size(138, 28);
            this.buttonLoadDeviceFeatures.TabIndex = 16;
            this.buttonLoadDeviceFeatures.Text = "Load Features";
            this.buttonLoadDeviceFeatures.UseVisualStyleBackColor = true;
            this.buttonLoadDeviceFeatures.Click += new System.EventHandler(this.buttonLoadDeviceFeatures_Click);
            // 
            // buttonProbeLiveFeatures
            // 
            this.buttonProbeLiveFeatures.Location = new System.Drawing.Point(577, 276);
            this.buttonProbeLiveFeatures.Name = "buttonProbeLiveFeatures";
            this.buttonProbeLiveFeatures.Size = new System.Drawing.Size(138, 28);
            this.buttonProbeLiveFeatures.TabIndex = 8;
            this.buttonProbeLiveFeatures.Text = "Live Features";
            this.buttonProbeLiveFeatures.UseVisualStyleBackColor = true;
            this.buttonProbeLiveFeatures.Click += new System.EventHandler(this.buttonProbeLiveFeatures_Click);
            // 
            // buttonProbeAcquisitionParameters
            // 
            this.buttonProbeAcquisitionParameters.Location = new System.Drawing.Point(430, 276);
            this.buttonProbeAcquisitionParameters.Name = "buttonProbeAcquisitionParameters";
            this.buttonProbeAcquisitionParameters.Size = new System.Drawing.Size(138, 28);
            this.buttonProbeAcquisitionParameters.TabIndex = 8;
            this.buttonProbeAcquisitionParameters.Text = "Acq Params";
            this.buttonProbeAcquisitionParameters.UseVisualStyleBackColor = true;
            this.buttonProbeAcquisitionParameters.Click += new System.EventHandler(this.buttonProbeAcquisitionParameters_Click);
            // 
            // labelReadResult
            // 
            this.labelReadResult.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelReadResult.AutoEllipsis = true;
            this.labelReadResult.Location = new System.Drawing.Point(35, 310);
            this.labelReadResult.Name = "labelReadResult";
            this.labelReadResult.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.labelReadResult.Size = new System.Drawing.Size(680, 30);
            this.labelReadResult.TabIndex = 10;
            this.labelReadResult.Text = "Load Sapera settings first, then read supported CCF values into the fields.";
            // 
            // tabPageImage
            // 
            this.tabPageImage.Controls.Add(this.groupBoxRollingCapture);
            this.tabPageImage.Controls.Add(this.labelExposure);
            this.tabPageImage.Controls.Add(this.numericExposure);
            this.tabPageImage.Controls.Add(this.labelGain);
            this.tabPageImage.Controls.Add(this.numericGain);
            this.tabPageImage.Controls.Add(this.labelLength);
            this.tabPageImage.Controls.Add(this.numericLength);
            this.tabPageImage.Controls.Add(this.labelInternalLineRate);
            this.tabPageImage.Controls.Add(this.numericInternalLineRate);
            this.tabPageImage.Controls.Add(this.labelImageNote);
            this.tabPageImage.Location = new System.Drawing.Point(4, 26);
            this.tabPageImage.Name = "tabPageImage";
            this.tabPageImage.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageImage.Size = new System.Drawing.Size(752, 350);
            this.tabPageImage.TabIndex = 1;
            this.tabPageImage.Text = "Image";
            this.tabPageImage.UseVisualStyleBackColor = true;
            // 
            // groupBoxRollingCapture
            // 
            this.groupBoxRollingCapture.Controls.Add(this.numericRollingCaptureFrameCount);
            this.groupBoxRollingCapture.Controls.Add(this.labelRollingCaptureFrameCount);
            this.groupBoxRollingCapture.Controls.Add(this.checkBoxRollingCaptureEnabled);
            this.groupBoxRollingCapture.Controls.Add(this.labelRollingCaptureDirection);
            this.groupBoxRollingCapture.Controls.Add(this.comboBoxRollingCaptureDirection);
            this.groupBoxRollingCapture.Location = new System.Drawing.Point(35, 143);
            this.groupBoxRollingCapture.Name = "groupBoxRollingCapture";
            this.groupBoxRollingCapture.Size = new System.Drawing.Size(680, 150);
            this.groupBoxRollingCapture.TabIndex = 10;
            this.groupBoxRollingCapture.TabStop = false;
            this.groupBoxRollingCapture.Text = "滾動式拍照";
            // 
            // checkBoxRollingCaptureEnabled
            // 
            this.checkBoxRollingCaptureEnabled.AutoSize = true;
            this.checkBoxRollingCaptureEnabled.Location = new System.Drawing.Point(18, 31);
            this.checkBoxRollingCaptureEnabled.Name = "checkBoxRollingCaptureEnabled";
            this.checkBoxRollingCaptureEnabled.Size = new System.Drawing.Size(91, 20);
            this.checkBoxRollingCaptureEnabled.TabIndex = 0;
            this.checkBoxRollingCaptureEnabled.Text = "啟用";
            this.checkBoxRollingCaptureEnabled.UseVisualStyleBackColor = true;
            // 
            // labelRollingCaptureFrameCount
            // 
            this.labelRollingCaptureFrameCount.AutoSize = true;
            this.labelRollingCaptureFrameCount.Location = new System.Drawing.Point(18, 68);
            this.labelRollingCaptureFrameCount.Name = "labelRollingCaptureFrameCount";
            this.labelRollingCaptureFrameCount.Size = new System.Drawing.Size(104, 16);
            this.labelRollingCaptureFrameCount.TabIndex = 1;
            this.labelRollingCaptureFrameCount.Text = "持續張數";
            // 
            // numericRollingCaptureFrameCount
            // 
            this.numericRollingCaptureFrameCount.Location = new System.Drawing.Point(132, 66);
            this.numericRollingCaptureFrameCount.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numericRollingCaptureFrameCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericRollingCaptureFrameCount.Name = "numericRollingCaptureFrameCount";
            this.numericRollingCaptureFrameCount.Size = new System.Drawing.Size(120, 23);
            this.numericRollingCaptureFrameCount.TabIndex = 2;
            this.numericRollingCaptureFrameCount.Value = new decimal(new int[] { 12, 0, 0, 0 });
            // 
            // labelRollingCaptureDirection
            // 
            this.labelRollingCaptureDirection.AutoSize = true;
            this.labelRollingCaptureDirection.Location = new System.Drawing.Point(18, 105);
            this.labelRollingCaptureDirection.Name = "labelRollingCaptureDirection";
            this.labelRollingCaptureDirection.Size = new System.Drawing.Size(104, 16);
            this.labelRollingCaptureDirection.TabIndex = 3;
            this.labelRollingCaptureDirection.Text = "取像方向";
            // 
            // comboBoxRollingCaptureDirection
            // 
            this.comboBoxRollingCaptureDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRollingCaptureDirection.FormattingEnabled = true;
            this.comboBoxRollingCaptureDirection.Location = new System.Drawing.Point(132, 102);
            this.comboBoxRollingCaptureDirection.Name = "comboBoxRollingCaptureDirection";
            this.comboBoxRollingCaptureDirection.Size = new System.Drawing.Size(160, 24);
            this.comboBoxRollingCaptureDirection.TabIndex = 4;
            // 
            // labelExposure
            // 
            this.labelExposure.AutoSize = true;
            this.labelExposure.Location = new System.Drawing.Point(32, 35);
            this.labelExposure.Name = "labelExposure";
            this.labelExposure.Size = new System.Drawing.Size(63, 16);
            this.labelExposure.TabIndex = 5;
            this.labelExposure.Text = "Exposure";
            // 
            // labelGain
            // 
            this.labelGain.AutoSize = true;
            this.labelGain.Location = new System.Drawing.Point(208, 35);
            this.labelGain.Name = "labelGain";
            this.labelGain.Size = new System.Drawing.Size(36, 16);
            this.labelGain.TabIndex = 6;
            this.labelGain.Text = "Gain";
            // 
            // labelLength
            // 
            this.labelLength.AutoSize = true;
            this.labelLength.Location = new System.Drawing.Point(384, 35);
            this.labelLength.Name = "labelLength";
            this.labelLength.Size = new System.Drawing.Size(93, 16);
            this.labelLength.TabIndex = 7;
            this.labelLength.Text = "Length (Lines)";
            // 
            // labelInternalLineRate
            // 
            this.labelInternalLineRate.AutoSize = true;
            this.labelInternalLineRate.Location = new System.Drawing.Point(560, 35);
            this.labelInternalLineRate.Name = "labelInternalLineRate";
            this.labelInternalLineRate.Size = new System.Drawing.Size(113, 16);
            this.labelInternalLineRate.TabIndex = 8;
            this.labelInternalLineRate.Text = "Internal Line Rate";
            // 
            // labelImageNote
            // 
            this.labelImageNote.AutoSize = true;
            this.labelImageNote.Location = new System.Drawing.Point(32, 103);
            this.labelImageNote.Name = "labelImageNote";
            this.labelImageNote.Size = new System.Drawing.Size(652, 16);
            this.labelImageNote.TabIndex = 9;
            this.labelImageNote.Text = "Exposure, gain, length, and internal line rate will be written to supported Sapera parameters when available.";
            // 
            // image controls
            // 
            this.numericExposure.DecimalPlaces = 2;
            this.numericExposure.Location = new System.Drawing.Point(35, 57);
            this.numericExposure.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this.numericExposure.Size = new System.Drawing.Size(140, 23);
            this.numericGain.DecimalPlaces = 2;
            this.numericGain.Location = new System.Drawing.Point(211, 57);
            this.numericGain.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numericGain.Size = new System.Drawing.Size(140, 23);
            this.numericLength.Location = new System.Drawing.Point(387, 57);
            this.numericLength.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            this.numericLength.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericLength.Size = new System.Drawing.Size(140, 23);
            this.numericLength.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericInternalLineRate.DecimalPlaces = 2;
            this.numericInternalLineRate.Location = new System.Drawing.Point(563, 57);
            this.numericInternalLineRate.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            this.numericInternalLineRate.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericInternalLineRate.Size = new System.Drawing.Size(140, 23);
            this.numericInternalLineRate.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // tabPageTrigger
            // 
            this.tabPageTrigger.Controls.Add(this.labelTriggerMode);
            this.tabPageTrigger.Controls.Add(this.comboBoxTriggerMode);
            this.tabPageTrigger.Controls.Add(this.checkBoxExternalFrameTriggerOneFrame);
            this.tabPageTrigger.Controls.Add(this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder);
            this.tabPageTrigger.Controls.Add(this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger);
            this.tabPageTrigger.Controls.Add(this.labelTriggerNote);
            this.tabPageTrigger.Location = new System.Drawing.Point(4, 26);
            this.tabPageTrigger.Name = "tabPageTrigger";
            this.tabPageTrigger.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageTrigger.Size = new System.Drawing.Size(752, 350);
            this.tabPageTrigger.TabIndex = 2;
            this.tabPageTrigger.Text = "Trigger";
            this.tabPageTrigger.UseVisualStyleBackColor = true;
            // 
            // labelTriggerMode
            // 
            this.labelTriggerMode.AutoSize = true;
            this.labelTriggerMode.Location = new System.Drawing.Point(32, 35);
            this.labelTriggerMode.Name = "labelTriggerMode";
            this.labelTriggerMode.Size = new System.Drawing.Size(91, 16);
            this.labelTriggerMode.TabIndex = 2;
            this.labelTriggerMode.Text = "Trigger Mode";
            // 
            // labelTriggerNote
            // 
            this.labelTriggerNote.AutoSize = true;
            this.labelTriggerNote.Location = new System.Drawing.Point(32, 130);
            this.labelTriggerNote.Name = "labelTriggerNote";
            this.labelTriggerNote.Size = new System.Drawing.Size(584, 16);
            this.labelTriggerNote.TabIndex = 3;
            this.labelTriggerNote.Text = "Trigger Mode will be written to supported Sapera trigger features when the camera provides them.";
            // 
            // comboBoxTriggerMode
            // 
            this.comboBoxTriggerMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTriggerMode.FormattingEnabled = true;
            this.comboBoxTriggerMode.Location = new System.Drawing.Point(35, 57);
            this.comboBoxTriggerMode.Name = "comboBoxTriggerMode";
            this.comboBoxTriggerMode.Size = new System.Drawing.Size(220, 24);
            this.comboBoxTriggerMode.TabIndex = 0;
            this.comboBoxTriggerMode.SelectedIndexChanged += new System.EventHandler(this.comboBoxTriggerMode_SelectedIndexChanged);
            // 
            // checkBoxExternalFrameTriggerOneFrame
            // 
            this.checkBoxExternalFrameTriggerOneFrame.AutoSize = true;
            this.checkBoxExternalFrameTriggerOneFrame.Location = new System.Drawing.Point(35, 90);
            this.checkBoxExternalFrameTriggerOneFrame.Name = "checkBoxExternalFrameTriggerOneFrame";
            this.checkBoxExternalFrameTriggerOneFrame.Size = new System.Drawing.Size(187, 20);
            this.checkBoxExternalFrameTriggerOneFrame.TabIndex = 5;
            this.checkBoxExternalFrameTriggerOneFrame.Text = "External Trigger One Frame";
            this.checkBoxExternalFrameTriggerOneFrame.UseVisualStyleBackColor = true;
            this.checkBoxExternalFrameTriggerOneFrame.CheckedChanged += new System.EventHandler(this.checkBoxExternalFrameTriggerOneFrame_CheckedChanged);
            // 
            // checkBoxExternalFrameTriggerOneFrameCompareFromEncoder
            // 
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.AutoSize = true;
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Location = new System.Drawing.Point(54, 142);
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Name = "checkBoxExternalFrameTriggerOneFrameCompareFromEncoder";
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Size = new System.Drawing.Size(332, 20);
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.TabIndex = 6;
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.Text = "Compare Set follows current encoder value";
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.UseVisualStyleBackColor = true;
            this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder.CheckedChanged += new System.EventHandler(this.checkBoxExternalFrameTriggerOneFrameCompareFromEncoder_CheckedChanged);
            // 
            // checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger
            // 
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.AutoSize = true;
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Location = new System.Drawing.Point(73, 170);
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Name = "checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger";
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Size = new System.Drawing.Size(320, 20);
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.TabIndex = 7;
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.Text = "Also apply Encoder Set on external trigger";
            this.checkBoxExternalFrameTriggerOneFrameSetEncoderOnTrigger.UseVisualStyleBackColor = true;
            this.labelTriggerNote.Location = new System.Drawing.Point(32, 210);
            // 
            // tabPageSaving
            // 
            this.tabPageSaving.Controls.Add(this.labelImageSaveFormat);
            this.tabPageSaving.Controls.Add(this.checkBoxAutoSaveOnExternalTriggerOneFrame);
            this.tabPageSaving.Controls.Add(this.checkBoxAutoSaveOnSoftwareTriggerFrame);
            this.tabPageSaving.Controls.Add(this.comboBoxImageSaveFormat);
            this.tabPageSaving.Location = new System.Drawing.Point(4, 26);
            this.tabPageSaving.Name = "tabPageSaving";
            this.tabPageSaving.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSaving.Size = new System.Drawing.Size(752, 350);
            this.tabPageSaving.TabIndex = 3;
            this.tabPageSaving.Text = "Saving";
            this.tabPageSaving.UseVisualStyleBackColor = true;
            // 
            // labelImageSaveFormat
            // 
            this.labelImageSaveFormat.AutoSize = true;
            this.labelImageSaveFormat.Location = new System.Drawing.Point(32, 35);
            this.labelImageSaveFormat.Name = "labelImageSaveFormat";
            this.labelImageSaveFormat.Size = new System.Drawing.Size(104, 16);
            this.labelImageSaveFormat.TabIndex = 4;
            this.labelImageSaveFormat.Text = "圖片保存格式";
            // 
            // comboBoxImageSaveFormat
            // 
            this.comboBoxImageSaveFormat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxImageSaveFormat.FormattingEnabled = true;
            this.comboBoxImageSaveFormat.Location = new System.Drawing.Point(35, 57);
            this.comboBoxImageSaveFormat.Name = "comboBoxImageSaveFormat";
            this.comboBoxImageSaveFormat.Size = new System.Drawing.Size(180, 24);
            this.comboBoxImageSaveFormat.TabIndex = 5;
            // 
            // checkBoxAutoSaveOnExternalTriggerOneFrame
            // 
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.AutoSize = true;
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.Location = new System.Drawing.Point(35, 96);
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.Name = "checkBoxAutoSaveOnExternalTriggerOneFrame";
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.Size = new System.Drawing.Size(333, 20);
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.TabIndex = 6;
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.Text = "Auto save snapshot after external trigger frame";
            this.checkBoxAutoSaveOnExternalTriggerOneFrame.UseVisualStyleBackColor = true;
            // 
            // checkBoxAutoSaveOnSoftwareTriggerFrame
            // 
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.AutoSize = true;
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.Location = new System.Drawing.Point(35, 126);
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.Name = "checkBoxAutoSaveOnSoftwareTriggerFrame";
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.Size = new System.Drawing.Size(338, 20);
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.TabIndex = 7;
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.Text = "Auto save snapshot after software trigger frame";
            this.checkBoxAutoSaveOnSoftwareTriggerFrame.UseVisualStyleBackColor = true;
            // 
            // buttons
            // 
            this.buttonApply.Location = new System.Drawing.Point(446, 406);
            this.buttonApply.Size = new System.Drawing.Size(96, 34);
            this.buttonApply.Text = "Apply";
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            this.buttonOk.Location = new System.Drawing.Point(548, 406);
            this.buttonOk.Size = new System.Drawing.Size(108, 34);
            this.buttonOk.Text = "OK";
            this.buttonOk.UseVisualStyleBackColor = true;
            this.buttonOk.Click += new System.EventHandler(this.buttonOk_Click);
            this.buttonCancel.Location = new System.Drawing.Point(664, 406);
            this.buttonCancel.Size = new System.Drawing.Size(108, 34);
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // CameraSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 452);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.buttonApply);
            this.Controls.Add(this.tabControlSettings);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CameraSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Camera Settings";
            this.tabControlSettings.ResumeLayout(false);
            this.tabPageConnection.ResumeLayout(false);
            this.tabPageConnection.PerformLayout();
            this.tabPageImage.ResumeLayout(false);
            this.tabPageImage.PerformLayout();
            this.groupBoxRollingCapture.ResumeLayout(false);
            this.groupBoxRollingCapture.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericRollingCaptureFrameCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericInternalLineRate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericGain)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericExposure)).EndInit();
            this.tabPageTrigger.ResumeLayout(false);
            this.tabPageTrigger.PerformLayout();
            this.tabPageSaving.ResumeLayout(false);
            this.tabPageSaving.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
