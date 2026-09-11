using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SW = System.Windows;
using SWM = System.Windows.Media;
using SWMI = System.Windows.Media.Imaging;

namespace IntegratedImageProcessingApp.Controls
{
    public sealed class LargeImageSource : IDisposable
    {
        private const int TileSourceSize = 1024;
        private readonly object _sync = new object();
        private readonly string _filePath;
        private readonly FileStream _stream;
        private readonly SWMI.BitmapFrame _frame;
        private readonly Dictionary<string, Bitmap> _tileCache;
        private readonly LinkedList<string> _tileOrder;
        private readonly HashSet<string> _pendingTiles;
        private readonly List<PreviewLevel> _previewLevels;
        private readonly int _maxTileCacheCount;
        private int _referenceCount = 1;
        private bool _previewBuildQueued;
        private bool _disposed;

        public LargeImageSource(string filePath)
        {
            _filePath = filePath;
            _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = SWMI.BitmapDecoder.Create(_stream, SWMI.BitmapCreateOptions.PreservePixelFormat, SWMI.BitmapCacheOption.OnDemand);
            _frame = decoder.Frames[0];
            Width = _frame.PixelWidth;
            Height = _frame.PixelHeight;
            _maxTileCacheCount = checked(((Width + TileSourceSize - 1) / TileSourceSize) *
                ((Height + TileSourceSize - 1) / TileSourceSize) + 16);
            _tileCache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
            _tileOrder = new LinkedList<string>();
            _pendingTiles = new HashSet<string>(StringComparer.Ordinal);
            _previewLevels = new List<PreviewLevel>();
            InitializePreviewLevels();
            CreateFastPreview();
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public void PreloadAllTiles(CancellationToken cancellationToken)
        {
            for (int y = 0; y < Height; y += TileSourceSize)
            {
                for (int x = 0; x < Width; x += TileSourceSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Rectangle tileRect = new Rectangle(
                        x,
                        y,
                        Math.Min(TileSourceSize, Width - x),
                        Math.Min(TileSourceSize, Height - y));
                    string key = CreateTileKey(tileRect);
                    lock (_sync)
                    {
                        ThrowIfDisposed();
                        if (_tileCache.ContainsKey(key))
                        {
                            TouchKey(key);
                            continue;
                        }

                        AddTileToCacheUnsafe(key, CreateTileBitmap(tileRect));
                    }
                }
            }
        }

        public LargeImageSource AddReference()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _referenceCount++;
                return this;
            }
        }

