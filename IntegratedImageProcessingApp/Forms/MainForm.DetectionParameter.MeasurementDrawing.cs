using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Interlocked = System.Threading.Interlocked;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private void BeginObjectDetectionMeasurementDrawing()
        {
            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                statusLabel.Text = "請先選擇已找到的物件序號，再開始畫量測線";
                return;
            }

            pendingObjectDetectionMeasurementGeometry.Reset();
            objectDetectionMeasurementCreateNewRecord = true;
            objectDetectionMeasurementIsDrawing = true;
            objectDetectionMeasurementDrawingStage = 0;
            objectDetectionMeasurementDrawStart = Point.Empty;
            objectDetectionMeasurementDrawCurrent = Point.Empty;
            objectDetectionMeasurementDrawButton.Text = "畫線中...請在待量測圖上拖曳";
            objectDetectionMeasurementToolStatusLabel.Text =
                GetObjectDetectionMeasurementMode() == "Parallel"
                    ? "請按住 Ctrl，再用滑鼠拖曳畫第一條平行線"
                    : "請按住 Ctrl，再用滑鼠拖曳畫量測線";
            statusLabel.Text = "尺寸量測：請按住 Ctrl 後在待量測圖片上拖曳畫線";
            objectDetectionMeasurementDisplayControl.InvalidateImageView();
        }

        private void CancelObjectDetectionMeasurementDrawing()
        {
            objectDetectionMeasurementIsDrawing = false;
            objectDetectionMeasurementDrawingStage = 0;
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            LoadPendingObjectDetectionMeasurementGeometry(parameter);
            if (objectDetectionMeasurementDrawButton != null)
            {
                objectDetectionMeasurementDrawButton.Text = "開始畫線";
            }

            if (objectDetectionMeasurementToolStatusLabel != null)
            {
                objectDetectionMeasurementToolStatusLabel.Text =
                    parameter != null && parameter.MeasurementLineConfigured
                        ? "已載入已套用的量測線設定"
                        : "尚未設定量測線";
            }

            if (objectDetectionMeasurementDisplayControl != null)
            {
                objectDetectionMeasurementDisplayControl.InvalidateImageView();
            }
        }

        private string GetObjectDetectionMeasurementMode()
        {
            string mode = objectDetectionMeasurementModeComboBox == null
                ? "Single"
                : objectDetectionMeasurementModeComboBox.SelectedItem as string;
            return string.Equals(mode, "Parallel", StringComparison.Ordinal)
                ? "Parallel"
                : "Single";
        }

        private string GetObjectDetectionMeasurementDirection()
        {
            string direction = objectDetectionMeasurementDirectionComboBox == null
                ? "Horizontal"
                : objectDetectionMeasurementDirectionComboBox.SelectedItem as string;
            return string.Equals(direction, "Vertical", StringComparison.Ordinal)
                ? "Vertical"
                : "Horizontal";
        }

        private ObjectDetectionImageLine NormalizeObjectDetectionImageLine(
            Point start,
            Point end,
            ObjectDefinitionDetectedObject selectedObject,
            string direction)
        {
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            PointF startLocal = frame.ToLocal(start);
            PointF endLocal = frame.ToLocal(end);
            if (string.Equals(direction, "Vertical", StringComparison.Ordinal))
            {
                float x = startLocal.X;
                return new ObjectDetectionImageLine(
                    frame.ToImage(x, startLocal.Y),
                    frame.ToImage(x, endLocal.Y));
            }

            float y = startLocal.Y;
            return new ObjectDetectionImageLine(
                frame.ToImage(startLocal.X, y),
                frame.ToImage(endLocal.X, y));
        }

        private static ObjectDetectionImageLine AlignParallelObjectDetectionImageLine(
            ObjectDetectionImageLine first,
            ObjectDetectionImageLine second,
            ObjectDetectionMeasurementFrame frame,
            string direction)
        {
            PointF firstStart = frame.ToLocal(first.X1, first.Y1);
            PointF firstEnd = frame.ToLocal(first.X2, first.Y2);
            PointF secondStart = frame.ToLocal(second.X1, second.Y1);
            if (string.Equals(direction, "Vertical", StringComparison.Ordinal))
            {
                float x = secondStart.X;
                return new ObjectDetectionImageLine(
                    frame.ToImage(x, firstStart.Y),
                    frame.ToImage(x, firstEnd.Y));
            }

            float y = secondStart.Y;
            return new ObjectDetectionImageLine(
                frame.ToImage(firstStart.X, y),
                frame.ToImage(firstEnd.X, y));
        }

        private static Point GetObjectDetectionImagePoint(Point point)
        {
            return point;
        }

        private void StoreObjectDetectionMeasurementLine(
            ObjectDetectionImageLine line,
            ObjectDefinitionDetectedObject selectedObject,
            bool secondLine)
        {
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            if (frame.Width <= 0 || frame.Height <= 0)
            {
                return;
            }

            PointF startLocal = frame.ToNormalizedLocal(line.X1, line.Y1);
            PointF endLocal = frame.ToNormalizedLocal(line.X2, line.Y2);
            double startX = startLocal.X;
            double startY = startLocal.Y;
            double endX = endLocal.X;
            double endY = endLocal.Y;
            string lineOrder = GetObjectDetectionMeasurementLineOrder(
                startLocal,
                endLocal);
            bool startOutsideRoi = IsObjectDetectionMeasurementPointOutsideRoi(startLocal);
            bool endOutsideRoi = IsObjectDetectionMeasurementPointOutsideRoi(endLocal);
            if (secondLine)
            {
                pendingObjectDetectionMeasurementGeometry.HasSecondLine = true;
                pendingObjectDetectionMeasurementGeometry.SecondStartX = startX;
                pendingObjectDetectionMeasurementGeometry.SecondStartY = startY;
                pendingObjectDetectionMeasurementGeometry.SecondEndX = endX;
                pendingObjectDetectionMeasurementGeometry.SecondEndY = endY;
                pendingObjectDetectionMeasurementGeometry.SecondLineOrder = lineOrder;
                pendingObjectDetectionMeasurementGeometry.SecondStartOutsideRoi = startOutsideRoi;
                pendingObjectDetectionMeasurementGeometry.SecondEndOutsideRoi = endOutsideRoi;
            }
            else
            {
                pendingObjectDetectionMeasurementGeometry.HasFirstLine = true;
                pendingObjectDetectionMeasurementGeometry.StartX = startX;
                pendingObjectDetectionMeasurementGeometry.StartY = startY;
                pendingObjectDetectionMeasurementGeometry.EndX = endX;
                pendingObjectDetectionMeasurementGeometry.EndY = endY;
                pendingObjectDetectionMeasurementGeometry.LineOrder = lineOrder;
                pendingObjectDetectionMeasurementGeometry.StartOutsideRoi = startOutsideRoi;
                pendingObjectDetectionMeasurementGeometry.EndOutsideRoi = endOutsideRoi;
            }
        }

        private static string GetObjectDetectionMeasurementLineOrder(
            PointF startLocal,
            PointF endLocal)
        {
            double deltaX = Math.Abs(endLocal.X - startLocal.X);
            double deltaY = Math.Abs(endLocal.Y - startLocal.Y);
            if (deltaY > deltaX)
            {
                return endLocal.Y >= startLocal.Y ? "TopToBottom" : "BottomToTop";
            }

            return endLocal.X >= startLocal.X ? "LeftToRight" : "RightToLeft";
        }

        private static bool IsObjectDetectionMeasurementPointOutsideRoi(PointF normalizedLocal)
        {
            const float tolerance = 0.0001f;
            return normalizedLocal.X < -tolerance || normalizedLocal.X > 1f + tolerance ||
                normalizedLocal.Y < -tolerance || normalizedLocal.Y > 1f + tolerance;
        }

        private void ObjectDetectionMeasurementDisplayControl_ImageMouseDown(
            object sender,
            ImageMouseEventArgs e)
        {
            if (!objectDetectionMeasurementIsDrawing || e == null ||
                e.Button != MouseButtons.Left ||
                (e.Modifiers & Keys.Control) == 0 ||
                !e.IsInsideImage)
            {
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                objectDetectionMeasurementIsDrawing = false;
                return;
            }

            objectDetectionMeasurementDrawStart = GetObjectDetectionImagePoint(e.ImageLocation);
            objectDetectionMeasurementDrawCurrent = objectDetectionMeasurementDrawStart;
            e.Handled = true;
        }

        private void ObjectDetectionMeasurementDisplayControl_ImageMouseMove(
            object sender,
            ImageMouseEventArgs e)
        {
            if (!objectDetectionMeasurementIsDrawing || e == null ||
                objectDetectionMeasurementDrawStart == Point.Empty)
            {
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                return;
            }

            if (e.IsInsideImage)
            {
                objectDetectionMeasurementDrawCurrent = GetObjectDetectionImagePoint(e.ImageLocation);
            }
            e.Handled = true;
            objectDetectionMeasurementDisplayControl.InvalidateImageView();
        }

        private void ObjectDetectionMeasurementDisplayControl_ImageMouseUp(
            object sender,
            ImageMouseEventArgs e)
        {
            if (!objectDetectionMeasurementIsDrawing || e == null ||
                e.Button != MouseButtons.Left ||
                objectDetectionMeasurementDrawStart == Point.Empty)
            {
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                return;
            }

            if (e.IsInsideImage)
            {
                objectDetectionMeasurementDrawCurrent = GetObjectDetectionImagePoint(e.ImageLocation);
            }
            ObjectDetectionImageLine line = NormalizeObjectDetectionImageLine(
                objectDetectionMeasurementDrawStart,
                objectDetectionMeasurementDrawCurrent,
                selectedObject,
                GetObjectDetectionMeasurementDirection());
            if (Math.Abs(line.X2 - line.X1) + Math.Abs(line.Y2 - line.Y1) < 2)
            {
                e.Handled = true;
                return;
            }

            if (objectDetectionMeasurementDrawingStage == 0)
            {
                StoreObjectDetectionMeasurementLine(line, selectedObject, false);
                objectDetectionMeasurementDrawingStage =
                    GetObjectDetectionMeasurementMode() == "Parallel" ? 1 : 2;
                objectDetectionMeasurementDrawStart = Point.Empty;
                objectDetectionMeasurementDrawCurrent = Point.Empty;
                if (objectDetectionMeasurementDrawingStage == 1)
                {
                    objectDetectionMeasurementToolStatusLabel.Text =
                        "第一條已完成，請再拖曳畫第二條平行線";
                }
                else
                {
                    objectDetectionMeasurementIsDrawing = false;
                    objectDetectionMeasurementDrawButton.Text = "開始畫線";
                    objectDetectionMeasurementToolStatusLabel.Text =
                        "量測線草稿已完成，請按套用量測設定保存";
                }
            }
            else
            {
                ObjectDetectionImageLine first = CreateObjectDetectionImageLine(
                    selectedObject,
                    pendingObjectDetectionMeasurementGeometry,
                    false);
                line = AlignParallelObjectDetectionImageLine(
                    first,
                    line,
                    CreateObjectDetectionMeasurementFrame(selectedObject),
                    GetObjectDetectionMeasurementDirection());
                StoreObjectDetectionMeasurementLine(line, selectedObject, true);
                objectDetectionMeasurementDrawingStage = 2;
                objectDetectionMeasurementIsDrawing = false;
                objectDetectionMeasurementDrawStart = Point.Empty;
                objectDetectionMeasurementDrawCurrent = Point.Empty;
                objectDetectionMeasurementDrawButton.Text = "開始畫線";
                objectDetectionMeasurementToolStatusLabel.Text =
                    "兩條平行量測線草稿已完成，請按套用量測設定保存";
            }

            e.Handled = true;
            objectDetectionMeasurementDisplayControl.InvalidateImageView();
        }

        private static ObjectDetectionMeasurementFrame CreateObjectDetectionMeasurementFrame(
            ObjectDefinitionDetectedObject selectedObject)
        {
            if (selectedObject != null && selectedObject.HasRotationGeometry &&
                selectedObject.RotationCorners != null &&
                selectedObject.RotationCorners.Length == 4)
            {
                PointF horizontalStart = selectedObject.RotationCorners[0];
                PointF horizontalEnd = selectedObject.RotationCorners[1];
                double bestHorizontalScore = double.MaxValue;
                float horizontalWidth = 0;
                int horizontalEdgeIndex = 0;

                for (int index = 0; index < selectedObject.RotationCorners.Length; index++)
                {
                    PointF start = selectedObject.RotationCorners[index];
                    PointF end = selectedObject.RotationCorners[(index + 1) % selectedObject.RotationCorners.Length];
                    float deltaX = end.X - start.X;
                    float deltaY = end.Y - start.Y;
                    float length = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                    if (length <= 0)
                    {
                        continue;
                    }

                    double horizontalScore = Math.Abs(deltaY) / length;
                    if (horizontalScore < bestHorizontalScore)
                    {
                        bestHorizontalScore = horizontalScore;
                        horizontalStart = start;
                        horizontalEnd = end;
                        horizontalWidth = length;
                        horizontalEdgeIndex = index;
                    }
                }

                if (horizontalWidth > 0)
                {
                    PointF verticalEnd = selectedObject.RotationCorners[
                        (horizontalEdgeIndex + 2) % selectedObject.RotationCorners.Length];
                    float verticalDeltaX = verticalEnd.X - horizontalEnd.X;
                    float verticalDeltaY = verticalEnd.Y - horizontalEnd.Y;
                    float verticalHeight = (float)Math.Sqrt(
                        (verticalDeltaX * verticalDeltaX) +
                        (verticalDeltaY * verticalDeltaY));
                    if (verticalHeight > 0)
                    {
                        float angleDeltaX = horizontalEnd.X - horizontalStart.X;
                        float angleDeltaY = horizontalEnd.Y - horizontalStart.Y;
                        if (angleDeltaX < 0 ||
                            (Math.Abs(angleDeltaX) < 0.0001f && angleDeltaY < 0))
                        {
                            angleDeltaX = -angleDeltaX;
                            angleDeltaY = -angleDeltaY;
                        }

                        double angle = Math.Atan2(angleDeltaY, angleDeltaX) * 180.0 / Math.PI;
                        while (angle <= -90.0)
                        {
                            angle += 180.0;
                        }

                        while (angle > 90.0)
                        {
                            angle -= 180.0;
                        }

                        return new ObjectDetectionMeasurementFrame(
                            selectedObject.RotationCenter,
                            horizontalWidth,
                            verticalHeight,
                            angle);
                    }
                }
            }

            if (selectedObject != null && selectedObject.HasRotationGeometry &&
                selectedObject.RotationSize.Width > 0 &&
                selectedObject.RotationSize.Height > 0)
            {
                return new ObjectDetectionMeasurementFrame(
                    selectedObject.RotationCenter,
                    selectedObject.RotationSize.Width,
                    selectedObject.RotationSize.Height,
                    selectedObject.RotationAngleDegrees);
            }

            Rectangle bounds = selectedObject == null
                ? Rectangle.Empty
                : selectedObject.Bounds;
            return new ObjectDetectionMeasurementFrame(
                new PointF(
                    bounds.Left + (bounds.Width / 2f),
                    bounds.Top + (bounds.Height / 2f)),
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height),
                0);
        }

        private static ObjectDetectionImageLine CreateObjectDetectionImageLine(
            ObjectDefinitionDetectedObject selectedObject,
            ObjectDetectionMeasurementGeometry geometry,
            bool secondLine)
        {
            double startX = secondLine ? geometry.SecondStartX : geometry.StartX;
            double startY = secondLine ? geometry.SecondStartY : geometry.StartY;
            double endX = secondLine ? geometry.SecondEndX : geometry.EndX;
            double endY = secondLine ? geometry.SecondEndY : geometry.EndY;
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            return new ObjectDetectionImageLine(
                frame.ToImage(startX, startY),
                frame.ToImage(endX, endY));
        }

        private static ObjectDetectionImageLine CreateObjectDetectionImageLine(
            ObjectDefinitionDetectedObject selectedObject,
            ObjectDetectionMeasurementRecordSettings record,
            bool secondLine)
        {
            if (record == null)
            {
                return new ObjectDetectionImageLine(0, 0, 0, 0);
            }

            double startX = secondLine ? record.SecondStartX : record.StartX;
            double startY = secondLine ? record.SecondStartY : record.StartY;
            double endX = secondLine ? record.SecondEndX : record.EndX;
            double endY = secondLine ? record.SecondEndY : record.EndY;
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            return new ObjectDetectionImageLine(
                frame.ToImage(startX, startY),
                frame.ToImage(endX, endY));
        }

        private static ObjectDetectionImageLine InterpolateObjectDetectionImageLine(
            ObjectDetectionImageLine first,
            ObjectDetectionImageLine second,
            double ratio)
        {
            return new ObjectDetectionImageLine(
                (int)Math.Round(first.X1 + ((second.X1 - first.X1) * ratio)),
                (int)Math.Round(first.Y1 + ((second.Y1 - first.Y1) * ratio)),
                (int)Math.Round(first.X2 + ((second.X2 - first.X2) * ratio)),
                (int)Math.Round(first.Y2 + ((second.Y2 - first.Y2) * ratio)));
        }

        private void ObjectDetectionMeasurementDisplayControl_ImageOverlayPaint(
            object sender,
            ImageOverlayPaintEventArgs e)
        {
            if (e == null || !isObjectDetectionParameterImageLayout)
            {
                return;
            }

            if (isObjectDetectionResultReviewMode)
            {
                DrawObjectDetectionResultReviewMeasurementOverlay(
                    e.Graphics,
                    e.Zoom,
                    e.Offset);
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                return;
            }

            ObjectDetectionImageLine first = CreateObjectDetectionImageLine(
                selectedObject,
                pendingObjectDetectionMeasurementGeometry,
                false);
            ObjectDetectionImageLine second = CreateObjectDetectionImageLine(
                selectedObject,
                pendingObjectDetectionMeasurementGeometry,
                true);
            bool hasFirst = pendingObjectDetectionMeasurementGeometry.HasFirstLine;
            bool hasSecond = pendingObjectDetectionMeasurementGeometry.HasSecondLine;

            if (objectDetectionMeasurementIsDrawing &&
                objectDetectionMeasurementDrawStart != Point.Empty)
            {
                ObjectDetectionImageLine preview = NormalizeObjectDetectionImageLine(
                    objectDetectionMeasurementDrawStart,
                    objectDetectionMeasurementDrawCurrent,
                    selectedObject,
                    GetObjectDetectionMeasurementDirection());
                if (objectDetectionMeasurementDrawingStage == 0)
                {
                    first = preview;
                    hasFirst = true;
                }
                else
                {
                    second = AlignParallelObjectDetectionImageLine(
                        first,
                        preview,
                        CreateObjectDetectionMeasurementFrame(selectedObject),
                        GetObjectDetectionMeasurementDirection());
                    hasSecond = true;
                }
            }

            if (!hasFirst)
            {
                DrawObjectDetectionMeasurementResultHighlights(
                    e.Graphics,
                    e,
                    selectedObject);
                return;
            }

            string mode = GetObjectDetectionMeasurementMode();
            int lineCount = objectDetectionMeasurementLineCountBox == null
                ? 1
                : Decimal.ToInt32(objectDetectionMeasurementLineCountBox.Value);
            lineCount = Math.Max(1, Math.Min(1000, lineCount));
            ObjectDetectionParameterSettings activeParameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDetectionMeasurementRecordSettings activeRecord =
                GetActiveObjectDetectionMeasurementRecord(activeParameter);
            bool clipLinesToMask = activeParameter != null &&
                activeParameter.MeasurementClipLinesToMask &&
                !objectDetectionMeasurementIsDrawing;
            bool needsMeasuredLines = clipLinesToMask;
            bool hasClippedLines = needsMeasuredLines &&
                IsObjectDetectionMeasurementClipCacheValid(
                    activeParameter,
                    activeRecord,
                    selectedObject);
            if (needsMeasuredLines && !hasClippedLines)
            {
                DrawObjectDetectionMeasurementResultHighlights(
                    e.Graphics,
                    e,
                    selectedObject);
                return;
            }

            if (objectDetectionMeasurementHighlightVisible && !needsMeasuredLines)
            {
                using (var highlightPen = new Pen(Color.Yellow, 4f))
                using (var highlightBrush = new SolidBrush(Color.FromArgb(55, Color.Gold)))
                {
                    if (string.Equals(mode, "Parallel", StringComparison.Ordinal) && hasSecond)
                    {
                        e.Graphics.FillPolygon(
                            highlightBrush,
                            new[]
                            {
                                new PointF(e.Offset.X + (first.X1 * e.Zoom), e.Offset.Y + (first.Y1 * e.Zoom)),
                                new PointF(e.Offset.X + (first.X2 * e.Zoom), e.Offset.Y + (first.Y2 * e.Zoom)),
                                new PointF(e.Offset.X + (second.X2 * e.Zoom), e.Offset.Y + (second.Y2 * e.Zoom)),
                                new PointF(e.Offset.X + (second.X1 * e.Zoom), e.Offset.Y + (second.Y1 * e.Zoom))
                            });
                        DrawObjectDetectionImageLine(
                            e.Graphics,
                            first,
                            highlightPen,
                            e.Zoom,
                            e.Offset);
                        DrawObjectDetectionImageLine(
                            e.Graphics,
                            second,
                            highlightPen,
                            e.Zoom,
                            e.Offset);
                    }
                    else
                    {
                        DrawObjectDetectionImageLine(
                            e.Graphics,
                            first,
                            highlightPen,
                            e.Zoom,
                            e.Offset);
                    }
                }
            }

            // Keep measurement lines at a stable screen width. Scaling the
            // pen by 1 / zoom makes a 0.05x view render a huge 40px line.
            bool cacheRenderPaths = !objectDetectionMeasurementIsDrawing;
            string renderPathCacheKey = cacheRenderPaths
                ? CreateObjectDetectionMeasurementRenderOverlayCacheKey(
                    activeParameter,
                    activeRecord,
                    selectedObject,
                    first,
                    second,
                    mode,
                    hasSecond,
                    lineCount,
                    clipLinesToMask,
                    e.Zoom)
                : null;
            bool rebuildRenderPaths = true;
            System.Drawing.Drawing2D.GraphicsPath primaryPath;
            System.Drawing.Drawing2D.GraphicsPath guidePath;
            System.Drawing.Drawing2D.GraphicsPath endpointPath;
            if (cacheRenderPaths)
            {
                if (!string.Equals(
                        objectDetectionMeasurementRenderOverlayCacheKey,
                        renderPathCacheKey,
                        StringComparison.Ordinal) ||
                    objectDetectionMeasurementRenderPrimaryPath == null ||
                    objectDetectionMeasurementRenderGuidePath == null ||
                    objectDetectionMeasurementRenderEndpointPath == null)
                {
                    ClearObjectDetectionMeasurementRenderOverlayCache();
                    objectDetectionMeasurementRenderPrimaryPath =
                        new System.Drawing.Drawing2D.GraphicsPath();
                    objectDetectionMeasurementRenderGuidePath =
                        new System.Drawing.Drawing2D.GraphicsPath();
                    objectDetectionMeasurementRenderEndpointPath =
                        new System.Drawing.Drawing2D.GraphicsPath();
                }
                else
                {
                    rebuildRenderPaths = false;
                }

                primaryPath = objectDetectionMeasurementRenderPrimaryPath;
                guidePath = objectDetectionMeasurementRenderGuidePath;
                endpointPath = objectDetectionMeasurementRenderEndpointPath;
            }
            else
            {
                primaryPath = new System.Drawing.Drawing2D.GraphicsPath();
                guidePath = new System.Drawing.Drawing2D.GraphicsPath();
                endpointPath = new System.Drawing.Drawing2D.GraphicsPath();
            }

            using (var primaryPen = new Pen(Color.Red, 1.5f))
            using (var guidePen = new Pen(Color.FromArgb(180, Color.DeepSkyBlue), 1f))
            using (var endpointBrush = new SolidBrush(Color.Red))
            {
                try
                {
                    if (rebuildRenderPaths)
                    {
                        if (string.Equals(mode, "Parallel", StringComparison.Ordinal) && hasSecond)
                        {
                            int renderCount = Math.Max(2, lineCount);
                            int renderStride = GetObjectDetectionMeasurementRenderStride(
                                first,
                                second,
                                renderCount,
                                e.Zoom);
                            for (int index = 0; index < renderCount; index++)
                            {
                                bool isBoundaryLine = index == 0 || index == renderCount - 1;
                                if (!isBoundaryLine && index % renderStride != 0)
                                {
                                    continue;
                                }

                                System.Drawing.Drawing2D.GraphicsPath linePath =
                                    isBoundaryLine
                                        ? primaryPath
                                        : guidePath;
                                if (needsMeasuredLines)
                                {
                                    if (index >= objectDetectionMeasurementClippedLines.Count ||
                                        objectDetectionMeasurementClippedLineSegments[index].Count == 0)
                                    {
                                        continue;
                                    }

                                    foreach (ObjectDetectionImageLine segment in
                                        objectDetectionMeasurementClippedLineSegments[index])
                                    {
                                        AddObjectDetectionImageLine(
                                            linePath,
                                            segment,
                                            e.Zoom,
                                            PointF.Empty);
                                        if (isBoundaryLine)
                                        {
                                            AddObjectDetectionImageLineEndpoints(
                                                endpointPath,
                                                segment,
                                                e.Zoom,
                                                PointF.Empty);
                                        }
                                    }

                                    continue;
                                }

                                double ratio = index / (double)(renderCount - 1);
                                ObjectDetectionImageLine line = InterpolateObjectDetectionImageLine(
                                    first,
                                    second,
                                    ratio);
                                AddObjectDetectionImageLine(
                                    linePath,
                                    line,
                                    e.Zoom,
                                    PointF.Empty);
                                if (isBoundaryLine)
                                {
                                    AddObjectDetectionImageLineEndpoints(
                                        endpointPath,
                                        line,
                                        e.Zoom,
                                        PointF.Empty);
                                }
                            }
                        }
                        else if (needsMeasuredLines)
                        {
                            foreach (ObjectDetectionImageLine segment in
                                objectDetectionMeasurementClippedLineSegments[0])
                            {
                                AddObjectDetectionImageLine(
                                    primaryPath,
                                    segment,
                                    e.Zoom,
                                    PointF.Empty);
                                AddObjectDetectionImageLineEndpoints(
                                    endpointPath,
                                    segment,
                                    e.Zoom,
                                    PointF.Empty);
                            }
                        }
                        else
                        {
                            AddObjectDetectionImageLine(
                                primaryPath,
                                first,
                                e.Zoom,
                                PointF.Empty);
                            AddObjectDetectionImageLineEndpoints(
                                endpointPath,
                                first,
                                e.Zoom,
                                PointF.Empty);
                        }

                        if (cacheRenderPaths)
                        {
                            objectDetectionMeasurementRenderOverlayCacheKey =
                                renderPathCacheKey;
                        }
                    }

                    System.Drawing.Drawing2D.GraphicsState graphicsState =
                        e.Graphics.Save();
                    try
                    {
                        e.Graphics.TranslateTransform(e.Offset.X, e.Offset.Y);
                        if (primaryPath.PointCount > 0)
                        {
                            e.Graphics.DrawPath(primaryPen, primaryPath);
                        }
                        if (guidePath.PointCount > 0)
                        {
                            e.Graphics.DrawPath(guidePen, guidePath);
                        }
                        if (endpointPath.PointCount > 0)
                        {
                            e.Graphics.FillPath(endpointBrush, endpointPath);
                        }
                    }
                    finally
                    {
                        e.Graphics.Restore(graphicsState);
                    }
                }
                finally
                {
                    if (!cacheRenderPaths)
                    {
                        primaryPath.Dispose();
                        guidePath.Dispose();
                        endpointPath.Dispose();
                    }
                }
            }

            DrawObjectDetectionMeasurementResultHighlights(
                e.Graphics,
                e,
                selectedObject);
        }

        private void DrawObjectDetectionResultReviewMeasurementOverlay(
            Graphics graphics,
            float zoom,
            PointF offset)
        {
            ResultReviewMeasurementRowContext context =
                objectDetectionResultReviewSelectedMeasurement;
            if (graphics == null || context == null || context.Record == null ||
                context.DetectedObject == null)
            {
                return;
            }

            ObjectDetectionImageLine first = CreateObjectDetectionImageLine(
                context.DetectedObject,
                context.Record,
                false);
            bool parallel = string.Equals(
                context.Record.Mode,
                "Parallel",
                StringComparison.OrdinalIgnoreCase);
            ObjectDetectionImageLine second = parallel
                ? CreateObjectDetectionImageLine(context.DetectedObject, context.Record, true)
                : first;
            bool clipLinesToMask = objectDetectionResultReviewClipLinesToMaskCheckBox != null &&
                objectDetectionResultReviewClipLinesToMaskCheckBox.Checked &&
                context.Statistics != null &&
                context.Statistics.MeasuredLineSegments != null;

            using (var primaryPen = new Pen(Color.Red, 1.5f))
            using (var guidePen = new Pen(Color.FromArgb(180, Color.DeepSkyBlue), 1f))
            using (var endpointBrush = new SolidBrush(Color.Red))
            {
                if (parallel)
                {
                    int renderCount = Math.Max(2, Math.Min(1000, context.Record.LineCount));
                    int renderStride = GetObjectDetectionMeasurementRenderStride(
                        first,
                        second,
                        renderCount,
                        zoom);
                    for (int index = 0; index < renderCount; index++)
                    {
                        bool boundary = index == 0 || index == renderCount - 1;
                        if (!boundary && index % renderStride != 0)
                        {
                            continue;
                        }

                        ObjectDetectionImageLine line = InterpolateObjectDetectionImageLine(
                            first,
                            second,
                            index / (double)(renderCount - 1));
                        Pen linePen = boundary ? primaryPen : guidePen;
                        if (clipLinesToMask)
                        {
                            if (index < context.Statistics.MeasuredLineSegments.Count)
                            {
                                foreach (ObjectDetectionImageLine segment in
                                    context.Statistics.MeasuredLineSegments[index])
                                {
                                    DrawObjectDetectionImageLine(
                                        graphics, segment, linePen, zoom, offset);
                                    if (boundary)
                                    {
                                        DrawObjectDetectionImageLineEndpoints(
                                            graphics, segment, endpointBrush, zoom, offset);
                                    }
                                }
                            }
                        }
                        else
                        {
                            DrawObjectDetectionImageLine(
                                graphics, line, linePen, zoom, offset);
                            if (boundary)
                            {
                                DrawObjectDetectionImageLineEndpoints(
                                    graphics, line, endpointBrush, zoom, offset);
                            }
                        }
                    }
                }
                else
                {
                    if (clipLinesToMask && context.Statistics.MeasuredLineSegments.Count > 0)
                    {
                        foreach (ObjectDetectionImageLine segment in
                            context.Statistics.MeasuredLineSegments[0])
                        {
                            DrawObjectDetectionImageLine(
                                graphics, segment, primaryPen, zoom, offset);
                            DrawObjectDetectionImageLineEndpoints(
                                graphics, segment, endpointBrush, zoom, offset);
                        }
                    }
                    else if (!clipLinesToMask)
                    {
                        DrawObjectDetectionImageLine(graphics, first, primaryPen, zoom, offset);
                        DrawObjectDetectionImageLineEndpoints(
                            graphics, first, endpointBrush, zoom, offset);
                    }
                }
            }

            if (context.Statistics == null || !context.StatisticsHighlightsVisible)
            {
                return;
            }

            ObjectDetectionImageLine minimum = context.Statistics.MinimumLine;
            ObjectDetectionImageLine maximum = context.Statistics.MaximumLine;
            bool sameSegment = minimum.X1 == maximum.X1 && minimum.Y1 == maximum.Y1 &&
                minimum.X2 == maximum.X2 && minimum.Y2 == maximum.Y2;
            using (var maximumPen = new Pen(Color.Red, sameSegment ? 5f : 3f))
            using (var minimumPen = new Pen(Color.Yellow, sameSegment ? 2f : 3f))
            {
                DrawObjectDetectionMeasurementImageResultLine(
                    graphics,
                    maximum,
                    maximumPen,
                    zoom,
                    offset);
                DrawObjectDetectionMeasurementImageResultLine(
                    graphics,
                    minimum,
                    minimumPen,
                    zoom,
                    offset);
            }
        }

        private static void DrawObjectDetectionMeasurementImageResultLine(
            Graphics graphics,
            ObjectDetectionImageLine line,
            Pen pen,
            float zoom,
            PointF offset)
        {
            if (line.X1 == line.X2 && line.Y1 == line.Y2)
            {
                float x = offset.X + line.X1 * zoom;
                float y = offset.Y + line.Y1 * zoom;
                using (var brush = new SolidBrush(pen.Color))
                {
                    graphics.FillEllipse(brush, x - 3f, y - 3f, 6f, 6f);
                }

                return;
            }

            DrawObjectDetectionImageLine(graphics, line, pen, zoom, offset);
        }

        private string CreateObjectDetectionMeasurementRenderOverlayCacheKey(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record,
            ObjectDefinitionDetectedObject selectedObject,
            ObjectDetectionImageLine first,
            ObjectDetectionImageLine second,
            string mode,
            bool hasSecond,
            int lineCount,
            bool clipLinesToMask,
            float zoom)
        {
            return string.Join(
                "|",
                parameter == null ? string.Empty : parameter.Id ?? string.Empty,
                record == null ? string.Empty : record.Id ?? string.Empty,
                selectedObject.Number.ToString(CultureInfo.InvariantCulture),
                selectedObject.Bounds.X.ToString(CultureInfo.InvariantCulture),
                selectedObject.Bounds.Y.ToString(CultureInfo.InvariantCulture),
                selectedObject.Bounds.Width.ToString(CultureInfo.InvariantCulture),
                selectedObject.Bounds.Height.ToString(CultureInfo.InvariantCulture),
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                objectDefinitionResultGeneration.ToString(CultureInfo.InvariantCulture),
                mode ?? string.Empty,
                hasSecond ? "second-line" : "single-line",
                lineCount.ToString(CultureInfo.InvariantCulture),
                clipLinesToMask ? "clip-mask" : "no-mask-clip",
                clipLinesToMask
                    ? objectDetectionMeasurementClipCacheSignature ?? string.Empty
                    : string.Empty,
                first.X1.ToString(CultureInfo.InvariantCulture),
                first.Y1.ToString(CultureInfo.InvariantCulture),
                first.X2.ToString(CultureInfo.InvariantCulture),
                first.Y2.ToString(CultureInfo.InvariantCulture),
                second.X1.ToString(CultureInfo.InvariantCulture),
                second.Y1.ToString(CultureInfo.InvariantCulture),
                second.X2.ToString(CultureInfo.InvariantCulture),
                second.Y2.ToString(CultureInfo.InvariantCulture),
                zoom.ToString("R", CultureInfo.InvariantCulture));
        }

        private static int GetObjectDetectionMeasurementRenderStride(
            ObjectDetectionImageLine first,
            ObjectDetectionImageLine second,
            int lineCount,
            float zoom)
        {
            if (lineCount <= 2 || zoom <= 0f)
            {
                return 1;
            }

            double dx = second.X1 - first.X1;
            double dy = second.Y1 - first.Y1;
            double screenSpacing = Math.Sqrt((dx * dx) + (dy * dy)) * zoom / (lineCount - 1);
            if (screenSpacing <= 0d || double.IsNaN(screenSpacing) || double.IsInfinity(screenSpacing))
            {
                return 1;
            }

            const double targetScreenSpacing = 1.5d;
            return Math.Max(1, (int)Math.Ceiling(targetScreenSpacing / screenSpacing));
        }

        private void ClearObjectDetectionMeasurementRenderOverlayCache()
        {
            if (objectDetectionMeasurementRenderPrimaryPath != null)
            {
                objectDetectionMeasurementRenderPrimaryPath.Dispose();
                objectDetectionMeasurementRenderPrimaryPath = null;
            }

            if (objectDetectionMeasurementRenderGuidePath != null)
            {
                objectDetectionMeasurementRenderGuidePath.Dispose();
                objectDetectionMeasurementRenderGuidePath = null;
            }

            if (objectDetectionMeasurementRenderEndpointPath != null)
            {
                objectDetectionMeasurementRenderEndpointPath.Dispose();
                objectDetectionMeasurementRenderEndpointPath = null;
            }

            objectDetectionMeasurementRenderOverlayCacheKey = null;
        }

        private void DrawObjectDetectionMeasurementResultHighlights(
            Graphics graphics,
            ImageOverlayPaintEventArgs e,
            ObjectDefinitionDetectedObject selectedObject)
        {
            if (!objectDetectionMeasurementResultHighlightsVisible ||
                graphics == null ||
                e == null ||
                selectedObject == null ||
                !string.Equals(
                    objectDetectionMeasurementResultParameterId,
                    activeObjectDetectionParameterId,
                    StringComparison.Ordinal) ||
                objectDetectionMeasurementResultObjectNumber != selectedObject.Number ||
                objectDetectionMeasurementResultObjectBounds != selectedObject.Bounds)
            {
                return;
            }

            bool sameSegment =
                objectDetectionMeasurementMinimumResultLine.X1 == objectDetectionMeasurementMaximumResultLine.X1 &&
                objectDetectionMeasurementMinimumResultLine.Y1 == objectDetectionMeasurementMaximumResultLine.Y1 &&
                objectDetectionMeasurementMinimumResultLine.X2 == objectDetectionMeasurementMaximumResultLine.X2 &&
                objectDetectionMeasurementMinimumResultLine.Y2 == objectDetectionMeasurementMaximumResultLine.Y2;

            if (sameSegment)
            {
                using (var maximumPen = new Pen(Color.Red, 5f))
                using (var minimumPen = new Pen(Color.Yellow, 2f))
                {
                    DrawObjectDetectionMeasurementResultLine(
                        graphics,
                        objectDetectionMeasurementMaximumResultLine,
                        maximumPen,
                        e.Zoom,
                        e.Offset);
                    DrawObjectDetectionMeasurementResultLine(
                        graphics,
                        objectDetectionMeasurementMinimumResultLine,
                        minimumPen,
                        e.Zoom,
                        e.Offset);
                }
            }
            else
            {
                using (var minimumPen = new Pen(Color.Yellow, 3f))
                using (var maximumPen = new Pen(Color.Red, 3f))
                {
                    DrawObjectDetectionMeasurementResultLine(
                        graphics,
                        objectDetectionMeasurementMinimumResultLine,
                        minimumPen,
                        e.Zoom,
                        e.Offset);
                    DrawObjectDetectionMeasurementResultLine(
                        graphics,
                        objectDetectionMeasurementMaximumResultLine,
                        maximumPen,
                        e.Zoom,
                        e.Offset);
                }
            }
        }

        private static void DrawObjectDetectionMeasurementResultLine(
            Graphics graphics,
            ObjectDetectionImageLine line,
            Pen pen,
            float zoom,
            PointF offset)
        {
            if (line.X1 == line.X2 && line.Y1 == line.Y2)
            {
                float x = offset.X + (line.X1 * zoom);
                float y = offset.Y + (line.Y1 * zoom);
                using (var brush = new SolidBrush(pen.Color))
                {
                    graphics.FillEllipse(brush, x - 3f, y - 3f, 6f, 6f);
                }

                return;
            }

            DrawObjectDetectionImageLine(graphics, line, pen, zoom, offset);
        }

        private static void DrawObjectDetectionImageLine(
            Graphics graphics,
            ObjectDetectionImageLine line,
            Pen pen,
            float zoom,
            PointF offset)
        {
            graphics.DrawLine(
                pen,
                offset.X + (line.X1 * zoom),
                offset.Y + (line.Y1 * zoom),
                offset.X + (line.X2 * zoom),
                offset.Y + (line.Y2 * zoom));
        }

        private static void DrawObjectDetectionImageLineEndpoints(
            Graphics graphics,
            ObjectDetectionImageLine line,
            Brush brush,
            float zoom,
            PointF offset)
        {
            float radius = 3f;
            graphics.FillEllipse(
                brush,
                offset.X + (line.X1 * zoom) - radius,
                offset.Y + (line.Y1 * zoom) - radius,
                radius * 2,
                radius * 2);
            graphics.FillEllipse(
                brush,
                offset.X + (line.X2 * zoom) - radius,
                offset.Y + (line.Y2 * zoom) - radius,
                radius * 2,
                radius * 2);
        }

        private static void AddObjectDetectionImageLine(
            System.Drawing.Drawing2D.GraphicsPath path,
            ObjectDetectionImageLine line,
            float zoom,
            PointF offset)
        {
            if (path == null)
            {
                return;
            }

            path.StartFigure();
            path.AddLine(
                offset.X + (line.X1 * zoom),
                offset.Y + (line.Y1 * zoom),
                offset.X + (line.X2 * zoom),
                offset.Y + (line.Y2 * zoom));
        }

        private static void AddObjectDetectionImageLineEndpoints(
            System.Drawing.Drawing2D.GraphicsPath path,
            ObjectDetectionImageLine line,
            float zoom,
            PointF offset)
        {
            if (path == null)
            {
                return;
            }

            const float radius = 3f;
            path.AddEllipse(
                offset.X + (line.X1 * zoom) - radius,
                offset.Y + (line.Y1 * zoom) - radius,
                radius * 2,
                radius * 2);
            path.AddEllipse(
                offset.X + (line.X2 * zoom) - radius,
                offset.Y + (line.Y2 * zoom) - radius,
                radius * 2,
                radius * 2);
        }

        private bool TryGetSelectedObjectDetectionObject(out ObjectDefinitionDetectedObject detectedObject)
        {
            detectedObject = null;
            if (selectedObjectDetectionNumber <= 0 ||
                string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return false;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDefinitionSettings definition = parameter == null ||
                string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null ||
                !TryGetCompletedObjectDefinitionObject(
                    definition,
                    selectedObjectDetectionNumber,
                    out detectedObject) ||
                detectedObject.Bounds.Width <= 0 ||
                detectedObject.Bounds.Height <= 0)
            {
                detectedObject = null;
                return false;
            }

            return true;
        }

        private bool TryGetSelectedObjectDetectionBounds(out Rectangle objectBounds)
        {
            objectBounds = Rectangle.Empty;
            ObjectDefinitionDetectedObject detectedObject;
            if (!TryGetSelectedObjectDetectionObject(out detectedObject))
            {
                return false;
            }

            objectBounds = detectedObject.Bounds;
            return true;
        }

        private static void DrawObjectDetectionObjectOutline(
            Graphics graphics,
            ObjectDefinitionDetectedObject detectedObject,
            Pen outline,
            float zoom,
            PointF offset)
        {
            if (graphics == null || detectedObject == null || outline == null)
            {
                return;
            }

            if (detectedObject.HasRotationGeometry &&
                detectedObject.RotationCorners != null &&
                detectedObject.RotationCorners.Length == 4)
            {
                PointF[] screenCorners = detectedObject.RotationCorners
                    .Select(point => new PointF(
                        offset.X + point.X * zoom,
                        offset.Y + point.Y * zoom))
                    .ToArray();
                graphics.DrawPolygon(outline, screenCorners);
                return;
            }

            Rectangle bounds = detectedObject.Bounds;
            graphics.DrawRectangle(
                outline,
                offset.X + bounds.X * zoom,
                offset.Y + bounds.Y * zoom,
                Math.Max(1f, bounds.Width * zoom),
                Math.Max(1f, bounds.Height * zoom));
        }

        private void RefreshObjectDetectionMeasurementDisplay()
        {
            if (objectDetectionMeasurementDisplayControl == null ||
                !isObjectDetectionParameterImageLayout ||
                rightOriginalDisplayControl == null ||
                !rightOriginalDisplayControl.HasImage)
            {
                return;
            }

            if (isObjectDetectionResultReviewMode)
            {
                RefreshObjectDetectionResultReviewMeasurementDisplay();
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            bool hasSelectedObject = TryGetSelectedObjectDetectionObject(out selectedObject);
            Rectangle objectBounds = hasSelectedObject
                ? selectedObject.Bounds
                : Rectangle.Empty;
            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                LargeImageSource source =
                    rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (source == null)
                {
                    return;
                }

                try
                {
                    objectDetectionMeasurementDisplayControl.SetSharedLargeImageSource(
                        source,
                        true);
                }
                finally
                {
                    source.ReleaseReference();
                }

                objectDetectionMeasurementDisplayControl.InvalidateImageView();
                return;
            }

            Bitmap measurementImage = rightOriginalDisplayControl.CloneImage();
            if (measurementImage == null)
            {
                measurementImage = leftOriginalDisplayControl == null
                    ? null
                    : leftOriginalDisplayControl.CloneImage();
            }

            if (measurementImage == null)
            {
                return;
            }

            try
            {
                if (hasSelectedObject)
                {
                    ObjectDetectionParameterSettings parameter =
                        FindObjectDetectionParameter(activeObjectDetectionParameterId);
                    Cv.Mat measurementMask;
                    if (parameter != null &&
                        TryGetObjectDetectionMeasurementMask(
                            parameter,
                            objectBounds,
                            null,
                            measurementImage,
                            out measurementMask))
                    {
                        using (measurementMask)
                        using (Bitmap maskOverlay = CreateObjectDetectionMaskOverlay(
                            measurementMask,
                            ObjectDetectionMeasurementMaskColor))
                        using (Graphics graphics = Graphics.FromImage(measurementImage))
                        {
                            graphics.DrawImageUnscaled(maskOverlay, objectBounds.Location);
                        }
                    }

                    Rectangle imageBounds = new Rectangle(
                        0,
                        0,
                        measurementImage.Width,
                        measurementImage.Height);
                    Rectangle visibleBounds = Rectangle.Intersect(
                        objectBounds,
                        imageBounds);
                    if (visibleBounds.Width > 0 && visibleBounds.Height > 0)
                    {
                        using (Graphics graphics = Graphics.FromImage(measurementImage))
                        using (var outline = new Pen(Color.LimeGreen, 3f))
                        {
                            DrawObjectDetectionObjectOutline(
                                graphics,
                                selectedObject,
                                outline,
                                1f,
                                PointF.Empty);
                        }
                    }
                }

                objectDetectionMeasurementDisplayControl.SetDisplayImage(
                    measurementImage,
                    true);
                measurementImage = null;
            }
            finally
            {
                if (measurementImage != null)
                {
                    measurementImage.Dispose();
                }
            }
        }

        private void RefreshObjectDetectionResultReviewMeasurementDisplay()
        {
            if (objectDetectionMeasurementDisplayControl == null ||
                rightOriginalDisplayControl == null ||
                !rightOriginalDisplayControl.HasImage)
            {
                return;
            }

            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                LargeImageSource source = rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (source == null)
                {
                    return;
                }

                try
                {
                    objectDetectionMeasurementDisplayControl.SetSharedLargeImageSource(source, true);
                }
                finally
                {
                    source.ReleaseReference();
                }

                objectDetectionMeasurementDisplayControl.InvalidateImageView();
                return;
            }

            Bitmap image = rightOriginalDisplayControl.CloneImage();
            if (image == null)
            {
                return;
            }

            try
            {
                ResultReviewMeasurementRowContext context =
                    objectDetectionResultReviewSelectedMeasurement;
                if (context != null && context.DetectedObject != null)
                {
                    Rectangle objectBounds = context.DetectedObject.Bounds;
                    Cv.Mat mask;
                    if (context.MaskParameter != null &&
                        TryGetObjectDetectionMeasurementMask(
                            context.MaskParameter,
                            context.ObjectNumber,
                            objectBounds,
                            null,
                            image,
                            out mask,
                            true))
                    {
                        using (mask)
                        using (Bitmap overlay = CreateObjectDetectionMaskOverlay(
                            mask,
                            ObjectDetectionMeasurementMaskColor))
                        using (Graphics graphics = Graphics.FromImage(image))
                        {
                            graphics.DrawImageUnscaled(overlay, objectBounds.Location);
                        }
                    }

                    Rectangle visibleBounds = Rectangle.Intersect(
                        objectBounds,
                        new Rectangle(0, 0, image.Width, image.Height));
                    if (visibleBounds.Width > 0 && visibleBounds.Height > 0)
                    {
                        using (Graphics graphics = Graphics.FromImage(image))
                        using (var outline = new Pen(Color.LimeGreen, 3f))
                        {
                            DrawObjectDetectionObjectOutline(
                                graphics,
                                context.DetectedObject,
                                outline,
                                1f,
                                PointF.Empty);
                        }
                    }
                }

                objectDetectionMeasurementDisplayControl.SetDisplayImage(image, true);
                image = null;
            }
            finally
            {
                if (image != null)
                {
                    image.Dispose();
                }
            }
        }


    }
}
