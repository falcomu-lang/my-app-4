using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace IntegratedImageProcessingApp.Services
{
    public sealed class CameraDeviceSettings
    {
        public CameraDeviceSettings()
        {
            XMillimetersPerPixel = 1.0;
            YMillimetersPerPixel = 1.0;
            MillimeterConversionEnabled = false;
            ProgramName = string.Empty;
            SoftwareId = 1;
            ListenIpAddress = "0.0.0.0";
            ListenPort = 5000;
        }

        public double XMillimetersPerPixel { get; set; }

        public double YMillimetersPerPixel { get; set; }

        public bool MillimeterConversionEnabled { get; set; }

        public string ProgramName { get; set; }

        public int SoftwareId { get; set; }

        public string ListenIpAddress { get; set; }

        public int ListenPort { get; set; }
    }

    public sealed class CameraDeviceSettingsIniService
    {
        private readonly string filePath;

        public CameraDeviceSettingsIniService(string filePath)
        {
            this.filePath = filePath;
        }

        public CameraDeviceSettings Load()
        {
            var settings = new CameraDeviceSettings();
            if (!File.Exists(filePath))
            {
                return settings;
            }

            bool inCameraSection = false;
            bool inApplicationSection = false;
            bool inNetworkSection = false;
            foreach (string rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal))
                {
                    string sectionName = line.Substring(1, line.Length - 2).Trim();
                    inCameraSection = string.Equals(
                        sectionName, "Camera", StringComparison.OrdinalIgnoreCase);
                    inApplicationSection = string.Equals(
                        sectionName, "Application", StringComparison.OrdinalIgnoreCase);
                    inNetworkSection = string.Equals(
                        sectionName, "Network", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (inApplicationSection && string.Equals(
                    key, "ProgramName", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ProgramName = value;
                }
                else if (inApplicationSection && string.Equals(
                    key, "SoftwareId", StringComparison.OrdinalIgnoreCase))
                {
                    settings.SoftwareId = ParseSoftwareId(value, settings.SoftwareId);
                }
                else if (inCameraSection && string.Equals(
                    key, "XMillimetersPerPixel", StringComparison.OrdinalIgnoreCase))
                {
                    settings.XMillimetersPerPixel = ParsePositiveScale(value, settings.XMillimetersPerPixel);
                }
                else if (inCameraSection && string.Equals(
                    key, "YMillimetersPerPixel", StringComparison.OrdinalIgnoreCase))
                {
                    settings.YMillimetersPerPixel = ParsePositiveScale(value, settings.YMillimetersPerPixel);
                }
                else if (inCameraSection && string.Equals(
                    key, "MillimeterConversionEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    settings.MillimeterConversionEnabled = ParseBoolean(value, false);
                }
                else if (inNetworkSection && string.Equals(
                    key, "ListenIpAddress", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ListenIpAddress = value;
                }
                else if (inNetworkSection && string.Equals(
                    key, "ListenPort", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ListenPort = ParseIntegerInRange(value, 5000, 1024, 65535);
                }
            }

            return settings;
        }

        public void Save(CameraDeviceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var content = new StringBuilder();
            content.AppendLine("[Application]");
            content.Append("ProgramName=").AppendLine((settings.ProgramName ?? string.Empty).Trim());
            content.Append("SoftwareId=").AppendLine(ClampSoftwareId(settings.SoftwareId).ToString(CultureInfo.InvariantCulture));
            content.AppendLine("[Camera]");
            content.Append("XMillimetersPerPixel=")
                .AppendLine(settings.XMillimetersPerPixel.ToString("R", CultureInfo.InvariantCulture));
            content.Append("YMillimetersPerPixel=")
                .AppendLine(settings.YMillimetersPerPixel.ToString("R", CultureInfo.InvariantCulture));
            content.Append("MillimeterConversionEnabled=")
                .AppendLine(settings.MillimeterConversionEnabled ? "true" : "false");
            content.AppendLine("[Network]");
            content.Append("ListenIpAddress=").AppendLine(string.IsNullOrWhiteSpace(settings.ListenIpAddress)
                ? "0.0.0.0"
                : settings.ListenIpAddress.Trim());
            content.Append("ListenPort=").AppendLine(Math.Max(1024, Math.Min(65535, settings.ListenPort)).ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(filePath, content.ToString(), new UTF8Encoding(false));
        }

        private static double ParsePositiveScale(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                parsed > 0.0 && !double.IsNaN(parsed) && !double.IsInfinity(parsed)
                ? parsed
                : fallback;
        }

        private static int ParseSoftwareId(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) &&
                parsed >= 1 && parsed <= 10
                ? parsed
                : fallback;
        }

        private static int ClampSoftwareId(int value)
        {
            return Math.Max(1, Math.Min(10, value));
        }

        private static int ParseIntegerInRange(string value, int fallback, int minimum, int maximum)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) &&
                parsed >= minimum && parsed <= maximum
                ? parsed
                : fallback;
        }

        private static bool ParseBoolean(string value, bool fallback)
        {
            bool parsed;
            if (bool.TryParse(value, out parsed))
            {
                return parsed;
            }

            if (string.Equals(value, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(value, "0", StringComparison.Ordinal))
            {
                return false;
            }

            return fallback;
        }
    }
}
