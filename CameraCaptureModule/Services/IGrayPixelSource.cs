using System;
using System.Drawing;

namespace CameraCaptureApp.Services
{
    public interface IGrayPixelSource : IDisposable
    {
        int Width { get; }

        int Height { get; }

        int GetGrayAt(int x, int y);

        void GetGrayValues(Point[] points, int[] destination);
    }
}
