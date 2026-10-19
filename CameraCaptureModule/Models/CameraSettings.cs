namespace CameraCaptureApp.Models
{
    public class CameraSettings
    {
        public string CameraName { get; set; }

        public string ConfigFilePath { get; set; }

        public string ServerName { get; set; }

        public int ServerIndex { get; set; }

        public int ResourceIndex { get; set; }

        public string DeviceFeatureServerName { get; set; }

        public string DeviceFeatureConfigFilePath { get; set; }

        public int DeviceFeatureResourceIndex { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Length { get; set; }

        public bool RollingCaptureEnabled { get; set; }

        public int RollingCaptureFrameCount { get; set; }

        public RollingCaptureDirection RollingCaptureDirection { get; set; }

        public decimal ExposureTime { get; set; }

        public decimal Gain { get; set; }

        public decimal InternalLineRate { get; set; }

        public decimal FrameRate { get; set; }

        public string PixelFormat { get; set; }

        public TriggerMode TriggerMode { get; set; }

        public bool ExternalFrameTriggerOneFrame { get; set; }

        public bool ExternalFrameTriggerOneFrameCompareFromEncoder { get; set; }

        public bool ExternalFrameTriggerOneFrameSetEncoderOnTrigger { get; set; }

        public bool AutoConnect { get; set; }

        public bool AutoSave { get; set; }

        public bool AutoSaveOnExternalTriggerOneFrame { get; set; }

        public bool AutoSaveOnSoftwareTriggerFrame { get; set; }

        public string SaveFolder { get; set; }

        public string FileNamePattern { get; set; }

        public ImageSaveFormat ImageSaveFormat { get; set; }

        public int MeterWheelCompareIncrement { get; set; }

        public int MeterWheelEncoderValue { get; set; }

        public int MeterWheelCompareValue { get; set; }

        public int MeterWheelCardId { get; set; }

        public int MeterWheelMultipleRate { get; set; }

        public bool MeterWheelReverseDirection { get; set; }

        public int MeterWheelCmpOutWidth { get; set; }

        public int MeterWheelExtensionCompareMask { get; set; }

        public string MeterWheelExtensionCompareOffsets { get; set; }

        public string MeterWheelExtensionComparePulseWidths { get; set; }

        public int MeterWheelExtensionCompareOutputStates { get; set; }

        public static CameraSettings CreateDefault()
        {
            return new CameraSettings
            {
                CameraName = "Default Camera",
                ConfigFilePath = string.Empty,
                ServerName = string.Empty,
                ServerIndex = -1,
                ResourceIndex = 0,
                DeviceFeatureServerName = string.Empty,
                DeviceFeatureConfigFilePath = string.Empty,
                DeviceFeatureResourceIndex = -1,
                Width = 1280,
                Height = 720,
                Length = 720,
                RollingCaptureEnabled = false,
                RollingCaptureFrameCount = 12,
                RollingCaptureDirection = RollingCaptureDirection.TopToBottom,
                ExposureTime = 1200,
                Gain = 1,
                InternalLineRate = 30,
                FrameRate = 30,
                PixelFormat = "Mono8",
                TriggerMode = TriggerMode.Continuous,
                ExternalFrameTriggerOneFrame = false,
                ExternalFrameTriggerOneFrameCompareFromEncoder = false,
                ExternalFrameTriggerOneFrameSetEncoderOnTrigger = false,
                AutoConnect = false,
                AutoSave = false,
                AutoSaveOnExternalTriggerOneFrame = false,
                AutoSaveOnSoftwareTriggerFrame = false,
                SaveFolder = string.Empty,
                FileNamePattern = "capture_{yyyyMMdd_HHmmss}",
                ImageSaveFormat = ImageSaveFormat.Png,
                MeterWheelCompareIncrement = 0,
                MeterWheelEncoderValue = 0,
                MeterWheelCompareValue = 0,
                MeterWheelCardId = 0,
                MeterWheelMultipleRate = 0,
                MeterWheelReverseDirection = false,
                MeterWheelCmpOutWidth = 0,
                MeterWheelExtensionCompareMask = 0,
                MeterWheelExtensionCompareOffsets = "0,0,0,0,0,0,0,0",
                MeterWheelExtensionComparePulseWidths = "0,0,0,0,0,0,0,0",
                MeterWheelExtensionCompareOutputStates = 0
            };
        }

        public CameraSettings Clone()
        {
            return (CameraSettings)MemberwiseClone();
        }
    }
}
