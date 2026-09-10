using System;
using System.Windows.Forms;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            statusLabel.Text = "準備就緒";
        }
    }
}
