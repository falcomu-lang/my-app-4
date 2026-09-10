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
            e.Graphics.Clear(Color.Black);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;

            Bitmap bitmap;
            float zoom;
            PointF offset;
            lock (_imageLock)
            {
                bitmap = _sourceBitmap;
                zoom = _zoom;
                offset = _imageOffset;
            }

            if (bitmap == null || zoom <= 0f)
            {
                return;
            }

            e.Graphics.DrawImage(bitmap, offset.X, offset.Y, bitmap.Width * zoom, bitmap.Height * zoom);
        }

        private void ImageDisplayControl_SizeChanged(object sender, EventArgs e)
        {
            FitImageToView();
            viewerPanel.Invalidate();
        }

        private void buttonFitToWindow_Click(object sender, EventArgs e)
        {
            FitImageToView();
            UpdateStatusLabel();
            viewerPanel.Invalidate();
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
                StatusText = string.Format("Zoom {0:0.00}x", _zoom);
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
}
