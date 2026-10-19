using System;
using System.Threading;
using System.Windows.Forms;

namespace CameraCaptureApp.Services
{
    internal static class UiExceptionHandler
    {
        public static void Register()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Handle("UI thread exception", e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Handle("AppDomain unhandled exception", e.ExceptionObject as Exception);
        }

        private static void Handle(string title, Exception ex)
        {
            AppLogger.Log(title, ex);

            try
            {
                MessageBox.Show(
                    "An unexpected error occurred.\r\n\r\n" +
                    (ex == null ? title : ex.Message) +
                    "\r\n\r\nLog: " + AppLogger.GetLogPath(),
                    "Application Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
