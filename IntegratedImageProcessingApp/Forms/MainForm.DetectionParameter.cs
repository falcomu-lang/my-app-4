using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private bool objectDetectionParameterMenuExpanded;
        private bool isRebuildingObjectDetectionParameterMenu;
        private readonly Dictionary<int, string> visibleObjectDetectionParameterIds =
            new Dictionary<int, string>();
        private Panel objectDetectionParameterPanel;
        private TabControl objectDetectionParameterTabControl;
        private bool isObjectDetectionParameterImageLayout;
        private string activeObjectDetectionParameterId;
        private Label objectDetectionImageCheckStatusLabel;
        private TableLayoutPanel objectDetectionObjectNumberPanel;
        private int selectedObjectDetectionNumber = -1;
        private TabPage objectDetectionMeasurementTabPage;
        private Panel objectDetectionMeasurementDisplayHostPanel;
        private ImageDisplayControl objectDetectionMeasurementDisplayControl;
        private readonly object objectDetectionMeasurementMaskLock = new object();
        private Cv.Mat objectDetectionMeasurementMaskCache;
        private string objectDetectionMeasurementMaskCacheKey;
        private ComboBox objectDetectionMeasurementModeComboBox;
        private ComboBox objectDetectionMeasurementDirectionComboBox;
        private NumericUpDown objectDetectionMeasurementLineCountBox;
        private TextBox objectDetectionMeasurementNameTextBox;
        private Label objectDetectionMeasurementToolStatusLabel;
        private Button objectDetectionMeasurementDrawButton;
        private DataGridView objectDetectionMeasurementRecordsGrid;
        private string objectDetectionMeasurementAppliedRecordId;
        private bool objectDetectionMeasurementIsDrawing;
        private int objectDetectionMeasurementDrawingStage;
        private Point objectDetectionMeasurementDrawStart;
        private Point objectDetectionMeasurementDrawCurrent;
        private readonly ObjectDetectionMeasurementGeometry pendingObjectDetectionMeasurementGeometry =
            new ObjectDetectionMeasurementGeometry();

        private static readonly Color ObjectDetectionMeasurementMaskColor =
            Color.FromArgb(100, 255, 105, 180);

        private void EnsureObjectDetectionMeasurementDisplay()
        {
            if (objectDetectionMeasurementDisplayControl != null ||
                leftImageTabControl == null)
            {
                return;
            }

            objectDetectionMeasurementTabPage = CreateImageTabPage(
                "leftObjectDetectionMeasurementTabPage",
                "待量測",
                out objectDetectionMeasurementDisplayHostPanel);
            objectDetectionMeasurementDisplayControl = CreateImageDisplayControl(
                objectDetectionMeasurementDisplayHostPanel,
                "左側 待量測");
            objectDetectionMeasurementDisplayControl.ImageMouseDown +=
                ObjectDetectionMeasurementDisplayControl_ImageMouseDown;
            objectDetectionMeasurementDisplayControl.ImageMouseMove +=
                ObjectDetectionMeasurementDisplayControl_ImageMouseMove;
            objectDetectionMeasurementDisplayControl.ImageMouseUp +=
                ObjectDetectionMeasurementDisplayControl_ImageMouseUp;
            objectDetectionMeasurementDisplayControl.ImageOverlayPaint +=
                ObjectDetectionMeasurementDisplayControl_ImageOverlayPaint;
        }

        private void LoadPendingObjectDetectionMeasurementGeometry(
            ObjectDetectionParameterSettings parameter)
        {
            pendingObjectDetectionMeasurementGeometry.Reset();
            if (parameter == null || !parameter.MeasurementLineConfigured)
            {
                return;
            }

            pendingObjectDetectionMeasurementGeometry.HasFirstLine = true;
            pendingObjectDetectionMeasurementGeometry.HasSecondLine =
                string.Equals(parameter.MeasurementMode, "Parallel", StringComparison.Ordinal);
            pendingObjectDetectionMeasurementGeometry.StartX = parameter.MeasurementStartX;
            pendingObjectDetectionMeasurementGeometry.StartY = parameter.MeasurementStartY;
            pendingObjectDetectionMeasurementGeometry.EndX = parameter.MeasurementEndX;
            pendingObjectDetectionMeasurementGeometry.EndY = parameter.MeasurementEndY;
            pendingObjectDetectionMeasurementGeometry.SecondStartX = parameter.MeasurementSecondStartX;
            pendingObjectDetectionMeasurementGeometry.SecondStartY = parameter.MeasurementSecondStartY;
            pendingObjectDetectionMeasurementGeometry.SecondEndX = parameter.MeasurementSecondEndX;
            pendingObjectDetectionMeasurementGeometry.SecondEndY = parameter.MeasurementSecondEndY;
            pendingObjectDetectionMeasurementGeometry.LineOrder =
                NormalizeObjectDetectionMeasurementLineOrder(
                    parameter.MeasurementLineOrder,
                    parameter.MeasurementDirection);
            pendingObjectDetectionMeasurementGeometry.SecondLineOrder =
                NormalizeObjectDetectionMeasurementLineOrder(
                    parameter.MeasurementSecondLineOrder,
                    parameter.MeasurementDirection);
            pendingObjectDetectionMeasurementGeometry.StartOutsideRoi = parameter.MeasurementStartOutsideRoi;
            pendingObjectDetectionMeasurementGeometry.EndOutsideRoi = parameter.MeasurementEndOutsideRoi;
            pendingObjectDetectionMeasurementGeometry.SecondStartOutsideRoi = parameter.MeasurementSecondStartOutsideRoi;
            pendingObjectDetectionMeasurementGeometry.SecondEndOutsideRoi = parameter.MeasurementSecondEndOutsideRoi;
        }

        private static void SavePendingObjectDetectionMeasurementGeometry(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementGeometry geometry,
            string mode,
            string direction,
            int lineCount)
        {
            if (parameter == null || geometry == null)
            {
                return;
            }

            parameter.MeasurementMode = mode;
            parameter.MeasurementDirection = direction;
            parameter.MeasurementLineCount = Math.Max(1, lineCount);
            parameter.MeasurementLineConfigured = geometry.HasFirstLine &&
                (!string.Equals(mode, "Parallel", StringComparison.Ordinal) ||
                 geometry.HasSecondLine);
            parameter.MeasurementStartX = geometry.StartX;
            parameter.MeasurementStartY = geometry.StartY;
            parameter.MeasurementEndX = geometry.EndX;
            parameter.MeasurementEndY = geometry.EndY;
            parameter.MeasurementSecondStartX = geometry.SecondStartX;
            parameter.MeasurementSecondStartY = geometry.SecondStartY;
            parameter.MeasurementSecondEndX = geometry.SecondEndX;
            parameter.MeasurementSecondEndY = geometry.SecondEndY;
            parameter.MeasurementLineOrder = geometry.LineOrder;
            parameter.MeasurementSecondLineOrder = geometry.SecondLineOrder;
            parameter.MeasurementStartOutsideRoi = geometry.StartOutsideRoi;
            parameter.MeasurementEndOutsideRoi = geometry.EndOutsideRoi;
            parameter.MeasurementSecondStartOutsideRoi = geometry.SecondStartOutsideRoi;
            parameter.MeasurementSecondEndOutsideRoi = geometry.SecondEndOutsideRoi;
        }

        private static string NormalizeObjectDetectionMeasurementLineOrder(
            string order,
            string direction)
        {
            if (string.Equals(direction, "Vertical", StringComparison.Ordinal))
            {
                return string.Equals(order, "BottomToTop", StringComparison.Ordinal)
                    ? "BottomToTop"
                    : "TopToBottom";
            }

            return string.Equals(order, "RightToLeft", StringComparison.Ordinal)
                ? "RightToLeft"
                : "LeftToRight";
        }

        private string GetObjectDetectionMeasurementName(
            ObjectDetectionParameterSettings parameter)
        {
            string name = objectDetectionMeasurementNameTextBox == null
                ? null
                : objectDetectionMeasurementNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = parameter == null ? null : parameter.MeasurementName;
            }

            return string.IsNullOrWhiteSpace(name) ? "量測線1" : name.Trim();
        }

        private ObjectDetectionMeasurementRecordSettings CreateObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            string name,
            string mode,
            string direction,
            int lineCount)
        {
            return new ObjectDetectionMeasurementRecordSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Mode = mode,
                Direction = GetObjectDetectionMeasurementGeometryDirection(
                    pendingObjectDetectionMeasurementGeometry),
                LineCount = lineCount,
                StartX = pendingObjectDetectionMeasurementGeometry.StartX,
                StartY = pendingObjectDetectionMeasurementGeometry.StartY,
                EndX = pendingObjectDetectionMeasurementGeometry.EndX,
                EndY = pendingObjectDetectionMeasurementGeometry.EndY,
                SecondStartX = pendingObjectDetectionMeasurementGeometry.SecondStartX,
                SecondStartY = pendingObjectDetectionMeasurementGeometry.SecondStartY,
                SecondEndX = pendingObjectDetectionMeasurementGeometry.SecondEndX,
                SecondEndY = pendingObjectDetectionMeasurementGeometry.SecondEndY,
                LineOrder = pendingObjectDetectionMeasurementGeometry.LineOrder,
                SecondLineOrder = pendingObjectDetectionMeasurementGeometry.SecondLineOrder,
                StartOutsideRoi = pendingObjectDetectionMeasurementGeometry.StartOutsideRoi,
                EndOutsideRoi = pendingObjectDetectionMeasurementGeometry.EndOutsideRoi,
                SecondStartOutsideRoi = pendingObjectDetectionMeasurementGeometry.SecondStartOutsideRoi,
                SecondEndOutsideRoi = pendingObjectDetectionMeasurementGeometry.SecondEndOutsideRoi,
                SourceMaskDisplayName = parameter == null
                    ? "未指定來源 MASK"
                    : (string.IsNullOrWhiteSpace(parameter.MeasurementSourceMaskDisplayName)
                        ? "未指定來源 MASK"
                        : parameter.MeasurementSourceMaskDisplayName),
                SourceMaskMode = parameter == null ? "Direct" : parameter.SourceMaskMode,
                SourceMaskPrimaryType = parameter == null ? string.Empty : parameter.SourceMaskPrimaryType,
                SourceMaskPrimaryId = parameter == null ? string.Empty : parameter.SourceMaskPrimaryId,
                SourceMaskPrimaryNamespace = parameter == null ? string.Empty : parameter.SourceMaskPrimaryNamespace,
                SourceMaskOperation = parameter == null ? "None" : parameter.SourceMaskOperation,
                SourceMaskSecondaryType = parameter == null ? string.Empty : parameter.SourceMaskSecondaryType,
                SourceMaskSecondaryId = parameter == null ? string.Empty : parameter.SourceMaskSecondaryId,
                SourceMaskSecondaryNamespace = parameter == null ? string.Empty : parameter.SourceMaskSecondaryNamespace
            };
        }

        private void UpsertObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            string name,
            string mode,
            string direction,
            int lineCount)
        {
            if (parameter == null)
            {
                return;
            }

            if (parameter.MeasurementRecords == null)
            {
                parameter.MeasurementRecords = new List<ObjectDetectionMeasurementRecordSettings>();
            }

            ObjectDetectionMeasurementRecordSettings updated =
                CreateObjectDetectionMeasurementRecord(parameter, name, mode, direction, lineCount);
            ObjectDetectionMeasurementRecordSettings target = null;
            if (!string.IsNullOrWhiteSpace(objectDetectionMeasurementAppliedRecordId))
            {
                target = parameter.MeasurementRecords.FirstOrDefault(
                    item => item != null && string.Equals(
                        item.Id,
                        objectDetectionMeasurementAppliedRecordId,
                        StringComparison.Ordinal));
            }

            if (target == null)
            {
                target = updated;
                parameter.MeasurementRecords.Add(target);
                objectDetectionMeasurementAppliedRecordId = target.Id;
            }
            else
            {
                CopyObjectDetectionMeasurementRecord(updated, target);
            }
        }

        private static void CopyObjectDetectionMeasurementRecord(
            ObjectDetectionMeasurementRecordSettings source,
            ObjectDetectionMeasurementRecordSettings target)
        {
            target.Name = source.Name;
            target.Mode = source.Mode;
            target.Direction = source.Direction;
            target.LineCount = source.LineCount;
            target.LineOrder = source.LineOrder;
            target.SecondLineOrder = source.SecondLineOrder;
            target.StartOutsideRoi = source.StartOutsideRoi;
            target.EndOutsideRoi = source.EndOutsideRoi;
            target.SecondStartOutsideRoi = source.SecondStartOutsideRoi;
            target.SecondEndOutsideRoi = source.SecondEndOutsideRoi;
            target.SourceMaskDisplayName = source.SourceMaskDisplayName;
            target.SourceMaskMode = source.SourceMaskMode;
            target.SourceMaskPrimaryType = source.SourceMaskPrimaryType;
            target.SourceMaskPrimaryId = source.SourceMaskPrimaryId;
            target.SourceMaskPrimaryNamespace = source.SourceMaskPrimaryNamespace;
            target.SourceMaskOperation = source.SourceMaskOperation;
            target.SourceMaskSecondaryType = source.SourceMaskSecondaryType;
            target.SourceMaskSecondaryId = source.SourceMaskSecondaryId;
            target.SourceMaskSecondaryNamespace = source.SourceMaskSecondaryNamespace;
            target.StartX = source.StartX;
            target.StartY = source.StartY;
            target.EndX = source.EndX;
            target.EndY = source.EndY;
            target.SecondStartX = source.SecondStartX;
            target.SecondStartY = source.SecondStartY;
            target.SecondEndX = source.SecondEndX;
            target.SecondEndY = source.SecondEndY;
        }

        private void RefreshObjectDetectionMeasurementRecordsGrid(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionMeasurementRecordsGrid == null)
            {
                return;
            }

            objectDetectionMeasurementRecordsGrid.Rows.Clear();
            if (parameter == null || parameter.MeasurementRecords == null)
            {
                return;
            }

            foreach (ObjectDetectionMeasurementRecordSettings record in parameter.MeasurementRecords)
            {
                if (record == null)
                {
                    continue;
                }

                string direction = GetObjectDetectionMeasurementRecordDirection(record);
                objectDetectionMeasurementRecordsGrid.Rows.Add(
                    record.Name,
                    record.Mode == "Parallel" ? "平行線" : "單線",
                    direction == "Vertical" ? "垂直" : "水平",
                    record.LineCount,
                    FormatObjectDetectionMeasurementLineOrder(
                        GetObjectDetectionMeasurementRecordLineOrder(record, false),
                        direction,
                        record.Mode == "Parallel"
                            ? GetObjectDetectionMeasurementRecordLineOrder(record, true)
                            : null),
                    FormatObjectDetectionMeasurementOutsideRoi(record),
                    string.IsNullOrWhiteSpace(record.SourceMaskDisplayName)
                        ? "未指定來源 MASK"
                        : record.SourceMaskDisplayName,
                    record.Id);
            }
        }

        private static string GetObjectDetectionMeasurementGeometryDirection(
            ObjectDetectionMeasurementGeometry geometry)
        {
            if (geometry == null || !geometry.HasFirstLine)
            {
                return "Horizontal";
            }

            double deltaX = Math.Abs(geometry.EndX - geometry.StartX);
            double deltaY = Math.Abs(geometry.EndY - geometry.StartY);
            return deltaX >= deltaY ? "Horizontal" : "Vertical";
        }

        private static string GetObjectDetectionMeasurementRecordDirection(
            ObjectDetectionMeasurementRecordSettings record)
        {
            if (record == null)
            {
                return "Horizontal";
            }

            double deltaX = Math.Abs(record.EndX - record.StartX);
            double deltaY = Math.Abs(record.EndY - record.StartY);
            return deltaX >= deltaY ? "Horizontal" : "Vertical";
        }

        private static string GetObjectDetectionMeasurementRecordLineOrder(
            ObjectDetectionMeasurementRecordSettings record,
            bool secondLine)
        {
            if (record == null)
            {
                return "LeftToRight";
            }

            double startX = secondLine ? record.SecondStartX : record.StartX;
            double startY = secondLine ? record.SecondStartY : record.StartY;
            double endX = secondLine ? record.SecondEndX : record.EndX;
            double endY = secondLine ? record.SecondEndY : record.EndY;
            double deltaX = Math.Abs(endX - startX);
            double deltaY = Math.Abs(endY - startY);
            if (deltaY > deltaX)
            {
                return endY >= startY ? "TopToBottom" : "BottomToTop";
            }

            return endX >= startX ? "LeftToRight" : "RightToLeft";
        }

        private static string FormatObjectDetectionMeasurementLineOrder(
            string lineOrder,
            string direction,
            string secondLineOrder)
        {
            string first = string.Equals(direction, "Vertical", StringComparison.Ordinal)
                ? (lineOrder == "BottomToTop" ? "下→上" : "上→下")
                : (lineOrder == "RightToLeft" ? "右→左" : "左→右");
            if (string.IsNullOrWhiteSpace(secondLineOrder))
            {
                return first;
            }

            string second = string.Equals(direction, "Vertical", StringComparison.Ordinal)
                ? (secondLineOrder == "BottomToTop" ? "下→上" : "上→下")
                : (secondLineOrder == "RightToLeft" ? "右→左" : "左→右");
            return first + " / " + second;
        }

        private static string FormatObjectDetectionMeasurementOutsideRoi(
            ObjectDetectionMeasurementRecordSettings record)
        {
            bool firstOutside = record.StartOutsideRoi || record.EndOutsideRoi;
            bool secondOutside = record.SecondStartOutsideRoi || record.SecondEndOutsideRoi;
            if (!firstOutside && !secondOutside)
            {
                return "否";
            }

            if (secondOutside)
            {
                return firstOutside ? "第一、二條有" : "第二條有";
            }

            return "第一條有";
        }

        private void LoadObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            int rowIndex)
        {
            if (parameter == null || parameter.MeasurementRecords == null ||
                rowIndex < 0 || rowIndex >= parameter.MeasurementRecords.Count)
            {
                return;
            }

            ObjectDetectionMeasurementRecordSettings record = parameter.MeasurementRecords[rowIndex];
            if (record == null)
            {
                return;
            }

            parameter.MeasurementName = string.IsNullOrWhiteSpace(record.Name)
                ? "量測線1"
                : record.Name;
            parameter.MeasurementMode = record.Mode;
            parameter.MeasurementDirection = GetObjectDetectionMeasurementRecordDirection(record);
            parameter.MeasurementLineCount = Math.Max(1, record.LineCount);
            parameter.MeasurementLineConfigured = true;
            parameter.MeasurementStartX = record.StartX;
            parameter.MeasurementStartY = record.StartY;
            parameter.MeasurementEndX = record.EndX;
            parameter.MeasurementEndY = record.EndY;
            parameter.MeasurementSecondStartX = record.SecondStartX;
            parameter.MeasurementSecondStartY = record.SecondStartY;
            parameter.MeasurementSecondEndX = record.SecondEndX;
            parameter.MeasurementSecondEndY = record.SecondEndY;
            parameter.MeasurementLineOrder = GetObjectDetectionMeasurementRecordLineOrder(record, false);
            parameter.MeasurementSecondLineOrder = GetObjectDetectionMeasurementRecordLineOrder(record, true);
            parameter.MeasurementStartOutsideRoi = record.StartOutsideRoi;
            parameter.MeasurementEndOutsideRoi = record.EndOutsideRoi;
            parameter.MeasurementSecondStartOutsideRoi = record.SecondStartOutsideRoi;
            parameter.MeasurementSecondEndOutsideRoi = record.SecondEndOutsideRoi;
            parameter.MeasurementSourceMaskDisplayName = string.IsNullOrWhiteSpace(record.SourceMaskDisplayName)
                ? "未指定來源 MASK"
                : record.SourceMaskDisplayName;
            parameter.SourceMaskMode = record.SourceMaskMode;
            parameter.SourceMaskPrimaryType = record.SourceMaskPrimaryType;
            parameter.SourceMaskPrimaryId = record.SourceMaskPrimaryId;
            parameter.SourceMaskPrimaryNamespace = record.SourceMaskPrimaryNamespace;
            parameter.SourceMaskOperation = record.SourceMaskOperation;
            parameter.SourceMaskSecondaryType = record.SourceMaskSecondaryType;
            parameter.SourceMaskSecondaryId = record.SourceMaskSecondaryId;
            parameter.SourceMaskSecondaryNamespace = record.SourceMaskSecondaryNamespace;
            objectDetectionMeasurementAppliedRecordId = record.Id;
            LoadPendingObjectDetectionMeasurementGeometry(parameter);
            if (objectDetectionMeasurementNameTextBox != null)
            {
                objectDetectionMeasurementNameTextBox.Text = parameter.MeasurementName;
            }
            if (objectDetectionMeasurementModeComboBox != null)
            {
                objectDetectionMeasurementModeComboBox.SelectedItem =
                    record.Mode == "Parallel" ? "Parallel" : "Single";
            }
            if (objectDetectionMeasurementDirectionComboBox != null)
            {
                objectDetectionMeasurementDirectionComboBox.SelectedItem =
                    parameter.MeasurementDirection == "Vertical" ? "Vertical" : "Horizontal";
            }
            if (objectDetectionMeasurementLineCountBox != null)
            {
                objectDetectionMeasurementLineCountBox.Value =
                    Math.Max(
                        objectDetectionMeasurementLineCountBox.Minimum,
                        Math.Min(
                            objectDetectionMeasurementLineCountBox.Maximum,
                            record.LineCount));
            }

            objectDetectionMeasurementToolStatusLabel.Text =
                "已載入紀錄：" + parameter.MeasurementName +
                "，MASK：" + parameter.MeasurementSourceMaskDisplayName;
            objectDetectionMeasurementDisplayControl.InvalidateImageView();
        }

        private void SaveObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || parameter.MeasurementRecords == null ||
                parameter.MeasurementRecords.Count == 0)
            {
                statusLabel.Text = "請先按套用量測設定，建立量測資料紀錄";
                return;
            }

            SaveSystemParameters();
            objectDetectionMeasurementToolStatusLabel.Text =
                "量測資料已保存到參數檔（包含使用的 MASK）";
            statusLabel.Text = parameter.DisplayName +
                " 已保存量測資料，共 " + parameter.MeasurementRecords.Count + " 筆";
        }

        private void BeginObjectDetectionMeasurementDrawing()
        {
            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                statusLabel.Text = "請先選擇已找到的物件序號，再開始畫量測線";
                return;
            }

            pendingObjectDetectionMeasurementGeometry.Reset();
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
                return;
            }

            string mode = GetObjectDetectionMeasurementMode();
            int lineCount = objectDetectionMeasurementLineCountBox == null
                ? 1
                : Decimal.ToInt32(objectDetectionMeasurementLineCountBox.Value);
            lineCount = Math.Max(1, Math.Min(1000, lineCount));
            // Keep measurement lines at a stable screen width. Scaling the
            // pen by 1 / zoom makes a 0.05x view render a huge 40px line.
            using (var primaryPen = new Pen(Color.Red, 1.5f))
            using (var guidePen = new Pen(Color.FromArgb(180, Color.DeepSkyBlue), 1f))
            using (var endpointBrush = new SolidBrush(Color.Red))
            {
                if (string.Equals(mode, "Parallel", StringComparison.Ordinal) && hasSecond)
                {
                    int renderCount = Math.Max(2, lineCount);
                    for (int index = 0; index < renderCount; index++)
                    {
                        double ratio = renderCount == 1
                            ? 0
                            : index / (double)(renderCount - 1);
                        ObjectDetectionImageLine line = InterpolateObjectDetectionImageLine(
                            first,
                            second,
                            ratio);
                        Pen pen = index == 0 || index == renderCount - 1
                            ? primaryPen
                            : guidePen;
                        DrawObjectDetectionImageLine(e.Graphics, line, pen, e.Zoom, e.Offset);
                    }
                    DrawObjectDetectionImageLineEndpoints(
                        e.Graphics,
                        first,
                        endpointBrush,
                        e.Zoom,
                        e.Offset);
                    DrawObjectDetectionImageLineEndpoints(
                        e.Graphics,
                        second,
                        endpointBrush,
                        e.Zoom,
                        e.Offset);
                }
                else
                {
                    DrawObjectDetectionImageLine(
                        e.Graphics,
                        first,
                        primaryPen,
                        e.Zoom,
                        e.Offset);
                    DrawObjectDetectionImageLineEndpoints(
                        e.Graphics,
                        first,
                        endpointBrush,
                        e.Zoom,
                        e.Offset);
                }
            }
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

        private bool TryGetObjectDetectionMeasurementMask(
            ObjectDetectionParameterSettings parameter,
            Rectangle objectBounds,
            LargeImageSource largeSource,
            Bitmap original,
            out Cv.Mat mask)
        {
            mask = null;
            if (parameter == null || objectBounds.Width <= 0 || objectBounds.Height <= 0 ||
                string.IsNullOrWhiteSpace(parameter.SourceMaskPrimaryType) ||
                string.IsNullOrWhiteSpace(parameter.SourceMaskPrimaryId))
            {
                return false;
            }

            string cacheKey = string.Join(
                "|",
                parameter.Id ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                selectedObjectDetectionNumber.ToString(CultureInfo.InvariantCulture),
                objectBounds.X.ToString(CultureInfo.InvariantCulture),
                objectBounds.Y.ToString(CultureInfo.InvariantCulture),
                objectBounds.Width.ToString(CultureInfo.InvariantCulture),
                objectBounds.Height.ToString(CultureInfo.InvariantCulture),
                parameter.SourceMaskMode ?? string.Empty,
                parameter.SourceMaskPrimaryType ?? string.Empty,
                parameter.SourceMaskPrimaryId ?? string.Empty,
                parameter.SourceMaskPrimaryNamespace ?? string.Empty,
                parameter.SourceMaskOperation ?? string.Empty,
                parameter.SourceMaskSecondaryType ?? string.Empty,
                parameter.SourceMaskSecondaryId ?? string.Empty,
                parameter.SourceMaskSecondaryNamespace ?? string.Empty);

            lock (objectDetectionMeasurementMaskLock)
            {
                if (string.Equals(
                    objectDetectionMeasurementMaskCacheKey,
                    cacheKey,
                    StringComparison.Ordinal) &&
                    objectDetectionMeasurementMaskCache != null &&
                    !objectDetectionMeasurementMaskCache.Empty() &&
                    objectDetectionMeasurementMaskCache.Cols == objectBounds.Width &&
                    objectDetectionMeasurementMaskCache.Rows == objectBounds.Height)
                {
                    mask = objectDetectionMeasurementMaskCache.Clone();
                    return true;
                }
            }

            Cv.Mat created = null;
            try
            {
                Cv.Mat originalGray = null;
                try
                {
                    if (largeSource != null)
                    {
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskPrimaryType,
                            parameter.SourceMaskPrimaryId,
                            parameter,
                            objectBounds,
                            largeSource,
                            null,
                            null,
                            parameter.SourceMaskPrimaryNamespace,
                            out created))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (original == null)
                        {
                            return false;
                        }

                        originalGray = CreateOpenCvGrayMat(original);
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskPrimaryType,
                            parameter.SourceMaskPrimaryId,
                            parameter,
                            objectBounds,
                            null,
                            original,
                            originalGray,
                            parameter.SourceMaskPrimaryNamespace,
                            out created))
                        {
                            return false;
                        }
                    }
                }
                finally
                {
                    if (originalGray != null)
                    {
                        originalGray.Dispose();
                    }
                }

                string operation = string.IsNullOrWhiteSpace(parameter.SourceMaskOperation)
                    ? "None"
                    : parameter.SourceMaskOperation;
                if (string.Equals(parameter.SourceMaskMode, "Direct", StringComparison.Ordinal) ||
                    string.Equals(operation, "None", StringComparison.Ordinal))
                {
                    return StoreObjectDetectionMeasurementMask(cacheKey, ref created, out mask);
                }

                if (string.Equals(operation, "Not", StringComparison.Ordinal))
                {
                    var inverted = new Cv.Mat();
                    Cv.Cv2.BitwiseNot(created, inverted);
                    created.Dispose();
                    created = inverted;
                    return StoreObjectDetectionMeasurementMask(cacheKey, ref created, out mask);
                }

                Cv.Mat secondary = null;
                Cv.Mat secondaryGray = null;
                try
                {
                    if (largeSource != null)
                    {
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskSecondaryType,
                            parameter.SourceMaskSecondaryId,
                            parameter,
                            objectBounds,
                            largeSource,
                            null,
                            null,
                            parameter.SourceMaskSecondaryNamespace,
                            out secondary))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        secondaryGray = CreateOpenCvGrayMat(original);
                        if (!TryCreateObjectDetectionSourceMask(
                            parameter.SourceMaskSecondaryType,
                            parameter.SourceMaskSecondaryId,
                            parameter,
                            objectBounds,
                            null,
                            original,
                            secondaryGray,
                            parameter.SourceMaskSecondaryNamespace,
                            out secondary))
                        {
                            return false;
                        }
                    }

                    if (created.Rows != secondary.Rows || created.Cols != secondary.Cols)
                    {
                        return false;
                    }

                    var combined = new Cv.Mat();
                    if (string.Equals(operation, "Or", StringComparison.Ordinal))
                    {
                        Cv.Cv2.BitwiseOr(created, secondary, combined);
                    }
                    else if (string.Equals(operation, "And", StringComparison.Ordinal))
                    {
                        Cv.Cv2.BitwiseAnd(created, secondary, combined);
                    }
                    else if (string.Equals(operation, "Subtract", StringComparison.Ordinal))
                    {
                        using (var invertedSecondary = new Cv.Mat())
                        {
                            Cv.Cv2.BitwiseNot(secondary, invertedSecondary);
                            Cv.Cv2.BitwiseAnd(created, invertedSecondary, combined);
                        }
                    }
                    else if (string.Equals(operation, "Xor", StringComparison.Ordinal))
                    {
                        Cv.Cv2.BitwiseXor(created, secondary, combined);
                    }
                    else
                    {
                        combined.Dispose();
                        return false;
                    }

                    created.Dispose();
                    created = combined;
                    return StoreObjectDetectionMeasurementMask(cacheKey, ref created, out mask);
                }
                finally
                {
                    if (secondaryGray != null)
                    {
                        secondaryGray.Dispose();
                    }

                    if (secondary != null)
                    {
                        secondary.Dispose();
                    }
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

        private bool TryCreateObjectDetectionSourceMask(
            string sourceType,
            string sourceId,
            ObjectDetectionParameterSettings parameter,
            Rectangle objectBounds,
            LargeImageSource largeSource,
            Bitmap original,
            Cv.Mat originalGray,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            if (string.Equals(sourceType, "ObjectDefinition", StringComparison.Ordinal))
            {
                return TryGetObjectDetectionObjectDefinitionMask(
                    sourceId,
                    objectBounds,
                    out mask);
            }

            Rectangle sourceRoi;
            if (!TryGetObjectDetectionContainingRoi(objectBounds, out sourceRoi))
            {
                return false;
            }

            Cv.Mat sourceMask = null;
            if (largeSource != null)
            {
                if (!TryGetCachedObjectDetectionMaskSource(
                    largeSource,
                    sourceType,
                    sourceId,
                    sourceRoi,
                    sourceNamespace,
                    out sourceMask))
                {
                    return false;
                }
            }
            else
            {
                if (original == null || originalGray == null ||
                    !TryGetCachedObjectDetectionMaskSourceFromBitmap(
                        original,
                        originalGray,
                        sourceType,
                        sourceId,
                        sourceRoi,
                        sourceNamespace,
                        out sourceMask))
                {
                    return false;
                }
            }

            try
            {
                int localX = objectBounds.X - sourceRoi.X;
                int localY = objectBounds.Y - sourceRoi.Y;
                if (localX < 0 || localY < 0 ||
                    localX + objectBounds.Width > sourceMask.Cols ||
                    localY + objectBounds.Height > sourceMask.Rows)
                {
                    return false;
                }

                using (var view = new Cv.Mat(
                    sourceMask,
                    new Cv.Rect(
                        localX,
                        localY,
                        objectBounds.Width,
                        objectBounds.Height)))
                {
                    mask = view.Clone();
                }

                return true;
            }
            finally
            {
                sourceMask.Dispose();
            }
        }

        private bool TryGetCachedObjectDetectionMaskSource(
            LargeImageSource source,
            string sourceType,
            string sourceId,
            Rectangle roi,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.Equals(sourceType, "ObjectJudgementProcessing", StringComparison.Ordinal))
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                    item => string.Equals(item.Id, sourceNamespace, StringComparison.Ordinal));
                if (objectJudgement == null)
                {
                    return false;
                }

                int processingIndex = objectJudgement.ProcessingSteps.FindIndex(
                    item => item != null &&
                        string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (processingIndex < 0)
                {
                    return false;
                }

                return TryGetCachedObjectJudgementMask(
                    objectJudgement,
                    GetObjectJudgementProcessingChain(objectJudgement, processingIndex),
                    roi,
                    out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroupStep", StringComparison.Ordinal) ||
                string.Equals(sourceType, "ImageProcessingStep", StringComparison.Ordinal))
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (step == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(sourceNamespace) &&
                    TryGetCachedProcessedBinaryMask(
                        roi,
                        step,
                        sourceNamespace,
                        out mask))
                {
                    return true;
                }

                return TryGetCachedLargeProcessedStepMask(roi, step, out mask) ||
                    TryGetCachedObjectDefinitionMaskSource(
                        source,
                        sourceType == "ImageProcessingGroupStep"
                            ? "ImageProcessingStep"
                            : sourceType,
                        sourceId,
                        roi,
                        out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroup", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(sourceNamespace))
            {
                ImageProcessingGroupSettings group = FindImageProcessingGroup(sourceId);
                if (group == null)
                {
                    return false;
                }

                var steps = new List<ImageProcessingStepSettings>();
                CollectImageProcessingGroupSteps(group.Id, steps);
                return TryCreateCachedImageProcessingMask(
                    roi,
                    steps,
                    out mask,
                    sourceNamespace);
            }

            return TryGetCachedObjectDefinitionMaskSource(
                source,
                sourceType,
                sourceId,
                roi,
                out mask);
        }

        private bool TryGetCachedObjectDetectionMaskSourceFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            string sourceType,
            string sourceId,
            Rectangle roi,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            // Object-judgement processing step masks are stored per ROI by
            // the large-image processing path. The bitmap path has no
            // equivalent per-step cache yet, so do not silently rerun the
            // relation here or show a result from the wrong processing step.
            if (string.Equals(sourceType, "ObjectJudgementProcessing", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(sourceType, "ImageProcessingGroupStep", StringComparison.Ordinal) ||
                string.Equals(sourceType, "ImageProcessingStep", StringComparison.Ordinal))
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                    item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
                if (step == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(sourceNamespace) &&
                    TryCreateCachedImageProcessingMaskFromBitmap(
                        original,
                        originalGray,
                        roi,
                        new[] { step },
                        sourceNamespace,
                        out mask))
                {
                    return true;
                }

                return TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                    original,
                    originalGray,
                    "ImageProcessingStep",
                    sourceId,
                    roi,
                    out mask);
            }

            if (string.Equals(sourceType, "ImageProcessingGroup", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(sourceNamespace))
            {
                ImageProcessingGroupSettings group = FindImageProcessingGroup(sourceId);
                if (group == null)
                {
                    return false;
                }

                var steps = new List<ImageProcessingStepSettings>();
                CollectImageProcessingGroupSteps(group.Id, steps);
                return TryCreateCachedImageProcessingMaskFromBitmap(
                    original,
                    originalGray,
                    roi,
                    steps,
                    sourceNamespace,
                    out mask);
            }

            return TryGetCachedObjectDefinitionMaskSourceFromBitmap(
                original,
                originalGray,
                sourceType,
                sourceId,
                roi,
                out mask);
        }

        private bool TryGetObjectDetectionContainingRoi(
            Rectangle objectBounds,
            out Rectangle roi)
        {
            roi = Rectangle.Empty;
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle candidate = roiRegion.Bounds;
                if (candidate.Width > 0 && candidate.Height > 0 &&
                    candidate.Contains(objectBounds))
                {
                    roi = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetObjectDetectionObjectDefinitionMask(
            string definitionId,
            Rectangle objectBounds,
            out Cv.Mat mask)
        {
            mask = null;
            if (string.IsNullOrWhiteSpace(definitionId) || selectedObjectDetectionNumber <= 0)
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = roiRegion.Bounds;
                    if (roi.Width <= 0 || roi.Height <= 0 ||
                        objectBounds.Left < roi.Left || objectBounds.Top < roi.Top ||
                        objectBounds.Right > roi.Right || objectBounds.Bottom > roi.Bottom)
                    {
                        continue;
                    }

                    string resultKey = CreateObjectDefinitionResultKey(definitionId, roi);
                    List<ObjectDefinitionDetectedObject> objects;
                    Cv.Mat sourceMask;
                    if (!objectDefinitionResults.TryGetValue(resultKey, out objects) ||
                        !objectDefinitionSourceMasks.TryGetValue(resultKey, out sourceMask) ||
                        sourceMask == null || sourceMask.Empty() ||
                        !objects.Any(item => item.Number == selectedObjectDetectionNumber &&
                            item.Bounds == objectBounds))
                    {
                        continue;
                    }

                    int localX = objectBounds.X - roi.X;
                    int localY = objectBounds.Y - roi.Y;
                    if (localX < 0 || localY < 0 ||
                        localX + objectBounds.Width > sourceMask.Cols ||
                        localY + objectBounds.Height > sourceMask.Rows)
                    {
                        continue;
                    }

                    using (var view = new Cv.Mat(
                        sourceMask,
                        new Cv.Rect(
                            localX,
                            localY,
                            objectBounds.Width,
                            objectBounds.Height)))
                    {
                        mask = view.Clone();
                    }

                    return true;
                }
            }

            return false;
        }

        private bool StoreObjectDetectionMeasurementMask(
            string cacheKey,
            ref Cv.Mat created,
            out Cv.Mat mask)
        {
            mask = null;
            if (created == null || created.Empty())
            {
                return false;
            }

            lock (objectDetectionMeasurementMaskLock)
            {
                if (objectDetectionMeasurementMaskCache != null)
                {
                    objectDetectionMeasurementMaskCache.Dispose();
                }

                objectDetectionMeasurementMaskCache = created;
                objectDetectionMeasurementMaskCacheKey = cacheKey;
                created = null;
                mask = objectDetectionMeasurementMaskCache.Clone();
                return true;
            }
        }

        private void InvalidateObjectDetectionMeasurementMaskCache()
        {
            lock (objectDetectionMeasurementMaskLock)
            {
                if (objectDetectionMeasurementMaskCache != null)
                {
                    objectDetectionMeasurementMaskCache.Dispose();
                    objectDetectionMeasurementMaskCache = null;
                }

                objectDetectionMeasurementMaskCacheKey = null;
            }
        }

        private static Bitmap CreateObjectDetectionMaskOverlay(Cv.Mat mask, Color color)
        {
            return CreateObjectDetectionMaskOverlay(
                mask,
                mask == null ? 0 : mask.Cols,
                mask == null ? 0 : mask.Rows,
                color);
        }

        private static Bitmap CreateObjectDetectionMaskOverlay(
            Cv.Mat mask,
            int targetWidth,
            int targetHeight,
            Color color)
        {
            if (mask == null || mask.Empty() || targetWidth <= 0 || targetHeight <= 0)
            {
                return new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            }

            if (mask.Cols != targetWidth || mask.Rows != targetHeight)
            {
                using (var scaled = new Cv.Mat())
                {
                    Cv.Cv2.Resize(
                        mask,
                        scaled,
                        new Cv.Size(targetWidth, targetHeight),
                        0,
                        0,
                        targetWidth < mask.Cols || targetHeight < mask.Rows
                            ? Cv.InterpolationFlags.Area
                            : Cv.InterpolationFlags.Nearest);
                    return CreateObjectDetectionMaskOverlayBitmap(scaled, color);
                }
            }

            return CreateObjectDetectionMaskOverlayBitmap(mask, color);
        }

        private static Bitmap CreateObjectDetectionMaskOverlayBitmap(Cv.Mat mask, Color color)
        {
            var overlay = new Bitmap(mask.Cols, mask.Rows, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(
                new Rectangle(0, 0, overlay.Width, overlay.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                byte[] sourceRow = new byte[mask.Cols];
                byte[] outputRow = new byte[Math.Abs(data.Stride)];
                long step = mask.Step();
                for (int y = 0; y < mask.Rows; y++)
                {
                    Array.Clear(outputRow, 0, outputRow.Length);
                    Marshal.Copy(
                        IntPtr.Add(mask.Data, checked((int)(y * step))),
                        sourceRow,
                        0,
                        sourceRow.Length);
                    for (int x = 0; x < mask.Cols; x++)
                    {
                        if (sourceRow[x] == 0)
                        {
                            continue;
                        }

                        int offset = x * 4;
                        outputRow[offset] = color.B;
                        outputRow[offset + 1] = color.G;
                        outputRow[offset + 2] = color.R;
                        outputRow[offset + 3] = color.A;
                    }

                    Marshal.Copy(
                        outputRow,
                        0,
                        data.Scan0 + (y * data.Stride),
                        outputRow.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private void ObjectDetectionMeasurementDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            if (!isObjectDetectionParameterImageLayout || e == null)
            {
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject) ||
                selectedObject == null ||
                !e.VisibleSourceRect.IntersectsWith(selectedObject.Bounds))
            {
                return;
            }

            Rectangle objectBounds = selectedObject.Bounds;

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter != null)
            {
                LargeImageSource source = rightOriginalDisplayControl == null
                    ? null
                    : rightOriginalDisplayControl.GetSharedLargeImageSource();
                try
                {
                    Cv.Mat measurementMask;
                    if (TryGetObjectDetectionMeasurementMask(
                        parameter,
                        objectBounds,
                        source,
                        null,
                        out measurementMask))
                    {
                        using (measurementMask)
                        {
                            Rectangle visibleMaskBounds = Rectangle.Intersect(
                                objectBounds,
                                e.VisibleSourceRect);
                            if (visibleMaskBounds.Width > 0 && visibleMaskBounds.Height > 0 &&
                                measurementMask.Cols == objectBounds.Width &&
                                measurementMask.Rows == objectBounds.Height)
                            {
                                int maskX = visibleMaskBounds.X - objectBounds.X;
                                int maskY = visibleMaskBounds.Y - objectBounds.Y;
                                using (var visibleMask = new Cv.Mat(
                                    measurementMask,
                                    new Cv.Rect(
                                        maskX,
                                        maskY,
                                        visibleMaskBounds.Width,
                                        visibleMaskBounds.Height)))
                                {
                                    int targetWidth = Math.Max(
                                        1,
                                        Math.Min(
                                            2048,
                                            (int)Math.Ceiling(visibleMaskBounds.Width * e.Zoom)));
                                    int targetHeight = Math.Max(
                                        1,
                                        Math.Min(
                                            2048,
                                            (int)Math.Ceiling(visibleMaskBounds.Height * e.Zoom)));
                                    using (Bitmap overlay = CreateObjectDetectionMaskOverlay(
                                        visibleMask,
                                        targetWidth,
                                        targetHeight,
                                        ObjectDetectionMeasurementMaskColor))
                                    {
                                        DrawLargeProcessedOverlayTile(
                                            e.Graphics,
                                            overlay,
                                            visibleMaskBounds,
                                            e.Zoom,
                                            e.Offset);
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (source != null)
                    {
                        source.ReleaseReference();
                    }
                }
            }

            using (var outline = new Pen(Color.LimeGreen, 2f))
            {
                DrawObjectDetectionObjectOutline(
                    e.Graphics,
                    selectedObject,
                    outline,
                    e.Zoom,
                    e.Offset);
            }
        }

        private static string GetObjectDetectionParameterDisplayName(
            ObjectDetectionParameterSettings parameter,
            int parameterIndex)
        {
            return string.IsNullOrWhiteSpace(parameter.DisplayName)
                ? "檢測參數" + (parameterIndex + 1).ToString(CultureInfo.InvariantCulture)
                : parameter.DisplayName.Trim();
        }

        private ObjectDetectionParameterSettings FindObjectDetectionParameter(string parameterId)
        {
            return systemParameters.ObjectDetectionParameters.FirstOrDefault(
                parameter => string.Equals(parameter.Id, parameterId, StringComparison.Ordinal));
        }

        private bool TryGetObjectDetectionParameterLocation(
            int visibleIndex,
            string menuText,
            out string parameterId)
        {
            parameterId = null;
            if (!visibleObjectDetectionParameterIds.TryGetValue(visibleIndex, out parameterId))
            {
                string name = menuText == null ? string.Empty : menuText.Trim();
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    if (string.Equals(
                        GetObjectDetectionParameterDisplayName(systemParameters.ObjectDetectionParameters[index], index),
                        name,
                        StringComparison.Ordinal))
                    {
                        parameterId = systemParameters.ObjectDetectionParameters[index].Id;
                        break;
                    }
                }
            }

            return !string.IsNullOrEmpty(parameterId) && FindObjectDetectionParameter(parameterId) != null;
        }

        private void ToggleObjectDetectionParameterMenu()
        {
            objectDetectionParameterMenuExpanded = !objectDetectionParameterMenuExpanded;
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = objectDetectionParameterMenuExpanded
                ? "已展開檢測參數設定"
                : "已收合檢測參數設定";
        }

        private void AddObjectDetectionParameter()
        {
            var parameter = new ObjectDetectionParameterSettings
            {
                DisplayName = "檢測參數" +
                    (systemParameters.ObjectDetectionParameters.Count + 1).ToString(CultureInfo.InvariantCulture),
                Parameters = string.Empty
            };
            systemParameters.ObjectDetectionParameters.Add(parameter);
            objectDetectionParameterMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameter.Id);
            statusLabel.Text = "已新增" + parameter.DisplayName;
        }

        private void RebuildVisibleObjectDetectionParameters()
        {
            bool wasRebuilding = isRebuildingObjectDetectionParameterMenu;
            isRebuildingObjectDetectionParameterMenu = true;
            try
            {
                int menuIndex = functionListBox.Items.IndexOf(ObjectDetectionParameterMenuText);
                if (menuIndex < 0)
                {
                    return;
                }

                visibleObjectDetectionParameterIds.Clear();
                int removeIndex = menuIndex + 1;
                while (removeIndex < functionListBox.Items.Count)
                {
                    string text = functionListBox.Items[removeIndex] as string;
                    if (string.IsNullOrEmpty(text) || !text.StartsWith("    ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    functionListBox.Items.RemoveAt(removeIndex);
                }

                if (!objectDetectionParameterMenuExpanded)
                {
                    return;
                }

                int insertIndex = menuIndex + 1;
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
                    visibleObjectDetectionParameterIds[insertIndex] = parameter.Id;
                    functionListBox.Items.Insert(
                        insertIndex++,
                        "    " + GetObjectDetectionParameterDisplayName(parameter, index));
                }
            }
            finally
            {
                isRebuildingObjectDetectionParameterMenu = wasRebuilding;
            }
        }

        private void SelectObjectDetectionParameter(string parameterId)
        {
            foreach (KeyValuePair<int, string> item in visibleObjectDetectionParameterIds)
            {
                if (string.Equals(item.Value, parameterId, StringComparison.Ordinal))
                {
                    functionListBox.SelectedIndex = item.Key;
                    return;
                }
            }
        }

        private void ShowObjectDetectionParameterMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增檢測參數", null, delegate { AddObjectDetectionParameter(); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDetectionParameterItemContextMenu(string parameterId, Point location)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("命名", null, delegate { RenameObjectDetectionParameter(parameterId); });
            menu.Items.Add("上移", null, delegate { MoveObjectDetectionParameter(parameterId, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectDetectionParameter(parameterId, 1); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectDetectionParameter(parameterId); });
            menu.Show(functionListBox, location);
        }

        private void RenameObjectDetectionParameter(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            string name = PromptForText("命名檢測參數", "檢測參數名稱", parameter.DisplayName);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            parameter.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameterId);
            statusLabel.Text = "已命名檢測參數" + parameter.DisplayName;
        }

        private void MoveObjectDetectionParameter(string parameterId, int direction)
        {
            int index = systemParameters.ObjectDetectionParameters.FindIndex(
                item => string.Equals(item.Id, parameterId, StringComparison.Ordinal));
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= systemParameters.ObjectDetectionParameters.Count)
            {
                return;
            }

            ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
            systemParameters.ObjectDetectionParameters.RemoveAt(index);
            systemParameters.ObjectDetectionParameters.Insert(targetIndex, parameter);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameterId);
        }

        private void DeleteObjectDetectionParameter(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "確定要刪除「" + parameter.DisplayName + "」嗎？",
                    "刪除檢測參數",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            systemParameters.ObjectDetectionParameters.Remove(parameter);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = "已刪除檢測參數";
        }

        private void ShowObjectDetectionParameterPanel(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            HideObjectDefinitionParameterPanel();
            if (objectDetectionParameterPanel != null)
            {
                parameterPanel.Controls.Remove(objectDetectionParameterPanel);
            }

            parameterPlaceholderLabel.Visible = false;
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            objectDetectionParameterPanel = panel;

            panel.Controls.Add(new Label
            {
                Text = "檢測參數名稱",
                Left = 8,
                Top = 12,
                Width = 250
            });
            var nameBox = new TextBox
            {
                Left = 8,
                Top = 34,
                Width = parameterPanel.Width - 18,
                Text = parameter.DisplayName ?? string.Empty
            };
            panel.Controls.Add(nameBox);

            panel.Controls.Add(new Label
            {
                Text = "參數內容",
                Left = 8,
                Top = 72,
                Width = 250
            });
            var parametersBox = new TextBox
            {
                Left = 8,
                Top = 94,
                Width = parameterPanel.Width - 18,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = parameter.Parameters ?? string.Empty
            };
            panel.Controls.Add(parametersBox);

            panel.Controls.Add(new Label
            {
                Text = "物件定義結果關聯",
                Left = 8,
                Top = 228,
                Width = 250
            });
            var objectDefinitionSource = new ComboBox
            {
                Left = 8,
                Top = 250,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            objectDefinitionSource.Items.Add(new ObjectDetectionDefinitionChoice
            {
                DisplayText = "未指定物件定義結果",
                Id = string.Empty
            });
            for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
            {
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                objectDefinitionSource.Items.Add(new ObjectDetectionDefinitionChoice
                {
                    DisplayText = GetObjectDefinitionDisplayName(definition, index),
                    Id = definition.Id
                });
            }

            objectDefinitionSource.SelectedIndex = 0;
            for (int index = 0; index < objectDefinitionSource.Items.Count; index++)
            {
                ObjectDetectionDefinitionChoice choice =
                    objectDefinitionSource.Items[index] as ObjectDetectionDefinitionChoice;
                if (choice != null && string.Equals(
                        choice.Id,
                        parameter.ObjectDefinitionId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    objectDefinitionSource.SelectedIndex = index;
                    break;
                }
            }
            panel.Controls.Add(objectDefinitionSource);

            var apply = new Button
            {
                Text = "套用",
                Left = 8,
                Top = 286,
                Width = parameterPanel.Width - 18
            };
            apply.Click += delegate
            {
                parameter.DisplayName = string.IsNullOrWhiteSpace(nameBox.Text)
                        ? GetObjectDetectionParameterDisplayName(
                        parameter,
                        systemParameters.ObjectDetectionParameters.IndexOf(parameter))
                    : nameBox.Text.Trim();
                parameter.Parameters = parametersBox.Text ?? string.Empty;
                ObjectDetectionDefinitionChoice selectedDefinition =
                    objectDefinitionSource.SelectedItem as ObjectDetectionDefinitionChoice;
                parameter.ObjectDefinitionId = selectedDefinition == null
                    ? string.Empty
                    : selectedDefinition.Id;
                SaveSystemParameters();
                RebuildVisibleObjectDetectionParameters();
                SelectObjectDetectionParameter(parameter.Id);
                selectedObjectDetectionNumber = -1;
                statusLabel.Text = string.IsNullOrEmpty(parameter.ObjectDefinitionId)
                    ? "已套用" + parameter.DisplayName
                    : "已套用" + parameter.DisplayName + "，已關聯物件定義結果";
                UpdateObjectDetectionParameterTabs(parameter);
                EnsureObjectDetectionParameterSourceProcessed(parameter);
                RefreshObjectDetectionMeasurementDisplay();
            };
            panel.Controls.Add(apply);

            var cancel = new Button
            {
                Text = "取消",
                Left = 8,
                Top = 322,
                Width = parameterPanel.Width - 18
            };
            cancel.Click += delegate { ShowObjectDetectionParameterPanel(parameter.Id); };
            panel.Controls.Add(cancel);

            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            if (!string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal))
            {
                selectedObjectDetectionNumber = -1;
            }
            activeObjectDetectionParameterId = parameter.Id;
            SetObjectDetectionParameterDisplayMode(true);
            UpdateObjectDetectionParameterTabs(parameter);
            EnsureObjectDetectionParameterSourceProcessed(parameter);
        }

        private void EnsureObjectDetectionParameterSourceProcessed(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter == null ? null : parameter.Id,
                    "尚未指定物件定義結果",
                    Color.FromArgb(75, 83, 95));
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "物件定義結果不存在，請重新設定關聯",
                    Color.Firebrick);
                return;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                RefreshObjectDetectionParameterQuantityStatus(parameter);
                return;
            }

            if (!HasConfiguredObjectDefinitionSource(definition))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "物件定義尚未設定完整來源",
                    Color.Firebrick);
                ProcessObjectDefinition(definition.Id);
                return;
            }

            if ((rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage) &&
                (leftOriginalDisplayControl == null || !leftOriginalDisplayControl.HasImage))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "請先載入圖片",
                    Color.Firebrick);
                return;
            }

            UpdateObjectDetectionParameterSourceStatus(
                parameter.Id,
                "進行影像確認，請稍等",
                Color.Firebrick);
            ProcessObjectDefinition(definition.Id);
        }

        private void UpdateObjectDetectionParameterSourceStatus(
            string parameterId,
            string text,
            Color color)
        {
            if (objectDetectionImageCheckStatusLabel == null ||
                !string.Equals(
                    activeObjectDetectionParameterId,
                    parameterId,
                    StringComparison.Ordinal))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            string actualCount = string.Equals(text, "進行影像確認，請稍等", StringComparison.Ordinal)
                ? "處理中"
                : "尚未確認";
            if (parameter == null)
            {
                objectDetectionImageCheckStatusLabel.Text = text ?? string.Empty;
            }
            else
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        actualCount,
                        text);
            }
            objectDetectionImageCheckStatusLabel.ForeColor = color;
        }

        private void RefreshObjectDetectionParameterQuantityStatus(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionImageCheckStatusLabel == null ||
                !string.Equals(
                    activeObjectDetectionParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                return;
            }

            long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
            if (parameter.ColumnCount == 0 && parameter.RowCount == 0)
            {
                objectDetectionImageCheckStatusLabel.Text = "數量檢查：未啟用";
                objectDetectionImageCheckStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                return;
            }

            if (parameter.ColumnCount <= 0 || parameter.RowCount <= 0)
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        "尚未確認",
                        "數量設定不完整");
                objectDetectionImageCheckStatusLabel.ForeColor = Color.Firebrick;
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            int actualCount;
            if (definition == null || !TryGetCompletedObjectDefinitionCount(definition, out actualCount))
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        "尚未確認",
                        "尚未進行影像確認");
                objectDetectionImageCheckStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                return;
            }

            bool isMatch = expectedCount == actualCount;
            objectDetectionImageCheckStatusLabel.Text =
                BuildObjectDetectionParameterQuantityStatusText(
                    parameter,
                    actualCount.ToString(CultureInfo.InvariantCulture),
                    isMatch ? "數量符合" : "數量不符，請確認影像或參數");
            objectDetectionImageCheckStatusLabel.ForeColor =
                isMatch ? Color.ForestGreen : Color.Firebrick;
        }

        private void AppendObjectDetectionParameterQuantityToStatusLabel(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null ||
                !string.Equals(parameter.ObjectDefinitionId, definitionId, StringComparison.Ordinal) ||
                parameter.ColumnCount <= 0 ||
                parameter.RowCount <= 0)
            {
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            int actualCount;
            if (definition == null || !TryGetCompletedObjectDefinitionCount(definition, out actualCount))
            {
                return;
            }

            long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
            bool isMatch = expectedCount == actualCount;
            statusLabel.Text += " || 數量確認：" +
                (isMatch ? "符合 " : "不符 ") +
                actualCount.ToString(CultureInfo.InvariantCulture) +
                " / " + expectedCount.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryGetCompletedObjectDefinitionCount(
            ObjectDefinitionSettings definition,
            out int actualCount)
        {
            actualCount = 0;
            if (definition == null)
            {
                return false;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(
                        completedObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                actualCount = objectDefinitionResults.Values
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .Count();
                return true;
            }
        }

        private static string BuildObjectDetectionParameterQuantityStatusText(
            ObjectDetectionParameterSettings parameter,
            string actualCount,
            string resultText)
        {
            string expectedCount = parameter == null
                ? "尚未設定"
                : parameter.ColumnCount > 0 && parameter.RowCount > 0
                    ? ((long)parameter.ColumnCount * parameter.RowCount)
                        .ToString(CultureInfo.InvariantCulture)
                    : "尚未設定";
            return "預期物件數量：" + expectedCount +
                "\r\n實際找到數量：" + (actualCount ?? "尚未確認") +
                "\r\n" + (resultText ?? string.Empty);
        }

        private void NotifyObjectDetectionParameterSourceProcessingCompleted(
            string definitionId,
            bool succeeded,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null ||
                !string.Equals(parameter.ObjectDefinitionId, definitionId, StringComparison.Ordinal))
            {
                return;
            }

            if (succeeded)
            {
                RefreshObjectDetectionParameterQuantityStatus(parameter);
                AppendObjectDetectionParameterQuantityToStatusLabel(definitionId);
                RefreshObjectDetectionMeasurementDisplay();
            }
            else
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "影像確認失敗：" + (errorMessage ?? "未知錯誤"),
                    Color.Firebrick);
            }
        }

        private void EnsureObjectDetectionParameterTabs()
        {
            if (objectDetectionParameterTabControl != null || imageLayoutPanel == null)
            {
                return;
            }

            objectDetectionParameterTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "objectDetectionParameterTabControl",
                Visible = false
            };
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "主參數設定",
                    "主參數設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "尺寸量測設定",
                    "尺寸量測設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "缺陷量測設定",
                    "缺陷量測設定將在此處配置。"));
            imageLayoutPanel.Controls.Add(objectDetectionParameterTabControl, 1, 0);
            objectDetectionParameterTabControl.BringToFront();
        }

        private static TabPage CreateObjectDetectionParameterTabPage(
            string title,
            string placeholderText)
        {
            var tabPage = new TabPage
            {
                Text = title,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(250, 251, 253)
            };
            var label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                Text = placeholderText,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            tabPage.Controls.Add(label);
            return tabPage;
        }

        private void UpdateObjectDetectionParameterTabs(ObjectDetectionParameterSettings parameter)
        {
            EnsureObjectDetectionParameterTabs();
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrEmpty(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            string sourceName = definition == null
                ? "未指定物件定義結果"
                : GetObjectDefinitionDisplayName(
                    definition,
                    systemParameters.ObjectDefinitions.IndexOf(definition));
            BuildObjectDetectionMainParameterTab(parameter, sourceName);
            BuildObjectDetectionSizeMeasurementTab(parameter, sourceName);
            SetObjectDetectionParameterTabText(
                2,
                "缺陷量測來源：" + sourceName + "。將使用已找出的物件結果。");
        }

        private void BuildObjectDetectionSizeMeasurementTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[1];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionObjectNumberPanel = null;
                objectDetectionMeasurementModeComboBox = null;
                objectDetectionMeasurementDirectionComboBox = null;
                objectDetectionMeasurementLineCountBox = null;
                objectDetectionMeasurementNameTextBox = null;
                objectDetectionMeasurementToolStatusLabel = null;
                objectDetectionMeasurementDrawButton = null;
                objectDetectionMeasurementRecordsGrid = null;
                objectDetectionMeasurementIsDrawing = false;
                objectDetectionMeasurementDrawingStage = 0;
                LoadPendingObjectDetectionMeasurementGeometry(parameter);

                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                tabPage.Controls.Add(contentPanel);

                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    Text = "尺寸量測來源：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };

                if (parameter.ColumnCount <= 0 || parameter.RowCount <= 0)
                {
                    var invalidQuantityLabel = new Label
                    {
                        Dock = DockStyle.Top,
                        AutoSize = false,
                        Height = 42,
                        Text = "請先在主參數設定輸入有效的列數與排數。",
                        TextAlign = ContentAlignment.TopLeft,
                        ForeColor = Color.Firebrick
                    };
                    contentPanel.Controls.Add(invalidQuantityLabel);
                    contentPanel.Controls.Add(sourceLabel);
                    return;
                }

                long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
                if (expectedCount > 10000)
                {
                    var tooManyObjectsLabel = new Label
                    {
                        Dock = DockStyle.Top,
                        AutoSize = false,
                        Height = 42,
                        Text = "序號按鈕數量過大，請先縮小列數與排數設定。",
                        TextAlign = ContentAlignment.TopLeft,
                        ForeColor = Color.Firebrick
                    };
                    contentPanel.Controls.Add(tooManyObjectsLabel);
                    contentPanel.Controls.Add(sourceLabel);
                    return;
                }

                if (selectedObjectDetectionNumber > expectedCount)
                {
                    selectedObjectDetectionNumber = -1;
                }

                var instruction = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 30,
                    Text = "請選擇 ROI／物件序號，左側各影像分頁會聚焦到對應物件。",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };

                var sourceMaskGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 166,
                    Text = "量測來源 MASK（套用到全部物件序號）",
                    Padding = new Padding(8)
                };

                var sourceMaskLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(0),
                    Margin = new Padding(0),
                    AutoSize = false
                };
                sourceMaskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
                sourceMaskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
                sourceMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

                var sourceMaskPrimary = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    sourceMaskPrimary.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(
                    sourceMaskPrimary,
                    parameter.SourceMaskPrimaryType,
                    parameter.SourceMaskPrimaryId,
                    parameter.SourceMaskPrimaryNamespace);

                var sourceMaskOperation = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("直接使用主要 MASK", "None"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("OR（A 加 B）", "Or"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("AND（A 與 B 交集）", "And"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("排除（A AND NOT B）", "Subtract"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("XOR（A 與 B 不同處）", "Xor"));
                sourceMaskOperation.Items.Add(new ObjectDefinitionOption("NOT（反相主要 MASK）", "Not"));
                SelectObjectDefinitionOption(
                    sourceMaskOperation,
                    parameter.SourceMaskOperation,
                    "None");

                var sourceMaskSecondary = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    sourceMaskSecondary.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(
                    sourceMaskSecondary,
                    parameter.SourceMaskSecondaryType,
                    parameter.SourceMaskSecondaryId,
                    parameter.SourceMaskSecondaryNamespace);

                var sourceMaskHint = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "選定後按套用，設定會同步套用到物件 1～6。",
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                var sourceMaskApply = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = "套用來源 MASK"
                };
                sourceMaskApply.Click += delegate
                {
                    ObjectDetectionMaskSourceChoice primary =
                        sourceMaskPrimary.SelectedItem as ObjectDetectionMaskSourceChoice;
                    ObjectDefinitionOption operation =
                        sourceMaskOperation.SelectedItem as ObjectDefinitionOption;
                    ObjectDetectionMaskSourceChoice secondary =
                        sourceMaskSecondary.SelectedItem as ObjectDetectionMaskSourceChoice;
                    if (primary == null || string.IsNullOrWhiteSpace(primary.SourceType) ||
                        string.IsNullOrWhiteSpace(primary.Id))
                    {
                        statusLabel.Text = "請先指定量測主要 MASK 來源";
                        return;
                    }

                    string operationValue = operation == null
                        ? "None"
                        : Convert.ToString(operation.Value, CultureInfo.InvariantCulture);
                    bool requiresSecondary = !string.Equals(operationValue, "None", StringComparison.Ordinal) &&
                        !string.Equals(operationValue, "Not", StringComparison.Ordinal);
                    if (requiresSecondary &&
                        (secondary == null || string.IsNullOrWhiteSpace(secondary.SourceType) ||
                         string.IsNullOrWhiteSpace(secondary.Id)))
                    {
                        statusLabel.Text = "請先指定量測次要 MASK 來源";
                        return;
                    }

                    parameter.SourceMaskMode = string.Equals(operationValue, "None", StringComparison.Ordinal)
                        ? "Direct"
                        : "Composite";
                    parameter.SourceMaskPrimaryType = primary.SourceType;
                    parameter.SourceMaskPrimaryId = primary.Id;
                    parameter.SourceMaskPrimaryNamespace = primary.SourceNamespace;
                    parameter.SourceMaskOperation = operationValue;
                    parameter.SourceMaskSecondaryType = requiresSecondary && secondary != null
                        ? secondary.SourceType
                        : string.Empty;
                    parameter.SourceMaskSecondaryId = requiresSecondary && secondary != null
                        ? secondary.Id
                        : string.Empty;
                    parameter.SourceMaskSecondaryNamespace = requiresSecondary && secondary != null
                        ? secondary.SourceNamespace
                        : string.Empty;
                    parameter.MeasurementSourceMaskDisplayName = primary.DisplayText;
                    if (operation != null &&
                        !string.Equals(operationValue, "None", StringComparison.Ordinal))
                    {
                        parameter.MeasurementSourceMaskDisplayName +=
                            " / " + operation.DisplayText;
                    }

                    if (requiresSecondary && secondary != null)
                    {
                        parameter.MeasurementSourceMaskDisplayName +=
                            " + " + secondary.DisplayText;
                    }
                    InvalidateObjectDetectionMeasurementMaskCache();
                    SaveSystemParameters();
                    UpdateObjectDetectionParameterTabs(parameter);
                    RefreshObjectDetectionMeasurementDisplay();
                    statusLabel.Text = parameter.DisplayName +
                        " 的量測來源 MASK 已套用，並同步所有物件序號";
                };
                sourceMaskLayout.Controls.Add(sourceMaskPrimary, 0, 0);
                sourceMaskLayout.Controls.Add(sourceMaskOperation, 1, 0);
                sourceMaskLayout.Controls.Add(sourceMaskSecondary, 0, 1);
                sourceMaskLayout.SetColumnSpan(sourceMaskSecondary, 2);
                sourceMaskLayout.Controls.Add(sourceMaskHint, 0, 2);
                sourceMaskLayout.SetColumnSpan(sourceMaskHint, 2);
                sourceMaskLayout.Controls.Add(sourceMaskApply, 0, 3);
                sourceMaskLayout.SetColumnSpan(sourceMaskApply, 2);
                sourceMaskGroup.Controls.Add(sourceMaskLayout);

                var measurementToolGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 250,
                    Text = "量測工具",
                    Padding = new Padding(8)
                };
                var measurementToolLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 7,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                measurementToolLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
                measurementToolLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
                for (int row = 0; row < 6; row++)
                {
                    measurementToolLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                }
                measurementToolLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                objectDetectionMeasurementNameTextBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Text = string.IsNullOrWhiteSpace(parameter.MeasurementName)
                        ? "量測線1"
                        : parameter.MeasurementName
                };
                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "量測名稱",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 0);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementNameTextBox, 1, 0);

                objectDetectionMeasurementDirectionComboBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                objectDetectionMeasurementDirectionComboBox.Items.Add("Horizontal");
                objectDetectionMeasurementDirectionComboBox.Items.Add("Vertical");
                objectDetectionMeasurementDirectionComboBox.SelectedItem =
                    string.Equals(parameter.MeasurementDirection, "Vertical", StringComparison.Ordinal)
                        ? "Vertical"
                        : "Horizontal";

                objectDetectionMeasurementModeComboBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                objectDetectionMeasurementModeComboBox.Items.Add("Single");
                objectDetectionMeasurementModeComboBox.Items.Add("Parallel");
                objectDetectionMeasurementModeComboBox.SelectedItem =
                    string.Equals(parameter.MeasurementMode, "Parallel", StringComparison.Ordinal)
                        ? "Parallel"
                        : "Single";

                objectDetectionMeasurementLineCountBox = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 1,
                    Maximum = 1000,
                    Value = Math.Max(1, Math.Min(1000, parameter.MeasurementLineCount))
                };

                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "方向",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 1);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementDirectionComboBox, 1, 1);
                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "量測模式",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 2);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementModeComboBox, 1, 2);
                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "平行線數量",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 3);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementLineCountBox, 1, 3);

                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                objectDetectionMeasurementDrawButton = new Button
                {
                    Text = "開始畫線",
                    Width = 128,
                    Height = 25,
                    Margin = new Padding(0, 1, 4, 1)
                };
                objectDetectionMeasurementDrawButton.Click += delegate
                {
                    if (objectDetectionMeasurementIsDrawing)
                    {
                        CancelObjectDetectionMeasurementDrawing();
                    }
                    else
                    {
                        BeginObjectDetectionMeasurementDrawing();
                    }
                };
                var clearMeasurementButton = new Button
                {
                    Text = "清除",
                    Width = 72,
                    Height = 25,
                    Margin = new Padding(0, 1, 4, 1)
                };
                clearMeasurementButton.Click += delegate
                {
                    pendingObjectDetectionMeasurementGeometry.Reset();
                    objectDetectionMeasurementIsDrawing = false;
                    objectDetectionMeasurementDrawingStage = 0;
                    objectDetectionMeasurementDrawButton.Text = "開始畫線";
                    objectDetectionMeasurementToolStatusLabel.Text = "尚未設定量測線";
                    objectDetectionMeasurementDisplayControl.InvalidateImageView();
                };
                buttonPanel.Controls.Add(objectDetectionMeasurementDrawButton);
                buttonPanel.Controls.Add(clearMeasurementButton);
                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "操作",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 4);
                measurementToolLayout.Controls.Add(buttonPanel, 1, 4);

                objectDetectionMeasurementToolStatusLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95),
                    Text = parameter.MeasurementLineConfigured
                        ? "已載入已套用的量測線設定"
                        : "尚未設定量測線"
                };
                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "狀態",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft
                }, 0, 5);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementToolStatusLabel, 1, 5);

                var measurementApplyCancelPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                var measurementApplyButton = new Button
                {
                    Text = "套用量測設定",
                    Width = 128,
                    Height = 26,
                    Margin = new Padding(0, 1, 4, 1)
                };
                measurementApplyButton.Click += delegate
                {
                    string mode = GetObjectDetectionMeasurementMode();
                    if (!pendingObjectDetectionMeasurementGeometry.HasFirstLine ||
                        (mode == "Parallel" &&
                         !pendingObjectDetectionMeasurementGeometry.HasSecondLine))
                    {
                        statusLabel.Text = mode == "Parallel"
                            ? "請先畫完兩條平行量測線"
                            : "請先畫出量測線";
                        return;
                    }

                    int lineCount = objectDetectionMeasurementLineCountBox == null
                        ? 1
                        : Decimal.ToInt32(objectDetectionMeasurementLineCountBox.Value);
                    if (mode == "Parallel")
                    {
                        lineCount = Math.Max(2, lineCount);
                        objectDetectionMeasurementLineCountBox.Value = lineCount;
                    }

                    SavePendingObjectDetectionMeasurementGeometry(
                        parameter,
                        pendingObjectDetectionMeasurementGeometry,
                        mode,
                        GetObjectDetectionMeasurementDirection(),
                        lineCount);
                    parameter.MeasurementName = GetObjectDetectionMeasurementName(parameter);
                    UpsertObjectDetectionMeasurementRecord(
                        parameter,
                        parameter.MeasurementName,
                        mode,
                        GetObjectDetectionMeasurementDirection(),
                        lineCount);
                    RefreshObjectDetectionMeasurementRecordsGrid(parameter);
                    objectDetectionMeasurementToolStatusLabel.Text =
                        "量測線設定已套用到下方表格，請按保存量測資料寫入參數檔";
                    statusLabel.Text = parameter.DisplayName +
                        " 的尺寸量測線設定已套用";
                    objectDetectionMeasurementDisplayControl.InvalidateImageView();
                };
                var measurementCancelButton = new Button
                {
                    Text = "取消",
                    Width = 72,
                    Height = 26,
                    Margin = new Padding(0, 1, 4, 1)
                };
                measurementCancelButton.Click += delegate
                {
                    CancelObjectDetectionMeasurementDrawing();
                    statusLabel.Text = parameter.DisplayName + " 已取消尺寸量測線修改";
                };
                measurementApplyCancelPanel.Controls.Add(measurementApplyButton);
                measurementApplyCancelPanel.Controls.Add(measurementCancelButton);
                measurementToolLayout.Controls.Add(measurementApplyCancelPanel, 0, 6);
                measurementToolLayout.SetColumnSpan(measurementApplyCancelPanel, 2);
                measurementToolGroup.Controls.Add(measurementToolLayout);

                var measurementRecordsGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 190,
                    Text = "量測資料紀錄",
                    Padding = new Padding(8)
                };
                objectDetectionMeasurementRecordsGrid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    ReadOnly = true,
                    MultiSelect = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoGenerateColumns = false,
                    RowHeadersVisible = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = SystemColors.Window,
                    BorderStyle = BorderStyle.FixedSingle
                };
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementName",
                        HeaderText = "名稱",
                        FillWeight = 25
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementMode",
                        HeaderText = "模式",
                        FillWeight = 18
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementDirection",
                        HeaderText = "方向",
                        FillWeight = 18
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementLineCount",
                        HeaderText = "線數",
                        FillWeight = 12
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementLineOrder",
                        HeaderText = "繪製方向",
                        FillWeight = 24
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementOutsideRoi",
                        HeaderText = "ROI外",
                        FillWeight = 18
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementSourceMask",
                        HeaderText = "使用 MASK",
                        FillWeight = 34
                    });
                objectDetectionMeasurementRecordsGrid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = "MeasurementId",
                        HeaderText = "ID",
                        FillWeight = 27
                    });
                objectDetectionMeasurementRecordsGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
                {
                    if (e.RowIndex < 0)
                    {
                        return;
                    }

                    LoadObjectDetectionMeasurementRecord(parameter, e.RowIndex);
                };
                var saveMeasurementRecordButton = new Button
                {
                    Dock = DockStyle.Bottom,
                    Height = 28,
                    Text = "保存量測資料"
                };
                saveMeasurementRecordButton.Click += delegate
                {
                    SaveObjectDetectionMeasurementRecord(parameter);
                };
                measurementRecordsGroup.Controls.Add(objectDetectionMeasurementRecordsGrid);
                measurementRecordsGroup.Controls.Add(saveMeasurementRecordButton);
                RefreshObjectDetectionMeasurementRecordsGrid(parameter);

                objectDetectionObjectNumberPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = parameter.ColumnCount,
                    RowCount = parameter.RowCount,
                    Padding = new Padding(0, 4, 0, 4),
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
                };
                for (int column = 0; column < parameter.ColumnCount; column++)
                {
                    objectDetectionObjectNumberPanel.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100f / parameter.ColumnCount));
                }

                for (int row = 0; row < parameter.RowCount; row++)
                {
                    objectDetectionObjectNumberPanel.RowStyles.Add(
                        new RowStyle(SizeType.Absolute, 36f));
                    for (int column = 0; column < parameter.ColumnCount; column++)
                    {
                        int number = (row * parameter.ColumnCount) + column + 1;
                        var button = new Button
                        {
                            Text = number.ToString(CultureInfo.InvariantCulture),
                            Dock = DockStyle.Fill,
                            Margin = new Padding(2),
                            Tag = number,
                            UseVisualStyleBackColor = true
                        };
                        button.Click += ObjectDetectionNumberButton_Click;
                        objectDetectionObjectNumberPanel.Controls.Add(button, column, row);
                    }
                }

                // Add in reverse order so DockStyle.Top renders the ROI number
                // buttons, MASK settings, then measurement tools from top to bottom.
                contentPanel.Controls.Add(measurementRecordsGroup);
                contentPanel.Controls.Add(measurementToolGroup);
                contentPanel.Controls.Add(sourceMaskGroup);
                contentPanel.Controls.Add(objectDetectionObjectNumberPanel);
                contentPanel.Controls.Add(instruction);
                contentPanel.Controls.Add(sourceLabel);
                UpdateObjectDetectionNumberButtonState(parameter);
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }

        private List<ObjectDetectionMaskSourceChoice> CreateObjectDetectionMaskSourceChoices(
            ObjectDetectionParameterSettings parameter)
        {
            var choices = new List<ObjectDetectionMaskSourceChoice>
            {
                new ObjectDetectionMaskSourceChoice
                {
                    DisplayText = "未指定來源 MASK",
                    SourceType = string.Empty,
                    Id = string.Empty
                }
            };
            var keys = new HashSet<string>(StringComparer.Ordinal);

            if (parameter != null && !string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
                if (definition != null)
                {
                    AddObjectDetectionMaskSourceChoice(
                        choices,
                        keys,
                        "物件定義結果：" +
                        GetObjectDefinitionDisplayName(
                            definition,
                            systemParameters.ObjectDefinitions.IndexOf(definition)),
                        "ObjectDefinition",
                        definition.Id,
                        string.Empty);

                    var relatedObjectJudgements = new List<ObjectJudgementSettings>();
                    string definitionSourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                        ? "ObjectJudgement"
                        : definition.SourceType;
                    if (string.Equals(definitionSourceType, "ObjectJudgement", StringComparison.Ordinal))
                    {
                        ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements.Find(
                            item => string.Equals(item.Id, definition.SourceId, StringComparison.Ordinal));
                        if (objectJudgement != null)
                        {
                            relatedObjectJudgements.Add(objectJudgement);
                        }
                    }
                    else if (string.Equals(definitionSourceType, "Group", StringComparison.Ordinal))
                    {
                        ObjectJudgementGroupSettings objectJudgementGroup =
                            FindObjectJudgementGroup(definition.SourceId);
                        if (objectJudgementGroup != null)
                        {
                            AddObjectDetectionObjectJudgementGroupChoices(
                                choices,
                                keys,
                                objectJudgementGroup.Id,
                                string.Empty,
                                new HashSet<string>(StringComparer.Ordinal));
                        }
                    }

                    foreach (ObjectJudgementSettings objectJudgement in relatedObjectJudgements)
                    {
                        int objectJudgementIndex = systemParameters.ObjectJudgements.IndexOf(objectJudgement);
                        AddObjectDetectionMaskSourceChoice(
                            choices,
                            keys,
                            "區塊：" + GetObjectJudgementDisplayName(objectJudgement, objectJudgementIndex),
                            "ObjectJudgement",
                            objectJudgement.Id,
                            string.Empty);

                        for (int processingIndex = 0;
                            processingIndex < objectJudgement.ProcessingSteps.Count;
                            processingIndex++)
                        {
                            ObjectJudgementProcessingSettings processing =
                                objectJudgement.ProcessingSteps[processingIndex];
                            AddObjectDetectionMaskSourceChoice(
                                choices,
                                keys,
                                "區塊：" +
                                GetObjectJudgementDisplayName(objectJudgement, objectJudgementIndex) +
                                "／" +
                                GetObjectJudgementProcessingDisplayName(processing, processingIndex),
                                "ObjectJudgementProcessing",
                                processing.Id,
                                objectJudgement.Id);
                        }

                        foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                        {
                            AddObjectDetectionRelationMaskSourceChoices(
                                choices,
                                keys,
                                relation);
                        }
                    }

                    if (!HasConfiguredObjectDefinitionBlockSource(definition))
                    {
                        if (string.Equals(definition.SourceRelationType, "Group", StringComparison.Ordinal))
                        {
                            ImageRelationGroupSettings relationGroup = FindImageRelationGroup(
                                definition.SourceRelationId);
                            if (relationGroup != null)
                            {
                                AddObjectDetectionMaskSourceChoice(
                                    choices,
                                    keys,
                                    "關聯群組：" + GetImageRelationGroupDisplayName(relationGroup),
                                    "RelationGroup",
                                    relationGroup.Id,
                                    string.Empty);
                            }
                        }

                        foreach (ImageRelationSettings relation in GetObjectDefinitionSourceRelations(definition))
                        {
                            AddObjectDetectionRelationMaskSourceChoices(
                                choices,
                                keys,
                                relation);
                        }
                    }
                }
            }

            return choices;
        }

        private void AddObjectDetectionObjectJudgementGroupChoices(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            string groupId,
            string parentPath,
            HashSet<string> visitedGroups)
        {
            if (string.IsNullOrWhiteSpace(groupId) ||
                visitedGroups == null ||
                !visitedGroups.Add(groupId))
            {
                return;
            }

            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            string groupName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName.Trim();
            string groupPath = string.IsNullOrWhiteSpace(parentPath)
                ? groupName
                : parentPath + "／" + groupName;

            AddObjectDetectionMaskSourceChoice(
                choices,
                keys,
                "區塊群組：" + groupPath,
                "ObjectJudgementGroup",
                group.Id,
                string.Empty);

            foreach (ObjectJudgementSettings objectJudgement in systemParameters.ObjectJudgements)
            {
                if (!string.Equals(objectJudgement.GroupId, group.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                int objectJudgementIndex = systemParameters.ObjectJudgements.IndexOf(objectJudgement);
                AddObjectDetectionMaskSourceChoice(
                    choices,
                    keys,
                    "區塊：" + groupPath + "／" +
                    GetObjectJudgementDisplayName(objectJudgement, objectJudgementIndex),
                    "ObjectJudgement",
                    objectJudgement.Id,
                    string.Empty);

                for (int processingIndex = 0;
                    processingIndex < objectJudgement.ProcessingSteps.Count;
                    processingIndex++)
                {
                    ObjectJudgementProcessingSettings processing =
                        objectJudgement.ProcessingSteps[processingIndex];
                    AddObjectDetectionMaskSourceChoice(
                        choices,
                        keys,
                        "區塊：" + groupPath + "／" +
                        GetObjectJudgementProcessingDisplayName(processing, processingIndex),
                        "ObjectJudgementProcessing",
                        processing.Id,
                        objectJudgement.Id);
                }

                foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                {
                    AddObjectDetectionRelationMaskSourceChoices(
                        choices,
                        keys,
                        relation);
                }
            }

            foreach (ObjectJudgementGroupSettings childGroup in systemParameters.ObjectJudgementGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    AddObjectDetectionObjectJudgementGroupChoices(
                        choices,
                        keys,
                        childGroup.Id,
                        groupPath,
                        visitedGroups);
                }
            }
        }

        private void AddObjectDetectionRelationMaskSourceChoices(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            ImageRelationSettings relation)
        {
            if (relation == null)
            {
                return;
            }

            string relationName = string.IsNullOrWhiteSpace(relation.DisplayName)
                ? "未命名關聯"
                : relation.DisplayName.Trim();
            string relationNamespace = CreateImageRelationSourceNamespace(relation);
            AddObjectDetectionMaskSourceChoice(
                choices,
                keys,
                "關聯：" + relationName,
                "Relation",
                relation.Id,
                string.Empty);

            if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal))
            {
                AddObjectDetectionImageProcessingGroupChoices(
                    choices,
                    keys,
                    relation.ProcessingId,
                    relationNamespace,
                    string.Empty);
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                item => string.Equals(item.Id, relation.ProcessingId, StringComparison.Ordinal));
            if (step == null || !IsBinaryMaskProcessingMethod(step.Method))
            {
                return;
            }

            int stepIndex = systemParameters.ImageProcessingSteps.IndexOf(step);
            AddObjectDetectionMaskSourceChoice(
                choices,
                keys,
                "影像處理：" + GetObjectDetectionImageProcessingStepName(step, stepIndex),
                "ImageProcessingStep",
                step.Id,
                relationNamespace);
        }

        private void AddObjectDetectionImageProcessingGroupChoices(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            string groupId,
            string sourceNamespace,
            string parentPath)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            string groupName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName.Trim();
            string groupPath = string.IsNullOrWhiteSpace(parentPath)
                ? groupName
                : parentPath + "／" + groupName;
            List<ImageProcessingStepSettings> groupSteps = new List<ImageProcessingStepSettings>();
            CollectImageProcessingGroupSteps(group.Id, groupSteps);
            if (groupSteps.Any(step => step != null && IsBinaryMaskProcessingMethod(step.Method)))
            {
                AddObjectDetectionMaskSourceChoice(
                    choices,
                    keys,
                    "影像處理－群組(" + groupPath + ")",
                    "ImageProcessingGroup",
                    group.Id,
                    sourceNamespace);
            }

            foreach (ImageProcessingStepSettings step in systemParameters.ImageProcessingSteps)
            {
                if (!string.Equals(step.GroupId, group.Id, StringComparison.Ordinal) ||
                    !IsBinaryMaskProcessingMethod(step.Method))
                {
                    continue;
                }

                int stepIndex = systemParameters.ImageProcessingSteps.IndexOf(step);
                AddObjectDetectionMaskSourceChoice(
                    choices,
                    keys,
                    "影像處理－群組(" + groupPath + ")－" +
                    GetObjectDetectionImageProcessingStepName(step, stepIndex),
                    "ImageProcessingGroupStep",
                    step.Id,
                    sourceNamespace);
            }

            foreach (ImageProcessingGroupSettings child in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    AddObjectDetectionImageProcessingGroupChoices(
                        choices,
                        keys,
                        child.Id,
                        sourceNamespace,
                        groupPath);
                }
            }
        }

        private static string GetObjectDetectionImageProcessingStepName(
            ImageProcessingStepSettings step,
            int stepIndex)
        {
            return step == null || string.IsNullOrWhiteSpace(step.DisplayName)
                ? "處理" + (stepIndex + 1).ToString(CultureInfo.InvariantCulture)
                : step.DisplayName.Trim();
        }

        private static string GetImageRelationGroupDisplayName(ImageRelationGroupSettings group)
        {
            return group == null || string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName.Trim();
        }

        private static void AddObjectDetectionMaskSourceChoice(
            List<ObjectDetectionMaskSourceChoice> choices,
            HashSet<string> keys,
            string displayText,
            string sourceType,
            string id,
            string sourceNamespace)
        {
            string key = string.Join(
                "|",
                sourceType ?? string.Empty,
                id ?? string.Empty,
                sourceNamespace ?? string.Empty);
            if (string.IsNullOrWhiteSpace(id) || !keys.Add(key))
            {
                return;
            }

            choices.Add(new ObjectDetectionMaskSourceChoice
            {
                DisplayText = displayText,
                SourceType = sourceType,
                Id = id,
                SourceNamespace = sourceNamespace ?? string.Empty
            });
        }

        private static void SelectObjectDetectionMaskSource(
            ComboBox comboBox,
            string sourceType,
            string sourceId,
            string sourceNamespace)
        {
            comboBox.SelectedIndex = 0;
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                ObjectDetectionMaskSourceChoice choice =
                    comboBox.Items[index] as ObjectDetectionMaskSourceChoice;
                if (choice != null &&
                    string.Equals(choice.SourceType, sourceType, StringComparison.Ordinal) &&
                    string.Equals(choice.Id, sourceId, StringComparison.Ordinal) &&
                    string.Equals(choice.SourceNamespace, sourceNamespace, StringComparison.Ordinal))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }
        }

        private void ObjectDetectionNumberButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !(button.Tag is int))
            {
                return;
            }

            int number = (int)button.Tag;
            selectedObjectDetectionNumber = number;
            InvalidateObjectDetectionMeasurementMaskCache();
            UpdateObjectDetectionNumberButtonState(
                FindObjectDetectionParameter(activeObjectDetectionParameterId));

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDefinitionSettings definition = parameter == null ||
                string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            ObjectDefinitionDetectedObject detectedObject;
            if (definition == null ||
                !TryGetCompletedObjectDefinitionObject(
                    definition,
                    number,
                    out detectedObject))
            {
                statusLabel.Text = "物件" + number.ToString(CultureInfo.InvariantCulture) +
                    " 尚未找到可用的物件結果";
                return;
            }

            if (leftImageTabControl != null &&
                objectDetectionMeasurementTabPage != null &&
                leftImageTabControl.TabPages.Contains(objectDetectionMeasurementTabPage))
            {
                leftImageTabControl.SelectedTab = objectDetectionMeasurementTabPage;
            }

            RefreshObjectDetectionMeasurementDisplay();
            FocusObjectDetectionImage(detectedObject.Bounds);
            statusLabel.Text = parameter.DisplayName + "：目前顯示物件" +
                number.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateObjectDetectionNumberButtonState(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionObjectNumberPanel == null)
            {
                return;
            }

            foreach (Control control in objectDetectionObjectNumberPanel.Controls)
            {
                Button button = control as Button;
                if (button == null || !(button.Tag is int))
                {
                    continue;
                }

                int number = (int)button.Tag;
                button.BackColor = number == selectedObjectDetectionNumber
                    ? Color.FromArgb(190, 220, 255)
                    : SystemColors.Control;
                button.FlatStyle = number == selectedObjectDetectionNumber
                    ? FlatStyle.Flat
                    : FlatStyle.Standard;
            }
        }

        private bool TryGetCompletedObjectDefinitionObject(
            ObjectDefinitionSettings definition,
            int number,
            out ObjectDefinitionDetectedObject detectedObject)
        {
            detectedObject = null;
            if (definition == null || number <= 0)
            {
                return false;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(
                        completedObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                foreach (List<ObjectDefinitionDetectedObject> items in objectDefinitionResults.Values)
                {
                    if (items == null)
                    {
                        continue;
                    }

                    detectedObject = items.FirstOrDefault(item => item.Number == number);
                    if (detectedObject != null)
                    {
                        return true;
                    }
                }
            }

            detectedObject = null;
            return false;
        }

        private void FocusObjectDetectionImage(Rectangle objectBounds)
        {
            ImageDisplayControl[] displays =
            {
                leftOriginalDisplayControl,
                leftPreprocessedDisplayControl,
                leftProcessedDisplayControl,
                leftBlockProcessingDisplayControl,
                objectDetectionMeasurementDisplayControl
            };

            ImageDisplayControl anchor = displays.FirstOrDefault(
                display => display != null && display.HasImage);
            if (anchor == null)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                anchor.FocusOnSourceRectangle(objectBounds);
                ImageViewState viewState = anchor.ViewState;
                sharedImageViewState = viewState;
                hasSharedImageViewState = true;
                foreach (ImageDisplayControl display in displays)
                {
                    if (display != null && display.HasImage)
                    {
                        display.ApplyViewState(viewState);
                    }
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void BuildObjectDetectionMainParameterTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[0];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionImageCheckStatusLabel = null;
                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    Text = "來源物件定義結果：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                tabPage.Controls.Add(sourceLabel);

                var group = new GroupBox
                {
                    Text = "數量設定",
                    Dock = DockStyle.Top,
                    Height = 132,
                    Padding = new Padding(10)
                };

                var columnLabel = new Label
                {
                    Text = "列：",
                    Left = 12,
                    Top = 28,
                    Width = 48,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var columnBox = new NumericUpDown
                {
                    Left = 62,
                    Top = 26,
                    Width = 110,
                    Minimum = 0,
                    Maximum = 10000,
                    Value = Math.Max(0, Math.Min(10000, parameter.ColumnCount))
                };
                var rowLabel = new Label
                {
                    Text = "排：",
                    Left = 198,
                    Top = 28,
                    Width = 48,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var rowBox = new NumericUpDown
                {
                    Left = 248,
                    Top = 26,
                    Width = 110,
                    Minimum = 0,
                    Maximum = 10000,
                    Value = Math.Max(0, Math.Min(10000, parameter.RowCount))
                };
                group.Controls.Add(columnLabel);
                group.Controls.Add(columnBox);
                group.Controls.Add(rowLabel);
                group.Controls.Add(rowBox);

                var confirm = new Button
                {
                    Text = "確認",
                    Left = 12,
                    Top = 70,
                    Width = 346,
                    Height = 28
                };
                confirm.Click += delegate
                {
                    parameter.ColumnCount = Decimal.ToInt32(columnBox.Value);
                    parameter.RowCount = Decimal.ToInt32(rowBox.Value);
                    SaveSystemParameters();
                    statusLabel.Text = parameter.DisplayName +
                        " 已套用數量設定：列 " + parameter.ColumnCount.ToString(CultureInfo.InvariantCulture) +
                        "、排 " + parameter.RowCount.ToString(CultureInfo.InvariantCulture);
                    UpdateObjectDetectionParameterTabs(parameter);
                    RefreshObjectDetectionParameterQuantityStatus(parameter);
                    AppendObjectDetectionParameterQuantityToStatusLabel(parameter.ObjectDefinitionId);
                };
                group.Controls.Add(confirm);

                objectDetectionImageCheckStatusLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 64,
                    Text = "尚未進行影像確認",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                contentPanel.Controls.Add(objectDetectionImageCheckStatusLabel);
                contentPanel.Controls.Add(group);
                contentPanel.Controls.Add(sourceLabel);
                tabPage.Controls.Add(contentPanel);
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }

        private void SetObjectDetectionParameterTabText(int tabIndex, string text)
        {
            if (objectDetectionParameterTabControl == null ||
                tabIndex < 0 ||
                tabIndex >= objectDetectionParameterTabControl.TabPages.Count)
            {
                return;
            }

            Label label = objectDetectionParameterTabControl.TabPages[tabIndex]
                .Controls
                .OfType<Label>()
                .FirstOrDefault();
            if (label != null)
            {
                label.Text = text;
            }
        }

        private void HideObjectDetectionParameterPanel()
        {
            if (objectDetectionParameterPanel != null)
            {
                objectDetectionParameterPanel.Visible = false;
            }
        }

        private void SetObjectDetectionParameterDisplayMode(bool enabled)
        {
            if (imageLayoutPanel == null || leftImageTabControl == null || rightImageTabControl == null)
            {
                return;
            }

            EnsureObjectDetectionMeasurementDisplay();
            isObjectDetectionParameterImageLayout = enabled;
            if (!enabled)
            {
                InvalidateObjectDetectionMeasurementMaskCache();
            }
            EnsureObjectDetectionParameterTabs();
            if (isImageViewerMaximized)
            {
                SetImageViewerMaximized(false, false);
            }

            imageLayoutPanel.SuspendLayout();
            try
            {
                if (enabled)
                {
                    leftImageTabControl.TabPages.Clear();
                    leftImageTabControl.TabPages.Add(leftOriginalTabPage);
                    leftImageTabControl.TabPages.Add(objectDetectionMeasurementTabPage);
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    objectDetectionMeasurementDisplayControl.TitleText = "左側 待量測";
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = false;
                    objectDetectionParameterTabControl.Visible = true;
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                }
                else
                {
                    leftImageTabControl.TabPages.Clear();
                    leftImageTabControl.TabPages.Add(leftOriginalTabPage);
                    leftImageTabControl.TabPages.Add(leftPreprocessedTabPage);
                    leftImageTabControl.TabPages.Add(leftProcessedTabPage);
                    leftImageTabControl.TabPages.Add(leftBlockProcessingTabPage);
                    leftImageTabControl.TabPages.Add(leftObjectsTabPage);
                    leftImageTabControl.TabPages.Add(leftDebugTabPage);
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    if (leftObjectsDisplayControl != null)
                    {
                        leftObjectsDisplayControl.TitleText = "左側 區塊結果";
                    }
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = true;
                    if (objectDetectionParameterTabControl != null)
                    {
                        objectDetectionParameterTabControl.Visible = false;
                    }
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                    imageLayoutPanel.SetColumnSpan(rightImageTabControl, 1);
                }
            }
            finally
            {
                imageLayoutPanel.ResumeLayout(true);
            }

            ImageDisplayControl activeDisplay = GetVisibleLeftImageDisplayControl();
            if (activeDisplay != null)
            {
                activeDisplay.InvalidateImageView();
            }

            if (enabled)
            {
                RefreshObjectDetectionMeasurementDisplay();
            }
        }

        private sealed class ObjectDetectionDefinitionChoice
        {
            public string DisplayText { get; set; }

            public string Id { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class ObjectDetectionMaskSourceChoice
        {
            public string DisplayText { get; set; }
            public string Id { get; set; }
            public string SourceType { get; set; }
            public string SourceNamespace { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class ObjectDetectionMeasurementGeometry
        {
            public bool HasFirstLine { get; set; }

            public bool HasSecondLine { get; set; }

            public double StartX { get; set; }

            public double StartY { get; set; }

            public double EndX { get; set; }

            public double EndY { get; set; }

            public double SecondStartX { get; set; }

            public double SecondStartY { get; set; }

            public double SecondEndX { get; set; }

            public double SecondEndY { get; set; }

            public void Reset()
            {
                HasFirstLine = false;
                HasSecondLine = false;
                StartX = 0;
                StartY = 0;
                EndX = 1;
                EndY = 0;
                SecondStartX = 0;
                SecondStartY = 0;
                SecondEndX = 1;
                SecondEndY = 0;
                LineOrder = "LeftToRight";
                SecondLineOrder = "LeftToRight";
                StartOutsideRoi = false;
                EndOutsideRoi = false;
                SecondStartOutsideRoi = false;
                SecondEndOutsideRoi = false;
            }

            public string LineOrder { get; set; }

            public string SecondLineOrder { get; set; }

            public bool StartOutsideRoi { get; set; }

            public bool EndOutsideRoi { get; set; }

            public bool SecondStartOutsideRoi { get; set; }

            public bool SecondEndOutsideRoi { get; set; }
        }

        private struct ObjectDetectionMeasurementFrame
        {
            public ObjectDetectionMeasurementFrame(
                PointF center,
                float width,
                float height,
                double angleDegrees)
            {
                Center = center;
                Width = Math.Max(1f, width);
                Height = Math.Max(1f, height);
                double radians = angleDegrees * Math.PI / 180.0;
                Cosine = (float)Math.Cos(radians);
                Sine = (float)Math.Sin(radians);
            }

            public PointF Center { get; private set; }

            public float Width { get; private set; }

            public float Height { get; private set; }

            private float Cosine { get; set; }

            private float Sine { get; set; }

            public PointF ToLocal(float imageX, float imageY)
            {
                float dx = imageX - Center.X;
                float dy = imageY - Center.Y;
                return new PointF(
                    (Cosine * dx) + (Sine * dy),
                    (-Sine * dx) + (Cosine * dy));
            }

            public PointF ToLocal(int imageX, int imageY)
            {
                return ToLocal((float)imageX, (float)imageY);
            }

            public PointF ToLocal(Point imagePoint)
            {
                return ToLocal(imagePoint.X, imagePoint.Y);
            }

            public PointF ToNormalizedLocal(int imageX, int imageY)
            {
                PointF local = ToLocal(imageX, imageY);
                return new PointF(
                    0.5f + (local.X / Width),
                    0.5f + (local.Y / Height));
            }

            public PointF ToImage(double normalizedX, double normalizedY)
            {
                float localX = (float)((normalizedX - 0.5) * Width);
                float localY = (float)((normalizedY - 0.5) * Height);
                return ToImage(localX, localY);
            }

            public PointF ToImage(float localX, float localY)
            {
                return new PointF(
                    Center.X + (Cosine * localX) - (Sine * localY),
                    Center.Y + (Sine * localX) + (Cosine * localY));
            }
        }

        private struct ObjectDetectionImageLine
        {
            public ObjectDetectionImageLine(int x1, int y1, int x2, int y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }

            public ObjectDetectionImageLine(PointF start, PointF end)
                : this(
                    (int)Math.Round(start.X),
                    (int)Math.Round(start.Y),
                    (int)Math.Round(end.X),
                    (int)Math.Round(end.Y))
            {
            }

            public int X1 { get; private set; }

            public int Y1 { get; private set; }

            public int X2 { get; private set; }

            public int Y2 { get; private set; }
        }
    }
}
