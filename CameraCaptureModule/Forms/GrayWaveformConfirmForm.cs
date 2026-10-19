using System;
using System.Windows.Forms;

namespace CameraCaptureApp.Forms
{
    public partial class GrayWaveformConfirmForm : Form
    {
        public GrayWaveformConfirmForm(string summaryText)
        {
            InitializeComponent();
            labelSummary.Text = string.IsNullOrWhiteSpace(summaryText) ? string.Empty : summaryText;
        }

        public bool IsConfirmSelected { get; private set; }

        public bool IsReselectSelected { get; private set; }

        private void buttonConfirm_Click(object sender, EventArgs e)
        {
            IsConfirmSelected = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonReselect_Click(object sender, EventArgs e)
        {
            IsReselectSelected = true;
            DialogResult = DialogResult.Retry;
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
