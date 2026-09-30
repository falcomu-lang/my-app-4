using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Interlocked = System.Threading.Interlocked;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private readonly object objectDetectionMeasurementMaskLock = new object();
        private Cv.Mat objectDetectionMeasurementMaskCache;
        private string objectDetectionMeasurementMaskCacheKey;
        private readonly Dictionary<string, Bitmap> objectDetectionMeasurementMaskOverlayTileCache =
            new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private readonly Queue<string> objectDetectionMeasurementMaskOverlayTileCacheOrder =
            new Queue<string>();
        private string objectDetectionMeasurementMaskOverlayCachePrefix;
        private const int ObjectDetectionMeasurementMaskOverlayTileDisplaySize = 512;
        private const int MaxObjectDetectionMeasurementMaskOverlayTileCacheCount = 24;
        private static readonly Color ObjectDetectionMeasurementMaskColor =
            Color.FromArgb(100, 255, 105, 180);


        private bool TryGetObjectDetectionMeasurementMask(
            ObjectDetectionParameterSettings parameter,
            Rectangle objectBounds,
            LargeImageSource largeSource,
            Bitmap original,
            out Cv.Mat mask)
        {
            return TryGetObjectDetectionMeasurementMask(
                parameter,
                selectedObjectDetectionNumber,
                objectBounds,
                largeSource,
                original,
                out mask);
        }

        private bool TryGetObjectDetectionMeasurementMask(
            ObjectDetectionParameterSettings parameter,
            int objectNumber,
            Rectangle objectBounds,
            LargeImageSource largeSource,
            Bitmap original,
            out Cv.Mat mask)
        {
            mask = null;
            if (parameter == null || objectBounds.Width <= 0 || objectBounds.Height <= 0 ||
                string.IsNullOrWhiteSpace(parameter.SourceMaskPrimaryType) ||
                string.IsNullOrWhiteSpace(parameter.SourceMaskPrimaryId))
            {
                return false;
            }

            string cacheKey = CreateObjectDetectionMeasurementMaskCacheKey(
                parameter,
                objectNumber,
                objectBounds);

            lock (objectDetectionMeasurementMaskLock)
            {
                if (string.Equals(
                    objectDetectionMeasurementMaskCacheKey,
                    cacheKey,
                    StringComparison.Ordinal) &&
                    objectDetectionMeasurementMaskCache != null &&
                    !objectDetectionMeasurementMaskCache.Empty() &&
                    objectDetectionMeasurementMaskCache.Cols == objectBounds.Width &&
                    objectDetectionMeasurementMaskCache.Rows == objectBounds.Height)
                {
                    mask = objectDetectionMeasurementMaskCache.Clone();
                    return true;
                }
            }

            Cv.Mat created = null;
            try
            {
                Cv.Mat originalGray = null;
                try
                {
                    if (largeSource != null)
                    {
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskPrimaryType,
                            parameter.SourceMaskPrimaryId,
                            parameter,
                            objectBounds,
                            largeSource,
                            null,
                            null,
                            parameter.SourceMaskPrimaryNamespace,
                            objectNumber,
                            out created))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (original == null)
                        {
                            return false;
                        }

                        originalGray = CreateOpenCvGrayMat(original);
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskPrimaryType,
                            parameter.SourceMaskPrimaryId,
                            parameter,
                            objectBounds,
                            null,
                            original,
                            originalGray,
                            parameter.SourceMaskPrimaryNamespace,
                            objectNumber,
                            out created))
                        {
                            return false;
                        }
                    }
                }
                finally
                {
                    if (originalGray != null)
                    {
                        originalGray.Dispose();
                    }
                }

                string operation = string.IsNullOrWhiteSpace(parameter.SourceMaskOperation)
                    ? "None"
                    : parameter.SourceMaskOperation;
                if (string.Equals(parameter.SourceMaskMode, "Direct", StringComparison.Ordinal) ||
                    string.Equals(operation, "None", StringComparison.Ordinal))
                {
                    return StoreObjectDetectionMeasurementMask(cacheKey, ref created, out mask);
                }

                if (string.Equals(operation, "Not", StringComparison.Ordinal))
                {
                    var inverted = new Cv.Mat();
                    Cv.Cv2.BitwiseNot(created, inverted);
                    created.Dispose();
                    created = inverted;
                    return StoreObjectDetectionMeasurementMask(cacheKey, ref created, out mask);
                }

                Cv.Mat secondary = null;
                Cv.Mat secondaryGray = null;
                try
                {
                    if (largeSource != null)
                    {
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskSecondaryType,
                            parameter.SourceMaskSecondaryId,
                            parameter,
                            objectBounds,
                            largeSource,
                            null,
                            null,
                            parameter.SourceMaskSecondaryNamespace,
                            objectNumber,
                            out secondary))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        secondaryGray = CreateOpenCvGrayMat(original);
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskSecondaryType,
                            parameter.SourceMaskSecondaryId,
                            parameter,
                            objectBounds,
                            null,
                            original,
                            secondaryGray,
                            parameter.SourceMaskSecondaryNamespace,
                            objectNumber,
                            out secondary))
                        {
                            return false;
                        }
                    }

                    if (created.Rows != secondary.Rows || created.Cols != secondary.Cols)
                    {
                        return false;
                    }

                    var combined = new Cv.Mat();
                    if (string.Equals(operation, "Or", StringComparison.Ordinal))
                    {
                        Cv.Cv2.BitwiseOr(created, secondary, combined);
                    }
                    else if (string.Equals(operation, "And", StringComparison.Ordinal))
                    {
                        Cv.Cv2.BitwiseAnd(created, secondary, combined);
                    }
                    else if (string.Equals(operation, "Subtract", StringComparison.Ordinal))
                    {
                        using (var invertedSecondary = new Cv.Mat())
                        {
                            Cv.Cv2.BitwiseNot(secondary, invertedSecondary);
                            Cv.Cv2.BitwiseAnd(created, invertedSecondary, combined);
                        }
                    }
                    else if (string.Equals(operation, "Xor", StringComparison.Ordinal))
                    {
                        Cv.Cv2.BitwiseXor(created, secondary, combined);
                    }
                    else
                    {
                        combined.Dispose();
                        return false;
                    }

                    created.Dispose();
                    created = combined;
                    return StoreObjectDetectionMeasurementMask(cacheKey, ref created, out mask);
                }
                finally
                {
                    if (secondaryGray != null)
                    {
                        secondaryGray.Dispose();
                    }

                    if (secondary != null)
                    {
                        secondary.Dispose();
                    }
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

        private string CreateObjectDetectionMeasurementMaskCacheKey(
            ObjectDetectionParameterSettings parameter,
            int objectNumber,
            Rectangle objectBounds)
        {
            return string.Join(
                "|",
                parameter.Id ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                objectNumber.ToString(CultureInfo.InvariantCulture),
                objectBounds.X.ToString(CultureInfo.InvariantCulture),
                objectBounds.Y.ToString(CultureInfo.InvariantCulture),
                objectBounds.Width.ToString(CultureInfo.InvariantCulture),
                objectBounds.Height.ToString(CultureInfo.InvariantCulture),
                parameter.SourceMaskMode ?? string.Empty,
                parameter.SourceMaskPrimaryType ?? string.Empty,
                parameter.SourceMaskPrimaryId ?? string.Empty,
                parameter.SourceMaskPrimaryNamespace ?? string.Empty,
                parameter.SourceMaskOperation ?? string.Empty,
                parameter.SourceMaskSecondaryType ?? string.Empty,
                parameter.SourceMaskSecondaryId ?? string.Empty,
                parameter.SourceMaskSecondaryNamespace ?? string.Empty);
        }

        private bool TryCreateObjectDetectionSourceMask(
            string sourceType,
            string sourceId,
            ObjectDetectionParameterSettings parameter,
            Rectangle objectBounds,
            LargeImageSource largeSource,
            Bitmap original,
            Cv.Mat originalGray,
            string sourceNamespace,
            int objectNumber,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            if (string.Equals(sourceType, "ObjectDefinition", StringComparison.Ordinal))
            {
                return TryGetObjectDetectionObjectDefinitionMask(
                    sourceId,
                    objectBounds,
                    objectNumber,
                    out mask);
            }

            Rectangle sourceRoi;
            if (!TryGetObjectDetectionContainingRoi(objectBounds, out sourceRoi))
            {
                return false;
            }

            Cv.Mat sourceMask = null;
            if (largeSource != null)
            {
                if (!TryGetCachedObjectDetectionMaskSource(
                    largeSource,
                    sourceType,
                    sourceId,
                    sourceRoi,
                    sourceNamespace,
                    out sourceMask))
                {
                    return false;
                }
            }
            else
            {
                if (original == null || originalGray == null ||
                    !TryGetCachedObjectDetectionMaskSourceFromBitmap(
                        original,
                        originalGray,
                        sourceType,
                        sourceId,
                        sourceRoi,
                        sourceNamespace,
                        out sourceMask))
                {
                    return false;
                }
            }

            try
            {
                int localX = objectBounds.X - sourceRoi.X;
                int localY = objectBounds.Y - sourceRoi.Y;
                if (localX < 0 || localY < 0 ||
                    localX + objectBounds.Width > sourceMask.Cols ||
                    localY + objectBounds.Height > sourceMask.Rows)
                {
                    return false;
                }

                using (var view = new Cv.Mat(
                    sourceMask,
                    new Cv.Rect(
                        localX,
                        localY,
                        objectBounds.Width,
                        objectBounds.Height)))
                {
                    mask = view.Clone();
                }

                return true;
            }
            finally
            {
                sourceMask.Dispose();
            }
        }

        private bool TryGetCachedObjectDetectionMaskSource(
            LargeImageSource source,
            string sourceType,
            string sourceId,
            Rectangle roi,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.Equals(sourceType, "ObjectJudgementProcessing", StringComparison.Ordinal))
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, sourceNamespace, StringComparison.Ordinal));
                if (objectJudgement == null)
                {
                    return false;
                }

                int processingIndex = objectJudgement.ProcessingSteps.FindIndex(
                    item => item != null &&
                        string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (processingIndex < 0)
                {
                    return false;
                }

                return TryGetCachedObjectJudgementMask(
                    objectJudgement,
                    GetObjectJudgementProcessingChain(objectJudgement, processingIndex),
                    roi,
                    out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroupStep", StringComparison.Ordinal) ||
                string.Equals(sourceType, "ImageProcessingStep", StringComparison.Ordinal))
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (step == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(sourceNamespace) &&
                    TryGetCachedProcessedBinaryMask(
                        roi,
                        step,
                        sourceNamespace,
                        out mask))
                {
                    return true;
                }

                return TryGetCachedLargeProcessedStepMask(roi, step, out mask) ||
                    TryGetCachedObjectDefinitionMaskSource(
                        source,
                        sourceType == "ImageProcessingGroupStep"
                            ? "ImageProcessingStep"
                            : sourceType,
                        sourceId,
                        roi,
                        out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroup", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(sourceNamespace))
            {
                ImageProcessingGroupSettings group = FindImageProcessingGroup(sourceId);
                if (group == null)
                {
                    return false;
                }

                var steps = new List<ImageProcessingStepSettings>();
                CollectImageProcessingGroupSteps(group.Id, steps);
                return TryCreateCachedImageProcessingMask(
                    roi,
                    steps,
                    out mask,
                    sourceNamespace);
            }

            return TryGetCachedObjectDefinitionMaskSource(
                source,
                sourceType,
                sourceId,
                roi,
                out mask);
        }

        private bool TryGetCachedObjectDetectionMaskSourceFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            string sourceType,
            string sourceId,
            Rectangle roi,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            // Object-judgement processing step masks are stored per ROI by
            // the large-image processing path. The bitmap path has no
            // equivalent per-step cache yet, so do not silently rerun the
            // relation here or show a result from the wrong processing step.
            if (string.Equals(sourceType, "ObjectJudgementProcessing", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(sourceType, "ImageProcessingGroupStep", StringComparison.Ordinal) ||
                string.Equals(sourceType, "ImageProcessingStep", StringComparison.Ordinal))
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (step == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(sourceNamespace) &&
                    TryCreateCachedImageProcessingMaskFromBitmap(
                        original,
                        originalGray,
                        roi,
                        new[] { step },
                        sourceNamespace,
                        out mask))
                {
                    return true;
                }

                return TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                    original,
                    originalGray,
                    "ImageProcessingStep",
                    sourceId,
                    roi,
                    out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroup", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(sourceNamespace))
            {
                ImageProcessingGroupSettings group = FindImageProcessingGroup(sourceId);
                if (group == null)
                {
                    return false;
                }

                var steps = new List<ImageProcessingStepSettings>();
                CollectImageProcessingGroupSteps(group.Id, steps);
                return TryCreateCachedImageProcessingMaskFromBitmap(
                    original,
                    originalGray,
                    roi,
                    steps,
                    sourceNamespace,
                    out mask);
            }

            return TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                original,
                originalGray,
                sourceType,
                sourceId,
                roi,
                out mask);
        }

        private bool TryGetObjectDetectionContainingRoi(
            Rectangle objectBounds,
            out Rectangle roi)
        {
            roi = Rectangle.Empty;
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle candidate = roiRegion.Bounds;
                if (candidate.Width > 0 && candidate.Height > 0 &&
                    candidate.Contains(objectBounds))
                {
                    roi = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetObjectDetectionObjectDefinitionMask(
            string definitionId,
            Rectangle objectBounds,
            int objectNumber,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(definitionId) || objectNumber <= 0)
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = roiRegion.Bounds;
                    if (roi.Width <= 0 || roi.Height <= 0 ||
                        objectBounds.Left < roi.Left || objectBounds.Top < roi.Top ||
                        objectBounds.Right > roi.Right || objectBounds.Bottom > roi.Bottom)
                    {
                        continue;
                    }

                    string resultKey = CreateObjectDefinitionResultKey(definitionId, roi);
                    List<ObjectDefinitionDetectedObject> objects;
                    Cv.Mat sourceMask;
                    if (!objectDefinitionResults.TryGetValue(resultKey, out objects) ||
                        !objectDefinitionSourceMasks.TryGetValue(resultKey, out sourceMask) ||
                        sourceMask == null || sourceMask.Empty() ||
                        !objects.Any(item => item.Number == objectNumber &&
                            item.Bounds == objectBounds))
                    {
                        continue;
                    }

                    int localX = objectBounds.X - roi.X;
                    int localY = objectBounds.Y - roi.Y;
                    if (localX < 0 || localY < 0 ||
                        localX + objectBounds.Width > sourceMask.Cols ||
                        localY + objectBounds.Height > sourceMask.Rows)
                    {
                        continue;
                    }

                    using (var view = new Cv.Mat(
                        sourceMask,
                        new Cv.Rect(
                            localX,
                            localY,
                            objectBounds.Width,
                            objectBounds.Height)))
                    {
                        mask = view.Clone();
                    }

                    return true;
                }
            }

            return false;
        }

        private bool StoreObjectDetectionMeasurementMask(
            string cacheKey,
            ref Cv.Mat created,
            out Cv.Mat mask)
        {
            mask = null;
            if (created == null || created.Empty())
            {
                return false;
            }

            lock (objectDetectionMeasurementMaskLock)
            {
                if (!string.Equals(
                    objectDetectionMeasurementMaskCacheKey,
                    cacheKey,
                    StringComparison.Ordinal))
                {
                    ClearObjectDetectionMeasurementMaskOverlayTileCacheUnsafe();
                }

                if (objectDetectionMeasurementMaskCache != null)
                {
                    objectDetectionMeasurementMaskCache.Dispose();
                }

                objectDetectionMeasurementMaskCache = created;
                objectDetectionMeasurementMaskCacheKey = cacheKey;
                created = null;
                mask = objectDetectionMeasurementMaskCache.Clone();
                return true;
            }
        }

        private void InvalidateObjectDetectionMeasurementMaskCache()
        {
            lock (objectDetectionMeasurementMaskLock)
            {
                if (objectDetectionMeasurementMaskCache != null)
                {
                    objectDetectionMeasurementMaskCache.Dispose();
                    objectDetectionMeasurementMaskCache = null;
                }

                objectDetectionMeasurementMaskCacheKey = null;
                ClearObjectDetectionMeasurementMaskOverlayTileCacheUnsafe();
            }
        }

        private void EnsureObjectDetectionMeasurementMaskOverlayCachePrefix(string prefix)
        {
            lock (objectDetectionMeasurementMaskLock)
            {
                if (string.Equals(
                    objectDetectionMeasurementMaskOverlayCachePrefix,
                    prefix,
                    StringComparison.Ordinal))
                {
                    return;
                }

                ClearObjectDetectionMeasurementMaskOverlayTileCacheUnsafe();
                objectDetectionMeasurementMaskOverlayCachePrefix = prefix;
            }
        }

        private bool TryDrawCachedObjectDetectionMeasurementMaskOverlayTile(
            string cacheKey,
            Graphics graphics,
            Rectangle tileBounds,
            float zoom,
            PointF offset)
        {
            lock (objectDetectionMeasurementMaskLock)
            {
                Bitmap overlay;
                if (!objectDetectionMeasurementMaskOverlayTileCache.TryGetValue(
                    cacheKey,
                    out overlay))
                {
                    return false;
                }

                try
                {
                    if (overlay == null || overlay.Width <= 0 || overlay.Height <= 0)
                    {
                        throw new ArgumentException("Invalid cached overlay.");
                    }

                    DrawObjectDetectionMeasurementMaskOverlayTile(
                        graphics,
                        overlay,
                        tileBounds,
                        zoom,
                        offset);
                    return true;
                }
                catch (ArgumentException)
                {
                    objectDetectionMeasurementMaskOverlayTileCache.Remove(cacheKey);
                    overlay.Dispose();
                    return false;
                }
            }
        }

        private void StoreAndDrawObjectDetectionMeasurementMaskOverlayTile(
            string cacheKey,
            Bitmap overlay,
            Rectangle tileBounds,
            float zoom,
            PointF offset,
            Graphics graphics)
        {
            lock (objectDetectionMeasurementMaskLock)
            {
                Bitmap existing;
                if (objectDetectionMeasurementMaskOverlayTileCache.TryGetValue(
                    cacheKey,
                    out existing))
                {
                    overlay.Dispose();
                    DrawObjectDetectionMeasurementMaskOverlayTile(
                        graphics,
                        existing,
                        tileBounds,
                        zoom,
                        offset);
                    return;
                }

                while (objectDetectionMeasurementMaskOverlayTileCache.Count >=
                    MaxObjectDetectionMeasurementMaskOverlayTileCacheCount)
                {
                    string oldestKey = objectDetectionMeasurementMaskOverlayTileCacheOrder.Count == 0
                        ? null
                        : objectDetectionMeasurementMaskOverlayTileCacheOrder.Dequeue();
                    Bitmap oldest;
                    if (!string.IsNullOrEmpty(oldestKey) &&
                        objectDetectionMeasurementMaskOverlayTileCache.TryGetValue(
                            oldestKey,
                            out oldest))
                    {
                        objectDetectionMeasurementMaskOverlayTileCache.Remove(oldestKey);
                        oldest.Dispose();
                    }
                }

                objectDetectionMeasurementMaskOverlayTileCache[cacheKey] = overlay;
                objectDetectionMeasurementMaskOverlayTileCacheOrder.Enqueue(cacheKey);
                DrawObjectDetectionMeasurementMaskOverlayTile(
                    graphics,
                    overlay,
                    tileBounds,
                    zoom,
                    offset);
            }
        }

        private static void DrawObjectDetectionMeasurementMaskOverlayTile(
            Graphics graphics,
            Bitmap overlay,
            Rectangle tileBounds,
            float zoom,
            PointF offset)
        {
            if (graphics == null || overlay == null || tileBounds.Width <= 0 ||
                tileBounds.Height <= 0 || zoom <= 0f)
            {
                return;
            }

            int left = (int)Math.Floor(offset.X + (tileBounds.Left * zoom));
            int top = (int)Math.Floor(offset.Y + (tileBounds.Top * zoom));
            int right = (int)Math.Floor(offset.X + (tileBounds.Right * zoom));
            int bottom = (int)Math.Floor(offset.Y + (tileBounds.Bottom * zoom));
            if (right <= left)
            {
                right = left + 1;
            }

            if (bottom <= top)
            {
                bottom = top + 1;
            }

            var destination = Rectangle.FromLTRB(left, top, right, bottom);
            var source = new Rectangle(0, 0, overlay.Width, overlay.Height);
            System.Drawing.Drawing2D.InterpolationMode previousInterpolation =
                graphics.InterpolationMode;
            System.Drawing.Drawing2D.PixelOffsetMode previousPixelOffset =
                graphics.PixelOffsetMode;
            try
            {
                graphics.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(overlay, destination, source, GraphicsUnit.Pixel);
            }
            finally
            {
                graphics.InterpolationMode = previousInterpolation;
                graphics.PixelOffsetMode = previousPixelOffset;
            }
        }

        private void ClearObjectDetectionMeasurementMaskOverlayTileCacheUnsafe()
        {
            foreach (Bitmap overlay in objectDetectionMeasurementMaskOverlayTileCache.Values)
            {
                overlay.Dispose();
            }

            objectDetectionMeasurementMaskOverlayTileCache.Clear();
            objectDetectionMeasurementMaskOverlayTileCacheOrder.Clear();
            objectDetectionMeasurementMaskOverlayCachePrefix = null;
        }

        private static Bitmap CreateObjectDetectionMaskOverlay(Cv.Mat mask, Color color)
        {
            return CreateObjectDetectionMaskOverlay(
                mask,
                mask == null ? 0 : mask.Cols,
                mask == null ? 0 : mask.Rows,
                color);
        }

        private static Bitmap CreateObjectDetectionMaskOverlay(
            Cv.Mat mask,
            int targetWidth,
            int targetHeight,
            Color color)
        {
            if (mask == null || mask.Empty() || targetWidth <= 0 || targetHeight <= 0)
            {
                return new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            }

            if (mask.Cols != targetWidth || mask.Rows != targetHeight)
            {
                using (var scaled = new Cv.Mat())
                {
                    Cv.Cv2.Resize(
                        mask,
                        scaled,
                        new Cv.Size(targetWidth, targetHeight),
                        0,
                        0,
                        targetWidth < mask.Cols || targetHeight < mask.Rows
                            ? Cv.InterpolationFlags.Area
                            : Cv.InterpolationFlags.Nearest);
                    return CreateObjectDetectionMaskOverlayBitmap(scaled, color);
                }
            }

            return CreateObjectDetectionMaskOverlayBitmap(mask, color);
        }

        private static Bitmap CreateObjectDetectionMaskOverlayBitmap(Cv.Mat mask, Color color)
        {
            var overlay = new Bitmap(mask.Cols, mask.Rows, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(
                new Rectangle(0, 0, overlay.Width, overlay.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                byte[] sourceRow = new byte[mask.Cols];
                byte[] outputRow = new byte[Math.Abs(data.Stride)];
                long step = mask.Step();
                for (int y = 0; y < mask.Rows; y++)
                {
                    Array.Clear(outputRow, 0, outputRow.Length);
                    Marshal.Copy(
                        IntPtr.Add(mask.Data, checked((int)(y * step))),
                        sourceRow,
                        0,
                        sourceRow.Length);
                    for (int x = 0; x < mask.Cols; x++)
                    {
                        if (sourceRow[x] == 0)
                        {
                            continue;
                        }

                        int offset = x * 4;
                        outputRow[offset] = color.B;
                        outputRow[offset + 1] = color.G;
                        outputRow[offset + 2] = color.R;
                        outputRow[offset + 3] = color.A;
                    }

                    Marshal.Copy(
                        outputRow,
                        0,
                        data.Scan0 + (y * data.Stride),
                        outputRow.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private void ObjectDetectionMeasurementDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            if (!isObjectDetectionParameterImageLayout || e == null)
            {
                return;
            }

            if (isObjectDetectionResultReviewMode)
            {
                ResultReviewMeasurementRowContext reviewContext =
                    objectDetectionResultReviewSelectedMeasurement;
                if (reviewContext == null || reviewContext.DetectedObject == null ||
                    !e.VisibleSourceRect.IntersectsWith(reviewContext.DetectedObject.Bounds))
                {
                    return;
                }

                if (reviewContext.MaskParameter != null)
                {
                    DrawObjectDetectionMeasurementMaskOverlayTiles(
                        e,
                        reviewContext.MaskParameter,
                        reviewContext.DetectedObject);
                }

                using (var outline = new Pen(Color.LimeGreen, 2f))
                {
                    DrawObjectDetectionObjectOutline(
                        e.Graphics,
                        reviewContext.DetectedObject,
                        outline,
                        e.Zoom,
                        e.Offset);
                }

                DrawObjectDetectionResultReviewMeasurementOverlay(
                    e.Graphics,
                    e.Zoom,
                    e.Offset);
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject) ||
                selectedObject == null ||
                !e.VisibleSourceRect.IntersectsWith(selectedObject.Bounds))
            {
                return;
            }

            Rectangle objectBounds = selectedObject.Bounds;

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter != null)
            {
                DrawObjectDetectionMeasurementMaskOverlayTiles(
                    e,
                    parameter,
                    selectedObject);
            }

            using (var outline = new Pen(Color.LimeGreen, 2f))
            {
                DrawObjectDetectionObjectOutline(
                    e.Graphics,
                    selectedObject,
                    outline,
                    e.Zoom,
                e.Offset);
            }
        }

        private void DrawObjectDetectionMeasurementMaskOverlayTiles(
            LargeImageOverlayPaintEventArgs e,
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionDetectedObject selectedObject)
        {
            Rectangle objectBounds = selectedObject.Bounds;
            Rectangle visibleBounds = Rectangle.Intersect(objectBounds, e.VisibleSourceRect);
            if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0 || e.Zoom <= 0f)
            {
                return;
            }

            string maskCacheKey = CreateObjectDetectionMeasurementMaskCacheKey(
                parameter,
                selectedObject.Number,
                objectBounds);
            string overlayCachePrefix = maskCacheKey + "|zoom|" + e.Zoom.ToString(
                "R",
                CultureInfo.InvariantCulture);
            EnsureObjectDetectionMeasurementMaskOverlayCachePrefix(overlayCachePrefix);

            double tileSizeValue = Math.Ceiling(
                ObjectDetectionMeasurementMaskOverlayTileDisplaySize / (double)e.Zoom);
            int sourceTileSize = tileSizeValue >= int.MaxValue
                ? int.MaxValue
                : Math.Max(1, (int)tileSizeValue);
            int startLocalX = ((visibleBounds.Left - objectBounds.Left) / sourceTileSize) * sourceTileSize;
            int startLocalY = ((visibleBounds.Top - objectBounds.Top) / sourceTileSize) * sourceTileSize;
            int endLocalX = visibleBounds.Right - objectBounds.Left;
            int endLocalY = visibleBounds.Bottom - objectBounds.Top;
            Cv.Mat measurementMask = null;
            LargeImageSource source = null;
            bool maskLookupAttempted = false;
            try
            {
                for (long localYValue = startLocalY; localYValue < endLocalY; localYValue += sourceTileSize)
                {
                    int localY = (int)localYValue;
                    int tileHeight = Math.Min(sourceTileSize, objectBounds.Height - localY);
                    for (long localXValue = startLocalX; localXValue < endLocalX; localXValue += sourceTileSize)
                    {
                        int localX = (int)localXValue;
                        int tileWidth = Math.Min(sourceTileSize, objectBounds.Width - localX);
                        var tileBounds = new Rectangle(
                            objectBounds.X + localX,
                            objectBounds.Y + localY,
                            tileWidth,
                            tileHeight);
                        string tileCacheKey = string.Join(
                            "|",
                            overlayCachePrefix,
                            tileBounds.X.ToString(CultureInfo.InvariantCulture),
                            tileBounds.Y.ToString(CultureInfo.InvariantCulture),
                            tileBounds.Width.ToString(CultureInfo.InvariantCulture),
                            tileBounds.Height.ToString(CultureInfo.InvariantCulture));

                        if (TryDrawCachedObjectDetectionMeasurementMaskOverlayTile(
                            tileCacheKey,
                            e.Graphics,
                            tileBounds,
                            e.Zoom,
                            e.Offset))
                        {
                            continue;
                        }

                        if (!maskLookupAttempted)
                        {
                            maskLookupAttempted = true;
                            source = rightOriginalDisplayControl == null
                                ? null
                                : rightOriginalDisplayControl.GetSharedLargeImageSource();
                            if (!TryGetObjectDetectionMeasurementMask(
                                parameter,
                                selectedObject.Number,
                                objectBounds,
                                source,
                                null,
                                out measurementMask))
                            {
                                return;
                            }
                        }

                        if (measurementMask == null ||
                            measurementMask.Cols != objectBounds.Width ||
                            measurementMask.Rows != objectBounds.Height)
                        {
                            return;
                        }

                        int targetWidth = Math.Max(
                            1,
                            Math.Min(
                                ObjectDetectionMeasurementMaskOverlayTileDisplaySize,
                                (int)Math.Ceiling(tileBounds.Width * (double)e.Zoom)));
                        int targetHeight = Math.Max(
                            1,
                            Math.Min(
                                ObjectDetectionMeasurementMaskOverlayTileDisplaySize,
                                (int)Math.Ceiling(tileBounds.Height * (double)e.Zoom)));
                        using (var maskTile = new Cv.Mat(
                            measurementMask,
                            new Cv.Rect(localX, localY, tileWidth, tileHeight)))
                        {
                            Bitmap overlay = CreateObjectDetectionMaskOverlay(
                                maskTile,
                                targetWidth,
                                targetHeight,
                                ObjectDetectionMeasurementMaskColor);
                            EnsureObjectDetectionMeasurementMaskOverlayCachePrefix(overlayCachePrefix);
                            StoreAndDrawObjectDetectionMeasurementMaskOverlayTile(
                                tileCacheKey,
                                overlay,
                                tileBounds,
                                e.Zoom,
                                e.Offset,
                                e.Graphics);
                        }
                    }
                }
            }
            finally
            {
                if (measurementMask != null)
                {
                    measurementMask.Dispose();
                }

                if (source != null)
                {
                    source.ReleaseReference();
                }
            }
        }


        private List<ObjectDetectionMaskSourceChoice> CreateObjectDetectionMaskSourceChoices(
            ObjectDetectionParameterSettings parameter)
        {
            var choices = new List<ObjectDetectionMaskSourceChoice>
            {
                new ObjectDetectionMaskSourceChoice
                {
                    DisplayText = "未指定來源 MASK",
                    SourceType = string.Empty,
                    Id = string.Empty
                }
            };
            var keys = new HashSet<string>(StringComparer.Ordinal);

            if (parameter != null && !string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
                if (definition != null)
                {
                    AddObjectDetectionMaskSourceChoice(
                        choices,
                        keys,
                        "物件定義結果：" +
                        GetObjectDefinitionDisplayName(
                            definition,
                            systemParameters.ObjectDefinitions.IndexOf(definition)),
                        "ObjectDefinition",
                        definition.Id,
                        string.Empty);

                    var relatedObjectJudgements = new List<ObjectJudgementSettings>();
                    string definitionSourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                        ? "ObjectJudgement"
                        : definition.SourceType;
                    if (string.Equals(definitionSourceType, "ObjectJudgement", StringComparison.Ordinal))
                    {
                        ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                            item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
                        if (objectJudgement != null)
                        {
                            relatedObjectJudgements.Add(objectJudgement);
                        }
                    }
                    else if (string.Equals(definitionSourceType, "Group", StringComparison.Ordinal))
                    {
                        ObjectJudgementGroupSettings objectJudgementGroup =
                            FindObjectJudgementGroup(definition.SourceId);
                        if (objectJudgementGroup != null)
                        {
                            AddObjectDetectionObjectJudgementGroupChoices(
                                choices,
                                keys,
                                objectJudgementGroup.Id,
                                string.Empty,
                                new HashSet<string>(StringComparer.Ordinal));
                        }
                    }

                    foreach (ObjectJudgementSettings objectJudgement in relatedObjectJudgements)
                    {
                        int objectJudgementIndex = systemParameters.ObjectJudgements.IndexOf(objectJudgement);
                        AddObjectDetectionMaskSourceChoice(
                            choices,
                            keys,
                            "區塊：" + GetObjectJudgementDisplayName(objectJudgement, objectJudgementIndex),
                            "ObjectJudgement",
                            objectJudgement.Id,
                            string.Empty);

                        for (int processingIndex = 0;
                            processingIndex < objectJudgement.ProcessingSteps.Count;
                            processingIndex++)
                        {
                            ObjectJudgementProcessingSettings processing =
                                objectJudgement.ProcessingSteps[processingIndex];
                            AddObjectDetectionMaskSourceChoice(
                                choices,
                                keys,
                                "區塊：" +
                                GetObjectJudgementDisplayName(objectJudgement, objectJudgementIndex) +
                                "／" +
                                GetObjectJudgementProcessingDisplayName(processing, processingIndex),
                                "ObjectJudgementProcessing",
                                processing.Id,
                                objectJudgement.Id);
                        }

                        foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                        {
                            AddObjectDetectionRelationMaskSourceChoices(
                                choices,
                                keys,
                                relation);
                        }
                    }

                    if (!HasConfiguredObjectDefinitionBlockSource(definition))
                    {
                        if (string.Equals(definition.SourceRelationType, "Group", StringComparison.Ordinal))
                        {
                            ImageRelationGroupSettings relationGroup = FindImageRelationGroup(
                                definition.SourceRelationId);
                            if (relationGroup != null)
                            {
                                AddObjectDetectionMaskSourceChoice(
                                    choices,
                                    keys,
                                    "關聯群組：" + GetImageRelationGroupDisplayName(relationGroup),
                                    "RelationGroup",
                                    relationGroup.Id,
                                    string.Empty);
                            }
                        }

                        foreach (ImageRelationSettings relation in GetObjectDefinitionSourceRelations(definition))
                        {
                            AddObjectDetectionRelationMaskSourceChoices(
                                choices,
                                keys,
                                relation);
                        }
                    }
                }
            }

            return choices;
        }

        private void AddObjectDetectionObjectJudgementGroupChoices(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            string groupId,
            string parentPath,
            HashSet<string> visitedGroups)
        {
            if (string.IsNullOrWhiteSpace(groupId) ||
                visitedGroups == null ||
                !visitedGroups.Add(groupId))
            {
                return;
            }

            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            string groupName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName.Trim();
            string groupPath = string.IsNullOrWhiteSpace(parentPath)
                ? groupName
                : parentPath + "／" + groupName;

            AddObjectDetectionMaskSourceChoice(
                choices,
                keys,
                "區塊群組：" + groupPath,
                "ObjectJudgementGroup",
                group.Id,
                string.Empty);

            foreach (ObjectJudgementSettings objectJudgement in systemParameters.ObjectJudgements)
            {
                if (!string.Equals(objectJudgement.GroupId, group.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                int objectJudgementIndex = systemParameters.ObjectJudgements.IndexOf(objectJudgement);
                AddObjectDetectionMaskSourceChoice(
                    choices,
                    keys,
                    "區塊：" + groupPath + "／" +
                    GetObjectJudgementDisplayName(objectJudgement, objectJudgementIndex),
                    "ObjectJudgement",
                    objectJudgement.Id,
                    string.Empty);

                for (int processingIndex = 0;
                    processingIndex < objectJudgement.ProcessingSteps.Count;
                    processingIndex++)
                {
                    ObjectJudgementProcessingSettings processing =
                        objectJudgement.ProcessingSteps[processingIndex];
                    AddObjectDetectionMaskSourceChoice(
                        choices,
                        keys,
                        "區塊：" + groupPath + "／" +
                        GetObjectJudgementProcessingDisplayName(processing, processingIndex),
                        "ObjectJudgementProcessing",
                        processing.Id,
                        objectJudgement.Id);
                }

                foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                {
                    AddObjectDetectionRelationMaskSourceChoices(
                        choices,
                        keys,
                        relation);
                }
            }

            foreach (ObjectJudgementGroupSettings childGroup in systemParameters.ObjectJudgementGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    AddObjectDetectionObjectJudgementGroupChoices(
                        choices,
                        keys,
                        childGroup.Id,
                        groupPath,
                        visitedGroups);
                }
            }
        }

        private void AddObjectDetectionRelationMaskSourceChoices(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            ImageRelationSettings relation)
        {
            if (relation == null)
            {
                return;
            }

            string relationName = string.IsNullOrWhiteSpace(relation.DisplayName)
                ? "未命名關聯"
                : relation.DisplayName.Trim();
            string relationNamespace = CreateImageRelationSourceNamespace(relation);
            AddObjectDetectionMaskSourceChoice(
                choices,
                keys,
                "關聯：" + relationName,
                "Relation",
                relation.Id,
                string.Empty);

            if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal))
            {
                AddObjectDetectionImageProcessingGroupChoices(
                    choices,
                    keys,
                    relation.ProcessingId,
                    relationNamespace,
                    string.Empty);
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                item => string.Equals(item.Id, relation.ProcessingId, StringComparison.Ordinal));
            if (step == null || !IsBinaryMaskProcessingMethod(step.Method))
            {
                return;
            }

            int stepIndex = systemParameters.ImageProcessingSteps.IndexOf(step);
            AddObjectDetectionMaskSourceChoice(
                choices,
                keys,
                "影像處理：" + GetObjectDetectionImageProcessingStepName(step, stepIndex),
                "ImageProcessingStep",
                step.Id,
                relationNamespace);
        }

        private void AddObjectDetectionImageProcessingGroupChoices(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            string groupId,
            string sourceNamespace,
            string parentPath)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            string groupName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName.Trim();
            string groupPath = string.IsNullOrWhiteSpace(parentPath)
                ? groupName
                : parentPath + "／" + groupName;
            List<ImageProcessingStepSettings> groupSteps = new List<ImageProcessingStepSettings>();
            CollectImageProcessingGroupSteps(group.Id, groupSteps);
            if (groupSteps.Any(step => step != null && IsBinaryMaskProcessingMethod(step.Method)))
            {
                AddObjectDetectionMaskSourceChoice(
                    choices,
                    keys,
                    "影像處理－群組(" + groupPath + ")",
                    "ImageProcessingGroup",
                    group.Id,
                    sourceNamespace);
            }

            foreach (ImageProcessingStepSettings step in systemParameters.ImageProcessingSteps)
            {
                if (!string.Equals(step.GroupId, group.Id, StringComparison.Ordinal) ||
                    !IsBinaryMaskProcessingMethod(step.Method))
                {
                    continue;
                }

                int stepIndex = systemParameters.ImageProcessingSteps.IndexOf(step);
                AddObjectDetectionMaskSourceChoice(
                    choices,
                    keys,
                    "影像處理－群組(" + groupPath + ")－" +
                    GetObjectDetectionImageProcessingStepName(step, stepIndex),
                    "ImageProcessingGroupStep",
                    step.Id,
                    sourceNamespace);
            }

            foreach (ImageProcessingGroupSettings child in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    AddObjectDetectionImageProcessingGroupChoices(
                        choices,
                        keys,
                        child.Id,
                        sourceNamespace,
                        groupPath);
                }
            }
        }

        private static string GetObjectDetectionImageProcessingStepName(
            ImageProcessingStepSettings step,
            int stepIndex)
        {
            return step == null || string.IsNullOrWhiteSpace(step.DisplayName)
                ? "處理" + (stepIndex + 1).ToString(CultureInfo.InvariantCulture)
                : step.DisplayName.Trim();
        }

        private static string GetImageRelationGroupDisplayName(ImageRelationGroupSettings group)
        {
            return group == null || string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName.Trim();
        }

        private static void AddObjectDetectionMaskSourceChoice(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            string displayText,
            string sourceType,
            string id,
            string sourceNamespace)
        {
            string key = string.Join(
                "|",
                sourceType ?? string.Empty,
                id ?? string.Empty,
                sourceNamespace ?? string.Empty);
            if (string.IsNullOrWhiteSpace(id) || !keys.Add(key))
            {
                return;
            }

            choices.Add(new ObjectDetectionMaskSourceChoice
            {
                DisplayText = displayText,
                SourceType = sourceType,
                Id = id,
                SourceNamespace = sourceNamespace ?? string.Empty
            });
        }

        private static void SelectObjectDetectionMaskSource(
            ComboBox comboBox,
            string sourceType,
            string sourceId,
            string sourceNamespace)
        {
            comboBox.SelectedIndex = 0;
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                ObjectDetectionMaskSourceChoice choice =
                    comboBox.Items[index] as ObjectDetectionMaskSourceChoice;
                if (choice != null &&
                    string.Equals(choice.SourceType, sourceType, StringComparison.Ordinal) &&
                    string.Equals(choice.Id, sourceId, StringComparison.Ordinal) &&
                    string.Equals(choice.SourceNamespace, sourceNamespace, StringComparison.Ordinal))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }
        }


    }
}
