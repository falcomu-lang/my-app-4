using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private void PrepareLargeProcessedPreview()
        {
            if (string.IsNullOrWhiteSpace(systemParameters.LastImagePath) || !File.Exists(systemParameters.LastImagePath))
            {
                return;
            }

            List<ImageProcessingStepSettings> selectedSteps = GetDisplayedImageProcessingSteps();
            if (systemParameters.RoiRegions.Count == 0 || !HasDisplayedPreviewableImageProcessingSteps())
            {
                ClearProcessedPreviewImages();
                return;
            }

            if (!string.IsNullOrWhiteSpace(displayedImageRelationGroupId))
            {
                PrepareLargeRelationGroupPreview();
                return;
            }

            if (ShouldUseDisplayedRelationPreprocessedSource() &&
                HasConfiguredImagePreprocessingSteps() &&
                preprocessedImageDirty)
            {
                RequestPreprocessedImageUpdate();
                return;
            }

            isSyncingImageView = true;
            LargeImageSource sharedSource = null;
            LargeImageSource processingSource = null;
            try
            {
                sharedSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (sharedSource == null)
                {
                    ClearProcessedPreviewImages();
                    return;
                }

                processingSource = GetLargeImageProcessingSource(sharedSource);
                LargeImageSource processedDisplaySource =
                    string.Equals(displayedImageRelationSourceType, "Original", StringComparison.Ordinal)
                        ? sharedSource
                        : processingSource;

                if (!IsImageDisplayUpdateSuppressed)
                {
                    leftProcessedDisplayControl.SetSharedLargeImageSource(processedDisplaySource, true);
                    rightProcessedDisplayControl.SetSharedLargeImageSource(processedDisplaySource, true);

                    Rectangle? selectedRoi = GetSelectedRoi();
                    if (selectedRoi.HasValue)
                    {
                        leftProcessedDisplayControl.SetRoiOverlay(selectedRoi.Value);
                        rightProcessedDisplayControl.SetRoiOverlay(selectedRoi.Value);
                    }
                }

                foreach (Rectangle roi in OrderRoiRectanglesForVisibleArea(
                    systemParameters.RoiRegions.Select(region => region.Bounds)))
                {
                    foreach (ImageProcessingStepSettings step in selectedSteps)
                    {
                        StartLargeProcessedMaskBuild(processingSource, roi, step);
                    }
                }
            }
            finally
            {
                if (sharedSource != null)
                {
                    sharedSource.ReleaseReference();
                }

                if (processingSource != null)
                {
                    processingSource.ReleaseReference();
                }

                isSyncingImageView = false;
            }

            // A selected step may already have a completed mask in memory.
            // StartLargeProcessedMaskBuild then correctly returns immediately,
            // so explicitly refresh the processed viewers to display that cache.
            if (!IsImageDisplayUpdateSuppressed)
            {
                leftProcessedDisplayControl.ScheduleImageViewRefresh();
                rightProcessedDisplayControl.ScheduleImageViewRefresh();
                ApplySharedImageViewStateToVisibleControls();
                RestoreProcessedImageViewState();
                RestorePreprocessedImageViewState();
            }
        }

        private LargeImageSource GetLargeImageProcessingSource(LargeImageSource originalSource)
        {
            if (!string.Equals(displayedImageRelationSourceType, "Step", StringComparison.Ordinal) &&
                !string.Equals(displayedImageRelationSourceType, "Group", StringComparison.Ordinal))
            {
                return originalSource.AddReference();
            }

            lock (largePreprocessedImageLock)
            {
                if (largePreprocessedImageSource != null && !preprocessedImageDirty)
                {
                    return largePreprocessedImageSource.AddReference();
                }
            }

            return originalSource.AddReference();
        }

        private void StartLargeProcessedMaskBuild(LargeImageSource source, Rectangle roi, ImageProcessingStepSettings step)
        {
            if (source == null || roi.Width <= 0 || roi.Height <= 0 || step == null || !IsBinaryMaskProcessingMethod(step.Method))
            {
                return;
            }

            string maskKey = CreateLargeProcessedMaskKey(roi, step);
            int generation;
            if (!largeImageMaskCache.TryBeginBuild(maskKey, out generation))
            {
                return;
            }

            string method = step.Method;
            string parameters = step.Parameters;
            LargeImageSource sharedSource = source.AddReference();
            statusLabel.Text = CanUseNativeEdgeMask(method, null)
                ? "影像處理運算中...使用 OpenCV " + method
                : "影像處理運算中...";

            Task.Run(
                delegate
                {
                    bool[,] mask = null;
                    try
                    {
                        Dictionary<string, string> parsedParameters = ParseImageProcessingParameters(parameters);

                        // Native edge detectors always receive the entire ROI as one
                        // OpenCV Mat. Tiles exist only for display, never as separate
                        // Canny/Polarity/Sobel calculations.
                        if (CanUseNativeEdgeMask(method, parsedParameters))
                        {
                            largeNativeProcessingGate.Wait();
                            try
                            {
                                if (!IsLargeProcessedMaskBuildCurrent(maskKey, generation))
                                {
                                    return;
                                }

                                BeginInvoke(
                                    new Action(
                                        delegate
                                        {
                                            statusLabel.Text = "影像處理運算中...OpenCV 原圖準備";
                                        }));
                                using (Cv.Mat nativeGray = GetOrCreateLargeRoiOpenCvGrayCache(sharedSource, roi))
                                {
                                    if (!IsLargeProcessedMaskBuildCurrent(maskKey, generation))
                                    {
                                        return;
                                    }

                                    BeginInvoke(
                                        new Action(
                                            delegate
                                            {
                                                statusLabel.Text = "影像處理運算中...使用 OpenCV " + method;
                                    }));
                                    Stopwatch imageProcessingStopwatch = Stopwatch.StartNew();
                                    Cv.Mat binaryMask = null;
                                    List<ImageProcessingStepSettings> executionChain =
                                        GetImageProcessingExecutionChain(step);
                                    binaryMask = CreateCombinedImageProcessingGroupMask(
                                        nativeGray,
                                        roi,
                                        executionChain,
                                        CreateImageProcessingSourceNamespace());
                                    PublishCompletedLargeProcessedBinaryMask(
                                        binaryMask,
                                        roi,
                                        step,
                                        maskKey,
                                        generation,
                                        imageProcessingStopwatch.ElapsedMilliseconds);
                                    binaryMask = null;
                                }
                            }
                            finally
                            {
                                largeNativeProcessingGate.Release();
                            }

                            return;
                        }

                        if ((long)roi.Width * roi.Height <= MaxSinglePassLargeRoiPixels)
                        {
                            byte[,] gray = GetOrCreateLargeRoiGrayCache(sharedSource, roi);
                            if (!IsLargeProcessedMaskBuildCurrent(maskKey, generation))
                            {
                                return;
                            }

                            mask = CreateLargeEdgeMask(gray, method, parsedParameters);
                            PublishCompletedLargeProcessedMask(mask, roi, maskKey, generation);
                            mask = null;
                            return;
                        }

                        mask = LargeRoiMaskBuilder.BuildTiledMask(
                            roi,
                            sharedSource.Width,
                            sharedSource.Height,
                            LargeProcessedMaskChunkSize,
                            method,
                            parsedParameters,
                            delegate(Rectangle paddedChunkRect)
                            {
                                Bitmap chunkImage = null;
                                try
                                {
                                    chunkImage = sharedSource.CreateRegionBitmapFromTiles(paddedChunkRect);
                                }
                                catch (Exception ex)
                                {
                                    if (!(ex is OutOfMemoryException) && !(ex is ArgumentException))
                                    {
                                        throw;
                                    }

                                    Debug.WriteLine(ex);
                                    TryCreateRegionBitmapFromPreview(sharedSource, paddedChunkRect, out chunkImage);
                                }

                                return chunkImage;
                            },
                            delegate(Bitmap chunkImage)
                            {
                                return CreateEdgeMask(chunkImage, method, parsedParameters);
                            },
                            delegate
                            {
                                return IsLargeProcessedMaskBuildCurrent(maskKey, generation);
                            },
                            delegate(bool[,] partialMask, int progress, int totalChunks, bool publishPartialMask)
                            {
                                BeginInvoke(
                                    new Action(
                                        delegate
                                        {
                                            if (!largeImageMaskCache.IsBuildCurrent(maskKey, generation))
                                            {
                                                return;
                                            }

                                            bool publishedPartialMask = publishPartialMask &&
                                                largeImageMaskCache.TryPublishPartialMask(
                                                    maskKey,
                                                    generation,
                                                    partialMask);
                                            if (publishPartialMask && !publishedPartialMask)
                                            {
                                                return;
                                            }

                                            ReportBackgroundStatus("影像處理運算中...ROI Mask " +
                                                progress.ToString(CultureInfo.InvariantCulture) + "/" +
                                                totalChunks.ToString(CultureInfo.InvariantCulture));
                                            if (publishedPartialMask)
                                            {
                                                leftProcessedDisplayControl.InvalidateImageView();
                                                rightProcessedDisplayControl.InvalidateImageView();
                                                InvalidateBlockProcessingDisplays();
                                            }
                                        }));
                            });
                        if (mask == null)
                        {
                            return;
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    if (!largeImageMaskCache.TryPublishMask(maskKey, generation, mask))
                                    {
                                        return;
                                    }
                                    mask = null;

                                    statusLabel.Text = "大圖 ROI Mask 建立完成";
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                    InvalidateBlockProcessingDisplays();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    largeImageMaskCache.FailBuild(maskKey, generation);

                                    statusLabel.Text = "大圖 ROI Mask 建立失敗：" + ex.Message;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                    InvalidateBlockProcessingDisplays();
                                }));
                    }
                    finally
                    {
                        mask = null;
                        sharedSource.ReleaseReference();
                    }
                });
        }

        private void PublishCompletedLargeProcessedMask(bool[,] mask, Rectangle roi, string maskKey, int generation)
        {
            BeginInvoke(
                new Action(
                    delegate
                    {
                        if (!largeImageMaskCache.TryPublishMask(maskKey, generation, mask))
                        {
                            return;
                        }

                        statusLabel.Text = "大圖 ROI Mask 建立完成";
                        leftProcessedDisplayControl.InvalidateImageView();
                        rightProcessedDisplayControl.InvalidateImageView();
                        InvalidateBlockProcessingDisplays();
                    }));
        }

        private string CreateLargeProcessedMaskKey(Rectangle roi, ImageProcessingStepSettings step)
        {
            string parameters = step.Parameters;
            if (string.Equals(step.Method, "Canny Edge", StringComparison.OrdinalIgnoreCase))
            {
                // Only these values affect the raw OpenCV Canny result. Older
                // profiles may still contain selection/length/gap values; they
                // must not force a duplicate full-ROI calculation.
                Dictionary<string, string> parsed = ParseImageProcessingParameters(parameters);
                parameters = string.Join(
                    ";",
                    "LowThreshold=" + GetIntParameter(parsed, "LowThreshold", 50).ToString(CultureInfo.InvariantCulture),
                    "HighThreshold=" + GetIntParameter(parsed, "HighThreshold", 150).ToString(CultureInfo.InvariantCulture),
                    "KernelSize=" + OpenCvEdgeDetectionService.NormalizeCannyKernelSize(GetIntParameter(parsed, "KernelSize", 3)).ToString(CultureInfo.InvariantCulture),
                    "L2Gradient=" + GetBoolParameter(parsed, "L2Gradient", false).ToString(),
                    "GaussianBlurSize=" + GetIntParameter(parsed, "GaussianBlurSize", 5).ToString(CultureInfo.InvariantCulture),
                    "GaussianSigma=" + GetDoubleParameter(parsed, "GaussianSigma", 1.4).ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(
                "|",
                step.Method,
                parameters,
                roi.X.ToString(CultureInfo.InvariantCulture),
                roi.Y.ToString(CultureInfo.InvariantCulture),
                roi.Width.ToString(CultureInfo.InvariantCulture),
                roi.Height.ToString(CultureInfo.InvariantCulture));
        }

        private static int EnsureOdd(int value)
        {
            int normalized = Math.Max(1, value);
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }

        private void ProcessedDisplayControl_LargeImageOverlayPaint(object sender, LargeImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            bool isPanning = display != null && display.IsPanning;

            if (!string.IsNullOrWhiteSpace(displayedImageRelationGroupId))
            {
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    PaintLargeProcessedRelationGroupOverlayForRoi(e, roiRegion.Bounds, isPanning);
                }

                return;
            }

            List<ImageProcessingStepSettings> selectedSteps = GetDisplayedImageProcessingSteps();
            if (selectedSteps.Count == 0)
            {
                return;
            }

            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                foreach (ImageProcessingStepSettings step in selectedSteps)
                {
                    if (IsBinaryMaskProcessingMethod(step.Method))
                    {
                        PaintLargeProcessedOverlayForRoi(e, roiRegion.Bounds, step, isPanning);
                    }
                }
            }
        }

        private void PaintLargeProcessedOverlayForRoi(
            LargeImageOverlayPaintEventArgs e, Rectangle roi, ImageProcessingStepSettings step, bool isPanning)
        {
            Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, roi);
            if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
            {
                return;
            }

            bool[,] mask;
            Cv.Mat binaryMask;
            bool isBuilding;
            string maskKey = CreateLargeProcessedMaskKey(roi, step);
            largeImageMaskCache.TryGet(
                maskKey,
                roi.Height,
                roi.Width,
                out mask,
                out binaryMask,
                out isBuilding);

            if (mask == null && binaryMask == null)
            {
                if (!isBuilding)
                {
                    StartLargeProcessedMaskBuild(e.Source, roi, step);
                }

                return;
            }

            try
            {
                if (!IsAnyProcessedTabVisible() && !IsAnyBlockProcessingTabVisible())
                {
                    return;
                }

                if (binaryMask != null)
                {
                    if (isPanning)
                    {
                        Bitmap overview;
                        if (TryGetLargeProcessedOverlayFromCache("overview|" + maskKey, out overview))
                        {
                            DrawLargeProcessedOverlayRegion(
                                e.Graphics,
                                overview,
                                roi,
                                visibleRoi,
                                e.Zoom,
                                e.Offset);
                        }

                        // Do not create a viewport-sized bitmap for every mouse move.
                        // The exact full-resolution overlay is rebuilt on the first
                        // repaint after panning stops.
                        return;
                    }
                    PaintLargeProcessedBinaryViewportOverlay(e, roi, visibleRoi, step, maskKey, binaryMask);
                    return;
                }

                int startTileX = (visibleRoi.Left / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
                int endTileX = ((visibleRoi.Right + LargeProcessedOverlayTileSize - 1) / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
                int startTileY = (visibleRoi.Top / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
                int endTileY = ((visibleRoi.Bottom + LargeProcessedOverlayTileSize - 1) / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
                Point focusPoint = new Point(
                    visibleRoi.Left + (visibleRoi.Width / 2),
                    visibleRoi.Top + (visibleRoi.Height / 2));

                for (int tileY = startTileY; tileY < endTileY; tileY += LargeProcessedOverlayTileSize)
                {
                    for (int tileX = startTileX; tileX < endTileX; tileX += LargeProcessedOverlayTileSize)
                    {
                        Rectangle tileRect = Rectangle.Intersect(
                            roi,
                            new Rectangle(tileX, tileY, LargeProcessedOverlayTileSize, LargeProcessedOverlayTileSize));
                        if (tileRect.Width <= 0 || tileRect.Height <= 0)
                        {
                            continue;
                        }

                        Bitmap overlay;
                        string cacheKey = CreateLargeProcessedOverlayCacheKey(tileRect, roi, step);
                        if (TryGetLargeProcessedOverlayFromCache(cacheKey, out overlay))
                        {
                            DrawLargeProcessedOverlayTile(e.Graphics, overlay, tileRect, e.Zoom, e.Offset);
                        }
                        else
                        {
                            if (tileRect.Contains(focusPoint))
                            {
                                // One small tile is cheap to create and makes the current
                                // viewport show a result immediately. The remaining tiles
                                // are still generated off the UI thread.
                                overlay = binaryMask != null
                                    ? CreateRedOverlayTileFromBinaryMask(binaryMask, roi, tileRect)
                                    : CreateRedOverlayTileFromMask(mask, roi, tileRect);
                                TrimLargeProcessedOverlayCache();
                                largeProcessedOverlayCache[cacheKey] = overlay;
                                DrawLargeProcessedOverlayTile(e.Graphics, overlay, tileRect, e.Zoom, e.Offset);
                            }
                            else
                            {
                                if (binaryMask != null)
                                {
                                    QueueLargeProcessedOverlayTileFromBinaryMask(binaryMask, roi, tileRect, cacheKey);
                                }
                                else
                                {
                                    QueueLargeProcessedOverlayTileFromMask(mask, roi, tileRect, cacheKey);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (binaryMask != null)
                {
                    binaryMask.Dispose();
                }
            }
        }

        private Cv.Mat GetOrCreateLargeRoiOpenCvGrayCache(LargeImageSource source, Rectangle roi)
        {
            if (!Environment.Is64BitProcess)
            {
                throw new InvalidOperationException(
                    "大圖 OpenCV ROI 處理必須以 64 位元執行。請重新建置目前的 x64 設定後再執行。");
            }

            if (source.IsMemoryBacked)
            {
                return source.CreateGrayscaleMatView(roi);
            }

            try
            {
                return largeRoiGrayscaleCache.GetOrCreateOpenCvRoiView(
                    source,
                    roi,
                    delegate
                    {
                        return LargeImageGrayscaleDecoder.Decode(source);
                    },
                    CreateLargeRoiMatView);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OpenCV full-image decode failed: " + ex);
                throw new InvalidOperationException(
                    "OpenCV 無法建立完整灰階原圖，已停止處理，未使用舊的 ROI tile 備援流程。",
                    ex);
            }
        }

        private static Cv.Mat CreateLargeRoiMatView(Cv.Mat source, Rectangle roi)
        {
            return new Cv.Mat(source, new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height));
        }

        private void PaintLargeProcessedBinaryViewportOverlay(
            LargeImageOverlayPaintEventArgs e,
            Rectangle roi,
            Rectangle visibleRoi,
            ImageProcessingStepSettings step,
            string maskKey,
            Cv.Mat binaryMask)
        {
            int targetWidth = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(visibleRoi.Width * e.Zoom)));
            int targetHeight = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(visibleRoi.Height * e.Zoom)));
            string cacheKey = string.Join(
                "|",
                "viewport",
                maskKey,
                visibleRoi.X.ToString(CultureInfo.InvariantCulture),
                visibleRoi.Y.ToString(CultureInfo.InvariantCulture),
                visibleRoi.Width.ToString(CultureInfo.InvariantCulture),
                visibleRoi.Height.ToString(CultureInfo.InvariantCulture),
                targetWidth.ToString(CultureInfo.InvariantCulture),
                targetHeight.ToString(CultureInfo.InvariantCulture));
            Bitmap overlay;
            if (TryGetLargeProcessedOverlayFromCache(cacheKey, out overlay))
            {
                DrawLargeProcessedOverlayTile(e.Graphics, overlay, visibleRoi, e.Zoom, e.Offset);
                return;
            }

            // The raw OpenCV mask is the source of truth.  Render the complete
            // visible part of it as one display-sized bitmap at every zoom so a
            // pending source-tile batch can never leave a visible edge segment
            // blank. This affects presentation only; contour calculations keep
            // using the full-resolution native mask. Display conversion is
            // deliberately synchronous here: it is bounded by the viewport,
            // avoids a second queue, and must not make the result blink out
            // while the user pans or resets the view.
            try
            {
                Stopwatch displayStopwatch = Stopwatch.StartNew();
                int maskX = visibleRoi.X - roi.X;
                int maskY = visibleRoi.Y - roi.Y;
                using (var visibleMask = new Cv.Mat(binaryMask,
                    new Cv.Rect(maskX, maskY, visibleRoi.Width, visibleRoi.Height)))
                {
                    overlay = CreateLargeProcessedBinaryViewportOverlay(visibleMask, targetWidth, targetHeight);
                }

                TrimLargeProcessedOverlayCache();
                largeProcessedOverlayCache[cacheKey] = overlay;
                lastDisplayProcessingElapsedMilliseconds = displayStopwatch.ElapsedMilliseconds;
                UpdateProcessingTimingStatus(includeImageProcessingTimeOnNextDisplay);
                includeImageProcessingTimeOnNextDisplay = false;
                CompleteParameterApplyStatus();
                DrawLargeProcessedOverlayTile(e.Graphics, overlay, visibleRoi, e.Zoom, e.Offset);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (TryGetLargeProcessedOverlayFromCache("overview|" + maskKey, out overlay))
                {
                    DrawLargeProcessedOverlayTile(e.Graphics, overlay, roi, e.Zoom, e.Offset);
                }
            }
        }

        private void PaintLargeProcessedBinaryExactTiles(
            LargeImageOverlayPaintEventArgs e,
            Rectangle roi,
            Rectangle visibleRoi,
            ImageProcessingStepSettings step,
            Cv.Mat binaryMask)
        {
            int tileSize = e.Zoom < 0.5f ? 512 : e.Zoom < 1f ? 256 : LargeProcessedOverlayTileSize;
            // Finish the tiles currently on screen before any prefetch work.
            // A queued off-screen ring used to consume the short queue and
            // leave lower visible line segments absent until the user panned.
            int startTileX = (visibleRoi.Left / tileSize) * tileSize;
            int endTileX = ((visibleRoi.Right + tileSize - 1) / tileSize) * tileSize;
            int startTileY = (visibleRoi.Top / tileSize) * tileSize;
            int endTileY = ((visibleRoi.Bottom + tileSize - 1) / tileSize) * tileSize;
            Point focusPoint = new Point(
                visibleRoi.Left + (visibleRoi.Width / 2),
                visibleRoi.Top + (visibleRoi.Height / 2));

            for (int tileY = startTileY; tileY < endTileY; tileY += tileSize)
            {
                for (int tileX = startTileX; tileX < endTileX; tileX += tileSize)
                {
                    Rectangle tileRect = Rectangle.Intersect(
                        roi,
                        new Rectangle(tileX, tileY, tileSize, tileSize));
                    if (tileRect.Width <= 0 || tileRect.Height <= 0)
                    {
                        continue;
                    }

                    string cacheKey = CreateLargeProcessedOverlayCacheKey(tileRect, roi, step);
                    Bitmap tile;
                    if (TryGetLargeProcessedOverlayFromCache(cacheKey, out tile))
                    {
                        DrawLargeProcessedOverlayTile(e.Graphics, tile, tileRect, e.Zoom, e.Offset);
                    }
                    else if (tileRect.Contains(focusPoint))
                    {
                        tile = CreateRedOverlayTileFromBinaryMask(binaryMask, roi, tileRect);
                        TrimLargeProcessedOverlayCache();
                        largeProcessedOverlayCache[cacheKey] = tile;
                        DrawLargeProcessedOverlayTile(e.Graphics, tile, tileRect, e.Zoom, e.Offset);
                    }
                    else
                    {
                        QueueLargeProcessedOverlayTileFromBinaryMask(binaryMask, roi, tileRect, cacheKey);
                    }
                }
            }
        }

        private void QueueLargeProcessedBinaryViewportOverlay(
            Cv.Mat mask,
            Rectangle roi,
            Rectangle visibleRoi,
            int targetWidth,
            int targetHeight,
            string cacheKey)
        {
            if (pendingLargeProcessedViewportOverlays.Contains(cacheKey) ||
                pendingLargeProcessedViewportOverlays.Count >= MaxPendingLargeProcessedViewportOverlays)
            {
                return;
            }

            int maskX = visibleRoi.X - roi.X;
            int maskY = visibleRoi.Y - roi.Y;
            Cv.Mat visibleMask = new Cv.Mat(mask, new Cv.Rect(maskX, maskY, visibleRoi.Width, visibleRoi.Height));
            pendingLargeProcessedViewportOverlays.Add(cacheKey);
            statusLabel.Text = "正在更新處理後顯示...";
            Task.Factory.StartNew(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        using (visibleMask)
                        {
                            overlay = CreateLargeProcessedBinaryViewportOverlay(visibleMask, targetWidth, targetHeight);
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedViewportOverlays.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    statusLabel.Text = "影像處理結果已顯示";
                                    CompleteParameterApplyStatus();
                                    leftProcessedDisplayControl.ScheduleImageViewRefresh();
                                    rightProcessedDisplayControl.ScheduleImageViewRefresh();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        pendingLargeProcessedViewportOverlays.Remove(cacheKey);
                                        statusLabel.Text = "處理後顯示失敗：" + ex.Message;
                                    }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private bool TryGetLargeProcessedOverlayFromCache(string cacheKey, out Bitmap overlay)
        {
            if (largeProcessedOverlayCache.TryGetValue(cacheKey, out overlay))
            {
                try
                {
                    if (overlay != null && overlay.Width > 0 && overlay.Height > 0)
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                }

                largeProcessedOverlayCache.Remove(cacheKey);
            }

            overlay = null;
            return false;
        }

        private static Cv.Mat CreateOpenCvFilteredBinaryMask(Cv.Mat edgeMask, int maxGap, int minEdgeLength)
        {
            ApplyOpenCvMaskClosing(edgeMask, maxGap);

            if (minEdgeLength <= 1)
            {
                return edgeMask.Clone();
            }

            using (var labels = new Cv.Mat())
            using (var stats = new Cv.Mat())
            using (var centroids = new Cv.Mat())
            {
                int labelCount = Cv.Cv2.ConnectedComponentsWithStats(
                    edgeMask,
                    labels,
                    stats,
                    centroids,
                    Cv.PixelConnectivity.Connectivity8,
                    Cv.MatType.CV_32SC1);
                var accepted = new bool[labelCount];
                for (int label = 1; label < labelCount; label++)
                {
                    accepted[label] = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area) >= minEdgeLength;
                }

                var filtered = new Cv.Mat(edgeMask.Rows, edgeMask.Cols, Cv.MatType.CV_8UC1);
                int[] labelsRow = new int[edgeMask.Width];
                byte[] resultRow = new byte[edgeMask.Width];
                long labelsStride = labels.Step();
                long resultStride = filtered.Step();
                for (int y = 0; y < edgeMask.Height; y++)
                {
                    Marshal.Copy(labels.Data + checked((int)(y * labelsStride)), labelsRow, 0, labelsRow.Length);
                    Array.Clear(resultRow, 0, resultRow.Length);
                    for (int x = 0; x < labelsRow.Length; x++)
                    {
                        int label = labelsRow[x];
                        if (label > 0 && label < accepted.Length && accepted[label])
                        {
                            resultRow[x] = 255;
                        }
                    }

                    Marshal.Copy(resultRow, 0, filtered.Data + checked((int)(y * resultStride)), resultRow.Length);
                }

                return filtered;
            }
        }

        private static void ApplyOpenCvMaskClosing(Cv.Mat edgeMask, int maxGap)
        {
            if (maxGap <= 0)
            {
                return;
            }

            int kernelSize = EnsureOdd(Math.Max(3, (maxGap * 2) + 1));
            using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(Cv.MorphShapes.Rect, new Cv.Size(kernelSize, kernelSize)))
            {
                Cv.Cv2.MorphologyEx(edgeMask, edgeMask, Cv.MorphTypes.Close, kernel);
            }
        }

        private void QueueLargeProcessedOverlayTileFromMask(
            bool[,] mask, Rectangle roi, Rectangle tileRect, string cacheKey)
        {
            if (pendingLargeProcessedOverlayTiles.Contains(cacheKey) ||
                pendingLargeProcessedOverlayTiles.Count >= MaxPendingCachedMaskOverlayTiles)
            {
                return;
            }

            pendingLargeProcessedOverlayTiles.Add(cacheKey);
            Task.Run(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        overlay = CreateRedOverlayTileFromMask(mask, roi, tileRect);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    leftProcessedDisplayControl.ScheduleImageViewRefresh();
                                    rightProcessedDisplayControl.ScheduleImageViewRefresh();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(new Action(delegate { pendingLargeProcessedOverlayTiles.Remove(cacheKey); }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }
                    }
            });
        }

        private bool IsLargeProcessedMaskBuildCurrent(string maskKey, int generation)
        {
            return largeImageMaskCache.IsBuildCurrent(maskKey, generation);
        }

        private static bool CanUseNativeEdgeMask(string method, Dictionary<string, string> parameters)
        {
            return string.Equals(method, "Canny Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Polarity Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Sobel Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Global Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Adaptive Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Otsu Threshold", StringComparison.OrdinalIgnoreCase);
        }

        private static Cv.Mat CreateNativeLargeEdgeBinaryMask(
            Cv.Mat source,
            string method,
            Dictionary<string, string> parameters)
        {
            return OpenCvImageProcessingService.CreateBinaryMask(
                source,
                method,
                parameters,
                "Canny Edge");
        }

        private void PublishCompletedLargeProcessedBinaryMask(
            Cv.Mat mask,
            Rectangle roi,
            ImageProcessingStepSettings step,
            string maskKey,
            int generation,
            long processingElapsedMilliseconds)
        {
            BeginInvoke(
                new Action(
                    delegate
                    {
                        if (!largeImageMaskCache.TryPublishBinaryMask(maskKey, generation, mask))
                        {
                            mask.Dispose();
                            return;
                        }

                        Cv.Mat maskView;
                        if (largeImageMaskCache.TryGetBinaryMaskView(
                            maskKey,
                            roi.Height,
                            roi.Width,
                            out maskView))
                        {
                            using (maskView)
                            {
                                QueueLargeProcessedBinaryOverview(maskView, roi, maskKey, generation);
                            }
                        }

                        lastImageProcessingElapsedMilliseconds = processingElapsedMilliseconds;
                        RecordImageProcessingStepElapsed(step, processingElapsedMilliseconds);
                        // Defer the timing message until the first actual
                        // viewport render. Subsequent cache/display updates
                        // must not imply that the algorithm ran again.
                        includeImageProcessingTimeOnNextDisplay = true;
                        if (IsAnyProcessedTabVisible())
                        {
                            SetParameterApplyStatus("產生預覽圖中...");
                        }
                        else
                        {
                            CompleteParameterApplyStatus(true);
                        }
                        leftProcessedDisplayControl.InvalidateImageView();
                        rightProcessedDisplayControl.InvalidateImageView();
                    }));
        }

        private void RecordImageProcessingStepElapsed(
            ImageProcessingStepSettings step,
            long processingElapsedMilliseconds)
        {
            if (step == null)
            {
                return;
            }

            long existingElapsedMilliseconds;
            imageProcessingStepElapsedMilliseconds.TryGetValue(step, out existingElapsedMilliseconds);
            imageProcessingStepElapsedMilliseconds[step] = existingElapsedMilliseconds + processingElapsedMilliseconds;
        }

        private void UpdateProcessingTimingStatus(bool includeImageProcessingTime)
        {
            imageProcessingDisplayPending = false;
            statusLabel.Text = BuildPipelineTimingText(includeImageProcessingTime, false);
        }

        private void QueueLargeProcessedBinaryOverview(Cv.Mat mask, Rectangle roi, string maskKey, int generation)
        {
            string cacheKey = "overview|" + maskKey;
            if (largeProcessedOverlayCache.ContainsKey(cacheKey) ||
                pendingLargeProcessedOverviewOverlays.Contains(cacheKey))
            {
                return;
            }

            // A full ROI view retains the native buffer without cloning the
            // large image.  The Mat copy constructor maps to ranges in this
            // OpenCvSharp version and throws "empty ranges".
            Cv.Mat overviewMask = new Cv.Mat(mask, new Cv.Rect(0, 0, mask.Width, mask.Height));
            pendingLargeProcessedOverviewOverlays.Add(cacheKey);
            Task.Factory.StartNew(
                delegate
                {
                    Bitmap overview = null;
                    try
                    {
                        using (overviewMask)
                        {
                            float scale = Math.Min(1f, 2048f / Math.Max(overviewMask.Width, overviewMask.Height));
                            overview = CreateLargeProcessedBinaryViewportOverlay(
                                overviewMask,
                                Math.Max(1, (int)Math.Round(overviewMask.Width * scale)),
                                Math.Max(1, (int)Math.Round(overviewMask.Height * scale)));
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverviewOverlays.Remove(cacheKey);
                                    if (!largeImageMaskCache.IsBinaryMaskCurrent(maskKey, generation))
                                    {
                                        overview.Dispose();
                                        return;
                                    }

                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overview;
                                    overview = null;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(new Action(delegate { pendingLargeProcessedOverviewOverlays.Remove(cacheKey); }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overview != null)
                        {
                            overview.Dispose();
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void TrimLargeProcessedOverlayCache()
        {
            while (largeProcessedOverlayCache.Count >= MaxLargeProcessedOverlayCacheCount)
            {
                KeyValuePair<string, Bitmap> oldest = largeProcessedOverlayCache.FirstOrDefault(
                    pair => !pair.Key.StartsWith("overview|", StringComparison.Ordinal));
                if (string.IsNullOrEmpty(oldest.Key))
                {
                    oldest = largeProcessedOverlayCache.First();
                }

                largeProcessedOverlayCache.Remove(oldest.Key);
                oldest.Value.Dispose();
            }
        }

        private string CreateLargeProcessedOverlayCacheKey(Rectangle tileRect, Rectangle roi, ImageProcessingStepSettings step)
        {
            return string.Join(
                "|",
                step.Method,
                step.Parameters,
                roi.X.ToString(CultureInfo.InvariantCulture),
                roi.Y.ToString(CultureInfo.InvariantCulture),
                roi.Width.ToString(CultureInfo.InvariantCulture),
                roi.Height.ToString(CultureInfo.InvariantCulture),
                tileRect.X.ToString(CultureInfo.InvariantCulture),
                tileRect.Y.ToString(CultureInfo.InvariantCulture),
                tileRect.Width.ToString(CultureInfo.InvariantCulture),
                tileRect.Height.ToString(CultureInfo.InvariantCulture));
        }

        private void QueueLargeProcessedOverlayTile(LargeImageSource source, Rectangle tileRect, ImageProcessingStepSettings step, string cacheKey)
        {
            if (pendingLargeProcessedOverlayTiles.Contains(cacheKey))
            {
                return;
            }

            if (pendingLargeProcessedOverlayTiles.Count >= MaxPendingLargeProcessedOverlayTiles)
            {
                return;
            }

            pendingLargeProcessedOverlayTiles.Add(cacheKey);
            string method = step.Method;
            string parameters = step.Parameters;
            LargeImageSource sharedSource = source.AddReference();
            ReportBackgroundStatus("影像處理運算中...等待 " + pendingLargeProcessedOverlayTiles.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊");

            Task.Run(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        Bitmap tile;
                        bool usedPreviewTile = false;
                        if (!sharedSource.TryCreateRegionBitmapFromCachedTile(tileRect, out tile))
                        {
                            usedPreviewTile = TryCreateRegionBitmapFromPreview(sharedSource, tileRect, out tile);
                        }

                        if (tile == null)
                        {
                            sharedSource.QueueTile(
                                tileRect,
                                delegate
                                {
                                    try
                                    {
                                        BeginInvoke(
                                            new Action(
                                                delegate
                                                {
                                                    leftProcessedDisplayControl.InvalidateImageView();
                                                    rightProcessedDisplayControl.InvalidateImageView();
                                                }));
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                    }
                                    catch (InvalidOperationException)
                                    {
                                    }
                                });
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                        statusLabel.Text = "等待原圖區塊載入後再處理...";
                                        leftProcessedDisplayControl.InvalidateImageView();
                                        rightProcessedDisplayControl.InvalidateImageView();
                                    }));
                            return;
                        }

                        if (usedPreviewTile)
                        {
                            sharedSource.QueueTile(
                                tileRect,
                                delegate
                                {
                                    try
                                    {
                                        BeginInvoke(
                                            new Action(
                                                delegate
                                                {
                                                    Bitmap staleOverlay;
                                                    if (largeProcessedOverlayCache.TryGetValue(cacheKey, out staleOverlay))
                                                    {
                                                        largeProcessedOverlayCache.Remove(cacheKey);
                                                        staleOverlay.Dispose();
                                                    }

                                                    leftProcessedDisplayControl.InvalidateImageView();
                                                    rightProcessedDisplayControl.InvalidateImageView();
                                                }));
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                    }
                                    catch (InvalidOperationException)
                                    {
                                    }
                                });
                        }

                        using (tile)
                        {
                            bool[,] mask = CreateEdgeMask(tile, method, ParseImageProcessingParameters(parameters));
                            overlay = CreateRedOverlayTile(mask);
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    ReportBackgroundStatus(pendingLargeProcessedOverlayTiles.Count > 0
                                        ? "影像處理運算中...等待 " + pendingLargeProcessedOverlayTiles.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊"
                                        : "影像處理完成，已顯示 " + largeProcessedOverlayCache.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊");
                                    if (pendingLargeProcessedOverlayTiles.Count == 0)
                                    {
                                        CompleteParameterApplyStatus();
                                    }
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    statusLabel.Text = "影像處理失敗：" + ex.Message;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                }));
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }

                        sharedSource.ReleaseReference();
                    }
                });
        }

        private void QueueLargeProcessedOverlayTileFromBinaryMask(
            Cv.Mat mask, Rectangle roi, Rectangle tileRect, string cacheKey)
        {
            if (pendingLargeProcessedOverlayTiles.Contains(cacheKey) ||
                pendingLargeProcessedOverlayTiles.Count >= MaxPendingCachedMaskOverlayTiles)
            {
                return;
            }

            int maskX = tileRect.X - roi.X;
            int maskY = tileRect.Y - roi.Y;
            Cv.Mat maskTile;
            using (var tileView = new Cv.Mat(mask, new Cv.Rect(maskX, maskY, tileRect.Width, tileRect.Height)))
            {
                maskTile = tileView.Clone();
            }

            pendingLargeProcessedOverlayTiles.Add(cacheKey);
            Task.Run(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        using (maskTile)
                        {
                            overlay = CreateRedOverlayTileFromBinaryMask(maskTile, Rectangle.Empty,
                                new Rectangle(0, 0, tileRect.Width, tileRect.Height));
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    leftProcessedDisplayControl.ScheduleImageViewRefresh();
                                    rightProcessedDisplayControl.ScheduleImageViewRefresh();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(new Action(delegate { pendingLargeProcessedOverlayTiles.Remove(cacheKey); }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }
                    }
                });
        }

        private static bool TryCreateRegionBitmapFromPreview(LargeImageSource source, Rectangle sourceRect, out Bitmap region)
        {
            region = null;
            LargeImageSource.PreviewBitmap preview = null;
            try
            {
                preview = source.GetBestPreview(0f);
                if (preview == null || preview.Bitmap == null || preview.Scale <= 0f)
                {
                    return false;
                }

                Rectangle previewRect = Rectangle.FromLTRB(
                    Math.Max(0, Math.Min(preview.Bitmap.Width - 1, (int)Math.Floor(sourceRect.Left * preview.Scale))),
                    Math.Max(0, Math.Min(preview.Bitmap.Height - 1, (int)Math.Floor(sourceRect.Top * preview.Scale))),
                    Math.Max(1, Math.Min(preview.Bitmap.Width, (int)Math.Ceiling(sourceRect.Right * preview.Scale))),
                    Math.Max(1, Math.Min(preview.Bitmap.Height, (int)Math.Ceiling(sourceRect.Bottom * preview.Scale))));
                if (previewRect.Right <= previewRect.Left || previewRect.Bottom <= previewRect.Top)
                {
                    return false;
                }

                region = new Bitmap(sourceRect.Width, sourceRect.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(region))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    graphics.DrawImage(preview.Bitmap, new Rectangle(0, 0, region.Width, region.Height), previewRect, GraphicsUnit.Pixel);
                }

                return true;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
                if (region != null)
                {
                    region.Dispose();
                    region = null;
                }

                return false;
            }
            finally
            {
                if (preview != null)
                {
                    preview.Dispose();
                }
            }
        }

        private static Bitmap CreateRedOverlayTile(bool[,] mask)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            var overlay = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] row = new byte[Math.Abs(stride)];
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    for (int x = 0; x < width; x++)
                    {
                        if (mask[x, y])
                        {
                            int offset = x * 4;
                            row[offset] = 0;
                            row[offset + 1] = 0;
                            row[offset + 2] = 255;
                            row[offset + 3] = 255;
                        }
                    }

                    Marshal.Copy(row, 0, data.Scan0 + (y * stride), row.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private static Bitmap CreateRedOverlayTileFromMask(bool[,] mask, Rectangle maskRoi, Rectangle tileRect)
        {
            int width = tileRect.Width;
            int height = tileRect.Height;
            var overlay = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] row = new byte[Math.Abs(stride)];
                int maskWidth = mask.GetLength(0);
                int maskHeight = mask.GetLength(1);
                int maskStartX = tileRect.X - maskRoi.X;
                int maskStartY = tileRect.Y - maskRoi.Y;
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    int maskY = maskStartY + y;
                    if (maskY >= 0 && maskY < maskHeight)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int maskX = maskStartX + x;
                            if (maskX >= 0 && maskX < maskWidth && mask[maskX, maskY])
                            {
                                int offset = x * 4;
                                row[offset] = 0;
                                row[offset + 1] = 0;
                                row[offset + 2] = 255;
                                row[offset + 3] = 255;
                            }
                        }
                    }

                    Marshal.Copy(row, 0, data.Scan0 + (y * stride), row.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private static Bitmap CreateRedOverlayTileFromBinaryMask(Cv.Mat mask, Rectangle maskRoi, Rectangle tileRect)
        {
            int width = tileRect.Width;
            int height = tileRect.Height;
            var overlay = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] sourceRow = new byte[width];
                byte[] outputRow = new byte[Math.Abs(stride)];
                int maskStartX = tileRect.X - maskRoi.X;
                int maskStartY = tileRect.Y - maskRoi.Y;
                long maskStride = mask.Step();
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(outputRow, 0, outputRow.Length);
                    int maskY = maskStartY + y;
                    Marshal.Copy(
                        IntPtr.Add(mask.Data, checked((int)((maskY * maskStride) + maskStartX))),
                        sourceRow,
                        0,
                        width);
                    for (int x = 0; x < width; x++)
                    {
                        if (sourceRow[x] == 0)
                        {
                            continue;
                        }

                        int offset = x * 4;
                        outputRow[offset + 2] = 255;
                        outputRow[offset + 3] = 255;
                    }

                    Marshal.Copy(outputRow, 0, data.Scan0 + (y * stride), outputRow.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private static Bitmap CreateLargeProcessedBinaryViewportOverlay(Cv.Mat visibleMask, int targetWidth, int targetHeight)
        {
            using (var scaledMask = new Cv.Mat())
            {
                if (visibleMask.Width > targetWidth || visibleMask.Height > targetHeight)
                {
                    Cv.Cv2.Resize(
                        visibleMask,
                        scaledMask,
                        new Cv.Size(targetWidth, targetHeight),
                        0,
                        0,
                        Cv.InterpolationFlags.Area);
                }
                else
                {
                    Cv.Cv2.Resize(
                        visibleMask,
                        scaledMask,
                        new Cv.Size(targetWidth, targetHeight),
                        0,
                        0,
                        Cv.InterpolationFlags.Nearest);
                }

                return CreateRedOverlayTileFromBinaryMask(
                    scaledMask,
                    Rectangle.Empty,
                    new Rectangle(0, 0, targetWidth, targetHeight));
            }
        }

        private static void DrawLargeProcessedOverlayTile(Graphics graphics, Bitmap overlay, Rectangle tileRect, float zoom, PointF offset)
        {
            int overlayWidth;
            int overlayHeight;
            try
            {
                overlayWidth = overlay != null ? overlay.Width : 0;
                overlayHeight = overlay != null ? overlay.Height : 0;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
                return;
            }

            if (overlayWidth <= 0 || overlayHeight <= 0 || tileRect.Width <= 0 || tileRect.Height <= 0 || zoom <= 0f)
            {
                return;
            }

            int left = (int)Math.Floor(offset.X + (tileRect.Left * zoom));
            int top = (int)Math.Floor(offset.Y + (tileRect.Top * zoom));
            int right = (int)Math.Ceiling(offset.X + (tileRect.Right * zoom));
            int bottom = (int)Math.Ceiling(offset.Y + (tileRect.Bottom * zoom));
            if (right <= left)
            {
                right = left + 1;
            }

            if (bottom <= top)
            {
                bottom = top + 1;
            }

            var destination = Rectangle.FromLTRB(left, top, right, bottom);
            var source = new Rectangle(0, 0, overlayWidth, overlayHeight);
            System.Drawing.Drawing2D.InterpolationMode previousInterpolation = graphics.InterpolationMode;
            System.Drawing.Drawing2D.PixelOffsetMode previousPixelOffset = graphics.PixelOffsetMode;
            try
            {
                // Masks are categorical data.  Bicubic/bilinear interpolation
                // invents red values between pixels and makes edge positions
                // look wider or shifted when zoomed.
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(overlay, destination, source, GraphicsUnit.Pixel);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                graphics.PixelOffsetMode = previousPixelOffset;
                graphics.InterpolationMode = previousInterpolation;
            }
        }

        private static void DrawLargeProcessedOverlayRegion(
            Graphics graphics,
            Bitmap overlay,
            Rectangle overlayBounds,
            Rectangle sourceRegion,
            float zoom,
            PointF offset)
        {
            if (overlay == null || overlayBounds.Width <= 0 || overlayBounds.Height <= 0 ||
                sourceRegion.Width <= 0 || sourceRegion.Height <= 0 || zoom <= 0f)
            {
                return;
            }

            Rectangle clippedRegion = Rectangle.Intersect(overlayBounds, sourceRegion);
            if (clippedRegion.Width <= 0 || clippedRegion.Height <= 0)
            {
                return;
            }

            int sourceLeft = Math.Max(
                0,
                Math.Min(overlay.Width - 1,
                    (int)Math.Floor((clippedRegion.Left - overlayBounds.Left) * (double)overlay.Width / overlayBounds.Width)));
            int sourceTop = Math.Max(
                0,
                Math.Min(overlay.Height - 1,
                    (int)Math.Floor((clippedRegion.Top - overlayBounds.Top) * (double)overlay.Height / overlayBounds.Height)));
            int sourceRight = Math.Max(
                sourceLeft + 1,
                Math.Min(overlay.Width,
                    (int)Math.Ceiling((clippedRegion.Right - overlayBounds.Left) * (double)overlay.Width / overlayBounds.Width)));
            int sourceBottom = Math.Max(
                sourceTop + 1,
                Math.Min(overlay.Height,
                    (int)Math.Ceiling((clippedRegion.Bottom - overlayBounds.Top) * (double)overlay.Height / overlayBounds.Height)));

            Rectangle source = Rectangle.FromLTRB(sourceLeft, sourceTop, sourceRight, sourceBottom);
            Rectangle destination = Rectangle.FromLTRB(
                (int)Math.Floor(offset.X + (clippedRegion.Left * zoom)),
                (int)Math.Floor(offset.Y + (clippedRegion.Top * zoom)),
                (int)Math.Ceiling(offset.X + (clippedRegion.Right * zoom)),
                (int)Math.Ceiling(offset.Y + (clippedRegion.Bottom * zoom)));
            if (destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            System.Drawing.Drawing2D.InterpolationMode previousInterpolation = graphics.InterpolationMode;
            System.Drawing.Drawing2D.PixelOffsetMode previousPixelOffset = graphics.PixelOffsetMode;
            try
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(overlay, destination, source, GraphicsUnit.Pixel);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                graphics.PixelOffsetMode = previousPixelOffset;
                graphics.InterpolationMode = previousInterpolation;
            }
        }

    }
}
