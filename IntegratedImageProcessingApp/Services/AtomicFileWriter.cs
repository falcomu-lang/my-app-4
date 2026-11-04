using System;
using System.IO;
using System.Text;

namespace IntegratedImageProcessingApp.Services
{
    internal static class AtomicFileWriter
    {
        public static void Write(string filePath, Action<TextWriter> writeContent)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A destination path is required.", "filePath");
            }
            if (writeContent == null)
            {
                throw new ArgumentNullException("writeContent");
            }

            string fullPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = Path.Combine(
                directory ?? string.Empty,
                Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writeContent(writer);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, null);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }
}
