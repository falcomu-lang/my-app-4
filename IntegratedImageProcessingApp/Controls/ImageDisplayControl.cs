using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IntegratedImageProcessingApp.Controls
{
    public partial class ImageDisplayControl : UserControl
    {
        private const int TileSourceSize = 1024;
        private const float TileRenderZoomThreshold = 0.12f;
        private const float TilePreviewHandoffRatio = 1.02f;
        private const float CachedTilePanZoomThreshold = 0.22f;
        private const int MaxCachedTilesWhilePanning = 48;
        private const int TileRefreshIntervalMs = 33;
        private const long MaxDisplayPixels = 50000000L;
        private readonly object _imageLock = new object();
        private readonly System.Windows.Forms.Timer _tileRefreshTimer;
        private Bitmap _sourceBitmap;
        private LargeImageSource _largeImageSource;
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
        private bool _tileRefreshPending;

        public event EventHandler ViewChanged;
        public event EventHandler<RoiSelectedEventArgs> RoiSelected;

        public ImageDisplayControl()
        {
            InitializeComponent();
            _tileRefreshTimer = new System.Windows.Forms.Timer();
            _tileRefreshTimer.Interval = TileRefreshIntervalMs;
            _tileRefreshTimer.Tick += TileRefreshTimer_Tick;
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
                    return _sourceBitmap != null || _largeImageSource != null;
                }
            }
        }

        public Bitmap CloneImage()
        {
            lock (_imageLock)
            {
                if (_sourceBitmap != null)
                {
                    return new Bitmap(_sourceBitmap);
                }

                if (_largeImageSource != null)
                {
                    return new Bitmap(_largeImageSource.CreateSnapshotBitmap());
                }

                return null;
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
                    using (var image = Image.FromStream(stream, false, false))
                    {
                        long sourcePixels = (long)image.Width * image.Height;
                        if (sourcePixels > MaxDisplayPixels)
                        {
                            return null;
                        }

                        return CreateDisplayBitmap(image);
                    }
                },
                cancellationToken);

            if (cancellationToken.IsCancellationRequested || version != _imageVersion)
            {
                loadedBitmap.Dispose();
                return;
            }

            if (loadedBitmap == null)
            {
                var largeImageSource = await Task.Run(() => new LargeImageSource(filePath), cancellationToken);
                if (cancellationToken.IsCancellationRequested || version != _imageVersion)
                {
                    largeImageSource.Dispose();
                    return;
                }

                SetLargeImageSource(largeImageSource);
                return;
            }

            SetImage(loadedBitmap);
        }

        private void SetLargeImageSource(LargeImageSource largeImageSource)
        {
            if (largeImageSource == null)
            {
                throw new ArgumentNullException("largeImageSource");
            }

            lock (_imageLock)
            {
                DisposeCurrentImage();
                _largeImageSource = largeImageSource;
            }

            FitImageToView();
            UpdateStatusLabel();
            viewerPanel.Invalidate();
            largeImageSource.QueuePreviewBuilds(ScheduleTileRefresh);
            OnViewChanged();
        }

        public void SetImage(Bitmap bitmap)
        {
            Bitmap displayBitmap = EnsureGrayscaleBitmap(bitmap);
            if (!ReferenceEquals(displayBitmap, bitmap))
            {
                bitmap.Dispose();
            }

            lock (_imageLock)
            {
                DisposeCurrentImage();
                _sourceBitmap = displayBitmap;
            }

            FitImageToView();
            UpdateStatusLabel();
            viewerPanel.Invalidate();
            OnViewChanged();
        }

        public void SetDisplayImage(Bitmap bitmap, bool preserveView)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException("bitmap");
            }

            bool canPreserveView;
            lock (_imageLock)
            {
                canPreserveView = preserveView &&
                    _sourceBitmap != null &&
                    _largeImageSource == null &&
                    _sourceBitmap.Width == bitmap.Width &&
                    _sourceBitmap.Height == bitmap.Height;
                DisposeCurrentImage();
                _sourceBitmap = bitmap;
            }

            if (!canPreserveView)
            {
                FitImageToView();
            }

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
            LargeImageSource largeImageSource;
            float zoom;
            PointF offset;
            Rectangle? roiOverlay;
            lock (_imageLock)
            {
                bitmap = _sourceBitmap;
                largeImageSource = _largeImageSource;
                zoom = _zoom;
                offset = _imageOffset;
                roiOverlay = _roiOverlay;
            }

            if ((bitmap == null && largeImageSource == null) || zoom <= 0f)
            {
                return;
            }

            if (bitmap != null)
            {
                e.Graphics.DrawImage(bitmap, offset.X, offset.Y, bitmap.Width * zoom, bitmap.Height * zoom);
            }
            else
            {
                DrawLargeImage(e.Graphics, largeImageSource, zoom, offset);
            }

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
                if (e.Button != MouseButtons.Left || (_sourceBitmap == null && _largeImageSource == null))
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
                int imageWidth;
                int imageHeight;
                if (!TryGetImageSizeUnsafe(out imageWidth, out imageHeight))
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

                float scaleX = bounds.Width / (float)imageWidth;
                float scaleY = bounds.Height / (float)imageHeight;
                _zoom = Math.Min(scaleX, scaleY);

                float drawWidth = imageWidth * _zoom;
                float drawHeight = imageHeight * _zoom;
                _imageOffset = new PointF(
                    bounds.X + ((bounds.Width - drawWidth) / 2f),
                    bounds.Y + ((bounds.Height - drawHeight) / 2f));
            }
        }

        private void UpdateStatusLabel()
        {
            lock (_imageLock)
            {
                int imageWidth;
                int imageHeight;
                if (!TryGetImageSizeUnsafe(out imageWidth, out imageHeight))
                {
                    ResolutionText = string.Empty;
                    StatusText = "尚未載入圖片";
                    return;
                }

                ResolutionText = imageWidth + " x " + imageHeight;
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
                width = 0;
                height = 0;
                TryGetImageSizeUnsafe(out width, out height);
                zoom = _zoom;
                offset = _imageOffset;
                return width > 0 && height > 0;
            }
        }

        private bool TryGetImageSizeUnsafe(out int width, out int height)
        {
            if (_sourceBitmap != null)
            {
                width = _sourceBitmap.Width;
                height = _sourceBitmap.Height;
                return true;
            }

            if (_largeImageSource != null)
            {
                width = _largeImageSource.Width;
                height = _largeImageSource.Height;
                return true;
            }

            width = 0;
            height = 0;
            return false;
        }

        private void DrawLargeImage(Graphics graphics, LargeImageSource source, float zoom, PointF offset)
        {
            if (source == null)
            {
                return;
            }

            Rectangle visibleSourceRect = GetVisibleSourceRectangle(source.Width, source.Height, viewerPanel.ClientRectangle, zoom, offset);
            if (visibleSourceRect.Width <= 0 || visibleSourceRect.Height <= 0)
            {
                return;
            }

            using (var preview = source.GetBestPreview(zoom))
            {
                InterpolationMode previousInterpolation = graphics.InterpolationMode;
                graphics.InterpolationMode = _isPanning ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
                DrawPreviewRegion(graphics, preview.Bitmap, preview.Scale, visibleSourceRect, zoom, offset);
                graphics.InterpolationMode = previousInterpolation;
                if (!ShouldRenderTiles(zoom, preview.Scale) ||
                    (_isPanning && !ShouldDrawCachedTilesWhilePanning(zoom, visibleSourceRect)))
                {
                    return;
                }
            }

            int startTileX = (visibleSourceRect.Left / TileSourceSize) * TileSourceSize;
            int endTileX = ((visibleSourceRect.Right + TileSourceSize - 1) / TileSourceSize) * TileSourceSize;
            int startTileY = (visibleSourceRect.Top / TileSourceSize) * TileSourceSize;
            int endTileY = ((visibleSourceRect.Bottom + TileSourceSize - 1) / TileSourceSize) * TileSourceSize;

            for (int tileY = startTileY; tileY < endTileY; tileY += TileSourceSize)
            {
                for (int tileX = startTileX; tileX < endTileX; tileX += TileSourceSize)
                {
                    Rectangle tileRect = source.GetVisibleTileBounds(new Rectangle(tileX, tileY, TileSourceSize, TileSourceSize));
                    Bitmap tile;
                    if (source.TryGetTile(tileRect, out tile))
                    {
                        DrawTile(graphics, tile, tileRect, zoom, offset, _isPanning);
                    }
                    else if (!_isPanning)
                    {
                        RequestTile(source, tileRect);
                    }
                }
            }
        }

        private static Rectangle GetVisibleSourceRectangle(int imageWidth, int imageHeight, Rectangle viewBounds, float zoom, PointF offset)
        {
            if (zoom <= 0f || viewBounds.Width <= 0 || viewBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int left = Math.Max(0, Math.Min(imageWidth, (int)Math.Floor((viewBounds.Left - offset.X) / zoom)));
            int top = Math.Max(0, Math.Min(imageHeight, (int)Math.Floor((viewBounds.Top - offset.Y) / zoom)));
            int right = Math.Max(0, Math.Min(imageWidth, (int)Math.Ceiling((viewBounds.Right - offset.X) / zoom)));
            int bottom = Math.Max(0, Math.Min(imageHeight, (int)Math.Ceiling((viewBounds.Bottom - offset.Y) / zoom)));
            if (right <= left || bottom <= top)
            {
                return Rectangle.Empty;
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static void DrawPreviewRegion(Graphics graphics, Bitmap previewBitmap, float previewScale, Rectangle visibleSourceRect, float zoom, PointF offset)
        {
            if (previewBitmap == null || previewScale <= 0f)
            {
                return;
            }

            int sourceLeft = Math.Max(0, Math.Min(previewBitmap.Width - 1, (int)Math.Floor(visibleSourceRect.Left * previewScale)));
            int sourceTop = Math.Max(0, Math.Min(previewBitmap.Height - 1, (int)Math.Floor(visibleSourceRect.Top * previewScale)));
            int sourceRight = Math.Max(sourceLeft + 1, Math.Min(previewBitmap.Width, (int)Math.Ceiling(visibleSourceRect.Right * previewScale)));
            int sourceBottom = Math.Max(sourceTop + 1, Math.Min(previewBitmap.Height, (int)Math.Ceiling(visibleSourceRect.Bottom * previewScale)));
            var sourceRect = Rectangle.FromLTRB(sourceLeft, sourceTop, sourceRight, sourceBottom);
            var destinationRect = RectangleF.FromLTRB(
                offset.X + (visibleSourceRect.Left * zoom),
                offset.Y + (visibleSourceRect.Top * zoom),
                offset.X + (visibleSourceRect.Right * zoom),
                offset.Y + (visibleSourceRect.Bottom * zoom));

            graphics.DrawImage(previewBitmap, destinationRect, sourceRect, GraphicsUnit.Pixel);
        }

        private void RequestTile(LargeImageSource source, Rectangle tileRect)
        {
            source.QueueTile(tileRect, ScheduleTileRefresh);
            source.PrefetchNeighborhood(tileRect, ScheduleTileRefresh);
        }

        private static void DrawTile(Graphics graphics, Bitmap tile, Rectangle tileRect, float zoom, PointF offset, bool lightweight)
        {
            float left = offset.X + (tileRect.Left * zoom);
            float top = offset.Y + (tileRect.Top * zoom);
            float right = offset.X + (tileRect.Right * zoom);
            float bottom = offset.Y + (tileRect.Bottom * zoom);

            float drawLeft = (float)Math.Floor(left);
            float drawTop = (float)Math.Floor(top);
            float drawRight = (float)Math.Ceiling(right);
            float drawBottom = (float)Math.Ceiling(bottom);
            if (drawRight <= drawLeft)
            {
                drawRight = drawLeft + 1f;
            }

            if (drawBottom <= drawTop)
            {
                drawBottom = drawTop + 1f;
            }

            Rectangle drawRect = Rectangle.Round(RectangleF.FromLTRB(drawLeft, drawTop, drawRight, drawBottom));
            InterpolationMode previousInterpolation = graphics.InterpolationMode;
            PixelOffsetMode previousPixelOffset = graphics.PixelOffsetMode;
            graphics.InterpolationMode = lightweight ? InterpolationMode.Low : (zoom >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear);
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using (var attributes = new ImageAttributes())
            {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(tile, drawRect, 0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel, attributes);
            }

            graphics.PixelOffsetMode = previousPixelOffset;
            graphics.InterpolationMode = previousInterpolation;
        }

        private static bool ShouldRenderTiles(float zoom, float previewScale)
        {
            if (zoom < TileRenderZoomThreshold)
            {
                return false;
            }

            return zoom > (previewScale * TilePreviewHandoffRatio);
        }

        private static bool ShouldDrawCachedTilesWhilePanning(float zoom, Rectangle visibleSourceRect)
        {
            if (zoom < CachedTilePanZoomThreshold)
            {
                return false;
            }

            int tileColumns = ((visibleSourceRect.Width + TileSourceSize - 1) / TileSourceSize) + 1;
            int tileRows = ((visibleSourceRect.Height + TileSourceSize - 1) / TileSourceSize) + 1;
            return tileColumns * tileRows <= MaxCachedTilesWhilePanning;
        }

        private void ScheduleTileRefresh()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(
                    new Action(
                        delegate
                        {
                            if (IsDisposed)
                            {
                                return;
                            }

                            _tileRefreshPending = true;
                            if (!_tileRefreshTimer.Enabled)
                            {
                                _tileRefreshTimer.Start();
                            }
                        }));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void TileRefreshTimer_Tick(object sender, EventArgs e)
        {
            _tileRefreshTimer.Stop();
            if (!_tileRefreshPending || IsDisposed)
            {
                return;
            }

            _tileRefreshPending = false;
            viewerPanel.Invalidate();
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
                int imageWidth;
                int imageHeight;
                if (!TryGetImageSizeUnsafe(out imageWidth, out imageHeight) || _zoom <= 0f)
                {
                    return false;
                }

                float left = (viewRectangle.Left - _imageOffset.X) / _zoom;
                float top = (viewRectangle.Top - _imageOffset.Y) / _zoom;
                float right = (viewRectangle.Right - _imageOffset.X) / _zoom;
                float bottom = (viewRectangle.Bottom - _imageOffset.Y) / _zoom;

                int imageLeft = Math.Max(0, Math.Min(imageWidth, (int)Math.Floor(left)));
                int imageTop = Math.Max(0, Math.Min(imageHeight, (int)Math.Floor(top)));
                int imageRight = Math.Max(0, Math.Min(imageWidth, (int)Math.Ceiling(right)));
                int imageBottom = Math.Max(0, Math.Min(imageHeight, (int)Math.Ceiling(bottom)));

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

        private static Bitmap CreateDisplayBitmap(Image source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            try
            {
                var bitmap = new Bitmap(source);
                Bitmap displayBitmap = EnsureGrayscaleBitmap(bitmap);
                if (!ReferenceEquals(displayBitmap, bitmap))
                {
                    bitmap.Dispose();
                }

                return displayBitmap;
            }
            catch (ArgumentException)
            {
                return CreateScaledDisplayBitmap(source);
            }
            catch (OutOfMemoryException)
            {
                return CreateScaledDisplayBitmap(source);
            }
        }

        private static Bitmap CreateScaledDisplayBitmap(Image source)
        {
            long sourcePixels = (long)source.Width * source.Height;
            double scale = sourcePixels > MaxDisplayPixels
                ? Math.Sqrt(MaxDisplayPixels / (double)sourcePixels)
                : 1.0;
            int displayWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int displayHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            return CreateGrayscaleBitmap(source, displayWidth, displayHeight);
        }

        private static Bitmap EnsureGrayscaleBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException("bitmap");
            }

            if (IsGrayscaleBitmap(bitmap))
            {
                return bitmap;
            }

            Bitmap grayscaleBitmap = CreateGrayscaleBitmap(bitmap);
            return grayscaleBitmap;
        }

        private static Bitmap CreateGrayscaleBitmap(Image source)
        {
            return CreateGrayscaleBitmap(source, source.Width, source.Height);
        }

        private static Bitmap CreateGrayscaleBitmap(Image source, int width, int height)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var grayscaleBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            grayscaleBitmap.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(grayscaleBitmap))
            using (var attributes = new ImageAttributes())
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                var colorMatrix = new ColorMatrix(new[]
                {
                    new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
                    new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
                    new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { 0f, 0f, 0f, 0f, 1f }
                });

                attributes.SetColorMatrix(colorMatrix);
                graphics.DrawImage(
                    source,
                    new Rectangle(0, 0, width, height),
                    0,
                    0,
                    source.Width,
                    source.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            return grayscaleBitmap;
        }

        private static bool IsGrayscaleBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return false;
            }

            if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed && IsGrayscalePalette(bitmap.Palette))
            {
                return true;
            }

            PixelFormat pixelFormat = bitmap.PixelFormat;
            if (pixelFormat == PixelFormat.Format24bppRgb ||
                pixelFormat == PixelFormat.Format32bppRgb ||
                pixelFormat == PixelFormat.Format32bppArgb ||
                pixelFormat == PixelFormat.Format32bppPArgb)
            {
                return IsLockBitsGrayscale(bitmap, pixelFormat);
            }

            using (Bitmap readableBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb))
            {
                readableBitmap.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);
                using (Graphics graphics = Graphics.FromImage(readableBitmap))
                {
                    graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                }

                return IsLockBitsGrayscale(readableBitmap, PixelFormat.Format24bppRgb);
            }
        }

        private static bool IsGrayscalePalette(ColorPalette palette)
        {
            foreach (Color color in palette.Entries)
            {
                if (color.R != color.G || color.G != color.B)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLockBitsGrayscale(Bitmap bitmap, PixelFormat pixelFormat)
        {
            Rectangle bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = null;

            try
            {
                data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, pixelFormat);
                int bytesPerPixel = Image.GetPixelFormatSize(pixelFormat) / 8;
                int stride = data.Stride;
                int rowBytes = bitmap.Width * bytesPerPixel;

                byte[] pixels = new byte[Math.Abs(stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                for (int y = 0; y < bitmap.Height; y++)
                {
                    int rowStart = y * Math.Abs(stride);
                    for (int x = 0; x < rowBytes; x += bytesPerPixel)
                    {
                        byte blue = pixels[rowStart + x];
                        byte green = pixels[rowStart + x + 1];
                        byte red = pixels[rowStart + x + 2];
                        if (red != green || green != blue)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            finally
            {
                if (data != null)
                {
                    bitmap.UnlockBits(data);
                }
            }
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

            if (_largeImageSource != null)
            {
                _largeImageSource.Dispose();
                _largeImageSource = null;
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
