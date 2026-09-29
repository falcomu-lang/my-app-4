using System;
using System.Collections.Generic;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal sealed class ProcessedBinaryMaskCache
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, Cv.Mat> entries =
            new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);

        internal Cv.Mat GetOrCreateClone(string key, Func<Cv.Mat> create)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            if (create == null)
            {
                throw new ArgumentNullException("create");
            }

            lock (syncRoot)
            {
                Cv.Mat cached;
                if (entries.TryGetValue(key, out cached) && IsUsable(cached))
                {
                    return cached.Clone();
                }
            }

            Cv.Mat created = create();
            if (created == null)
            {
                throw new InvalidOperationException("The mask factory returned no result.");
            }

            try
            {
                lock (syncRoot)
                {
                    Cv.Mat existing;
                    if (entries.TryGetValue(key, out existing) && IsUsable(existing))
                    {
                        return existing.Clone();
                    }

                    if (existing != null)
                    {
                        existing.Dispose();
                    }

                    entries[key] = created;
                    created = null;
                    return entries[key].Clone();
                }
            }
            finally
            {
                if (created != null)
                {
                    created.Dispose();
                }
            }
        }

        internal bool TryGetClone(string key, int expectedRows, int expectedColumns, out Cv.Mat clone)
        {
            clone = null;
            if (key == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                Cv.Mat cached;
                if (!entries.TryGetValue(key, out cached) ||
                    !IsUsable(cached) ||
                    cached.Rows != expectedRows ||
                    cached.Cols != expectedColumns)
                {
                    return false;
                }

                clone = cached.Clone();
                return true;
            }
        }

        internal void Clear()
        {
            lock (syncRoot)
            {
                foreach (Cv.Mat mask in entries.Values)
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                }

                entries.Clear();
            }
        }

        private static bool IsUsable(Cv.Mat mask)
        {
            return mask != null && !mask.Empty();
        }
    }
}
