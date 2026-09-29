using System;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal static class OpenCvEdgeDetectionService
    {
        internal static Cv.Mat CreateCannyBinaryMask(
            Cv.Mat source,
            int lowThreshold,
            int highThreshold,
            int kernelSize,
            bool l2Gradient,
            int gaussianBlurSize,
            double gaussianSigma)
        {
            using (var blurred = new Cv.Mat())
            using (var edges = new Cv.Mat())
            {
                int blurSize = EnsureOdd(Math.Max(1, gaussianBlurSize));
                Cv.Cv2.GaussianBlur(source, blurred, new Cv.Size(blurSize, blurSize), Math.Max(0.1, gaussianSigma));
                Cv.Cv2.Canny(
                    blurred,
                    edges,
                    Math.Min(lowThreshold, highThreshold),
                    Math.Max(lowThreshold, highThreshold),
                    NormalizeCannyKernelSize(kernelSize),
                    l2Gradient);
                return edges.Clone();
            }
        }

        internal static Cv.Mat CreateSobelBinaryMask(Cv.Mat source, int threshold, string direction, int kernelSize)
        {
            using (var gradientX = new Cv.Mat())
            using (var gradientY = new Cv.Mat())
            using (var edgeMask = new Cv.Mat())
            using (var secondaryMask = new Cv.Mat())
            {
                int aperture = NormalizeSobelKernelSize(kernelSize);
                double gradientScale = aperture == 7 ? 1.0 / 16.0 : 1.0;
                double scaledThreshold = threshold * gradientScale;
                string normalizedDirection = NormalizeSearchDirection(direction);
                bool xOnly = normalizedDirection == "X";
                bool yOnly = normalizedDirection == "Y";

                if (!yOnly)
                {
                    Cv.Cv2.Sobel(source, gradientX, Cv.MatType.CV_16SC1, 1, 0, aperture, gradientScale);
                    CreatePolarityComparison(gradientX, edgeMask, "Any", scaledThreshold);
                }

                if (!xOnly)
                {
                    Cv.Cv2.Sobel(source, gradientY, Cv.MatType.CV_16SC1, 0, 1, aperture, gradientScale);
                    CreatePolarityComparison(gradientY, (xOnly || yOnly) ? edgeMask : secondaryMask, "Any", scaledThreshold);
                }

                if (!xOnly && !yOnly)
                {
                    Cv.Cv2.BitwiseOr(edgeMask, secondaryMask, edgeMask);
                }

                return edgeMask.Clone();
            }
        }

        internal static Cv.Mat CreatePolarityBinaryMask(
            Cv.Mat source,
            int contrastThreshold,
            int edgeWidth,
            int smoothing,
            string polarity,
            string searchDirection,
            double gaussianSigma,
            string borderType)
        {
            using (var blurred = new Cv.Mat())
            using (var gradientX = new Cv.Mat())
            using (var gradientY = new Cv.Mat())
            using (var edgeMask = new Cv.Mat())
            using (var secondaryMask = new Cv.Mat())
            {
                int blurSize = EnsureOdd(Math.Max(1, smoothing));
                Cv.Cv2.GaussianBlur(source, blurred, new Cv.Size(blurSize, blurSize), Math.Max(0.1, gaussianSigma), 0, GetBorderType(borderType));
                int aperture = NormalizeSobelKernelSize(edgeWidth);
                double gradientScale = aperture == 7 ? 1.0 / 16.0 : 1.0;
                double scaledContrastThreshold = contrastThreshold * gradientScale;
                string normalizedDirection = NormalizeSearchDirection(searchDirection);
                bool horizontalOnly = normalizedDirection == "X";
                bool verticalOnly = normalizedDirection == "Y";

                if (!verticalOnly)
                {
                    Cv.Cv2.Sobel(blurred, gradientX, Cv.MatType.CV_16SC1, 1, 0, aperture, gradientScale);
                    CreatePolarityComparison(gradientX, edgeMask, polarity, scaledContrastThreshold);
                }

                if (!horizontalOnly)
                {
                    Cv.Cv2.Sobel(blurred, gradientY, Cv.MatType.CV_16SC1, 0, 1, aperture, gradientScale);
                    CreatePolarityComparison(gradientY, (horizontalOnly || verticalOnly) ? edgeMask : secondaryMask, polarity, scaledContrastThreshold);
                }

                if (!horizontalOnly && !verticalOnly)
                {
                    Cv.Cv2.BitwiseOr(edgeMask, secondaryMask, edgeMask);
                }

                return edgeMask.Clone();
            }
        }

        internal static int NormalizeCannyKernelSize(int kernelSize)
        {
            if (kernelSize <= 3)
            {
                return 3;
            }

            return kernelSize <= 5 ? 5 : 7;
        }

        private static void CreatePolarityComparison(Cv.Mat gradient, Cv.Mat destination, string polarity, double contrastThreshold)
        {
            if (string.Equals(polarity, "BrightToDark", StringComparison.OrdinalIgnoreCase))
            {
                Cv.Cv2.Compare(gradient, -contrastThreshold, destination, Cv.CmpType.LT);
                return;
            }

            if (string.Equals(polarity, "DarkToBright", StringComparison.OrdinalIgnoreCase))
            {
                Cv.Cv2.Compare(gradient, contrastThreshold, destination, Cv.CmpType.GT);
                return;
            }

            Cv.Cv2.Absdiff(gradient, Cv.Scalar.All(0), gradient);
            Cv.Cv2.Compare(gradient, contrastThreshold, destination, Cv.CmpType.GT);
        }

        private static string NormalizeSearchDirection(string direction)
        {
            if (string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, "X", StringComparison.OrdinalIgnoreCase))
            {
                return "X";
            }

            if (string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return "Y";
            }

            return "Any";
        }

        private static Cv.BorderTypes GetBorderType(string borderType)
        {
            if (string.Equals(borderType, "Replicate", StringComparison.OrdinalIgnoreCase))
            {
                return Cv.BorderTypes.Replicate;
            }

            if (string.Equals(borderType, "Constant", StringComparison.OrdinalIgnoreCase))
            {
                return Cv.BorderTypes.Constant;
            }

            return Cv.BorderTypes.Reflect101;
        }

        private static int NormalizeSobelKernelSize(int kernelSize)
        {
            if (kernelSize <= 1)
            {
                return 1;
            }

            if (kernelSize <= 3)
            {
                return 3;
            }

            return kernelSize <= 5 ? 5 : 7;
        }

        private static int EnsureOdd(int value)
        {
            int normalized = Math.Max(1, value);
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }
    }
}
