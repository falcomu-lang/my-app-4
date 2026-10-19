using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using CameraCaptureApp.Services;

namespace CameraCaptureApp.Forms
{
    public partial class GrayScaleWaveformForm : Form
    {
        private readonly IGrayPixelSource _pixelSource;
        private readonly Point[] _linePoints;
        private readonly int[] _grayValues;
        private readonly int _minGray;
        private readonly int _maxGray;
        private readonly Font _axisLabelFont;
        private float _viewXMin;
        private float _viewXMax;
        private float _viewYMin;
        private float _viewYMax;
        private bool _isSelectingZoom;
        private Point _selectionStart;
        private Point _selectionEnd;

        public GrayScaleWaveformForm(IGrayPixelSource pixelSource, Point[] linePoints)
        {
            _pixelSource = pixelSource;
            _linePoints = linePoints ?? new Point[0];
            InitializeComponent();
            _axisLabelFont = new Font(Font.FontFamily, Math.Max(7f, Font.Size - 1f));
            panelChart.MouseWheel += panelChart_MouseWheel;
            if (_pixelSource == null || _linePoints.Length == 0)
            {
                buttonClose.Text = "關閉";
                labelInfo.Text = "No waveform data.";
                return;
            }

            _grayValues = SampleGrayValues(_pixelSource, _linePoints);
            _minGray = _grayValues.Min();
            _maxGray = _grayValues.Max();
            ResetView();
            labelInfo.Text = "Points: " + _grayValues.Length + " | Gray range: " + _minGray + " - " + _maxGray;
            panelChart.Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _axisLabelFont?.Dispose();
            _pixelSource?.Dispose();

            base.OnFormClosed(e);
        }

