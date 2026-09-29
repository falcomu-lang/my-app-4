using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        [Flags]
        private enum ObjectDetectionDefectRegionEdges
        {
            None = 0,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8
        }

        private Button objectDetectionDefectRegionDrawButton;
        private Button objectDetectionDefectRegionApplyButton;
        private Button objectDetectionDefectRegionCancelButton;
        private Button objectDetectionDefectRegionClearButton;
        private Button objectDetectionDefectRunButton;
        private CheckBox objectDetectionDefectParallelExecutionCheckBox;
        private Label objectDetectionDefectRegionStatusLabel;
        private bool objectDetectionDefectRegionDrawMode;
        private bool objectDetectionDefectRegionIsDrawing;
        private bool objectDetectionDefectRegionIsResizing;
        private bool objectDetectionDefectRegionDraftConfigured;
        private ObjectDetectionDefectRegionEdges objectDetectionDefectRegionResizeEdges;
        private Point objectDetectionDefectRegionDrawStart;
        private Point objectDetectionDefectRegionDrawCurrent;
        private RectangleF objectDetectionDefectRegionDraft;
        private string objectDetectionDefectRegionDraftParameterId;
        private int objectDetectionDefectRegionDraftObjectNumber;

        private Control BuildObjectDetectionDefectInspectionRegionPanel(
            ObjectDetectionParameterSettings parameter)
        {
            if (!string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                ResetObjectDetectionDefectRegionDraft();
                objectDetectionDefectRegionDraftParameterId = parameter.Id;
            }

            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 158,
                Text = "檢測範圍",
                Padding = new Padding(8, 18, 8, 6)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var instruction = new Label
            {
                Dock = DockStyle.Fill,
                Text = "按住 Ctrl 拖曳框選；抓住框線或角落可微調範圍。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            objectDetectionDefectRegionDrawButton = new Button
            {
                AutoSize = false,
                Width = 104,
                Height = 27,
                Text = "框選範圍",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionApplyButton = new Button
            {
                AutoSize = false,
                Width = 62,
                Height = 27,
                Text = "套用",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionCancelButton = new Button
            {
                AutoSize = false,
                Width = 62,
                Height = 27,
                Text = "取消",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionClearButton = new Button
            {
                AutoSize = false,
                Width = 62,
                Height = 27,
                Text = "清除",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionDrawButton.Click +=
                ObjectDetectionDefectRegionDrawButton_Click;
            objectDetectionDefectRegionApplyButton.Click +=
                ObjectDetectionDefectRegionApplyButton_Click;
            objectDetectionDefectRegionCancelButton.Click +=
                ObjectDetectionDefectRegionCancelButton_Click;
            objectDetectionDefectRegionClearButton.Click +=
                ObjectDetectionDefectRegionClearButton_Click;
            buttons.Controls.Add(objectDetectionDefectRegionDrawButton);
            buttons.Controls.Add(objectDetectionDefectRegionApplyButton);
            buttons.Controls.Add(objectDetectionDefectRegionCancelButton);
            buttons.Controls.Add(objectDetectionDefectRegionClearButton);

            objectDetectionDefectRegionStatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            layout.Controls.Add(instruction, 0, 0);
            layout.Controls.Add(buttons, 0, 1);
            layout.Controls.Add(objectDetectionDefectRegionStatusLabel, 0, 2);
            group.Controls.Add(layout);

            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            return group;
        }

        private Control BuildObjectDetectionDefectExecutionPanel(
            ObjectDetectionParameterSettings parameter)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(8, 4, 8, 2)
            };
            var executionOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                FlowDirection = FlowDirection.LeftToRight
            };
            objectDetectionDefectParallelExecutionCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "平行運算（物件 x 四核心）",
                Checked = parameter.DefectParallelExecutionEnabled,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 8, 0)
            };
            objectDetectionDefectParallelExecutionCheckBox.CheckedChanged += delegate
            {
                ObjectDetectionParameterSettings current = FindObjectDetectionParameter(parameter.Id);
                if (current == null)
                {
                    return;
                }
                current.DefectParallelExecutionEnabled =
                    objectDetectionDefectParallelExecutionCheckBox.Checked;
                SaveSystemParameters();
            };
            objectDetectionDefectRunButton = new Button
            {
                AutoSize = false,
                Width = 128,
                Height = 27,
                Text = "執行四核心檢測",
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            objectDetectionDefectRunButton.Click += async delegate
            {
                ObjectDetectionParameterSettings current =
                    FindObjectDetectionParameter(parameter.Id);
                if (current != null)
                {
                    await RunObjectDetectionDefectProcessingAsync(current.Id, null);
                }
            };
            executionOptions.Controls.Add(objectDetectionDefectParallelExecutionCheckBox);
            executionOptions.Controls.Add(objectDetectionDefectRunButton);
            panel.Controls.Add(executionOptions);
            return panel;
        }

        private void ObjectDetectionDefectRegionDrawButton_Click(object sender, EventArgs e)
        {
            if (objectDetectionDefectRegionDrawMode)
            {
                objectDetectionDefectRegionDrawMode = false;
                objectDetectionDefectRegionIsDrawing = false;
                objectDetectionDefectRegionIsResizing = false;
                objectDetectionDefectRegionDrawStart = Point.Empty;
                if (objectDetectionDefectDisplayControl != null)
                {
                    objectDetectionDefectDisplayControl.ReleaseViewerPointer();
                }
                objectDetectionDefectRegionDrawButton.Text = "框選範圍";
                SetObjectDetectionDefectRegionStatus("已停止編輯；尚未套用的範圍仍可套用或取消。");
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDefinitionDetectedObject selectedObject;
            if (parameter == null ||
                !TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                SetObjectDetectionDefectRegionStatus("請先選擇已找到的物件序號。");
                return;
            }

            if (IsImageDisplayUpdateSuppressed)
            {
                SetObjectDetectionDefectRegionStatus("目前勾選了不顯示畫面，請先取消勾選再框選。");
                return;
            }

            if (leftImageTabControl == null || objectDetectionDefectDisplayTabPage == null)
            {
                SetObjectDetectionDefectRegionStatus("缺陷顯示畫面尚未準備完成。");
                return;
            }

            leftImageTabControl.SelectedTab = objectDetectionDefectDisplayTabPage;
            RefreshObjectDetectionDefectDisplay();
            if (objectDetectionDefectDisplayControl == null ||
                !objectDetectionDefectDisplayControl.HasImage ||
                !string.Equals(
                    objectDetectionDefectDisplayParameterId,
                    parameter.Id,
                    StringComparison.Ordinal) ||
                objectDetectionDefectDisplayImageGeneration != imageSourceGeneration)
            {
                SetObjectDetectionDefectRegionStatus(
                    "平場校正後的缺陷顯示影像尚未就緒；請確認校正結果後再試。 ");
                return;
            }

            FocusObjectDetectionImage(selectedObject.Bounds);
            objectDetectionDefectRegionDrawMode = true;
            objectDetectionDefectRegionIsDrawing = false;
            objectDetectionDefectRegionIsResizing = false;
            objectDetectionDefectRegionDraftParameterId = parameter.Id;
            objectDetectionDefectRegionDraftObjectNumber = selectedObject.Number;
            objectDetectionDefectRegionDrawButton.Text = "停止框選";
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            SetObjectDetectionDefectRegionStatus(
                "物件" + selectedObject.Number.ToString(CultureInfo.InvariantCulture) +
                "：Ctrl 拖曳可框選；抓住現有框線可微調。放開後按「套用」保存。");
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectRegionApplyButton_Click(object sender, EventArgs e)
        {
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null || !objectDetectionDefectRegionDraftConfigured ||
                !string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                SetObjectDetectionDefectRegionStatus("請先完成框選，再套用檢測範圍。");
                return;
            }

            if (string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                SetObjectDetectionDefectRegionStatus("目前檢測參數尚未指定物件定義來源。");
                return;
            }

            parameter.DefectInspectionRegionConfigured = true;
            parameter.DefectInspectionRegionObjectDefinitionId = parameter.ObjectDefinitionId;
            parameter.DefectInspectionRegionReferenceObjectNumber =
                objectDetectionDefectRegionDraftObjectNumber;
            parameter.DefectInspectionRegionLeft = objectDetectionDefectRegionDraft.Left;
            parameter.DefectInspectionRegionTop = objectDetectionDefectRegionDraft.Top;
            parameter.DefectInspectionRegionRight = objectDetectionDefectRegionDraft.Right;
            parameter.DefectInspectionRegionBottom = objectDetectionDefectRegionDraft.Bottom;
            if (string.IsNullOrWhiteSpace(parameter.DefectInspectionRegionId))
            {
                parameter.DefectInspectionRegionId = Guid.NewGuid().ToString("N");
            }

            SaveSystemParameters();
            objectDetectionDefectRegionDrawMode = false;
            objectDetectionDefectRegionIsDrawing = false;
            objectDetectionDefectRegionIsResizing = false;
            objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
            objectDetectionDefectRegionDraftConfigured = false;
            objectDetectionDefectRegionDrawStart = Point.Empty;
            if (objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.ReleaseViewerPointer();
            }
            objectDetectionDefectRegionDrawButton.Text = "框選範圍";
            SetObjectDetectionDefectRegionStatus(
                "已套用：範例物件" +
                parameter.DefectInspectionRegionReferenceObjectNumber.ToString(CultureInfo.InvariantCulture) +
                "；此 ROI 相對範圍會套用至其他序號。");
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            InvalidateObjectDetectionDefectRegionDisplay();
            if (statusLabel != null)
            {
                statusLabel.Text = parameter.DisplayName + "：檢測範圍已套用並保存";
            }
        }

        private void ObjectDetectionDefectRegionCancelButton_Click(object sender, EventArgs e)
        {
            ResetObjectDetectionDefectRegionDraft();
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            SetObjectDetectionDefectRegionStatus("已取消尚未套用的檢測範圍。");
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectRegionClearButton_Click(object sender, EventArgs e)
        {
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null)
            {
                return;
            }

            if (parameter.DefectInspectionRegionConfigured &&
                MessageBox.Show(
                    this,
                    "確定清除已保存的檢測範圍嗎？",
                    "清除檢測範圍",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            ResetObjectDetectionDefectRegionDraft();
            parameter.DefectInspectionRegionId = Guid.NewGuid().ToString("N");
            parameter.DefectInspectionRegionObjectDefinitionId = string.Empty;
            parameter.DefectInspectionRegionReferenceObjectNumber = 0;
            parameter.DefectInspectionRegionConfigured = false;
            parameter.DefectInspectionRegionLeft = 0;
            parameter.DefectInspectionRegionTop = 0;
            parameter.DefectInspectionRegionRight = 1;
            parameter.DefectInspectionRegionBottom = 1;
            SaveSystemParameters();
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            SetObjectDetectionDefectRegionStatus("已清除檢測範圍。");
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectDisplayControl_ImageMouseDown(
            object sender,
            ImageMouseEventArgs e)
        {
            if (!objectDetectionDefectRegionDrawMode || e == null ||
                e.Button != MouseButtons.Left ||
                (e.Modifiers & Keys.Control) == 0 ||
                !e.IsInsideImage)
            {
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                SetObjectDetectionDefectRegionStatus("目前序號沒有可用的物件 ROI。");
                return;
            }

            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            PointF local = frame.ToNormalizedLocal(
                e.ImageLocation.X,
                e.ImageLocation.Y);
            e.Handled = true;
            if (!IsObjectDetectionDefectRegionPointInsideRoi(local))
            {
                SetObjectDetectionDefectRegionStatus("請從綠色 ROI 框內開始拖曳。");
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            RectangleF currentRegion;
            if (TryGetObjectDetectionDefectRegionForEditing(
                    parameter,
                    selectedObject,
                    out currentRegion))
            {
                ObjectDetectionDefectRegionEdges resizeEdges =
                    FindObjectDetectionDefectRegionResizeEdges(
                        local,
                        currentRegion,
                        frame,
                        objectDetectionDefectDisplayControl.ViewState.Zoom);
                if (resizeEdges != ObjectDetectionDefectRegionEdges.None)
                {
                    objectDetectionDefectRegionDraft = currentRegion;
                    objectDetectionDefectRegionDraftConfigured = true;
                    objectDetectionDefectRegionDraftParameterId = activeObjectDetectionParameterId;
                    objectDetectionDefectRegionDraftObjectNumber = selectedObject.Number;
                    objectDetectionDefectRegionResizeEdges = resizeEdges;
                    objectDetectionDefectRegionIsResizing = true;
                    objectDetectionDefectDisplayControl.CaptureViewerPointer();
                    objectDetectionDefectDisplayControl.SetViewerCursor(
                        GetObjectDetectionDefectRegionResizeCursor(resizeEdges));
                    SetObjectDetectionDefectRegionStatus(
                        "調整中：拖曳框線或角落，放開後按「套用」保存。");
                    InvalidateObjectDetectionDefectRegionDisplay();
                    return;
                }
            }

            objectDetectionDefectRegionDraftConfigured = false;
            objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
            objectDetectionDefectRegionIsDrawing = true;
            objectDetectionDefectRegionDrawStart = e.ImageLocation;
            objectDetectionDefectRegionDrawCurrent = e.ImageLocation;
            objectDetectionDefectDisplayControl.CaptureViewerPointer();
            objectDetectionDefectDisplayControl.SetViewerCursor(Cursors.Cross);
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectDisplayControl_ImageMouseMove(
            object sender,
            ImageMouseEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (objectDetectionDefectRegionIsResizing)
            {
                e.Handled = true;
                ObjectDefinitionDetectedObject selectedObject;
                if (TryGetSelectedObjectDetectionObject(out selectedObject))
                {
                    ObjectDetectionMeasurementFrame frame =
                        CreateObjectDetectionMeasurementFrame(selectedObject);
                    PointF local = frame.ToNormalizedLocal(
                        e.ImageLocation.X,
                        e.ImageLocation.Y);
                    UpdateObjectDetectionDefectRegionResize(local, frame);
                    e.Handled = true;
                    InvalidateObjectDetectionDefectRegionDisplay();
                }

                return;
            }

            if (objectDetectionDefectRegionIsDrawing)
            {
                objectDetectionDefectRegionDrawCurrent = e.ImageLocation;
                e.Handled = true;
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            UpdateObjectDetectionDefectRegionHoverCursor(e);
        }

        private void ObjectDetectionDefectDisplayControl_ImageMouseUp(
            object sender,
            ImageMouseEventArgs e)
        {
            if ((!objectDetectionDefectRegionIsDrawing &&
                !objectDetectionDefectRegionIsResizing) || e == null ||
                e.Button != MouseButtons.Left)
            {
                return;
            }

            e.Handled = true;
            objectDetectionDefectDisplayControl.ReleaseViewerPointer();
            if (objectDetectionDefectRegionIsResizing)
            {
                ObjectDefinitionDetectedObject resizedObject;
                if (TryGetSelectedObjectDetectionObject(out resizedObject))
                {
                    ObjectDetectionMeasurementFrame resizedFrame =
                        CreateObjectDetectionMeasurementFrame(resizedObject);
                    PointF local = resizedFrame.ToNormalizedLocal(
                        e.ImageLocation.X,
                        e.ImageLocation.Y);
                    UpdateObjectDetectionDefectRegionResize(local, resizedFrame);
                }

                objectDetectionDefectRegionIsResizing = false;
                objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
                SetObjectDetectionDefectRegionDraftStatus("範圍已調整");
                UpdateObjectDetectionDefectInspectionRegionControls(
                    FindObjectDetectionParameter(activeObjectDetectionParameterId));
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            objectDetectionDefectRegionDrawCurrent = e.ImageLocation;
            objectDetectionDefectRegionIsDrawing = false;
            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                objectDetectionDefectRegionIsResizing = false;
                objectDetectionDefectDisplayControl.ReleaseViewerPointer();
                SetObjectDetectionDefectRegionStatus("物件 ROI 已失效，請重新選擇序號。");
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            PointF start = frame.ToNormalizedLocal(
                objectDetectionDefectRegionDrawStart.X,
                objectDetectionDefectRegionDrawStart.Y);
            PointF end = frame.ToNormalizedLocal(
                objectDetectionDefectRegionDrawCurrent.X,
                objectDetectionDefectRegionDrawCurrent.Y);
            float left = ClampUnit(Math.Min(start.X, end.X));
            float top = ClampUnit(Math.Min(start.Y, end.Y));
            float right = ClampUnit(Math.Max(start.X, end.X));
            float bottom = ClampUnit(Math.Max(start.Y, end.Y));
            if ((right - left) * frame.Width < 1f ||
                (bottom - top) * frame.Height < 1f)
            {
                SetObjectDetectionDefectRegionStatus("框選範圍太小，請重新拖曳。");
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            objectDetectionDefectRegionDraft = RectangleF.FromLTRB(
                left,
                top,
                right,
                bottom);
            objectDetectionDefectRegionDraftConfigured = true;
            objectDetectionDefectRegionDraftParameterId = activeObjectDetectionParameterId;
            objectDetectionDefectRegionDraftObjectNumber = selectedObject.Number;
            objectDetectionDefectRegionDrawButton.Text = "停止框選";
            SetObjectDetectionDefectRegionDraftStatus("框選完成");
            UpdateObjectDetectionDefectInspectionRegionControls(
                FindObjectDetectionParameter(activeObjectDetectionParameterId));
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectDisplayControl_ImageOverlayPaint(
            object sender,
            ImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            int displayIndex = GetObjectDetectionDefectDisplayIndex(display);
            if (e == null || !isObjectDetectionParameterImageLayout ||
                leftImageTabControl == null ||
                displayIndex < 0 ||
                leftImageTabControl.SelectedTab != GetObjectDetectionDefectDisplayTabPage(displayIndex))
            {
                return;
            }

            DrawObjectDetectionDefectCoreResult(
                e.Graphics,
                e.Zoom,
                e.Offset,
                e.VisibleSourceRect,
                GetObjectDetectionDefectDisplayCoreKey(displayIndex));

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                return;
            }

            using (var roiPen = new Pen(Color.LimeGreen, 2f))
            {
                DrawObjectDetectionObjectOutline(
                    e.Graphics,
                    selectedObject,
                    roiPen,
                    e.Zoom,
                    e.Offset);
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            RectangleF normalizedRegion;
            bool draft = false;
            if (objectDetectionDefectRegionIsDrawing)
            {
                ObjectDetectionMeasurementFrame frame =
                    CreateObjectDetectionMeasurementFrame(selectedObject);
                PointF start = frame.ToNormalizedLocal(
                    objectDetectionDefectRegionDrawStart.X,
                    objectDetectionDefectRegionDrawStart.Y);
                PointF current = frame.ToNormalizedLocal(
                    objectDetectionDefectRegionDrawCurrent.X,
                    objectDetectionDefectRegionDrawCurrent.Y);
                normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit(Math.Min(start.X, current.X)),
                    ClampUnit(Math.Min(start.Y, current.Y)),
                    ClampUnit(Math.Max(start.X, current.X)),
                    ClampUnit(Math.Max(start.Y, current.Y)));
                draft = true;
            }
            else if (objectDetectionDefectRegionDraftConfigured &&
                string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    activeObjectDetectionParameterId,
                    StringComparison.Ordinal))
            {
                normalizedRegion = objectDetectionDefectRegionDraft;
                draft = true;
            }
            else if (parameter != null && parameter.DefectInspectionRegionConfigured &&
                string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal))
            {
                normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit((float)parameter.DefectInspectionRegionLeft),
                    ClampUnit((float)parameter.DefectInspectionRegionTop),
                    ClampUnit((float)parameter.DefectInspectionRegionRight),
                    ClampUnit((float)parameter.DefectInspectionRegionBottom));
            }
            else
            {
                return;
            }

            if (normalizedRegion.Width <= 0 || normalizedRegion.Height <= 0)
            {
                return;
            }

            PointF[] imageCorners = CreateObjectDetectionDefectRegionImageCorners(
                selectedObject,
                normalizedRegion);
            PointF[] screenCorners = new PointF[imageCorners.Length];
            for (int index = 0; index < imageCorners.Length; index++)
            {
                screenCorners[index] = new PointF(
                    e.Offset.X + (imageCorners[index].X * e.Zoom),
                    e.Offset.Y + (imageCorners[index].Y * e.Zoom));
            }

            Color fillColor = draft
                ? Color.FromArgb(48, 255, 166, 0)
                : Color.FromArgb(46, 0, 150, 220);
            Color lineColor = draft ? Color.DarkOrange : Color.DeepSkyBlue;
            using (var fill = new SolidBrush(fillColor))
            using (var outline = new Pen(lineColor, 2f))
            {
                e.Graphics.FillPolygon(fill, screenCorners);
                e.Graphics.DrawPolygon(outline, screenCorners);
            }
        }

        private static PointF[] CreateObjectDetectionDefectRegionImageCorners(
            ObjectDefinitionDetectedObject selectedObject,
            RectangleF normalizedRegion)
        {
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            return new[]
            {
                frame.ToImage((double)normalizedRegion.Left, (double)normalizedRegion.Top),
                frame.ToImage((double)normalizedRegion.Right, (double)normalizedRegion.Top),
                frame.ToImage((double)normalizedRegion.Right, (double)normalizedRegion.Bottom),
                frame.ToImage((double)normalizedRegion.Left, (double)normalizedRegion.Bottom)
            };
        }

        private bool TryGetObjectDetectionDefectRegionForEditing(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionDetectedObject selectedObject,
            out RectangleF region)
        {
            region = RectangleF.Empty;
            if (parameter == null || selectedObject == null)
            {
                return false;
            }

            if (objectDetectionDefectRegionDraftConfigured &&
                string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionDefectRegionDraftObjectNumber == selectedObject.Number)
            {
                region = objectDetectionDefectRegionDraft;
                return region.Width > 0 && region.Height > 0;
            }

            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            region = RectangleF.FromLTRB(
                ClampUnit((float)parameter.DefectInspectionRegionLeft),
                ClampUnit((float)parameter.DefectInspectionRegionTop),
                ClampUnit((float)parameter.DefectInspectionRegionRight),
                ClampUnit((float)parameter.DefectInspectionRegionBottom));
            return region.Width > 0 && region.Height > 0;
        }

        private static ObjectDetectionDefectRegionEdges FindObjectDetectionDefectRegionResizeEdges(
            PointF normalizedPoint,
            RectangleF region,
            ObjectDetectionMeasurementFrame frame,
            float zoom)
        {
            const float hitTolerancePixels = 8f;
            if (zoom <= 0)
            {
                return ObjectDetectionDefectRegionEdges.None;
            }

            float toleranceX = hitTolerancePixels / Math.Max(1f, frame.Width * zoom);
            float toleranceY = hitTolerancePixels / Math.Max(1f, frame.Height * zoom);
            bool withinVerticalEdge = normalizedPoint.Y >= region.Top - toleranceY &&
                normalizedPoint.Y <= region.Bottom + toleranceY;
            bool withinHorizontalEdge = normalizedPoint.X >= region.Left - toleranceX &&
                normalizedPoint.X <= region.Right + toleranceX;
            bool nearLeft = withinVerticalEdge &&
                Math.Abs(normalizedPoint.X - region.Left) <= toleranceX;
            bool nearRight = withinVerticalEdge &&
                Math.Abs(normalizedPoint.X - region.Right) <= toleranceX;
            bool nearTop = withinHorizontalEdge &&
                Math.Abs(normalizedPoint.Y - region.Top) <= toleranceY;
            bool nearBottom = withinHorizontalEdge &&
                Math.Abs(normalizedPoint.Y - region.Bottom) <= toleranceY;

            if (nearLeft && nearRight)
            {
                if (Math.Abs(normalizedPoint.X - region.Left) <=
                    Math.Abs(normalizedPoint.X - region.Right))
                {
                    nearRight = false;
                }
                else
                {
                    nearLeft = false;
                }
            }

            if (nearTop && nearBottom)
            {
                if (Math.Abs(normalizedPoint.Y - region.Top) <=
                    Math.Abs(normalizedPoint.Y - region.Bottom))
                {
                    nearBottom = false;
                }
                else
                {
                    nearTop = false;
                }
            }

            ObjectDetectionDefectRegionEdges edges = ObjectDetectionDefectRegionEdges.None;
            if (nearLeft)
            {
                edges |= ObjectDetectionDefectRegionEdges.Left;
            }

            if (nearTop)
            {
                edges |= ObjectDetectionDefectRegionEdges.Top;
            }

            if (nearRight)
            {
                edges |= ObjectDetectionDefectRegionEdges.Right;
            }

            if (nearBottom)
            {
                edges |= ObjectDetectionDefectRegionEdges.Bottom;
            }

            return edges;
        }

        private void UpdateObjectDetectionDefectRegionResize(
            PointF normalizedPoint,
            ObjectDetectionMeasurementFrame frame)
        {
            RectangleF region = objectDetectionDefectRegionDraft;
            float minimumWidth = 1f / Math.Max(1f, frame.Width);
            float minimumHeight = 1f / Math.Max(1f, frame.Height);
            float x = ClampUnit(normalizedPoint.X);
            float y = ClampUnit(normalizedPoint.Y);

            if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Left) != 0)
            {
                float fixedRight = region.Right;
                region.X = Math.Min(x, fixedRight - minimumWidth);
                region.Width = fixedRight - region.Left;
            }
            else if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Right) != 0)
            {
                region.Width = Math.Max(x, region.Left + minimumWidth) - region.Left;
            }

            if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Top) != 0)
            {
                float fixedBottom = region.Bottom;
                region.Y = Math.Min(y, fixedBottom - minimumHeight);
                region.Height = fixedBottom - region.Top;
            }
            else if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Bottom) != 0)
            {
                region.Height = Math.Max(y, region.Top + minimumHeight) - region.Top;
            }

            objectDetectionDefectRegionDraft = region;
            objectDetectionDefectRegionDraftConfigured = true;
        }

        private void UpdateObjectDetectionDefectRegionHoverCursor(ImageMouseEventArgs e)
        {
            if (objectDetectionDefectDisplayControl == null)
            {
                return;
            }

            ObjectDetectionDefectRegionEdges edges = ObjectDetectionDefectRegionEdges.None;
            ObjectDefinitionDetectedObject selectedObject;
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (objectDetectionDefectRegionDrawMode && e.IsInsideImage &&
                (e.Modifiers & Keys.Control) != 0 &&
                TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                RectangleF region;
                if (TryGetObjectDetectionDefectRegionForEditing(
                        parameter,
                        selectedObject,
                        out region))
                {
                    ObjectDetectionMeasurementFrame frame =
                        CreateObjectDetectionMeasurementFrame(selectedObject);
                    PointF local = frame.ToNormalizedLocal(
                        e.ImageLocation.X,
                        e.ImageLocation.Y);
                    edges = FindObjectDetectionDefectRegionResizeEdges(
                        local,
                        region,
                        frame,
                        objectDetectionDefectDisplayControl.ViewState.Zoom);
                }
            }

            objectDetectionDefectDisplayControl.SetViewerCursor(
                edges == ObjectDetectionDefectRegionEdges.None
                    ? Cursors.Default
                    : GetObjectDetectionDefectRegionResizeCursor(edges));
        }

        private void ObjectDetectionDefectDisplayControl_ImagePointerMoved(
            object sender,
            ImagePointerMovedEventArgs e)
        {
            if (e != null && !e.IsInsideViewer &&
                !objectDetectionDefectRegionIsDrawing &&
                !objectDetectionDefectRegionIsResizing &&
                objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.SetViewerCursor(Cursors.Default);
            }
        }

        private static Cursor GetObjectDetectionDefectRegionResizeCursor(
            ObjectDetectionDefectRegionEdges edges)
        {
            bool horizontal =
                (edges & (ObjectDetectionDefectRegionEdges.Left | ObjectDetectionDefectRegionEdges.Right)) != 0;
            bool vertical =
                (edges & (ObjectDetectionDefectRegionEdges.Top | ObjectDetectionDefectRegionEdges.Bottom)) != 0;
            if (horizontal && vertical)
            {
                bool sameDiagonal =
                    ((edges & ObjectDetectionDefectRegionEdges.Left) != 0) ==
                    ((edges & ObjectDetectionDefectRegionEdges.Top) != 0);
                return sameDiagonal ? Cursors.SizeNWSE : Cursors.SizeNESW;
            }

            return horizontal ? Cursors.SizeWE : Cursors.SizeNS;
        }

        private void SetObjectDetectionDefectRegionDraftStatus(string prefix)
        {
            RectangleF region = objectDetectionDefectRegionDraft;
            SetObjectDetectionDefectRegionStatus(
                prefix + "：X " + (region.Left * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%，Y " + (region.Top * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%，寬 " + (region.Width * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%，高 " + (region.Height * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%；按「套用」保存。");
        }

        private static bool IsObjectDetectionDefectRegionPointInsideRoi(PointF normalizedPoint)
        {
            const float tolerance = 0.0001f;
            return normalizedPoint.X >= -tolerance && normalizedPoint.X <= 1f + tolerance &&
                normalizedPoint.Y >= -tolerance && normalizedPoint.Y <= 1f + tolerance;
        }

        private static float ClampUnit(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private void UpdateObjectDetectionDefectInspectionRegionControls(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionDefectRegionStatusLabel == null ||
                objectDetectionDefectRegionStatusLabel.IsDisposed)
            {
                return;
            }

            bool hasSelectedObject = TryGetSelectedObjectDetectionObject(
                out ObjectDefinitionDetectedObject selectedObject);
            bool sourceMatches = parameter != null &&
                string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal);
            if (objectDetectionDefectRegionDrawButton != null)
            {
                objectDetectionDefectRegionDrawButton.Enabled = parameter != null && hasSelectedObject;
                objectDetectionDefectRegionDrawButton.Text = objectDetectionDefectRegionDrawMode
                    ? "停止框選"
                    : "框選範圍";
            }

            if (objectDetectionDefectRegionApplyButton != null)
            {
                objectDetectionDefectRegionApplyButton.Enabled =
                    parameter != null && objectDetectionDefectRegionDraftConfigured;
            }

            if (objectDetectionDefectRegionCancelButton != null)
            {
                objectDetectionDefectRegionCancelButton.Enabled =
                    objectDetectionDefectRegionDrawMode ||
                    objectDetectionDefectRegionIsDrawing ||
                    objectDetectionDefectRegionDraftConfigured;
            }

            if (objectDetectionDefectRegionClearButton != null)
            {
                objectDetectionDefectRegionClearButton.Enabled = parameter != null &&
                    (parameter.DefectInspectionRegionConfigured ||
                    objectDetectionDefectRegionDraftConfigured);
            }

            if (objectDetectionDefectParallelExecutionCheckBox != null)
            {
                objectDetectionDefectParallelExecutionCheckBox.Enabled =
                    parameter != null && !objectDetectionDefectProcessingRequested;
            }

            if (objectDetectionDefectRunButton != null)
            {
                objectDetectionDefectRunButton.Enabled = parameter != null &&
                    parameter.DefectInspectionRegionConfigured && sourceMatches &&
                    !objectDetectionDefectProcessingRequested;
            }

            if (objectDetectionDefectRegionDraftConfigured)
            {
                RectangleF region = objectDetectionDefectRegionDraft;
                objectDetectionDefectRegionStatusLabel.Text =
                    "草稿範例物件 " + objectDetectionDefectRegionDraftObjectNumber.ToString(CultureInfo.InvariantCulture) +
                    "；X " + (region.Left * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，Y " + (region.Top * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，寬 " + (region.Width * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，高 " + (region.Height * 100f).ToString("0.0", CultureInfo.CurrentCulture) + "%。";
            }
            else if (parameter != null && parameter.DefectInspectionRegionConfigured && !sourceMatches)
            {
                objectDetectionDefectRegionStatusLabel.Text =
                    "物件定義來源已變更，請重新框選並套用範圍。";
            }
            else if (parameter != null && parameter.DefectInspectionRegionConfigured)
            {
                objectDetectionDefectRegionStatusLabel.Text =
                    "已套用至所有序號；範例物件 " +
                    parameter.DefectInspectionRegionReferenceObjectNumber.ToString(CultureInfo.InvariantCulture) +
                    "，X " + (parameter.DefectInspectionRegionLeft * 100.0).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，Y " + (parameter.DefectInspectionRegionTop * 100.0).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，寬 " + ((parameter.DefectInspectionRegionRight - parameter.DefectInspectionRegionLeft) * 100.0).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，高 " + ((parameter.DefectInspectionRegionBottom - parameter.DefectInspectionRegionTop) * 100.0).ToString("0.0", CultureInfo.CurrentCulture) + "%。";
            }
            else if (!hasSelectedObject)
            {
                objectDetectionDefectRegionStatusLabel.Text = "請先選擇已找到的物件序號。";
            }
            else
            {
                objectDetectionDefectRegionStatusLabel.Text = "尚未設定檢測範圍。";
            }
        }

        private void CancelObjectDetectionDefectInspectionRegionDraftForObjectChange(
            int newObjectNumber)
        {
            if ((objectDetectionDefectRegionDrawMode || objectDetectionDefectRegionIsDrawing ||
                objectDetectionDefectRegionDraftConfigured) &&
                objectDetectionDefectRegionDraftObjectNumber > 0 &&
                objectDetectionDefectRegionDraftObjectNumber != newObjectNumber)
            {
                ResetObjectDetectionDefectRegionDraft();
                SetObjectDetectionDefectRegionStatus("序號已變更，未套用的框選草稿已取消。");
                InvalidateObjectDetectionDefectRegionDisplay();
            }
        }

        private void ResetObjectDetectionDefectRegionDraft()
        {
            if (objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.ReleaseViewerPointer();
            }

            objectDetectionDefectRegionDrawMode = false;
            objectDetectionDefectRegionIsDrawing = false;
            objectDetectionDefectRegionIsResizing = false;
            objectDetectionDefectRegionDraftConfigured = false;
            objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
            objectDetectionDefectRegionDrawStart = Point.Empty;
            objectDetectionDefectRegionDrawCurrent = Point.Empty;
            objectDetectionDefectRegionDraft = RectangleF.Empty;
            objectDetectionDefectRegionDraftObjectNumber = 0;
            if (objectDetectionDefectRegionDrawButton != null &&
                !objectDetectionDefectRegionDrawButton.IsDisposed)
            {
                objectDetectionDefectRegionDrawButton.Text = "框選範圍";
            }
        }

        private void SetObjectDetectionDefectRegionStatus(string text)
        {
            if (objectDetectionDefectRegionStatusLabel != null &&
                !objectDetectionDefectRegionStatusLabel.IsDisposed)
            {
                objectDetectionDefectRegionStatusLabel.Text = text ?? string.Empty;
            }
        }

        private void InvalidateObjectDetectionDefectRegionDisplay()
        {
            if (objectDetectionDefectDisplayControl != null &&
                !objectDetectionDefectDisplayControl.IsDisposed)
            {
                objectDetectionDefectDisplayControl.InvalidateImageView();
            }
        }
    }
}
