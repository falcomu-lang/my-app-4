using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private static List<ObjectDefinitionDetectedObject> CreateObjectDefinitionDetectedObjects(
            Cv.Mat sourceMask,
            Rectangle roi,
            ObjectDefinitionSettings definition,
            out Cv.Mat retainedMask,
            out long cclElapsedMilliseconds,
            out long retainedMaskElapsedMilliseconds,
            out long mergeElapsedMilliseconds,
            out long contourElapsedMilliseconds,
            out long rotationGeometryElapsedMilliseconds)
        {
            cclElapsedMilliseconds = 0;
            retainedMaskElapsedMilliseconds = 0;
            mergeElapsedMilliseconds = 0;
            contourElapsedMilliseconds = 0;
            rotationGeometryElapsedMilliseconds = 0;
            if (sourceMask == null || sourceMask.Empty())
            {
                retainedMask = new Cv.Mat();
                return new List<ObjectDefinitionDetectedObject>();
            }

            retainedMask = null;
            using (var labels = new Cv.Mat())
            using (var stats = new Cv.Mat())
            using (var centroids = new Cv.Mat())
            {
                Cv.PixelConnectivity connectivity = definition.Connectivity == 4
                    ? Cv.PixelConnectivity.Connectivity4
                    : Cv.PixelConnectivity.Connectivity8;
                Stopwatch cclStopwatch = Stopwatch.StartNew();
                int labelCount = Cv.Cv2.ConnectedComponentsWithStats(
                    sourceMask,
                    labels,
                    stats,
                    centroids,
                    connectivity,
                    Cv.MatType.CV_32SC1);
                var result = new List<ObjectDefinitionDetectedObject>();
                var accepted = new bool[labelCount];
                var acceptedComponents = new List<ObjectDefinitionComponentRegion>();
                var rejectedComponents = new List<ObjectDefinitionComponentRegion>();
                long acceptedBoundsArea = 0;
                for (int label = 1; label < labelCount; label++)
                {
                    int area = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area);
                    int x = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Left);
                    int y = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Top);
                    int width = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Width);
                    int height = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Height);
                    var component = new ObjectDefinitionComponentRegion
                    {
                        Label = label,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    };
                    if (definition.MinArea > 0 && area < definition.MinArea)
                    {
                        rejectedComponents.Add(component);
                        continue;
                    }

                    if (definition.MaxArea > 0 && area > definition.MaxArea)
                    {
                        rejectedComponents.Add(component);
                        continue;
                    }

                    accepted[label] = true;
                    acceptedComponents.Add(component);
                    acceptedBoundsArea += (long)width * height;
                    result.Add(new ObjectDefinitionDetectedObject
                    {
                        Bounds = new Rectangle(roi.X + x, roi.Y + y, width, height),
                        Area = area,
                        SourceLabels = new List<int> { label }
                    });
                }
                cclStopwatch.Stop();
                cclElapsedMilliseconds = cclStopwatch.ElapsedMilliseconds;

                if (string.Equals(definition.MergeMethod, "Distance", StringComparison.Ordinal) &&
                    definition.MaxMergeDistance > 0)
                {
                    Stopwatch mergeStopwatch = Stopwatch.StartNew();
                    result = MergeObjectDefinitionDetectedObjects(
                        result,
                        definition.MaxMergeDistance);
                    mergeStopwatch.Stop();
                    mergeElapsedMilliseconds = mergeStopwatch.ElapsedMilliseconds;
                }

                result = FilterObjectDefinitionGroupsByArea(
                    result,
                    definition.GroupMinArea,
                    definition.GroupMaxArea);
                var retainedLabels = new HashSet<int>(
                    result
                        .SelectMany(item => item.SourceLabels ?? Enumerable.Empty<int>()));
                for (int label = 1; label < accepted.Length; label++)
                {
                    accepted[label] = retainedLabels.Contains(label);
                }

                if (definition.EnableRotationAnalysis && result.Count > 0)
                {
                    Stopwatch contourStopwatch = Stopwatch.StartNew();
                    Dictionary<int, Cv.Point[]> labelContours =
                        CreateObjectDefinitionLabelContours(
                            labels,
                            acceptedComponents,
                            retainedLabels);
                    contourStopwatch.Stop();
                    contourElapsedMilliseconds = contourStopwatch.ElapsedMilliseconds;

                    Stopwatch rotationStopwatch = Stopwatch.StartNew();
                    CalculateObjectDefinitionRotationGeometry(result, labelContours, roi);
                    rotationStopwatch.Stop();
                    rotationGeometryElapsedMilliseconds = rotationStopwatch.ElapsedMilliseconds;
                }

                Stopwatch retainedMaskStopwatch = Stopwatch.StartNew();
                long imageArea = (long)sourceMask.Width * sourceMask.Height;
                if (rejectedComponents.Count == 0 && retainedLabels.Count == acceptedComponents.Count)
                {
                    retainedMask = sourceMask.Clone();
                }
                else
                {
                    rejectedComponents = acceptedComponents
                        .Where(component => !retainedLabels.Contains(component.Label))
                        .Concat(rejectedComponents)
                        .ToList();
                    long finalRejectedBoundsArea = rejectedComponents.Sum(
                        component => (long)component.Width * component.Height);
                    if (finalRejectedBoundsArea * 2 < acceptedBoundsArea && result.Count > 0)
                    {
                        retainedMask = CreateObjectDefinitionRetainedMaskByCloneAndRemove(
                            sourceMask,
                            labels,
                            rejectedComponents);
                    }
                    else
                    {
                        retainedMask = new Cv.Mat(
                            sourceMask.Rows,
                            sourceMask.Cols,
                            Cv.MatType.CV_8UC1,
                            Cv.Scalar.All(0));
                        if (result.Count > 0 && acceptedBoundsArea * 4 < imageArea * 3)
                        {
                            CreateObjectDefinitionRetainedMaskByAcceptedComponents(
                                labels,
                                retainedMask,
                                acceptedComponents.Where(component => retainedLabels.Contains(component.Label)));
                        }
                        else if (result.Count > 0)
                        {
                            CreateObjectDefinitionRetainedMaskByRows(
                                labels,
                                retainedMask,
                                accepted);
                        }
                    }
                }
                retainedMaskStopwatch.Stop();
                retainedMaskElapsedMilliseconds = retainedMaskStopwatch.ElapsedMilliseconds;

                return result;
            }
        }

        private static List<ObjectDefinitionDetectedObject> FilterObjectDefinitionGroupsByArea(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            double minArea,
            double maxArea)
        {
            IEnumerable<ObjectDefinitionDetectedObject> filtered = objects ??
                Enumerable.Empty<ObjectDefinitionDetectedObject>();
            if (minArea > 0)
            {
                filtered = filtered.Where(item => item.Area >= minArea);
            }

            if (maxArea > 0)
            {
                filtered = filtered.Where(item => item.Area <= maxArea);
            }

            return filtered.ToList();
        }

        private static Dictionary<int, Cv.Point[]> CreateObjectDefinitionLabelContours(
            Cv.Mat labels,
            IEnumerable<ObjectDefinitionComponentRegion> components,
            ISet<int> retainedLabels)
        {
            var contoursByLabel = new Dictionary<int, Cv.Point[]>();
            if (labels == null || labels.Empty() || components == null || retainedLabels == null)
            {
                return contoursByLabel;
            }

            foreach (ObjectDefinitionComponentRegion component in components)
            {
                if (component == null || !retainedLabels.Contains(component.Label) ||
                    component.Width <= 0 || component.Height <= 0)
                {
                    continue;
                }

                using (var labelRoi = new Cv.Mat(
                    labels,
                    new Cv.Rect(
                        component.X,
                        component.Y,
                        component.Width,
                        component.Height)))
                using (var componentMask = new Cv.Mat())
                {
                    Cv.Cv2.Compare(
                        labelRoi,
                        component.Label,
                        componentMask,
                        Cv.CmpType.EQ);

                    Cv.Point[][] contours;
                    Cv.HierarchyIndex[] hierarchy;
                    Cv.Cv2.FindContours(
                        componentMask,
                        out contours,
                        out hierarchy,
                        Cv.RetrievalModes.External,
                        Cv.ContourApproximationModes.ApproxSimple);
                    if (contours == null || contours.Length == 0)
                    {
                        continue;
                    }

                    Cv.Point[] points = contours
                        .Where(contour => contour != null && contour.Length > 0)
                        .SelectMany(contour => contour)
                        .Select(point => new Cv.Point(
                            point.X + component.X,
                            point.Y + component.Y))
                        .ToArray();
                    if (points.Length > 0)
                    {
                        contoursByLabel[component.Label] = points;
                    }
                }
            }

            return contoursByLabel;
        }

        private static void CalculateObjectDefinitionRotationGeometry(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            IDictionary<int, Cv.Point[]> labelContours,
            Rectangle roi)
        {
            if (objects == null || labelContours == null || labelContours.Count == 0)
            {
                return;
            }

            foreach (ObjectDefinitionDetectedObject item in objects)
            {
                if (item == null || item.SourceLabels == null || item.SourceLabels.Count == 0)
                {
                    continue;
                }

                Cv.Point2f[] points = item.SourceLabels
                    .Distinct()
                    .Where(label => labelContours.ContainsKey(label))
                    .SelectMany(label => labelContours[label])
                    .Select(point => new Cv.Point2f(point.X, point.Y))
                    .ToArray();
                if (points.Length < 3)
                {
                    continue;
                }

                Cv.RotatedRect rotatedRect = Cv.Cv2.MinAreaRect(points);
                float width = rotatedRect.Size.Width;
                float height = rotatedRect.Size.Height;
                double angle = rotatedRect.Angle;
                if (width < height)
                {
                    float swap = width;
                    width = height;
                    height = swap;
                    angle += 90.0;
                }

                while (angle <= -90.0)
                {
                    angle += 180.0;
                }

                while (angle > 90.0)
                {
                    angle -= 180.0;
                }

                Cv.Point2f[] corners = rotatedRect.Points();
                item.HasRotationGeometry = corners != null && corners.Length == 4;
                item.RotationAngleDegrees = angle;
                item.RotationCenter = new PointF(
                    roi.X + rotatedRect.Center.X,
                    roi.Y + rotatedRect.Center.Y);
                item.RotationSize = new SizeF(width, height);
                item.RotationCorners = item.HasRotationGeometry
                    ? corners.Select(point => new PointF(
                        roi.X + point.X,
                        roi.Y + point.Y)).ToArray()
                    : null;
            }
        }

        private static Cv.Mat CreateObjectDefinitionRetainedMaskByCloneAndRemove(
            Cv.Mat sourceMask,
            Cv.Mat labels,
            IEnumerable<ObjectDefinitionComponentRegion> rejectedComponents)
        {
            Cv.Mat retainedMask = sourceMask.Clone();
            try
            {
                foreach (ObjectDefinitionComponentRegion component in rejectedComponents)
                {
                    using (var labelRoi = new Cv.Mat(
                        labels,
                        new Cv.Rect(
                            component.X,
                            component.Y,
                            component.Width,
                            component.Height)))
                    using (var componentMask = new Cv.Mat())
                    using (var retainedRoi = new Cv.Mat(
                        retainedMask,
                        new Cv.Rect(
                            component.X,
                            component.Y,
                            component.Width,
                            component.Height)))
                    {
                        Cv.Cv2.Compare(
                            labelRoi,
                            component.Label,
                            componentMask,
                            Cv.CmpType.EQ);
                        retainedRoi.SetTo(Cv.Scalar.All(0), componentMask);
                    }
                }

                return retainedMask;
            }
            catch
            {
                retainedMask.Dispose();
                throw;
            }
        }

        private static void CreateObjectDefinitionRetainedMaskByAcceptedComponents(
            Cv.Mat labels,
            Cv.Mat retainedMask,
            IEnumerable<ObjectDefinitionComponentRegion> acceptedComponents)
        {
            foreach (ObjectDefinitionComponentRegion component in acceptedComponents)
            {
                using (var labelRoi = new Cv.Mat(
                    labels,
                    new Cv.Rect(
                        component.X,
                        component.Y,
                        component.Width,
                        component.Height)))
                using (var componentMask = new Cv.Mat())
                using (var retainedRoi = new Cv.Mat(
                    retainedMask,
                    new Cv.Rect(
                        component.X,
                        component.Y,
                        component.Width,
                        component.Height)))
                {
                    Cv.Cv2.Compare(
                        labelRoi,
                        component.Label,
                        componentMask,
                        Cv.CmpType.EQ);
                    Cv.Cv2.BitwiseOr(
                        retainedRoi,
                        componentMask,
                        retainedRoi);
                }
            }
        }

        private static void CreateObjectDefinitionRetainedMaskByRows(
            Cv.Mat labels,
            Cv.Mat retainedMask,
            bool[] accepted)
        {
            int[] labelsRow = new int[labels.Width];
            byte[] retainedRow = new byte[labels.Width];
            long labelsStride = labels.Step();
            long retainedStride = retainedMask.Step();
            for (int y = 0; y < labels.Height; y++)
            {
                Marshal.Copy(
                    GetObjectDefinitionMatRowPointer(labels, y, labelsStride),
                    labelsRow,
                    0,
                    labelsRow.Length);
                Array.Clear(retainedRow, 0, retainedRow.Length);
                for (int x = 0; x < labelsRow.Length; x++)
                {
                    int label = labelsRow[x];
                    if (label > 0 && label < accepted.Length && accepted[label])
                    {
                        retainedRow[x] = 255;
                    }
                }

                Marshal.Copy(
                    retainedRow,
                    0,
                    GetObjectDefinitionMatRowPointer(retainedMask, y, retainedStride),
                    retainedRow.Length);
            }
        }

        private static IntPtr GetObjectDefinitionMatRowPointer(
            Cv.Mat mat,
            int row,
            long stride)
        {
            long address = mat.Data.ToInt64() + ((long)row * stride);
            return new IntPtr(address);
        }

        private static List<ObjectDefinitionDetectedObject> OrderObjectDefinitionDetectedObjects(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            ObjectDefinitionSettings definition)
        {
            IEnumerable<ObjectDefinitionDetectedObject> ordered = objects ?? Enumerable.Empty<ObjectDefinitionDetectedObject>();
            if (string.Equals(definition.NumberingOrder, "LeftToRightTopToBottom", StringComparison.Ordinal))
            {
                ordered = ordered.OrderBy(item => item.Bounds.Left).ThenBy(item => item.Bounds.Top);
            }
            else if (string.Equals(definition.NumberingOrder, "AreaDescending", StringComparison.Ordinal))
            {
                ordered = ordered.OrderByDescending(item => item.Area).ThenBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left);
            }
            else if (string.Equals(definition.NumberingOrder, "AreaAscending", StringComparison.Ordinal))
            {
                ordered = ordered.OrderBy(item => item.Area).ThenBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left);
            }
            else
            {
                ordered = OrderObjectDefinitionObjectsByRows(ordered);
            }

            return ordered.ToList();
        }

        private static IEnumerable<ObjectDefinitionDetectedObject> OrderObjectDefinitionObjectsByRows(
            IEnumerable<ObjectDefinitionDetectedObject> objects)
        {
            List<ObjectDefinitionDetectedObject> items = (objects ??
                Enumerable.Empty<ObjectDefinitionDetectedObject>()).ToList();
            if (items.Count <= 1)
            {
                return items;
            }

            List<int> heights = items
                .Where(item => item.Bounds.Height > 0)
                .Select(item => item.Bounds.Height)
                .OrderBy(height => height)
                .ToList();
            int typicalHeight = heights.Count == 0
                ? 1
                : heights[heights.Count / 2];
            double rowTolerance = Math.Max(1.0, typicalHeight * 0.5);

            var rows = new List<ObjectDefinitionNumberingRow>();
            foreach (ObjectDefinitionDetectedObject item in items
                .OrderBy(item => item.Bounds.Top + (item.Bounds.Height / 2.0))
                .ThenBy(item => item.Bounds.Left))
            {
                double centerY = item.Bounds.Top + (item.Bounds.Height / 2.0);
                ObjectDefinitionNumberingRow row = rows
                    .OrderBy(candidate => Math.Abs(candidate.CenterY - centerY))
                    .FirstOrDefault(candidate =>
                        Math.Abs(candidate.CenterY - centerY) <= rowTolerance);
                if (row == null)
                {
                    row = new ObjectDefinitionNumberingRow();
                    rows.Add(row);
                }

                row.Items.Add(item);
                row.CenterY = row.Items
                    .Average(candidate => candidate.Bounds.Top + (candidate.Bounds.Height / 2.0));
            }

            return rows
                .OrderBy(row => row.CenterY)
                .SelectMany(row => row.Items
                    .OrderBy(item => item.Bounds.Left)
                    .ThenBy(item => item.Bounds.Top));
        }

        private sealed class ObjectDefinitionNumberingRow
        {
            public ObjectDefinitionNumberingRow()
            {
                Items = new List<ObjectDefinitionDetectedObject>();
            }

            public double CenterY { get; set; }

            public List<ObjectDefinitionDetectedObject> Items { get; private set; }
        }

        private static List<ObjectDefinitionDetectedObject> MergeObjectDefinitionDetectedObjects(
            List<ObjectDefinitionDetectedObject> objects,
            int maxDistance)
        {
            if (objects == null || objects.Count < 2 || maxDistance <= 0)
            {
                return objects ?? new List<ObjectDefinitionDetectedObject>();
            }

            int[] parents = new int[objects.Count];
            for (int index = 0; index < parents.Length; index++)
            {
                parents[index] = index;
            }

            var orderedIndexes = Enumerable.Range(0, objects.Count)
                .OrderBy(index => objects[index].Bounds.Left)
                .ToList();
            var activeIndexes = new List<int>();
            long maxDistanceSquared = (long)maxDistance * maxDistance;

            foreach (int currentIndex in orderedIndexes)
            {
                Rectangle currentBounds = objects[currentIndex].Bounds;
                for (int activeIndex = activeIndexes.Count - 1; activeIndex >= 0; activeIndex--)
                {
                    int previousIndex = activeIndexes[activeIndex];
                    if (objects[previousIndex].Bounds.Right + maxDistance < currentBounds.Left)
                    {
                        activeIndexes.RemoveAt(activeIndex);
                    }
                }

                foreach (int previousIndex in activeIndexes)
                {
                    if (GetRectangleGapDistanceSquared(
                            objects[previousIndex].Bounds,
                            currentBounds) <= maxDistanceSquared)
                    {
                        UnionObjectDefinitionParents(parents, previousIndex, currentIndex);
                    }
                }

                activeIndexes.Add(currentIndex);
            }

            var merged = new Dictionary<int, ObjectDefinitionDetectedObject>();
            for (int index = 0; index < objects.Count; index++)
            {
                int root = FindObjectDefinitionParent(parents, index);
                ObjectDefinitionDetectedObject current;
                if (!merged.TryGetValue(root, out current))
                {
                    merged[root] = new ObjectDefinitionDetectedObject
                    {
                        Bounds = objects[index].Bounds,
                        Area = objects[index].Area,
                        SourceLabels = new List<int>(
                            objects[index].SourceLabels ?? Enumerable.Empty<int>())
                    };
                    continue;
                }

                current.Bounds = Rectangle.Union(current.Bounds, objects[index].Bounds);
                current.Area += objects[index].Area;
                if (objects[index].SourceLabels != null)
                {
                    current.SourceLabels.AddRange(objects[index].SourceLabels);
                }
            }

            return merged.Values.ToList();
        }

        private static long GetRectangleGapDistanceSquared(Rectangle first, Rectangle second)
        {
            long dx = 0;
            if (first.Right < second.Left)
            {
                dx = second.Left - first.Right;
            }
            else if (second.Right < first.Left)
            {
                dx = first.Left - second.Right;
            }

            long dy = 0;
            if (first.Bottom < second.Top)
            {
                dy = second.Top - first.Bottom;
            }
            else if (second.Bottom < first.Top)
            {
                dy = first.Top - second.Bottom;
            }

            return (dx * dx) + (dy * dy);
        }

        private static int FindObjectDefinitionParent(int[] parents, int index)
        {
            int root = index;
            while (parents[root] != root)
            {
                root = parents[root];
            }

            while (parents[index] != index)
            {
                int next = parents[index];
                parents[index] = root;
                index = next;
            }

            return root;
        }

        private static void UnionObjectDefinitionParents(int[] parents, int first, int second)
        {
            int firstRoot = FindObjectDefinitionParent(parents, first);
            int secondRoot = FindObjectDefinitionParent(parents, second);
            if (firstRoot != secondRoot)
            {
                parents[secondRoot] = firstRoot;
            }
        }

    }
}
