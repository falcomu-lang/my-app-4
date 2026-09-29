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
        // An explicit object-definition run should leave every upstream
        // preview inspectable, even when its tab is not currently selected.
        // The flag is consumed after the processed preview is applied.
        private bool objectDefinitionDependencyPreviewRequested;
        private string activeObjectDefinitionProcessingSignature;
        private string completedObjectDefinitionProcessingSignature;
        private string pendingObjectDefinitionProcessingSignature;

        private sealed class ObjectDefinitionDetectedObject
        {
            public int Number { get; set; }

            public Rectangle Bounds { get; set; }

            public double Area { get; set; }

            public List<int> SourceLabels { get; set; }

            public bool HasRotationGeometry { get; set; }

            public double RotationAngleDegrees { get; set; }

            public PointF RotationCenter { get; set; }

            public SizeF RotationSize { get; set; }

            public PointF[] RotationCorners { get; set; }
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

        private bool HasConfiguredObjectDefinitionBlockSource(ObjectDefinitionSettings definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.SourceId))
            {
                return false;
            }

            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            return string.Equals(sourceType, "ObjectJudgement", StringComparison.Ordinal) ||
                string.Equals(sourceType, "Group", StringComparison.Ordinal);
        }

        private bool HasConfiguredObjectDefinitionMaskSource(ObjectDefinitionSettings definition)
        {
            if (definition == null ||
                (!string.Equals(definition.SourceMaskMode, "Direct", StringComparison.Ordinal) &&
                 !string.Equals(definition.SourceMaskMode, "Composite", StringComparison.Ordinal)))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.SourceMaskPrimaryType) ||
                string.IsNullOrWhiteSpace(definition.SourceMaskPrimaryId))
            {
                return false;
            }

            if (string.Equals(definition.SourceMaskMode, "Direct", StringComparison.Ordinal))
            {
                return true;
            }

            string operation = string.IsNullOrWhiteSpace(definition.SourceMaskOperation)
                ? "None"
                : definition.SourceMaskOperation;
            return string.Equals(operation, "Not", StringComparison.Ordinal) ||
                string.Equals(operation, "None", StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(definition.SourceMaskSecondaryType) &&
                 !string.IsNullOrWhiteSpace(definition.SourceMaskSecondaryId));
        }

        private bool HasConfiguredObjectDefinitionRelationSource(ObjectDefinitionSettings definition)
        {
            return definition != null &&
                !string.IsNullOrWhiteSpace(definition.SourceRelationId) &&
                (string.Equals(definition.SourceRelationType, "Relation", StringComparison.Ordinal) ||
                 string.Equals(definition.SourceRelationType, "Group", StringComparison.Ordinal));
        }

        private bool HasConfiguredObjectDefinitionSource(ObjectDefinitionSettings definition)
        {
            return HasConfiguredObjectDefinitionMaskSource(definition) ||
                HasConfiguredObjectDefinitionBlockSource(definition) ||
                HasConfiguredObjectDefinitionRelationSource(definition);
        }

        private List<ImageRelationSettings> GetObjectDefinitionSourceRelations(
            ObjectDefinitionSettings definition)
        {
            var relations = new List<ImageRelationSettings>();
            if (!HasConfiguredObjectDefinitionRelationSource(definition))
            {
                return relations;
            }

            if (string.Equals(definition.SourceRelationType, "Relation", StringComparison.Ordinal))
            {
                ImageRelationSettings relation = systemParameters.ImageRelations.Find(
                    item => string.Equals(item.Id, definition.SourceRelationId, StringComparison.Ordinal));
                if (relation != null)
                {
                    relations.Add(relation);
                }

                return relations;
            }

            return GetImageRelationGroupRelations(definition.SourceRelationId);
        }

        private void StartObjectDefinitionProcessing(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null || !HasConfiguredObjectDefinitionSource(definition))
            {
                return;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            // An explicit object-definition command should make every upstream
            // stage available to inspect.  This requests the matching views,
            // while their own signatures prevent redundant processing.
            RequestObjectDefinitionDependencyDisplays(definition);
            if (HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                statusLabel.Text = definition.DisplayName + " 已處理";
                return;
            }

            if (string.Equals(
                    pendingObjectDefinitionProcessingSignature,
                    processingSignature,
                    StringComparison.Ordinal))
            {
                return;
            }

            pendingObjectDefinitionProcessingSignature = processingSignature;
            BeginInvoke(new Action(delegate
            {
                if (!string.Equals(
                        pendingObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return;
                }

                pendingObjectDefinitionProcessingSignature = null;
                StartObjectDefinitionProcessingCore(definitionId, processingSignature);
            }));
        }

        private void StartObjectDefinitionProcessingCore(
            string definitionId,
            string processingSignature)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            ResetDebugTimingMemo();
            AppendDebugTimingMemo(definition.DisplayName + " 開始處理");

            if (!HasConfiguredObjectDefinitionSource(definition))
            {
                statusLabel.Text = definition.DisplayName + " 尚未設定來源區塊或影像關聯";
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

                List<Rectangle> bitmapRois = OrderRoiRectanglesForVisibleArea(
                    GetValidObjectDefinitionBitmapRois(bitmap.Size));
                if (bitmapRois.Count == 0)
                {
                    bitmap.Dispose();
                    statusLabel.Text = "尚未設定有效的 ROI";
                    return;
                }

                StartObjectDefinitionBitmapProcessing(
                    definition,
                    bitmap,
                    bitmapRois,
                    processingSignature);
                return;
            }

            List<Rectangle> rois = OrderRoiRectanglesForVisibleArea(systemParameters.RoiRegions
                .Where(region => region.Bounds.Width > 0 && region.Bounds.Height > 0)
                .Select(region => region.Bounds)
                .ToList());
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
                activeObjectDefinitionProcessingSignature = processingSignature;
                completedObjectDefinitionProcessingSignature = null;
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

            ClearObjectDetectionFlatFieldMaskOverlays();
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
                    long contourElapsedMilliseconds = 0;
                    long rotationGeometryElapsedMilliseconds = 0;
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
                                long roiContourElapsedMilliseconds;
                                long roiRotationGeometryElapsedMilliseconds;
                                completedResults[resultKey] =
                                    CreateObjectDefinitionDetectedObjects(
                                        sourceMask,
                                        roi,
                                        definition,
                                        out retainedMask,
                                        out roiCclElapsedMilliseconds,
                                        out roiRetainedMaskElapsedMilliseconds,
                                        out roiMergeElapsedMilliseconds,
                                        out roiContourElapsedMilliseconds,
                                        out roiRotationGeometryElapsedMilliseconds);
                                completedSourceMasks[resultKey] = retainedMask;
                                cclElapsedMilliseconds += roiCclElapsedMilliseconds;
                                retainedMaskElapsedMilliseconds += roiRetainedMaskElapsedMilliseconds;
                                mergeElapsedMilliseconds += roiMergeElapsedMilliseconds;
                                contourElapsedMilliseconds += roiContourElapsedMilliseconds;
                                rotationGeometryElapsedMilliseconds += roiRotationGeometryElapsedMilliseconds;
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
                                    completedObjectDefinitionProcessingSignature = processingSignature;
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
                                        contourElapsedMilliseconds,
                                        definition.EnableRotationAnalysis,
                                                rotationGeometryElapsedMilliseconds,
                                                sourceCacheHitCount,
                                        sourceCacheMissCount);
                                    statusLabel.Text = definition.DisplayName + "：" +
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
                                    NotifyObjectDetectionParameterSourceProcessingCompleted(
                                        definition.Id,
                                        true,
                                        null);
                                    leftObjectsDisplayControl.InvalidateImageView();
                                    rightObjectsDisplayControl.InvalidateImageView();
                                    InvalidateBlockProcessingDisplays();
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
                                            NotifyObjectDetectionParameterSourceProcessingCompleted(
                                                definition.Id,
                                                false,
                                                ex.Message);
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
            List<Rectangle> rois,
            string processingSignature)
        {
            rois = OrderRoiRectanglesForVisibleArea(rois);
            int generation;
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                generation = objectDefinitionResultGeneration;
                activeObjectDefinitionResultId = definition.Id;
                activeObjectDefinitionProcessingSignature = processingSignature;
                completedObjectDefinitionProcessingSignature = null;
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

            ClearObjectDetectionFlatFieldMaskOverlays();
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
                    long contourElapsedMilliseconds = 0;
                    long rotationGeometryElapsedMilliseconds = 0;
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
                                    long roiContourElapsedMilliseconds;
                                    long roiRotationGeometryElapsedMilliseconds;
                                    completedResults[resultKey] =
                                        CreateObjectDefinitionDetectedObjects(
                                            sourceMask,
                                            roi,
                                            definition,
                                            out retainedMask,
                                            out roiCclElapsedMilliseconds,
                                            out roiRetainedMaskElapsedMilliseconds,
                                            out roiMergeElapsedMilliseconds,
                                            out roiContourElapsedMilliseconds,
                                            out roiRotationGeometryElapsedMilliseconds);
                                    completedSourceMasks[resultKey] = retainedMask;
                                    cclElapsedMilliseconds += roiCclElapsedMilliseconds;
                                    retainedMaskElapsedMilliseconds += roiRetainedMaskElapsedMilliseconds;
                                    mergeElapsedMilliseconds += roiMergeElapsedMilliseconds;
                                    contourElapsedMilliseconds += roiContourElapsedMilliseconds;
                                    rotationGeometryElapsedMilliseconds += roiRotationGeometryElapsedMilliseconds;
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
                                            objectDefinitionProcessingElapsedMilliseconds = elapsedMilliseconds;
                                            objectDefinitionSourceProcessingElapsedMilliseconds = sourceProcessingElapsedMilliseconds;
                                            objectDefinitionSourceRelationElapsedMilliseconds = 0;
                                            objectDefinitionSourceGrayElapsedMilliseconds = 0;
                                            objectDefinitionSourceImageProcessingElapsedMilliseconds = 0;
                                            objectDefinitionSourceMergeElapsedMilliseconds = 0;
                                            objectDefinitionSourceObjectProcessingElapsedMilliseconds = 0;
                                            objectDefinitionSourceCacheHitCount = 0;
                                            objectDefinitionSourceCacheMissCount = 0;
                                            objectDefinitionCclElapsedMilliseconds = cclElapsedMilliseconds;
                                            objectDefinitionRetainedMaskElapsedMilliseconds = retainedMaskElapsedMilliseconds;
                                            objectDefinitionMergeElapsedMilliseconds = mergeElapsedMilliseconds;
                                            completedObjectDefinitionProcessingSignature = processingSignature;
                                            long displayElapsedMilliseconds =
                                                RefreshVisibleObjectDefinitionDisplays();
                                            InvalidateBlockProcessingDisplays();
                                            AppendObjectDefinitionTimingMemo(
                                                definition.DisplayName,
                                                elapsedMilliseconds,
                                                displayElapsedMilliseconds,
                                                sourceProcessingElapsedMilliseconds,
                                                0,
                                                0,
                                                0,
                                                null,
                                                cclElapsedMilliseconds,
                                                retainedMaskElapsedMilliseconds,
                                                mergeElapsedMilliseconds,
                                                contourElapsedMilliseconds,
                                                definition.EnableRotationAnalysis,
                                                rotationGeometryElapsedMilliseconds,
                                                0,
                                                0);
                                            statusLabel.Text = definition.DisplayName + "：" +
                                                BuildObjectDefinitionTimingText(
                                                    elapsedMilliseconds,
                                                    displayElapsedMilliseconds,
                                                    sourceProcessingElapsedMilliseconds,
                                                    cclElapsedMilliseconds,
                                                    retainedMaskElapsedMilliseconds,
                                                    mergeElapsedMilliseconds);
                                            NotifyObjectDetectionParameterSourceProcessingCompleted(
                                                definition.Id,
                                                true,
                                                null);
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
                                            NotifyObjectDetectionParameterSourceProcessingCompleted(
                                                definition.Id,
                                                false,
                                                ex.Message);
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

        private void RequestObjectDefinitionDependencyDisplays(ObjectDefinitionSettings definition)
        {
            if (definition == null || !HasConfiguredObjectDefinitionSource(definition))
            {
                return;
            }

            // A source-MASK definition consumes already-built upstream masks.
            // Do not start a different dependency flow or rebuild its inputs.
            if (HasConfiguredObjectDefinitionMaskSource(definition))
            {
                return;
            }

            // An object definition can use an image relation directly. Start
            // that relation's complete preview chain so preprocessing and the
            // processed-image tab remain inspectable before the final CCL
            // result is shown.
            if (!HasConfiguredObjectDefinitionBlockSource(definition))
            {
                RequestObjectDefinitionRelationDependencyDisplays(definition);
                objectDefinitionDependencyPreviewRequested = rightOriginalDisplayControl == null ||
                    !rightOriginalDisplayControl.IsLargeImageMode;
                return;
            }

            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                List<ObjectJudgementSettings> objectJudgements = group == null
                    ? new List<ObjectJudgementSettings>()
                    : GetObjectJudgementsInGroup(group.Id);
                if (group == null || objectJudgements.Count == 0)
                {
                    return;
                }

                // This starts the block result as well as its relation,
                // preprocessing, and image-processing preview chain.
                ProcessObjectJudgementGroup(group.Id);
            }
            else
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
                if (objectJudgement == null)
                {
                    return;
                }

                // This starts the block result as well as its relation,
                // preprocessing, and image-processing preview chain.
                ProcessObjectJudgement(systemParameters.ObjectJudgements.IndexOf(objectJudgement));
            }

            objectDefinitionDependencyPreviewRequested = rightOriginalDisplayControl == null ||
                !rightOriginalDisplayControl.IsLargeImageMode;
            InvalidateBlockProcessingDisplays();
        }

        private void RequestObjectDefinitionRelationDependencyDisplays(
            ObjectDefinitionSettings definition)
        {
            List<ImageRelationSettings> relations = GetObjectDefinitionSourceRelations(definition);
            if (relations.Count == 0)
            {
                return;
            }

            if (string.Equals(definition.SourceRelationType, "Group", StringComparison.Ordinal))
            {
                ProcessImageRelationGroup(definition.SourceRelationId);
                return;
            }

            ImageRelationSettings relation = relations[0];
            int relationIndex = systemParameters.ImageRelations.IndexOf(relation);
            if (relationIndex >= 0)
            {
                ProcessImageRelation(relationIndex);
            }
        }

        private string CreateObjectDefinitionProcessingSignature(ObjectDefinitionSettings definition)
        {
            bool usesBlockSource = HasConfiguredObjectDefinitionBlockSource(definition);
            var parts = new List<string>
            {
                "object-definition-result",
                systemParameters.LastImagePath ?? string.Empty,
                imageSourceGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                preprocessedImageGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                definition == null ? string.Empty : definition.Id ?? string.Empty,
                definition == null ? string.Empty : definition.SourceType ?? string.Empty,
                definition == null ? string.Empty : definition.SourceId ?? string.Empty,
                "source-relation-type",
                usesBlockSource || definition == null ? string.Empty : definition.SourceRelationType ?? string.Empty,
                "source-relation-id",
                usesBlockSource || definition == null ? string.Empty : definition.SourceRelationId ?? string.Empty,
                "source-mask-mode",
                definition == null ? string.Empty : definition.SourceMaskMode ?? string.Empty,
                "source-mask-primary-type",
                definition == null ? string.Empty : definition.SourceMaskPrimaryType ?? string.Empty,
                "source-mask-primary-id",
                definition == null ? string.Empty : definition.SourceMaskPrimaryId ?? string.Empty,
                "source-mask-operation",
                definition == null ? string.Empty : definition.SourceMaskOperation ?? string.Empty,
                "source-mask-secondary-type",
                definition == null ? string.Empty : definition.SourceMaskSecondaryType ?? string.Empty,
                "source-mask-secondary-id",
                definition == null ? string.Empty : definition.SourceMaskSecondaryId ?? string.Empty
            };

            if (usesBlockSource && definition != null && string.Equals(definition.SourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                List<ObjectJudgementSettings> objectJudgements = group == null
                    ? new List<ObjectJudgementSettings>()
                    : GetObjectJudgementsInGroup(group.Id);
                parts.Add(CreateObjectJudgementGroupProcessingSignature(
                    definition.SourceId,
                    objectJudgements));
            }
            else if (usesBlockSource && definition != null)
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
                parts.Add(CreateObjectJudgementProcessingSignature(
                    objectJudgement,
                    objectJudgement == null
                        ? new List<ObjectJudgementProcessingSettings>()
                        : GetObjectJudgementProcessingChain(objectJudgement, -1)));
            }

            if (definition != null)
            {
                parts.Add(definition.Connectivity.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.MinArea.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.MaxArea.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.NumberingOrder ?? string.Empty);
                parts.Add(definition.MergeMethod ?? string.Empty);
                parts.Add(definition.MaxMergeDistance.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.GroupMinArea.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.GroupMaxArea.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.ResultBoxLineWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.ResultNumberFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(definition.EnableRotationAnalysis ? "rotation-on" : "rotation-off");
            }

            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle roi = roiRegion.Bounds;
                parts.Add(roi.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(roi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(roi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                parts.Add(roi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return string.Join("|", parts.ToArray());
        }

        private bool HasCompletedObjectDefinitionResult(
            ObjectDefinitionSettings definition,
            string processingSignature)
        {
            if (definition == null || string.IsNullOrWhiteSpace(processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!objectDefinitionProcessingRequested ||
                    !string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(activeObjectDefinitionProcessingSignature, processingSignature, StringComparison.Ordinal) ||
                    !string.Equals(completedObjectDefinitionProcessingSignature, processingSignature, StringComparison.Ordinal))
                {
                    return false;
                }

                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = roiRegion.Bounds;
                    if (roi.Width <= 0 || roi.Height <= 0)
                    {
                        continue;
                    }

                    string resultKey = CreateObjectDefinitionResultKey(definition.Id, roi);
                    if (!objectDefinitionResults.ContainsKey(resultKey) ||
                        !objectDefinitionSourceMasks.ContainsKey(resultKey))
                    {
                        return false;
                    }
                }

                return objectDefinitionResults.Count > 0;
            }
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
                activeObjectDefinitionProcessingSignature = null;
                completedObjectDefinitionProcessingSignature = null;
                pendingObjectDefinitionProcessingSignature = null;
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

            ClearObjectDetectionFlatFieldMaskOverlays();

            if (leftObjectsDisplayControl != null)
            {
                leftObjectsDisplayControl.InvalidateImageView();
            }

            if (rightObjectsDisplayControl != null)
            {
                rightObjectsDisplayControl.InvalidateImageView();
            }
        }


    }
}
