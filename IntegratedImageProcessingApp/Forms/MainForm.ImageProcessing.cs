using System;
using System.Collections.Generic;
using System.Drawing;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        // Keep each processing node's raw OpenCV mask so later stages can
        // reuse it instead of running the detector again. The cache is
        // invalidated with the processed-image cache when the source or
        // parameters change.
        private readonly object processedBinaryMaskCacheLock = new object();
        private readonly Dictionary<string, Cv.Mat> processedBinaryMaskCache =
            new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);

        private Cv.Mat CreateCombinedImageProcessingGroupMask(
            Cv.Mat source,
            Rectangle roi,
            IEnumerable<ImageProcessingStepSettings> steps,
            string sourceNamespace)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var orderedSteps = new List<ImageProcessingStepSettings>();
            foreach (ImageProcessingStepSettings step in steps ??
                new ImageProcessingStepSettings[0])
            {
                if (step != null && IsBinaryMaskProcessingMethod(step.Method))
                {
                    orderedSteps.Add(step);
                }
            }

            var combined = new Cv.Mat(
                source.Rows,
                source.Cols,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0));
            try
            {
                foreach (ImageProcessingStepSettings step in orderedSteps)
                {
                    // The cache owns its Mat. The helper returns a clone so
                    // the cache can be cleared safely while this task runs.
                    using (Cv.Mat next = GetOrCreateProcessedBinaryMask(
                        source,
                        roi,
                        step,
                        sourceNamespace))
                    {
                        Cv.Cv2.BitwiseOr(combined, next, combined);
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

        private Cv.Mat GetOrCreateProcessedBinaryMask(
            Cv.Mat source,
            Rectangle roi,
            ImageProcessingStepSettings step,
            string sourceNamespace)
        {
            if (source == null || step == null)
            {
                throw new ArgumentNullException(source == null ? "source" : "step");
            }

            string cacheKey = CreateProcessedBinaryMaskCacheKey(
                "step",
                roi,
                sourceNamespace,
                new[] { step });
            lock (processedBinaryMaskCacheLock)
            {
                Cv.Mat cached;
                if (processedBinaryMaskCache.TryGetValue(cacheKey, out cached) &&
                    cached != null && !cached.Empty())
                {
                    return cached.Clone();
                }
            }

            Cv.Mat created = CreateNativeLargeEdgeBinaryMask(
                source,
                step.Method,
                ParseImageProcessingParameters(step.Parameters));
            lock (processedBinaryMaskCacheLock)
            {
                Cv.Mat existing;
                if (processedBinaryMaskCache.TryGetValue(cacheKey, out existing) &&
                    existing != null && !existing.Empty())
                {
                    created.Dispose();
                    return existing.Clone();
                }

                processedBinaryMaskCache[cacheKey] = created;
                return created.Clone();
            }
        }

        private bool TryGetCachedProcessedBinaryMask(
            Rectangle roi,
            ImageProcessingStepSettings step,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            if (step == null || roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            string cacheKey = CreateProcessedBinaryMaskCacheKey(
                "step",
                roi,
                sourceNamespace,
                new[] { step });
            lock (processedBinaryMaskCacheLock)
            {
                Cv.Mat cached;
                if (!processedBinaryMaskCache.TryGetValue(cacheKey, out cached) ||
                    cached == null || cached.Empty() ||
                    cached.Rows != roi.Height || cached.Cols != roi.Width)
                {
                    return false;
                }

                mask = cached.Clone();
                return true;
            }
        }

        private string CreateProcessedBinaryMaskCacheKey(
            string maskKind,
            Rectangle roi,
            string sourceNamespace,
            IEnumerable<ImageProcessingStepSettings> steps)
        {
            var parts = new List<string>
            {
                maskKind ?? string.Empty,
                imageSourceGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                systemParameters == null ? string.Empty : systemParameters.LastImagePath ?? string.Empty,
                sourceNamespace ?? string.Empty,
                roi.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            foreach (ImageProcessingStepSettings step in steps ??
                new ImageProcessingStepSettings[0])
            {
                if (step == null)
                {
                    continue;
                }

                parts.Add(step.Id ?? string.Empty);
                parts.Add(step.Method ?? string.Empty);
                parts.Add(step.Parameters ?? string.Empty);
            }

            return string.Join("|", parts.ToArray());
        }

        private void ClearProcessedBinaryMaskCache()
        {
            lock (processedBinaryMaskCacheLock)
            {
                foreach (Cv.Mat mask in processedBinaryMaskCache.Values)
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                }

                processedBinaryMaskCache.Clear();
            }
        }

        private string CreateImageProcessingSourceNamespace()
        {
            return string.Join(
                "|",
                "image-processing",
                displayedImageRelationSourceType ?? activeImageRelationSourceType ?? "Original",
                displayedImageRelationSourceId ?? activeImageRelationSourceId ?? string.Empty);
        }

        private static string CreateImageRelationSourceNamespace(ImageRelationSettings relation)
        {
            if (relation == null)
            {
                return "relation|unknown";
            }

            return string.Join(
                "|",
                "relation",
                relation.Id ?? string.Empty,
                relation.SourceType ?? "Original",
                relation.SourceId ?? string.Empty);
        }

        private static bool[,] CreateOpenCvCannyMask(byte[,] gray, int lowThreshold, int highThreshold, int kernelSize, bool l2Gradient, int gaussianBlurSize, double gaussianSigma, string edgeSelection, int minEdgeLength, int maxGap)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var blurred = new Cv.Mat())
            using (var edges = new Cv.Mat())
            {
                int blurSize = EnsureOdd(Math.Max(1, gaussianBlurSize));
                Cv.Cv2.GaussianBlur(source, blurred, new Cv.Size(blurSize, blurSize), Math.Max(0.1, gaussianSigma));
                Cv.Cv2.Canny(blurred, edges,
                    Math.Min(lowThreshold, highThreshold),
                    Math.Max(lowThreshold, highThreshold),
                    NormalizeCannyKernelSize(kernelSize), l2Gradient);
                return CreateBoolMaskFromOpenCvMat(edges);
            }
        }

        private static bool[,] CreateOpenCvSobelMask(byte[,] gray, int threshold, string direction, int kernelSize)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var edgeMask = CreateOpenCvSobelBinaryMask(source, threshold, direction, kernelSize))
            {
                return ConvertOpenCvBinaryMask(edgeMask);
            }
        }

        private static bool[,] CreateOpenCvPolarityMask(byte[,] gray, int contrastThreshold, int edgeWidth, int smoothing, string polarity, string searchDirection, double gaussianSigma, string borderType)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var blurred = new Cv.Mat())
            using (var gradientX = new Cv.Mat())
            using (var gradientY = new Cv.Mat())
            using (var directedGradient = new Cv.Mat())
            using (var absoluteX = new Cv.Mat())
            using (var absoluteY = new Cv.Mat())
            using (var pickX = new Cv.Mat())
            using (var absoluteGradient = new Cv.Mat())
            using (var edgeMask = new Cv.Mat())
            {
                int blurSize = EnsureOdd(Math.Max(1, smoothing));
                Cv.Cv2.GaussianBlur(source, blurred, new Cv.Size(blurSize, blurSize), Math.Max(0.1, gaussianSigma), 0, GetOpenCvBorderType(borderType));
                int aperture = NormalizeSobelKernelSize(edgeWidth);
                Cv.Cv2.Sobel(blurred, gradientX, Cv.MatType.CV_32FC1, 1, 0, aperture);
                Cv.Cv2.Sobel(blurred, gradientY, Cv.MatType.CV_32FC1, 0, 1, aperture);
                string normalizedDirection = NormalizePolaritySearchDirection(searchDirection);
                if (normalizedDirection == "X") gradientX.CopyTo(directedGradient);
                else if (normalizedDirection == "Y") gradientY.CopyTo(directedGradient);
                else
                {
                    Cv.Cv2.Absdiff(gradientX, Cv.Scalar.All(0), absoluteX);
                    Cv.Cv2.Absdiff(gradientY, Cv.Scalar.All(0), absoluteY);
                    Cv.Cv2.Compare(absoluteX, absoluteY, pickX, Cv.CmpType.GE);
                    gradientY.CopyTo(directedGradient);
                    gradientX.CopyTo(directedGradient, pickX);
                }
                if (string.Equals(polarity, "BrightToDark", StringComparison.OrdinalIgnoreCase))
                    Cv.Cv2.Threshold(directedGradient, edgeMask, -contrastThreshold, 255, Cv.ThresholdTypes.BinaryInv);
                else if (string.Equals(polarity, "DarkToBright", StringComparison.OrdinalIgnoreCase))
                    Cv.Cv2.Threshold(directedGradient, edgeMask, contrastThreshold, 255, Cv.ThresholdTypes.Binary);
                else
                {
                    Cv.Cv2.Absdiff(directedGradient, Cv.Scalar.All(0), absoluteGradient);
                    Cv.Cv2.Threshold(absoluteGradient, edgeMask, contrastThreshold, 255, Cv.ThresholdTypes.Binary);
                }
                edgeMask.ConvertTo(edgeMask, Cv.MatType.CV_8UC1);
                return ConvertOpenCvBinaryMask(edgeMask);
            }
        }

        private static bool[,] CreateOpenCvGlobalThresholdMask(
            byte[,] gray,
            string thresholdMode,
            int threshold,
            int lowerThreshold,
            int upperThreshold,
            int maxValue,
            string thresholdType)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var mask = CreateOpenCvGlobalThresholdBinaryMask(
                source,
                thresholdMode,
                threshold,
                lowerThreshold,
                upperThreshold,
                maxValue,
                thresholdType))
            {
                return ConvertOpenCvBinaryMask(mask);
            }
        }

        private static bool[,] CreateOpenCvAdaptiveThresholdMask(
            byte[,] gray,
            int maxValue,
            string adaptiveMethod,
            string thresholdType,
            int blockSize,
            double c)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var mask = CreateOpenCvAdaptiveThresholdBinaryMask(
                source,
                maxValue,
                adaptiveMethod,
                thresholdType,
                blockSize,
                c))
            {
                return ConvertOpenCvBinaryMask(mask);
            }
        }

        private static bool[,] CreateOpenCvOtsuThresholdMask(byte[,] gray, int maxValue, string thresholdType)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var mask = CreateOpenCvOtsuThresholdBinaryMask(source, maxValue, thresholdType))
            {
                return ConvertOpenCvBinaryMask(mask);
            }
        }

        private void ProcessImageProcessingStep(string stepText)
        {
            objectDefinitionDependencyPreviewRequested = false;
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                statusLabel.Text = "影像處理項目不存在，請重新選擇影像處理項目";
                return;
            }

            selectedImageProcessingStepIndex = stepIndex;
            selectedImageProcessingGroupId = null;
            activeImageRelationGroupId = null;
            displayedImageProcessingStepIndex = stepIndex;
            displayedImageProcessingGroupId = null;
            displayedImageRelationGroupId = null;
            displayedImageRelationSourceType = string.IsNullOrWhiteSpace(activeImageRelationSourceType)
                ? "Original"
                : activeImageRelationSourceType;
            displayedImageRelationSourceId = activeImageRelationSourceId;
            imageProcessingExecutionRequested = true;
            explicitProcessedImageUpdateRequested = true;
            bool hasCachedResult = HasCachedProcessedImageForCurrentSelection();
            if (hasCachedResult)
            {
                SetParameterApplyStatus("已處理，使用快取結果");
            }
            else
            {
                BeginParameterApplyStatus(false);
            }
            MarkProcessedPreviewDirty();
            // Selecting "處理" again must reuse the matching result.  A
            // parameter/image/ROI change still performs the full invalidation
            // through the default MarkProcessedImageDirty() path elsewhere.
            MarkProcessedImageDirty(false);
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = hasCachedResult
                ? "已處理" + stepText.Trim() + "，使用快取結果"
                : "已開始處理" + stepText.Trim();
        }

        private void ProcessImageProcessingGroup(string groupId)
        {
            objectDefinitionDependencyPreviewRequested = false;
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                statusLabel.Text = "影像處理群組不存在，請重新選擇影像處理群組";
                return;
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = group.Id;
            activeImageRelationGroupId = null;
            displayedImageProcessingStepIndex = -1;
            displayedImageProcessingGroupId = group.Id;
            displayedImageRelationGroupId = null;
            displayedImageRelationSourceType = string.IsNullOrWhiteSpace(activeImageRelationSourceType)
                ? "Original"
                : activeImageRelationSourceType;
            displayedImageRelationSourceId = activeImageRelationSourceId;
            imageProcessingExecutionRequested = true;
            explicitProcessedImageUpdateRequested = true;
            bool hasCachedResult = HasCachedProcessedImageForCurrentSelection();
            if (hasCachedResult)
            {
                SetParameterApplyStatus("已處理，使用快取結果");
            }
            else
            {
                BeginParameterApplyStatus(false);
            }
            MarkProcessedPreviewDirty();
            // A group has its own cache key. Keep existing step/group masks so
            // returning to an unchanged group can reuse them immediately.
            MarkProcessedImageDirty(false);
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = hasCachedResult
                ? "已處理" + group.DisplayName + "，使用快取結果"
                : "已開始處理" + group.DisplayName;
        }
    }
}
