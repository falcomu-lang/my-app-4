using System;

namespace CameraCaptureApp.Services
{
    /// <summary>
    /// Provides completed full-resolution camera frames to subscribers. Subscribers should
    /// copy or enqueue the image quickly and must not retain the event-owned bitmap.
    /// </summary>
    public interface ICameraImageSource
    {
        /// <summary>
        /// Raised on the camera transfer callback thread. CreateImageCopy must be called
        /// before the event handler returns, and the returned bitmap must be disposed by its caller.
        /// </summary>
        event EventHandler<CameraImageAcquiredEventArgs> ImageAcquired;
    }
}
