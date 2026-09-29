using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private long RefreshVisibleObjectDefinitionDisplays()
        {
            if (SkipImageDisplayUpdate())
            {
                return 0;
            }

            Stopwatch displayStopwatch = Stopwatch.StartNew();
            bool refreshed = false;

            if (leftImageTabControl.Visible &&
                leftImageTabControl.SelectedTab == leftObjectsTabPage &&
                leftObjectsDisplayControl != null &&
                leftObjectsDisplayControl.HasImage)
            {
                leftObjectsDisplayControl.RefreshImageViewNow();
                refreshed = true;
            }

            if (rightImageTabControl.Visible &&
                rightImageTabControl.SelectedTab == rightObjectsTabPage &&
                rightObjectsDisplayControl != null &&
                rightObjectsDisplayControl.HasImage)
            {
                rightObjectsDisplayControl.RefreshImageViewNow();
                refreshed = true;
            }

            displayStopwatch.Stop();
            if (!refreshed)
            {
                objectDefinitionDisplayTimePending = true;
                return 0;
            }

            objectDefinitionDisplayTimePending = false;
            return Math.Max(1, displayStopwatch.ElapsedMilliseconds);
        }

        private string BuildObjectDefinitionTimingText(
            long processingElapsedMilliseconds,
            long displayElapsedMilliseconds,
            long sourceProcessingElapsedMilliseconds,
            long cclElapsedMilliseconds,
            long retainedMaskElapsedMilliseconds,
            long mergeElapsedMilliseconds,
            long sourceRelationElapsedMilliseconds = -1,
            long sourceGrayElapsedMilliseconds = -1,
            long sourceImageProcessingElapsedMilliseconds = -1,
            long sourceMergeElapsedMilliseconds = -1,
            long sourceObjectProcessingElapsedMilliseconds = -1,
            int sourceCacheHitCount = -1,
            int sourceCacheMissCount = -1)
        {
            string displayText = displayElapsedMilliseconds > 0
                ? displayElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms"
                : "待顯示";
            long preprocessingElapsedMilliseconds =
                Math.Max(0, sourceRelationElapsedMilliseconds) +
                Math.Max(0, sourceGrayElapsedMilliseconds);
            long integrationBlockElapsedMilliseconds =
                Math.Max(0, sourceMergeElapsedMilliseconds) +
                Math.Max(0, sourceObjectProcessingElapsedMilliseconds);
            long objectDefinitionElapsedMilliseconds =
                Math.Max(0, cclElapsedMilliseconds) +
                Math.Max(0, retainedMaskElapsedMilliseconds) +
                Math.Max(0, mergeElapsedMilliseconds);

            return "影像處理全部時間：" +
                Math.Max(0, processingElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 影像前處理：" +
                preprocessingElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 影像處理：" +
                Math.Max(0, sourceImageProcessingElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 整合區塊處理：" +
                integrationBlockElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 物件定義處理：" +
                objectDefinitionElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 顯示時間：" + displayText;
        }

        private void ResetDebugTimingMemo()
        {
            if (debugTimingMemo != null && !debugTimingMemo.IsDisposed)
            {
                debugTimingMemo.Clear();
            }
        }

        private void AppendDebugTimingMemo(string text)
        {
            if (debugTimingMemo == null || debugTimingMemo.IsDisposed)
            {
                return;
            }

            debugTimingMemo.AppendText((text ?? string.Empty) + Environment.NewLine);
            debugTimingMemo.SelectionStart = debugTimingMemo.TextLength;
            debugTimingMemo.ScrollToCaret();
        }

        private void AppendObjectDefinitionTimingMemo(
            string name,
            long processingElapsedMilliseconds,
            long displayElapsedMilliseconds,
            long sourceProcessingElapsedMilliseconds,
            long sourceImageProcessingElapsedMilliseconds,
            long sourceMergeElapsedMilliseconds,
            long sourceObjectProcessingElapsedMilliseconds,
            IEnumerable<string> sourceObjectProcessingDetails,
            long cclElapsedMilliseconds,
            long retainedMaskElapsedMilliseconds,
            long mergeElapsedMilliseconds,
            long contourElapsedMilliseconds,
            bool rotationAnalysisEnabled,
            long rotationGeometryElapsedMilliseconds,
            int sourceCacheHitCount,
            int sourceCacheMissCount)
        {
            AppendDebugTimingMemo(name + " 完成");
            AppendDebugTimingMemo("影像處理總時間：" + processingElapsedMilliseconds + " ms");
            AppendDebugTimingMemo("來源區塊處理：" + sourceProcessingElapsedMilliseconds + " ms");
            AppendDebugTimingMemo("  來源影像處理：" + sourceImageProcessingElapsedMilliseconds + " ms");
            AppendDebugTimingMemo("  遮罩合併：" + sourceMergeElapsedMilliseconds + " ms");
            AppendDebugTimingMemo("  區塊後處理：" + sourceObjectProcessingElapsedMilliseconds + " ms");
            if (sourceObjectProcessingDetails != null)
            {
                foreach (string detail in sourceObjectProcessingDetails)
                {
                    AppendDebugTimingMemo("    " + detail);
                }
            }
            AppendDebugTimingMemo("CCL：" + cclElapsedMilliseconds + " ms");
            if (rotationAnalysisEnabled)
            {
                AppendDebugTimingMemo("輪廓：" + contourElapsedMilliseconds + " ms");
            }
            AppendDebugTimingMemo("保留 MASK：" + retainedMaskElapsedMilliseconds + " ms");
            AppendDebugTimingMemo("Merge by Distance：" + mergeElapsedMilliseconds + " ms");
            AppendDebugTimingMemo(rotationAnalysisEnabled
                ? "旋轉框：" + rotationGeometryElapsedMilliseconds + " ms"
                : "旋轉資訊：未啟用");
            AppendDebugTimingMemo("顯示時間：" + displayElapsedMilliseconds + " ms");
            AppendDebugTimingMemo(
                "來源快取：命中 " + sourceCacheHitCount + "／未命中 " + sourceCacheMissCount);
        }

        private void UpdateObjectDefinitionDisplayTimingIfNeeded()
        {
            if (!objectDefinitionDisplayTimePending ||
                !objectDefinitionProcessingRequested ||
                string.IsNullOrWhiteSpace(activeObjectDefinitionResultId) ||
                objectDefinitionResults.Count == 0)
            {
                return;
            }

            long displayElapsedMilliseconds = RefreshVisibleObjectDefinitionDisplays();
            if (displayElapsedMilliseconds <= 0)
            {
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(activeObjectDefinitionResultId);
            string name = definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)
                ? "物件組"
                : definition.DisplayName;
            statusLabel.Text = name + "：" + BuildObjectDefinitionTimingText(
                objectDefinitionProcessingElapsedMilliseconds,
                displayElapsedMilliseconds,
                objectDefinitionSourceProcessingElapsedMilliseconds,
                objectDefinitionCclElapsedMilliseconds,
                objectDefinitionRetainedMaskElapsedMilliseconds,
                objectDefinitionMergeElapsedMilliseconds,
                objectDefinitionSourceRelationElapsedMilliseconds,
                objectDefinitionSourceGrayElapsedMilliseconds,
                objectDefinitionSourceImageProcessingElapsedMilliseconds,
                objectDefinitionSourceMergeElapsedMilliseconds,
                objectDefinitionSourceObjectProcessingElapsedMilliseconds,
                objectDefinitionSourceCacheHitCount,
                objectDefinitionSourceCacheMissCount);
        }

        private void InvalidateObjectDefinitionDisplayOnly(string definitionId)
        {
            if (rightOriginalDisplayControl != null && rightOriginalDisplayControl.IsLargeImageMode)
            {
                leftObjectsDisplayControl.InvalidateImageView();
                rightObjectsDisplayControl.InvalidateImageView();
                return;
            }

            Dictionary<string, List<ObjectDefinitionDetectedObject>> results;
            Dictionary<string, Cv.Mat> sourceMasks;
            lock (objectDefinitionResultLock)
            {
                if (!objectDefinitionProcessingRequested ||
                    !string.Equals(activeObjectDefinitionResultId, definitionId, StringComparison.Ordinal) ||
                    objectDefinitionResults.Count == 0)
                {
                    leftObjectsDisplayControl.InvalidateImageView();
                    rightObjectsDisplayControl.InvalidateImageView();
                    return;
                }

                results = objectDefinitionResults.ToDictionary(
                    item => item.Key,
                    item => new List<ObjectDefinitionDetectedObject>(item.Value),
                    StringComparer.Ordinal);
                sourceMasks = objectDefinitionSourceMasks.ToDictionary(
                    item => item.Key,
                    item => item.Value.Clone(),
                    StringComparer.Ordinal);
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            Bitmap original = rightOriginalDisplayControl == null
                ? null
                : rightOriginalDisplayControl.CloneImage();
            if (original == null && leftOriginalDisplayControl != null)
            {
                original = leftOriginalDisplayControl.CloneImage();
            }

            if (definition == null || original == null)
            {
                DisposeObjectDefinitionSourceMasks(sourceMasks);
                if (original != null)
                {
                    original.Dispose();
                }

                return;
            }

            Bitmap leftResult = null;
            Bitmap rightResult = null;
            try
            {
                List<Rectangle> rois = GetValidObjectDefinitionBitmapRois(original.Size);
                leftResult = CreateObjectDefinitionAnnotatedBitmap(original, results, sourceMasks, definition, rois);
                rightResult = new Bitmap(leftResult);
                leftObjectsDisplayControl.SetDisplayImage(leftResult, true);
                leftResult = null;
                rightObjectsDisplayControl.SetDisplayImage(rightResult, true);
                rightResult = null;
            }
            finally
            {
                if (leftResult != null)
                {
                    leftResult.Dispose();
                }

                if (rightResult != null)
                {
                    rightResult.Dispose();
                }

                DisposeObjectDefinitionSourceMasks(sourceMasks);

                original.Dispose();
            }
        }

        private Bitmap CreateObjectDefinitionAnnotatedBitmap(
            Bitmap original,
            IDictionary<string, List<ObjectDefinitionDetectedObject>> results,
            IDictionary<string, Cv.Mat> sourceMasks,
            ObjectDefinitionSettings definition,
            IEnumerable<Rectangle> rois)
        {
            var result = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            using (var pen = new Pen(Color.Yellow, Math.Max(1, Math.Min(20, definition.ResultBoxLineWidth))))
            using (var brush = new SolidBrush(Color.Yellow))
            using (var font = new Font(
                SystemFonts.DefaultFont.FontFamily,
                Math.Max(6, Math.Min(72, definition.ResultNumberFontSize)),
                FontStyle.Bold))
            {
                graphics.DrawImageUnscaled(original, Point.Empty);
                foreach (Rectangle roi in rois)
                {
                    string key = CreateObjectDefinitionResultKey(definition.Id, roi);
                    List<ObjectDefinitionDetectedObject> objects;
                    if (!results.TryGetValue(key, out objects))
                    {
                        continue;
                    }

                    Cv.Mat sourceMask;
                    if (sourceMasks != null && sourceMasks.TryGetValue(key, out sourceMask))
                    {
                        PaintRedOverlayImage(result, roi, ConvertOpenCvBinaryMask(sourceMask));
                    }

                    foreach (ObjectDefinitionDetectedObject item in objects)
                    {
                        if (item.HasRotationGeometry && item.RotationCorners != null && item.RotationCorners.Length == 4)
                        {
                            graphics.DrawPolygon(pen, item.RotationCorners);
                        }
                        else
                        {
                            graphics.DrawRectangle(pen, item.Bounds);
                        }
                        graphics.DrawString(
                            item.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            font,
                            brush,
                            item.Bounds.Left + 2,
                            item.Bounds.Top + 2);
                    }
                }
            }

            return result;
        }

        private void PaintObjectDefinitionSourceMaskOverlay(
            LargeImageOverlayPaintEventArgs e,
            Rectangle roi,
            Rectangle visibleRoi,
            string maskKey,
            Cv.Mat sourceMask)
        {
            if (sourceMask == null || visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
            {
                return;
            }

            int targetWidth = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(visibleRoi.Width * e.Zoom)));
            int targetHeight = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(visibleRoi.Height * e.Zoom)));
            string cacheKey = string.Join(
                "|",
                "object-definition-source",
                maskKey,
                visibleRoi.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                visibleRoi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                visibleRoi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                visibleRoi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                targetWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                targetHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Bitmap overlay;
            if (TryGetLargeProcessedOverlayFromCache(cacheKey, out overlay))
            {
                DrawLargeProcessedOverlayTile(e.Graphics, overlay, visibleRoi, e.Zoom, e.Offset);
                return;
            }

            try
            {
                int maskX = visibleRoi.X - roi.X;
                int maskY = visibleRoi.Y - roi.Y;
                using (var visibleMask = new Cv.Mat(
                    sourceMask,
                    new Cv.Rect(maskX, maskY, visibleRoi.Width, visibleRoi.Height)))
                {
                    overlay = CreateLargeProcessedBinaryViewportOverlay(
                        visibleMask,
                        targetWidth,
                        targetHeight);
                }

                TrimLargeProcessedOverlayCache();
                largeProcessedOverlayCache[cacheKey] = overlay;
                DrawLargeProcessedOverlayTile(e.Graphics, overlay, visibleRoi, e.Zoom, e.Offset);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Object definition red overlay failed: " + ex);
            }
        }

        private string CreateObjectDefinitionResultKey(string definitionId, Rectangle roi)
        {
            return string.Join(
                "|",
                imageSourceGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                definitionId ?? string.Empty,
                roi.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private void ObjectDefinitionDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            if (display == null || display.IsPanning)
            {
                return;
            }

            string definitionId;
            lock (objectDefinitionResultLock)
            {
                definitionId = activeObjectDefinitionResultId;
            }
            if (string.IsNullOrEmpty(definitionId))
            {
                return;
            }

            List<ObjectDefinitionDetectedObject> objects = new List<ObjectDefinitionDetectedObject>();
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, roiRegion.Bounds);
                if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
                {
                    continue;
                }

                List<ObjectDefinitionDetectedObject> roiObjects = null;
                Cv.Mat sourceMask = null;
                string resultKey = CreateObjectDefinitionResultKey(definitionId, roiRegion.Bounds);
                int resultGeneration;
                lock (objectDefinitionResultLock)
                {
                    if (!objectDefinitionProcessingRequested ||
                        !string.Equals(activeObjectDefinitionResultId, definitionId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    resultGeneration = objectDefinitionResultGeneration;
                    objectDefinitionResults.TryGetValue(resultKey, out roiObjects);
                    objectDefinitionSourceMasks.TryGetValue(resultKey, out sourceMask);
                }

                if (sourceMask != null)
                {
                    PaintObjectDefinitionSourceMaskOverlay(
                        e,
                        roiRegion.Bounds,
                        visibleRoi,
                        resultKey + "|generation=" + resultGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        sourceMask);
                }

                if (roiObjects != null)
                {
                    objects.AddRange(roiObjects);
                }
            }

            if (objects.Count == 0)
            {
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            int boxLineWidth = definition == null ? 2 : Math.Max(1, Math.Min(20, definition.ResultBoxLineWidth));
            int numberFontSize = definition == null ? 10 : Math.Max(6, Math.Min(72, definition.ResultNumberFontSize));
            using (var pen = new Pen(Color.Yellow, boxLineWidth))
            using (var brush = new SolidBrush(Color.Yellow))
            using (var font = new Font(SystemFonts.DefaultFont.FontFamily, numberFontSize, FontStyle.Bold))
            {
                foreach (ObjectDefinitionDetectedObject item in objects)
                {
                    if (!e.VisibleSourceRect.IntersectsWith(item.Bounds))
                    {
                        continue;
                    }

                    float x = e.Offset.X + item.Bounds.X * e.Zoom;
                    float y = e.Offset.Y + item.Bounds.Y * e.Zoom;
                    if (item.HasRotationGeometry && item.RotationCorners != null && item.RotationCorners.Length == 4)
                    {
                        PointF[] screenCorners = item.RotationCorners
                            .Select(point => new PointF(
                                e.Offset.X + point.X * e.Zoom,
                                e.Offset.Y + point.Y * e.Zoom))
                            .ToArray();
                        e.Graphics.DrawPolygon(pen, screenCorners);
                    }
                    else
                    {
                        float width = Math.Max(1f, item.Bounds.Width * e.Zoom);
                        float height = Math.Max(1f, item.Bounds.Height * e.Zoom);
                        e.Graphics.DrawRectangle(pen, x, y, width, height);
                    }
                    e.Graphics.DrawString(
                        item.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        font,
                        brush,
                        x + 2f,
                        y + 2f);
                }
            }
        }

        private ObjectDefinitionDetectedObject FindObjectDefinitionObjectAtPoint(Point imagePoint)
        {
            lock (objectDefinitionResultLock)
            {
                if (!objectDefinitionProcessingRequested || string.IsNullOrEmpty(activeObjectDefinitionResultId))
                {
                    return null;
                }

                ObjectDefinitionDetectedObject hit = null;
                foreach (List<ObjectDefinitionDetectedObject> items in objectDefinitionResults.Values)
                {
                    if (items == null)
                    {
                        continue;
                    }

                    foreach (ObjectDefinitionDetectedObject item in items)
                    {
                        if (!item.Bounds.Contains(imagePoint))
                        {
                            continue;
                        }

                        long itemBoundsArea = (long)item.Bounds.Width * item.Bounds.Height;
                        long hitBoundsArea = hit == null
                            ? long.MaxValue
                            : (long)hit.Bounds.Width * hit.Bounds.Height;
                        if (hit == null || itemBoundsArea < hitBoundsArea)
                        {
                            hit = item;
                        }
                    }
                }

                return hit;
            }
        }
    }
}
