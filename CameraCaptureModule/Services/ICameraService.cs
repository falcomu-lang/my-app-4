using CameraCaptureApp.Models;

namespace CameraCaptureApp.Services
{
    public interface ICameraService
    {
        event System.EventHandler<CameraFrameEventArgs> FrameReady;

        event System.EventHandler<CameraImageAcquiredEventArgs> ImageAcquired;

        event System.EventHandler ExternalTriggerReceived;

        CameraSettings CurrentSettings { get; }

        CameraStatus Status { get; }

        bool IsTransferStartBlocked { get; }

        void ApplySettings(CameraSettings settings);

        bool Connect();

        bool SelectConnectionSettings(System.Windows.Forms.IWin32Window owner);

        bool SelectDeviceFeatureSettings(System.Windows.Forms.IWin32Window owner);

        string ExportLiveFeatureReport();

        string ExportAcquisitionParameterReport();

        void Disconnect();

        bool StartPreview();

        void StopPreview();

        bool CaptureFrame();
    }
}
