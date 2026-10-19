using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CameraCaptureApp.Models;

namespace CameraCaptureApp.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captureSetting.ini");
        }

        public CameraSettings Load()
        {
            if (!File.Exists(_settingsFilePath))
            {
                MigrateLegacySettingsIfPresent();
            }

            if (!File.Exists(_settingsFilePath))
            {
                var defaults = CameraSettings.CreateDefault();
                Save(defaults);
                return defaults.Clone();
            }

            var settings = CameraSettings.CreateDefault();
            var values = ReadIniValues();

            settings.CameraName = GetString(values, "CameraName", settings.CameraName);
            settings.ConfigFilePath = GetString(values, "ConfigFilePath", settings.ConfigFilePath);
            settings.ServerName = GetString(values, "ServerName", settings.ServerName);
            settings.ServerIndex = GetInt(values, "ServerIndex", settings.ServerIndex);
            settings.ResourceIndex = GetInt(values, "ResourceIndex", settings.ResourceIndex);
            settings.DeviceFeatureServerName = GetString(values, "DeviceFeatureServerName", settings.DeviceFeatureServerName);
            settings.DeviceFeatureConfigFilePath = GetString(values, "DeviceFeatureConfigFilePath", settings.DeviceFeatureConfigFilePath);
            settings.DeviceFeatureResourceIndex = GetInt(values, "DeviceFeatureResourceIndex", settings.DeviceFeatureResourceIndex);
            settings.Width = GetInt(values, "Width", settings.Width);
            settings.Height = GetInt(values, "Height", settings.Height);
            settings.Length = GetInt(values, "Length", settings.Length);
            settings.RollingCaptureEnabled = GetBool(values, "RollingCaptureEnabled", settings.RollingCaptureEnabled);
            settings.RollingCaptureFrameCount = GetInt(values, "RollingCaptureFrameCount", settings.RollingCaptureFrameCount);
            settings.RollingCaptureDirection = GetEnum(values, "RollingCaptureDirection", settings.RollingCaptureDirection);
            settings.ExposureTime = GetDecimal(values, "ExposureTime", settings.ExposureTime);
            settings.Gain = GetDecimal(values, "Gain", settings.Gain);
            settings.InternalLineRate = GetDecimal(values, "InternalLineRate", settings.InternalLineRate);
            settings.FrameRate = GetDecimal(values, "FrameRate", settings.FrameRate);
            settings.PixelFormat = GetString(values, "PixelFormat", settings.PixelFormat);
            settings.TriggerMode = GetTriggerMode(values, "TriggerMode", settings.TriggerMode);
            settings.ExternalFrameTriggerOneFrame = GetBool(values, "ExternalFrameTriggerOneFrame", settings.ExternalFrameTriggerOneFrame);
            settings.ExternalFrameTriggerOneFrameCompareFromEncoder = GetBool(values, "ExternalFrameTriggerOneFrameCompareFromEncoder", settings.ExternalFrameTriggerOneFrameCompareFromEncoder);
            settings.ExternalFrameTriggerOneFrameSetEncoderOnTrigger = GetBool(values, "ExternalFrameTriggerOneFrameSetEncoderOnTrigger", settings.ExternalFrameTriggerOneFrameSetEncoderOnTrigger);
            settings.AutoConnect = GetBool(values, "AutoConnect", settings.AutoConnect);
            settings.AutoSave = GetBool(values, "AutoSave", settings.AutoSave);
            settings.AutoSaveOnExternalTriggerOneFrame = GetBool(values, "AutoSaveOnExternalTriggerOneFrame", settings.AutoSaveOnExternalTriggerOneFrame);
            settings.AutoSaveOnSoftwareTriggerFrame = GetBool(values, "AutoSaveOnSoftwareTriggerFrame", settings.AutoSaveOnSoftwareTriggerFrame);
            settings.SaveFolder = GetString(values, "SaveFolder", settings.SaveFolder);
            settings.FileNamePattern = GetString(values, "FileNamePattern", settings.FileNamePattern);
            settings.ImageSaveFormat = GetEnum(values, "ImageSaveFormat", settings.ImageSaveFormat);
            settings.MeterWheelCompareIncrement = GetInt(values, "MeterWheelCompareIncrement", settings.MeterWheelCompareIncrement);
            settings.MeterWheelEncoderValue = GetInt(values, "MeterWheelEncoderValue", settings.MeterWheelEncoderValue);
            settings.MeterWheelCompareValue = GetInt(values, "MeterWheelCompareValue", settings.MeterWheelCompareValue);
            settings.MeterWheelCardId = GetInt(values, "MeterWheelCardId", settings.MeterWheelCardId);
            settings.MeterWheelMultipleRate = GetInt(values, "MeterWheelMultipleRate", settings.MeterWheelMultipleRate);
            settings.MeterWheelReverseDirection = GetBool(values, "MeterWheelReverseDirection", settings.MeterWheelReverseDirection);
            settings.MeterWheelCmpOutWidth = GetInt(values, "MeterWheelCmpOutWidth", settings.MeterWheelCmpOutWidth);
            settings.MeterWheelExtensionCompareMask = GetInt(values, "MeterWheelExtensionCompareMask", settings.MeterWheelExtensionCompareMask);
            settings.MeterWheelExtensionCompareOffsets = GetString(values, "MeterWheelExtensionCompareOffsets", settings.MeterWheelExtensionCompareOffsets);
            settings.MeterWheelExtensionComparePulseWidths = GetString(values, "MeterWheelExtensionComparePulseWidths", settings.MeterWheelExtensionComparePulseWidths);
            settings.MeterWheelExtensionCompareOutputStates = GetInt(values, "MeterWheelExtensionCompareOutputStates", settings.MeterWheelExtensionCompareOutputStates);

            return settings;
        }

        public void Save(CameraSettings settings)
        {
            var settingsDirectory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            var lines = new[]
            {
                "; CameraCaptureApp settings",
                "[Camera]",
                "CameraName=" + settings.CameraName,
                "ConfigFilePath=" + settings.ConfigFilePath,
                "ServerName=" + settings.ServerName,
                "ServerIndex=" + settings.ServerIndex.ToString(CultureInfo.InvariantCulture),
                "ResourceIndex=" + settings.ResourceIndex.ToString(CultureInfo.InvariantCulture),
                "DeviceFeatureServerName=" + settings.DeviceFeatureServerName,
                "DeviceFeatureConfigFilePath=" + settings.DeviceFeatureConfigFilePath,
                "DeviceFeatureResourceIndex=" + settings.DeviceFeatureResourceIndex.ToString(CultureInfo.InvariantCulture),
                "Width=" + settings.Width.ToString(CultureInfo.InvariantCulture),
                "Height=" + settings.Height.ToString(CultureInfo.InvariantCulture),
                "Length=" + settings.Length.ToString(CultureInfo.InvariantCulture),
                "RollingCaptureEnabled=" + settings.RollingCaptureEnabled.ToString(),
                "RollingCaptureFrameCount=" + settings.RollingCaptureFrameCount.ToString(CultureInfo.InvariantCulture),
                "RollingCaptureDirection=" + settings.RollingCaptureDirection.ToString(),
                "ExposureTime=" + settings.ExposureTime.ToString(CultureInfo.InvariantCulture),
                "Gain=" + settings.Gain.ToString(CultureInfo.InvariantCulture),
                "InternalLineRate=" + settings.InternalLineRate.ToString(CultureInfo.InvariantCulture),
                "FrameRate=" + settings.FrameRate.ToString(CultureInfo.InvariantCulture),
                "PixelFormat=" + settings.PixelFormat,
                "TriggerMode=" + settings.TriggerMode.ToString(),
                "ExternalFrameTriggerOneFrame=" + settings.ExternalFrameTriggerOneFrame.ToString(),
                "ExternalFrameTriggerOneFrameCompareFromEncoder=" + settings.ExternalFrameTriggerOneFrameCompareFromEncoder.ToString(),
                "ExternalFrameTriggerOneFrameSetEncoderOnTrigger=" + settings.ExternalFrameTriggerOneFrameSetEncoderOnTrigger.ToString(),
                "AutoConnect=" + settings.AutoConnect.ToString(),
                "AutoSave=" + settings.AutoSave.ToString(),
                "AutoSaveOnExternalTriggerOneFrame=" + settings.AutoSaveOnExternalTriggerOneFrame.ToString(),
                "AutoSaveOnSoftwareTriggerFrame=" + settings.AutoSaveOnSoftwareTriggerFrame.ToString(),
                "SaveFolder=" + settings.SaveFolder,
                "FileNamePattern=" + settings.FileNamePattern,
                "ImageSaveFormat=" + settings.ImageSaveFormat.ToString(),
                "MeterWheelCompareIncrement=" + settings.MeterWheelCompareIncrement.ToString(CultureInfo.InvariantCulture),
                "MeterWheelEncoderValue=" + settings.MeterWheelEncoderValue.ToString(CultureInfo.InvariantCulture),
                "MeterWheelCompareValue=" + settings.MeterWheelCompareValue.ToString(CultureInfo.InvariantCulture),
                "MeterWheelCardId=" + settings.MeterWheelCardId.ToString(CultureInfo.InvariantCulture),
                "MeterWheelMultipleRate=" + settings.MeterWheelMultipleRate.ToString(CultureInfo.InvariantCulture),
                "MeterWheelReverseDirection=" + settings.MeterWheelReverseDirection.ToString(),
                "MeterWheelCmpOutWidth=" + settings.MeterWheelCmpOutWidth.ToString(CultureInfo.InvariantCulture),
                "MeterWheelExtensionCompareMask=" + settings.MeterWheelExtensionCompareMask.ToString(CultureInfo.InvariantCulture),
                "MeterWheelExtensionCompareOffsets=" + settings.MeterWheelExtensionCompareOffsets,
                "MeterWheelExtensionComparePulseWidths=" + settings.MeterWheelExtensionComparePulseWidths,
                "MeterWheelExtensionCompareOutputStates=" + settings.MeterWheelExtensionCompareOutputStates.ToString(CultureInfo.InvariantCulture)
            };

            File.WriteAllLines(_settingsFilePath, lines, Encoding.UTF8);
        }

        private void MigrateLegacySettingsIfPresent()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var legacyPaths = new[]
            {
                Path.Combine(baseDirectory, "settings.ini"),
                Path.Combine(baseDirectory, "setting.ini")
            };

            foreach (var legacyPath in legacyPaths)
            {
                if (File.Exists(legacyPath))
                {
                    File.Copy(legacyPath, _settingsFilePath);
                    return;
                }
            }
        }

        private Dictionary<string, string> ReadIniValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(_settingsFilePath, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim();
                values[key] = value;
            }

            return values;
        }

        private static string GetString(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : fallback;
        }

        private static int GetInt(Dictionary<string, string> values, string key, int fallback)
        {
            string value;
            int parsed;
            return values.TryGetValue(key, out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static decimal GetDecimal(Dictionary<string, string> values, string key, decimal fallback)
        {
            string value;
            decimal parsed;
            return values.TryGetValue(key, out value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool parsed;
            return values.TryGetValue(key, out value) && bool.TryParse(value, out parsed)
                ? parsed
                : fallback;
        }

        private static TriggerMode GetTriggerMode(Dictionary<string, string> values, string key, TriggerMode fallback)
        {
            return GetEnum(values, key, fallback);
        }

        private static TEnum GetEnum<TEnum>(Dictionary<string, string> values, string key, TEnum fallback)
            where TEnum : struct
        {
            string value;
            TEnum parsed;
            return values.TryGetValue(key, out value) && Enum.TryParse(value, true, out parsed)
                ? parsed
                : fallback;
        }
    }
}
