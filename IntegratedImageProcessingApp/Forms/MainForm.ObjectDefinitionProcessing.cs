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
            if (HasConfiguredObjectDefinitionMaskSource(definition))
            {
                Cv.Mat configuredMask;
                sourceCacheHit = TryResolveConfiguredObjectDefinitionMask(
                    source,
                    definition,
                    roi,
                    out configuredMask,
                    timing);
                if (configuredMask == null)
                {
                    throw new InvalidOperationException("來源 MASK 尚未建立，請先處理來源項目");
                }

                return configuredMask;
            }

            Cv.Mat cachedMask;
            if (TryGetCachedObjectDefinitionSourceMask(definition, roi, out cachedMask))
            {
                sourceCacheHit = true;
                return cachedMask;
            }

            sourceCacheHit = false;

            if (!HasConfiguredObjectDefinitionBlockSource(definition))
            {
                return CreateLargeObjectDefinitionRelationMask(source, definition, roi, timing);
            }

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
                    createdMask = ApplyObjectJudgementProcessingOpenCvAndCache(
                        objectJudgement,
                        baseMask,
                        processingSteps,
                        roi,
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
            if (HasConfiguredObjectDefinitionMaskSource(definition))
            {
                return TryResolveConfiguredObjectDefinitionMask(
                    null,
                    definition,
                    roi,
                    out cachedMask,
                    null);
            }

            if (!HasConfiguredObjectDefinitionBlockSource(definition) ||
                roi.Width <= 0 ||
                roi.Height <= 0)
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

        private bool TryResolveConfiguredObjectDefinitionMask(
            LargeImageSource source,
            ObjectDefinitionSettings definition,
            Rectangle roi,
            out Cv.Mat mask,
            ObjectDefinitionSourceTiming timing)
        {
            mask = null;
            if (!HasConfiguredObjectDefinitionMaskSource(definition) ||
                roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            Cv.Mat primary;
            if (!TryGetCachedObjectDefinitionMaskSource(
                source,
                definition.SourceMaskPrimaryType,
                definition.SourceMaskPrimaryId,
                roi,
                out primary,
                timing))
            {
                return false;
            }

            string operation = string.IsNullOrWhiteSpace(definition.SourceMaskOperation)
                ? "None"
                : definition.SourceMaskOperation;
            if (string.Equals(definition.SourceMaskMode, "Direct", StringComparison.Ordinal) ||
                string.Equals(operation, "None", StringComparison.Ordinal))
            {
                mask = primary;
                return true;
            }

            if (string.Equals(operation, "Not", StringComparison.Ordinal))
            {
                var inverted = new Cv.Mat();
                try
                {
                    Cv.Cv2.BitwiseNot(primary, inverted);
                    mask = inverted;
                    return true;
                }
                finally
                {
                    primary.Dispose();
                }
            }

            Cv.Mat secondary;
            if (!TryGetCachedObjectDefinitionMaskSource(
                source,
                definition.SourceMaskSecondaryType,
                definition.SourceMaskSecondaryId,
                roi,
                out secondary))
            {
                primary.Dispose();
                return false;
            }

            if (primary.Rows != secondary.Rows || primary.Cols != secondary.Cols)
            {
                primary.Dispose();
                secondary.Dispose();
                throw new InvalidOperationException("來源 MASK 尺寸不一致，無法進行 MASK 運算");
            }

            var combined = new Cv.Mat();
            try
            {
                if (string.Equals(operation, "Or", StringComparison.Ordinal))
                {
                    Cv.Cv2.BitwiseOr(primary, secondary, combined);
                }
                else if (string.Equals(operation, "And", StringComparison.Ordinal))
                {
                    Cv.Cv2.BitwiseAnd(primary, secondary, combined);
                }
                else if (string.Equals(operation, "Subtract", StringComparison.Ordinal))
                {
                    using (var invertedSecondary = new Cv.Mat())
                    {
                        Cv.Cv2.BitwiseNot(secondary, invertedSecondary);
                        Cv.Cv2.BitwiseAnd(primary, invertedSecondary, combined);
                    }
                }
                else if (string.Equals(operation, "Xor", StringComparison.Ordinal))
                {
                    Cv.Cv2.BitwiseXor(primary, secondary, combined);
                }
                else
                {
                    primary.Dispose();
                    secondary.Dispose();
                    combined.Dispose();
                    return false;
                }

                mask = combined;
                combined = null;
                return true;
            }
            finally
            {
                primary.Dispose();
                secondary.Dispose();
                if (combined != null)
                {
                    combined.Dispose();
                }
            }
        }

        private bool TryGetCachedObjectDefinitionMaskSource(
            LargeImageSource source,
            string sourceType,
            string sourceId,
            Rectangle roi,
            out Cv.Mat mask,
            ObjectDefinitionSourceTiming timing = null)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            if (string.Equals(sourceType, "ObjectJudgement", StringComparison.Ordinal))
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (objectJudgement == null)
                {
                    return false;
                }

                return TryGetCachedObjectJudgementMask(
                    objectJudgement,
                    GetObjectJudgementProcessingChain(objectJudgement, -1),
                    roi,
                    out mask);
            }

            if (string.Equals(sourceType, "ObjectJudgementGroup", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(sourceId);
                if (group == null)
                {
                    return false;
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                if (objectJudgements.Count == 0)
                {
                    return false;
                }

                string signature = CreateObjectJudgementGroupProcessingSignature(
                    group.Id,
                    objectJudgements);
                string key = CreateObjectJudgementGroupMaskKey(signature, roi);
                lock (objectJudgementMaskLock)
                {
                    Cv.Mat cached;
                    if (!objectJudgementGroupLargeMasks.TryGetValue(key, out cached) ||
                        cached == null || cached.Empty() ||
                        cached.Rows != roi.Height || cached.Cols != roi.Width)
                    {
                        return false;
                    }

                    mask = CreateLargeRoiMatView(
                        cached,
                        new Rectangle(0, 0, cached.Cols, cached.Rows));
                    return true;
                }
            }

            if (string.Equals(sourceType, "ImageProcessingStep", StringComparison.Ordinal))
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (step == null)
                {
                    return false;
                }

                return TryGetCachedLargeProcessedStepMask(roi, step, out mask) ||
                    TryGetCachedProcessedBinaryMask(
                        roi,
                        step,
                        CreateImageProcessingSourceNamespace(),
                        out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroup", StringComparison.Ordinal))
            {
                ImageProcessingGroupSettings group = FindImageProcessingGroup(sourceId);
                var steps = new List<ImageProcessingStepSettings>();
                if (group == null)
                {
                    return false;
                }

                CollectImageProcessingGroupSteps(group.Id, steps);
                return TryCreateCachedImageProcessingMask(roi, steps, out mask);
            }

            if (string.Equals(sourceType, "Relation", StringComparison.Ordinal))
            {
                ImageRelationSettings relation = systemParameters.ImageRelations.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                return relation != null && TryCreateCachedImageRelationMask(
                    roi,
                    new[] { relation },
                    out mask);
            }

            if (string.Equals(sourceType, "RelationGroup", StringComparison.Ordinal))
            {
                ImageRelationGroupSettings group = FindImageRelationGroup(sourceId);
                return group != null && TryCreateCachedImageRelationMask(
                    roi,
                    GetImageRelationGroupRelations(group.Id),
                    out mask);
            }

            return false;
        }

        private bool TryCreateCachedImageRelationMask(
            Rectangle roi,
            IEnumerable<ImageRelationSettings> relations,
            out Cv.Mat mask)
        {
            mask = null;
            var relationList = (relations ?? Enumerable.Empty<ImageRelationSettings>()).ToList();
            if (relationList.Count == 0)
            {
                return false;
            }

            var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                foreach (ImageRelationSettings relation in relationList)
                {
                    List<ImageProcessingStepSettings> steps = GetImageProcessingStepsForRelation(relation);
                    Cv.Mat relationMask;
                    if (!TryCreateCachedImageProcessingMask(
                        roi,
                        steps,
                        out relationMask,
                        CreateImageRelationSourceNamespace(relation)))
                    {
                        return false;
                    }

                    using (relationMask)
                    {
                        Cv.Cv2.BitwiseOr(combined, relationMask, combined);
                    }
                }

                mask = combined;
                combined = null;
                return true;
            }
            finally
            {
                if (combined != null)
                {
                    combined.Dispose();
                }
            }
        }

        private bool TryCreateCachedImageProcessingMask(
            Rectangle roi,
            IEnumerable<ImageProcessingStepSettings> steps,
            out Cv.Mat mask,
            string sourceNamespace = null)
        {
            mask = null;
            List<ImageProcessingStepSettings> orderedSteps = (steps ??
                Enumerable.Empty<ImageProcessingStepSettings>()).Where(
                    step => step != null && IsBinaryMaskProcessingMethod(step.Method)).ToList();
            if (orderedSteps.Count == 0)
            {
                return false;
            }

            var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                string namespaceValue = sourceNamespace ?? CreateImageProcessingSourceNamespace();
                foreach (ImageProcessingStepSettings step in orderedSteps)
                {
                    Cv.Mat next;
                    if (!TryGetCachedLargeProcessedStepMask(roi, step, out next))
                    {
                        if (!TryGetCachedProcessedBinaryMask(
                            roi,
                            step,
                            namespaceValue,
                            out next))
                        {
                            return false;
                        }
                    }

                    using (next)
                    {
                        Cv.Cv2.BitwiseOr(combined, next, combined);
                    }
                }

                mask = combined;
                combined = null;
                return true;
            }
            finally
            {
                if (combined != null)
                {
                    combined.Dispose();
                }
            }
        }

        private bool TryGetCachedLargeProcessedStepMask(
            Rectangle roi,
            ImageProcessingStepSettings step,
            out Cv.Mat mask)
        {
            mask = null;
            if (step == null || roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            string key = CreateLargeProcessedMaskKey(roi, step);
            lock (largeProcessedMaskLock)
            {
                Cv.Mat cached;
                if (!largeProcessedBinaryMasks.TryGetValue(key, out cached) ||
                    cached == null || cached.Empty() ||
                    cached.Rows != roi.Height || cached.Cols != roi.Width)
                {
                    return false;
                }

                mask = CreateLargeRoiMatView(
                    cached,
                    new Rectangle(0, 0, cached.Cols, cached.Rows));
                return true;
            }
        }

        private bool TryResolveConfiguredObjectDefinitionMaskFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectDefinitionSettings definition,
            Rectangle roi,
            out Cv.Mat mask)
        {
            mask = null;
            if (original == null || originalGray == null ||
                !HasConfiguredObjectDefinitionMaskSource(definition))
            {
                return false;
            }

            Cv.Mat primary;
            if (!TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                original,
                originalGray,
                definition.SourceMaskPrimaryType,
                definition.SourceMaskPrimaryId,
                roi,
                out primary))
            {
                return false;
            }

            string operation = string.IsNullOrWhiteSpace(definition.SourceMaskOperation)
                ? "None"
                : definition.SourceMaskOperation;
            if (string.Equals(definition.SourceMaskMode, "Direct", StringComparison.Ordinal) ||
                string.Equals(operation, "None", StringComparison.Ordinal))
            {
                mask = primary;
                return true;
            }

            if (string.Equals(operation, "Not", StringComparison.Ordinal))
            {
                var inverted = new Cv.Mat();
                try
                {
                    Cv.Cv2.BitwiseNot(primary, inverted);
                    mask = inverted;
                    return true;
                }
                finally
                {
                    primary.Dispose();
                }
            }

            Cv.Mat secondary;
            if (!TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                original,
                originalGray,
                definition.SourceMaskSecondaryType,
                definition.SourceMaskSecondaryId,
                roi,
                out secondary))
            {
                primary.Dispose();
                return false;
            }

            if (primary.Rows != secondary.Rows || primary.Cols != secondary.Cols)
            {
                primary.Dispose();
                secondary.Dispose();
                throw new InvalidOperationException("來源 MASK 尺寸不一致，無法進行 MASK 運算");
            }

            var combined = new Cv.Mat();
            try
            {
                if (string.Equals(operation, "Or", StringComparison.Ordinal))
                {
                    Cv.Cv2.BitwiseOr(primary, secondary, combined);
                }
                else if (string.Equals(operation, "And", StringComparison.Ordinal))
                {
                    Cv.Cv2.BitwiseAnd(primary, secondary, combined);
                }
                else if (string.Equals(operation, "Subtract", StringComparison.Ordinal))
                {
                    using (var invertedSecondary = new Cv.Mat())
                    {
                        Cv.Cv2.BitwiseNot(secondary, invertedSecondary);
                        Cv.Cv2.BitwiseAnd(primary, invertedSecondary, combined);
                    }
                }
                else if (string.Equals(operation, "Xor", StringComparison.Ordinal))
                {
                    Cv.Cv2.BitwiseXor(primary, secondary, combined);
                }
                else
                {
                    return false;
                }

                mask = combined;
                combined = null;
                return true;
            }
            finally
            {
                primary.Dispose();
                secondary.Dispose();
                if (combined != null)
                {
                    combined.Dispose();
                }
            }
        }

        private bool TryGetCachedObjectDefinitionMaskSourceFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            string sourceType,
            string sourceId,
            Rectangle roi,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            if (string.Equals(sourceType, "ObjectJudgement", StringComparison.Ordinal) ||
                string.Equals(sourceType, "ObjectJudgementGroup", StringComparison.Ordinal))
            {
                // Small-image processing keeps its completed red result as a
                // bitmap. Reuse that result instead of running the object
                // processing chain again.
                if (latestObjectJudgementImage == null ||
                    (string.Equals(sourceType, "ObjectJudgement", StringComparison.Ordinal) &&
                     !string.Equals(activeObjectJudgementId, sourceId, StringComparison.Ordinal)) ||
                    (string.Equals(sourceType, "ObjectJudgementGroup", StringComparison.Ordinal) &&
                     !string.Equals(activeObjectJudgementGroupId, sourceId, StringComparison.Ordinal)))
                {
                    return false;
                }

                mask = CreateMaskFromSmallObjectJudgementImage(latestObjectJudgementImage, roi);
                return mask != null;
            }

            if (string.Equals(sourceType, "ImageProcessingStep", StringComparison.Ordinal))
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                return step != null && TryCreateCachedImageProcessingMaskFromBitmap(
                    original,
                    originalGray,
                    roi,
                    new[] { step },
                    CreateImageProcessingSourceNamespace(),
                    out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroup", StringComparison.Ordinal))
            {
                ImageProcessingGroupSettings group = FindImageProcessingGroup(sourceId);
                var steps = new List<ImageProcessingStepSettings>();
                if (group == null)
                {
                    return false;
                }

                CollectImageProcessingGroupSteps(group.Id, steps);
                return TryCreateCachedImageProcessingMaskFromBitmap(
                    original,
                    originalGray,
                    roi,
                    steps,
                    CreateImageProcessingSourceNamespace(),
                    out mask);
            }

            IEnumerable<ImageRelationSettings> relations = null;
            if (string.Equals(sourceType, "Relation", StringComparison.Ordinal))
            {
                ImageRelationSettings relation = systemParameters.ImageRelations.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                relations = relation == null
                    ? Enumerable.Empty<ImageRelationSettings>()
                    : new[] { relation };
            }
            else if (string.Equals(sourceType, "RelationGroup", StringComparison.Ordinal))
            {
                ImageRelationGroupSettings group = FindImageRelationGroup(sourceId);
                relations = group == null
                    ? Enumerable.Empty<ImageRelationSettings>()
                    : GetImageRelationGroupRelations(group.Id);
            }

            if (relations == null)
            {
                return false;
            }

            var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                List<ImageRelationSettings> relationList = relations.ToList();
                if (relationList.Count == 0)
                {
                    return false;
                }

                foreach (ImageRelationSettings relation in relationList)
                {
                    using (Bitmap relationBitmap = CreateRelationSourceBitmap(original, relation))
                    using (Cv.Mat relationGray = CreateOpenCvGrayMat(relationBitmap))
                    {
                        Cv.Mat relationMask;
                        if (!TryCreateCachedImageProcessingMaskFromBitmap(
                            relationBitmap,
                            relationGray,
                            roi,
                            GetImageProcessingStepsForRelation(relation),
                            CreateImageRelationSourceNamespace(relation),
                            out relationMask))
                        {
                            return false;
                        }

                        using (relationMask)
                        {
                            Cv.Cv2.BitwiseOr(combined, relationMask, combined);
                        }
                    }
                }

                mask = combined;
                combined = null;
                return true;
            }
            finally
            {
                if (combined != null)
                {
                    combined.Dispose();
                }
            }
        }

        private bool TryCreateCachedImageProcessingMaskFromBitmap(
            Bitmap sourceBitmap,
            Cv.Mat sourceGray,
            Rectangle roi,
            IEnumerable<ImageProcessingStepSettings> steps,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            var orderedSteps = (steps ?? Enumerable.Empty<ImageProcessingStepSettings>())
                .Where(step => step != null && IsBinaryMaskProcessingMethod(step.Method))
                .ToList();
            if (orderedSteps.Count == 0)
            {
                return false;
            }

            var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                foreach (ImageProcessingStepSettings step in orderedSteps)
                {
                    Cv.Mat next;
                    if (!TryGetCachedProcessedBinaryMask(
                        roi,
                        step,
                        sourceNamespace,
                        out next))
                    {
                        return false;
                    }

                    using (next)
                    {
                        Cv.Cv2.BitwiseOr(combined, next, combined);
                    }
                }

                mask = combined;
                combined = null;
                return true;
            }
            finally
            {
                if (combined != null)
                {
                    combined.Dispose();
                }
            }
        }

        private static Cv.Mat CreateMaskFromSmallObjectJudgementImage(Bitmap image, Rectangle roi)
        {
            if (image == null || roi.Width <= 0 || roi.Height <= 0 ||
                roi.Right > image.Width || roi.Bottom > image.Height)
            {
                return null;
            }

            var mask = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                for (int y = 0; y < roi.Height; y++)
                {
                    for (int x = 0; x < roi.Width; x++)
                    {
                        Color color = image.GetPixel(roi.X + x, roi.Y + y);
                        if (color.R > 200 && color.G < 80 && color.B < 80)
                        {
                            mask.Set<byte>(y, x, 255);
                        }
                    }
                }

                return mask;
            }
            catch
            {
                mask.Dispose();
                throw;
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

            if (!HasConfiguredObjectDefinitionBlockSource(definition))
            {
                throw new InvalidOperationException("影像關聯來源不可寫入區塊來源快取");
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

        private void StoreObjectJudgementProcessingMaskCache(
            ObjectJudgementSettings objectJudgement,
            IList<ObjectJudgementProcessingSettings> processingSteps,
            Rectangle roi,
            Cv.Mat mask)
        {
            if (objectJudgement == null || mask == null || mask.Empty() ||
                roi.Width <= 0 || roi.Height <= 0)
            {
                return;
            }

            string maskKey = CreateObjectJudgementMaskKey(
                objectJudgement,
                processingSteps,
                roi);
            Cv.Mat cached = mask.Clone();
            try
            {
                lock (objectJudgementMaskLock)
                {
                    Cv.Mat previous;
                    if (objectJudgementLargeMasks.TryGetValue(maskKey, out previous) &&
                        previous != null)
                    {
                        previous.Dispose();
                    }

                    objectJudgementLargeMasks[maskKey] = cached;
                    cached = null;
                }
            }
            finally
            {
                if (cached != null)
                {
                    cached.Dispose();
                }
            }
        }

        private Cv.Mat CreateObjectDefinitionSourceMask(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectDefinitionSettings definition,
            Rectangle roi)
        {
            if (HasConfiguredObjectDefinitionMaskSource(definition))
            {
                Cv.Mat configuredMask;
                if (!TryResolveConfiguredObjectDefinitionMaskFromBitmap(
                    original,
                    originalGray,
                    definition,
                    roi,
                    out configuredMask))
                {
                    throw new InvalidOperationException("來源 MASK 尚未建立，請先處理來源項目");
                }

                return configuredMask;
            }

            if (!HasConfiguredObjectDefinitionBlockSource(definition))
            {
                return CreateObjectDefinitionRelationMaskFromBitmap(
                    original,
                    originalGray,
                    definition,
                    roi);
            }

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

        private Cv.Mat CreateLargeObjectDefinitionRelationMask(
            LargeImageSource source,
            ObjectDefinitionSettings definition,
            Rectangle roi,
            ObjectDefinitionSourceTiming timing)
        {
            List<ImageRelationSettings> relations = GetObjectDefinitionSourceRelations(definition);
            if (relations.Count == 0)
            {
                throw new InvalidOperationException("物件定義的影像關聯來源不存在");
            }

            var combined = new Cv.Mat(
                roi.Height,
                roi.Width,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0));
            try
            {
                foreach (ImageRelationSettings relation in relations)
                {
                    List<ImageProcessingStepSettings> steps =
                        GetImageProcessingStepsForRelation(relation);
                    if (steps.Count == 0)
                    {
                        throw new InvalidOperationException("影像關聯尚未設定影像處理");
                    }

                    Stopwatch relationSourceStopwatch = timing == null
                        ? null
                        : Stopwatch.StartNew();
                    LargeImageSource relationSource = GetLargeRelationSource(source, relation);
                    if (relationSourceStopwatch != null)
                    {
                        relationSourceStopwatch.Stop();
                        timing.RelationSourceMilliseconds += relationSourceStopwatch.ElapsedMilliseconds;
                    }

                    try
                    {
                        Stopwatch grayStopwatch = timing == null
                            ? null
                            : Stopwatch.StartNew();
                        using (Cv.Mat gray = GetOrCreateLargeRoiOpenCvGrayCache(relationSource, roi))
                        {
                            if (grayStopwatch != null)
                            {
                                grayStopwatch.Stop();
                                timing.GrayPreparationMilliseconds += grayStopwatch.ElapsedMilliseconds;
                            }

                            Stopwatch processingStopwatch = timing == null
                                ? null
                                : Stopwatch.StartNew();
                            using (Cv.Mat relationMask = CreateCombinedImageProcessingGroupMask(
                                gray,
                                roi,
                                steps,
                                CreateImageRelationSourceNamespace(relation)))
                            {
                                if (processingStopwatch != null)
                                {
                                    processingStopwatch.Stop();
                                    timing.ImageProcessingMilliseconds += processingStopwatch.ElapsedMilliseconds;
                                }

                                Stopwatch mergeStopwatch = timing == null
                                    ? null
                                    : Stopwatch.StartNew();
                                Cv.Cv2.BitwiseOr(combined, relationMask, combined);
                                if (mergeStopwatch != null)
                                {
                                    mergeStopwatch.Stop();
                                    timing.MaskMergeMilliseconds += mergeStopwatch.ElapsedMilliseconds;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (relationSource != null)
                        {
                            relationSource.ReleaseReference();
                        }
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

        private Cv.Mat CreateObjectDefinitionRelationMaskFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectDefinitionSettings definition,
            Rectangle roi)
        {
            List<ImageRelationSettings> relations = GetObjectDefinitionSourceRelations(definition);
            if (relations.Count == 0)
            {
                throw new InvalidOperationException("物件定義的影像關聯來源不存在");
            }

            var combined = new Cv.Mat(
                roi.Height,
                roi.Width,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0));
            try
            {
                foreach (ImageRelationSettings relation in relations)
                {
                    List<ImageProcessingStepSettings> steps =
                        GetImageProcessingStepsForRelation(relation);
                    if (steps.Count == 0)
                    {
                        throw new InvalidOperationException("影像關聯尚未設定影像處理");
                    }

                    Cv.Mat gray = null;
                    Bitmap relationSource = null;
                    try
                    {
                        if (string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
                        {
                            gray = new Cv.Mat(
                                originalGray,
                                new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height));
                        }
                        else
                        {
                            relationSource = CreateRelationSourceBitmap(original, relation);
                            if (relationSource == null)
                            {
                                throw new InvalidOperationException("影像關聯來源影像不存在");
                            }

                            using (Cv.Mat relationGray = CreateOpenCvGrayMat(relationSource))
                            {
                                gray = new Cv.Mat(
                                    relationGray,
                                    new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height));
                            }
                        }

                        using (gray)
                        using (Cv.Mat relationMask = CreateCombinedImageProcessingGroupMask(
                            gray,
                            roi,
                            steps,
                            CreateImageRelationSourceNamespace(relation)))
                        {
                            Cv.Cv2.BitwiseOr(combined, relationMask, combined);
                        }
                    }
                    finally
                    {
                        if (relationSource != null)
                        {
                            relationSource.Dispose();
                        }
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

        private static List<ObjectDefinitionDetectedObject> CreateObjectDefinitionDetectedObjects(
            Cv.Mat sourceMask,
            Rectangle roi,
            ObjectDefinitionSettings definition,
            out Cv.Mat retainedMask,
            out long cclElapsedMilliseconds,
            out long retainedMaskElapsedMilliseconds,
            out long mergeElapsedMilliseconds,
            out long contourElapsedMilliseconds,
            out long rotationGeometryElapsedMilliseconds)
        {
            cclElapsedMilliseconds = 0;
            retainedMaskElapsedMilliseconds = 0;
            mergeElapsedMilliseconds = 0;
            contourElapsedMilliseconds = 0;
            rotationGeometryElapsedMilliseconds = 0;
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

                if (definition.EnableRotationAnalysis && result.Count > 0)
                {
                    Stopwatch contourStopwatch = Stopwatch.StartNew();
                    Dictionary<int, Cv.Point[]> labelContours =
                        CreateObjectDefinitionLabelContours(
                            labels,
                            acceptedComponents,
                            retainedLabels);
                    contourStopwatch.Stop();
                    contourElapsedMilliseconds = contourStopwatch.ElapsedMilliseconds;

                    Stopwatch rotationStopwatch = Stopwatch.StartNew();
                    CalculateObjectDefinitionRotationGeometry(result, labelContours, roi);
                    rotationStopwatch.Stop();
                    rotationGeometryElapsedMilliseconds = rotationStopwatch.ElapsedMilliseconds;
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

        private static Dictionary<int, Cv.Point[]> CreateObjectDefinitionLabelContours(
            Cv.Mat labels,
            IEnumerable<ObjectDefinitionComponentRegion> components,
            ISet<int> retainedLabels)
        {
            var contoursByLabel = new Dictionary<int, Cv.Point[]>();
            if (labels == null || labels.Empty() || components == null || retainedLabels == null)
            {
                return contoursByLabel;
            }

            foreach (ObjectDefinitionComponentRegion component in components)
            {
                if (component == null || !retainedLabels.Contains(component.Label) ||
                    component.Width <= 0 || component.Height <= 0)
                {
                    continue;
                }

                using (var labelRoi = new Cv.Mat(
                    labels,
                    new Cv.Rect(
                        component.X,
                        component.Y,
                        component.Width,
                        component.Height)))
                using (var componentMask = new Cv.Mat())
                {
                    Cv.Cv2.Compare(
                        labelRoi,
                        component.Label,
                        componentMask,
                        Cv.CmpType.EQ);

                    Cv.Point[][] contours;
                    Cv.HierarchyIndex[] hierarchy;
                    Cv.Cv2.FindContours(
                        componentMask,
                        out contours,
                        out hierarchy,
                        Cv.RetrievalModes.External,
                        Cv.ContourApproximationModes.ApproxSimple);
                    if (contours == null || contours.Length == 0)
                    {
                        continue;
                    }

                    Cv.Point[] points = contours
                        .Where(contour => contour != null && contour.Length > 0)
                        .SelectMany(contour => contour)
                        .Select(point => new Cv.Point(
                            point.X + component.X,
                            point.Y + component.Y))
                        .ToArray();
                    if (points.Length > 0)
                    {
                        contoursByLabel[component.Label] = points;
                    }
                }
            }

            return contoursByLabel;
        }

        private static void CalculateObjectDefinitionRotationGeometry(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            IDictionary<int, Cv.Point[]> labelContours,
            Rectangle roi)
        {
            if (objects == null || labelContours == null || labelContours.Count == 0)
            {
                return;
            }

            foreach (ObjectDefinitionDetectedObject item in objects)
            {
                if (item == null || item.SourceLabels == null || item.SourceLabels.Count == 0)
                {
                    continue;
                }

                Cv.Point2f[] points = item.SourceLabels
                    .Distinct()
                    .Where(label => labelContours.ContainsKey(label))
                    .SelectMany(label => labelContours[label])
                    .Select(point => new Cv.Point2f(point.X, point.Y))
                    .ToArray();
                if (points.Length < 3)
                {
                    continue;
                }

                Cv.RotatedRect rotatedRect = Cv.Cv2.MinAreaRect(points);
                float width = rotatedRect.Size.Width;
                float height = rotatedRect.Size.Height;
                double angle = rotatedRect.Angle;
                if (width < height)
                {
                    float swap = width;
                    width = height;
                    height = swap;
                    angle += 90.0;
                }

                while (angle <= -90.0)
                {
                    angle += 180.0;
                }

                while (angle > 90.0)
                {
                    angle -= 180.0;
                }

                Cv.Point2f[] corners = rotatedRect.Points();
                item.HasRotationGeometry = corners != null && corners.Length == 4;
                item.RotationAngleDegrees = angle;
                item.RotationCenter = new PointF(
                    roi.X + rotatedRect.Center.X,
                    roi.Y + rotatedRect.Center.Y);
                item.RotationSize = new SizeF(width, height);
                item.RotationCorners = item.HasRotationGeometry
                    ? corners.Select(point => new PointF(
                        roi.X + point.X,
                        roi.Y + point.Y)).ToArray()
                    : null;
            }
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
                ordered = OrderObjectDefinitionObjectsByRows(ordered);
            }

            return ordered.ToList();
        }

        private static IEnumerable<ObjectDefinitionDetectedObject> OrderObjectDefinitionObjectsByRows(
            IEnumerable<ObjectDefinitionDetectedObject> objects)
        {
            List<ObjectDefinitionDetectedObject> items = (objects ??
                Enumerable.Empty<ObjectDefinitionDetectedObject>()).ToList();
            if (items.Count <= 1)
            {
                return items;
            }

            List<int> heights = items
                .Where(item => item.Bounds.Height > 0)
                .Select(item => item.Bounds.Height)
                .OrderBy(height => height)
                .ToList();
            int typicalHeight = heights.Count == 0
                ? 1
                : heights[heights.Count / 2];
            double rowTolerance = Math.Max(1.0, typicalHeight * 0.5);

            var rows = new List<ObjectDefinitionNumberingRow>();
            foreach (ObjectDefinitionDetectedObject item in items
                .OrderBy(item => item.Bounds.Top + (item.Bounds.Height / 2.0))
                .ThenBy(item => item.Bounds.Left))
            {
                double centerY = item.Bounds.Top + (item.Bounds.Height / 2.0);
                ObjectDefinitionNumberingRow row = rows
                    .OrderBy(candidate => Math.Abs(candidate.CenterY - centerY))
                    .FirstOrDefault(candidate =>
                        Math.Abs(candidate.CenterY - centerY) <= rowTolerance);
                if (row == null)
                {
                    row = new ObjectDefinitionNumberingRow();
                    rows.Add(row);
                }

                row.Items.Add(item);
                row.CenterY = row.Items
                    .Average(candidate => candidate.Bounds.Top + (candidate.Bounds.Height / 2.0));
            }

            return rows
                .OrderBy(row => row.CenterY)
                .SelectMany(row => row.Items
                    .OrderBy(item => item.Bounds.Left)
                    .ThenBy(item => item.Bounds.Top));
        }

        private sealed class ObjectDefinitionNumberingRow
        {
            public ObjectDefinitionNumberingRow()
            {
                Items = new List<ObjectDefinitionDetectedObject>();
            }

            public double CenterY { get; set; }

            public List<ObjectDefinitionDetectedObject> Items { get; private set; }
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
