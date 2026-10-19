using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using CameraCaptureApp.Models;
using SWM = System.Windows.Media;
using SWMI = System.Windows.Media.Imaging;

namespace CameraCaptureApp.Services
{
    public sealed class FrameRecorder : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<Bitmap> _rollingFrames = new List<Bitmap>();
        private Bitmap _latestFrame;

        public void StoreLatest(Bitmap frame)
        {
            if (frame == null)
            {
                return;
            }

            var copy = (Bitmap)frame.Clone();
            lock (_sync)
            {
                if (_latestFrame != null)
                {
                    _latestFrame.Dispose();
                }

                _latestFrame = copy;
            }
        }

        public void StoreRolling(Bitmap frame, int frameCount)
        {
            if (frame == null)
            {
                return;
            }

            frameCount = Math.Max(1, frameCount);
            var copy = (Bitmap)frame.Clone();
            lock (_sync)
            {
                _rollingFrames.Insert(0, copy);
                while (_rollingFrames.Count > frameCount)
                {
                    var lastIndex = _rollingFrames.Count - 1;
                    _rollingFrames[lastIndex].Dispose();
                    _rollingFrames.RemoveAt(lastIndex);
                }
            }
        }

        public void ClearRolling()
        {
            lock (_sync)
            {
                DisposeRollingFramesUnsafe();
            }
        }

        public Bitmap SnapshotLatest()
        {
            lock (_sync)
            {
                return _latestFrame == null ? null : (Bitmap)_latestFrame.Clone();
            }
        }

        public Bitmap SnapshotRolling()
        {
            List<Bitmap> frames;
            lock (_sync)
            {
                if (_rollingFrames.Count == 0)
                {
                    return null;
                }

                frames = new List<Bitmap>(_rollingFrames.Count);
                foreach (var frame in _rollingFrames)
                {
                    frames.Add((Bitmap)frame.Clone());
                }
            }

            try
            {
                return CombineVertical(frames);
            }
            finally
            {
                foreach (var frame in frames)
                {
                    frame.Dispose();
                }
            }
        }

        public RollingFrameSnapshot SnapshotRollingFrames()
        {
            lock (_sync)
            {
                if (_rollingFrames.Count == 0)
                {
                    return null;
                }

                var frames = new List<Bitmap>(_rollingFrames.Count);
                foreach (var frame in _rollingFrames)
                {
                    frames.Add((Bitmap)frame.Clone());
                }

                return new RollingFrameSnapshot(frames);
            }
        }

        public bool SaveRollingPng(string filePath)
        {
            return SaveRollingImage(filePath, ImageSaveFormat.Png, RollingCaptureDirection.TopToBottom, null, null);
        }

        public bool SaveRollingPng(string filePath, RollingCaptureDirection direction)
        {
            return SaveRollingImage(filePath, ImageSaveFormat.Png, direction, null, null);
        }

        public bool SaveRollingPng(string filePath, RollingCaptureDirection direction, Action<int, string> reportProgress)
        {
            return SaveRollingImage(filePath, ImageSaveFormat.Png, direction, reportProgress, null);
        }

        public bool SaveRollingPng(string filePath, RollingCaptureDirection direction, Action<int, string> reportProgress, Action<int> reportRemaining)
        {
            return SaveRollingImage(filePath, ImageSaveFormat.Png, direction, reportProgress, reportRemaining);
        }

        public bool SaveRollingImage(string filePath, ImageSaveFormat saveFormat, RollingCaptureDirection direction, Action<int, string> reportProgress, Action<int> reportRemaining)
        {
            using (var snapshot = SnapshotRollingFrames())
            {
                if (snapshot == null)
                {
                    return false;
                }

                ReportProgress(reportProgress, 5, "Copying current rolling frames...");
                snapshot.SaveImage(filePath, saveFormat, direction, reportProgress, reportRemaining);
                return true;
            }
        }