        public void ReleaseReference()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _referenceCount--;
                if (_referenceCount > 0)
                {
                    return;
                }
            }

            Dispose();
        }

        public static Size ReadImageSize(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var decoder = SWMI.BitmapDecoder.Create(stream, SWMI.BitmapCreateOptions.PreservePixelFormat, SWMI.BitmapCacheOption.OnDemand);
                SWMI.BitmapFrame frame = decoder.Frames[0];
                return new Size(frame.PixelWidth, frame.PixelHeight);
            }
        }

        public Rectangle GetVisibleTileBounds(Rectangle sourceRect)
        {
            var normalized = NormalizeRect(sourceRect);
            var tileX = (normalized.X / TileSourceSize) * TileSourceSize;
            var tileY = (normalized.Y / TileSourceSize) * TileSourceSize;
            var width = Math.Min(TileSourceSize, Width - tileX);
            var height = Math.Min(TileSourceSize, Height - tileY);
            return new Rectangle(tileX, tileY, width, height);
        }

        public Bitmap CreateRegionBitmap(Rectangle sourceRect)
        {
            Rectangle normalized;
            lock (_sync)
            {
                ThrowIfDisposed();
                normalized = NormalizeRect(sourceRect);
            }

            return CreateTileBitmap(normalized);
        }

        // Build a region from the 1024px source tiles so adjacent processing
        // chunks reuse decoded pixels instead of decoding overlapping regions.
        public Bitmap CreateRegionBitmapFromTiles(Rectangle sourceRect)
        {
            Rectangle normalized;
            lock (_sync)
            {
                ThrowIfDisposed();
                normalized = NormalizeRect(sourceRect);
            }

            var result = new Bitmap(normalized.Width, normalized.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Black);
                lock (_sync)
                {
                    ThrowIfDisposed();
                    int firstTileX = (normalized.Left / TileSourceSize) * TileSourceSize;
                    int firstTileY = (normalized.Top / TileSourceSize) * TileSourceSize;
                    int lastTileX = ((normalized.Right - 1) / TileSourceSize) * TileSourceSize;
                    int lastTileY = ((normalized.Bottom - 1) / TileSourceSize) * TileSourceSize;

                    for (int tileY = firstTileY; tileY <= lastTileY; tileY += TileSourceSize)
                    {
                        for (int tileX = firstTileX; tileX <= lastTileX; tileX += TileSourceSize)
                        {
                            Rectangle tileRect = new Rectangle(
                                tileX,
                                tileY,
                                Math.Min(TileSourceSize, Width - tileX),
                                Math.Min(TileSourceSize, Height - tileY));
                            string key = CreateTileKey(tileRect);
                            Bitmap tile;
                            if (!_tileCache.TryGetValue(key, out tile))
                            {
                                tile = CreateTileBitmap(tileRect);
                                AddTileToCacheUnsafe(key, tile);
                                tile = _tileCache[key];
                            }
                            else
                            {
                                TouchKey(key);
                            }

                            Rectangle copyRect = Rectangle.Intersect(normalized, tileRect);
                            if (copyRect.Width <= 0 || copyRect.Height <= 0)
                            {
                                continue;
                            }

                            Rectangle sourcePart = new Rectangle(
                                copyRect.X - tileRect.X,
                                copyRect.Y - tileRect.Y,
                                copyRect.Width,
                                copyRect.Height);
                            Rectangle destination = new Rectangle(
                                copyRect.X - normalized.X,
                                copyRect.Y - normalized.Y,
                                copyRect.Width,
                                copyRect.Height);
                            graphics.DrawImage(tile, destination, sourcePart.X, sourcePart.Y,
                                sourcePart.Width, sourcePart.Height, GraphicsUnit.Pixel);
                        }
                    }
                }
            }

            return result;
        }

        public byte[,] CreateGrayRegionFromTiles(Rectangle sourceRect)
        {
            Rectangle normalized;
            lock (_sync)
            {
                ThrowIfDisposed();
                normalized = NormalizeRect(sourceRect);
            }

            var result = new byte[normalized.Width, normalized.Height];
            lock (_sync)
            {
                ThrowIfDisposed();
                int firstTileX = (normalized.Left / TileSourceSize) * TileSourceSize;
                int firstTileY = (normalized.Top / TileSourceSize) * TileSourceSize;
                int lastTileX = ((normalized.Right - 1) / TileSourceSize) * TileSourceSize;
                int lastTileY = ((normalized.Bottom - 1) / TileSourceSize) * TileSourceSize;

                for (int tileY = firstTileY; tileY <= lastTileY; tileY += TileSourceSize)
                {
                    for (int tileX = firstTileX; tileX <= lastTileX; tileX += TileSourceSize)
                    {
                        Rectangle tileRect = new Rectangle(
                            tileX,
                            tileY,
                            Math.Min(TileSourceSize, Width - tileX),
                            Math.Min(TileSourceSize, Height - tileY));
                        string key = CreateTileKey(tileRect);
                        Bitmap tile;
                        if (!_tileCache.TryGetValue(key, out tile))
                        {
                            tile = CreateTileBitmap(tileRect);
                            AddTileToCacheUnsafe(key, tile);
                            tile = _tileCache[key];
                        }
                        else
                        {
                            TouchKey(key);
                        }

                        Rectangle copyRect = Rectangle.Intersect(normalized, tileRect);
                        BitmapData data = tile.LockBits(
                            new Rectangle(0, 0, tile.Width, tile.Height),
                            ImageLockMode.ReadOnly,
                            tile.PixelFormat);
                        try
                        {
                            int bytesPerPixel = Math.Max(1, Image.GetPixelFormatSize(tile.PixelFormat) / 8);
                            int sourceStride = Math.Abs(data.Stride);
                            byte[] sourceRow = new byte[sourceStride];
                            for (int y = copyRect.Top; y < copyRect.Bottom; y++)
                            {
                                int sourceY = y - tileRect.Top;
                                int targetY = y - normalized.Top;
                                Marshal.Copy(data.Scan0 + (sourceY * data.Stride), sourceRow, 0, sourceStride);
                                for (int x = copyRect.Left; x < copyRect.Right; x++)
                                {
                                    int sourceX = x - tileRect.Left;
                                    int offset = sourceX * bytesPerPixel;
                                    byte b = sourceRow[offset];
                                    byte g = bytesPerPixel > 1 ? sourceRow[offset + 1] : b;
                                    byte r = bytesPerPixel > 2 ? sourceRow[offset + 2] : b;
                                    result[x - normalized.Left, targetY] = (byte)((r + g + b) / 3);
                                }
                            }
                        }
                        finally
                        {
                            tile.UnlockBits(data);
                        }
                    }
                }
            }

            return result;
        }

        public bool TryCreateRegionBitmapFromCachedTile(Rectangle sourceRect, out Bitmap region)
        {
            region = null;
            lock (_sync)
            {
                ThrowIfDisposed();
                Rectangle normalized = NormalizeRect(sourceRect);
                Rectangle tileRect = GetVisibleTileBounds(normalized);
                string key = CreateTileKey(tileRect);
                Bitmap cachedTile;
                if (!_tileCache.TryGetValue(key, out cachedTile))
                {
                    return false;
                }

                Rectangle localRect = new Rectangle(
                    normalized.X - tileRect.X,
                    normalized.Y - tileRect.Y,
                    normalized.Width,
                    normalized.Height);
                region = cachedTile.Clone(localRect, PixelFormat.Format32bppArgb);
                TouchKey(key);
                return true;
            }
        }

        public PreviewBitmap GetBestPreview(float zoom)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                PreviewLevel selected = null;
                for (var i = 0; i < _previewLevels.Count; i++)
                {
                    if (_previewLevels[i].Bitmap != null && _previewLevels[i].Scale >= zoom)
                    {
                        selected = _previewLevels[i];
                        break;
                    }
                }

                if (selected == null)
                {
                    for (var i = _previewLevels.Count - 1; i >= 0; i--)
                    {
                        if (_previewLevels[i].Bitmap != null)
                        {
                            selected = _previewLevels[i];
                            break;
                        }
                    }
                }

                if (selected == null)
                {
                    selected = _previewLevels[_previewLevels.Count - 1];
                    selected.Bitmap = CreateScaledBitmap(selected.DecodeWidth, selected.DecodeHeight);
                }

                return new PreviewBitmap(selected.Bitmap, selected.Scale, false);
            }
        }

        public void QueuePreviewBuilds(Action onReady)
        {
            lock (_sync)
            {
                if (_disposed || _previewBuildQueued)
                {
                    return;
                }

                _previewBuildQueued = true;
            }

            Task.Run(
                () =>
                {
                    try
                    {
                        for (var i = _previewLevels.Count - 1; i >= 0; i--)
                        {
                            PreviewLevel level;
                            lock (_sync)
                            {
                                if (_disposed)
                                {
                                    return;
                                }

                                level = _previewLevels[i];
                                if (level.Bitmap != null)
                                {
                                    continue;
                                }
                            }

                            Bitmap bitmap = null;
                            try
                            {
                                bitmap = CreateScaledBitmap(level.DecodeWidth, level.DecodeHeight);
                                lock (_sync)
                                {
                                    if (_disposed)
                                    {
                                        return;
                                    }

                                    if (level.Bitmap == null)
                                    {
                                        level.Bitmap = bitmap;
                                        bitmap = null;
                                    }
                                }

                                if (onReady != null)
                                {
                                    onReady();
                                }
                            }
                            finally
                            {
                                if (bitmap != null)
                                {
                                    bitmap.Dispose();
                                }
                            }
                        }
                    }
                    finally
                    {
                        lock (_sync)
                        {
                            _previewBuildQueued = false;
                        }
                    }
                });
        }

        public bool TryGetTile(Rectangle sourceRect, out Bitmap tile)
        {
            var normalized = NormalizeRect(sourceRect);
            var key = CreateTileKey(normalized);

            lock (_sync)
            {
                ThrowIfDisposed();
                Bitmap cached;
                if (_tileCache.TryGetValue(key, out cached))
                {
                    TouchKey(key);
                    tile = cached;
                    return true;
                }
            }

            tile = null;
            return false;
        }

        public void QueueTile(Rectangle sourceRect, Action onReady)
        {
            var normalized = NormalizeRect(sourceRect);
            var key = CreateTileKey(normalized);

            lock (_sync)
            {
                if (_disposed || _tileCache.ContainsKey(key) || _pendingTiles.Contains(key))
                {
                    return;
                }

                _pendingTiles.Add(key);
            }

            Task.Run(
                () =>
                {
                    Bitmap tile = null;
                    try
                    {
                        tile = CreateTileBitmap(normalized);
                        lock (_sync)
                        {
                            if (_disposed)
                            {
                                return;
                            }

                            AddTileToCacheUnsafe(key, tile);
                            tile = null;
                        }

                        if (onReady != null)
                        {
                            onReady();
                        }
                    }
                    finally
                    {
                        if (tile != null)
                        {
                            tile.Dispose();
                        }

                        lock (_sync)
                        {
                            _pendingTiles.Remove(key);
                        }
                    }
                });
        }

        public void PrefetchNeighborhood(Rectangle centerRect, Action onReady)
        {
            var center = GetVisibleTileBounds(centerRect);
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var neighbor = new Rectangle(
                        center.X + (offsetX * TileSourceSize),
                        center.Y + (offsetY * TileSourceSize),
                        TileSourceSize,
                        TileSourceSize);

                    if (neighbor.Right <= 0 || neighbor.Bottom <= 0 || neighbor.X >= Width || neighbor.Y >= Height)
                    {
                        continue;
                    }

                    QueueTile(neighbor, onReady);
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                foreach (var level in _previewLevels)
                {
                    if (level.Bitmap != null)
                    {
                        level.Bitmap.Dispose();
                        level.Bitmap = null;
                    }
                }

                foreach (var pair in _tileCache)
                {
                    pair.Value.Dispose();
                }

                _tileCache.Clear();
                _tileOrder.Clear();
                _pendingTiles.Clear();
                _stream.Dispose();
            }
        }

        public Bitmap CreateSnapshotBitmap()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return GetBestPreview(0f).Bitmap;
            }
        }

        public int GetGrayAt(int x, int y)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                {
                    return 0;
                }

                var tileX = (x / TileSourceSize) * TileSourceSize;
                var tileY = (y / TileSourceSize) * TileSourceSize;
                var tileRect = new Rectangle(tileX, tileY, Math.Min(TileSourceSize, Width - tileX), Math.Min(TileSourceSize, Height - tileY));
                var key = CreateTileKey(tileRect);
                Bitmap tile;
                if (!_tileCache.TryGetValue(key, out tile))
                {
                    tile = CreateTileBitmap(tileRect);
                    AddTileToCacheUnsafe(key, tile);
                }
                else
                {
                    TouchKey(key);
                }

                var localX = x - tileRect.X;
                var localY = y - tileRect.Y;
                return ReadGrayFromBitmap(tile, localX, localY);
            }
        }

        public void GetGrayValues(Point[] points, int[] destination)
        {
            if (points == null || destination == null)
            {
                return;
            }

            lock (_sync)
            {
                ThrowIfDisposed();
                var count = Math.Min(points.Length, destination.Length);
                var groups = new Dictionary<string, TileSampleGroup>(StringComparer.Ordinal);
                for (var i = 0; i < count; i++)
                {
                    var point = points[i];
                    if (point.X < 0 || point.Y < 0 || point.X >= Width || point.Y >= Height)
                    {
                        destination[i] = 0;
                        continue;
                    }

                    var tileX = (point.X / TileSourceSize) * TileSourceSize;
                    var tileY = (point.Y / TileSourceSize) * TileSourceSize;
                    var tileRect = new Rectangle(tileX, tileY, Math.Min(TileSourceSize, Width - tileX), Math.Min(TileSourceSize, Height - tileY));
                    var key = CreateTileKey(tileRect);
                    TileSampleGroup group;
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new TileSampleGroup(tileRect);
                        groups.Add(key, group);
                    }

                    group.SampleIndexes.Add(i);
                }

                foreach (var pair in groups)
                {
                    Bitmap tile;
                    if (!_tileCache.TryGetValue(pair.Key, out tile))
                    {
                        tile = CreateTileBitmap(pair.Value.TileRect);
                        AddTileToCacheUnsafe(pair.Key, tile);
                    }
                    else
                    {
                        TouchKey(pair.Key);
                    }

                    ReadGrayValuesFromBitmap(tile, pair.Value.TileRect, points, pair.Value.SampleIndexes, destination);
                }
            }
        }

        private void InitializePreviewLevels()
        {
            AddPreviewLevel(512);
            AddPreviewLevel(1024);
            AddPreviewLevel(2048);
            AddPreviewLevel(4096);
            _previewLevels.Sort((a, b) => b.Scale.CompareTo(a.Scale));
        }

        private void CreateFastPreview()
        {
            if (_previewLevels.Count == 0)
            {
                return;
            }

            var fastestLevel = _previewLevels[_previewLevels.Count - 1];
            fastestLevel.Bitmap = CreateScaledBitmap(fastestLevel.DecodeWidth, fastestLevel.DecodeHeight);
        }

        private void AddPreviewLevel(int maxDimension)
        {
            var scale = Math.Min((double)maxDimension / Width, (double)maxDimension / Height);
            scale = Math.Min(scale, 1d);
            if (scale <= 0d)
            {
                scale = 1d;
            }

            var decodeWidth = Math.Max(1, (int)Math.Round(Width * scale));
            var decodeHeight = Math.Max(1, (int)Math.Round(Height * scale));
            _previewLevels.Add(new PreviewLevel((float)scale, decodeWidth, decodeHeight));
        }

        private Rectangle NormalizeRect(Rectangle sourceRect)
        {
            var x = Math.Max(0, Math.Min(sourceRect.X, Width - 1));
            var y = Math.Max(0, Math.Min(sourceRect.Y, Height - 1));
            var width = Math.Max(1, Math.Min(sourceRect.Width, Width - x));
            var height = Math.Max(1, Math.Min(sourceRect.Height, Height - y));
            return new Rectangle(x, y, width, height);
        }

        private void TouchKey(string key)
        {
            var node = _tileOrder.Find(key);
            if (node == null)
            {
                return;
            }

            _tileOrder.Remove(node);
            _tileOrder.AddFirst(node);
        }

        private void TrimCache()
        {
            while (_tileOrder.Count > _maxTileCacheCount)
            {
                var last = _tileOrder.Last;
                if (last == null)
                {
                    break;
                }

                Bitmap bitmap;
                if (_tileCache.TryGetValue(last.Value, out bitmap))
                {
                    bitmap.Dispose();
                    _tileCache.Remove(last.Value);
                }

                _tileOrder.RemoveLast();
            }
        }

        private void AddTileToCacheUnsafe(string key, Bitmap tile)
        {
            Bitmap existing;
            if (_tileCache.TryGetValue(key, out existing))
            {
                TouchKey(key);
                if (!ReferenceEquals(existing, tile))
                {
                    tile.Dispose();
                }

                return;
            }

            _tileCache[key] = tile;
            _tileOrder.AddFirst(key);
            TrimCache();
        }

        private Bitmap CreateTileBitmap(Rectangle sourceRect)
        {
            var cropped = new SWMI.CroppedBitmap(
                _frame,
                new SW.Int32Rect(sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height));
            return ConvertToBitmap(cropped);
        }

        private Bitmap CreateScaledBitmap(int decodeWidth, int decodeHeight)
        {
            decodeWidth = Math.Max(1, Math.Min(65535, decodeWidth));
            decodeHeight = Math.Max(1, Math.Min(65535, decodeHeight));
            double scale = Math.Min((double)decodeWidth / Width, (double)decodeHeight / Height);
            scale = Math.Max(double.Epsilon, Math.Min(1d, scale));
            // Build previews from the cached source tiles for every image size.
            // This keeps preview creation away from the GDI+ conversion path,
            // which is not reliable for very large WIC frames.
            return CreateScaledBitmapFromTiles(scale, decodeWidth, decodeHeight);
        }

        private Bitmap CreateScaledBitmapFromTiles(double scale, int decodeWidth, int decodeHeight)
        {
            int outputWidth = Math.Max(1, Math.Min(4096, (int)Math.Round(Width * scale)));
            int outputHeight = Math.Max(1, Math.Min(4096, (int)Math.Round(Height * scale)));
            var result = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Black);
                lock (_sync)
                {
                    for (int y = 0; y < Height; y += TileSourceSize)
                    {
                        for (int x = 0; x < Width; x += TileSourceSize)
                        {
                            Rectangle tileRect = new Rectangle(
                                x,
                                y,
                                Math.Min(TileSourceSize, Width - x),
                                Math.Min(TileSourceSize, Height - y));
                            string key = CreateTileKey(tileRect);
                            Bitmap tile;
                            if (!_tileCache.TryGetValue(key, out tile))
                            {
                                tile = CreateTileBitmap(tileRect);
                                AddTileToCacheUnsafe(key, tile);
                                tile = _tileCache[key];
                            }
                            else
                            {
                                TouchKey(key);
                            }

                            Rectangle destination = new Rectangle(
                                Math.Max(0, (int)Math.Round(tileRect.X * scale)),
                                Math.Max(0, (int)Math.Round(tileRect.Y * scale)),
                                Math.Max(1, (int)Math.Round(tileRect.Width * scale)),
                                Math.Max(1, (int)Math.Round(tileRect.Height * scale)));
                            graphics.DrawImage(tile, destination);
                        }
                    }
                }
            }

            return result;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LargeImageSource");
            }
        }

        private static string CreateTileKey(Rectangle rect)
        {
            return rect.X + ":" + rect.Y + ":" + rect.Width + ":" + rect.Height;
        }

        private static Bitmap ConvertToBitmap(SWMI.BitmapSource source)
        {
            var formatted = new SWMI.FormatConvertedBitmap(source, SWM.PixelFormats.Bgr32, null, 0);
            if (formatted.PixelWidth <= 0 || formatted.PixelHeight <= 0 ||
                formatted.PixelWidth > 65535 || formatted.PixelHeight > 65535)
            {
                throw new ArgumentException("WIC 預覽影像尺寸無效。", "source");
            }

            var bitmap = new Bitmap(formatted.PixelWidth, formatted.PixelHeight, PixelFormat.Format24bppRgb);
            var sourceStride = formatted.PixelWidth * 4;
            var sourcePixels = new byte[sourceStride * formatted.PixelHeight];
            formatted.CopyPixels(
                new SW.Int32Rect(0, 0, formatted.PixelWidth, formatted.PixelHeight),
                sourcePixels,
                sourceStride,
                sourceStride);
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int destinationStride = Math.Abs(data.Stride);
                byte[] destinationRow = new byte[destinationStride];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int sourceOffset = y * sourceStride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int sourceOffsetX = sourceOffset + (x * 4);
                        int destinationOffset = x * 3;
                        destinationRow[destinationOffset] = sourcePixels[sourceOffsetX];
                        destinationRow[destinationOffset + 1] = sourcePixels[sourceOffsetX + 1];
                        destinationRow[destinationOffset + 2] = sourcePixels[sourceOffsetX + 2];
                    }

                    Marshal.Copy(destinationRow, 0, data.Scan0 + (y * data.Stride), destinationStride);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private static int ReadGrayFromBitmap(Bitmap bitmap, int x, int y)
        {
            if (bitmap == null)
            {
                return 0;
            }

            x = Math.Max(0, Math.Min(bitmap.Width - 1, x));
            y = Math.Max(0, Math.Min(bitmap.Height - 1, y));

            if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                try
                {
                    var value = Marshal.ReadByte(data.Scan0, y * data.Stride + x);
                    return value;
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }

            var pixel = bitmap.GetPixel(x, y);
            return (pixel.R + pixel.G + pixel.B) / 3;
        }

        private static void ReadGrayValuesFromBitmap(Bitmap bitmap, Rectangle tileRect, Point[] points, List<int> sampleIndexes, int[] destination)
        {
            if (bitmap == null || sampleIndexes == null)
            {
                return;
            }

            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var bitsPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat);
                var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
                foreach (var sampleIndex in sampleIndexes)
                {
                    var point = points[sampleIndex];
                    var localX = Math.Max(0, Math.Min(bitmap.Width - 1, point.X - tileRect.X));
                    var localY = Math.Max(0, Math.Min(bitmap.Height - 1, point.Y - tileRect.Y));
                    var row = data.Scan0 + (localY * data.Stride);
                    var offset = localX * bytesPerPixel;
                    if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
                    {
                        destination[sampleIndex] = Marshal.ReadByte(row, offset);
                        continue;
                    }

                    if (bytesPerPixel >= 3)
                    {
                        var b = Marshal.ReadByte(row, offset);
                        var g = Marshal.ReadByte(row, offset + 1);
                        var r = Marshal.ReadByte(row, offset + 2);
                        destination[sampleIndex] = (r + g + b) / 3;
                        continue;
                    }

                    destination[sampleIndex] = Marshal.ReadByte(row, offset);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private sealed class TileSampleGroup
        {
            public TileSampleGroup(Rectangle tileRect)
            {
                TileRect = tileRect;
                SampleIndexes = new List<int>();
            }

            public Rectangle TileRect { get; private set; }

            public List<int> SampleIndexes { get; private set; }
        }

        public sealed class PreviewBitmap : IDisposable
        {
            private readonly bool _ownsBitmap;

            public PreviewBitmap(Bitmap bitmap, float scale, bool ownsBitmap)
            {
                Bitmap = bitmap;
                Scale = scale;
                _ownsBitmap = ownsBitmap;
            }

            public Bitmap Bitmap { get; private set; }

            public float Scale { get; private set; }

            public void Dispose()
            {
                if (_ownsBitmap && Bitmap != null)
                {
                    Bitmap.Dispose();
                }

                Bitmap = null;
            }
        }

        private sealed class PreviewLevel
        {
            public PreviewLevel(float scale, int decodeWidth, int decodeHeight)
            {
                Scale = scale;
                DecodeWidth = decodeWidth;
                DecodeHeight = decodeHeight;
            }

            public float Scale { get; private set; }

            public int DecodeWidth { get; private set; }

            public int DecodeHeight { get; private set; }

            public Bitmap Bitmap { get; set; }
        }
    }
}
