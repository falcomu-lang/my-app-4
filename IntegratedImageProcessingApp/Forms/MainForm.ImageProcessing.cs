using System;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private static Cv.Mat CreateCombinedImageProcessingGroupMask(
            Cv.Mat source,
            System.Collections.Generic.IEnumerable<ImageProcessingStepSettings> steps)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var combined = new Cv.Mat(
                source.Rows,
                source.Cols,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0));
            try
            {
                foreach (ImageProcessingStepSettings step in steps ??
                    System.Linq.Enumerable.Empty<ImageProcessingStepSettings>())
                {
                    if (step == null || !IsBinaryMaskProcessingMethod(step.Method))
                    {
                        continue;
                    }

                    // Each detector analyzes the same source image.  A group
                    // combines their binary results instead of feeding one
                    // detector's mask into the next detector.
                    using (Cv.Mat next = CreateNativeLargeEdgeBinaryMask(
                        source,
                        step.Method,
                        ParseImageProcessingParameters(step.Parameters)))
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
            BeginParameterApplyStatus(false);
            MarkProcessedPreviewDirty();
            MarkProcessedImageDirty();
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = "已開始處理" + stepText.Trim();
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
            MarkProcessedPreviewDirty();
            // A group has its own combined-mask cache key. Invalidate the
            // previous single-step/group result before rebuilding the OR mask.
            MarkProcessedImageDirty();
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = "已開始處理" + group.DisplayName;
        }
    }
}
