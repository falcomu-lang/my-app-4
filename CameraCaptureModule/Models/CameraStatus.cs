namespace CameraCaptureApp.Models
{
    public class CameraStatus
    {
        public bool IsConnected { get; set; }

        public bool IsPreviewing { get; set; }

        public bool IsCaptureInProgress { get; set; }

        public bool HasSignal { get; set; }

        public int FrameWidth { get; set; }

        public int FrameHeight { get; set; }

        public long ScannedLineCount { get; set; }

        public int UpdateRateHz { get; set; }

        public bool FollowLatestLine { get; set; }

        public string LastMessage { get; set; }

        public string CameraName { get; set; }

        public string ScanStateText { get; set; }
    }
}
