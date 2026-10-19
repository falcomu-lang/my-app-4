using System;
using System.Drawing;

namespace CameraCaptureApp.Services
{
    public sealed class CameraFrameEventArgs : EventArgs
    {
        public CameraFrameEventArgs(Bitmap frame)
        {
            Frame = frame;
        }

        public Bitmap Frame { get; private set; }
    }
}
