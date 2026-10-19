using System;
using System.IO;
using System.Text;

namespace CameraCaptureApp.Services
{
    internal static class AppLogger
    {
        private static readonly object SyncRoot = new object();

        public static void Log(string title, Exception ex = null)
        {
            try
            {
                var logPath = GetLogPath();
                var builder = new StringBuilder();
                builder.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + title);

                if (ex != null)
                {
                    builder.AppendLine(ex.ToString());
                }

                builder.AppendLine();

                lock (SyncRoot)
                {
                    File.AppendAllText(logPath, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        public static string GetLogPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var logDirectory = Path.Combine(baseDirectory, "logs", "camera");
            Directory.CreateDirectory(logDirectory);
            return Path.Combine(logDirectory, "app.log");
        }
    }
}
