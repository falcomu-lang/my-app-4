using CameraCaptureApp.Models;

namespace CameraCaptureApp.Services
{
    public interface ISettingsService
    {
        CameraSettings Load();

        void Save(CameraSettings settings);
    }
}
