using System;
using System.Drawing;
using IntegratedImageProcessingApp.Controls;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal sealed class LargeRoiGrayscaleCache
    {
        private readonly object syncRoot = new object();
        private LargeImageSource roiGraySource;
        private Rectangle roiGrayBounds;
        private byte[,] roiGray;
        private LargeImageSource fullGraySource;
        private Cv.Mat fullGray;

        internal byte[,] GetOrCreateRoiGray(
            LargeImageSource source,
            Rectangle roi,
            Func<LargeImageSource, Rectangle, byte[,]> create)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (create == null)
            {
                throw new ArgumentNullException("create");
            }

            lock (syncRoot)
            {
                if (ReferenceEquals(roiGraySource, source) &&
                    roiGrayBounds.Equals(roi) &&
                    roiGray != null)
                {
                    return roiGray;
                }
            }

            byte[,] created = create(source, roi);
            lock (syncRoot)
            {
                roiGraySource = source;
                roiGrayBounds = roi;
                roiGray = created;
                return roiGray;
            }
        }

        internal Cv.Mat GetOrCreateOpenCvRoiView(
            LargeImageSource source,
            Rectangle roi,
            Func<Cv.Mat> createFullGray,
            Func<Cv.Mat, Rectangle, Cv.Mat> createRoiView)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (createFullGray == null)
            {
                throw new ArgumentNullException("createFullGray");
            }

            if (createRoiView == null)
            {
                throw new ArgumentNullException("createRoiView");
            }

            lock (syncRoot)
            {
                if (ReferenceEquals(fullGraySource, source) && IsUsable(fullGray))
                {
                    return createRoiView(fullGray, roi);
                }
            }

            Cv.Mat created = null;
            try
            {
                created = createFullGray();
                if (!IsUsable(created))
                {
                    throw new InvalidOperationException("無法建立完整灰階影像快取。");
                }

                lock (syncRoot)
                {
                    if (ReferenceEquals(fullGraySource, source) && IsUsable(fullGray))
                    {
                        created.Dispose();
                        created = null;
                        return createRoiView(fullGray, roi);
                    }

                    if (fullGray != null)
                    {
                        fullGray.Dispose();
                    }

                    fullGray = created;
                    fullGraySource = source;
                    created = null;
                    return createRoiView(fullGray, roi);
                }
            }
            finally
            {
                if (created != null)
                {
                    created.Dispose();
                }
            }
        }

        internal void Clear()
        {
            lock (syncRoot)
            {
                if (fullGray != null)
                {
                    fullGray.Dispose();
                    fullGray = null;
                }

                fullGraySource = null;
                roiGraySource = null;
                roiGrayBounds = Rectangle.Empty;
                roiGray = null;
            }
        }

        private static bool IsUsable(Cv.Mat mat)
        {
            return mat != null && !mat.Empty();
        }
    }
}
