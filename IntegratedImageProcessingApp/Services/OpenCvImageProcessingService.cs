using System;
using System.Collections.Generic;
using System.Globalization;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal static class OpenCvImageProcessingService
    {
        internal static Cv.Mat CreateBinaryMask(
            Cv.Mat source,
            string method,
            Dictionary<string, string> parameters,
            string fallbackMethod)
        {
            string selectedMethod = IsSupportedMethod(method) ? method : fallbackMethod;
            if (string.Equals(selectedMethod, "Global Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return OpenCvThresholdService.CreateGlobalThresholdBinaryMask(
                    source,
                    GetStringParameter(parameters, "ThresholdMode", "Single"),
                    GetIntParameter(parameters, "Threshold", 128),
                    GetIntParameter(parameters, "LowerThreshold", 0),
                    GetIntParameter(parameters, "UpperThreshold", 255),
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (string.Equals(selectedMethod, "Adaptive Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return OpenCvThresholdService.CreateAdaptiveThresholdBinaryMask(
                    source,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "AdaptiveMethod", "GaussianC"),
                    GetStringParameter(parameters, "ThresholdType", "Binary"),
                    GetIntParameter(parameters, "BlockSize", 11),
                    GetDoubleParameter(parameters, "C", 2));
            }

            if (string.Equals(selectedMethod, "Otsu Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return OpenCvThresholdService.CreateOtsuThresholdBinaryMask(
                    source,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (string.Equals(selectedMethod, "Sobel Edge", StringComparison.OrdinalIgnoreCase))
            {
                return OpenCvEdgeDetectionService.CreateSobelBinaryMask(
                    source,
                    GetIntParameter(parameters, "Threshold", 30),
                    GetStringParameter(parameters, "Direction", "Any"),
                    GetIntParameter(parameters, "KernelSize", 3));
            }

            if (string.Equals(selectedMethod, "Polarity Edge", StringComparison.OrdinalIgnoreCase))
            {
                int edgeWidth = GetIntParameter(
                    parameters,
                    "CoreWidth",
                    GetIntParameter(parameters, "EdgeWidth", 3));
                return OpenCvEdgeDetectionService.CreatePolarityBinaryMask(
                    source,
                    GetIntParameter(parameters, "ContrastThreshold", 20),
                    edgeWidth,
                    GetIntParameter(parameters, "Smoothing", 1),
                    GetStringParameter(parameters, "Polarity", "Any"),
                    GetStringParameter(parameters, "SearchDirection", "Any"),
                    GetDoubleParameter(parameters, "GaussianSigma", 0.5),
                    GetStringParameter(parameters, "BorderType", "Reflect"));
            }

            return OpenCvEdgeDetectionService.CreateCannyBinaryMask(
                source,
                GetIntParameter(parameters, "LowThreshold", 50),
                GetIntParameter(parameters, "HighThreshold", 150),
                GetIntParameter(parameters, "KernelSize", 3),
                GetBoolParameter(parameters, "L2Gradient", false),
                GetIntParameter(parameters, "GaussianBlurSize", 5),
                GetDoubleParameter(parameters, "GaussianSigma", 1.4));
        }

        private static bool IsSupportedMethod(string method)
        {
            return string.Equals(method, "Global Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Adaptive Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Otsu Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Canny Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Sobel Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Polarity Edge", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetIntParameter(Dictionary<string, string> parameters, string key, int defaultValue)
        {
            string value;
            int parsedValue;
            return parameters != null && parameters.TryGetValue(key, out value) && int.TryParse(value, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static double GetDoubleParameter(Dictionary<string, string> parameters, string key, double defaultValue)
        {
            string value;
            double parsedValue;
            return parameters != null &&
                parameters.TryGetValue(key, out value) &&
                double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static bool GetBoolParameter(Dictionary<string, string> parameters, string key, bool defaultValue)
        {
            string value;
            bool parsedValue;
            return parameters != null && parameters.TryGetValue(key, out value) && bool.TryParse(value, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static string GetStringParameter(Dictionary<string, string> parameters, string key, string defaultValue)
        {
            string value;
            return parameters != null && parameters.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}
