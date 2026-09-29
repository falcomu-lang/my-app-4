using System;
using System.IO;
using System.Runtime.InteropServices;
using IntegratedImageProcessingApp.Controls;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal static class LargeImageGrayscaleDecoder
    {
        internal static Cv.Mat Decode(LargeImageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            Cv.Mat gray = null;
            try
            {
                gray = Cv.Cv2.ImRead(source.FilePath, Cv.ImreadModes.Grayscale);
                if (gray == null ||
                    gray.Empty() ||
                    gray.Width != source.Width ||
                    gray.Height != source.Height ||
                    gray.Type() != Cv.MatType.CV_8UC1)
                {
                    if (gray != null)
                    {
                        gray.Dispose();
                        gray = null;
                    }

                    // WIC can decode some large TIFF variants that OpenCV rejects.
                    gray = DecodeWithWic(source.FilePath, source.Width, source.Height);
                }

                Cv.Mat result = gray;
                gray = null;
                return result;
            }
            finally
            {
                if (gray != null)
                {
                    gray.Dispose();
                }
            }
        }

        private static Cv.Mat DecodeWithWic(string filePath, int expectedWidth, int expectedHeight)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                System.Windows.Media.Imaging.BitmapSource source = decoder.Frames[0];
                if (source.PixelWidth != expectedWidth || source.PixelHeight != expectedHeight)
                {
                    throw new InvalidOperationException("WIC 解碼後的影像尺寸與來源不一致。");
                }

                var gray = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                    source,
                    System.Windows.Media.PixelFormats.Gray8,
                    null,
                    0);
                int stride = gray.PixelWidth;
                byte[] row = new byte[stride];
                var result = new Cv.Mat(expectedHeight, expectedWidth, Cv.MatType.CV_8UC1);
                try
                {
                    for (int y = 0; y < expectedHeight; y++)
                    {
                        gray.CopyPixels(
                            new System.Windows.Int32Rect(0, y, expectedWidth, 1),
                            row,
                            stride,
                            0);
                        int rowOffset = checked((int)((long)y * result.Step()));
                        Marshal.Copy(row, 0, IntPtr.Add(result.Data, rowOffset), stride);
                    }

                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }
    }
}