        private void panelChart_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(20, 24, 35));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_grayValues == null || _grayValues.Length == 0)
            {
                using (var brush = new SolidBrush(Color.Gainsboro))
                {
                    e.Graphics.DrawString("No waveform data.", Font, brush, 16, 16);
                }

                return;
            }

            var bounds = GetChartBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            DrawAxes(e.Graphics, bounds);
            DrawWaveform(e.Graphics, bounds);
            DrawSelectionRectangle(e.Graphics);
        }

        private void DrawAxes(Graphics graphics, Rectangle bounds)
        {
            DrawHorizontalGridLines(graphics, bounds);

            using (var pen = new Pen(Color.FromArgb(120, 135, 155), 1f))
            {
                graphics.DrawRectangle(pen, bounds);
                graphics.DrawLine(pen, bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
                graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
            }

            using (var brush = new SolidBrush(Color.Gainsboro))
            {
                graphics.DrawString(((int)Math.Round(_viewXMin)).ToString(), Font, brush, bounds.Left, bounds.Bottom + 5);
                graphics.DrawString(((int)Math.Round(_viewXMax)).ToString(), Font, brush, bounds.Right - 42, bounds.Bottom + 5);
            }
        }

        private void DrawHorizontalGridLines(Graphics graphics, Rectangle bounds)
        {
            using (var gridPen = new Pen(Color.FromArgb(42, 120, 135, 155), 1f))
            using (var majorPen = new Pen(Color.FromArgb(70, 150, 165, 185), 1f))
            using (var brush = new SolidBrush(Color.FromArgb(170, 210, 215, 225)))
            {
                if (_viewYMin <= 0f && _viewYMax >= 0f)
                {
                    var y = ValueToChartY(0f, bounds);
                    graphics.DrawString("0", _axisLabelFont, brush, bounds.Left - 24, y - 7f);
                }

                for (var gray = 16; gray <= 255; gray += 16)
                {
                    if (gray < _viewYMin || gray > _viewYMax)
                    {
                        continue;
                    }

                    var y = ValueToChartY(gray, bounds);
                    var pen = gray % 64 == 0 || gray == 255 ? majorPen : gridPen;
                    graphics.DrawLine(pen, bounds.Left, y, bounds.Right, y);
                    graphics.DrawString(gray.ToString(), _axisLabelFont, brush, bounds.Left - 34, y - 7f);
                }

                if (_viewYMin <= 255f && _viewYMax >= 255f)
                {
                    var y = ValueToChartY(255f, bounds);
                    graphics.DrawLine(majorPen, bounds.Left, y, bounds.Right, y);
                    graphics.DrawString("255", _axisLabelFont, brush, bounds.Left - 34, y - 7f);
                }
            }
        }

        private void DrawWaveform(Graphics graphics, Rectangle bounds)
        {
            if (_grayValues.Length < 2)
            {
                return;
            }

            var points = new List<PointF>();
            for (var i = 0; i < _grayValues.Length; i++)
            {
                if (i < _viewXMin || i > _viewXMax)
                {
                    continue;
                }

                var x = ValueToChartX(i, bounds);
                var y = ValueToChartY(_grayValues[i], bounds);
                points.Add(new PointF(x, y));
            }

            using (var pen = new Pen(Color.FromArgb(90, 200, 255), 2f))
            {
                if (points.Count >= 2)
                {
                    graphics.DrawLines(pen, points.ToArray());
                }
            }

            using (var brush = new SolidBrush(Color.Gainsboro))
            {
                graphics.DrawString("X: line position", Font, brush, bounds.Left, bounds.Top - 22);
                graphics.DrawString("Y: gray value", Font, brush, bounds.Right - 120, bounds.Top - 22);
            }
        }

        private void DrawSelectionRectangle(Graphics graphics)
        {
            if (!_isSelectingZoom)
            {
                return;
            }

            var rect = GetSelectionRectangle();
            if (rect.Width < 2 || rect.Height < 2)
            {
                return;
            }

            using (var brush = new SolidBrush(Color.FromArgb(45, 90, 200, 255)))
            using (var pen = new Pen(Color.FromArgb(170, 90, 200, 255), 1f))
            {
                graphics.FillRectangle(brush, rect);
                graphics.DrawRectangle(pen, rect);
            }
        }

        private void ResetView()
        {
            _viewXMin = 0f;
            _viewXMax = Math.Max(1, _grayValues.Length - 1);
            _viewYMin = 0f;
            _viewYMax = 255f;
            _isSelectingZoom = false;
            _selectionStart = Point.Empty;
            _selectionEnd = Point.Empty;
            UpdateViewLabel();
        }

        private void UpdateViewLabel()
        {
            if (_grayValues == null || _grayValues.Length == 0)
            {
                return;
            }

            labelInfo.Text = string.Format(
                "Points: {0} | Gray range: {1} - {2} | View X: {3:0}-{4:0}, Y: {5:0}-{6:0}",
                _grayValues.Length,
                _minGray,
                _maxGray,
                _viewXMin,
                _viewXMax,
                _viewYMin,
                _viewYMax);
        }

        private float ValueToChartX(float xValue, Rectangle bounds)
        {
            var range = Math.Max(0.0001f, _viewXMax - _viewXMin);
            return bounds.Left + ((xValue - _viewXMin) / range * bounds.Width);
        }

        private float ValueToChartY(float yValue, Rectangle bounds)
        {
            var range = Math.Max(0.0001f, _viewYMax - _viewYMin);
            return bounds.Bottom - ((yValue - _viewYMin) / range * bounds.Height);
        }

        private float ChartToValueX(int x, Rectangle bounds)
        {
            var range = Math.Max(0.0001f, _viewXMax - _viewXMin);
            return _viewXMin + ((x - bounds.Left) / (float)Math.Max(1, bounds.Width) * range);
        }

        private float ChartToValueY(int y, Rectangle bounds)
        {
            var range = Math.Max(0.0001f, _viewYMax - _viewYMin);
            return _viewYMax - ((y - bounds.Top) / (float)Math.Max(1, bounds.Height) * range);
        }

        private Rectangle GetChartBounds()
        {
            var bounds = panelChart.ClientRectangle;
            bounds.Inflate(-42, -28);
            return bounds;
        }

        private Rectangle GetSelectionRectangle()
        {
            return Rectangle.FromLTRB(
                Math.Min(_selectionStart.X, _selectionEnd.X),
                Math.Min(_selectionStart.Y, _selectionEnd.Y),
                Math.Max(_selectionStart.X, _selectionEnd.X),
                Math.Max(_selectionStart.Y, _selectionEnd.Y));
        }

        private void ZoomAt(Point mousePoint, float factor)
        {
            var bounds = GetChartBounds();
            if (!bounds.Contains(mousePoint))
            {
                return;
            }

            var centerX = ChartToValueX(mousePoint.X, bounds);
            var centerY = ChartToValueY(mousePoint.Y, bounds);
            var newXRange = Math.Max(1f, (_viewXMax - _viewXMin) * factor);
            var newYRange = Math.Max(1f, (_viewYMax - _viewYMin) * factor);
            ApplyView(centerX - newXRange / 2f, centerX + newXRange / 2f, centerY - newYRange / 2f, centerY + newYRange / 2f);
        }

        private void ApplyView(float xMin, float xMax, float yMin, float yMax)
        {
            if (xMax <= xMin || yMax <= yMin)
            {
                return;
            }

            var maxX = Math.Max(1, _grayValues.Length - 1);
            _viewXMin = Math.Max(0f, Math.Min(maxX - 1f, xMin));
            _viewXMax = Math.Max(_viewXMin + 1f, Math.Min(maxX, xMax));
            _viewYMin = Math.Max(0f, Math.Min(254f, yMin));
            _viewYMax = Math.Max(_viewYMin + 1f, Math.Min(255f, yMax));
            UpdateViewLabel();
            panelChart.Invalidate();
        }

        private static int[] SampleGrayValues(IGrayPixelSource pixelSource, Point[] linePoints)
        {
            var result = new int[linePoints.Length];
            pixelSource.GetGrayValues(linePoints, result);
            return result;
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void panelChart_MouseDown(object sender, MouseEventArgs e)
        {
            panelChart.Focus();
            if (e.Button == MouseButtons.Right)
            {
                ResetView();
                panelChart.Invalidate();
                return;
            }

            if (e.Button != MouseButtons.Left || _grayValues == null || _grayValues.Length == 0)
            {
                return;
            }

            var bounds = GetChartBounds();
            if (!bounds.Contains(e.Location))
            {
                return;
            }

            _isSelectingZoom = true;
            _selectionStart = e.Location;
            _selectionEnd = e.Location;
            panelChart.Invalidate();
        }

        private void panelChart_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelectingZoom)
            {
                return;
            }

            _selectionEnd = e.Location;
            panelChart.Invalidate();
        }

        private void panelChart_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isSelectingZoom)
            {
                return;
            }

            _selectionEnd = e.Location;
            _isSelectingZoom = false;
            var rect = GetSelectionRectangle();
            var bounds = GetChartBounds();
            var clipped = Rectangle.Intersect(rect, bounds);
            if (clipped.Width >= 6 && clipped.Height >= 6)
            {
                var xMin = ChartToValueX(clipped.Left, bounds);
                var xMax = ChartToValueX(clipped.Right, bounds);
                var yMax = ChartToValueY(clipped.Top, bounds);
                var yMin = ChartToValueY(clipped.Bottom, bounds);
                ApplyView(xMin, xMax, yMin, yMax);
                return;
            }

            panelChart.Invalidate();
        }

        private void panelChart_MouseWheel(object sender, MouseEventArgs e)
        {
            if (_grayValues == null || _grayValues.Length == 0)
            {
                return;
            }

            ZoomAt(e.Location, e.Delta > 0 ? 0.8f : 1.25f);
        }

        private void panelChart_MouseEnter(object sender, EventArgs e)
        {
            panelChart.Focus();
        }
    }
}
