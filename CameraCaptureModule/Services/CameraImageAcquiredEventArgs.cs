using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace CameraCaptureApp.Services
{
    public sealed class CameraImageAcquiredEventArgs : EventArgs
    {
        // The source bitmap is valid only while the acquisition event is being dispatched.
        private readonly Bitmap image;

        internal CameraImageAcquiredEventArgs(Bitmap image, long sequenceNumber, DateTime timestampUtc)
        {
            this.image = image;
            SequenceNumber = sequenceNumber;
            TimestampUtc = timestampUtc;
        }

        public long SequenceNumber { get; private set; }

        public DateTime TimestampUtc { get; private set; }

        public int Width { get { return image.Width; } }

        public int Height { get { return image.Height; } }

        public PixelFormat PixelFormat { get { return image.PixelFormat; } }

        public Bitmap CreateImageCopy()
        {
            return (Bitmap)image.Clone();
        }
    }
}
