using System;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Forms;

namespace IntegratedImageProcessingApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
