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
        private void ClearObjectDetectionMeasurementClipCache()
        {
            objectDetectionMeasurementClipCacheParameterId = null;
            objectDetectionMeasurementClipCacheRecordId = null;
            objectDetectionMeasurementClipCacheSignature = null;
            objectDetectionMeasurementClipCacheObjectNumber = -1;
            objectDetectionMeasurementClipCacheGeneration = -1;
            objectDetectionMeasurementClipCacheObjectBounds = Rectangle.Empty;
            objectDetectionMeasurementClippedLines = null;
            objectDetectionMeasurementClippedLineLengths = null;
            objectDetectionMeasurementClippedLineSegments = null;
        }

        private bool IsObjectDetectionMeasurementClipCacheValid(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record,
            ObjectDefinitionDetectedObject selectedObject)
        {
            return parameter != null && record != null && selectedObject != null &&
                objectDetectionMeasurementClippedLines != null &&
                objectDetectionMeasurementClippedLineLengths != null &&
                objectDetectionMeasurementClippedLineSegments != null &&
                objectDetectionMeasurementClippedLines.Count ==
                    objectDetectionMeasurementClippedLineLengths.Count &&
                objectDetectionMeasurementClippedLines.Count ==
                    objectDetectionMeasurementClippedLineSegments.Count &&
                objectDetectionMeasurementClippedLines.Count ==
                    (string.Equals(record.Mode, "Parallel", StringComparison.Ordinal)
                        ? Math.Max(2, record.LineCount)
                        : 1) &&
                string.Equals(
                    objectDetectionMeasurementClipCacheParameterId,
                    parameter.Id,
                    StringComparison.Ordinal) &&
                string.Equals(
                    objectDetectionMeasurementClipCacheRecordId,
                    record.Id,
                    StringComparison.Ordinal) &&
                string.Equals(
                    objectDetectionMeasurementClipCacheSignature,
                    GetObjectDetectionMeasurementClipCacheSignature(record),
                    StringComparison.Ordinal) &&
                objectDetectionMeasurementClipCacheObjectNumber == selectedObject.Number &&
                objectDetectionMeasurementClipCacheObjectBounds == selectedObject.Bounds &&
                objectDetectionMeasurementClipCacheGeneration == objectDefinitionResultGeneration;
        }

        private void StoreObjectDetectionMeasurementClipCache(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record,
            ObjectDetectionMeasurementStatistics statistics)
        {
            if (parameter == null || record == null || statistics == null)
            {
                ClearObjectDetectionMeasurementClipCache();
                return;
            }

            objectDetectionMeasurementClipCacheParameterId = parameter.Id;
            objectDetectionMeasurementClipCacheRecordId = record.Id;
            objectDetectionMeasurementClipCacheSignature =
                GetObjectDetectionMeasurementClipCacheSignature(record);
            objectDetectionMeasurementClipCacheObjectNumber = statistics.ObjectNumber;
            objectDetectionMeasurementClipCacheObjectBounds = statistics.ObjectBounds;
            objectDetectionMeasurementClipCacheGeneration = objectDefinitionResultGeneration;
            objectDetectionMeasurementClippedLines =
                new List<ObjectDetectionImageLine>(statistics.MeasuredLines);
            objectDetectionMeasurementClippedLineLengths =
                new List<double>(statistics.MeasuredLineLengths);
            objectDetectionMeasurementClippedLineSegments = statistics.MeasuredLineSegments
                .Select(segments => new List<ObjectDetectionImageLine>(segments))
                .ToList();
        }

        private static string GetObjectDetectionMeasurementClipCacheSignature(
            ObjectDetectionMeasurementRecordSettings record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                new[]
                {
                    record.Mode ?? string.Empty,
                    record.LineCount.ToString(CultureInfo.InvariantCulture),
                    record.LengthMode ?? string.Empty,
                    record.StartX.ToString("R", CultureInfo.InvariantCulture),
                    record.StartY.ToString("R", CultureInfo.InvariantCulture),
                    record.EndX.ToString("R", CultureInfo.InvariantCulture),
                    record.EndY.ToString("R", CultureInfo.InvariantCulture),
                    record.SecondStartX.ToString("R", CultureInfo.InvariantCulture),
                    record.SecondStartY.ToString("R", CultureInfo.InvariantCulture),
                    record.SecondEndX.ToString("R", CultureInfo.InvariantCulture),
                    record.SecondEndY.ToString("R", CultureInfo.InvariantCulture),
                    record.SourceMaskMode ?? string.Empty,
                    record.SourceMaskPrimaryType ?? string.Empty,
                    record.SourceMaskPrimaryId ?? string.Empty,
                    record.SourceMaskPrimaryNamespace ?? string.Empty,
                    record.SourceMaskOperation ?? string.Empty,
                    record.SourceMaskSecondaryType ?? string.Empty,
                    record.SourceMaskSecondaryId ?? string.Empty,
                    record.SourceMaskSecondaryNamespace ?? string.Empty
                });
        }

        private void PrepareObjectDetectionMeasurementClipLines(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record)
        {
            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                ClearObjectDetectionMeasurementClipCache();
                statusLabel.Text = "請先選擇物件序號，再依 MASK 裁切量測線";
                return;
            }

            if (IsObjectDetectionMeasurementClipCacheValid(parameter, record, selectedObject))
            {
                return;
            }

            ObjectDetectionMeasurementStatistics statistics;
            string errorMessage;
            if (!TryCalculateObjectDetectionMeasurementRecord(
                parameter,
                record,
                out statistics,
                out errorMessage))
            {
                ClearObjectDetectionMeasurementClipCache();
                statusLabel.Text = "量測線 MASK 裁切失敗：" + errorMessage;
                return;
            }

            StoreObjectDetectionMeasurementClipCache(parameter, record, statistics);
            statusLabel.Text = parameter.DisplayName +
                " 已依 MASK 裁切量測線";
        }

        private void ObjectDetectionMeasurementClipLinesCheckBox_CheckedChanged(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionMeasurementClipLinesCheckBox == null ||
                parameter == null)
            {
                return;
            }

            parameter.MeasurementClipLinesToMask =
                objectDetectionMeasurementClipLinesCheckBox.Checked;
            SaveSystemParameters();
            if (parameter.MeasurementClipLinesToMask)
            {
                ObjectDetectionMeasurementRecordSettings record =
                    GetActiveObjectDetectionMeasurementRecord(parameter);
                if (record == null)
                {
                    statusLabel.Text = "請先套用並選取量測紀錄，再依 MASK 裁切量測線";
                }
                else
                {
                    objectDetectionMeasurementAppliedRecordId = record.Id;
                    PrepareObjectDetectionMeasurementClipLines(parameter, record);
                }
            }
            else
            {
                statusLabel.Text = parameter.DisplayName + " 已恢復顯示完整量測線";
            }

            if (objectDetectionMeasurementDisplayControl != null)
            {
                objectDetectionMeasurementDisplayControl.InvalidateImageView();
            }
        }

        private void CalculateObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            string recordId)
        {
            if (parameter == null || parameter.MeasurementRecords == null)
            {
                return;
            }

            ObjectDetectionMeasurementRecordSettings record =
                parameter.MeasurementRecords.FirstOrDefault(
                    item => item != null && string.Equals(
                        item.Id,
                        recordId,
                        StringComparison.Ordinal));
            if (record == null)
            {
                return;
            }

            ObjectDetectionMeasurementStatistics statistics;
            string errorMessage;
            if (!TryCalculateObjectDetectionMeasurementRecord(
                    parameter,
                    record,
                    out statistics,
                    out errorMessage))
            {
                statusLabel.Text = "量測運算失敗：" + errorMessage;
                return;
            }

            StoreObjectDetectionMeasurementClipCache(parameter, record, statistics);
            objectDetectionMeasurementMinimumResultLine = statistics.MinimumLine;
            objectDetectionMeasurementMaximumResultLine = statistics.MaximumLine;
            objectDetectionMeasurementResultParameterId = parameter.Id;
            objectDetectionMeasurementResultObjectNumber = statistics.ObjectNumber;
            objectDetectionMeasurementResultObjectBounds = statistics.ObjectBounds;
            objectDetectionMeasurementResultHighlightsVisible = true;
            if (objectDetectionMeasurementResultHighlightTimer != null)
            {
                objectDetectionMeasurementResultHighlightTimer.Stop();
                objectDetectionMeasurementResultHighlightTimer.Start();
            }
            if (objectDetectionMeasurementDisplayControl != null)
            {
                objectDetectionMeasurementDisplayControl.InvalidateImageView();
            }

            string numberFormat = string.Equals(statistics.Unit, "mm", StringComparison.Ordinal)
                ? "0.####"
                : "0.##";
            string unit = statistics.Unit ?? "px";
            string text =
                "最小：" + statistics.Minimum.ToString(numberFormat, CultureInfo.InvariantCulture) + " " + unit + "\r\n" +
                "平均：" + statistics.Average.ToString(numberFormat, CultureInfo.InvariantCulture) + " " + unit + "\r\n" +
                "最大：" + statistics.Maximum.ToString(numberFormat, CultureInfo.InvariantCulture) + " " + unit;
            if (objectAreaToolTip != null)
            {
                Point cursor = Cursor.Position;
                Point tooltipLocation = PointToClient(
                    new Point(cursor.X + 14, cursor.Y + 14));
                objectAreaToolTip.Show(text, this, tooltipLocation, 10000);
            }

            statusLabel.Text = parameter.DisplayName +
                " 已完成量測運算：最小 " +
                statistics.Minimum.ToString(numberFormat, CultureInfo.InvariantCulture) +
                " " + unit + "，平均 " +
                statistics.Average.ToString(numberFormat, CultureInfo.InvariantCulture) +
                " " + unit + "，最大 " +
                statistics.Maximum.ToString(numberFormat, CultureInfo.InvariantCulture) + " " + unit;
        }

        private bool TryCalculateObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record,
            out ObjectDetectionMeasurementStatistics statistics,
            out string errorMessage)
        {
            statistics = null;
            errorMessage = string.Empty;
            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                errorMessage = "請先選擇已找到的物件序號";
                return false;
            }

            Rectangle objectBounds = selectedObject.Bounds;
            if (objectBounds.Width <= 0 || objectBounds.Height <= 0)
            {
                errorMessage = "目前物件沒有有效 ROI";
                return false;
            }

            Cv.Mat mask = null;
            LargeImageSource largeSource = null;
            Bitmap original = null;
            try
            {
                if (rightOriginalDisplayControl == null ||
                    !rightOriginalDisplayControl.HasImage)
                {
                    errorMessage = "尚未載入原始影像";
                    return false;
                }

                if (rightOriginalDisplayControl.IsLargeImageMode)
                {
                    largeSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                    if (largeSource == null ||
                        !TryGetObjectDetectionMeasurementMask(
                            parameter,
                            objectBounds,
                            largeSource,
                            null,
                            out mask))
                    {
                        errorMessage = "無法取得量測來源 MASK";
                        return false;
                    }
                }
                else
                {
                    original = rightOriginalDisplayControl.CloneImage();
                    if (original == null ||
                        !TryGetObjectDetectionMeasurementMask(
                            parameter,
                            objectBounds,
                            null,
                            original,
                            out mask))
                    {
                        errorMessage = "無法取得量測來源 MASK";
                        return false;
                    }
                }

                if (mask == null || mask.Empty())
                {
                    errorMessage = "量測來源 MASK 為空";
                    return false;
                }

                Cv.Mat normalizedMask = null;
                Cv.Mat sampleMask = mask;
                try
                {
                    if (mask.Type() != Cv.MatType.CV_8UC1)
                    {
                        normalizedMask = new Cv.Mat();
                        mask.ConvertTo(normalizedMask, Cv.MatType.CV_8UC1);
                        sampleMask = normalizedMask;
                    }

                    ObjectDetectionImageLine first =
                        CreateObjectDetectionImageLine(selectedObject, record, false);
                    ObjectDetectionImageLine second =
                        CreateObjectDetectionImageLine(selectedObject, record, true);
                    bool parallel = string.Equals(record.Mode, "Parallel", StringComparison.Ordinal);
                    if (parallel &&
                        (Math.Abs(second.X2 - second.X1) + Math.Abs(second.Y2 - second.Y1) < 1))
                    {
                        errorMessage = "平行量測紀錄缺少第二條線";
                        return false;
                    }

                    int lineCount = parallel ? Math.Max(2, record.LineCount) : 1;
                    var lengths = new List<double>(lineCount);
                    var measuredLines = new List<ObjectDetectionImageLine>(lineCount);
                    var measuredLineSegments =
                        new List<List<ObjectDetectionImageLine>>(lineCount);
                    double minimum = double.PositiveInfinity;
                    double maximum = double.NegativeInfinity;
                    bool useMillimeters = IsObjectDetectionCameraPrecisionEnabled(parameter);
                    ObjectDetectionImageLine minimumLine = new ObjectDetectionImageLine();
                    ObjectDetectionImageLine maximumLine = new ObjectDetectionImageLine();
                    for (int index = 0; index < lineCount; index++)
                    {
                        double ratio = lineCount == 1
                            ? 0
                            : index / (double)(lineCount - 1);
                        ObjectDetectionImageLine line = parallel
                            ? InterpolateObjectDetectionImageLine(first, second, ratio)
                            : first;
                        ObjectDetectionImageLine measuredSegment;
                        List<ObjectDetectionImageLine> visibleSegments;
                        double length = MeasureObjectDetectionLength(
                            sampleMask,
                            objectBounds,
                            line,
                            record.LengthMode,
                            out measuredSegment,
                            out visibleSegments);
                        if (useMillimeters)
                        {
                            length = ConvertObjectDetectionPixelsToMillimeters(
                                length,
                                line,
                                parameter.CameraXMillimetersPerPixel,
                                parameter.CameraYMillimetersPerPixel);
                        }

                        lengths.Add(length);
                        measuredLines.Add(measuredSegment);
                        measuredLineSegments.Add(visibleSegments);
                        if (length < minimum)
                        {
                            minimum = length;
                            minimumLine = measuredSegment;
                        }

                        if (length > maximum)
                        {
                            maximum = length;
                            maximumLine = measuredSegment;
                        }
                    }

                    if (lengths.Count == 0)
                    {
                        errorMessage = "沒有可量測的線段";
                        return false;
                    }

                    statistics = new ObjectDetectionMeasurementStatistics
                    {
                        Minimum = lengths.Min(),
                        Average = lengths.Average(),
                        Maximum = lengths.Max(),
                        Unit = useMillimeters ? "mm" : "px",
                        MinimumLine = minimumLine,
                        MaximumLine = maximumLine,
                        MeasuredLines = measuredLines,
                        MeasuredLineLengths = lengths,
                        MeasuredLineSegments = measuredLineSegments,
                        ObjectNumber = selectedObject.Number,
                        ObjectBounds = objectBounds
                    };
                    return true;
                }
                finally
                {
                    if (normalizedMask != null)
                    {
                        normalizedMask.Dispose();
                    }
                }
            }
            finally
            {
                if (mask != null)
                {
                    mask.Dispose();
                }

                if (original != null)
                {
                    original.Dispose();
                }

                if (largeSource != null)
                {
                    largeSource.ReleaseReference();
                }
            }
        }

        private static double MeasureFirstContinuousObjectLength(
            Cv.Mat mask,
            Rectangle objectBounds,
            ObjectDetectionImageLine line,
            out ObjectDetectionImageLine measuredSegment,
            out List<ObjectDetectionImageLine> visibleSegments)
        {
            measuredSegment = new ObjectDetectionImageLine(line.X1, line.Y1, line.X1, line.Y1);
            visibleSegments = new List<ObjectDetectionImageLine>(1);
            if (mask == null || mask.Empty())
            {
                return 0;
            }

            double startX = line.X1 - objectBounds.X;
            double startY = line.Y1 - objectBounds.Y;
            double endX = line.X2 - objectBounds.X;
            double endY = line.Y2 - objectBounds.Y;
            double deltaX = endX - startX;
            double deltaY = endY - startY;
            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            int sampleCount = Math.Max(1, (int)Math.Ceiling(distance));
            double sampleSpacing = distance / sampleCount;
            bool started = false;
            int runStart = 0;
            int runEnd = 0;

            for (int index = 0; index <= sampleCount; index++)
            {
                double ratio = index / (double)sampleCount;
                int x = (int)Math.Round(startX + (deltaX * ratio));
                int y = (int)Math.Round(startY + (deltaY * ratio));
                bool foreground = x >= 0 && y >= 0 &&
                    x < mask.Cols && y < mask.Rows &&
                    mask.At<byte>(y, x) != 0;

                if (!started)
                {
                    if (foreground)
                    {
                        started = true;
                        runStart = index;
                        runEnd = index;
                    }

                    continue;
                }

                if (!foreground)
                {
                    break;
                }

                runEnd = index;
            }

            if (!started)
            {
                return 0;
            }

            measuredSegment = CreateObjectDetectionMeasuredSegment(
                objectBounds,
                startX,
                startY,
                deltaX,
                deltaY,
                runStart,
                runEnd,
                sampleCount);
            visibleSegments.Add(measuredSegment);
            return (runEnd - runStart + 1) * sampleSpacing;
        }

        private static double MeasureObjectDetectionLength(
            Cv.Mat mask,
            Rectangle objectBounds,
            ObjectDetectionImageLine line,
            string lengthMode,
            out ObjectDetectionImageLine measuredSegment,
            out List<ObjectDetectionImageLine> visibleSegments)
        {
            if (string.Equals(
                NormalizeObjectDetectionMeasurementLengthMode(lengthMode),
                "IgnoreGaps",
                StringComparison.Ordinal))
            {
                return MeasureObjectDetectionLengthIgnoringGaps(
                    mask,
                    objectBounds,
                    line,
                    out measuredSegment,
                    out visibleSegments);
            }

            return MeasureFirstContinuousObjectLength(
                mask,
                objectBounds,
                line,
                out measuredSegment,
                out visibleSegments);
        }

        private static bool IsObjectDetectionCameraPrecisionEnabled(
            ObjectDetectionParameterSettings parameter)
        {
            return parameter != null && parameter.CameraPrecisionConfigured &&
                parameter.CameraXMillimetersPerPixel > 0.0 &&
                parameter.CameraYMillimetersPerPixel > 0.0 &&
                !double.IsNaN(parameter.CameraXMillimetersPerPixel) &&
                !double.IsNaN(parameter.CameraYMillimetersPerPixel) &&
                !double.IsInfinity(parameter.CameraXMillimetersPerPixel) &&
                !double.IsInfinity(parameter.CameraYMillimetersPerPixel);
        }

        private static double ConvertObjectDetectionPixelsToMillimeters(
            double pixelLength,
            ObjectDetectionImageLine line,
            double xMillimetersPerPixel,
            double yMillimetersPerPixel)
        {
            double deltaX = line.X2 - line.X1;
            double deltaY = line.Y2 - line.Y1;
            double pixelDistance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (pixelLength <= 0.0 || pixelDistance <= 0.0)
            {
                return 0.0;
            }

            double unitX = deltaX / pixelDistance;
            double unitY = deltaY / pixelDistance;
            double millimetersPerPixelAlongLine = Math.Sqrt(
                Math.Pow(unitX * xMillimetersPerPixel, 2) +
                Math.Pow(unitY * yMillimetersPerPixel, 2));
            return pixelLength * millimetersPerPixelAlongLine;
        }

        private static ObjectDetectionImageLine CreateObjectDetectionMeasuredSegment(
            Rectangle objectBounds,
            double startX,
            double startY,
            double deltaX,
            double deltaY,
            int firstIndex,
            int lastIndex,
            int sampleCount)
        {
            double firstRatio = firstIndex / (double)sampleCount;
            // Sample indices identify pixel centers; advance the terminal point
            // one sample so the displayed segment includes the final hit pixel.
            double lastRatio = (lastIndex + 1d) / sampleCount;
            return new ObjectDetectionImageLine(
                (int)Math.Round(objectBounds.X + startX + (deltaX * firstRatio)),
                (int)Math.Round(objectBounds.Y + startY + (deltaY * firstRatio)),
                (int)Math.Round(objectBounds.X + startX + (deltaX * lastRatio)),
                (int)Math.Round(objectBounds.Y + startY + (deltaY * lastRatio)));
        }

        private static double MeasureObjectDetectionLengthIgnoringGaps(
            Cv.Mat mask,
            Rectangle objectBounds,
            ObjectDetectionImageLine line,
            out ObjectDetectionImageLine measuredSegment,
            out List<ObjectDetectionImageLine> visibleSegments)
        {
            measuredSegment = new ObjectDetectionImageLine(line.X1, line.Y1, line.X1, line.Y1);
            visibleSegments = new List<ObjectDetectionImageLine>();
            if (mask == null || mask.Empty())
            {
                return 0;
            }

            double startX = line.X1 - objectBounds.X;
            double startY = line.Y1 - objectBounds.Y;
            double endX = line.X2 - objectBounds.X;
            double endY = line.Y2 - objectBounds.Y;
            double deltaX = endX - startX;
            double deltaY = endY - startY;
            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            int sampleCount = Math.Max(1, (int)Math.Ceiling(distance));
            double sampleSpacing = distance / sampleCount;
            int firstForegroundIndex = -1;
            int lastForegroundIndex = -1;
            int runStartIndex = -1;
            int runEndIndex = -1;

            for (int index = 0; index <= sampleCount; index++)
            {
                double ratio = index / (double)sampleCount;
                int x = (int)Math.Round(startX + (deltaX * ratio));
                int y = (int)Math.Round(startY + (deltaY * ratio));
                bool foreground = x >= 0 && y >= 0 &&
                    x < mask.Cols && y < mask.Rows &&
                    mask.At<byte>(y, x) != 0;
                if (foreground)
                {
                    if (runStartIndex < 0)
                    {
                        runStartIndex = index;
                    }

                    runEndIndex = index;
                    if (firstForegroundIndex < 0)
                    {
                        firstForegroundIndex = index;
                    }

                    lastForegroundIndex = index;
                }
                else if (runStartIndex >= 0)
                {
                    visibleSegments.Add(CreateObjectDetectionMeasuredSegment(
                        objectBounds,
                        startX,
                        startY,
                        deltaX,
                        deltaY,
                        runStartIndex,
                        runEndIndex,
                        sampleCount));
                    runStartIndex = -1;
                }
            }

            if (firstForegroundIndex < 0)
            {
                return 0;
            }

            if (runStartIndex >= 0)
            {
                visibleSegments.Add(CreateObjectDetectionMeasuredSegment(
                    objectBounds,
                    startX,
                    startY,
                    deltaX,
                    deltaY,
                    runStartIndex,
                    runEndIndex,
                    sampleCount));
            }

            measuredSegment = CreateObjectDetectionMeasuredSegment(
                objectBounds,
                startX,
                startY,
                deltaX,
                deltaY,
                firstForegroundIndex,
                lastForegroundIndex,
                sampleCount);
            return (lastForegroundIndex - firstForegroundIndex + 1) * sampleSpacing;
        }


    }
}
