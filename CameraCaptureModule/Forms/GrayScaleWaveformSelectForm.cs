using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CameraCaptureApp.Forms
{
    public partial class GrayScaleWaveformSelectForm : Form
    {
        private readonly Bitmap _image;
        private readonly Bitmap _displayImage;
        private Point? _dragStart;
        private Point? _dragEnd;

        public GrayScaleWaveformSelectForm(Bitmap image)
        {
            _image = image != null ? (Bitmap)image.Clone() : null;
            InitializeComponent();
            if (_image == null)
            {
                labelHint.Text = "No image is available.";
                buttonOk.Enabled = false;
            }
            else
            {
                _displayImage = (Bitmap)_image.Clone();
                UpdateSelectionDefaults();
            }
        }

        public Point[] SelectedLine { get; private set; }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_image != null)
            {
                _image.Dispose();
            }

            if (_displayImage != null)
            {
                _displayImage.Dispose();
            }

            base.OnFormClosed(e);
        }

        private void UpdateSelectionDefaults()
        {
            if (_image == null)
            {
                return;
            }

            _dragStart = new Point(0, 0);
            _dragEnd = new Point(Math.Max(0, _image.Width - 1), Math.Max(0, _image.Height - 1));
            panelImage.Invalidate();
        }

        private void panelImage_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
            if (_image == null)
            {
                return;
            }

            var rect = GetImageBounds(panelImage.ClientRectangle, _image.Size);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            e.Graphics.DrawImage(_image, rect);

            if (_dragStart.HasValue && _dragEnd.HasValue)
            {
                var p1 = ImageToClient(_dragStart.Value, rect, _image.Size);
                var p2 = ImageToClient(_dragEnd.Value, rect, _image.Size);
                using (var pen = new Pen(Color.Yellow, 2f))
                {
                    e.Graphics.DrawLine(pen, p1, p2);
                }
            }
        }

        private void panelImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (_image == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            var rect = GetImageBounds(panelImage.ClientRectangle, _image.Size);
            if (!rect.Contains(e.Location))
            {
                return;
            }

            _dragStart = ClientToImage(e.Location, rect, _image.Size);
            _dragEnd = _dragStart;
            panelImage.Invalidate();
        }

        private void panelImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_image == null || !_dragStart.HasValue || e.Button != MouseButtons.Left)
            {
                return;
            }

            var rect = GetImageBounds(panelImage.ClientRectangle, _image.Size);
            _dragEnd = ClampToImage(ClientToImage(e.Location, rect, _image.Size), _image.Size);
            panelImage.Invalidate();
        }

        private void panelImage_MouseUp(object sender, MouseEventArgs e)
        {
            if (_image == null || !_dragStart.HasValue)
            {
                return;
            }

            var rect = GetImageBounds(panelImage.ClientRectangle, _image.Size);
            _dragEnd = ClampToImage(ClientToImage(e.Location, rect, _image.Size), _image.Size);
            panelImage.Invalidate();
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            UpdateSelectionDefaults();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (_image == null || !_dragStart.HasValue || !_dragEnd.HasValue)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            SelectedLine = BuildLinePoints(_dragStart.Value, _dragEnd.Value);
            if (SelectedLine == null || SelectedLine.Length < 2)
            {
                MessageBox.Show(this, "Please drag a longer line.", "Gray Waveform", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static Point[] BuildLinePoints(Point start, Point end)
        {
            var points = new System.Collections.Generic.List<Point>();
            var x0 = start.X;
            var y0 = start.Y;
            var x1 = end.X;
            var y1 = end.Y;
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var err = dx + dy;

            while (true)
            {
                points.Add(new Point(x0, y0));
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return points.ToArray();
        }

        private static Rectangle GetImageBounds(Rectangle clientBounds, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            var scale = Math.Min(clientBounds.Width / (float)imageSize.Width, clientBounds.Height / (float)imageSize.Height);
            var width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
            var height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
            return new Rectangle(
                clientBounds.Left + ((clientBounds.Width - width) / 2),
                clientBounds.Top + ((clientBounds.Height - height) / 2),
                width,
                height);
        }

        private static Point ClientToImage(Point point, Rectangle imageBounds, Size imageSize)
        {
            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                return Point.Empty;
            }

            var x = (point.X - imageBounds.Left) * imageSize.Width / (float)imageBounds.Width;
            var y = (point.Y - imageBounds.Top) * imageSize.Height / (float)imageBounds.Height;
            return ClampToImage(new Point((int)Math.Round(x), (int)Math.Round(y)), imageSize);
        }

        private static Point ImageToClient(Point point, Rectangle imageBounds, Size imageSize)
        {
            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                return Point.Empty;
            }

            var x = imageBounds.Left + (point.X * imageBounds.Width / (float)imageSize.Width);
            var y = imageBounds.Top + (point.Y * imageBounds.Height / (float)imageSize.Height);
            return new Point((int)Math.Round(x), (int)Math.Round(y));
        }

        private static Point ClampToImage(Point point, Size imageSize)
        {
            return new Point(
                Math.Max(0, Math.Min(imageSize.Width - 1, point.X)),
                Math.Max(0, Math.Min(imageSize.Height - 1, point.Y)));
        }
    }
}
