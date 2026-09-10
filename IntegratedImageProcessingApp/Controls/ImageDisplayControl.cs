using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IntegratedImageProcessingApp.Controls
{
    public partial class ImageDisplayControl : UserControl
    {
        private readonly object _imageLock = new object();
        private Bitmap _sourceBitmap;
        private int _imageVersion;
        private float _zoom = 1f;
        private PointF _imageOffset = PointF.Empty;
        private bool _isPanning;
        private Point _lastMousePoint;
        private bool _suppressViewChanged;
        private bool _isSelectingRoi;
        private bool _isDrawingRoi;
        private Point _roiStartPoint;
        private Point _roiCurrentPoint;
        private Rectangle? _roiOverlay;

        public event EventHandler ViewChanged;
        public event EventHandler<RoiSelectedEventArgs> RoiSelected;

        public ImageDisplayControl()
        {
            InitializeComponent();
            StatusText = "尚未載入圖片";
            ResolutionText = string.Empty;
        }

        public string TitleText
        {
            get { return titleLabel.Text; }
            set { titleLabel.Text = value; }
        }

        public string ResolutionText
        {
            get { return resolutionLabel.Text; }
            private set { resolutionLabel.Text = value; }
        }

        public string StatusText
        {
            get { return statusLabel.Text; }
            private set { statusLabel.Text = value; }
        }

        public ImageViewState ViewState
        {
            get
            {
                lock (_imageLock)
                {
                    return new ImageViewState(_zoom, _imageOffset);
                }
            }
        }

        public bool HasImage
        {
            get
            {
                lock (_imageLock)
                {
                    return _sourceBitmap != null;
                }
            }
        }

        public void ApplyViewState(ImageViewState viewState)
        {
            if (!HasImage)
            {
                return;
            }

            _suppressViewChanged = true;
            try
            {
                lock (_imageLock)
                {
                    _zoom = ClampZoom(viewState.Zoom);
                    _imageOffset = viewState.Offset;
                }

                UpdateStatusLabel();
                viewerPanel.Invalidate();
            }
            finally
            {
                _suppressViewChanged = false;
            }
        }

        public bool BeginRoiSelection()
        {
            if (!HasImage)
            {
                StatusText = "請先載入圖片";
                return false;
            }

            _isSelectingRoi = true;
            _isDrawingRoi = false;
            viewerPanel.Cursor = Cursors.Cross;
            StatusText = "拖曳滑鼠指定 ROI";
            viewerPanel.Focus();
            viewerPanel.Invalidate();
            return true;
        }

        public void CancelRoiSelection()
        {
            _isSelectingRoi = false;
            _isDrawingRoi = false;
            viewerPanel.Capture = false;
            viewerPanel.Cursor = Cursors.Default;
            viewerPanel.Invalidate();
        }

        public void SetRoiOverlay(Rectangle roi)
        {
            lock (_imageLock)
            {
                _roiOverlay = NormalizeImageRectangle(roi);
            }

            viewerPanel.Invalidate();
        }

        public void ClearRoiOverlay()
        {
            lock (_imageLock)
            {
                _roiOverlay = null;
            }

            viewerPanel.Invalidate();
        }

        public async Task LoadImageFromFileAsync(string filePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Image file path is required.", "filePath");
            }

            int version = Interlocked.Increment(ref _imageVersion);
            StatusText = "讀取圖片中...";

            Bitmap loadedBitmap = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var image = Image.FromStream(stream))
                    {
                        return new Bitmap(image);
                    }
                },
                cancellationToken);

            if (cancellationToken.IsCancellationRequested || version != _imageVersion)
            {
                loadedBitmap.Dispose();
                return;
            }

            SetImage(loadedBitmap);
        }

        public void SetImage(Bitmap bitmap)
        {
            lock (_imageLock)
            {
                DisposeCurrentImage();
                _sourceBitmap = bitmap;
            }

            FitImageToView();
            UpdateStatusLabel();
            viewerPanel.Invalidate();
            OnViewChanged();
        }

        public void ClearImage()
        {
            Interlocked.Increment(ref _imageVersion);
            lock (_imageLock)
            {
                DisposeCurrentImage();
                _zoom = 1f;
                _imageOffset = PointF.Empty;
            }

            ResolutionText = string.Empty;
            StatusText = "尚未載入圖片";
            viewerPanel.Invalidate();
        }

        private void viewerPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            e.Graphics.InterpolationMode = _isPanning ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;

            Bitmap bitmap;
            float zoom;
            PointF offset;
            Rectangle? roiOverlay;
            lock (_imageLock)
            {
                bitmap = _sourceBitmap;
                zoom = _zoom;
                offset = _imageOffset;
                roiOverlay = _roiOverlay;
            }

            if (bitmap == null || zoom <= 0f)
            {
                return;
            }

            e.Graphics.DrawImage(bitmap, offset.X, offset.Y, bitmap.Width * zoom, bitmap.Height * zoom);
            DrawRoiOverlay(e.Graphics, roiOverlay, zoom, offset);
            DrawActiveRoiSelection(e.Graphics);
        }

        private void viewerPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            int width;
            int height;
            float oldZoom;
            PointF oldOffset;
            if (!TryGetSourceMetrics(out width, out height, out oldZoom, out oldOffset))
            {
                return;
            }

            float zoomFactor = e.Delta > 0 ? 1.25f : 0.8f;
            float newZoom = ClampZoom(oldZoom * zoomFactor);
            if (Math.Abs(newZoom - oldZoom) < 0.0001f)
            {
                return;
            }

            float imageX = (e.X - oldOffset.X) / oldZoom;
            float imageY = (e.Y - oldOffset.Y) / oldZoom;

            lock (_imageLock)
            {
                _zoom = newZoom;
                _imageOffset = new PointF(
                    e.X - (imageX * newZoom),
                    e.Y - (imageY * newZoom));
            }

            UpdateStatusLabel();
            viewerPanel.Invalidate();
            OnViewChanged();
        }

        private void viewerPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_isSelectingRoi && e.Button == MouseButtons.Left && HasImage)
            {
                _isDrawingRoi = true;
                _roiStartPoint = e.Location;
                _roiCurrentPoint = e.Location;
                viewerPanel.Capture = true;
                viewerPanel.Invalidate();
                return;
            }

            lock (_imageLock)
            {
                if (e.Button != MouseButtons.Left || _sourceBitmap == null)
                {
                    return;
                }
            }

            _isPanning = true;
            _lastMousePoint = e.Location;
            viewerPanel.Cursor = Cursors.Hand;
        }

        private void viewerPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawingRoi)
            {
                _roiCurrentPoint = e.Location;
                viewerPanel.Invalidate();
                return;
            }

            if (!_isPanning)
            {
                return;
            }

            int deltaX = e.X - _lastMousePoint.X;
            int deltaY = e.Y - _lastMousePoint.Y;
            _lastMousePoint = e.Location;

            lock (_imageLock)
            {
                _imageOffset = new PointF(_imageOffset.X + deltaX, _imageOffset.Y + deltaY);
            }

            UpdateStatusLabel();
            viewerPanel.Invalidate();
            OnViewChanged();
        }

        private void viewerPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDrawingRoi)
            {
                _roiCurrentPoint = e.Location;
                _isDrawingRoi = false;
                _isSelectingRoi = false;
                viewerPanel.Capture = false;
                viewerPanel.Cursor = Cursors.Default;
                viewerPanel.Invalidate();
                FinishRoiSelection();
                return;
            }

            _isPanning = false;
            viewerPanel.Cursor = Cursors.Default;
            viewerPanel.Invalidate();
        }

        private void viewerPanel_MouseEnter(object sender, EventArgs e)
        {
            viewerPanel.Focus();
        }

        private void ImageDisplayControl_SizeChanged(object sender, EventArgs e)
        {
            if (!_isPanning)
            {
                FitImageToView();
                UpdateStatusLabel();
            }

            viewerPanel.Invalidate();
        }

        private void buttonFitToWindow_Click(object sender, EventArgs e)
        {
            FitImageToView();
            UpdateStatusLabel();
            viewerPanel.Invalidate();
            OnViewChanged();
        }

        private void FitImageToView()
        {
            lock (_imageLock)
            {
                if (_sourceBitmap == null)
                {
                    _zoom = 1f;
                    _imageOffset = PointF.Empty;
                    return;
                }

                Rectangle bounds = viewerPanel.ClientRectangle;
                bounds.Inflate(-8, -8);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    _zoom = 1f;
                    _imageOffset = PointF.Empty;
                    return;
                }

                float scaleX = bounds.Width / (float)_sourceBitmap.Width;
                float scaleY = bounds.Height / (float)_sourceBitmap.Height;
                _zoom = Math.Min(scaleX, scaleY);

                float drawWidth = _sourceBitmap.Width * _zoom;
                float drawHeight = _sourceBitmap.Height * _zoom;
                _imageOffset = new PointF(
                    bounds.X + ((bounds.Width - drawWidth) / 2f),
                    bounds.Y + ((bounds.Height - drawHeight) / 2f));
            }
        }

        private void UpdateStatusLabel()
        {
            lock (_imageLock)
            {
                if (_sourceBitmap == null)
                {
                    ResolutionText = string.Empty;
                    StatusText = "尚未載入圖片";
                    return;
                }

                ResolutionText = _sourceBitmap.Width + " x " + _sourceBitmap.Height;
                float imageX = (-_imageOffset.X) / _zoom;
                float imageY = (-_imageOffset.Y) / _zoom;
                StatusText = string.Format(
                    "Zoom {0:0.00}x | Offset {1:0},{2:0} | Image {3:0},{4:0}",
                    _zoom,
                    _imageOffset.X,
                    _imageOffset.Y,
                    imageX,
                    imageY);
            }
        }

        private bool TryGetSourceMetrics(out int width, out int height, out float zoom, out PointF offset)
        {
            lock (_imageLock)
            {
                width = _sourceBitmap != null ? _sourceBitmap.Width : 0;
                height = _sourceBitmap != null ? _sourceBitmap.Height : 0;
                zoom = _zoom;
                offset = _imageOffset;
                return width > 0 && height > 0;
            }
        }

        private void DrawRoiOverlay(Graphics graphics, Rectangle? roiOverlay, float zoom, PointF offset)
        {
            if (!roiOverlay.HasValue)
            {
                return;
            }

            RectangleF viewRectangle = ImageRectangleToViewRectangle(roiOverlay.Value, zoom, offset);
            using (var pen = new Pen(Color.LimeGreen, 2f))
            {
                graphics.DrawRectangle(
                    pen,
                    viewRectangle.X,
                    viewRectangle.Y,
                    viewRectangle.Width,
                    viewRectangle.Height);
            }
        }

        private void DrawActiveRoiSelection(Graphics graphics)
        {
            if (!_isDrawingRoi)
            {
                return;
            }

            Rectangle viewRectangle = NormalizeViewRectangle(_roiStartPoint, _roiCurrentPoint);
            if (viewRectangle.Width < 1 || viewRectangle.Height < 1)
            {
                return;
            }

            using (var pen = new Pen(Color.LimeGreen, 2f))
            {
                graphics.DrawRectangle(pen, viewRectangle);
            }
        }

        private void FinishRoiSelection()
        {
            Rectangle viewRectangle = NormalizeViewRectangle(_roiStartPoint, _roiCurrentPoint);
            if (viewRectangle.Width < 3 || viewRectangle.Height < 3)
            {
                StatusText = "ROI 範圍太小";
                return;
            }

            Rectangle imageRectangle;
            if (!TryConvertViewRectangleToImageRectangle(viewRectangle, out imageRectangle))
            {
                StatusText = "ROI 未落在圖片範圍內";
                return;
            }

            EventHandler<RoiSelectedEventArgs> handler = RoiSelected;
            if (handler != null)
            {
                handler(this, new RoiSelectedEventArgs(imageRectangle));
            }
        }

        private bool TryConvertViewRectangleToImageRectangle(Rectangle viewRectangle, out Rectangle imageRectangle)
        {
            lock (_imageLock)
            {
                imageRectangle = Rectangle.Empty;
                if (_sourceBitmap == null || _zoom <= 0f)
                {
                    return false;
                }

                float left = (viewRectangle.Left - _imageOffset.X) / _zoom;
                float top = (viewRectangle.Top - _imageOffset.Y) / _zoom;
                float right = (viewRectangle.Right - _imageOffset.X) / _zoom;
                float bottom = (viewRectangle.Bottom - _imageOffset.Y) / _zoom;

                int imageLeft = Math.Max(0, Math.Min(_sourceBitmap.Width, (int)Math.Floor(left)));
                int imageTop = Math.Max(0, Math.Min(_sourceBitmap.Height, (int)Math.Floor(top)));
                int imageRight = Math.Max(0, Math.Min(_sourceBitmap.Width, (int)Math.Ceiling(right)));
                int imageBottom = Math.Max(0, Math.Min(_sourceBitmap.Height, (int)Math.Ceiling(bottom)));

                if (imageRight <= imageLeft || imageBottom <= imageTop)
                {
                    return false;
                }

                imageRectangle = Rectangle.FromLTRB(imageLeft, imageTop, imageRight, imageBottom);
                return true;
            }
        }

        private static Rectangle NormalizeViewRectangle(Point start, Point end)
        {
            int left = Math.Min(start.X, end.X);
            int top = Math.Min(start.Y, end.Y);
            int right = Math.Max(start.X, end.X);
            int bottom = Math.Max(start.Y, end.Y);
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static Rectangle NormalizeImageRectangle(Rectangle rectangle)
        {
            int left = Math.Min(rectangle.Left, rectangle.Right);
            int top = Math.Min(rectangle.Top, rectangle.Bottom);
            int right = Math.Max(rectangle.Left, rectangle.Right);
            int bottom = Math.Max(rectangle.Top, rectangle.Bottom);
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static RectangleF ImageRectangleToViewRectangle(Rectangle rectangle, float zoom, PointF offset)
        {
            return new RectangleF(
                offset.X + (rectangle.X * zoom),
                offset.Y + (rectangle.Y * zoom),
                rectangle.Width * zoom,
                rectangle.Height * zoom);
        }

        private static float ClampZoom(float zoom)
        {
            if (zoom < 0.02f)
            {
                return 0.02f;
            }

            if (zoom > 50f)
            {
                return 50f;
            }

            return zoom;
        }

        private void OnViewChanged()
        {
            if (_suppressViewChanged)
            {
                return;
            }

            EventHandler handler = ViewChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void DisposeCurrentImage()
        {
            if (_sourceBitmap != null)
            {
                _sourceBitmap.Dispose();
                _sourceBitmap = null;
            }
        }
    }

    public struct ImageViewState
    {
        public ImageViewState(float zoom, PointF offset)
        {
            Zoom = zoom;
            Offset = offset;
        }

        public float Zoom { get; private set; }

        public PointF Offset { get; private set; }
    }

    public class RoiSelectedEventArgs : EventArgs
    {
        public RoiSelectedEventArgs(Rectangle roi)
        {
            Roi = roi;
        }

        public Rectangle Roi { get; private set; }
    }
}