        public static void SaveBitmapImage(Bitmap bitmap, string filePath, ImageSaveFormat saveFormat, Action<int, string> reportProgress)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException("bitmap");
            }

            ReportProgress(reportProgress, 10, "Preparing image buffer...");
            var width = bitmap.Width;
            var height = bitmap.Height;
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("Image size is invalid.");
            }

            var stride = width;
            var totalBytes = (long)stride * height;
            if (totalBytes > int.MaxValue)
            {
                throw new InvalidOperationException("Image is too large to save as a single file.");
            }

            var pixels = new byte[(int)totalBytes];
            CopyFrameIntoGrayBuffer(bitmap, pixels, stride, 0, false);
            SaveGrayBuffer(filePath, saveFormat, width, height, stride, pixels, reportProgress);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_latestFrame != null)
                {
                    _latestFrame.Dispose();
                    _latestFrame = null;
                }

                DisposeRollingFramesUnsafe();
            }
        }

        private void DisposeRollingFramesUnsafe()
        {
            foreach (var frame in _rollingFrames)
            {
                frame.Dispose();
            }

            _rollingFrames.Clear();
        }

        private static Bitmap CombineVertical(IList<Bitmap> frames)
        {
            var width = 0;
            var height = 0;
            foreach (var frame in frames)
            {
                width = Math.Max(width, frame.Width);
                height += frame.Height;
            }

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var combined = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(combined))
            {
                graphics.Clear(Color.Black);
                var y = 0;
                foreach (var frame in frames)
                {
                    var destination = new Rectangle(0, y + frame.Height, frame.Width, -frame.Height);
                    graphics.DrawImage(frame, destination);
                    y += frame.Height;
                }
            }

            return combined;
        }

        private static void SaveVerticalImage(IList<Bitmap> frames, string filePath, ImageSaveFormat saveFormat, RollingCaptureDirection direction, Action<int, string> reportProgress, Action<int> reportRemaining)
        {
            ReportProgress(reportProgress, 10, "Preparing image buffer...");
            var width = 0;
            var height = 0;
            foreach (var frame in frames)
            {
                width = Math.Max(width, frame.Width);
                checked
                {
                    height += frame.Height;
                }
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            var stride = width;
            var totalBytes = (long)stride * height;
            if (totalBytes > int.MaxValue)
            {
                throw new InvalidOperationException("Rolling image is too large to save as a single file.");
            }

            var pixels = new byte[(int)totalBytes];
            var topToBottomY = 0;
            var bottomToTopY = height;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                int destinationY;
                if (direction == RollingCaptureDirection.BottomToTop)
                {
                    bottomToTopY -= frame.Height;
                    destinationY = bottomToTopY;
                    CopyFrameIntoGrayBuffer(frame, pixels, stride, destinationY, false);
                }
                else
                {
                    destinationY = topToBottomY;
                    topToBottomY += frame.Height;
                    CopyFrameIntoGrayBuffer(frame, pixels, stride, destinationY, true);
                }

                var percent = 10 + (int)Math.Round(((index + 1) / (double)frames.Count) * 75d);
                var remainingCount = frames.Count - index - 1;
                ReportProgress(reportProgress, percent, "Saving image " + (index + 1) + " of " + frames.Count + ". Remaining: " + remainingCount + ".");
            }

            SaveGrayBuffer(filePath, saveFormat, width, height, stride, pixels, reportProgress);
        }

        private static void SaveGrayBuffer(string filePath, ImageSaveFormat saveFormat, int width, int height, int stride, byte[] pixels, Action<int, string> reportProgress)
        {
            ReportProgress(reportProgress, 90, "Encoding image...");
            var bitmapSource = SWMI.BitmapSource.Create(
                width,
                height,
                96,
                96,
                SWM.PixelFormats.Gray8,
                null,
                pixels,
                stride);
            var encoder = CreateBitmapEncoder(saveFormat);
            encoder.Frames.Add(SWMI.BitmapFrame.Create(bitmapSource));

            var tempPath = filePath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024))
            {
                encoder.Save(stream);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempPath, filePath);
            ReportProgress(reportProgress, 100, "Image saved.");
        }

        private static void CopyFrameIntoGrayBuffer(Bitmap frame, byte[] destinationPixels, int destinationStride, int destinationY, bool flipVertically)
        {
            if (frame.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                Copy8BppFrameIntoGrayBuffer(frame, destinationPixels, destinationStride, destinationY, flipVertically);
                return;
            }

            using (var converted = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(converted))
                {
                    graphics.DrawImageUnscaled(frame, 0, 0);
                }

                var sourceRect = new Rectangle(0, 0, converted.Width, converted.Height);
                var data = converted.LockBits(sourceRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var sourceStride = Math.Abs(data.Stride);
                    var sourceRow = new byte[sourceStride];
                    var destinationRowOffset = destinationY * destinationStride;
                    for (var y = 0; y < converted.Height; y++)
                    {
                        var sourceY = flipVertically ? converted.Height - 1 - y : y;
                        Marshal.Copy(data.Scan0 + (sourceY * data.Stride), sourceRow, 0, sourceStride);
                        var targetIndex = destinationRowOffset + (y * destinationStride);
                        for (var x = 0; x < converted.Width; x++)
                        {
                            var sourceIndex = x * 4;
                            destinationPixels[targetIndex + x] = sourceRow[sourceIndex];
                        }
                    }
                }
                finally
                {
                    converted.UnlockBits(data);
                }
            }
        }

        private static SWMI.BitmapEncoder CreateBitmapEncoder(ImageSaveFormat saveFormat)
        {
            if (saveFormat == ImageSaveFormat.UncompressedTif)
            {
                return new SWMI.TiffBitmapEncoder
                {
                    Compression = SWMI.TiffCompressOption.None
                };
            }

            if (saveFormat == ImageSaveFormat.Tif)
            {
                return new SWMI.TiffBitmapEncoder();
            }

            return new SWMI.PngBitmapEncoder();
        }

        private static void Copy8BppFrameIntoGrayBuffer(Bitmap frame, byte[] destinationPixels, int destinationStride, int destinationY, bool flipVertically)
        {
            var sourceRect = new Rectangle(0, 0, frame.Width, frame.Height);
            var data = frame.LockBits(sourceRect, ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
            try
            {
                var sourceStride = Math.Abs(data.Stride);
                var sourceRow = new byte[sourceStride];
                var destinationRowOffset = destinationY * destinationStride;
                for (var y = 0; y < frame.Height; y++)
                {
                    var sourceY = flipVertically ? frame.Height - 1 - y : y;
                    Marshal.Copy(data.Scan0 + (sourceY * data.Stride), sourceRow, 0, sourceStride);
                    Buffer.BlockCopy(sourceRow, 0, destinationPixels, destinationRowOffset + (y * destinationStride), frame.Width);
                }
            }
            finally
            {
                frame.UnlockBits(data);
            }
        }

        private static void ReportProgress(Action<int, string> reportProgress, int percent, string statusText)
        {
            if (reportProgress != null)
            {
                reportProgress(percent, statusText);
            }
        }

        public sealed class RollingFrameSnapshot : IDisposable
        {
            private List<Bitmap> _frames;

            internal RollingFrameSnapshot(List<Bitmap> frames)
            {
                _frames = frames;
            }

            public void SavePng(string filePath, RollingCaptureDirection direction, Action<int, string> reportProgress)
            {
                SaveImage(filePath, ImageSaveFormat.Png, direction, reportProgress, null);
            }

            public void SavePng(string filePath, RollingCaptureDirection direction, Action<int, string> reportProgress, Action<int> reportRemaining)
            {
                SaveImage(filePath, ImageSaveFormat.Png, direction, reportProgress, reportRemaining);
            }

            public void SaveImage(string filePath, ImageSaveFormat saveFormat, RollingCaptureDirection direction, Action<int, string> reportProgress, Action<int> reportRemaining)
            {
                if (_frames == null || _frames.Count == 0)
                {
                    throw new InvalidOperationException("No rolling image is available to save.");
                }

                SaveVerticalImage(_frames, filePath, saveFormat, direction, reportProgress, reportRemaining);
            }

            public void Dispose()
            {
                if (_frames == null)
                {
                    return;
                }

                foreach (var frame in _frames)
                {
                    frame.Dispose();
                }

                _frames = null;
            }
        }
    }
}
