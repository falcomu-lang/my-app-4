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
        private readonly object objectDefinitionResultLock = new object();
        private readonly Dictionary<string, List<ObjectDefinitionDetectedObject>> objectDefinitionResults =
            new Dictionary<string, List<ObjectDefinitionDetectedObject>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Cv.Mat> objectDefinitionSourceMasks =
            new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
        private int objectDefinitionResultGeneration;
        private string activeObjectDefinitionResultId;
        private bool objectDefinitionProcessingRequested;
        private long objectDefinitionProcessingElapsedMilliseconds;
        private long objectDefinitionSourceProcessingElapsedMilliseconds;
        private long objectDefinitionSourceRelationElapsedMilliseconds;
        private long objectDefinitionSourceGrayElapsedMilliseconds;
        private long objectDefinitionSourceImageProcessingElapsedMilliseconds;
        private long objectDefinitionSourceMergeElapsedMilliseconds;
        private long objectDefinitionSourceObjectProcessingElapsedMilliseconds;
        private long objectDefinitionCclElapsedMilliseconds;
        private long objectDefinitionRetainedMaskElapsedMilliseconds;
        private long objectDefinitionMergeElapsedMilliseconds;
        private int objectDefinitionSourceCacheHitCount = -1;
        private int objectDefinitionSourceCacheMissCount = -1;
        private bool objectDefinitionDisplayTimePending;

        private sealed class ObjectDefinitionDetectedObject
        {
            public int Number { get; set; }

            public Rectangle Bounds { get; set; }

            public double Area { get; set; }

            public List<int> SourceLabels { get; set; }
        }

        private sealed class ObjectDefinitionComponentRegion
        {
            public int Label { get; set; }

            public int X { get; set; }

            public int Y { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }
        }

        private sealed class ObjectDefinitionSourceTiming
        {
            public long RelationSourceMilliseconds { get; set; }

            public long GrayPreparationMilliseconds { get; set; }

            public long ImageProcessingMilliseconds { get; set; }

            public long MaskMergeMilliseconds { get; set; }

            public long ObjectProcessingMilliseconds { get; set; }

            public long InitialCloneMilliseconds { get; set; }

            public List<string> ObjectProcessingDetails { get; private set; } =
                new List<string>();
        }

        private void StartObjectDefinitionProcessing(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            ResetDebugTimingMemo();
            AppendDebugTimingMemo(definition.DisplayName + " 開始處理");

            if (string.IsNullOrWhiteSpace(definition.SourceId))
            {
                statusLabel.Text = definition.DisplayName + " 尚未設定來源區塊";
                return;
            }

            LargeImageSource source = GetObjectDefinitionSharedImageSource();
            if (source == null)
            {
                Bitmap bitmap = rightOriginalDisplayControl == null
                    ? null
                    : rightOriginalDisplayControl.CloneImage();
                if (bitmap == null && leftOriginalDisplayControl != null)
                {
                    bitmap = leftOriginalDisplayControl.CloneImage();
                }

                if (bitmap == null)
                {
                    statusLabel.Text = "請先載入圖片";
                    return;
                }

                List<Rectangle> bitmapRois = GetValidObjectDefinitionBitmapRois(bitmap.Size);
                if (bitmapRois.Count == 0)
                {
                    bitmap.Dispose();
                    statusLabel.Text = "尚未設定有效的 ROI";
                    return;
                }

                StartObjectDefinitionBitmapProcessing(definition, bitmap, bitmapRois);
                return;
            }

            List<Rectangle> rois = systemParameters.RoiRegions
                .Where(region => region.Bounds.Width > 0 && region.Bounds.Height > 0)
                .Select(region => region.Bounds)
                .ToList();
            if (rois.Count == 0)
            {
                source.ReleaseReference();
                statusLabel.Text = "尚未設定有效的 ROI";
                return;
            }

            int generation;
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                generation = objectDefinitionResultGeneration;
                activeObjectDefinitionResultId = definition.Id;
                objectDefinitionProcessingRequested = true;
                objectDefinitionProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceRelationElapsedMilliseconds = 0;
                objectDefinitionSourceGrayElapsedMilliseconds = 0;
                objectDefinitionSourceImageProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceMergeElapsedMilliseconds = 0;
                objectDefinitionSourceObjectProcessingElapsedMilliseconds = 0;
                objectDefinitionCclElapsedMilliseconds = 0;
                objectDefinitionRetainedMaskElapsedMilliseconds = 0;
                objectDefinitionMergeElapsedMilliseconds = 0;
                objectDefinitionSourceCacheHitCount = 0;
                objectDefinitionSourceCacheMissCount = 0;
                objectDefinitionDisplayTimePending = true;
                objectDefinitionResults.Clear();
                DisposeObjectDefinitionSourceMasksUnsafe();
            }

            statusLabel.Text = definition.DisplayName + " 影像處理中...使用 OpenCV CCL";
            Task.Run(
                delegate
                {
                    var completedResults = new Dictionary<string, List<ObjectDefinitionDetectedObject>>(StringComparer.Ordinal);
                    var completedSourceMasks = new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
                    Dictionary<string, Cv.Mat> displaySourceMasks = null;
                    bool displaySourceMasksTransferred = false;
                    long sourceProcessingElapsedMilliseconds = 0;
                    long sourceRelationElapsedMilliseconds = 0;
                    long sourceGrayElapsedMilliseconds = 0;
                    long sourceImageProcessingElapsedMilliseconds = 0;
                    long sourceMergeElapsedMilliseconds = 0;
                    long sourceObjectProcessingElapsedMilliseconds = 0;
                    var sourceObjectProcessingDetails = new List<string>();
                    long cclElapsedMilliseconds = 0;
                    long retainedMaskElapsedMilliseconds = 0;
                    long mergeElapsedMilliseconds = 0;
                    int sourceCacheHitCount = 0;
                    int sourceCacheMissCount = 0;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        foreach (Rectangle roi in rois)
                        {
                            EnsureObjectDefinitionRequestIsCurrent(definition.Id, generation);
                            Stopwatch sourceStopwatch = Stopwatch.StartNew();
                            bool sourceCacheHit;
                            var sourceTiming = new ObjectDefinitionSourceTiming();
                            Cv.Mat sourceMask = CreateObjectDefinitionSourceMask(
                                source,
                                definition,
                                roi,
                                out sourceCacheHit,
                                sourceTiming);
                            sourceStopwatch.Stop();
                            if (sourceCacheHit)
                            {
                                sourceCacheHitCount++;
                            }
                            else
                            {
                                sourceCacheMissCount++;
                            }
                            sourceProcessingElapsedMilliseconds += sourceStopwatch.ElapsedMilliseconds;
                            sourceRelationElapsedMilliseconds += sourceTiming.RelationSourceMilliseconds;
                            sourceGrayElapsedMilliseconds += sourceTiming.GrayPreparationMilliseconds;
                            sourceImageProcessingElapsedMilliseconds += sourceTiming.ImageProcessingMilliseconds;
                            sourceMergeElapsedMilliseconds += sourceTiming.MaskMergeMilliseconds;
                            sourceObjectProcessingElapsedMilliseconds += sourceTiming.ObjectProcessingMilliseconds;
                            sourceObjectProcessingDetails.AddRange(sourceTiming.ObjectProcessingDetails);
                            using (sourceMask)
                            {
                                string resultKey = CreateObjectDefinitionResultKey(definition.Id, roi);
                                Cv.Mat retainedMask;
                                long roiCclElapsedMilliseconds;
                                long roiRetainedMaskElapsedMilliseconds;
                                long roiMergeElapsedMilliseconds;
                                completedResults[resultKey] =
                                    CreateObjectDefinitionDetectedObjects(
                                        sourceMask,
                                        roi,
                                        definition,
                                        out retainedMask,
                                        out roiCclElapsedMilliseconds,
                                        out roiRetainedMaskElapsedMilliseconds,
                                        out roiMergeElapsedMilliseconds);
                                completedSourceMasks[resultKey] = retainedMask;
                                cclElapsedMilliseconds += roiCclElapsedMilliseconds;
                                retainedMaskElapsedMilliseconds += roiRetainedMaskElapsedMilliseconds;
                                mergeElapsedMilliseconds += roiMergeElapsedMilliseconds;
                            }
                        }

                        List<ObjectDefinitionDetectedObject> allObjects = completedResults.Values
                            .SelectMany(items => items)
                            .ToList();
                        List<ObjectDefinitionDetectedObject> orderedObjects =
                            OrderObjectDefinitionDetectedObjects(allObjects, definition);
                        for (int index = 0; index < orderedObjects.Count; index++)
                        {
                            orderedObjects[index].Number = index + 1;
                        }

                        stopwatch.Stop();
                        long elapsedMilliseconds = Math.Max(1, stopwatch.ElapsedMilliseconds);
                        displaySourceMasks = completedSourceMasks;
                        completedSourceMasks = null;
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    lock (objectDefinitionResultLock)
                                    {
                                        if (generation != objectDefinitionResultGeneration ||
                                            !string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal))
                                        {
                                            DisposeObjectDefinitionSourceMasks(displaySourceMasks);
                                            displaySourceMasks = null;
                                            return;
                                        }

                                        objectDefinitionResults.Clear();
                                        foreach (KeyValuePair<string, List<ObjectDefinitionDetectedObject>> item in completedResults)
                                        {
                                            objectDefinitionResults[item.Key] = item.Value;
                                        }

                                        DisposeObjectDefinitionSourceMasksUnsafe();
                                        foreach (KeyValuePair<string, Cv.Mat> item in displaySourceMasks)
                                        {
                                            objectDefinitionSourceMasks[item.Key] = item.Value;
                                        }
                                        displaySourceMasks = null;
                                    }

                                    int count = completedResults.Values.Sum(items => items.Count);
                                    objectDefinitionProcessingElapsedMilliseconds = elapsedMilliseconds;
                                    objectDefinitionSourceProcessingElapsedMilliseconds = sourceProcessingElapsedMilliseconds;
                                    objectDefinitionSourceRelationElapsedMilliseconds = sourceRelationElapsedMilliseconds;
                                    objectDefinitionSourceGrayElapsedMilliseconds = sourceGrayElapsedMilliseconds;
                                    objectDefinitionSourceImageProcessingElapsedMilliseconds = sourceImageProcessingElapsedMilliseconds;
                                    objectDefinitionSourceMergeElapsedMilliseconds = sourceMergeElapsedMilliseconds;
                                    objectDefinitionSourceObjectProcessingElapsedMilliseconds = sourceObjectProcessingElapsedMilliseconds;
                                    objectDefinitionCclElapsedMilliseconds = cclElapsedMilliseconds;
                                    objectDefinitionRetainedMaskElapsedMilliseconds = retainedMaskElapsedMilliseconds;
                                    objectDefinitionMergeElapsedMilliseconds = mergeElapsedMilliseconds;
                                    objectDefinitionSourceCacheHitCount = sourceCacheHitCount;
                                    objectDefinitionSourceCacheMissCount = sourceCacheMissCount;
                                    long displayElapsedMilliseconds =
                                        RefreshVisibleObjectDefinitionDisplays();
                                    AppendObjectDefinitionTimingMemo(
                                        definition.DisplayName,
                                        elapsedMilliseconds,
                                        displayElapsedMilliseconds,
                                        sourceProcessingElapsedMilliseconds,
                                        sourceImageProcessingElapsedMilliseconds,
                                        sourceMergeElapsedMilliseconds,
                                        sourceObjectProcessingElapsedMilliseconds,
                                        sourceObjectProcessingDetails,
                                        cclElapsedMilliseconds,
                                        retainedMaskElapsedMilliseconds,
                                        mergeElapsedMilliseconds,
                                        sourceCacheHitCount,
                                        sourceCacheMissCount);
                                    statusLabel.Text = definition.DisplayName +
                                        " 已完成：OpenCV CCL，找到 " +
                                        count.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                        " 個物件，" +
                                        BuildObjectDefinitionTimingText(
                                            elapsedMilliseconds,
                                            displayElapsedMilliseconds,
                                            sourceProcessingElapsedMilliseconds,
                                            cclElapsedMilliseconds,
                                            retainedMaskElapsedMilliseconds,
                                            mergeElapsedMilliseconds,
                                            sourceRelationElapsedMilliseconds,
                                            sourceGrayElapsedMilliseconds,
                                            sourceImageProcessingElapsedMilliseconds,
                                            sourceMergeElapsedMilliseconds,
                                            sourceObjectProcessingElapsedMilliseconds,
                                            sourceCacheHitCount,
                                            sourceCacheMissCount);
                                    leftObjectsDisplayControl.InvalidateImageView();
                                    rightObjectsDisplayControl.InvalidateImageView();
                                }));
                        displaySourceMasksTransferred = true;
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
                                        if (generation == objectDefinitionResultGeneration &&
                                            string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal))
                                        {
                                            statusLabel.Text = definition.DisplayName +
                                                " 物件編號失敗：" + ex.Message;
                                        }
                                    }));
                        }
                        catch (InvalidOperationException)
                        {
                            // The form may close while a background CCL is finishing.
                        }
                    }
                    finally
                    {
                        DisposeObjectDefinitionSourceMasks(completedSourceMasks);
                        if (!displaySourceMasksTransferred)
                        {
                            DisposeObjectDefinitionSourceMasks(displaySourceMasks);
                        }
                        source.ReleaseReference();
                    }
                });
        }

        private List<Rectangle> GetValidObjectDefinitionBitmapRois(Size imageSize)
        {
            Rectangle imageBounds = new Rectangle(Point.Empty, imageSize);
            return systemParameters.RoiRegions
                .Select(region => Rectangle.Intersect(region.Bounds, imageBounds))
                .Where(roi => roi.Width > 0 && roi.Height > 0)
                .ToList();
        }

        private void StartObjectDefinitionBitmapProcessing(
            ObjectDefinitionSettings definition,
            Bitmap original,
            List<Rectangle> rois)
        {
            int generation;
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                generation = objectDefinitionResultGeneration;
                activeObjectDefinitionResultId = definition.Id;
                objectDefinitionProcessingRequested = true;
                objectDefinitionProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceProcessingElapsedMilliseconds = 0;
                objectDefinitionCclElapsedMilliseconds = 0;
                objectDefinitionRetainedMaskElapsedMilliseconds = 0;
                objectDefinitionMergeElapsedMilliseconds = 0;
                objectDefinitionDisplayTimePending = true;
                objectDefinitionResults.Clear();
                DisposeObjectDefinitionSourceMasksUnsafe();
            }

            statusLabel.Text = definition.DisplayName + " 影像處理中...使用 OpenCV CCL";
            Task.Run(
                delegate
                {
                    var completedResults = new Dictionary<string, List<ObjectDefinitionDetectedObject>>(StringComparer.Ordinal);
                    var completedSourceMasks = new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
                    Dictionary<string, Cv.Mat> displaySourceMasks = null;
                    bool displaySourceMasksTransferred = false;
                    Bitmap leftResult = null;
                    Bitmap rightResult = null;
                    long sourceProcessingElapsedMilliseconds = 0;
                    long cclElapsedMilliseconds = 0;
                    long retainedMaskElapsedMilliseconds = 0;
                    long mergeElapsedMilliseconds = 0;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        using (Cv.Mat originalGray = CreateOpenCvGrayMat(original))
                        {
                            foreach (Rectangle roi in rois)
                            {
                                EnsureObjectDefinitionRequestIsCurrent(definition.Id, generation);
                                Stopwatch sourceStopwatch = Stopwatch.StartNew();
                                Cv.Mat sourceMask = CreateObjectDefinitionSourceMask(
                                    original,
                                    originalGray,
                                    definition,
                                    roi);
                                sourceStopwatch.Stop();
                                sourceProcessingElapsedMilliseconds += sourceStopwatch.ElapsedMilliseconds;
                                using (sourceMask)
                                {
                                    string resultKey = CreateObjectDefinitionResultKey(definition.Id, roi);
                                    Cv.Mat retainedMask;
                                    long roiCclElapsedMilliseconds;
                                    long roiRetainedMaskElapsedMilliseconds;
                                    long roiMergeElapsedMilliseconds;
                                    completedResults[resultKey] =
                                        CreateObjectDefinitionDetectedObjects(
                                            sourceMask,
                                            roi,
                                            definition,
                                            out retainedMask,
                                            out roiCclElapsedMilliseconds,
                                            out roiRetainedMaskElapsedMilliseconds,
                                            out roiMergeElapsedMilliseconds);
                                    completedSourceMasks[resultKey] = retainedMask;
                                    cclElapsedMilliseconds += roiCclElapsedMilliseconds;
                                    retainedMaskElapsedMilliseconds += roiRetainedMaskElapsedMilliseconds;
                                    mergeElapsedMilliseconds += roiMergeElapsedMilliseconds;
                                }
                            }
                        }

                        List<ObjectDefinitionDetectedObject> allObjects = completedResults.Values
                            .SelectMany(items => items)
                            .ToList();
                        List<ObjectDefinitionDetectedObject> orderedObjects =
                            OrderObjectDefinitionDetectedObjects(allObjects, definition);
                        for (int index = 0; index < orderedObjects.Count; index++)
                        {
                            orderedObjects[index].Number = index + 1;
                        }

                        leftResult = CreateObjectDefinitionAnnotatedBitmap(
                            original,
                            completedResults,
                            completedSourceMasks,
                            definition,
                            rois);
                        rightResult = new Bitmap(leftResult);
                        stopwatch.Stop();
                        long elapsedMilliseconds = Math.Max(1, stopwatch.ElapsedMilliseconds);
                        displaySourceMasks = completedSourceMasks;
                        completedSourceMasks = null;
                        Bitmap displayLeftResult = leftResult;
                        Bitmap displayRightResult = rightResult;
                        leftResult = null;
                        rightResult = null;
                        try
                        {
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        bool isCurrent;
                                        lock (objectDefinitionResultLock)
                                        {
                                            isCurrent = generation == objectDefinitionResultGeneration &&
                                                string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal);
                                            if (isCurrent)
                                            {
                                                objectDefinitionResults.Clear();
                                            foreach (KeyValuePair<string, List<ObjectDefinitionDetectedObject>> item in completedResults)
                                            {
                                                objectDefinitionResults[item.Key] = item.Value;
                                            }

                                            DisposeObjectDefinitionSourceMasksUnsafe();
                                            foreach (KeyValuePair<string, Cv.Mat> item in displaySourceMasks)
                                            {
                                                objectDefinitionSourceMasks[item.Key] = item.Value;
                                            }
                                            displaySourceMasks = null;
                                            }
                                        }

                                        if (!isCurrent)
                                        {
                                            DisposeObjectDefinitionSourceMasks(displaySourceMasks);
                                            displaySourceMasks = null;
                                            displayLeftResult.Dispose();
                                            displayRightResult.Dispose();
                                            displayLeftResult = null;
                                            displayRightResult = null;
                                            return;
                                        }

                                        try
                                        {
                                            leftObjectsDisplayControl.SetDisplayImage(displayLeftResult, true);
                                            displayLeftResult = null;
                                            rightObjectsDisplayControl.SetDisplayImage(displayRightResult, true);
                                            displayRightResult = null;
                                            int count = completedResults.Values.Sum(items => items.Count);
                                    objectDefinitionProcessingElapsedMilliseconds = elapsedMilliseconds;
                                            objectDefinitionSourceProcessingElapsedMilliseconds = sourceProcessingElapsedMilliseconds;
                                            objectDefinitionCclElapsedMilliseconds = cclElapsedMilliseconds;
                                            objectDefinitionRetainedMaskElapsedMilliseconds = retainedMaskElapsedMilliseconds;
                                            objectDefinitionMergeElapsedMilliseconds = mergeElapsedMilliseconds;
                                            long displayElapsedMilliseconds =
                                                RefreshVisibleObjectDefinitionDisplays();
                                            statusLabel.Text = definition.DisplayName +
                                                " 已完成：OpenCV CCL，找到 " +
                                                count.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                                " 個物件，" +
                                                BuildObjectDefinitionTimingText(
                                                    elapsedMilliseconds,
                                                    displayElapsedMilliseconds,
                                                    sourceProcessingElapsedMilliseconds,
                                                    cclElapsedMilliseconds,
                                                    retainedMaskElapsedMilliseconds,
                                                    mergeElapsedMilliseconds);
                                        }
                                        catch
                                        {
                                            if (displayLeftResult != null)
                                            {
                                                displayLeftResult.Dispose();
                                                displayLeftResult = null;
                                            }

                                            if (displayRightResult != null)
                                            {
                                                displayRightResult.Dispose();
                                                displayRightResult = null;
                                            }

                                            throw;
                                        }
                                    }));
                            displaySourceMasksTransferred = true;
                        }
                        catch
                        {
                            displayLeftResult.Dispose();
                            displayRightResult.Dispose();
                            throw;
                        }
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
                                        if (generation == objectDefinitionResultGeneration &&
                                            string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal))
                                        {
                                            statusLabel.Text = definition.DisplayName +
                                                " 物件編號失敗：" + ex.Message;
                                        }
                                    }));
                        }
                        catch (InvalidOperationException)
                        {
                            // The form may close while a background CCL is finishing.
                        }
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

                        DisposeObjectDefinitionSourceMasks(completedSourceMasks);
                        if (!displaySourceMasksTransferred)
                        {
                            DisposeObjectDefinitionSourceMasks(displaySourceMasks);
                        }

                        original.Dispose();
                    }
                });
        }

        private LargeImageSource GetObjectDefinitionSharedImageSource()
        {
            LargeImageSource source = rightOriginalDisplayControl == null
                ? null
                : rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source != null)
            {
                return source;
            }

            source = leftOriginalDisplayControl == null
                ? null
                : leftOriginalDisplayControl.GetSharedLargeImageSource();
            if (source != null)
            {
                return source;
            }

            source = rightObjectsDisplayControl == null
                ? null
                : rightObjectsDisplayControl.GetSharedLargeImageSource();
            if (source != null)
            {
                return source;
            }

            return leftObjectsDisplayControl == null
                ? null
                : leftObjectsDisplayControl.GetSharedLargeImageSource();
        }

        private void EnsureObjectDefinitionRequestIsCurrent(string definitionId, int generation)
        {
            lock (objectDefinitionResultLock)
            {
                if (!objectDefinitionProcessingRequested ||
                    generation != objectDefinitionResultGeneration ||
                    !string.Equals(activeObjectDefinitionResultId, definitionId, StringComparison.Ordinal))
                {
                    throw new OperationCanceledException("物件編號請求已過期");
                }
            }
        }

        private void InvalidateObjectDefinitionResults()
        {
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                activeObjectDefinitionResultId = null;
                objectDefinitionProcessingRequested = false;
                objectDefinitionProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceRelationElapsedMilliseconds = 0;
                objectDefinitionSourceGrayElapsedMilliseconds = 0;
                objectDefinitionSourceImageProcessingElapsedMilliseconds = 0;
                objectDefinitionSourceMergeElapsedMilliseconds = 0;
                objectDefinitionSourceObjectProcessingElapsedMilliseconds = 0;
                objectDefinitionCclElapsedMilliseconds = 0;
                objectDefinitionRetainedMaskElapsedMilliseconds = 0;
                objectDefinitionMergeElapsedMilliseconds = 0;
                objectDefinitionDisplayTimePending = false;
                objectDefinitionResults.Clear();
                DisposeObjectDefinitionSourceMasksUnsafe();
            }

            if (leftObjectsDisplayControl != null)
            {
                leftObjectsDisplayControl.InvalidateImageView();
            }

            if (rightObjectsDisplayControl != null)
            {
                rightObjectsDisplayControl.InvalidateImageView();
            }
        }

        private long RefreshVisibleObjectDefinitionDisplays()
        {
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
            string cacheText = sourceCacheHitCount >= 0 && sourceCacheMissCount >= 0
                ? " || 來源快取：命中 " +
                    sourceCacheHitCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    "／未命中 " +
                    sourceCacheMissCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
            string sourceDetailText = sourceImageProcessingElapsedMilliseconds >= 0
                ? " || 來源影像處理時間：" +
                    Math.Max(0, sourceImageProcessingElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " ms || 來源遮罩合併時間：" +
                    Math.Max(0, sourceMergeElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " ms || 區塊後處理時間：" +
                    Math.Max(0, sourceObjectProcessingElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " ms"
                : string.Empty;
            return "來源區塊處理時間：" +
                Math.Max(0, sourceProcessingElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || CCL時間：" +
                Math.Max(0, cclElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 保留Mask時間：" +
                Math.Max(0, retainedMaskElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || Merge by Distance時間：" +
                Math.Max(0, mergeElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 影像處理總時間：" +
                Math.Max(0, processingElapsedMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " ms || 顯示時間：" + displayText + sourceDetailText + cacheText;
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
            AppendDebugTimingMemo("保留 MASK：" + retainedMaskElapsedMilliseconds + " ms");
            AppendDebugTimingMemo("Merge by Distance：" + mergeElapsedMilliseconds + " ms");
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

        private void DisposeObjectDefinitionSourceMasksUnsafe()
        {
            foreach (Cv.Mat mask in objectDefinitionSourceMasks.Values)
            {
                if (mask != null)
                {
                    mask.Dispose();
                }
            }

            objectDefinitionSourceMasks.Clear();
        }

        private static void DisposeObjectDefinitionSourceMasks(
            IDictionary<string, Cv.Mat> masks)
        {
            if (masks == null)
            {
                return;
            }

            foreach (Cv.Mat mask in masks.Values)
            {
                if (mask != null)
                {
                    mask.Dispose();
                }
            }

            masks.Clear();
        }

        private Cv.Mat CreateObjectDefinitionSourceMask(
            LargeImageSource source,
            ObjectDefinitionSettings definition,
            Rectangle roi,
            out bool sourceCacheHit,
            ObjectDefinitionSourceTiming timing = null)
        {
            Cv.Mat cachedMask;
            if (TryGetCachedObjectDefinitionSourceMask(definition, roi, out cachedMask))
            {
                sourceCacheHit = true;
                return cachedMask;
            }

            sourceCacheHit = false;

            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                if (group == null)
                {
                    throw new InvalidOperationException("物件組來源群組不存在");
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                if (objectJudgements.Count == 0)
                {
                    throw new InvalidOperationException("物件組來源群組沒有區塊");
                }

                Cv.Mat createdGroupMask = null;
                try
                {
                    createdGroupMask = CreateLargeObjectJudgementGroupMask(
                        source,
                        objectJudgements,
                        roi,
                        timing);
                    return StoreObjectDefinitionSourceMaskInCache(
                        definition,
                        roi,
                        ref createdGroupMask);
                }
                catch
                {
                    if (createdGroupMask != null)
                    {
                        createdGroupMask.Dispose();
                    }

                    throw;
                }
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
            if (objectJudgement == null)
            {
                throw new InvalidOperationException("物件組來源區塊不存在");
            }

            List<ObjectJudgementProcessingSettings> processingSteps =
                GetObjectJudgementProcessingChain(objectJudgement, -1);
            Cv.Mat createdMask = null;
            try
            {
                using (Cv.Mat baseMask = CreateLargeObjectJudgementBaseMask(
                    source,
                    objectJudgement,
                    roi,
                    timing))
                {
                    Stopwatch objectProcessingStopwatch = timing == null
                        ? null
                        : Stopwatch.StartNew();
                    createdMask = ApplyObjectJudgementProcessingOpenCv(
                        baseMask,
                        processingSteps,
                        timing);
                    if (objectProcessingStopwatch != null)
                    {
                        objectProcessingStopwatch.Stop();
                        timing.ObjectProcessingMilliseconds += objectProcessingStopwatch.ElapsedMilliseconds;
                    }
                }

                return StoreObjectDefinitionSourceMaskInCache(
                    definition,
                    roi,
                    ref createdMask);
            }
            catch
            {
                if (createdMask != null)
                {
                    createdMask.Dispose();
                }

                throw;
            }
        }

        private bool TryGetCachedObjectDefinitionSourceMask(
            ObjectDefinitionSettings definition,
            Rectangle roi,
            out Cv.Mat cachedMask)
        {
            cachedMask = null;
            if (definition == null || roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            string maskKey;
            Dictionary<string, Cv.Mat> cache;

            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                if (group == null)
                {
                    return false;
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                if (objectJudgements.Count == 0)
                {
                    return false;
                }

                string processingSignature = CreateObjectJudgementGroupProcessingSignature(
                    group.Id,
                    objectJudgements);
                maskKey = CreateObjectJudgementGroupMaskKey(processingSignature, roi);
                cache = objectJudgementGroupLargeMasks;
            }
            else
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
                if (objectJudgement == null)
                {
                    return false;
                }

                List<ObjectJudgementProcessingSettings> processingSteps =
                    GetObjectJudgementProcessingChain(objectJudgement, -1);
                return TryGetCachedObjectJudgementMask(
                    objectJudgement,
                    processingSteps,
                    roi,
                    out cachedMask);
            }

            lock (objectJudgementMaskLock)
            {
                Cv.Mat mask;
                if (!cache.TryGetValue(maskKey, out mask) ||
                    mask == null ||
                    mask.Empty() ||
                    mask.Rows != roi.Height ||
                    mask.Cols != roi.Width)
                {
                    return false;
                }

                // The cached mask owns the pixel buffer. A full Clone here can
                // copy hundreds of megabytes before CCL even starts. Keep a
                // shared OpenCV header view instead; disposing the view does
                // not duplicate or accumulate the cached pixel buffer.
                cachedMask = CreateLargeRoiMatView(
                    mask,
                    new Rectangle(0, 0, mask.Cols, mask.Rows));
                return true;
            }
        }

        private bool TryGetCachedObjectJudgementMask(
            ObjectJudgementSettings objectJudgement,
            IEnumerable<ObjectJudgementProcessingSettings> processingSteps,
            Rectangle roi,
            out Cv.Mat cachedMask)
        {
            cachedMask = null;
            if (objectJudgement == null || roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            string maskKey = CreateObjectJudgementMaskKey(
                objectJudgement,
                processingSteps,
                roi);
            lock (objectJudgementMaskLock)
            {
                Cv.Mat mask;
                if (!objectJudgementLargeMasks.TryGetValue(maskKey, out mask) ||
                    mask == null ||
                    mask.Empty() ||
                    mask.Rows != roi.Height ||
                    mask.Cols != roi.Width)
                {
                    return false;
                }

                cachedMask = CreateLargeRoiMatView(
                    mask,
                    new Rectangle(0, 0, mask.Cols, mask.Rows));
                return true;
            }
        }

        private Cv.Mat StoreObjectDefinitionSourceMaskInCache(
            ObjectDefinitionSettings definition,
            Rectangle roi,
            ref Cv.Mat sourceMask)
        {
            if (sourceMask == null)
            {
                throw new ArgumentNullException("sourceMask");
            }

            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            string maskKey;
            Dictionary<string, Cv.Mat> cache;
            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                if (group == null)
                {
                    throw new InvalidOperationException("物件組來源群組不存在");
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                maskKey = CreateObjectJudgementGroupMaskKey(
                    CreateObjectJudgementGroupProcessingSignature(group.Id, objectJudgements),
                    roi);
                cache = objectJudgementGroupLargeMasks;
            }
            else
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
                if (objectJudgement == null)
                {
                    throw new InvalidOperationException("物件組來源區塊不存在");
                }

                maskKey = CreateObjectJudgementMaskKey(
                    objectJudgement,
                    GetObjectJudgementProcessingChain(objectJudgement, -1),
                    roi);
                cache = objectJudgementLargeMasks;
            }

            lock (objectJudgementMaskLock)
            {
                Cv.Mat previous;
                if (cache.TryGetValue(maskKey, out previous) && previous != null)
                {
                    previous.Dispose();
                }

                Cv.Mat view = CreateLargeRoiMatView(
                    sourceMask,
                    new Rectangle(0, 0, sourceMask.Cols, sourceMask.Rows));
                try
                {
                    cache[maskKey] = sourceMask;
                    sourceMask = null;
                    return view;
                }
                catch
                {
                    view.Dispose();
                    throw;
                }
            }
        }

        private Cv.Mat CreateObjectDefinitionSourceMask(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectDefinitionSettings definition,
            Rectangle roi)
        {
            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                if (group == null)
                {
                    throw new InvalidOperationException("物件組來源群組不存在");
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                if (objectJudgements.Count == 0)
                {
                    throw new InvalidOperationException("物件組來源群組沒有區塊");
                }

                var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
                try
                {
                    foreach (ObjectJudgementSettings objectJudgement in objectJudgements)
                    {
                        List<ObjectJudgementProcessingSettings> processingSteps =
                            GetObjectJudgementProcessingChain(objectJudgement, -1);
                        using (Cv.Mat baseMask = CreateObjectJudgementBaseMaskFromBitmap(
                            original,
                            originalGray,
                            objectJudgement,
                            roi))
                        using (Cv.Mat objectMask = ApplyObjectJudgementProcessingOpenCv(
                            baseMask,
                            processingSteps))
                        {
                            Cv.Cv2.BitwiseOr(combined, objectMask, combined);
                        }
                    }

                    return combined;
                }
                catch
                {
                    combined.Dispose();
                    throw;
                }
            }

            ObjectJudgementSettings objectJudgementSettings = systemParameters.ObjectJudgements.Find(
                item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
            if (objectJudgementSettings == null)
            {
                throw new InvalidOperationException("物件組來源區塊不存在");
            }

            List<ObjectJudgementProcessingSettings> steps =
                GetObjectJudgementProcessingChain(objectJudgementSettings, -1);
            using (Cv.Mat baseMask = CreateObjectJudgementBaseMaskFromBitmap(
                original,
                originalGray,
                objectJudgementSettings,
                roi))
            {
                return ApplyObjectJudgementProcessingOpenCv(baseMask, steps);
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
                        graphics.DrawRectangle(pen, item.Bounds);
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

        private static List<ObjectDefinitionDetectedObject> CreateObjectDefinitionDetectedObjects(
            Cv.Mat sourceMask,
            Rectangle roi,
            ObjectDefinitionSettings definition,
            out Cv.Mat retainedMask,
            out long cclElapsedMilliseconds,
            out long retainedMaskElapsedMilliseconds,
            out long mergeElapsedMilliseconds)
        {
            cclElapsedMilliseconds = 0;
            retainedMaskElapsedMilliseconds = 0;
            mergeElapsedMilliseconds = 0;
            if (sourceMask == null || sourceMask.Empty())
            {
                retainedMask = new Cv.Mat();
                return new List<ObjectDefinitionDetectedObject>();
            }

            retainedMask = null;
            using (var labels = new Cv.Mat())
            using (var stats = new Cv.Mat())
            using (var centroids = new Cv.Mat())
            {
                Cv.PixelConnectivity connectivity = definition.Connectivity == 4
                    ? Cv.PixelConnectivity.Connectivity4
                    : Cv.PixelConnectivity.Connectivity8;
                Stopwatch cclStopwatch = Stopwatch.StartNew();
                int labelCount = Cv.Cv2.ConnectedComponentsWithStats(
                    sourceMask,
                    labels,
                    stats,
                    centroids,
                    connectivity,
                    Cv.MatType.CV_32SC1);
                var result = new List<ObjectDefinitionDetectedObject>();
                var accepted = new bool[labelCount];
                var acceptedComponents = new List<ObjectDefinitionComponentRegion>();
                var rejectedComponents = new List<ObjectDefinitionComponentRegion>();
                long acceptedBoundsArea = 0;
                for (int label = 1; label < labelCount; label++)
                {
                    int area = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area);
                    int x = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Left);
                    int y = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Top);
                    int width = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Width);
                    int height = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Height);
                    var component = new ObjectDefinitionComponentRegion
                    {
                        Label = label,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    };
                    if (definition.MinArea > 0 && area < definition.MinArea)
                    {
                        rejectedComponents.Add(component);
                        continue;
                    }

                    if (definition.MaxArea > 0 && area > definition.MaxArea)
                    {
                        rejectedComponents.Add(component);
                        continue;
                    }

                    accepted[label] = true;
                    acceptedComponents.Add(component);
                    acceptedBoundsArea += (long)width * height;
                    result.Add(new ObjectDefinitionDetectedObject
                    {
                        Bounds = new Rectangle(roi.X + x, roi.Y + y, width, height),
                        Area = area,
                        SourceLabels = new List<int> { label }
                    });
                }
                cclStopwatch.Stop();
                cclElapsedMilliseconds = cclStopwatch.ElapsedMilliseconds;

                if (string.Equals(definition.MergeMethod, "Distance", StringComparison.Ordinal) &&
                    definition.MaxMergeDistance > 0)
                {
                    Stopwatch mergeStopwatch = Stopwatch.StartNew();
                    result = MergeObjectDefinitionDetectedObjects(
                        result,
                        definition.MaxMergeDistance);
                    mergeStopwatch.Stop();
                    mergeElapsedMilliseconds = mergeStopwatch.ElapsedMilliseconds;
                }

                result = FilterObjectDefinitionGroupsByArea(
                    result,
                    definition.GroupMinArea,
                    definition.GroupMaxArea);
                var retainedLabels = new HashSet<int>(
                    result
                        .SelectMany(item => item.SourceLabels ?? Enumerable.Empty<int>()));
                for (int label = 1; label < accepted.Length; label++)
                {
                    accepted[label] = retainedLabels.Contains(label);
                }

                Stopwatch retainedMaskStopwatch = Stopwatch.StartNew();
                long imageArea = (long)sourceMask.Width * sourceMask.Height;
                if (rejectedComponents.Count == 0 && retainedLabels.Count == acceptedComponents.Count)
                {
                    retainedMask = sourceMask.Clone();
                }
                else
                {
                    rejectedComponents = acceptedComponents
                        .Where(component => !retainedLabels.Contains(component.Label))
                        .Concat(rejectedComponents)
                        .ToList();
                    long finalRejectedBoundsArea = rejectedComponents.Sum(
                        component => (long)component.Width * component.Height);
                    if (finalRejectedBoundsArea * 2 < acceptedBoundsArea && result.Count > 0)
                    {
                        retainedMask = CreateObjectDefinitionRetainedMaskByCloneAndRemove(
                            sourceMask,
                            labels,
                            rejectedComponents);
                    }
                    else
                    {
                        retainedMask = new Cv.Mat(
                            sourceMask.Rows,
                            sourceMask.Cols,
                            Cv.MatType.CV_8UC1,
                            Cv.Scalar.All(0));
                        if (result.Count > 0 && acceptedBoundsArea * 4 < imageArea * 3)
                        {
                            CreateObjectDefinitionRetainedMaskByAcceptedComponents(
                                labels,
                                retainedMask,
                                acceptedComponents.Where(component => retainedLabels.Contains(component.Label)));
                        }
                        else if (result.Count > 0)
                        {
                            CreateObjectDefinitionRetainedMaskByRows(
                                labels,
                                retainedMask,
                                accepted);
                        }
                    }
                }
                retainedMaskStopwatch.Stop();
                retainedMaskElapsedMilliseconds = retainedMaskStopwatch.ElapsedMilliseconds;

                return result;
            }
        }

        private static List<ObjectDefinitionDetectedObject> FilterObjectDefinitionGroupsByArea(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            double minArea,
            double maxArea)
        {
            IEnumerable<ObjectDefinitionDetectedObject> filtered = objects ??
                Enumerable.Empty<ObjectDefinitionDetectedObject>();
            if (minArea > 0)
            {
                filtered = filtered.Where(item => item.Area >= minArea);
            }

            if (maxArea > 0)
            {
                filtered = filtered.Where(item => item.Area <= maxArea);
            }

            return filtered.ToList();
        }

        private static Cv.Mat CreateObjectDefinitionRetainedMaskByCloneAndRemove(
            Cv.Mat sourceMask,
            Cv.Mat labels,
            IEnumerable<ObjectDefinitionComponentRegion> rejectedComponents)
        {
            Cv.Mat retainedMask = sourceMask.Clone();
            try
            {
                foreach (ObjectDefinitionComponentRegion component in rejectedComponents)
                {
                    using (var labelRoi = new Cv.Mat(
                        labels,
                        new Cv.Rect(
                            component.X,
                            component.Y,
                            component.Width,
                            component.Height)))
                    using (var componentMask = new Cv.Mat())
                    using (var retainedRoi = new Cv.Mat(
                        retainedMask,
                        new Cv.Rect(
                            component.X,
                            component.Y,
                            component.Width,
                            component.Height)))
                    {
                        Cv.Cv2.Compare(
                            labelRoi,
                            component.Label,
                            componentMask,
                            Cv.CmpType.EQ);
                        retainedRoi.SetTo(Cv.Scalar.All(0), componentMask);
                    }
                }

                return retainedMask;
            }
            catch
            {
                retainedMask.Dispose();
                throw;
            }
        }

        private static void CreateObjectDefinitionRetainedMaskByAcceptedComponents(
            Cv.Mat labels,
            Cv.Mat retainedMask,
            IEnumerable<ObjectDefinitionComponentRegion> acceptedComponents)
        {
            foreach (ObjectDefinitionComponentRegion component in acceptedComponents)
            {
                using (var labelRoi = new Cv.Mat(
                    labels,
                    new Cv.Rect(
                        component.X,
                        component.Y,
                        component.Width,
                        component.Height)))
                using (var componentMask = new Cv.Mat())
                using (var retainedRoi = new Cv.Mat(
                    retainedMask,
                    new Cv.Rect(
                        component.X,
                        component.Y,
                        component.Width,
                        component.Height)))
                {
                    Cv.Cv2.Compare(
                        labelRoi,
                        component.Label,
                        componentMask,
                        Cv.CmpType.EQ);
                    Cv.Cv2.BitwiseOr(
                        retainedRoi,
                        componentMask,
                        retainedRoi);
                }
            }
        }

        private static void CreateObjectDefinitionRetainedMaskByRows(
            Cv.Mat labels,
            Cv.Mat retainedMask,
            bool[] accepted)
        {
            int[] labelsRow = new int[labels.Width];
            byte[] retainedRow = new byte[labels.Width];
            long labelsStride = labels.Step();
            long retainedStride = retainedMask.Step();
            for (int y = 0; y < labels.Height; y++)
            {
                Marshal.Copy(
                    GetObjectDefinitionMatRowPointer(labels, y, labelsStride),
                    labelsRow,
                    0,
                    labelsRow.Length);
                Array.Clear(retainedRow, 0, retainedRow.Length);
                for (int x = 0; x < labelsRow.Length; x++)
                {
                    int label = labelsRow[x];
                    if (label > 0 && label < accepted.Length && accepted[label])
                    {
                        retainedRow[x] = 255;
                    }
                }

                Marshal.Copy(
                    retainedRow,
                    0,
                    GetObjectDefinitionMatRowPointer(retainedMask, y, retainedStride),
                    retainedRow.Length);
            }
        }

        private static IntPtr GetObjectDefinitionMatRowPointer(
            Cv.Mat mat,
            int row,
            long stride)
        {
            long address = mat.Data.ToInt64() + ((long)row * stride);
            return new IntPtr(address);
        }

        private static List<ObjectDefinitionDetectedObject> OrderObjectDefinitionDetectedObjects(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            ObjectDefinitionSettings definition)
        {
            IEnumerable<ObjectDefinitionDetectedObject> ordered = objects ?? Enumerable.Empty<ObjectDefinitionDetectedObject>();
            if (string.Equals(definition.NumberingOrder, "LeftToRightTopToBottom", StringComparison.Ordinal))
            {
                ordered = ordered.OrderBy(item => item.Bounds.Left).ThenBy(item => item.Bounds.Top);
            }
            else if (string.Equals(definition.NumberingOrder, "AreaDescending", StringComparison.Ordinal))
            {
                ordered = ordered.OrderByDescending(item => item.Area).ThenBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left);
            }
            else if (string.Equals(definition.NumberingOrder, "AreaAscending", StringComparison.Ordinal))
            {
                ordered = ordered.OrderBy(item => item.Area).ThenBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left);
            }
            else
            {
                ordered = ordered.OrderBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left);
            }

            return ordered.ToList();
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

        private static List<ObjectDefinitionDetectedObject> MergeObjectDefinitionDetectedObjects(
            List<ObjectDefinitionDetectedObject> objects,
            int maxDistance)
        {
            if (objects == null || objects.Count < 2 || maxDistance <= 0)
            {
                return objects ?? new List<ObjectDefinitionDetectedObject>();
            }

            int[] parents = new int[objects.Count];
            for (int index = 0; index < parents.Length; index++)
            {
                parents[index] = index;
            }

            var orderedIndexes = Enumerable.Range(0, objects.Count)
                .OrderBy(index => objects[index].Bounds.Left)
                .ToList();
            var activeIndexes = new List<int>();
            long maxDistanceSquared = (long)maxDistance * maxDistance;

            foreach (int currentIndex in orderedIndexes)
            {
                Rectangle currentBounds = objects[currentIndex].Bounds;
                for (int activeIndex = activeIndexes.Count - 1; activeIndex >= 0; activeIndex--)
                {
                    int previousIndex = activeIndexes[activeIndex];
                    if (objects[previousIndex].Bounds.Right + maxDistance < currentBounds.Left)
                    {
                        activeIndexes.RemoveAt(activeIndex);
                    }
                }

                foreach (int previousIndex in activeIndexes)
                {
                    if (GetRectangleGapDistanceSquared(
                            objects[previousIndex].Bounds,
                            currentBounds) <= maxDistanceSquared)
                    {
                        UnionObjectDefinitionParents(parents, previousIndex, currentIndex);
                    }
                }

                activeIndexes.Add(currentIndex);
            }

            var merged = new Dictionary<int, ObjectDefinitionDetectedObject>();
            for (int index = 0; index < objects.Count; index++)
            {
                int root = FindObjectDefinitionParent(parents, index);
                ObjectDefinitionDetectedObject current;
                if (!merged.TryGetValue(root, out current))
                {
                    merged[root] = new ObjectDefinitionDetectedObject
                    {
                        Bounds = objects[index].Bounds,
                        Area = objects[index].Area,
                        SourceLabels = new List<int>(
                            objects[index].SourceLabels ?? Enumerable.Empty<int>())
                    };
                    continue;
                }

                current.Bounds = Rectangle.Union(current.Bounds, objects[index].Bounds);
                current.Area += objects[index].Area;
                if (objects[index].SourceLabels != null)
                {
                    current.SourceLabels.AddRange(objects[index].SourceLabels);
                }
            }

            return merged.Values.ToList();
        }

        private static long GetRectangleGapDistanceSquared(Rectangle first, Rectangle second)
        {
            long dx = 0;
            if (first.Right < second.Left)
            {
                dx = second.Left - first.Right;
            }
            else if (second.Right < first.Left)
            {
                dx = first.Left - second.Right;
            }

            long dy = 0;
            if (first.Bottom < second.Top)
            {
                dy = second.Top - first.Bottom;
            }
            else if (second.Bottom < first.Top)
            {
                dy = first.Top - second.Bottom;
            }

            return (dx * dx) + (dy * dy);
        }

        private static int FindObjectDefinitionParent(int[] parents, int index)
        {
            int root = index;
            while (parents[root] != root)
            {
                root = parents[root];
            }

            while (parents[index] != index)
            {
                int next = parents[index];
                parents[index] = root;
                index = next;
            }

            return root;
        }

        private static void UnionObjectDefinitionParents(int[] parents, int first, int second)
        {
            int firstRoot = FindObjectDefinitionParent(parents, first);
            int secondRoot = FindObjectDefinitionParent(parents, second);
            if (firstRoot != secondRoot)
            {
                parents[secondRoot] = firstRoot;
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

            string definitionId = GetObjectDefinitionId(
                functionListBox.SelectedIndex,
                functionListBox.SelectedItem as string);
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
                    float width = Math.Max(1f, item.Bounds.Width * e.Zoom);
                    float height = Math.Max(1f, item.Bounds.Height * e.Zoom);
                    e.Graphics.DrawRectangle(pen, x, y, width, height);
                    e.Graphics.DrawString(
                        item.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        font,
                        brush,
                        x + 2f,
                        y + 2f);
                }
            }
        }
    }
}
