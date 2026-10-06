using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ObjectDetectionLineTextureCell
        {
            public int ObjectNumber { get; set; }

            public Rectangle Bounds { get; set; }

            public double Score { get; set; }

            public bool IsAnomaly { get; set; }
        }

        private sealed class ObjectDetectionLineTextureSegment
        {
            public int ObjectNumber { get; set; }

            public PointF Start { get; set; }

            public PointF End { get; set; }

            public Rectangle Bounds { get; set; }

            public double Score { get; set; }

            public double SupportRatio { get; set; }

            public bool IsAnomaly { get; set; }
        }

        private sealed class ObjectDetectionLineTextureResult
        {
            public string Signature { get; set; }

            public long SourcePreparationMilliseconds { get; set; }

            public long TotalElapsedMilliseconds { get; set; }

            public long ScanElapsedMilliseconds { get; set; }

            public long TextureCalculationMilliseconds { get; set; }

            public long PreviewGenerationMilliseconds { get; set; }

            public long ScannedTileCount { get; set; }

            public Dictionary<int, PointF[]> ObjectPolygons { get; set; }

            public Dictionary<int, List<ObjectDetectionLineTextureCell>> CellsByObject { get; set; }

            public Dictionary<int, List<ObjectDetectionLineTextureSegment>> LinesByObject { get; set; }

            public List<ObjectDetectionLineTextureCell> Cells { get; set; }

            public List<ObjectDetectionLineTextureSegment> Lines { get; set; }

            public List<ObjectDetectionDefectProcessedPatch> ProcessedPatches { get; set; }
        }

        private readonly Dictionary<string, ObjectDetectionLineTextureResult> objectDetectionLineTextureResults =
            new Dictionary<string, ObjectDetectionLineTextureResult>(StringComparer.Ordinal);

        private CheckBox objectDetectionLineTextureEnabledCheckBox;
        private NumericUpDown objectDetectionLineTextureTileSizeInput;
        private NumericUpDown objectDetectionLineTextureMinimumLengthInput;
        private NumericUpDown objectDetectionLineTextureMaximumGapInput;
        private NumericUpDown objectDetectionLineTextureSensitivityInput;
        private NumericUpDown objectDetectionLineTextureSupportRatioInput;
        private CheckBox objectDetectionLineTextureShowHeatmapCheckBox;
        private CheckBox objectDetectionLineTextureShowLinesCheckBox;
        private Label objectDetectionLineTextureStatusLabel;
        private Button objectDetectionLineTextureRunButton;
        private string objectDetectionLineTextureDraftParameterId;
        private bool objectDetectionLineTextureAnalysisRunning;

        private TabPage BuildObjectDetectionLineTextureTab(ObjectDetectionParameterSettings parameter)
        {
            var page = new TabPage("線狀紋理異常");
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 251, 253)
            };
            objectDetectionLineTextureDraftParameterId = parameter.Id;

            var sourceInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(8, 7, 8, 2),
                Text = "分析來源：平場校正後的灰階影像，不追加對比或淡色缺陷增強。\r\n先計算局部紋理差異，再以可容忍斷點的直線投票尋找離散線狀異常。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(55, 63, 76)
            };

            var enabledPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
            objectDetectionLineTextureEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "是否使用線狀紋理異常",
                Checked = parameter.DefectLineTextureEnabled,
                Location = new Point(8, 5)
            };
            objectDetectionLineTextureEnabledCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureEnabled(parameter, objectDetectionLineTextureEnabledCheckBox.Checked);
            };
            enabledPanel.Controls.Add(objectDetectionLineTextureEnabledCheckBox);

            var scanGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 216,
                Text = "線狀紋理掃描與判定",
                Padding = new Padding(8, 16, 8, 4)
            };
            var scanLayout = CreateDefectCoreTable(6, 2);
            scanLayout.RowStyles.Clear();
            for (int row = 0; row < 6; row++) scanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            objectDetectionLineTextureTileSizeInput = CreateLineTextureNumeric(
                4, 128, 4, Math.Max(4, Math.Min(128, parameter.DefectLineTextureTileSizePixels)), 0);
            objectDetectionLineTextureMinimumLengthInput = CreateLineTextureNumeric(
                16, 5000, 16, Math.Max(16, Math.Min(5000, parameter.DefectLineTextureMinimumLengthPixels)), 0);
            objectDetectionLineTextureMaximumGapInput = CreateLineTextureNumeric(
                0, 2048, 4, Math.Max(0, Math.Min(2048, parameter.DefectLineTextureMaximumGapPixels)), 0);
            objectDetectionLineTextureSensitivityInput = CreateLineTextureNumeric(
                0.5m, 8m, 0.25m, (decimal)Math.Max(0.5, Math.Min(8, parameter.DefectLineTextureSensitivity)), 2);
            objectDetectionLineTextureSupportRatioInput = CreateLineTextureNumeric(
                0.05m, 0.8m, 0.05m, (decimal)Math.Max(0.05, Math.Min(0.8, parameter.DefectLineTextureMinimumSupportRatio)), 2);

            AddLineTextureScanRow(scanLayout, 0, "紋理格大小 (px)", objectDetectionLineTextureTileSizeInput);
            AddLineTextureScanRow(scanLayout, 1, "最短線段長度 (px)", objectDetectionLineTextureMinimumLengthInput);
            AddLineTextureScanRow(scanLayout, 2, "可容忍斷點 (px)", objectDetectionLineTextureMaximumGapInput);
            AddLineTextureScanRow(scanLayout, 3, "紋理異常敏感度", objectDetectionLineTextureSensitivityInput);
            AddLineTextureScanRow(scanLayout, 4, "線上異常點比例下限", objectDetectionLineTextureSupportRatioInput);
            var scanHint = new Label
            {
                Dock = DockStyle.Fill,
                Text = "方向自動搜尋；敏感度以局部紋理差異的穩健標準差倍數計算。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                AutoEllipsis = true
            };
            scanLayout.Controls.Add(scanHint, 0, 5);
            scanLayout.SetColumnSpan(scanHint, 2);
            scanGroup.Controls.Add(scanLayout);

            var displayOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(5, 5, 0, 0),
                Margin = Padding.Empty
            };
            objectDetectionLineTextureShowHeatmapCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示紋理差異",
                Checked = parameter.DefectLineTextureShowHeatmap,
                Margin = new Padding(2, 1, 14, 1)
            };
            objectDetectionLineTextureShowLinesCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示檢出線段",
                Checked = parameter.DefectLineTextureShowAnomalyLines,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionLineTextureShowHeatmapCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureDisplayOptions(parameter);
            };
            objectDetectionLineTextureShowLinesCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureDisplayOptions(parameter);
            };
            displayOptions.Controls.Add(objectDetectionLineTextureShowHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionLineTextureShowLinesCheckBox);

            objectDetectionLineTextureStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 58,
                Padding = new Padding(8, 4, 8, 2),
                Text = parameter.DefectLineTextureEnabled ? "尚未執行線狀紋理分析。" : "線狀紋理分析已停用。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 83, 95)
            };

            var actionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(4, 5, 4, 2),
                Margin = Padding.Empty
            };
            var cancelButton = new Button { Text = "取消", Width = 76, Height = 27, Margin = new Padding(3, 0, 0, 0) };
            var applyButton = new Button { Text = "套用", Width = 76, Height = 27, Margin = new Padding(3, 0, 0, 0) };
            objectDetectionLineTextureRunButton = new Button { Text = "開始分析", Width = 96, Height = 27, Margin = new Padding(3, 0, 0, 0) };
            cancelButton.Click += delegate { CancelObjectDetectionLineTextureSettings(parameter); };
            applyButton.Click += delegate { ApplyObjectDetectionLineTextureSettings(parameter); };
            objectDetectionLineTextureRunButton.Click += async delegate
            {
                if (ApplyObjectDetectionLineTextureSettings(parameter))
                {
                    await RunObjectDetectionLineTextureAnalysisAsync(parameter.Id);
                }
            };
            actionBar.Controls.Add(cancelButton);
            actionBar.Controls.Add(applyButton);
            actionBar.Controls.Add(objectDetectionLineTextureRunButton);

            content.Controls.Add(actionBar);
            content.Controls.Add(objectDetectionLineTextureStatusLabel);
            content.Controls.Add(displayOptions);
            content.Controls.Add(scanGroup);
            content.Controls.Add(sourceInfo);
            content.Controls.Add(enabledPanel);
            page.Controls.Add(content);
            UpdateObjectDetectionLineTextureControlsEnabled(parameter.DefectLineTextureEnabled);
            return page;
        }

        private static NumericUpDown CreateLineTextureNumeric(
            decimal minimum,
            decimal maximum,
            decimal increment,
            decimal value,
            int decimalPlaces)
        {
            return new NumericUpDown
            {
                Width = 94,
                Minimum = minimum,
                Maximum = maximum,
                Increment = increment,
                DecimalPlaces = decimalPlaces,
                Value = Math.Max(minimum, Math.Min(maximum, value)),
                ThousandsSeparator = decimalPlaces == 0
            };
        }

        private static void AddLineTextureScanRow(TableLayoutPanel layout, int row, string label, Control input)
        {
            layout.Controls.Add(CreateDefectCoreLabel(label), 0, row);
            input.Dock = DockStyle.Fill;
            layout.Controls.Add(input, 1, row);
        }

        private void UpdateObjectDetectionLineTextureControlsEnabled(bool enabled)
        {
            foreach (Control input in new Control[]
            {
                objectDetectionLineTextureTileSizeInput,
                objectDetectionLineTextureMinimumLengthInput,
                objectDetectionLineTextureMaximumGapInput,
                objectDetectionLineTextureSensitivityInput,
                objectDetectionLineTextureSupportRatioInput,
                objectDetectionLineTextureShowHeatmapCheckBox,
                objectDetectionLineTextureShowLinesCheckBox
            })
            {
                if (input != null) input.Enabled = enabled;
            }
            if (objectDetectionLineTextureRunButton != null)
            {
                objectDetectionLineTextureRunButton.Enabled = enabled && !objectDetectionLineTextureAnalysisRunning;
            }
        }

        private void UpdateObjectDetectionLineTextureEnabled(ObjectDetectionParameterSettings parameter, bool enabled)
        {
            if (parameter == null) return;
            parameter.DefectLineTextureEnabled = enabled;
            SaveSystemParameters();
            if (!enabled) RemoveObjectDetectionLineTextureResult(parameter.Id);
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectLineTextureDisplayIndex);
            if (display != null) display.InvalidateImageView();
            ImageDisplayControl integrated = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectIntegratedDisplayIndex);
            if (integrated != null) integrated.InvalidateImageView();
            UpdateObjectDetectionLineTextureControlsEnabled(enabled);
            SetObjectDetectionDefectRegionStatus(enabled
                ? "線狀紋理異常已啟用；需執行分析後才有結果。"
                : "線狀紋理異常已停用，相關標記已清除。 ");
        }

        private bool ApplyObjectDetectionLineTextureSettings(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || !string.Equals(objectDetectionLineTextureDraftParameterId, parameter.Id, StringComparison.Ordinal) ||
                objectDetectionLineTextureTileSizeInput == null || objectDetectionLineTextureMinimumLengthInput == null)
            {
                return false;
            }

            parameter.DefectLineTextureTileSizePixels = (int)objectDetectionLineTextureTileSizeInput.Value;
            parameter.DefectLineTextureMinimumLengthPixels = (int)objectDetectionLineTextureMinimumLengthInput.Value;
            parameter.DefectLineTextureMaximumGapPixels = (int)objectDetectionLineTextureMaximumGapInput.Value;
            parameter.DefectLineTextureSensitivity = Decimal.ToDouble(objectDetectionLineTextureSensitivityInput.Value);
            parameter.DefectLineTextureMinimumSupportRatio = Decimal.ToDouble(objectDetectionLineTextureSupportRatioInput.Value);
            parameter.DefectLineTextureShowHeatmap = objectDetectionLineTextureShowHeatmapCheckBox.Checked;
            parameter.DefectLineTextureShowAnomalyLines = objectDetectionLineTextureShowLinesCheckBox.Checked;
            RemoveObjectDetectionLineTextureResult(parameter.Id);
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            SaveSystemParameters();
            UpdateObjectDetectionLineTextureControlsEnabled(parameter.DefectLineTextureEnabled);
            ImageDisplayControl lineTextureDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectLineTextureDisplayIndex);
            if (lineTextureDisplay != null) lineTextureDisplay.InvalidateImageView();
            ImageDisplayControl integratedDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectIntegratedDisplayIndex);
            if (integratedDisplay != null) integratedDisplay.InvalidateImageView();
            SetObjectDetectionDefectRegionStatus(
                "線狀紋理設定已套用；格 " + parameter.DefectLineTextureTileSizePixels.ToString(CultureInfo.CurrentCulture) +
                " px、最短 " + parameter.DefectLineTextureMinimumLengthPixels.ToString(CultureInfo.CurrentCulture) +
                " px、斷點 " + parameter.DefectLineTextureMaximumGapPixels.ToString(CultureInfo.CurrentCulture) +
                " px。請執行分析更新結果。 ");
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            return true;
        }

        private void CancelObjectDetectionLineTextureSettings(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionLineTextureTileSizeInput == null) return;
            objectDetectionLineTextureTileSizeInput.Value = Math.Max(4, Math.Min(128, parameter.DefectLineTextureTileSizePixels));
            objectDetectionLineTextureMinimumLengthInput.Value = Math.Max(16, Math.Min(5000, parameter.DefectLineTextureMinimumLengthPixels));
            objectDetectionLineTextureMaximumGapInput.Value = Math.Max(0, Math.Min(2048, parameter.DefectLineTextureMaximumGapPixels));
            objectDetectionLineTextureSensitivityInput.Value = (decimal)Math.Max(0.5, Math.Min(8, parameter.DefectLineTextureSensitivity));
            objectDetectionLineTextureSupportRatioInput.Value = (decimal)Math.Max(0.05, Math.Min(0.8, parameter.DefectLineTextureMinimumSupportRatio));
            objectDetectionLineTextureShowHeatmapCheckBox.Checked = parameter.DefectLineTextureShowHeatmap;
            objectDetectionLineTextureShowLinesCheckBox.Checked = parameter.DefectLineTextureShowAnomalyLines;
            UpdateObjectDetectionLineTextureControlsEnabled(parameter.DefectLineTextureEnabled);
            SetObjectDetectionDefectRegionStatus("已取消線狀紋理設定變更。 ");
        }

        private void UpdateObjectDetectionLineTextureDisplayOptions(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionLineTextureShowHeatmapCheckBox == null ||
                objectDetectionLineTextureShowLinesCheckBox == null) return;
            parameter.DefectLineTextureShowHeatmap = objectDetectionLineTextureShowHeatmapCheckBox.Checked;
            parameter.DefectLineTextureShowAnomalyLines = objectDetectionLineTextureShowLinesCheckBox.Checked;
            SaveSystemParameters();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectLineTextureDisplayIndex);
            if (display != null) display.InvalidateImageView();
        }

        private void RemoveObjectDetectionLineTextureResult(string parameterId)
        {
            ObjectDetectionLineTextureResult previous;
            if (!string.IsNullOrWhiteSpace(parameterId) && objectDetectionLineTextureResults.TryGetValue(parameterId, out previous))
            {
                objectDetectionLineTextureResults.Remove(parameterId);
                DisposeObjectDetectionLineTextureResult(previous);
            }
        }

        private async Task RunObjectDetectionLineTextureAnalysisAsync(string parameterId, bool calledFromResultReview = false)
        {
            if (objectDetectionLineTextureAnalysisRunning) return;
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null || !parameter.DefectLineTextureEnabled)
            {
                SetObjectDetectionDefectRegionStatus("線狀紋理分析目前未啟用。 ");
                return;
            }
            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId, parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                SetObjectDetectionDefectRegionStatus("請先框選並套用缺陷檢測範圍，再執行線狀紋理分析。 ");
                return;
            }
            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                SetObjectDetectionDefectRegionStatus("尚未指定有效的物件定義來源。 ");
                return;
            }
            string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, definitionSignature))
            {
                SetObjectDetectionDefectRegionStatus("物件定義結果尚未就緒，請先完成物件定義處理。 ");
                return;
            }

            objectDetectionLineTextureAnalysisRunning = true;
            UpdateObjectDetectionLineTextureControlsEnabled(parameter.DefectLineTextureEnabled);
            if (!calledFromResultReview && objectDetectionDefectCoreTabs != null) objectDetectionDefectCoreTabs.Enabled = false;
            if (!calledFromResultReview && leftImageTabControl != null)
            {
                leftImageTabControl.SelectedTab = GetObjectDetectionDefectDisplayTabPage(ObjectDetectionDefectLineTextureDisplayIndex);
                RefreshObjectDetectionDefectDisplay();
            }
            try
            {
                Stopwatch total = Stopwatch.StartNew();
                SetObjectDetectionDefectRegionStatus("正在準備平場校正影像與 ROI... ");
                await Task.Yield();
                Stopwatch sourcePreparation = Stopwatch.StartNew();
                LargeImageSource source = await PrepareObjectDetectionFrequencySourceAsync(parameter);
                sourcePreparation.Stop();
                if (source == null) return;

                List<ObjectDefinitionDetectedObject> objects = SnapshotCompletedObjectDefinitionObjects(definition, definitionSignature);
                if (objects.Count == 0)
                {
                    SetObjectDetectionDefectRegionStatus("沒有已完成定義的 ROI 物件可供線狀紋理分析。 ");
                    return;
                }
                Rectangle imageBounds = new Rectangle(0, 0, source.Width, source.Height);
                RectangleF normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit((float)parameter.DefectInspectionRegionLeft),
                    ClampUnit((float)parameter.DefectInspectionRegionTop),
                    ClampUnit((float)parameter.DefectInspectionRegionRight),
                    ClampUnit((float)parameter.DefectInspectionRegionBottom));
                string signature = CreateObjectDetectionLineTextureSignature(
                    parameter, definitionSignature, imageSourceGeneration, objectDetectionFlatFieldEvaluationGeneration);
                LargeImageSource sourceReference = source.AddReference();
                var progress = new Progress<string>(message =>
                {
                    if (!IsDisposed && string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal))
                    {
                        SetObjectDetectionDefectRegionStatus(message);
                    }
                });
                SetObjectDetectionDefectRegionStatus(parameter.DefectParallelExecutionEnabled
                    ? "線狀紋理分析中：各物件 ROI 同時處理... "
                    : "線狀紋理分析中：各物件 ROI 依序處理... ");
                Stopwatch scan = Stopwatch.StartNew();
                ObjectDetectionLineTextureResult result = await Task.Run(delegate
                {
                    try
                    {
                        return ScanObjectDetectionLineTextureRegions(
                            sourceReference, objects, normalizedRegion, imageBounds, parameter,
                            parameter.DefectParallelExecutionEnabled, progress);
                    }
                    finally { sourceReference.ReleaseReference(); }
                });
                scan.Stop();
                total.Stop();
                result.Signature = signature;
                result.SourcePreparationMilliseconds = sourcePreparation.ElapsedMilliseconds;
                result.ScanElapsedMilliseconds = scan.ElapsedMilliseconds;
                result.TotalElapsedMilliseconds = total.ElapsedMilliseconds;

                if (IsDisposed || !string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal) ||
                    !string.Equals(CreateObjectDefinitionProcessingSignature(definition), definitionSignature, StringComparison.Ordinal) ||
                    !string.Equals(CreateObjectDetectionLineTextureSignature(
                        parameter, definitionSignature, imageSourceGeneration, objectDetectionFlatFieldEvaluationGeneration), signature, StringComparison.Ordinal))
                {
                    SetObjectDetectionDefectRegionStatus("分析期間影像或設定已變更，線狀紋理結果未套用；請重新分析。 ");
                    DisposeObjectDetectionLineTextureResult(result);
                    return;
                }

                RemoveObjectDetectionLineTextureResult(parameter.Id);
                objectDetectionLineTextureResults[parameter.Id] = result;
                InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
                RefreshObjectDetectionDefectIntegrationResults(parameter);
                ImageDisplayControl lineTextureDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectLineTextureDisplayIndex);
                if (lineTextureDisplay != null) lineTextureDisplay.InvalidateImageView();
                ImageDisplayControl integratedDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectIntegratedDisplayIndex);
                if (integratedDisplay != null) integratedDisplay.InvalidateImageView();
                int anomalies = result.Lines.Count(line => line.IsAnomaly);
                string summary = string.Format(
                    CultureInfo.CurrentCulture,
                    "檢查 {0:N0} 個紋理格，檢出 {1:N0} 條線狀異常；總耗時 {2:N0} ms（來源準備 {3:N0}、掃描 {4:N0} ms）",
                    result.ScannedTileCount,
                    anomalies,
                    result.TotalElapsedMilliseconds,
                    result.SourcePreparationMilliseconds,
                    result.ScanElapsedMilliseconds);
                if (objectDetectionLineTextureStatusLabel != null) objectDetectionLineTextureStatusLabel.Text = summary;
                SetObjectDetectionDefectRegionStatus(summary +
                    (IsObjectDetectionDefectLineTextureIntegrationRequired(parameter)
                        ? "已納入綜合結果。 "
                        : "目前未納入綜合判定。 "));
            }
            catch (OutOfMemoryException)
            {
                SetObjectDetectionDefectRegionStatus("線狀紋理分析記憶體不足；請增大紋理格或減少同時處理的 ROI。 ");
            }
            catch (Exception exception)
            {
                SetObjectDetectionDefectRegionStatus("線狀紋理分析失敗：" + exception.Message);
            }
            finally
            {
                objectDetectionLineTextureAnalysisRunning = false;
                if (!IsDisposed)
                {
                    UpdateObjectDetectionLineTextureControlsEnabled(parameter != null && parameter.DefectLineTextureEnabled);
                    if (!calledFromResultReview && objectDetectionDefectCoreTabs != null && !objectDetectionDefectCoreTabs.IsDisposed)
                    {
                        objectDetectionDefectCoreTabs.Enabled = true;
                    }
                }
            }
        }

        private static unsafe ObjectDetectionLineTextureResult ScanObjectDetectionLineTextureRegions(
            LargeImageSource source,
            IList<ObjectDefinitionDetectedObject> objects,
            RectangleF normalizedRegion,
            Rectangle imageBounds,
            ObjectDetectionParameterSettings parameter,
            bool runParallel,
            IProgress<string> progress)
        {
            var cellsByObject = new List<ObjectDetectionLineTextureCell>[objects.Count];
            var linesByObject = new List<ObjectDetectionLineTextureSegment>[objects.Count];
            var polygonsByObject = new PointF[objects.Count][];
            var patchesByObject = new ObjectDetectionDefectProcessedPatch[objects.Count];
            long textureTicks = 0;
            long previewTicks = 0;
            long scannedTiles = 0;
            int tileSize = Math.Max(4, parameter.DefectLineTextureTileSizePixels);
            Action<int> scanObject = objectIndex =>
            {
                ObjectDefinitionDetectedObject detected = objects[objectIndex];
                PointF[] corners = CreateObjectDetectionDefectRegionImageCorners(detected, normalizedRegion);
                polygonsByObject[objectIndex] = corners;
                var cells = new List<ObjectDetectionLineTextureCell>();
                var lines = new List<ObjectDetectionLineTextureSegment>();
                cellsByObject[objectIndex] = cells;
                linesByObject[objectIndex] = lines;
                int left = (int)Math.Floor(corners.Min(point => point.X));
                int top = (int)Math.Floor(corners.Min(point => point.Y));
                int right = (int)Math.Ceiling(corners.Max(point => point.X));
                int bottom = (int)Math.Ceiling(corners.Max(point => point.Y));
                Rectangle crop = Rectangle.Intersect(
                    Rectangle.Intersect(Rectangle.FromLTRB(left, top, right, bottom), detected.Bounds), imageBounds);
                if (crop.Width < tileSize * 3 || crop.Height < tileSize * 3) return;

                using (Cv.Mat gray = CreateObjectDetectionDefectGrayRegionMat(source, crop))
                using (var polygonMask = new Cv.Mat(crop.Height, crop.Width, Cv.MatType.CV_8UC1, Cv.Scalar.Black))
                using (var textureMask = new Cv.Mat())
                using (var erosionKernel = Cv.Cv2.GetStructuringElement(Cv.MorphShapes.Rect, new Cv.Size(3, 3)))
                using (var gradientX = new Cv.Mat())
                using (var gradientY = new Cv.Mat())
                using (var gradientMagnitude = new Cv.Mat())
                {
                    Stopwatch preview = Stopwatch.StartNew();
                    Bitmap previewBitmap = CreateObjectDetectionDefectPreviewBitmap(gray);
                    preview.Stop();
                    Interlocked.Add(ref previewTicks, preview.ElapsedTicks);
                    patchesByObject[objectIndex] = new ObjectDetectionDefectProcessedPatch
                    {
                        Bounds = crop,
                        InspectionPolygon = corners.ToArray(),
                        ProcessedImage = previewBitmap
                    };

                    Cv.Point[] polygon = corners.Select(point => new Cv.Point(
                        (int)Math.Round(point.X - crop.X), (int)Math.Round(point.Y - crop.Y))).ToArray();
                    Cv.Cv2.FillPoly(polygonMask, new[] { polygon }, Cv.Scalar.White);
                    Cv.Cv2.Erode(polygonMask, textureMask, erosionKernel, iterations: 1);
                    Stopwatch textureCalculation = Stopwatch.StartNew();
                    Cv.Cv2.Sobel(gray, gradientX, Cv.MatType.CV_32FC1, 1, 0, 3);
                    Cv.Cv2.Sobel(gray, gradientY, Cv.MatType.CV_32FC1, 0, 1, 3);
                    Cv.Cv2.Magnitude(gradientX, gradientY, gradientMagnitude);

                    int columns = (crop.Width + tileSize - 1) / tileSize;
                    int rows = (crop.Height + tileSize - 1) / tileSize;
                    if (columns < 3 || rows < 3) return;
                    int tileCount = rows * columns;
                    Interlocked.Add(ref scannedTiles, tileCount);
                    var tileActivity = new float[tileCount];
                    var validTiles = new bool[tileCount];
                    using (var activityMap = new Cv.Mat(rows, columns, Cv.MatType.CV_32FC1, Cv.Scalar.All(0)))
                    using (var validMap = new Cv.Mat(rows, columns, Cv.MatType.CV_32FC1, Cv.Scalar.All(0)))
                    using (var localActivity = new Cv.Mat())
                    using (var localWeight = new Cv.Mat())
                    using (var thresholdMap = new Cv.Mat(rows, columns, Cv.MatType.CV_8UC1, Cv.Scalar.All(0)))
                    {
                        float* gradient = (float*)gradientMagnitude.Data.ToPointer();
                        byte* mask = (byte*)textureMask.Data.ToPointer();
                        float* activity = (float*)activityMap.Data.ToPointer();
                        float* valid = (float*)validMap.Data.ToPointer();
                        long gradientStep = gradientMagnitude.Step() / sizeof(float);
                        long maskStep = textureMask.Step();
                        long activityStep = activityMap.Step() / sizeof(float);
                        long validStep = validMap.Step() / sizeof(float);
                        for (int tileY = 0; tileY < rows; tileY++)
                        {
                            int y0 = tileY * tileSize;
                            int y1 = Math.Min(crop.Height, y0 + tileSize);
                            for (int tileX = 0; tileX < columns; tileX++)
                            {
                                int x0 = tileX * tileSize;
                                int x1 = Math.Min(crop.Width, x0 + tileSize);
                                double sum = 0;
                                int count = 0;
                                int tileArea = (x1 - x0) * (y1 - y0);
                                for (int y = y0; y < y1; y++)
                                {
                                    byte* maskRow = mask + ((long)y * maskStep);
                                    float* gradientRow = gradient + ((long)y * gradientStep);
                                    for (int x = x0; x < x1; x++)
                                    {
                                        if (maskRow[x] == 0) continue;
                                        sum += gradientRow[x];
                                        count++;
                                    }
                                }
                                int index = tileY * columns + tileX;
                                if (count < Math.Max(4, tileArea * 0.35)) continue;
                                float mean = (float)(sum / count);
                                tileActivity[index] = mean;
                                validTiles[index] = true;
                                activity[tileY * activityStep + tileX] = mean;
                                valid[tileY * validStep + tileX] = 1;
                            }
                        }

                        Cv.Cv2.GaussianBlur(activityMap, localActivity, new Cv.Size(9, 9), 0);
                        Cv.Cv2.GaussianBlur(validMap, localWeight, new Cv.Size(9, 9), 0);
                        float* local = (float*)localActivity.Data.ToPointer();
                        float* weight = (float*)localWeight.Data.ToPointer();
                        byte* binary = (byte*)thresholdMap.Data.ToPointer();
                        long localStep = localActivity.Step() / sizeof(float);
                        long weightStep = localWeight.Step() / sizeof(float);
                        long binaryStep = thresholdMap.Step();
                        var residuals = new List<double>();
                        var residualByTile = new float[tileCount];
                        for (int tileY = 0; tileY < rows; tileY++)
                        {
                            for (int tileX = 0; tileX < columns; tileX++)
                            {
                                int index = tileY * columns + tileX;
                                if (!validTiles[index]) continue;
                                float localCoverage = weight[tileY * weightStep + tileX];
                                float background = localCoverage > 0.01f
                                    ? local[tileY * localStep + tileX] / localCoverage
                                    : tileActivity[index];
                                float residual = Math.Abs(tileActivity[index] - background);
                                residualByTile[index] = residual;
                                residuals.Add(residual);
                            }
                        }
                        if (residuals.Count < Math.Max(8, parameter.DefectLineTextureMinimumLengthPixels / tileSize)) return;
                        double median = CalculateObjectDetectionFrequencyMedian(residuals);
                        double mad = CalculateObjectDetectionFrequencyMedian(
                            residuals.Select(value => Math.Abs(value - median)).ToList());
                        double scale = Math.Max(0.25, mad * 1.4826);
                        var scores = new double[tileCount];
                        var candidates = new List<double>();
                        for (int tileY = 0; tileY < rows; tileY++)
                        {
                            byte* binaryRow = binary + ((long)tileY * binaryStep);
                            for (int tileX = 0; tileX < columns; tileX++)
                            {
                                int index = tileY * columns + tileX;
                                if (!validTiles[index]) continue;
                                double score = Math.Max(0, residualByTile[index] - median) / scale;
                                scores[index] = score;
                                if (score >= parameter.DefectLineTextureSensitivity)
                                {
                                    binaryRow[tileX] = 255;
                                    candidates.Add(score);
                                }
                                if (score >= parameter.DefectLineTextureSensitivity * 0.45)
                                {
                                    cells.Add(new ObjectDetectionLineTextureCell
                                    {
                                        ObjectNumber = detected.Number,
                                        Bounds = new Rectangle(crop.X + tileX * tileSize, crop.Y + tileY * tileSize,
                                            Math.Min(tileSize, crop.Width - tileX * tileSize),
                                            Math.Min(tileSize, crop.Height - tileY * tileSize)),
                                        Score = score,
                                        IsAnomaly = score >= parameter.DefectLineTextureSensitivity
                                    });
                                }
                            }
                        }

                        if (candidates.Count > 0)
                        {
                            int minimumLengthTiles = Math.Max(3,
                                (int)Math.Ceiling(parameter.DefectLineTextureMinimumLengthPixels / (double)tileSize));
                            int maximumGapTiles = Math.Max(0,
                                (int)Math.Ceiling(parameter.DefectLineTextureMaximumGapPixels / (double)tileSize));
                            int houghThreshold = Math.Max(2,
                                (int)Math.Ceiling(minimumLengthTiles * parameter.DefectLineTextureMinimumSupportRatio));
                            Cv.LineSegmentPoint[] detectedLines = Cv.Cv2.HoughLinesP(
                                thresholdMap,
                                1,
                                Math.PI / 180.0,
                                houghThreshold,
                                minimumLengthTiles,
                                maximumGapTiles);
                            foreach (Cv.LineSegmentPoint detectedLine in detectedLines)
                            {
                                ObjectDetectionLineTextureSegment line = CreateObjectDetectionLineTextureSegment(
                                    detected.Number,
                                    detectedLine.P1,
                                    detectedLine.P2,
                                    crop,
                                    tileSize,
                                    rows,
                                    columns,
                                    validTiles,
                                    scores,
                                    parameter.DefectLineTextureSensitivity,
                                    parameter.DefectLineTextureMinimumSupportRatio);
                                if (line != null) lines.Add(line);
                            }
                            RemoveDuplicateObjectDetectionLineTextureSegments(lines, tileSize);
                        }
                    }
                    textureCalculation.Stop();
                    Interlocked.Add(ref textureTicks, textureCalculation.ElapsedTicks);
                }
                progress?.Report("線狀紋理掃描 ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
                    objects.Count.ToString(CultureInfo.CurrentCulture) + " 完成... ");
            };

            if (runParallel && objects.Count > 1)
            {
                try
                {
                    Parallel.For(0, objects.Count,
                        new ParallelOptions { MaxDegreeOfParallelism = objects.Count }, scanObject);
                }
                catch
                {
                    foreach (ObjectDetectionDefectProcessedPatch patch in patchesByObject) DisposeObjectDetectionDefectProcessedPatch(patch);
                    throw;
                }
            }
            else
            {
                try
                {
                    for (int index = 0; index < objects.Count; index++) scanObject(index);
                }
                catch
                {
                    foreach (ObjectDetectionDefectProcessedPatch patch in patchesByObject) DisposeObjectDetectionDefectProcessedPatch(patch);
                    throw;
                }
            }

            var result = new ObjectDetectionLineTextureResult
            {
                Cells = new List<ObjectDetectionLineTextureCell>(),
                Lines = new List<ObjectDetectionLineTextureSegment>(),
                CellsByObject = new Dictionary<int, List<ObjectDetectionLineTextureCell>>(),
                LinesByObject = new Dictionary<int, List<ObjectDetectionLineTextureSegment>>(),
                ObjectPolygons = new Dictionary<int, PointF[]>(),
                ProcessedPatches = new List<ObjectDetectionDefectProcessedPatch>(),
                ScannedTileCount = scannedTiles,
                TextureCalculationMilliseconds = ConvertObjectDetectionFrequencyTicksToMilliseconds(textureTicks),
                PreviewGenerationMilliseconds = ConvertObjectDetectionFrequencyTicksToMilliseconds(previewTicks)
            };
            for (int index = 0; index < objects.Count; index++)
            {
                if (cellsByObject[index] == null) continue;
                int number = objects[index].Number;
                result.CellsByObject[number] = cellsByObject[index];
                result.LinesByObject[number] = linesByObject[index] ?? new List<ObjectDetectionLineTextureSegment>();
                result.ObjectPolygons[number] = polygonsByObject[index];
                result.Cells.AddRange(cellsByObject[index]);
                result.Lines.AddRange(result.LinesByObject[number]);
                if (patchesByObject[index] != null)
                {
                    result.ProcessedPatches.Add(patchesByObject[index]);
                    patchesByObject[index] = null;
                }
            }
            return result;
        }

        private static ObjectDetectionLineTextureSegment CreateObjectDetectionLineTextureSegment(
            int objectNumber,
            Cv.Point tileStart,
            Cv.Point tileEnd,
            Rectangle crop,
            int tileSize,
            int rows,
            int columns,
            bool[] validTiles,
            double[] scores,
            double scoreThreshold,
            double minimumSupportRatio)
        {
            double deltaX = tileEnd.X - tileStart.X;
            double deltaY = tileEnd.Y - tileStart.Y;
            double tileLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (tileLength < 1) return null;
            int sampleCount = Math.Max(2, (int)Math.Ceiling(tileLength * 2));
            int validCount = 0;
            int hitCount = 0;
            double scoreSum = 0;
            var visited = new HashSet<int>();
            for (int sample = 0; sample <= sampleCount; sample++)
            {
                double amount = sample / (double)sampleCount;
                int x = (int)Math.Round(tileStart.X + deltaX * amount);
                int y = (int)Math.Round(tileStart.Y + deltaY * amount);
                if (x < 0 || x >= columns || y < 0 || y >= rows) continue;
                int index = y * columns + x;
                if (!visited.Add(index) || !validTiles[index]) continue;
                validCount++;
                if (scores[index] < scoreThreshold) continue;
                hitCount++;
                scoreSum += scores[index];
            }
            double support = validCount == 0 ? 0 : hitCount / (double)validCount;
            if (support < minimumSupportRatio || hitCount < 3) return null;

            PointF start = new PointF(
                crop.X + (tileStart.X + 0.5f) * tileSize,
                crop.Y + (tileStart.Y + 0.5f) * tileSize);
            PointF end = new PointF(
                crop.X + (tileEnd.X + 0.5f) * tileSize,
                crop.Y + (tileEnd.Y + 0.5f) * tileSize);
            int margin = Math.Max(2, tileSize / 2);
            Rectangle bounds = Rectangle.Intersect(
                Rectangle.FromLTRB(
                    (int)Math.Floor(Math.Min(start.X, end.X)) - margin,
                    (int)Math.Floor(Math.Min(start.Y, end.Y)) - margin,
                    (int)Math.Ceiling(Math.Max(start.X, end.X)) + margin + 1,
                    (int)Math.Ceiling(Math.Max(start.Y, end.Y)) + margin + 1),
                crop);
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;
            return new ObjectDetectionLineTextureSegment
            {
                ObjectNumber = objectNumber,
                Start = start,
                End = end,
                Bounds = bounds,
                Score = scoreSum / hitCount,
                SupportRatio = support,
                IsAnomaly = true
            };
        }

        private static void RemoveDuplicateObjectDetectionLineTextureSegments(
            List<ObjectDetectionLineTextureSegment> lines,
            int tileSize)
        {
            if (lines == null || lines.Count < 2) return;
            List<ObjectDetectionLineTextureSegment> ordered = lines
                .OrderByDescending(line => line.Bounds.Width + line.Bounds.Height)
                .ThenByDescending(line => line.Score)
                .ToList();
            var kept = new List<ObjectDetectionLineTextureSegment>();
            var occupiedLineBins = new HashSet<long>();
            const double angleStep = Math.PI / 36.0;
            int angleBinCount = 36;
            double rhoStep = Math.Max(1.0, tileSize * 0.5);
            foreach (ObjectDetectionLineTextureSegment candidate in ordered)
            {
                double angle = Math.Atan2(candidate.End.Y - candidate.Start.Y, candidate.End.X - candidate.Start.X);
                if (angle < 0) angle += Math.PI;
                if (angle >= Math.PI) angle -= Math.PI;
                int angleBin = (int)Math.Round(angle / angleStep) % angleBinCount;
                double middleX = (candidate.Start.X + candidate.End.X) * 0.5;
                double middleY = (candidate.Start.Y + candidate.End.Y) * 0.5;
                int rhoBin = (int)Math.Round((middleX * Math.Cos(angle) + middleY * Math.Sin(angle)) / rhoStep);
                bool duplicate = false;
                for (int rhoOffset = -1; rhoOffset <= 1; rhoOffset++)
                {
                    long key = ((long)angleBin << 32) ^ (uint)(rhoBin + rhoOffset);
                    if (occupiedLineBins.Contains(key))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;
                occupiedLineBins.Add(((long)angleBin << 32) ^ (uint)rhoBin);
                kept.Add(candidate);
            }
            lines.Clear();
            lines.AddRange(kept);
        }

        private string CreateObjectDetectionLineTextureSignature(
            ObjectDetectionParameterSettings parameter,
            string definitionSignature,
            int imageGeneration,
            int flatFieldGeneration)
        {
            return string.Join("|", new[]
            {
                parameter.Id ?? string.Empty,
                parameter.DefectLineTextureEnabled ? "1" : "0",
                definitionSignature ?? string.Empty,
                imageGeneration.ToString(CultureInfo.InvariantCulture),
                flatFieldGeneration.ToString(CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionId ?? string.Empty,
                parameter.DefectInspectionRegionLeft.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionTop.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionRight.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionBottom.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectLineTextureTileSizePixels.ToString(CultureInfo.InvariantCulture),
                parameter.DefectLineTextureMinimumLengthPixels.ToString(CultureInfo.InvariantCulture),
                parameter.DefectLineTextureMaximumGapPixels.ToString(CultureInfo.InvariantCulture),
                parameter.DefectLineTextureSensitivity.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectLineTextureMinimumSupportRatio.ToString("R", CultureInfo.InvariantCulture),
                parameter.FlatFieldTargetGray.ToString(CultureInfo.InvariantCulture),
                parameter.FlatFieldSavedSettingsSignature ?? string.Empty,
                CreateObjectDetectionFlatFieldSettingsSignature(parameter)
            });
        }

        private bool IsObjectDetectionDefectLineTextureIntegrationRequired(ObjectDetectionParameterSettings parameter)
        {
            return parameter != null && parameter.DefectIntegrationIncludeLineTexture && parameter.DefectLineTextureEnabled;
        }

        private bool TryGetCurrentObjectDetectionLineTextureResult(
            ObjectDetectionParameterSettings parameter,
            out ObjectDetectionLineTextureResult result)
        {
            result = null;
            if (parameter == null || !parameter.DefectLineTextureEnabled ||
                !objectDetectionLineTextureResults.TryGetValue(parameter.Id, out result)) return false;
            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                RemoveObjectDetectionLineTextureResult(parameter.Id);
                result = null;
                return false;
            }
            string signature = CreateObjectDetectionLineTextureSignature(
                parameter,
                CreateObjectDefinitionProcessingSignature(definition),
                imageSourceGeneration,
                objectDetectionFlatFieldEvaluationGeneration);
            if (!string.Equals(result.Signature, signature, StringComparison.Ordinal))
            {
                RemoveObjectDetectionLineTextureResult(parameter.Id);
                result = null;
                return false;
            }
            return true;
        }

        private void DrawObjectDetectionLineTextureAnomalies(
            Graphics graphics,
            float zoom,
            PointF offset,
            Rectangle visibleSourceRect)
        {
            if (graphics == null || zoom <= 0 || !isObjectDetectionParameterImageLayout) return;
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDetectionLineTextureResult result;
            if (!TryGetCurrentObjectDetectionLineTextureResult(parameter, out result)) return;
            Rectangle visible = visibleSourceRect;
            if (result.ProcessedPatches != null)
            {
                foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
                {
                    if (patch == null || patch.ProcessedImage == null || !patch.Bounds.IntersectsWith(visible)) continue;
                    GraphicsState state = graphics.Save();
                    try
                    {
                        if (patch.InspectionPolygon != null && patch.InspectionPolygon.Length >= 3)
                        {
                            using (var clip = new GraphicsPath())
                            {
                                clip.AddPolygon(patch.InspectionPolygon.Select(point => new PointF(
                                    offset.X + point.X * zoom, offset.Y + point.Y * zoom)).ToArray());
                                graphics.SetClip(clip, CombineMode.Intersect);
                            }
                        }
                        graphics.DrawImage(patch.ProcessedImage, new RectangleF(
                            offset.X + patch.Bounds.X * zoom,
                            offset.Y + patch.Bounds.Y * zoom,
                            patch.Bounds.Width * zoom,
                            patch.Bounds.Height * zoom));
                    }
                    finally { graphics.Restore(state); }
                }
            }

            using (var lineShadow = new Pen(Color.FromArgb(220, 45, 32, 42), Math.Max(2f, Math.Min(6f, zoom * 4f))))
            using (var linePen = new Pen(Color.FromArgb(245, 255, 65, 90), Math.Max(1.5f, Math.Min(4f, zoom * 2.4f))))
            {
                foreach (KeyValuePair<int, List<ObjectDetectionLineTextureCell>> entry in result.CellsByObject)
                {
                    PointF[] polygon;
                    if (!result.ObjectPolygons.TryGetValue(entry.Key, out polygon) || polygon == null || polygon.Length < 3) continue;
                    GraphicsState state = graphics.Save();
                    try
                    {
                        using (var clip = new GraphicsPath())
                        {
                            clip.AddPolygon(polygon.Select(point => new PointF(
                                offset.X + point.X * zoom, offset.Y + point.Y * zoom)).ToArray());
                            graphics.SetClip(clip, CombineMode.Intersect);
                        }
                        if (parameter.DefectLineTextureShowHeatmap)
                        {
                            foreach (ObjectDetectionLineTextureCell cell in entry.Value)
                            {
                                if (!cell.Bounds.IntersectsWith(visible)) continue;
                                int alpha = Math.Max(12, Math.Min(100, 16 + (int)(cell.Score * 8)));
                                using (var fill = new SolidBrush(Color.FromArgb(alpha, 255, 188, 55)))
                                {
                                    graphics.FillRectangle(fill, new RectangleF(
                                        offset.X + cell.Bounds.X * zoom,
                                        offset.Y + cell.Bounds.Y * zoom,
                                        Math.Max(1, cell.Bounds.Width * zoom),
                                        Math.Max(1, cell.Bounds.Height * zoom)));
                                }
                            }
                        }
                        if (parameter.DefectLineTextureShowAnomalyLines)
                        {
                            List<ObjectDetectionLineTextureSegment> lines;
                            if (result.LinesByObject.TryGetValue(entry.Key, out lines))
                            {
                                foreach (ObjectDetectionLineTextureSegment line in lines)
                                {
                                    if (!line.IsAnomaly || !line.Bounds.IntersectsWith(visible)) continue;
                                    PointF start = new PointF(offset.X + line.Start.X * zoom, offset.Y + line.Start.Y * zoom);
                                    PointF end = new PointF(offset.X + line.End.X * zoom, offset.Y + line.End.Y * zoom);
                                    graphics.DrawLine(lineShadow, start, end);
                                    graphics.DrawLine(linePen, start, end);
                                }
                            }
                        }
                    }
                    finally { graphics.Restore(state); }
                }
            }
        }

        private static void DisposeObjectDetectionLineTextureResult(ObjectDetectionLineTextureResult result)
        {
            if (result == null || result.ProcessedPatches == null) return;
            foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
            {
                DisposeObjectDetectionDefectProcessedPatch(patch);
            }
            result.ProcessedPatches.Clear();
        }
    }
}
