using System;
using System.Collections.Generic;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Services
{
    internal sealed class LargeImageMaskCache
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, bool[,]> managedMasks =
            new Dictionary<string, bool[,]>(StringComparer.Ordinal);
        private readonly Dictionary<string, Cv.Mat> nativeMasks =
            new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
        private readonly HashSet<string> buildingKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private int generation;

        internal bool TryBeginBuild(string key, out int buildGeneration)
        {
            buildGeneration = 0;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (syncRoot)
            {
                if (managedMasks.ContainsKey(key) || nativeMasks.ContainsKey(key) ||
                    buildingKeys.Contains(key))
                {
                    return false;
                }

                buildGeneration = generation;
                buildingKeys.Add(key);
                return true;
            }
        }

        internal bool IsBuildCurrent(string key, int buildGeneration)
        {
            lock (syncRoot)
            {
                return buildGeneration == generation && buildingKeys.Contains(key);
            }
        }

        internal bool TryPublishPartialMask(string key, int buildGeneration, bool[,] mask)
        {
            if (mask == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                if (buildGeneration != generation || !buildingKeys.Contains(key))
                {
                    return false;
                }

                managedMasks[key] = mask;
                return true;
            }
        }

        internal bool TryPublishMask(string key, int buildGeneration, bool[,] mask)
        {
            if (mask == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                if (buildGeneration != generation || !buildingKeys.Contains(key))
                {
                    return false;
                }

                managedMasks[key] = mask;
                DisposeNativeMaskUnsafe(key);
                buildingKeys.Remove(key);
                return true;
            }
        }

        internal bool TryPublishBinaryMask(string key, int buildGeneration, Cv.Mat mask)
        {
            if (mask == null || mask.Empty())
            {
                return false;
            }

            lock (syncRoot)
            {
                if (buildGeneration != generation || !buildingKeys.Contains(key))
                {
                    return false;
                }

                DisposeNativeMaskUnsafe(key);
                managedMasks.Remove(key);
                nativeMasks[key] = mask;
                buildingKeys.Remove(key);
                return true;
            }
        }

        internal bool TryGet(
            string key,
            int expectedRows,
            int expectedColumns,
            out bool[,] managedMask,
            out Cv.Mat nativeMaskView,
            out bool isBuilding)
        {
            managedMask = null;
            nativeMaskView = null;
            isBuilding = false;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (syncRoot)
            {
                bool[,] cachedManagedMask;
                if (managedMasks.TryGetValue(key, out cachedManagedMask))
                {
                    managedMask = cachedManagedMask;
                }

                Cv.Mat cachedNativeMask;
                if (nativeMasks.TryGetValue(key, out cachedNativeMask) &&
                    cachedNativeMask != null && !cachedNativeMask.Empty() &&
                    cachedNativeMask.Rows == expectedRows &&
                    cachedNativeMask.Cols == expectedColumns)
                {
                    nativeMaskView = new Cv.Mat(
                        cachedNativeMask,
                        new Cv.Rect(0, 0, cachedNativeMask.Cols, cachedNativeMask.Rows));
                }

                isBuilding = buildingKeys.Contains(key);
                return managedMask != null || nativeMaskView != null;
            }
        }

        internal bool TryGetBinaryMaskView(
            string key,
            int expectedRows,
            int expectedColumns,
            out Cv.Mat maskView)
        {
            maskView = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (syncRoot)
            {
                Cv.Mat cached;
                if (!nativeMasks.TryGetValue(key, out cached) ||
                    cached == null || cached.Empty() ||
                    cached.Rows != expectedRows || cached.Cols != expectedColumns)
                {
                    return false;
                }

                maskView = new Cv.Mat(cached, new Cv.Rect(0, 0, cached.Cols, cached.Rows));
                return true;
            }
        }

        internal bool Contains(string key)
        {
            lock (syncRoot)
            {
                Cv.Mat native;
                return managedMasks.ContainsKey(key) ||
                    (nativeMasks.TryGetValue(key, out native) && native != null && !native.Empty());
            }
        }

        internal bool ContainsBinaryMask(string key)
        {
            lock (syncRoot)
            {
                Cv.Mat native;
                return nativeMasks.TryGetValue(key, out native) && native != null && !native.Empty();
            }
        }

        internal bool IsBuilding(string key)
        {
            lock (syncRoot)
            {
                return buildingKeys.Contains(key);
            }
        }

        internal void FailBuild(string key, int buildGeneration)
        {
            lock (syncRoot)
            {
                if (buildGeneration == generation)
                {
                    buildingKeys.Remove(key);
                }
            }
        }

        internal bool IsBinaryMaskCurrent(string key, int expectedGeneration)
        {
            lock (syncRoot)
            {
                Cv.Mat mask;
                return expectedGeneration == generation &&
                    nativeMasks.TryGetValue(key, out mask) && mask != null && !mask.Empty();
            }
        }

        internal void Clear()
        {
            lock (syncRoot)
            {
                foreach (Cv.Mat mask in nativeMasks.Values)
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                }

                nativeMasks.Clear();
                managedMasks.Clear();
                buildingKeys.Clear();
                generation++;
            }
        }

        private void DisposeNativeMaskUnsafe(string key)
        {
            Cv.Mat previous;
            if (nativeMasks.TryGetValue(key, out previous))
            {
                if (previous != null)
                {
                    previous.Dispose();
                }

                nativeMasks.Remove(key);
            }
        }
    }
}
