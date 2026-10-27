using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CameraCaptureApp.Services
{
    public sealed class CameraImageAcquiredEventArgs : EventArgs
    {
        private readonly CameraGrayscaleFrame frame;

        internal CameraImageAcquiredEventArgs(CameraGrayscaleFrame frame, long sequenceNumber, DateTime timestampUtc)
        {
            this.frame = frame ?? throw new ArgumentNullException("frame");
            SequenceNumber = sequenceNumber;
            TimestampUtc = timestampUtc;
        }

        public long SequenceNumber { get; private set; }

        public DateTime TimestampUtc { get; private set; }

        public int Width { get { return frame.Width; } }

        public int Height { get { return frame.Height; } }

        public PixelFormat PixelFormat
        {
            get { return System.Drawing.Imaging.PixelFormat.Format8bppIndexed; }
        }

        /// <summary>
        /// Captured 8-bit grayscale pixels. The array is owned by this captured
        /// frame and remains valid after the acquisition event returns. Consumers
        /// must treat it as read-only.
        /// </summary>
        public CameraGrayscaleFrame GrayscaleFrame { get { return frame; } }

        public Bitmap CreateImageCopy()
        {
            return frame.CreateBitmapCopy();
        }

    }

    public sealed class CameraGrayscaleFrame
    {
        internal CameraGrayscaleFrame(byte[] pixels, int width, int height, int stride)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException("pixels");
            }
            if (width <= 0 || height <= 0 || stride < width ||
                pixels.LongLength < (long)stride * height)
            {
                throw new ArgumentOutOfRangeException("pixels", "灰階影像尺寸或行距無效。");
            }

            Pixels = pixels;
            Width = width;
            Height = height;
            Stride = stride;
        }

        public byte[] Pixels { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public int Stride { get; private set; }

        public Bitmap CreateBitmapCopy()
        {
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);
            ColorPalette palette = bitmap.Palette;
            for (int index = 0; index < palette.Entries.Length; index++)
            {
                palette.Entries[index] = Color.FromArgb(index, index, index);
            }
            bitmap.Palette = palette;

            BitmapData bitmapData = null;
            bool copySucceeded = false;
            try
            {
                bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, Width, Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format8bppIndexed);
                for (int y = 0; y < Height; y++)
                {
                    Marshal.Copy(
                        Pixels,
                        y * Stride,
                        bitmapData.Scan0 + y * bitmapData.Stride,
                        Width);
                }
                copySucceeded = true;
            }
            finally
            {
                if (bitmapData != null)
                {
                    bitmap.UnlockBits(bitmapData);
                }
                if (!copySucceeded)
                {
                    bitmap.Dispose();
                }
            }

            return bitmap;
        }
    }
}
