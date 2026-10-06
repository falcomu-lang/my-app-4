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

        private sealed class ObjectDetectionTextureAnomalyRegion
        {
            public int ObjectNumber { get; set; }

            public Rectangle Bounds { get; set; }

            public double Score { get; set; }

            public int TileCount { get; set; }

            public long AreaPixels { get; set; }

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

            public Dictionary<int, List<ObjectDetectionTextureAnomalyRegion>> RegionsByObject { get; set; }

            public List<ObjectDetectionLineTextureCell> Cells { get; set; }

            public List<ObjectDetectionTextureAnomalyRegion> Regions { get; set; }

            public List<ObjectDetectionDefectProcessedPatch> ProcessedPatches { get; set; }
        }

        private readonly Dictionary<string, ObjectDetectionLineTextureResult> objectDetectionLineTextureResults =
            new Dictionary<string, ObjectDetectionLineTextureResult>(StringComparer.Ordinal);

        private CheckBox objectDetectionLineTextureEnabledCheckBox;
        private NumericUpDown objectDetectionLineTextureTileSizeInput;
        private NumericUpDown objectDetectionLineTextureMinimumAreaInput;
        private NumericUpDown objectDetectionLineTextureSensitivityInput;
        private CheckBox objectDetectionLineTextureShowHeatmapCheckBox;
        private CheckBox objectDetectionLineTextureShowRegionsCheckBox;
        private Label objectDetectionLineTextureStatusLabel;
        private Button objectDetectionLineTextureRunButton;
        private string objectDetectionLineTextureDraftParameterId;
        private bool objectDetectionLineTextureAnalysisRunning;

        private TabPage BuildObjectDetectionLineTextureTab(ObjectDetectionParameterSettings parameter)
        {
            var page = new TabPage("紋理異常");
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
                Text = "分析來源：平場校正後的灰階影像，不追加對比或淡色缺陷增強。\r\n比較局部紋理排列、強度與方向，找出偏離物件內多數正常紋理的區域。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(55, 63, 76)
            };

            var enabledPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
            objectDetectionLineTextureEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "是否使用紋理異常",
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
                Height = 160,
                Text = "紋理異常掃描與判定",
                Padding = new Padding(8, 16, 8, 4)
            };
            var scanLayout = CreateDefectCoreTable(4, 2);
            scanLayout.RowStyles.Clear();
            for (int row = 0; row < 4; row++) scanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            objectDetectionLineTextureTileSizeInput = CreateLineTextureNumeric(
                4, 128, 4, Math.Max(4, Math.Min(128, parameter.DefectLineTextureTileSizePixels)), 0);
            objectDetectionLineTextureMinimumAreaInput = CreateLineTextureNumeric(
                0, 1000000000, 16, Math.Max(0, Math.Min(1000000000, parameter.DefectLineTextureMinimumAreaPixels)), 0);
            objectDetectionLineTextureSensitivityInput = CreateLineTextureNumeric(
                0.5m, 8m, 0.25m, (decimal)Math.Max(0.5, Math.Min(8, parameter.DefectLineTextureSensitivity)), 2);

            AddLineTextureScanRow(scanLayout, 0, "紋理格大小 (px)", objectDetectionLineTextureTileSizeInput);
            AddLineTextureScanRow(scanLayout, 1, "最小異常面積 (px²)", objectDetectionLineTextureMinimumAreaInput);
            AddLineTextureScanRow(scanLayout, 2, "紋理異常敏感度", objectDetectionLineTextureSensitivityInput);
            var scanHint = new Label
            {
                Dock = DockStyle.Fill,
                Text = "需多項紋理特徵共同偏離，且鄰格方向連續才判異常。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                AutoEllipsis = true
            };
            scanLayout.Controls.Add(scanHint, 0, 3);
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
            objectDetectionLineTextureShowRegionsCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示異常區域",
                Checked = parameter.DefectLineTextureShowAnomalyLines,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionLineTextureShowHeatmapCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureDisplayOptions(parameter);
            };
            objectDetectionLineTextureShowRegionsCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureDisplayOptions(parameter);
            };
            displayOptions.Controls.Add(objectDetectionLineTextureShowHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionLineTextureShowRegionsCheckBox);

            objectDetectionLineTextureStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 58,
                Padding = new Padding(8, 4, 8, 2),
                Text = parameter.DefectLineTextureEnabled ? "尚未執行紋理異常分析。" : "紋理異常分析已停用。",
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
                objectDetectionLineTextureMinimumAreaInput,
                objectDetectionLineTextureSensitivityInput,
                objectDetectionLineTextureShowHeatmapCheckBox,
                objectDetectionLineTextureShowRegionsCheckBox
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
                ? "紋理異常已啟用；需執行分析後才有結果。"
                : "紋理異常已停用，相關標記已清除。 ");
        }

        private bool ApplyObjectDetectionLineTextureSettings(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || !string.Equals(objectDetectionLineTextureDraftParameterId, parameter.Id, StringComparison.Ordinal) ||
                objectDetectionLineTextureTileSizeInput == null || objectDetectionLineTextureMinimumAreaInput == null)
            {
                return false;
            }

            parameter.DefectLineTextureTileSizePixels = (int)objectDetectionLineTextureTileSizeInput.Value;
            parameter.DefectLineTextureMinimumAreaPixels = (int)objectDetectionLineTextureMinimumAreaInput.Value;
            parameter.DefectLineTextureSensitivity = Decimal.ToDouble(objectDetectionLineTextureSensitivityInput.Value);
            parameter.DefectLineTextureShowHeatmap = objectDetectionLineTextureShowHeatmapCheckBox.Checked;
            parameter.DefectLineTextureShowAnomalyLines = objectDetectionLineTextureShowRegionsCheckBox.Checked;
            RemoveObjectDetectionLineTextureResult(parameter.Id);
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            SaveSystemParameters();
            UpdateObjectDetectionLineTextureControlsEnabled(parameter.DefectLineTextureEnabled);
            ImageDisplayControl lineTextureDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectLineTextureDisplayIndex);
            if (lineTextureDisplay != null) lineTextureDisplay.InvalidateImageView();
            ImageDisplayControl integratedDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectIntegratedDisplayIndex);
            if (integratedDisplay != null) integratedDisplay.InvalidateImageView();
            SetObjectDetectionDefectRegionStatus(
                "紋理異常設定已套用；格 " + parameter.DefectLineTextureTileSizePixels.ToString(CultureInfo.CurrentCulture) +
                " px、最小異常面積 " + parameter.DefectLineTextureMinimumAreaPixels.ToString(CultureInfo.CurrentCulture) +
                " px²。請執行分析更新結果。 ");
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            return true;
        }

        private void CancelObjectDetectionLineTextureSettings(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionLineTextureTileSizeInput == null) return;
            objectDetectionLineTextureTileSizeInput.Value = Math.Max(4, Math.Min(128, parameter.DefectLineTextureTileSizePixels));
            objectDetectionLineTextureMinimumAreaInput.Value = Math.Max(0, Math.Min(1000000000, parameter.DefectLineTextureMinimumAreaPixels));
            objectDetectionLineTextureSensitivityInput.Value = (decimal)Math.Max(0.5, Math.Min(8, parameter.DefectLineTextureSensitivity));
            objectDetectionLineTextureShowHeatmapCheckBox.Checked = parameter.DefectLineTextureShowHeatmap;
            objectDetectionLineTextureShowRegionsCheckBox.Checked = parameter.DefectLineTextureShowAnomalyLines;
            UpdateObjectDetectionLineTextureControlsEnabled(parameter.DefectLineTextureEnabled);
            SetObjectDetectionDefectRegionStatus("已取消紋理異常設定變更。 ");
        }

        private void UpdateObjectDetectionLineTextureDisplayOptions(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionLineTextureShowHeatmapCheckBox == null ||
                objectDetectionLineTextureShowRegionsCheckBox == null) return;
            parameter.DefectLineTextureShowHeatmap = objectDetectionLineTextureShowHeatmapCheckBox.Checked;
            parameter.DefectLineTextureShowAnomalyLines = objectDetectionLineTextureShowRegionsCheckBox.Checked;
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
                SetObjectDetectionDefectRegionStatus("紋理異常分析目前未啟用。 ");
                return;
            }
            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId, parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                SetObjectDetectionDefectRegionStatus("請先框選並套用缺陷檢測範圍，再執行紋理異常分析。 ");
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
                    SetObjectDetectionDefectRegionStatus("沒有已完成定義的 ROI 物件可供紋理異常分析。 ");
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
                    ? "紋理異常分析中：各物件 ROI 同時處理... "
                    : "紋理異常分析中：各物件 ROI 依序處理... ");
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
                    SetObjectDetectionDefectRegionStatus("分析期間影像或設定已變更，紋理異常結果未套用；請重新分析。 ");
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
                int anomalies = result.Regions.Count(region => region.IsAnomaly);
                string summary = string.Format(
                    CultureInfo.CurrentCulture,
                    "檢查 {0:N0} 個紋理格，檢出 {1:N0} 個異常區域；總耗時 {2:N0} ms（來源準備 {3:N0}、掃描 {4:N0} ms）",
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
                SetObjectDetectionDefectRegionStatus("紋理異常分析記憶體不足；請增大紋理格或減少同時處理的 ROI。 ");
            }
            catch (Exception exception)
            {
                SetObjectDetectionDefectRegionStatus("紋理異常分析失敗：" + exception.Message);
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
            var regionsByObject = new List<ObjectDetectionTextureAnomalyRegion>[objects.Count];
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
                var regions = new List<ObjectDetectionTextureAnomalyRegion>();
                cellsByObject[objectIndex] = cells;
                regionsByObject[objectIndex] = regions;
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
                    var tileOrientationCos = new float[tileCount];
                    var tileOrientationSin = new float[tileCount];
                    var tileDirectionalSupport = new float[tileCount];
                    var tilePatternUniformRatio = new float[tileCount];
                    var tilePatternTransitionMean = new float[tileCount];
                    var tileValidPixelCounts = new int[tileCount];
                    var validTiles = new bool[tileCount];
                    using (var activityMap = new Cv.Mat(rows, columns, Cv.MatType.CV_32FC1, Cv.Scalar.All(0)))
                    using (var validMap = new Cv.Mat(rows, columns, Cv.MatType.CV_32FC1, Cv.Scalar.All(0)))
                    using (var localActivity = new Cv.Mat())
                    using (var localWeight = new Cv.Mat())
                    using (var thresholdMap = new Cv.Mat(rows, columns, Cv.MatType.CV_8UC1, Cv.Scalar.All(0)))
                    {
                        float* gradient = (float*)gradientMagnitude.Data.ToPointer();
                        float* gradientXData = (float*)gradientX.Data.ToPointer();
                        float* gradientYData = (float*)gradientY.Data.ToPointer();
                        byte* grayData = (byte*)gray.Data.ToPointer();
                        byte* mask = (byte*)textureMask.Data.ToPointer();
                        float* activity = (float*)activityMap.Data.ToPointer();
                        float* valid = (float*)validMap.Data.ToPointer();
                        long gradientStep = gradientMagnitude.Step() / sizeof(float);
                        long gradientXStep = gradientX.Step() / sizeof(float);
                        long gradientYStep = gradientY.Step() / sizeof(float);
                        long grayStep = gray.Step();
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
                                // Doubled-angle gradient moments compare texture axes without treating opposite edges as different directions.
                                double orientationCos = 0;
                                double orientationSin = 0;
                                double orientationEnergy = 0;
                                double transitionSum = 0;
                                int uniformPatternCount = 0;
                                int patternPixelCount = 0;
                                int count = 0;
                                int tileArea = (x1 - x0) * (y1 - y0);
                                for (int y = y0; y < y1; y++)
                                {
                                    byte* maskRow = mask + ((long)y * maskStep);
                                    float* gradientXRow = gradientXData + ((long)y * gradientXStep);
                                    float* gradientYRow = gradientYData + ((long)y * gradientYStep);
                                    float* gradientRow = gradient + ((long)y * gradientStep);
                                    byte* grayRow = grayData + ((long)y * grayStep);
                                    byte* grayAbove = y > 0 ? grayData + ((long)(y - 1) * grayStep) : null;
                                    byte* grayBelow = y + 1 < crop.Height ? grayData + ((long)(y + 1) * grayStep) : null;
                                    for (int x = x0; x < x1; x++)
                                    {
                                        if (maskRow[x] == 0) continue;
                                        float magnitude = gradientRow[x];
                                        double gx = gradientXRow[x];
                                        double gy = gradientYRow[x];
                                        sum += magnitude;
                                        count++;
                                        if (x > 0 && x + 1 < crop.Width && grayAbove != null && grayBelow != null)
                                        {
                                            byte center = grayRow[x];
                                            int patternCode = 0;
                                            if (grayAbove[x - 1] >= center) patternCode |= 1 << 0;
                                            if (grayAbove[x] >= center) patternCode |= 1 << 1;
                                            if (grayAbove[x + 1] >= center) patternCode |= 1 << 2;
                                            if (grayRow[x + 1] >= center) patternCode |= 1 << 3;
                                            if (grayBelow[x + 1] >= center) patternCode |= 1 << 4;
                                            if (grayBelow[x] >= center) patternCode |= 1 << 5;
                                            if (grayBelow[x - 1] >= center) patternCode |= 1 << 6;
                                            if (grayRow[x - 1] >= center) patternCode |= 1 << 7;
                                            int transitionBits = (patternCode ^ ((patternCode << 1) | (patternCode >> 7))) & 0xFF;
                                            transitionBits -= (transitionBits >> 1) & 0x55;
                                            transitionBits = (transitionBits & 0x33) + ((transitionBits >> 2) & 0x33);
                                            int transitions = (transitionBits + (transitionBits >> 4)) & 0x0F;
                                            transitionSum += transitions;
                                            if (transitions <= 2) uniformPatternCount++;
                                            patternPixelCount++;
                                        }
                                        if (magnitude <= 0.0001f) continue;
                                        orientationCos += gx * gx - gy * gy;
                                        orientationSin += 2 * gx * gy;
                                        orientationEnergy += gx * gx + gy * gy;
                                    }
                                }
                                int index = tileY * columns + tileX;
                                if (count < Math.Max(4, tileArea * 0.35)) continue;
                                float mean = (float)(sum / count);
                                if (orientationEnergy > 0)
                                {
                                    tileOrientationCos[index] = (float)(orientationCos / orientationEnergy);
                                    tileOrientationSin[index] = (float)(orientationSin / orientationEnergy);
                                }
                                tileActivity[index] = mean;
                                if (patternPixelCount > 0)
                                {
                                    tilePatternUniformRatio[index] = (float)uniformPatternCount / patternPixelCount;
                                    tilePatternTransitionMean[index] = (float)(transitionSum / patternPixelCount);
                                }
                                tileValidPixelCounts[index] = count;
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
                        var validIndices = new List<int>();
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
                                validIndices.Add(index);
                            }
                        }
                        if (residuals.Count < 8) return;

                        const int directionSupportRadius = 2;
                        foreach (int index in validIndices)
                        {
                            int tileX = index % columns;
                            int tileY = index / columns;
                            double directionCos = 0;
                            double directionSin = 0;
                            double directionEnergy = 0;
                            int directionNeighborCount = 0;
                            for (int neighborY = Math.Max(0, tileY - directionSupportRadius);
                                neighborY <= Math.Min(rows - 1, tileY + directionSupportRadius); neighborY++)
                            {
                                for (int neighborX = Math.Max(0, tileX - directionSupportRadius);
                                    neighborX <= Math.Min(columns - 1, tileX + directionSupportRadius); neighborX++)
                                {
                                    if (neighborX == tileX && neighborY == tileY) continue;
                                    int neighborIndex = neighborY * columns + neighborX;
                                    if (!validTiles[neighborIndex]) continue;
                                    double neighborCos = tileOrientationCos[neighborIndex];
                                    double neighborSin = tileOrientationSin[neighborIndex];
                                    double neighborEnergy = Math.Sqrt(neighborCos * neighborCos + neighborSin * neighborSin);
                                    if (neighborEnergy < 0.04) continue;
                                    directionCos += neighborCos;
                                    directionSin += neighborSin;
                                    directionEnergy += neighborEnergy;
                                    directionNeighborCount++;
                                }
                            }
                            if (directionNeighborCount >= 2 && directionEnergy > 0)
                            {
                                tileDirectionalSupport[index] = (float)(Math.Sqrt(
                                    directionCos * directionCos + directionSin * directionSin) / directionEnergy);
                            }
                        }

                        double majorityOrientationCos = CalculateObjectDetectionFrequencyMedian(
                            validIndices.Select(index => (double)tileOrientationCos[index]).ToList());
                        double majorityOrientationSin = CalculateObjectDetectionFrequencyMedian(
                            validIndices.Select(index => (double)tileOrientationSin[index]).ToList());
                        var orientationDistances = new List<double>(validIndices.Count);
                        var orientationDistanceByTile = new float[tileCount];
                        foreach (int index in validIndices)
                        {
                            double deltaCos = tileOrientationCos[index] - majorityOrientationCos;
                            double deltaSin = tileOrientationSin[index] - majorityOrientationSin;
                            double distance = Math.Sqrt(deltaCos * deltaCos + deltaSin * deltaSin);
                            orientationDistanceByTile[index] = (float)distance;
                            orientationDistances.Add(distance);
                        }

                        double median = CalculateObjectDetectionFrequencyMedian(residuals);
                        double mad = CalculateObjectDetectionFrequencyMedian(
                            residuals.Select(value => Math.Abs(value - median)).ToList());
                        double activityScale = Math.Max(0.25, mad * 1.4826);
                        double orientationMedian = CalculateObjectDetectionFrequencyMedian(orientationDistances);
                        double orientationMad = CalculateObjectDetectionFrequencyMedian(
                            orientationDistances.Select(value => Math.Abs(value - orientationMedian)).ToList());
                        double orientationScale = Math.Max(0.025, orientationMad * 1.4826);
                        var patternUniformRatios = validIndices
                            .Select(index => (double)tilePatternUniformRatio[index]).ToList();
                        double patternUniformMedian = CalculateObjectDetectionFrequencyMedian(patternUniformRatios);
                        double patternUniformMad = CalculateObjectDetectionFrequencyMedian(
                            patternUniformRatios.Select(value => Math.Abs(value - patternUniformMedian)).ToList());
                        double patternUniformScale = Math.Max(0.04, patternUniformMad * 1.4826);
                        var patternTransitions = validIndices
                            .Select(index => (double)tilePatternTransitionMean[index]).ToList();
                        double patternTransitionMedian = CalculateObjectDetectionFrequencyMedian(patternTransitions);
                        double patternTransitionMad = CalculateObjectDetectionFrequencyMedian(
                            patternTransitions.Select(value => Math.Abs(value - patternTransitionMedian)).ToList());
                        double patternTransitionScale = Math.Max(0.75, patternTransitionMad * 1.4826);
                        var directionalSupports = validIndices
                            .Select(index => (double)tileDirectionalSupport[index]).ToList();
                        double directionalSupportMedian = CalculateObjectDetectionFrequencyMedian(directionalSupports);
                        double directionalSupportMad = CalculateObjectDetectionFrequencyMedian(
                            directionalSupports.Select(value => Math.Abs(value - directionalSupportMedian)).ToList());
                        double directionalSupportScale = Math.Max(0.05, directionalSupportMad * 1.4826);
                        var scores = new double[tileCount];
                        for (int tileY = 0; tileY < rows; tileY++)
                        {
                            byte* binaryRow = binary + ((long)tileY * binaryStep);
                            for (int tileX = 0; tileX < columns; tileX++)
                            {
                                int index = tileY * columns + tileX;
                                if (!validTiles[index]) continue;
                                double activityScore = Math.Max(0, residualByTile[index] - median) / activityScale;
                                double orientationScore = Math.Max(0, orientationDistanceByTile[index] - orientationMedian) /
                                    orientationScale;
                                double patternUniformScore = Math.Abs(tilePatternUniformRatio[index] - patternUniformMedian) /
                                    patternUniformScale;
                                double patternTransitionScore = Math.Abs(tilePatternTransitionMean[index] - patternTransitionMedian) /
                                    patternTransitionScale;
                                double patternScore = Math.Max(patternUniformScore, patternTransitionScore);
                                double directionalSupportScore = Math.Max(0,
                                    tileDirectionalSupport[index] - directionalSupportMedian) / directionalSupportScale;
                                // Local speckle is suppressed unless several features and neighboring directions agree.
                                double strongest = 0;
                                double secondStrongest = 0;
                                double contributionLimit = parameter.DefectLineTextureSensitivity * 1.5;
                                double activityContribution = Math.Min(activityScore, contributionLimit);
                                double orientationContribution = Math.Min(orientationScore, contributionLimit);
                                double patternContribution = Math.Min(patternScore, contributionLimit);
                                double directionContribution = Math.Min(directionalSupportScore, contributionLimit);
                                if (activityContribution >= strongest)
                                {
                                    secondStrongest = strongest;
                                    strongest = activityContribution;
                                }
                                else if (activityContribution > secondStrongest) secondStrongest = activityContribution;
                                if (orientationContribution >= strongest)
                                {
                                    secondStrongest = strongest;
                                    strongest = orientationContribution;
                                }
                                else if (orientationContribution > secondStrongest) secondStrongest = orientationContribution;
                                if (patternContribution >= strongest)
                                {
                                    secondStrongest = strongest;
                                    strongest = patternContribution;
                                }
                                else if (patternContribution > secondStrongest) secondStrongest = patternContribution;
                                if (directionContribution >= strongest)
                                {
                                    secondStrongest = strongest;
                                    strongest = directionContribution;
                                }
                                else if (directionContribution > secondStrongest) secondStrongest = directionContribution;
                                double score = strongest * 0.65 + secondStrongest * 0.35;
                                scores[index] = score;
                                if (score >= parameter.DefectLineTextureSensitivity)
                                {
                                    binaryRow[tileX] = 255;
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

                        using (var labels = new Cv.Mat())
                        using (var stats = new Cv.Mat())
                        using (var centroids = new Cv.Mat())
                        {
                            int componentCount = Cv.Cv2.ConnectedComponentsWithStats(
                                thresholdMap,
                                labels,
                                stats,
                                centroids,
                                Cv.PixelConnectivity.Connectivity8,
                                Cv.MatType.CV_32SC1);
                            var componentTileCounts = new int[componentCount];
                            var componentPixelCounts = new long[componentCount];
                            var componentScoreSums = new double[componentCount];
                            int* labelData = (int*)labels.Data.ToPointer();
                            long labelStep = labels.Step() / sizeof(int);
                            for (int tileY = 0; tileY < rows; tileY++)
                            {
                                int* labelRow = labelData + ((long)tileY * labelStep);
                                for (int tileX = 0; tileX < columns; tileX++)
                                {
                                    int index = tileY * columns + tileX;
                                    int label = labelRow[tileX];
                                    if (label <= 0 || !validTiles[index]) continue;
                                    componentTileCounts[label]++;
                                    componentPixelCounts[label] += tileValidPixelCounts[index];
                                    componentScoreSums[label] += scores[index];
                                }
                            }

                            long minimumArea = Math.Max(0, parameter.DefectLineTextureMinimumAreaPixels);
                            for (int label = 1; label < componentCount; label++)
                            {
                                long areaPixels = componentPixelCounts[label];
                                if (componentTileCounts[label] == 0 || areaPixels < minimumArea) continue;
                                int tileLeft = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Left);
                                int tileTop = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Top);
                                int componentWidth = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Width);
                                int componentHeight = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Height);
                                Rectangle bounds = Rectangle.Intersect(
                                    new Rectangle(crop.X + tileLeft * tileSize, crop.Y + tileTop * tileSize,
                                        componentWidth * tileSize, componentHeight * tileSize), crop);
                                regions.Add(new ObjectDetectionTextureAnomalyRegion
                                {
                                    ObjectNumber = detected.Number,
                                    Bounds = bounds,
                                    Score = componentScoreSums[label] / componentTileCounts[label],
                                    TileCount = componentTileCounts[label],
                                    AreaPixels = areaPixels,
                                    IsAnomaly = true
                                });
                            }
                        }
                    }
                    textureCalculation.Stop();
                    Interlocked.Add(ref textureTicks, textureCalculation.ElapsedTicks);
                }
                progress?.Report("紋理異常掃描 ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
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
                Regions = new List<ObjectDetectionTextureAnomalyRegion>(),
                CellsByObject = new Dictionary<int, List<ObjectDetectionLineTextureCell>>(),
                RegionsByObject = new Dictionary<int, List<ObjectDetectionTextureAnomalyRegion>>(),
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
                result.RegionsByObject[number] = regionsByObject[index] ?? new List<ObjectDetectionTextureAnomalyRegion>();
                result.ObjectPolygons[number] = polygonsByObject[index];
                result.Cells.AddRange(cellsByObject[index]);
                result.Regions.AddRange(result.RegionsByObject[number]);
                if (patchesByObject[index] != null)
                {
                    result.ProcessedPatches.Add(patchesByObject[index]);
                    patchesByObject[index] = null;
                }
            }
            return result;
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
                parameter.DefectLineTextureMinimumAreaPixels.ToString(CultureInfo.InvariantCulture),
                parameter.DefectLineTextureSensitivity.ToString("R", CultureInfo.InvariantCulture),
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

            using (var regionShadow = new Pen(Color.FromArgb(210, 70, 20, 35), Math.Max(2f, Math.Min(6f, zoom * 4f))))
            using (var regionPen = new Pen(Color.FromArgb(245, 255, 65, 90), Math.Max(1.5f, Math.Min(4f, zoom * 2.4f))))
            using (var regionFill = new SolidBrush(Color.FromArgb(38, 255, 65, 90)))
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
                            List<ObjectDetectionTextureAnomalyRegion> regions;
                            if (result.RegionsByObject.TryGetValue(entry.Key, out regions))
                            {
                                foreach (ObjectDetectionTextureAnomalyRegion region in regions)
                                {
                                    if (!region.IsAnomaly || !region.Bounds.IntersectsWith(visible)) continue;
                                    var bounds = new RectangleF(
                                        offset.X + region.Bounds.X * zoom,
                                        offset.Y + region.Bounds.Y * zoom,
                                        Math.Max(1f, region.Bounds.Width * zoom),
                                        Math.Max(1f, region.Bounds.Height * zoom));
                                    graphics.FillRectangle(regionFill, bounds);
                                    graphics.DrawRectangle(regionShadow, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                                    graphics.DrawRectangle(regionPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
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
