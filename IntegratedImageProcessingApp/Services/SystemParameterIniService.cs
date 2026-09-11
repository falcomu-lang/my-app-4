using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace IntegratedImageProcessingApp.Services
{
    public class SystemParameterIniService
    {
        private const string SectionSystem = "System";
        private const string SectionRoi = "ROI";
        private const string SectionRois = "ROIs";
        private const string SectionImageProcessing = "ImageProcessing";
        private readonly string filePath;

        public SystemParameterIniService(string filePath)
        {
            this.filePath = filePath;
        }

        public SystemParameterSettings Load()
        {
            var settings = new SystemParameterSettings();
            Dictionary<string, Dictionary<string, string>> sections = ReadSections();

            settings.LastImagePath = GetValue(sections, SectionSystem, "LastImagePath", string.Empty);
            settings.RoiEnabled = GetBool(sections, SectionRoi, "Enabled", false);
            settings.Roi = new Rectangle(
                GetInt(sections, SectionRoi, "X", 0),
                GetInt(sections, SectionRoi, "Y", 0),
                GetInt(sections, SectionRoi, "Width", 0),
                GetInt(sections, SectionRoi, "Height", 0));

            if (settings.Roi.Width <= 0 || settings.Roi.Height <= 0)
            {
                settings.RoiEnabled = false;
            }

            int roiCount = GetInt(sections, SectionRois, "Count", 0);
            for (int index = 1; index <= roiCount; index++)
            {
                var roi = new Rectangle(
                    GetInt(sections, SectionRois, "Roi" + index + ".X", 0),
                    GetInt(sections, SectionRois, "Roi" + index + ".Y", 0),
                    GetInt(sections, SectionRois, "Roi" + index + ".Width", 0),
                    GetInt(sections, SectionRois, "Roi" + index + ".Height", 0));
                if (roi.Width > 0 && roi.Height > 0)
                {
                    settings.RoiRegions.Add(new RoiRegionSettings { Bounds = roi });
                }
            }

            if (settings.RoiRegions.Count == 0 && settings.RoiEnabled)
            {
                settings.RoiRegions.Add(new RoiRegionSettings { Bounds = settings.Roi });
            }
            else if (settings.RoiRegions.Count > 0)
            {
                settings.RoiEnabled = true;
                settings.Roi = settings.RoiRegions[0].Bounds;
            }

            int imageProcessingStepCount = GetInt(sections, SectionImageProcessing, "Count", 0);
            for (int index = 1; index <= imageProcessingStepCount; index++)
            {
                settings.ImageProcessingSteps.Add(new ImageProcessingStepSettings
                {
                    Method = GetValue(sections, SectionImageProcessing, "Step" + index + ".Method", string.Empty),
                    Parameters = GetValue(sections, SectionImageProcessing, "Step" + index + ".Parameters", string.Empty)
                });
            }

            return settings;
        }

        public void Save(SystemParameterSettings settings)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("[System]");
                writer.WriteLine("LastImagePath={0}", Escape(settings.LastImagePath));
                writer.WriteLine();
                writer.WriteLine("[ROI]");
                writer.WriteLine("Enabled={0}", settings.RoiEnabled ? "true" : "false");
                writer.WriteLine("X={0}", settings.Roi.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Y={0}", settings.Roi.Y.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Width={0}", settings.Roi.Width.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Height={0}", settings.Roi.Height.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine();
                writer.WriteLine("[ROIs]");
                writer.WriteLine("Count={0}", settings.RoiRegions.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.RoiRegions.Count; index++)
                {
                    Rectangle roi = settings.RoiRegions[index].Bounds;
                    string keyPrefix = "Roi" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.X={1}", keyPrefix, roi.X.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.Y={1}", keyPrefix, roi.Y.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.Width={1}", keyPrefix, roi.Width.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.Height={1}", keyPrefix, roi.Height.ToString(CultureInfo.InvariantCulture));
                }

                writer.WriteLine();
                writer.WriteLine("[ImageProcessing]");
                writer.WriteLine("Count={0}", settings.ImageProcessingSteps.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ImageProcessingSteps.Count; index++)
                {
                    ImageProcessingStepSettings step = settings.ImageProcessingSteps[index];
                    string keyPrefix = "Step" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Method={1}", keyPrefix, Escape(step.Method));
                    writer.WriteLine("{0}.Parameters={1}", keyPrefix, Escape(step.Parameters));
                }
            }
        }

        private Dictionary<string, Dictionary<string, string>> ReadSections()
        {
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(filePath))
            {
                return sections;
            }

            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!sections.ContainsKey(currentSection))
                    {
                        sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                sections[currentSection][key] = value;
            }

            return sections;
        }

        private static string GetValue(Dictionary<string, Dictionary<string, string>> sections, string section, string key, string defaultValue)
        {
            Dictionary<string, string> values;
            string value;
            return sections.TryGetValue(section, out values) && values.TryGetValue(key, out value)
                ? Unescape(value)
                : defaultValue;
        }

        private static int GetInt(Dictionary<string, Dictionary<string, string>> sections, string section, string key, int defaultValue)
        {
            int value;
            return int.TryParse(GetValue(sections, section, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, Dictionary<string, string>> sections, string section, string key, bool defaultValue)
        {
            bool value;
            return bool.TryParse(GetValue(sections, section, key, string.Empty), out value)
                ? value
                : defaultValue;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty).Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
        }
    }
}
