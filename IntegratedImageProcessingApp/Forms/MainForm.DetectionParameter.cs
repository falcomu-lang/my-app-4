using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private bool objectDetectionParameterMenuExpanded;
        private bool isRebuildingObjectDetectionParameterMenu;
        private readonly Dictionary<int, string> visibleObjectDetectionParameterIds =
            new Dictionary<int, string>();
        private Panel objectDetectionParameterPanel;
        private TabControl objectDetectionParameterTabControl;
        private bool isObjectDetectionParameterImageLayout;
        private string activeObjectDetectionParameterId;
        private Label objectDetectionImageCheckStatusLabel;
        private TableLayoutPanel objectDetectionObjectNumberPanel;
        private int selectedObjectDetectionNumber = -1;
        private TabPage objectDetectionMeasurementTabPage;
        private Panel objectDetectionMeasurementDisplayHostPanel;
        private ImageDisplayControl objectDetectionMeasurementDisplayControl;
        private readonly object objectDetectionMeasurementMaskLock = new object();
        private Cv.Mat objectDetectionMeasurementMaskCache;
        private string objectDetectionMeasurementMaskCacheKey;

        private static readonly Color ObjectDetectionMeasurementMaskColor =
            Color.FromArgb(100, 255, 105, 180);

        private void EnsureObjectDetectionMeasurementDisplay()
        {
            if (objectDetectionMeasurementDisplayControl != null ||
                leftImageTabControl == null)
            {
                return;
            }

            objectDetectionMeasurementTabPage = CreateImageTabPage(
                "leftObjectDetectionMeasurementTabPage",
                "待量測",
                out objectDetectionMeasurementDisplayHostPanel);
            objectDetectionMeasurementDisplayControl = CreateImageDisplayControl(
                objectDetectionMeasurementDisplayHostPanel,
                "左側 待量測");
        }

        private bool TryGetSelectedObjectDetectionBounds(out Rectangle objectBounds)
        {
            objectBounds = Rectangle.Empty;
            if (selectedObjectDetectionNumber <= 0 ||
                string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return false;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDefinitionSettings definition = parameter == null ||
                string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            ObjectDefinitionDetectedObject detectedObject;
            if (definition == null ||
                !TryGetCompletedObjectDefinitionObject(
                    definition,
                    selectedObjectDetectionNumber,
                    out detectedObject) ||
                detectedObject.Bounds.Width <= 0 ||
                detectedObject.Bounds.Height <= 0)
            {
                return false;
            }

            objectBounds = detectedObject.Bounds;
            return true;
        }

        private void RefreshObjectDetectionMeasurementDisplay()
        {
            if (objectDetectionMeasurementDisplayControl == null ||
                !isObjectDetectionParameterImageLayout ||
                rightOriginalDisplayControl == null ||
                !rightOriginalDisplayControl.HasImage)
            {
                return;
            }

            Rectangle objectBounds;
            bool hasSelectedObject = TryGetSelectedObjectDetectionBounds(out objectBounds);
            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                LargeImageSource source =
                    rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (source == null)
                {
                    return;
                }

                try
                {
                    objectDetectionMeasurementDisplayControl.SetSharedLargeImageSource(
                        source,
                        true);
                }
                finally
                {
                    source.ReleaseReference();
                }

                objectDetectionMeasurementDisplayControl.InvalidateImageView();
                return;
            }

            Bitmap measurementImage = rightOriginalDisplayControl.CloneImage();
            if (measurementImage == null)
            {
                measurementImage = leftOriginalDisplayControl == null
                    ? null
                    : leftOriginalDisplayControl.CloneImage();
            }

            if (measurementImage == null)
            {
                return;
            }

            try
            {
                if (hasSelectedObject)
                {
                    ObjectDetectionParameterSettings parameter =
                        FindObjectDetectionParameter(activeObjectDetectionParameterId);
                    Cv.Mat measurementMask;
                    if (parameter != null &&
                        TryGetObjectDetectionMeasurementMask(
                            parameter,
                            objectBounds,
                            null,
                            measurementImage,
                            out measurementMask))
                    {
                        using (measurementMask)
                        using (Bitmap maskOverlay = CreateObjectDetectionMaskOverlay(
                            measurementMask,
                            ObjectDetectionMeasurementMaskColor))
                        using (Graphics graphics = Graphics.FromImage(measurementImage))
                        {
                            graphics.DrawImageUnscaled(maskOverlay, objectBounds.Location);
                        }
                    }

                    Rectangle imageBounds = new Rectangle(
                        0,
                        0,
                        measurementImage.Width,
                        measurementImage.Height);
                    Rectangle visibleBounds = Rectangle.Intersect(
                        objectBounds,
                        imageBounds);
                    if (visibleBounds.Width > 0 && visibleBounds.Height > 0)
                    {
                        using (Graphics graphics = Graphics.FromImage(measurementImage))
                        using (var outline = new Pen(Color.LimeGreen, 3f))
                        {
                            graphics.DrawRectangle(outline, visibleBounds);
                        }
                    }
                }

                objectDetectionMeasurementDisplayControl.SetDisplayImage(
                    measurementImage,
                    true);
                measurementImage = null;
            }
            finally
            {
                if (measurementImage != null)
                {
                    measurementImage.Dispose();
                }
            }
        }

        private bool TryGetObjectDetectionMeasurementMask(
            ObjectDetectionParameterSettings parameter,
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

            string cacheKey = string.Join(
                "|",
                parameter.Id ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                selectedObjectDetectionNumber.ToString(CultureInfo.InvariantCulture),
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

        private bool TryCreateObjectDetectionSourceMask(
            string sourceType,
            string sourceId,
            ObjectDetectionParameterSettings parameter,
            Rectangle objectBounds,
            LargeImageSource largeSource,
            Bitmap original,
            Cv.Mat originalGray,
            string sourceNamespace,
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
            out Cv.Mat mask)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(definitionId) || selectedObjectDetectionNumber <= 0)
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
                        !objects.Any(item => item.Number == selectedObjectDetectionNumber &&
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
            }
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

            Rectangle objectBounds;
            if (!TryGetSelectedObjectDetectionBounds(out objectBounds) ||
                !e.VisibleSourceRect.IntersectsWith(objectBounds))
            {
                return;
            }

            float x = e.Offset.X + objectBounds.X * e.Zoom;
            float y = e.Offset.Y + objectBounds.Y * e.Zoom;
            float width = Math.Max(1f, objectBounds.Width * e.Zoom);
            float height = Math.Max(1f, objectBounds.Height * e.Zoom);

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter != null)
            {
                LargeImageSource source = rightOriginalDisplayControl == null
                    ? null
                    : rightOriginalDisplayControl.GetSharedLargeImageSource();
                try
                {
                    Cv.Mat measurementMask;
                    if (TryGetObjectDetectionMeasurementMask(
                        parameter,
                        objectBounds,
                        source,
                        null,
                        out measurementMask))
                    {
                        using (measurementMask)
                        {
                            Rectangle visibleMaskBounds = Rectangle.Intersect(
                                objectBounds,
                                e.VisibleSourceRect);
                            if (visibleMaskBounds.Width > 0 && visibleMaskBounds.Height > 0 &&
                                measurementMask.Cols == objectBounds.Width &&
                                measurementMask.Rows == objectBounds.Height)
                            {
                                int maskX = visibleMaskBounds.X - objectBounds.X;
                                int maskY = visibleMaskBounds.Y - objectBounds.Y;
                                using (var visibleMask = new Cv.Mat(
                                    measurementMask,
                                    new Cv.Rect(
                                        maskX,
                                        maskY,
                                        visibleMaskBounds.Width,
                                        visibleMaskBounds.Height)))
                                {
                                    int targetWidth = Math.Max(
                                        1,
                                        Math.Min(
                                            2048,
                                            (int)Math.Ceiling(visibleMaskBounds.Width * e.Zoom)));
                                    int targetHeight = Math.Max(
                                        1,
                                        Math.Min(
                                            2048,
                                            (int)Math.Ceiling(visibleMaskBounds.Height * e.Zoom)));
                                    using (Bitmap overlay = CreateObjectDetectionMaskOverlay(
                                        visibleMask,
                                        targetWidth,
                                        targetHeight,
                                        ObjectDetectionMeasurementMaskColor))
                                    {
                                        DrawLargeProcessedOverlayTile(
                                            e.Graphics,
                                            overlay,
                                            visibleMaskBounds,
                                            e.Zoom,
                                            e.Offset);
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (source != null)
                    {
                        source.ReleaseReference();
                    }
                }
            }

            using (var outline = new Pen(Color.LimeGreen, 2f))
            {
                e.Graphics.DrawRectangle(outline, x, y, width, height);
            }
        }

        private static string GetObjectDetectionParameterDisplayName(
            ObjectDetectionParameterSettings parameter,
            int parameterIndex)
        {
            return string.IsNullOrWhiteSpace(parameter.DisplayName)
                ? "檢測參數" + (parameterIndex + 1).ToString(CultureInfo.InvariantCulture)
                : parameter.DisplayName.Trim();
        }

        private ObjectDetectionParameterSettings FindObjectDetectionParameter(string parameterId)
        {
            return systemParameters.ObjectDetectionParameters.FirstOrDefault(
                parameter => string.Equals(parameter.Id, parameterId, StringComparison.Ordinal));
        }

        private bool TryGetObjectDetectionParameterLocation(
            int visibleIndex,
            string menuText,
            out string parameterId)
        {
            parameterId = null;
            if (!visibleObjectDetectionParameterIds.TryGetValue(visibleIndex, out parameterId))
            {
                string name = menuText == null ? string.Empty : menuText.Trim();
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    if (string.Equals(
                        GetObjectDetectionParameterDisplayName(systemParameters.ObjectDetectionParameters[index], index),
                        name,
                        StringComparison.Ordinal))
                    {
                        parameterId = systemParameters.ObjectDetectionParameters[index].Id;
                        break;
                    }
                }
            }

            return !string.IsNullOrEmpty(parameterId) && FindObjectDetectionParameter(parameterId) != null;
        }

        private void ToggleObjectDetectionParameterMenu()
        {
            objectDetectionParameterMenuExpanded = !objectDetectionParameterMenuExpanded;
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = objectDetectionParameterMenuExpanded
                ? "已展開檢測參數設定"
                : "已收合檢測參數設定";
        }

        private void AddObjectDetectionParameter()
        {
            var parameter = new ObjectDetectionParameterSettings
            {
                DisplayName = "檢測參數" +
                    (systemParameters.ObjectDetectionParameters.Count + 1).ToString(CultureInfo.InvariantCulture),
                Parameters = string.Empty
            };
            systemParameters.ObjectDetectionParameters.Add(parameter);
            objectDetectionParameterMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameter.Id);
            statusLabel.Text = "已新增" + parameter.DisplayName;
        }

        private void RebuildVisibleObjectDetectionParameters()
        {
            bool wasRebuilding = isRebuildingObjectDetectionParameterMenu;
            isRebuildingObjectDetectionParameterMenu = true;
            try
            {
                int menuIndex = functionListBox.Items.IndexOf(ObjectDetectionParameterMenuText);
                if (menuIndex < 0)
                {
                    return;
                }

                visibleObjectDetectionParameterIds.Clear();
                int removeIndex = menuIndex + 1;
                while (removeIndex < functionListBox.Items.Count)
                {
                    string text = functionListBox.Items[removeIndex] as string;
                    if (string.IsNullOrEmpty(text) || !text.StartsWith("    ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    functionListBox.Items.RemoveAt(removeIndex);
                }

                if (!objectDetectionParameterMenuExpanded)
                {
                    return;
                }

                int insertIndex = menuIndex + 1;
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
                    visibleObjectDetectionParameterIds[insertIndex] = parameter.Id;
                    functionListBox.Items.Insert(
                        insertIndex++,
                        "    " + GetObjectDetectionParameterDisplayName(parameter, index));
                }
            }
            finally
            {
                isRebuildingObjectDetectionParameterMenu = wasRebuilding;
            }
        }

        private void SelectObjectDetectionParameter(string parameterId)
        {
            foreach (KeyValuePair<int, string> item in visibleObjectDetectionParameterIds)
            {
                if (string.Equals(item.Value, parameterId, StringComparison.Ordinal))
                {
                    functionListBox.SelectedIndex = item.Key;
                    return;
                }
            }
        }

        private void ShowObjectDetectionParameterMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增檢測參數", null, delegate { AddObjectDetectionParameter(); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDetectionParameterItemContextMenu(string parameterId, Point location)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("命名", null, delegate { RenameObjectDetectionParameter(parameterId); });
            menu.Items.Add("上移", null, delegate { MoveObjectDetectionParameter(parameterId, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectDetectionParameter(parameterId, 1); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectDetectionParameter(parameterId); });
            menu.Show(functionListBox, location);
        }

        private void RenameObjectDetectionParameter(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            string name = PromptForText("命名檢測參數", "檢測參數名稱", parameter.DisplayName);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            parameter.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameterId);
            statusLabel.Text = "已命名檢測參數" + parameter.DisplayName;
        }

        private void MoveObjectDetectionParameter(string parameterId, int direction)
        {
            int index = systemParameters.ObjectDetectionParameters.FindIndex(
                item => string.Equals(item.Id, parameterId, StringComparison.Ordinal));
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= systemParameters.ObjectDetectionParameters.Count)
            {
                return;
            }

            ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
            systemParameters.ObjectDetectionParameters.RemoveAt(index);
            systemParameters.ObjectDetectionParameters.Insert(targetIndex, parameter);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameterId);
        }

        private void DeleteObjectDetectionParameter(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "確定要刪除「" + parameter.DisplayName + "」嗎？",
                    "刪除檢測參數",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            systemParameters.ObjectDetectionParameters.Remove(parameter);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = "已刪除檢測參數";
        }

        private void ShowObjectDetectionParameterPanel(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            HideObjectDefinitionParameterPanel();
            if (objectDetectionParameterPanel != null)
            {
                parameterPanel.Controls.Remove(objectDetectionParameterPanel);
            }

            parameterPlaceholderLabel.Visible = false;
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            objectDetectionParameterPanel = panel;

            panel.Controls.Add(new Label
            {
                Text = "檢測參數名稱",
                Left = 8,
                Top = 12,
                Width = 250
            });
            var nameBox = new TextBox
            {
                Left = 8,
                Top = 34,
                Width = parameterPanel.Width - 18,
                Text = parameter.DisplayName ?? string.Empty
            };
            panel.Controls.Add(nameBox);

            panel.Controls.Add(new Label
            {
                Text = "參數內容",
                Left = 8,
                Top = 72,
                Width = 250
            });
            var parametersBox = new TextBox
            {
                Left = 8,
                Top = 94,
                Width = parameterPanel.Width - 18,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = parameter.Parameters ?? string.Empty
            };
            panel.Controls.Add(parametersBox);

            panel.Controls.Add(new Label
            {
                Text = "物件定義結果關聯",
                Left = 8,
                Top = 228,
                Width = 250
            });
            var objectDefinitionSource = new ComboBox
            {
                Left = 8,
                Top = 250,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            objectDefinitionSource.Items.Add(new ObjectDetectionDefinitionChoice
            {
                DisplayText = "未指定物件定義結果",
                Id = string.Empty
            });
            for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
            {
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                objectDefinitionSource.Items.Add(new ObjectDetectionDefinitionChoice
                {
                    DisplayText = GetObjectDefinitionDisplayName(definition, index),
                    Id = definition.Id
                });
            }

            objectDefinitionSource.SelectedIndex = 0;
            for (int index = 0; index < objectDefinitionSource.Items.Count; index++)
            {
                ObjectDetectionDefinitionChoice choice =
                    objectDefinitionSource.Items[index] as ObjectDetectionDefinitionChoice;
                if (choice != null && string.Equals(
                        choice.Id,
                        parameter.ObjectDefinitionId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    objectDefinitionSource.SelectedIndex = index;
                    break;
                }
            }
            panel.Controls.Add(objectDefinitionSource);

            var apply = new Button
            {
                Text = "套用",
                Left = 8,
                Top = 286,
                Width = parameterPanel.Width - 18
            };
            apply.Click += delegate
            {
                parameter.DisplayName = string.IsNullOrWhiteSpace(nameBox.Text)
                        ? GetObjectDetectionParameterDisplayName(
                        parameter,
                        systemParameters.ObjectDetectionParameters.IndexOf(parameter))
                    : nameBox.Text.Trim();
                parameter.Parameters = parametersBox.Text ?? string.Empty;
                ObjectDetectionDefinitionChoice selectedDefinition =
                    objectDefinitionSource.SelectedItem as ObjectDetectionDefinitionChoice;
                parameter.ObjectDefinitionId = selectedDefinition == null
                    ? string.Empty
                    : selectedDefinition.Id;
                SaveSystemParameters();
                RebuildVisibleObjectDetectionParameters();
                SelectObjectDetectionParameter(parameter.Id);
                selectedObjectDetectionNumber = -1;
                statusLabel.Text = string.IsNullOrEmpty(parameter.ObjectDefinitionId)
                    ? "已套用" + parameter.DisplayName
                    : "已套用" + parameter.DisplayName + "，已關聯物件定義結果";
                UpdateObjectDetectionParameterTabs(parameter);
                EnsureObjectDetectionParameterSourceProcessed(parameter);
                RefreshObjectDetectionMeasurementDisplay();
            };
            panel.Controls.Add(apply);

            var cancel = new Button
            {
                Text = "取消",
                Left = 8,
                Top = 322,
                Width = parameterPanel.Width - 18
            };
            cancel.Click += delegate { ShowObjectDetectionParameterPanel(parameter.Id); };
            panel.Controls.Add(cancel);

            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            if (!string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal))
            {
                selectedObjectDetectionNumber = -1;
            }
            activeObjectDetectionParameterId = parameter.Id;
            SetObjectDetectionParameterDisplayMode(true);
            UpdateObjectDetectionParameterTabs(parameter);
            EnsureObjectDetectionParameterSourceProcessed(parameter);
        }

        private void EnsureObjectDetectionParameterSourceProcessed(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter == null ? null : parameter.Id,
                    "尚未指定物件定義結果",
                    Color.FromArgb(75, 83, 95));
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "物件定義結果不存在，請重新設定關聯",
                    Color.Firebrick);
                return;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                RefreshObjectDetectionParameterQuantityStatus(parameter);
                return;
            }

            if (!HasConfiguredObjectDefinitionSource(definition))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "物件定義尚未設定完整來源",
                    Color.Firebrick);
                ProcessObjectDefinition(definition.Id);
                return;
            }

            if ((rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage) &&
                (leftOriginalDisplayControl == null || !leftOriginalDisplayControl.HasImage))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "請先載入圖片",
                    Color.Firebrick);
                return;
            }

            UpdateObjectDetectionParameterSourceStatus(
                parameter.Id,
                "進行影像確認，請稍等",
                Color.Firebrick);
            ProcessObjectDefinition(definition.Id);
        }

        private void UpdateObjectDetectionParameterSourceStatus(
            string parameterId,
            string text,
            Color color)
        {
            if (objectDetectionImageCheckStatusLabel == null ||
                !string.Equals(
                    activeObjectDetectionParameterId,
                    parameterId,
                    StringComparison.Ordinal))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            string actualCount = string.Equals(text, "進行影像確認，請稍等", StringComparison.Ordinal)
                ? "處理中"
                : "尚未確認";
            if (parameter == null)
            {
                objectDetectionImageCheckStatusLabel.Text = text ?? string.Empty;
            }
            else
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        actualCount,
                        text);
            }
            objectDetectionImageCheckStatusLabel.ForeColor = color;
        }

        private void RefreshObjectDetectionParameterQuantityStatus(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionImageCheckStatusLabel == null ||
                !string.Equals(
                    activeObjectDetectionParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                return;
            }

            long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
            if (parameter.ColumnCount == 0 && parameter.RowCount == 0)
            {
                objectDetectionImageCheckStatusLabel.Text = "數量檢查：未啟用";
                objectDetectionImageCheckStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                return;
            }

            if (parameter.ColumnCount <= 0 || parameter.RowCount <= 0)
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        "尚未確認",
                        "數量設定不完整");
                objectDetectionImageCheckStatusLabel.ForeColor = Color.Firebrick;
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            int actualCount;
            if (definition == null || !TryGetCompletedObjectDefinitionCount(definition, out actualCount))
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        "尚未確認",
                        "尚未進行影像確認");
                objectDetectionImageCheckStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                return;
            }

            bool isMatch = expectedCount == actualCount;
            objectDetectionImageCheckStatusLabel.Text =
                BuildObjectDetectionParameterQuantityStatusText(
                    parameter,
                    actualCount.ToString(CultureInfo.InvariantCulture),
                    isMatch ? "數量符合" : "數量不符，請確認影像或參數");
            objectDetectionImageCheckStatusLabel.ForeColor =
                isMatch ? Color.ForestGreen : Color.Firebrick;
        }

        private void AppendObjectDetectionParameterQuantityToStatusLabel(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null ||
                !string.Equals(parameter.ObjectDefinitionId, definitionId, StringComparison.Ordinal) ||
                parameter.ColumnCount <= 0 ||
                parameter.RowCount <= 0)
            {
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            int actualCount;
            if (definition == null || !TryGetCompletedObjectDefinitionCount(definition, out actualCount))
            {
                return;
            }

            long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
            bool isMatch = expectedCount == actualCount;
            statusLabel.Text += " || 數量確認：" +
                (isMatch ? "符合 " : "不符 ") +
                actualCount.ToString(CultureInfo.InvariantCulture) +
                " / " + expectedCount.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryGetCompletedObjectDefinitionCount(
            ObjectDefinitionSettings definition,
            out int actualCount)
        {
            actualCount = 0;
            if (definition == null)
            {
                return false;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(
                        completedObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                actualCount = objectDefinitionResults.Values
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .Count();
                return true;
            }
        }

        private static string BuildObjectDetectionParameterQuantityStatusText(
            ObjectDetectionParameterSettings parameter,
            string actualCount,
            string resultText)
        {
            string expectedCount = parameter == null
                ? "尚未設定"
                : parameter.ColumnCount > 0 && parameter.RowCount > 0
                    ? ((long)parameter.ColumnCount * parameter.RowCount)
                        .ToString(CultureInfo.InvariantCulture)
                    : "尚未設定";
            return "預期物件數量：" + expectedCount +
                "\r\n實際找到數量：" + (actualCount ?? "尚未確認") +
                "\r\n" + (resultText ?? string.Empty);
        }

        private void NotifyObjectDetectionParameterSourceProcessingCompleted(
            string definitionId,
            bool succeeded,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null ||
                !string.Equals(parameter.ObjectDefinitionId, definitionId, StringComparison.Ordinal))
            {
                return;
            }

            if (succeeded)
            {
                RefreshObjectDetectionParameterQuantityStatus(parameter);
                AppendObjectDetectionParameterQuantityToStatusLabel(definitionId);
                RefreshObjectDetectionMeasurementDisplay();
            }
            else
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "影像確認失敗：" + (errorMessage ?? "未知錯誤"),
                    Color.Firebrick);
            }
        }

        private void EnsureObjectDetectionParameterTabs()
        {
            if (objectDetectionParameterTabControl != null || imageLayoutPanel == null)
            {
                return;
            }

            objectDetectionParameterTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "objectDetectionParameterTabControl",
                Visible = false
            };
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "主參數設定",
                    "主參數設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "尺寸量測設定",
                    "尺寸量測設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "缺陷量測設定",
                    "缺陷量測設定將在此處配置。"));
            imageLayoutPanel.Controls.Add(objectDetectionParameterTabControl, 1, 0);
            objectDetectionParameterTabControl.BringToFront();
        }

        private static TabPage CreateObjectDetectionParameterTabPage(
            string title,
            string placeholderText)
        {
            var tabPage = new TabPage
            {
                Text = title,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(250, 251, 253)
            };
            var label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                Text = placeholderText,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            tabPage.Controls.Add(label);
            return tabPage;
        }

        private void UpdateObjectDetectionParameterTabs(ObjectDetectionParameterSettings parameter)
        {
            EnsureObjectDetectionParameterTabs();
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrEmpty(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            string sourceName = definition == null
                ? "未指定物件定義結果"
                : GetObjectDefinitionDisplayName(
                    definition,
                    systemParameters.ObjectDefinitions.IndexOf(definition));
            BuildObjectDetectionMainParameterTab(parameter, sourceName);
            BuildObjectDetectionSizeMeasurementTab(parameter, sourceName);
            SetObjectDetectionParameterTabText(
                2,
                "缺陷量測來源：" + sourceName + "。將使用已找出的物件結果。");
        }

        private void BuildObjectDetectionSizeMeasurementTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[1];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionObjectNumberPanel = null;

                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                tabPage.Controls.Add(contentPanel);

                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    Text = "尺寸量測來源：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };

                if (parameter.ColumnCount <= 0 || parameter.RowCount <= 0)
                {
                    var invalidQuantityLabel = new Label
                    {
                        Dock = DockStyle.Top,
                        AutoSize = false,
                        Height = 42,
                        Text = "請先在主參數設定輸入有效的列數與排數。",
                        TextAlign = ContentAlignment.TopLeft,
                        ForeColor = Color.Firebrick
                    };
                    contentPanel.Controls.Add(invalidQuantityLabel);
                    contentPanel.Controls.Add(sourceLabel);
                    return;
                }

                long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
                if (expectedCount > 10000)
                {
                    var tooManyObjectsLabel = new Label
                    {
                        Dock = DockStyle.Top,
                        AutoSize = false,
                        Height = 42,
                        Text = "序號按鈕數量過大，請先縮小列數與排數設定。",
                        TextAlign = ContentAlignment.TopLeft,
                        ForeColor = Color.Firebrick
                    };
                    contentPanel.Controls.Add(tooManyObjectsLabel);
                    contentPanel.Controls.Add(sourceLabel);
                    return;
                }

                if (selectedObjectDetectionNumber > expectedCount)
                {
                    selectedObjectDetectionNumber = -1;
                }

                var instruction = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 30,
                    Text = "請選擇 ROI／物件序號，左側各影像分頁會聚焦到對應物件。",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };

                var sourceMaskGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 166,
                    Text = "量測來源 MASK（套用到全部物件序號）",
                    Padding = new Padding(8)
                };

                var sourceMaskLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(0),
                    Margin = new Padding(0),
                    AutoSize = false
                };
                sourceMaskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
                sourceMaskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

                var sourceMaskPrimary = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    sourceMaskPrimary.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(
                    sourceMaskPrimary,
                    parameter.SourceMaskPrimaryType,
                    parameter.SourceMaskPrimaryId,
                    parameter.SourceMaskPrimaryNamespace);

                var sourceMaskOperation = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("直接使用主要 MASK", "None"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("OR（A 加 B）", "Or"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("AND（A 與 B 交集）", "And"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("排除（A AND NOT B）", "Subtract"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("XOR（A 與 B 不同處）", "Xor"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("NOT（反相主要 MASK）", "Not"));
                SelectObjectDefinitionOption(
                    sourceMaskOperation,
                    parameter.SourceMaskOperation,
                    "None");

                var sourceMaskSecondary = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    sourceMaskSecondary.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(
                    sourceMaskSecondary,
                    parameter.SourceMaskSecondaryType,
                    parameter.SourceMaskSecondaryId,
                    parameter.SourceMaskSecondaryNamespace);

                var sourceMaskHint = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "選定後按套用，設定會同步套用到物件 1～6。",
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                var sourceMaskApply = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = "套用來源 MASK"
                };
                sourceMaskApply.Click += delegate
                {
                    ObjectDetectionMaskSourceChoice primary =
                        sourceMaskPrimary.SelectedItem as ObjectDetectionMaskSourceChoice;
                    ObjectDefinitionOption operation =
                        sourceMaskOperation.SelectedItem as ObjectDefinitionOption;
                    ObjectDetectionMaskSourceChoice secondary =
                        sourceMaskSecondary.SelectedItem as ObjectDetectionMaskSourceChoice;
                    if (primary == null || string.IsNullOrWhiteSpace(primary.SourceType) ||
                        string.IsNullOrWhiteSpace(primary.Id))
                    {
                        statusLabel.Text = "請先指定量測主要 MASK 來源";
                        return;
                    }

                    string operationValue = operation == null
                        ? "None"
                        : Convert.ToString(operation.Value, CultureInfo.InvariantCulture);
                    bool requiresSecondary = !string.Equals(operationValue, "None", StringComparison.Ordinal) &&
                        !string.Equals(operationValue, "Not", StringComparison.Ordinal);
                    if (requiresSecondary &&
                        (secondary == null || string.IsNullOrWhiteSpace(secondary.SourceType) ||
                         string.IsNullOrWhiteSpace(secondary.Id)))
                    {
                        statusLabel.Text = "請先指定量測次要 MASK 來源";
                        return;
                    }

                    parameter.SourceMaskMode = string.Equals(operationValue, "None", StringComparison.Ordinal)
                        ? "Direct"
                        : "Composite";
                    parameter.SourceMaskPrimaryType = primary.SourceType;
                    parameter.SourceMaskPrimaryId = primary.Id;
                    parameter.SourceMaskPrimaryNamespace = primary.SourceNamespace;
                    parameter.SourceMaskOperation = operationValue;
                    parameter.SourceMaskSecondaryType = requiresSecondary && secondary != null
                        ? secondary.SourceType
                        : string.Empty;
                    parameter.SourceMaskSecondaryId = requiresSecondary && secondary != null
                        ? secondary.Id
                        : string.Empty;
                    parameter.SourceMaskSecondaryNamespace = requiresSecondary && secondary != null
                        ? secondary.SourceNamespace
                        : string.Empty;
                    InvalidateObjectDetectionMeasurementMaskCache();
                    SaveSystemParameters();
                    UpdateObjectDetectionParameterTabs(parameter);
                    RefreshObjectDetectionMeasurementDisplay();
                    statusLabel.Text = parameter.DisplayName +
                        " 的量測來源 MASK 已套用，並同步所有物件序號";
                };
                sourceMaskLayout.Controls.Add(sourceMaskPrimary, 0, 0);
                sourceMaskLayout.Controls.Add(sourceMaskOperation, 1, 0);
                sourceMaskLayout.Controls.Add(sourceMaskSecondary, 0, 1);
                sourceMaskLayout.SetColumnSpan(sourceMaskSecondary, 2);
                sourceMaskLayout.Controls.Add(sourceMaskHint, 0, 2);
                sourceMaskLayout.SetColumnSpan(sourceMaskHint, 2);
                sourceMaskLayout.Controls.Add(sourceMaskApply, 0, 3);
                sourceMaskLayout.SetColumnSpan(sourceMaskApply, 2);
                sourceMaskGroup.Controls.Add(sourceMaskLayout);

                objectDetectionObjectNumberPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = parameter.ColumnCount,
                    RowCount = parameter.RowCount,
                    Padding = new Padding(0, 4, 0, 4),
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
                };
                for (int column = 0; column < parameter.ColumnCount; column++)
                {
                    objectDetectionObjectNumberPanel.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100f / parameter.ColumnCount));
                }

                for (int row = 0; row < parameter.RowCount; row++)
                {
                    objectDetectionObjectNumberPanel.RowStyles.Add(
                        new RowStyle(SizeType.Absolute, 36f));
                    for (int column = 0; column < parameter.ColumnCount; column++)
                    {
                        int number = (row * parameter.ColumnCount) + column + 1;
                        var button = new Button
                        {
                            Text = number.ToString(CultureInfo.InvariantCulture),
                            Dock = DockStyle.Fill,
                            Margin = new Padding(2),
                            Tag = number,
                            UseVisualStyleBackColor = true
                        };
                        button.Click += ObjectDetectionNumberButton_Click;
                        objectDetectionObjectNumberPanel.Controls.Add(button, column, row);
                    }
                }

                // Add in reverse order so DockStyle.Top renders the ROI number
                // buttons above the shared MASK settings.
                contentPanel.Controls.Add(sourceMaskGroup);
                contentPanel.Controls.Add(objectDetectionObjectNumberPanel);
                contentPanel.Controls.Add(instruction);
                contentPanel.Controls.Add(sourceLabel);
                UpdateObjectDetectionNumberButtonState(parameter);
            }
            finally
            {
                tabPage.ResumeLayout(true);
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

                        foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                        {
                            AddObjectDetectionRelationMaskSourceChoices(
                                choices,
                                keys,
                                relation);
                        }

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

                foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                {
                    AddObjectDetectionRelationMaskSourceChoices(
                        choices,
                        keys,
                        relation);
                }

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

        private void ObjectDetectionNumberButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !(button.Tag is int))
            {
                return;
            }

            int number = (int)button.Tag;
            selectedObjectDetectionNumber = number;
            InvalidateObjectDetectionMeasurementMaskCache();
            UpdateObjectDetectionNumberButtonState(
                FindObjectDetectionParameter(activeObjectDetectionParameterId));

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDefinitionSettings definition = parameter == null ||
                string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            ObjectDefinitionDetectedObject detectedObject;
            if (definition == null ||
                !TryGetCompletedObjectDefinitionObject(
                    definition,
                    number,
                    out detectedObject))
            {
                statusLabel.Text = "物件" + number.ToString(CultureInfo.InvariantCulture) +
                    " 尚未找到可用的物件結果";
                return;
            }

            if (leftImageTabControl != null &&
                objectDetectionMeasurementTabPage != null &&
                leftImageTabControl.TabPages.Contains(objectDetectionMeasurementTabPage))
            {
                leftImageTabControl.SelectedTab = objectDetectionMeasurementTabPage;
            }

            RefreshObjectDetectionMeasurementDisplay();
            FocusObjectDetectionImage(detectedObject.Bounds);
            statusLabel.Text = parameter.DisplayName + "：目前顯示物件" +
                number.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateObjectDetectionNumberButtonState(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionObjectNumberPanel == null)
            {
                return;
            }

            foreach (Control control in objectDetectionObjectNumberPanel.Controls)
            {
                Button button = control as Button;
                if (button == null || !(button.Tag is int))
                {
                    continue;
                }

                int number = (int)button.Tag;
                button.BackColor = number == selectedObjectDetectionNumber
                    ? Color.FromArgb(190, 220, 255)
                    : SystemColors.Control;
                button.FlatStyle = number == selectedObjectDetectionNumber
                    ? FlatStyle.Flat
                    : FlatStyle.Standard;
            }
        }

        private bool TryGetCompletedObjectDefinitionObject(
            ObjectDefinitionSettings definition,
            int number,
            out ObjectDefinitionDetectedObject detectedObject)
        {
            detectedObject = null;
            if (definition == null || number <= 0)
            {
                return false;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(
                        completedObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                foreach (List<ObjectDefinitionDetectedObject> items in objectDefinitionResults.Values)
                {
                    if (items == null)
                    {
                        continue;
                    }

                    detectedObject = items.FirstOrDefault(item => item.Number == number);
                    if (detectedObject != null)
                    {
                        return true;
                    }
                }
            }

            detectedObject = null;
            return false;
        }

        private void FocusObjectDetectionImage(Rectangle objectBounds)
        {
            ImageDisplayControl[] displays =
            {
                leftOriginalDisplayControl,
                leftPreprocessedDisplayControl,
                leftProcessedDisplayControl,
                leftBlockProcessingDisplayControl,
                objectDetectionMeasurementDisplayControl
            };

            ImageDisplayControl anchor = displays.FirstOrDefault(
                display => display != null && display.HasImage);
            if (anchor == null)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                anchor.FocusOnSourceRectangle(objectBounds);
                ImageViewState viewState = anchor.ViewState;
                sharedImageViewState = viewState;
                hasSharedImageViewState = true;
                foreach (ImageDisplayControl display in displays)
                {
                    if (display != null && display.HasImage)
                    {
                        display.ApplyViewState(viewState);
                    }
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void BuildObjectDetectionMainParameterTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[0];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionImageCheckStatusLabel = null;
                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    Text = "來源物件定義結果：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                tabPage.Controls.Add(sourceLabel);

                var group = new GroupBox
                {
                    Text = "數量設定",
                    Dock = DockStyle.Top,
                    Height = 132,
                    Padding = new Padding(10)
                };

                var columnLabel = new Label
                {
                    Text = "列：",
                    Left = 12,
                    Top = 28,
                    Width = 48,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var columnBox = new NumericUpDown
                {
                    Left = 62,
                    Top = 26,
                    Width = 110,
                    Minimum = 0,
                    Maximum = 10000,
                    Value = Math.Max(0, Math.Min(10000, parameter.ColumnCount))
                };
                var rowLabel = new Label
                {
                    Text = "排：",
                    Left = 198,
                    Top = 28,
                    Width = 48,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var rowBox = new NumericUpDown
                {
                    Left = 248,
                    Top = 26,
                    Width = 110,
                    Minimum = 0,
                    Maximum = 10000,
                    Value = Math.Max(0, Math.Min(10000, parameter.RowCount))
                };
                group.Controls.Add(columnLabel);
                group.Controls.Add(columnBox);
                group.Controls.Add(rowLabel);
                group.Controls.Add(rowBox);

                var confirm = new Button
                {
                    Text = "確認",
                    Left = 12,
                    Top = 70,
                    Width = 346,
                    Height = 28
                };
                confirm.Click += delegate
                {
                    parameter.ColumnCount = Decimal.ToInt32(columnBox.Value);
                    parameter.RowCount = Decimal.ToInt32(rowBox.Value);
                    SaveSystemParameters();
                    statusLabel.Text = parameter.DisplayName +
                        " 已套用數量設定：列 " + parameter.ColumnCount.ToString(CultureInfo.InvariantCulture) +
                        "、排 " + parameter.RowCount.ToString(CultureInfo.InvariantCulture);
                    UpdateObjectDetectionParameterTabs(parameter);
                    RefreshObjectDetectionParameterQuantityStatus(parameter);
                    AppendObjectDetectionParameterQuantityToStatusLabel(parameter.ObjectDefinitionId);
                };
                group.Controls.Add(confirm);

                objectDetectionImageCheckStatusLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 64,
                    Text = "尚未進行影像確認",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                contentPanel.Controls.Add(objectDetectionImageCheckStatusLabel);
                contentPanel.Controls.Add(group);
                contentPanel.Controls.Add(sourceLabel);
                tabPage.Controls.Add(contentPanel);
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }

        private void SetObjectDetectionParameterTabText(int tabIndex, string text)
        {
            if (objectDetectionParameterTabControl == null ||
                tabIndex < 0 ||
                tabIndex >= objectDetectionParameterTabControl.TabPages.Count)
            {
                return;
            }

            Label label = objectDetectionParameterTabControl.TabPages[tabIndex]
                .Controls
                .OfType<Label>()
                .FirstOrDefault();
            if (label != null)
            {
                label.Text = text;
            }
        }

        private void HideObjectDetectionParameterPanel()
        {
            if (objectDetectionParameterPanel != null)
            {
                objectDetectionParameterPanel.Visible = false;
            }
        }

        private void SetObjectDetectionParameterDisplayMode(bool enabled)
        {
            if (imageLayoutPanel == null || leftImageTabControl == null || rightImageTabControl == null)
            {
                return;
            }

            EnsureObjectDetectionMeasurementDisplay();
            isObjectDetectionParameterImageLayout = enabled;
            if (!enabled)
            {
                InvalidateObjectDetectionMeasurementMaskCache();
            }
            EnsureObjectDetectionParameterTabs();
            if (isImageViewerMaximized)
            {
                SetImageViewerMaximized(false, false);
            }

            imageLayoutPanel.SuspendLayout();
            try
            {
                if (enabled)
                {
                    leftImageTabControl.TabPages.Clear();
                    leftImageTabControl.TabPages.Add(leftOriginalTabPage);
                    leftImageTabControl.TabPages.Add(objectDetectionMeasurementTabPage);
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    objectDetectionMeasurementDisplayControl.TitleText = "左側 待量測";
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = false;
                    objectDetectionParameterTabControl.Visible = true;
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                }
                else
                {
                    leftImageTabControl.TabPages.Clear();
                    leftImageTabControl.TabPages.Add(leftOriginalTabPage);
                    leftImageTabControl.TabPages.Add(leftPreprocessedTabPage);
                    leftImageTabControl.TabPages.Add(leftProcessedTabPage);
                    leftImageTabControl.TabPages.Add(leftBlockProcessingTabPage);
                    leftImageTabControl.TabPages.Add(leftObjectsTabPage);
                    leftImageTabControl.TabPages.Add(leftDebugTabPage);
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    if (leftObjectsDisplayControl != null)
                    {
                        leftObjectsDisplayControl.TitleText = "左側 區塊結果";
                    }
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = true;
                    if (objectDetectionParameterTabControl != null)
                    {
                        objectDetectionParameterTabControl.Visible = false;
                    }
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                    imageLayoutPanel.SetColumnSpan(rightImageTabControl, 1);
                }
            }
            finally
            {
                imageLayoutPanel.ResumeLayout(true);
            }

            ImageDisplayControl activeDisplay = GetVisibleLeftImageDisplayControl();
            if (activeDisplay != null)
            {
                activeDisplay.InvalidateImageView();
            }

            if (enabled)
            {
                RefreshObjectDetectionMeasurementDisplay();
            }
        }

        private sealed class ObjectDetectionDefinitionChoice
        {
            public string DisplayText { get; set; }

            public string Id { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class ObjectDetectionMaskSourceChoice
        {
            public string DisplayText { get; set; }
            public string Id { get; set; }
            public string SourceType { get; set; }
            public string SourceNamespace { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }
    }
}
