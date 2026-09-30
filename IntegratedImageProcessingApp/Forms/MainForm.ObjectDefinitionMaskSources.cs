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
                    timing,
                    true);
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
            ObjectDefinitionSourceTiming timing,
            bool rebuildMissingSources = false)
        {
            mask = null;
            if (!HasConfiguredObjectDefinitionMaskSource(definition) ||
                roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            Cv.Mat primary = null;
            bool primaryReady = rebuildMissingSources && TryBuildObjectDetectionMaskSource(
                    source,
                    definition.SourceMaskPrimaryType,
                    definition.SourceMaskPrimaryId,
                    roi,
                    string.Empty,
                    out primary);
            if (!primaryReady)
            {
                primaryReady = TryGetCachedObjectDefinitionMaskSource(
                    source,
                    definition.SourceMaskPrimaryType,
                    definition.SourceMaskPrimaryId,
                    roi,
                    out primary,
                    timing);
            }
            if (!primaryReady)
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

            Cv.Mat secondary = null;
            bool secondaryReady = rebuildMissingSources && TryBuildObjectDetectionMaskSource(
                    source,
                    definition.SourceMaskSecondaryType,
                    definition.SourceMaskSecondaryId,
                    roi,
                    string.Empty,
                    out secondary);
            if (!secondaryReady)
            {
                secondaryReady = TryGetCachedObjectDefinitionMaskSource(
                    source,
                    definition.SourceMaskSecondaryType,
                    definition.SourceMaskSecondaryId,
                    roi,
                    out secondary);
            }
            if (!secondaryReady)
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
            return largeImageMaskCache.TryGetBinaryMaskView(
                key,
                roi.Height,
                roi.Width,
                out mask);
        }

        private bool TryResolveConfiguredObjectDefinitionMaskFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectDefinitionSettings definition,
            Rectangle roi,
            out Cv.Mat mask,
            bool rebuildMissingSources = false)
        {
            mask = null;
            if (original == null || originalGray == null ||
                !HasConfiguredObjectDefinitionMaskSource(definition))
            {
                return false;
            }

            Cv.Mat primary = null;
            bool primaryReady = rebuildMissingSources && TryBuildObjectDetectionMaskSourceFromBitmap(
                    original,
                    originalGray,
                    definition.SourceMaskPrimaryType,
                    definition.SourceMaskPrimaryId,
                    roi,
                    string.Empty,
                    out primary);
            if (!primaryReady)
            {
                primaryReady = TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                    original,
                    originalGray,
                    definition.SourceMaskPrimaryType,
                    definition.SourceMaskPrimaryId,
                    roi,
                    out primary);
            }
            if (!primaryReady)
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

            Cv.Mat secondary = null;
            bool secondaryReady = rebuildMissingSources && TryBuildObjectDetectionMaskSourceFromBitmap(
                    original,
                    originalGray,
                    definition.SourceMaskSecondaryType,
                    definition.SourceMaskSecondaryId,
                    roi,
                    string.Empty,
                    out secondary);
            if (!secondaryReady)
            {
                secondaryReady = TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                    original,
                    originalGray,
                    definition.SourceMaskSecondaryType,
                    definition.SourceMaskSecondaryId,
                    roi,
                    out secondary);
            }
            if (!secondaryReady)
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
                    out configuredMask,
                    true))
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

    }
}
