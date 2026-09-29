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
        private ObjectDetectionMeasurementRecordSettings CreateObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            string name,
            string mode,
            string direction,
            int lineCount,
            string lengthMode)
        {
            return new ObjectDetectionMeasurementRecordSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                Number = GetNextObjectDetectionMeasurementNumber(parameter),
                Name = name,
                Mode = mode,
                Direction = GetObjectDetectionMeasurementGeometryDirection(
                    pendingObjectDetectionMeasurementGeometry),
                LineCount = lineCount,
                LengthMode = NormalizeObjectDetectionMeasurementLengthMode(lengthMode),
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
                CreateObjectDetectionMeasurementRecord(
                    parameter,
                    name,
                    mode,
                    direction,
                    lineCount,
                    parameter.MeasurementLengthMode);
            ObjectDetectionMeasurementRecordSettings target = null;
            if (!objectDetectionMeasurementCreateNewRecord &&
                !string.IsNullOrWhiteSpace(objectDetectionMeasurementAppliedRecordId))
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

            objectDetectionMeasurementCreateNewRecord = false;
            ClearObjectDetectionMeasurementClipCache();
        }

        private static void CopyObjectDetectionMeasurementRecord(
            ObjectDetectionMeasurementRecordSettings source,
            ObjectDetectionMeasurementRecordSettings target)
        {
            target.Name = source.Name;
            target.Mode = source.Mode;
            target.Direction = source.Direction;
            target.LineCount = source.LineCount;
            target.LengthMode = source.LengthMode;
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
                    record.Number,
                    record.Name,
                    record.Mode == "Parallel" ? "平行線" : "單線",
                    direction == "Vertical" ? "垂直" : "水平",
                     record.LineCount,
                     GetObjectDetectionMeasurementLengthModeText(record.LengthMode),
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
            parameter.MeasurementLengthMode = NormalizeObjectDetectionMeasurementLengthMode(record.LengthMode);
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
            objectDetectionMeasurementCreateNewRecord = false;
            LoadPendingObjectDetectionMeasurementGeometry(parameter);
            if (objectDetectionMeasurementClipLinesCheckBox != null)
            {
                objectDetectionMeasurementClipLinesCheckBox.Enabled = true;
            }
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
            if (objectDetectionMeasurementLengthModeComboBox != null)
            {
                objectDetectionMeasurementLengthModeComboBox.SelectedItem =
                    GetObjectDetectionMeasurementLengthModeText(record.LengthMode);
            }

            objectDetectionMeasurementToolStatusLabel.Text =
                "已載入紀錄：" + parameter.MeasurementName +
                "，MASK：" + parameter.MeasurementSourceMaskDisplayName;
            if (parameter.MeasurementClipLinesToMask)
            {
                PrepareObjectDetectionMeasurementClipLines(parameter, record);
            }
            objectDetectionMeasurementDisplayControl.InvalidateImageView();
        }

        private ObjectDetectionMeasurementRecordSettings GetActiveObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || parameter.MeasurementRecords == null)
            {
                return null;
            }

            ObjectDetectionMeasurementRecordSettings record =
                parameter.MeasurementRecords.FirstOrDefault(
                    item => item != null && string.Equals(
                        item.Id,
                        objectDetectionMeasurementAppliedRecordId,
                        StringComparison.Ordinal));
            if (record != null)
            {
                return record;
            }

            return parameter.MeasurementRecords.FirstOrDefault(
                item => IsObjectDetectionMeasurementRecordDisplayed(parameter, item));
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

        private void RenameObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            string recordId)
        {
            if (parameter == null || parameter.MeasurementRecords == null ||
                string.IsNullOrWhiteSpace(recordId))
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

            string name = PromptForText("編輯量測紀錄", "量測名稱", record.Name);
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            record.Name = name.Trim();
            if (string.Equals(
                objectDetectionMeasurementAppliedRecordId,
                record.Id,
                StringComparison.Ordinal))
            {
                parameter.MeasurementName = record.Name;
                if (objectDetectionMeasurementNameTextBox != null)
                {
                    objectDetectionMeasurementNameTextBox.Text = record.Name;
                }
            }

            SaveSystemParameters();
            RefreshObjectDetectionMeasurementRecordsGrid(parameter);
            if (objectDetectionMeasurementRecordsGrid != null)
            {
                foreach (DataGridViewRow row in objectDetectionMeasurementRecordsGrid.Rows)
                {
                    string rowRecordId = Convert.ToString(
                        row.Cells["MeasurementId"].Value,
                        CultureInfo.InvariantCulture);
                    if (!string.Equals(rowRecordId, record.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    row.Selected = true;
                    objectDetectionMeasurementRecordsGrid.CurrentCell =
                        row.Cells["MeasurementName"];
                    break;
                }
            }

            objectDetectionMeasurementToolStatusLabel.Text =
                "已更新量測紀錄名稱：「" + record.Name + "」";
            statusLabel.Text = parameter.DisplayName +
                " 已更新量測紀錄名稱：「" + record.Name + "」";
        }

        private void DeleteObjectDetectionMeasurementRecord(
            ObjectDetectionParameterSettings parameter,
            string recordId)
        {
            if (parameter == null || parameter.MeasurementRecords == null ||
                string.IsNullOrWhiteSpace(recordId))
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

            if (MessageBox.Show(
                    this,
                    "確定要刪除量測紀錄「" + record.Name + "」嗎？\r\n\r\n使用的 MASK 來源資訊也會一併刪除。",
                    "刪除量測紀錄",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            bool clearDisplayedMeasurement =
                string.Equals(
                    objectDetectionMeasurementAppliedRecordId,
                    recordId,
                    StringComparison.Ordinal) ||
                IsObjectDetectionMeasurementRecordDisplayed(parameter, record);
            parameter.MeasurementRecords.Remove(record);
            if (clearDisplayedMeasurement)
            {
                objectDetectionMeasurementAppliedRecordId = null;
                ClearObjectDetectionMeasurementClipCache();
                ClearObjectDetectionMeasurementGeometry(parameter);
                pendingObjectDetectionMeasurementGeometry.Reset();
                objectDetectionMeasurementIsDrawing = false;
                objectDetectionMeasurementDrawingStage = 0;
                if (objectDetectionMeasurementDrawButton != null)
                {
                    objectDetectionMeasurementDrawButton.Text = "開始畫線";
                }
                if (objectDetectionMeasurementClipLinesCheckBox != null)
                {
                    objectDetectionMeasurementClipLinesCheckBox.Enabled = false;
                }
            }

            SaveSystemParameters();
            RefreshObjectDetectionMeasurementRecordsGrid(parameter);
            if (clearDisplayedMeasurement && objectDetectionMeasurementDisplayControl != null)
            {
                objectDetectionMeasurementDisplayControl.InvalidateImageView();
            }
            objectDetectionMeasurementToolStatusLabel.Text =
                "已刪除量測紀錄：" + record.Name;
            statusLabel.Text = parameter.DisplayName +
                " 已刪除量測紀錄：" + record.Name;
        }

        private static bool IsObjectDetectionMeasurementRecordDisplayed(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record)
        {
            if (parameter == null || record == null || !parameter.MeasurementLineConfigured)
            {
                return false;
            }

            const double tolerance = 0.000001;
            return string.Equals(parameter.MeasurementMode, record.Mode, StringComparison.Ordinal) &&
                Math.Abs(parameter.MeasurementStartX - record.StartX) <= tolerance &&
                Math.Abs(parameter.MeasurementStartY - record.StartY) <= tolerance &&
                Math.Abs(parameter.MeasurementEndX - record.EndX) <= tolerance &&
                Math.Abs(parameter.MeasurementEndY - record.EndY) <= tolerance &&
                Math.Abs(parameter.MeasurementSecondStartX - record.SecondStartX) <= tolerance &&
                Math.Abs(parameter.MeasurementSecondStartY - record.SecondStartY) <= tolerance &&
                Math.Abs(parameter.MeasurementSecondEndX - record.SecondEndX) <= tolerance &&
                Math.Abs(parameter.MeasurementSecondEndY - record.SecondEndY) <= tolerance;
        }

        private static void ClearObjectDetectionMeasurementGeometry(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null)
            {
                return;
            }

            parameter.MeasurementLineConfigured = false;
            parameter.MeasurementMode = "Single";
            parameter.MeasurementDirection = "Horizontal";
            parameter.MeasurementLineCount = 1;
            parameter.MeasurementStartX = 0;
            parameter.MeasurementStartY = 0;
            parameter.MeasurementEndX = 1;
            parameter.MeasurementEndY = 0;
            parameter.MeasurementSecondStartX = 0;
            parameter.MeasurementSecondStartY = 0;
            parameter.MeasurementSecondEndX = 1;
            parameter.MeasurementSecondEndY = 0;
            parameter.MeasurementLineOrder = "LeftToRight";
            parameter.MeasurementSecondLineOrder = "LeftToRight";
            parameter.MeasurementStartOutsideRoi = false;
            parameter.MeasurementEndOutsideRoi = false;
            parameter.MeasurementSecondStartOutsideRoi = false;
            parameter.MeasurementSecondEndOutsideRoi = false;
        }

        private void FlashObjectDetectionMeasurementRecord()
        {
            if (objectDetectionMeasurementHighlightTimer == null ||
                objectDetectionMeasurementDisplayControl == null)
            {
                return;
            }

            objectDetectionMeasurementHighlightVisible = true;
            objectDetectionMeasurementHighlightTimer.Stop();
            objectDetectionMeasurementHighlightTimer.Start();
            objectDetectionMeasurementDisplayControl.InvalidateImageView();
        }


    }
}
