using System;
using System.Collections.Generic;
using System.Drawing;

namespace IntegratedImageProcessingApp.Services
{
    internal static class LargeRoiMaskBuilder
    {
        internal static bool[,] BuildTiledMask(
            Rectangle roi,
            int imageWidth,
            int imageHeight,
            int chunkSize,
            string method,
            Dictionary<string, string> parameters,
            Func<Rectangle, Bitmap> createChunkImage,
            Func<Bitmap, bool[,]> processChunk,
            Func<bool> isCurrent,
            Action<bool[,], int, int, bool> reportProgress)
        {
            if (roi.Width <= 0 || roi.Height <= 0)
            {
                throw new ArgumentOutOfRangeException("roi");
            }

            if (imageWidth <= 0 || imageHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("imageWidth");
            }

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException("chunkSize");
            }

            if (createChunkImage == null)
            {
                throw new ArgumentNullException("createChunkImage");
            }

            if (processChunk == null)
            {
                throw new ArgumentNullException("processChunk");
            }

            if (isCurrent == null)
            {
                throw new ArgumentNullException("isCurrent");
            }

            bool[,] mask = new bool[roi.Width, roi.Height];
            int padding = GetChunkPadding(method, parameters);
            int chunkCountX = (roi.Width + chunkSize - 1) / chunkSize;
            int chunkCountY = (roi.Height + chunkSize - 1) / chunkSize;
            int totalChunks = chunkCountX * chunkCountY;
            int completedChunks = 0;

            for (int chunkY = 0; chunkY < chunkCountY; chunkY++)
            {
                for (int chunkX = 0; chunkX < chunkCountX; chunkX++)
                {
                    if (!isCurrent())
                    {
                        return null;
                    }

                    Rectangle chunkRect = Rectangle.Intersect(
                        roi,
                        new Rectangle(
                            roi.X + (chunkX * chunkSize),
                            roi.Y + (chunkY * chunkSize),
                            chunkSize,
                            chunkSize));
                    if (chunkRect.Width <= 0 || chunkRect.Height <= 0)
                    {
                        continue;
                    }

                    Rectangle paddedChunkRect = Rectangle.Intersect(
                        new Rectangle(0, 0, imageWidth, imageHeight),
                        Rectangle.FromLTRB(
                            chunkRect.Left - padding,
                            chunkRect.Top - padding,
                            chunkRect.Right + padding,
                            chunkRect.Bottom + padding));
                    Bitmap chunkImage = createChunkImage(paddedChunkRect);
                    if (chunkImage == null)
                    {
                        throw new InvalidOperationException("無法建立 ROI 區塊影像");
                    }

                    using (chunkImage)
                    {
                        bool[,] chunkMask = processChunk(chunkImage);
                        CopyChunkToRoiMask(mask, roi, paddedChunkRect, chunkRect, chunkMask);
                    }

                    completedChunks++;
                    if (completedChunks == 1 || completedChunks == totalChunks || completedChunks % 8 == 0)
                    {
                        bool publishPartialMask = completedChunks == 1 || completedChunks % 8 == 0;
                        if (reportProgress != null)
                        {
                            reportProgress(mask, completedChunks, totalChunks, publishPartialMask);
                        }
                    }
                }
            }

            return mask;
        }

        private static int GetChunkPadding(string method, Dictionary<string, string> parameters)
        {
            if (method == "Canny Edge")
            {
                int gaussianBlurSize = EnsureOdd(GetIntParameter(parameters, "GaussianBlurSize", 5));
                int kernelSize = EnsureOdd(GetIntParameter(parameters, "KernelSize", 3));
                int maxGap = GetIntParameter(parameters, "MaxGap", 2);
                return Math.Max(4, (gaussianBlurSize / 2) + (kernelSize / 2) + maxGap + 4);
            }

            if (method == "Sobel Edge")
            {
                int kernelSize = EnsureOdd(GetIntParameter(parameters, "KernelSize", 3));
                int maxGap = GetIntParameter(parameters, "MaxGap", 2);
                return Math.Max(4, (kernelSize / 2) + maxGap + 4);
            }

            int edgeWidth = EnsureOdd(GetPolarityCoreWidth(parameters));
            int smoothing = EnsureOdd(GetIntParameter(parameters, "Smoothing", 1));
            int polarityMaxGap = GetIntParameter(parameters, "MaxGap", 2);
            return Math.Max(4, (edgeWidth / 2) + (smoothing / 2) + polarityMaxGap + 4);
        }

        private static void CopyChunkToRoiMask(
            bool[,] roiMask,
            Rectangle roi,
            Rectangle processedRect,
            Rectangle chunkRect,
            bool[,] chunkMask)
        {
            if (chunkMask == null)
            {
                throw new InvalidOperationException("ROI 區塊影像處理未產生 Mask");
            }

            int chunkWidth = Math.Min(chunkRect.Width, chunkMask.GetLength(0));
            int chunkHeight = Math.Min(chunkRect.Height, chunkMask.GetLength(1));
            int offsetX = chunkRect.X - roi.X;
            int offsetY = chunkRect.Y - roi.Y;
            int maskOffsetX = chunkRect.X - processedRect.X;
            int maskOffsetY = chunkRect.Y - processedRect.Y;
            int roiMaskWidth = roiMask.GetLength(0);
            int roiMaskHeight = roiMask.GetLength(1);
            int chunkMaskWidth = chunkMask.GetLength(0);
            int chunkMaskHeight = chunkMask.GetLength(1);

            for (int y = 0; y < chunkHeight; y++)
            {
                int targetY = offsetY + y;
                if (targetY < 0 || targetY >= roiMaskHeight)
                {
                    continue;
                }

                for (int x = 0; x < chunkWidth; x++)
                {
                    int targetX = offsetX + x;
                    int sourceX = maskOffsetX + x;
                    int sourceY = maskOffsetY + y;
                    if (targetX >= 0 &&
                        targetX < roiMaskWidth &&
                        sourceX >= 0 &&
                        sourceX < chunkMaskWidth &&
                        sourceY >= 0 &&
                        sourceY < chunkMaskHeight)
                    {
                        roiMask[targetX, targetY] = chunkMask[sourceX, sourceY];
                    }
                }
            }
        }

        private static int GetPolarityCoreWidth(Dictionary<string, string> parameters)
        {
            return GetIntParameter(
                parameters,
                "CoreWidth",
                GetIntParameter(parameters, "EdgeWidth", 3));
        }

        private static int GetIntParameter(Dictionary<string, string> parameters, string key, int defaultValue)
        {
            string value;
            int parsedValue;
            return parameters != null &&
                parameters.TryGetValue(key, out value) &&
                int.TryParse(value, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static int EnsureOdd(int value)
        {
            int normalized = Math.Max(1, value);
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }
    }
}
