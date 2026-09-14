using System;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
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

        private void ProcessImageProcessingStep(string stepText)
        {
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            selectedImageProcessingStepIndex = stepIndex;
            selectedImageProcessingGroupId = null;
            imageProcessingExecutionRequested = true;
            BeginParameterApplyStatus(false);
            MarkProcessedPreviewDirty();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已開始處理" + stepText.Trim();
        }

        private void ProcessImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = group.Id;
            imageProcessingExecutionRequested = true;
            MarkProcessedPreviewDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已開始處理" + group.DisplayName;
        }
    }
}
