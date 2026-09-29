using System;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal static class OpenCvThresholdService
    {
        internal static Cv.Mat CreateGlobalThresholdBinaryMask(
            Cv.Mat source,
            string thresholdMode,
            int threshold,
            int lowerThreshold,
            int upperThreshold,
            int maxValue,
            string thresholdType)
        {
            var result = new Cv.Mat();
            try
            {
                if (string.Equals(thresholdMode, "Range", StringComparison.OrdinalIgnoreCase))
                {
                    int lower = ClampInt(Math.Min(lowerThreshold, upperThreshold), 0, 255);
                    int upper = ClampInt(Math.Max(lowerThreshold, upperThreshold), 0, 255);
                    Cv.Cv2.InRange(
                        source,
                        new Cv.Scalar(lower),
                        new Cv.Scalar(upper),
                        result);
                    int clampedMaxValue = ClampInt(maxValue, 1, 255);
                    if (clampedMaxValue != 255)
                    {
                        Cv.Cv2.Threshold(
                            result,
                            result,
                            0,
                            clampedMaxValue,
                            Cv.ThresholdTypes.Binary);
                    }

                    return result;
                }

                Cv.ThresholdTypes type = string.Equals(thresholdType, "BinaryInv", StringComparison.OrdinalIgnoreCase)
                    ? Cv.ThresholdTypes.BinaryInv
                    : Cv.ThresholdTypes.Binary;
                Cv.Cv2.Threshold(
                    source,
                    result,
                    ClampInt(threshold, 0, 255),
                    ClampInt(maxValue, 1, 255),
                    type);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        internal static Cv.Mat CreateAdaptiveThresholdBinaryMask(
            Cv.Mat source,
            int maxValue,
            string adaptiveMethod,
            string thresholdType,
            int blockSize,
            double c)
        {
            var result = new Cv.Mat();
            try
            {
                Cv.AdaptiveThresholdTypes adaptive = string.Equals(adaptiveMethod, "MeanC", StringComparison.OrdinalIgnoreCase)
                    ? Cv.AdaptiveThresholdTypes.MeanC
                    : Cv.AdaptiveThresholdTypes.GaussianC;
                Cv.ThresholdTypes type = string.Equals(thresholdType, "BinaryInv", StringComparison.OrdinalIgnoreCase)
                    ? Cv.ThresholdTypes.BinaryInv
                    : Cv.ThresholdTypes.Binary;
                Cv.Cv2.AdaptiveThreshold(
                    source,
                    result,
                    ClampInt(maxValue, 1, 255),
                    adaptive,
                    type,
                    EnsureOdd(Math.Max(3, blockSize)),
                    c);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        internal static Cv.Mat CreateOtsuThresholdBinaryMask(
            Cv.Mat source,
            int maxValue,
            string thresholdType)
        {
            var result = new Cv.Mat();
            try
            {
                Cv.ThresholdTypes type = string.Equals(thresholdType, "BinaryInv", StringComparison.OrdinalIgnoreCase)
                    ? Cv.ThresholdTypes.BinaryInv
                    : Cv.ThresholdTypes.Binary;
                Cv.Cv2.Threshold(
                    source,
                    result,
                    0,
                    ClampInt(maxValue, 1, 255),
                    type | Cv.ThresholdTypes.Otsu);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private static int ClampInt(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private static int EnsureOdd(int value)
        {
            int normalized = Math.Max(1, value);
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }
    }
}
