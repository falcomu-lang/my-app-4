using System;
using System.Drawing;
using CameraCaptureApp.Services;

namespace CameraCaptureApp.Services
{
    public sealed class GrayWaveformSelectionEventArgs : EventArgs
    {
        public GrayWaveformSelectionEventArgs(IGrayPixelSource pixelSource, Point startPoint, Point endPoint, Point[] linePoints)
        {
            PixelSource = pixelSource;
            StartPoint = startPoint;
            EndPoint = endPoint;
            LinePoints = linePoints ?? new Point[0];
        }

        public IGrayPixelSource PixelSource { get; private set; }

        public Point StartPoint { get; private set; }

        public Point EndPoint { get; private set; }

        public Point[] LinePoints { get; private set; }
    }
}
