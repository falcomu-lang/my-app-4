using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private readonly object objectDefinitionResultLock = new object();
        private readonly Dictionary<string, List<ObjectDefinitionDetectedObject>> objectDefinitionResults =
            new Dictionary<string, List<ObjectDefinitionDetectedObject>>(StringComparer.Ordinal);
        private int objectDefinitionResultGeneration;
        private string activeObjectDefinitionResultId;
        private bool objectDefinitionProcessingRequested;

        private sealed class ObjectDefinitionDetectedObject
        {
            public int Number { get; set; }

            public Rectangle Bounds { get; set; }

            public double Area { get; set; }
        }

        private void StartObjectDefinitionProcessing(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(definition.SourceId))
            {
                statusLabel.Text = definition.DisplayName + " 尚未設定來源區塊";
                return;
            }

            LargeImageSource source = GetObjectDefinitionSharedImageSource();
            if (source == null)
            {
                Bitmap bitmap = rightOriginalDisplayControl == null
                    ? null
                    : rightOriginalDisplayControl.CloneImage();
                if (bitmap == null && leftOriginalDisplayControl != null)
                {
                    bitmap = leftOriginalDisplayControl.CloneImage();
                }

                if (bitmap == null)
                {
                    statusLabel.Text = "請先載入圖片";
                    return;
                }

                List<Rectangle> bitmapRois = GetValidObjectDefinitionBitmapRois(bitmap.Size);
                if (bitmapRois.Count == 0)
                {
                    bitmap.Dispose();
                    statusLabel.Text = "尚未設定有效的 ROI";
                    return;
                }

                StartObjectDefinitionBitmapProcessing(definition, bitmap, bitmapRois);
                return;
            }

            List<Rectangle> rois = systemParameters.RoiRegions
                .Where(region => region.Bounds.Width > 0 && region.Bounds.Height > 0)
                .Select(region => region.Bounds)
                .ToList();
            if (rois.Count == 0)
            {
                source.ReleaseReference();
                statusLabel.Text = "尚未設定有效的 ROI";
                return;
            }

            int generation;
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                generation = objectDefinitionResultGeneration;
                activeObjectDefinitionResultId = definition.Id;
                objectDefinitionProcessingRequested = true;
                objectDefinitionResults.Clear();
            }

            statusLabel.Text = definition.DisplayName + " 影像處理中...使用 OpenCV CCL";
            Task.Run(
                delegate
                {
                    var completedResults = new Dictionary<string, List<ObjectDefinitionDetectedObject>>(StringComparer.Ordinal);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        foreach (Rectangle roi in rois)
                        {
                            EnsureObjectDefinitionRequestIsCurrent(definition.Id, generation);
                            using (Cv.Mat sourceMask = CreateObjectDefinitionSourceMask(source, definition, roi))
                            {
                                completedResults[CreateObjectDefinitionResultKey(definition.Id, roi)] =
                                    CreateObjectDefinitionDetectedObjects(sourceMask, roi, definition);
                            }
                        }

                        List<ObjectDefinitionDetectedObject> allObjects = completedResults.Values
                            .SelectMany(items => items)
                            .ToList();
                        List<ObjectDefinitionDetectedObject> orderedObjects =
                            OrderObjectDefinitionDetectedObjects(allObjects, definition);
                        for (int index = 0; index < orderedObjects.Count; index++)
                        {
                            orderedObjects[index].Number = index + 1;
                        }

                        stopwatch.Stop();
                        long elapsedMilliseconds = Math.Max(1, stopwatch.ElapsedMilliseconds);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    lock (objectDefinitionResultLock)
                                    {
                                        if (generation != objectDefinitionResultGeneration ||
                                            !string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal))
                                        {
                                            return;
                                        }

                                        objectDefinitionResults.Clear();
                                        foreach (KeyValuePair<string, List<ObjectDefinitionDetectedObject>> item in completedResults)
                                        {
                                            objectDefinitionResults[item.Key] = item.Value;
                                        }
                                    }

                                    int count = completedResults.Values.Sum(items => items.Count);
                                    statusLabel.Text = definition.DisplayName +
                                        " 已完成：OpenCV CCL，找到 " +
                                        count.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                        " 個物件，處理時間：" +
                                        elapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                        " ms";
                                    leftObjectsDisplayControl.InvalidateImageView();
                                    rightObjectsDisplayControl.InvalidateImageView();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        if (generation == objectDefinitionResultGeneration &&
                                            string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal))
                                        {
                                            statusLabel.Text = definition.DisplayName +
                                                " 物件編號失敗：" + ex.Message;
                                        }
                                    }));
                        }
                        catch (InvalidOperationException)
                        {
                            // The form may close while a background CCL is finishing.
                        }
                    }
                    finally
                    {
                        source.ReleaseReference();
                    }
                });
        }

        private List<Rectangle> GetValidObjectDefinitionBitmapRois(Size imageSize)
        {
            Rectangle imageBounds = new Rectangle(Point.Empty, imageSize);
            return systemParameters.RoiRegions
                .Select(region => Rectangle.Intersect(region.Bounds, imageBounds))
                .Where(roi => roi.Width > 0 && roi.Height > 0)
                .ToList();
        }

        private void StartObjectDefinitionBitmapProcessing(
            ObjectDefinitionSettings definition,
            Bitmap original,
            List<Rectangle> rois)
        {
            int generation;
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                generation = objectDefinitionResultGeneration;
                activeObjectDefinitionResultId = definition.Id;
                objectDefinitionProcessingRequested = true;
                objectDefinitionResults.Clear();
            }

            statusLabel.Text = definition.DisplayName + " 影像處理中...使用 OpenCV CCL";
            Task.Run(
                delegate
                {
                    var completedResults = new Dictionary<string, List<ObjectDefinitionDetectedObject>>(StringComparer.Ordinal);
                    Bitmap leftResult = null;
                    Bitmap rightResult = null;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        using (Cv.Mat originalGray = CreateOpenCvGrayMat(original))
                        {
                            foreach (Rectangle roi in rois)
                            {
                                EnsureObjectDefinitionRequestIsCurrent(definition.Id, generation);
                                using (Cv.Mat sourceMask = CreateObjectDefinitionSourceMask(
                                    original,
                                    originalGray,
                                    definition,
                                    roi))
                                {
                                    completedResults[CreateObjectDefinitionResultKey(definition.Id, roi)] =
                                        CreateObjectDefinitionDetectedObjects(sourceMask, roi, definition);
                                }
                            }
                        }

                        List<ObjectDefinitionDetectedObject> allObjects = completedResults.Values
                            .SelectMany(items => items)
                            .ToList();
                        List<ObjectDefinitionDetectedObject> orderedObjects =
                            OrderObjectDefinitionDetectedObjects(allObjects, definition);
                        for (int index = 0; index < orderedObjects.Count; index++)
                        {
                            orderedObjects[index].Number = index + 1;
                        }

                        leftResult = CreateObjectDefinitionAnnotatedBitmap(original, completedResults, definition, rois);
                        rightResult = new Bitmap(leftResult);
                        stopwatch.Stop();
                        long elapsedMilliseconds = Math.Max(1, stopwatch.ElapsedMilliseconds);
                        Bitmap displayLeftResult = leftResult;
                        Bitmap displayRightResult = rightResult;
                        leftResult = null;
                        rightResult = null;
                        try
                        {
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        bool isCurrent;
                                        lock (objectDefinitionResultLock)
                                        {
                                            isCurrent = generation == objectDefinitionResultGeneration &&
                                                string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal);
                                            if (isCurrent)
                                            {
                                                objectDefinitionResults.Clear();
                                                foreach (KeyValuePair<string, List<ObjectDefinitionDetectedObject>> item in completedResults)
                                                {
                                                    objectDefinitionResults[item.Key] = item.Value;
                                                }
                                            }
                                        }

                                        if (!isCurrent)
                                        {
                                            displayLeftResult.Dispose();
                                            displayRightResult.Dispose();
                                            displayLeftResult = null;
                                            displayRightResult = null;
                                            return;
                                        }

                                        try
                                        {
                                            leftObjectsDisplayControl.SetDisplayImage(displayLeftResult, true);
                                            displayLeftResult = null;
                                            rightObjectsDisplayControl.SetDisplayImage(displayRightResult, true);
                                            displayRightResult = null;
                                            int count = completedResults.Values.Sum(items => items.Count);
                                            statusLabel.Text = definition.DisplayName +
                                                " 已完成：OpenCV CCL，找到 " +
                                                count.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                                " 個物件，處理時間：" +
                                                elapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                                " ms";
                                        }
                                        catch
                                        {
                                            if (displayLeftResult != null)
                                            {
                                                displayLeftResult.Dispose();
                                                displayLeftResult = null;
                                            }

                                            if (displayRightResult != null)
                                            {
                                                displayRightResult.Dispose();
                                                displayRightResult = null;
                                            }

                                            throw;
                                        }
                                    }));
                        }
                        catch
                        {
                            displayLeftResult.Dispose();
                            displayRightResult.Dispose();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        if (generation == objectDefinitionResultGeneration &&
                                            string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal))
                                        {
                                            statusLabel.Text = definition.DisplayName +
                                                " 物件編號失敗：" + ex.Message;
                                        }
                                    }));
                        }
                        catch (InvalidOperationException)
                        {
                            // The form may close while a background CCL is finishing.
                        }
                    }
                    finally
                    {
                        if (leftResult != null)
                        {
                            leftResult.Dispose();
                        }

                        if (rightResult != null)
                        {
                            rightResult.Dispose();
                        }

                        original.Dispose();
                    }
                });
        }

        private LargeImageSource GetObjectDefinitionSharedImageSource()
        {
            LargeImageSource source = rightOriginalDisplayControl == null
                ? null
                : rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source != null)
            {
                return source;
            }

            source = leftOriginalDisplayControl == null
                ? null
                : leftOriginalDisplayControl.GetSharedLargeImageSource();
            if (source != null)
            {
                return source;
            }

            source = rightObjectsDisplayControl == null
                ? null
                : rightObjectsDisplayControl.GetSharedLargeImageSource();
            if (source != null)
            {
                return source;
            }

            return leftObjectsDisplayControl == null
                ? null
                : leftObjectsDisplayControl.GetSharedLargeImageSource();
        }

        private void EnsureObjectDefinitionRequestIsCurrent(string definitionId, int generation)
        {
            lock (objectDefinitionResultLock)
            {
                if (!objectDefinitionProcessingRequested ||
                    generation != objectDefinitionResultGeneration ||
                    !string.Equals(activeObjectDefinitionResultId, definitionId, StringComparison.Ordinal))
                {
                    throw new OperationCanceledException("物件編號請求已過期");
                }
            }
        }

        private void InvalidateObjectDefinitionResults()
        {
            lock (objectDefinitionResultLock)
            {
                objectDefinitionResultGeneration++;
                activeObjectDefinitionResultId = null;
                objectDefinitionProcessingRequested = false;
                objectDefinitionResults.Clear();
            }

            if (leftObjectsDisplayControl != null)
            {
                leftObjectsDisplayControl.InvalidateImageView();
            }

            if (rightObjectsDisplayControl != null)
            {
                rightObjectsDisplayControl.InvalidateImageView();
            }
        }

        private void InvalidateObjectDefinitionDisplayOnly(string definitionId)
        {
            if (rightOriginalDisplayControl != null && rightOriginalDisplayControl.IsLargeImageMode)
            {
                leftObjectsDisplayControl.InvalidateImageView();
                rightObjectsDisplayControl.InvalidateImageView();
                return;
            }

            Dictionary<string, List<ObjectDefinitionDetectedObject>> results;
            lock (objectDefinitionResultLock)
            {
                if (!objectDefinitionProcessingRequested ||
                    !string.Equals(activeObjectDefinitionResultId, definitionId, StringComparison.Ordinal) ||
                    objectDefinitionResults.Count == 0)
                {
                    leftObjectsDisplayControl.InvalidateImageView();
                    rightObjectsDisplayControl.InvalidateImageView();
                    return;
                }

                results = objectDefinitionResults.ToDictionary(
                    item => item.Key,
                    item => new List<ObjectDefinitionDetectedObject>(item.Value),
                    StringComparer.Ordinal);
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            Bitmap original = rightOriginalDisplayControl == null
                ? null
                : rightOriginalDisplayControl.CloneImage();
            if (original == null && leftOriginalDisplayControl != null)
            {
                original = leftOriginalDisplayControl.CloneImage();
            }

            if (definition == null || original == null)
            {
                if (original != null)
                {
                    original.Dispose();
                }

                return;
            }

            Bitmap leftResult = null;
            Bitmap rightResult = null;
            try
            {
                List<Rectangle> rois = GetValidObjectDefinitionBitmapRois(original.Size);
                leftResult = CreateObjectDefinitionAnnotatedBitmap(original, results, definition, rois);
                rightResult = new Bitmap(leftResult);
                leftObjectsDisplayControl.SetDisplayImage(leftResult, true);
                leftResult = null;
                rightObjectsDisplayControl.SetDisplayImage(rightResult, true);
                rightResult = null;
            }
            finally
            {
                if (leftResult != null)
                {
                    leftResult.Dispose();
                }

                if (rightResult != null)
                {
                    rightResult.Dispose();
                }

                original.Dispose();
            }
        }

        private Cv.Mat CreateObjectDefinitionSourceMask(
            LargeImageSource source,
            ObjectDefinitionSettings definition,
            Rectangle roi)
        {
            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                if (group == null)
                {
                    throw new InvalidOperationException("物件組來源群組不存在");
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                if (objectJudgements.Count == 0)
                {
                    throw new InvalidOperationException("物件組來源群組沒有區塊");
                }

                return CreateLargeObjectJudgementGroupMask(source, objectJudgements, roi);
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
            if (objectJudgement == null)
            {
                throw new InvalidOperationException("物件組來源區塊不存在");
            }

            List<ObjectJudgementProcessingSettings> processingSteps =
                GetObjectJudgementProcessingChain(objectJudgement, -1);
            using (Cv.Mat baseMask = CreateLargeObjectJudgementBaseMask(source, objectJudgement, roi))
            {
                return ApplyObjectJudgementProcessingOpenCv(baseMask, processingSteps);
            }
        }

        private Cv.Mat CreateObjectDefinitionSourceMask(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectDefinitionSettings definition,
            Rectangle roi)
        {
            string sourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            if (string.Equals(sourceType, "Group", StringComparison.Ordinal))
            {
                ObjectJudgementGroupSettings group = FindObjectJudgementGroup(definition.SourceId);
                if (group == null)
                {
                    throw new InvalidOperationException("物件組來源群組不存在");
                }

                List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(group.Id);
                if (objectJudgements.Count == 0)
                {
                    throw new InvalidOperationException("物件組來源群組沒有區塊");
                }

                var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
                try
                {
                    foreach (ObjectJudgementSettings objectJudgement in objectJudgements)
                    {
                        List<ObjectJudgementProcessingSettings> processingSteps =
                            GetObjectJudgementProcessingChain(objectJudgement, -1);
                        using (Cv.Mat baseMask = CreateObjectJudgementBaseMaskFromBitmap(
                            original,
                            originalGray,
                            objectJudgement,
                            roi))
                        using (Cv.Mat objectMask = ApplyObjectJudgementProcessingOpenCv(
                            baseMask,
                            processingSteps))
                        {
                            Cv.Cv2.BitwiseOr(combined, objectMask, combined);
                        }
                    }

                    return combined;
                }
                catch
                {
                    combined.Dispose();
                    throw;
                }
            }

            ObjectJudgementSettings objectJudgementSettings = systemParameters.ObjectJudgements.Find(
                item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
            if (objectJudgementSettings == null)
            {
                throw new InvalidOperationException("物件組來源區塊不存在");
            }

            List<ObjectJudgementProcessingSettings> steps =
                GetObjectJudgementProcessingChain(objectJudgementSettings, -1);
            using (Cv.Mat baseMask = CreateObjectJudgementBaseMaskFromBitmap(
                original,
                originalGray,
                objectJudgementSettings,
                roi))
            {
                return ApplyObjectJudgementProcessingOpenCv(baseMask, steps);
            }
        }

        private Bitmap CreateObjectDefinitionAnnotatedBitmap(
            Bitmap original,
            IDictionary<string, List<ObjectDefinitionDetectedObject>> results,
            ObjectDefinitionSettings definition,
            IEnumerable<Rectangle> rois)
        {
            var result = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            using (var pen = new Pen(Color.Yellow, Math.Max(1, Math.Min(20, definition.ResultBoxLineWidth))))
            using (var brush = new SolidBrush(Color.Yellow))
            using (var font = new Font(
                SystemFonts.DefaultFont.FontFamily,
                Math.Max(6, Math.Min(72, definition.ResultNumberFontSize)),
                FontStyle.Bold))
            {
                graphics.DrawImageUnscaled(original, Point.Empty);
                foreach (Rectangle roi in rois)
                {
                    string key = CreateObjectDefinitionResultKey(definition.Id, roi);
                    List<ObjectDefinitionDetectedObject> objects;
                    if (!results.TryGetValue(key, out objects))
                    {
                        continue;
                    }

                    foreach (ObjectDefinitionDetectedObject item in objects)
                    {
                        graphics.DrawRectangle(pen, item.Bounds);
                        graphics.DrawString(
                            item.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            font,
                            brush,
                            item.Bounds.Left + 2,
                            item.Bounds.Top + 2);
                    }
                }
            }

            return result;
        }

        private static List<ObjectDefinitionDetectedObject> CreateObjectDefinitionDetectedObjects(
            Cv.Mat sourceMask,
            Rectangle roi,
            ObjectDefinitionSettings definition)
        {
            if (sourceMask == null || sourceMask.Empty())
            {
                return new List<ObjectDefinitionDetectedObject>();
            }

            using (var labels = new Cv.Mat())
            using (var stats = new Cv.Mat())
            using (var centroids = new Cv.Mat())
            {
                Cv.PixelConnectivity connectivity = definition.Connectivity == 4
                    ? Cv.PixelConnectivity.Connectivity4
                    : Cv.PixelConnectivity.Connectivity8;
                int labelCount = Cv.Cv2.ConnectedComponentsWithStats(
                    sourceMask,
                    labels,
                    stats,
                    centroids,
                    connectivity,
                    Cv.MatType.CV_32SC1);
                var result = new List<ObjectDefinitionDetectedObject>();
                for (int label = 1; label < labelCount; label++)
                {
                    int area = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area);
                    if (definition.MinArea > 0 && area < definition.MinArea)
                    {
                        continue;
                    }

                    if (definition.MaxArea > 0 && area > definition.MaxArea)
                    {
                        continue;
                    }

                    int x = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Left);
                    int y = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Top);
                    int width = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Width);
                    int height = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Height);
                    result.Add(new ObjectDefinitionDetectedObject
                    {
                        Bounds = new Rectangle(roi.X + x, roi.Y + y, width, height),
                        Area = area
                    });
                }

                if (string.Equals(definition.MergeMethod, "Distance", StringComparison.Ordinal) &&
                    definition.MaxMergeDistance > 0)
                {
                    result = MergeObjectDefinitionDetectedObjects(
                        result,
                        definition.MaxMergeDistance);
                }

                return result;
            }
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
                ordered = ordered.OrderBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left);
            }

            return ordered.ToList();
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
                        Area = objects[index].Area
                    };
                    continue;
                }

                current.Bounds = Rectangle.Union(current.Bounds, objects[index].Bounds);
                current.Area += objects[index].Area;
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

        private string CreateObjectDefinitionResultKey(string definitionId, Rectangle roi)
        {
            return string.Join(
                "|",
                imageSourceGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                definitionId ?? string.Empty,
                roi.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private void ObjectDefinitionDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            if (display == null || display.IsPanning)
            {
                return;
            }

            string definitionId = GetObjectDefinitionId(
                functionListBox.SelectedIndex,
                functionListBox.SelectedItem as string);
            if (string.IsNullOrEmpty(definitionId))
            {
                return;
            }

            List<ObjectDefinitionDetectedObject> objects = new List<ObjectDefinitionDetectedObject>();
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                List<ObjectDefinitionDetectedObject> roiObjects;
                lock (objectDefinitionResultLock)
                {
                    if (!objectDefinitionProcessingRequested ||
                        !string.Equals(activeObjectDefinitionResultId, definitionId, StringComparison.Ordinal) ||
                        !objectDefinitionResults.TryGetValue(
                            CreateObjectDefinitionResultKey(definitionId, roiRegion.Bounds),
                            out roiObjects))
                    {
                        continue;
                    }

                    objects.AddRange(roiObjects);
                }
            }

            if (objects.Count == 0)
            {
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            int boxLineWidth = definition == null ? 2 : Math.Max(1, Math.Min(20, definition.ResultBoxLineWidth));
            int numberFontSize = definition == null ? 10 : Math.Max(6, Math.Min(72, definition.ResultNumberFontSize));
            using (var pen = new Pen(Color.Yellow, boxLineWidth))
            using (var brush = new SolidBrush(Color.Yellow))
            using (var font = new Font(SystemFonts.DefaultFont.FontFamily, numberFontSize, FontStyle.Bold))
            {
                foreach (ObjectDefinitionDetectedObject item in objects)
                {
                    if (!e.VisibleSourceRect.IntersectsWith(item.Bounds))
                    {
                        continue;
                    }

                    float x = e.Offset.X + item.Bounds.X * e.Zoom;
                    float y = e.Offset.Y + item.Bounds.Y * e.Zoom;
                    float width = Math.Max(1f, item.Bounds.Width * e.Zoom);
                    float height = Math.Max(1f, item.Bounds.Height * e.Zoom);
                    e.Graphics.DrawRectangle(pen, x, y, width, height);
                    e.Graphics.DrawString(
                        item.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        font,
                        brush,
                        x + 2f,
                        y + 2f);
                }
            }
        }
    }
}
