using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

        private sealed class ObjectDetectionLineTextureCandidate
        {
            public double AngleDegrees { get; set; }

            public double AngleRadians { get; set; }

            public double RhoTiles { get; set; }

            public double Score { get; set; }

            public double Contrast { get; set; }
        }

        private enum ObjectDetectionLineTextureDiagnosticLayer
        {
            FinalCandidates,
            RawDarkDensity,
            DirectionScore
        }

        private sealed class ObjectDetectionLineTextureDiagnosticPatch
        {
            public Rectangle Bounds { get; set; }

            public PointF[] InspectionPolygon { get; set; }

            public Bitmap Heatmap { get; set; }
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

            public ObjectDetectionLineTextureDiagnosticLayer DiagnosticLayer { get; set; }

            public Dictionary<int, ObjectDetectionLineTextureDiagnosticPatch> DiagnosticPatchesByObject { get; set; }
        }

        private readonly Dictionary<string, ObjectDetectionLineTextureResult> objectDetectionLineTextureResults =
            new Dictionary<string, ObjectDetectionLineTextureResult>(StringComparer.Ordinal);

        private CheckBox objectDetectionLineTextureEnabledCheckBox;
        private NumericUpDown objectDetectionLineTextureTileSizeInput;
        private NumericUpDown objectDetectionLineTextureMinimumAreaInput;
        private NumericUpDown objectDetectionLineTextureSensitivityInput;
        private CheckBox objectDetectionLineTextureShowHeatmapCheckBox;
        private CheckBox objectDetectionLineTextureShowRegionsCheckBox;
        private ComboBox objectDetectionLineTextureDiagnosticLayerComboBox;
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
                Text = "分析來源：平場校正後的灰階影像，不追加對比或淡色缺陷增強。\r\n熱圖圖層只供診斷；切換後請重新分析，診斷色階不影響最終判定。",
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
                Text = "以多方向長距離累積暗紋密度，並和路徑兩側比較；檢測範圍外不參與計算。",
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
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
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
            objectDetectionLineTextureDiagnosticLayerComboBox = new ComboBox
            {
                Width = 142,
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false,
                Margin = new Padding(2, 0, 0, 0)
            };
            objectDetectionLineTextureDiagnosticLayerComboBox.Items.AddRange(new object[]
            {
                "最終檢出分數",
                "局部暗像素密度",
                "方向候選分數"
            });
            objectDetectionLineTextureDiagnosticLayerComboBox.SelectedIndex = 0;
            objectDetectionLineTextureShowHeatmapCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureDisplayOptions(parameter);
            };
            objectDetectionLineTextureShowRegionsCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionLineTextureDisplayOptions(parameter);
            };
            objectDetectionLineTextureDiagnosticLayerComboBox.SelectedIndexChanged += delegate
            {
                ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                    ObjectDetectionDefectLineTextureDisplayIndex);
                if (display != null) display.InvalidateImageView();
                ObjectDetectionLineTextureResult current;
                if (TryGetCurrentObjectDetectionLineTextureResult(parameter, out current) &&
                    current.DiagnosticLayer != GetSelectedObjectDetectionLineTextureDiagnosticLayer())
                {
                    objectDetectionLineTextureStatusLabel.Text =
                        "診斷圖層已變更；請重新執行分析。診斷熱圖按每個物件分別拉伸色階，檢出框仍為目前有效結果。";
                }
            };
            displayOptions.Controls.Add(objectDetectionLineTextureShowHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionLineTextureShowRegionsCheckBox);
            displayOptions.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "熱圖來源",
                Margin = new Padding(10, 5, 1, 0)
            });
            displayOptions.Controls.Add(objectDetectionLineTextureDiagnosticLayerComboBox);

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
                objectDetectionLineTextureShowRegionsCheckBox,
                objectDetectionLineTextureDiagnosticLayerComboBox
            })
            {
                if (input != null) input.Enabled = enabled;
            }
            if (objectDetectionLineTextureRunButton != null)
            {
                objectDetectionLineTextureRunButton.Enabled = enabled && !objectDetectionLineTextureAnalysisRunning;
            }
        }

        private ObjectDetectionLineTextureDiagnosticLayer GetSelectedObjectDetectionLineTextureDiagnosticLayer()
        {
            if (objectDetectionLineTextureDiagnosticLayerComboBox == null)
            {
                return ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates;
            }

            switch (objectDetectionLineTextureDiagnosticLayerComboBox.SelectedIndex)
            {
                case 1:
                    return ObjectDetectionLineTextureDiagnosticLayer.RawDarkDensity;
                case 2:
                    return ObjectDetectionLineTextureDiagnosticLayer.DirectionScore;
                default:
                    return ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates;
            }
        }

        private ObjectDetectionLineTextureDiagnosticLayer GetCurrentObjectDetectionLineTextureDisplayLayer()
        {
            return isObjectDetectionResultReviewMode
                ? ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates
                : GetSelectedObjectDetectionLineTextureDiagnosticLayer();
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
                ObjectDetectionLineTextureDiagnosticLayer diagnosticLayer = calledFromResultReview
                    ? ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates
                    : GetSelectedObjectDetectionLineTextureDiagnosticLayer();
                ObjectDetectionLineTextureResult result = await Task.Run(delegate
                {
                    try
                    {
                        return ScanObjectDetectionLineTextureRegions(
                            sourceReference, objects, normalizedRegion, imageBounds, parameter,
                            diagnosticLayer, parameter.DefectParallelExecutionEnabled, progress);
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
            ObjectDetectionLineTextureDiagnosticLayer diagnosticLayer,
            bool runParallel,
            IProgress<string> progress)
        {
            var cellsByObject = new List<ObjectDetectionLineTextureCell>[objects.Count];
            var regionsByObject = new List<ObjectDetectionTextureAnomalyRegion>[objects.Count];
            var polygonsByObject = new PointF[objects.Count][];
            var patchesByObject = new ObjectDetectionDefectProcessedPatch[objects.Count];
            var diagnosticPatchesByObject = new ObjectDetectionLineTextureDiagnosticPatch[objects.Count];
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
                using (var localMean = new Cv.Mat())
                using (var darkResponse = new Cv.Mat())
                using (var erosionKernel = Cv.Cv2.GetStructuringElement(Cv.MorphShapes.Rect, new Cv.Size(3, 3)))
                using (var closeKernel = Cv.Cv2.GetStructuringElement(Cv.MorphShapes.Ellipse, new Cv.Size(3, 3)))
                using (var thresholdMap = new Cv.Mat())
                using (var closedMap = new Cv.Mat())
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
                    Cv.Cv2.GaussianBlur(gray, localMean, new Cv.Size(9, 9), 0);
                    Cv.Cv2.Subtract(localMean, gray, darkResponse);
                    Cv.Cv2.Threshold(darkResponse, darkResponse, 3, 255, Cv.ThresholdTypes.Binary);

                    int columns = (crop.Width + tileSize - 1) / tileSize;
                    int rows = (crop.Height + tileSize - 1) / tileSize;
                    if (columns < 3 || rows < 3) return;
                    int tileCount = rows * columns;
                    Interlocked.Add(ref scannedTiles, tileCount);
                    var tileDarkDensity = new float[tileCount];
                    var tileValidPixelCounts = new int[tileCount];
                    var validTiles = new bool[tileCount];
                    byte* response = (byte*)darkResponse.Data.ToPointer();
                    byte* mask = (byte*)textureMask.Data.ToPointer();
                    long responseStep = darkResponse.Step();
                    long maskStep = textureMask.Step();
                    var validIndices = new List<int>();
                    for (int tileY = 0; tileY < rows; tileY++)
                    {
                        int y0 = tileY * tileSize;
                        int y1 = Math.Min(crop.Height, y0 + tileSize);
                        for (int tileX = 0; tileX < columns; tileX++)
                        {
                            int x0 = tileX * tileSize;
                            int x1 = Math.Min(crop.Width, x0 + tileSize);
                            double densitySum = 0;
                            int count = 0;
                            int tileArea = (x1 - x0) * (y1 - y0);
                            for (int y = y0; y < y1; y++)
                            {
                                byte* maskRow = mask + ((long)y * maskStep);
                                byte* responseRow = response + ((long)y * responseStep);
                                for (int x = x0; x < x1; x++)
                                {
                                    if (maskRow[x] == 0) continue;
                                    if (responseRow[x] != 0) densitySum++;
                                    count++;
                                }
                            }
                            int index = tileY * columns + tileX;
                            if (count < Math.Max(4, tileArea * 0.35)) continue;
                            tileDarkDensity[index] = (float)(densitySum / count);
                            tileValidPixelCounts[index] = count;
                            validTiles[index] = true;
                            validIndices.Add(index);
                        }
                    }
                    if (diagnosticLayer == ObjectDetectionLineTextureDiagnosticLayer.RawDarkDensity)
                    {
                        diagnosticPatchesByObject[objectIndex] = new ObjectDetectionLineTextureDiagnosticPatch
                        {
                            Bounds = crop,
                            InspectionPolygon = corners.ToArray(),
                            Heatmap = CreateObjectDetectionLineTextureDiagnosticBitmap(
                                tileDarkDensity, null, validTiles, rows, columns)
                        };
                    }
                    if (validIndices.Count < 8) return;

                    List<ObjectDetectionLineTextureCandidate> candidates = FindObjectDetectionLineTextureCandidates(
                        tileDarkDensity, validTiles, rows, columns, tileSize,
                        parameter.DefectLineTextureSensitivity);
                    thresholdMap.Create(rows, columns, Cv.MatType.CV_8UC1);
                    thresholdMap.SetTo(Cv.Scalar.Black);
                    byte* anomalyData = (byte*)thresholdMap.Data.ToPointer();
                    long anomalyStep = thresholdMap.Step();
                    var scores = new double[tileCount];
                    double sensitivity = Math.Max(0.5, Math.Min(8.0, parameter.DefectLineTextureSensitivity));
                    double lineHalfWidth = Math.Max(1.0, 6.0 / tileSize);
                    double flankOffset = Math.Max(3.0, 24.0 / tileSize);
                    foreach (ObjectDetectionLineTextureCandidate candidate in candidates)
                    {
                        double cosine = Math.Cos(candidate.AngleRadians);
                        double sine = Math.Sin(candidate.AngleRadians);
                        double localThreshold = Math.Max(
                            0.005,
                            candidate.Contrast * Math.Max(0.25, Math.Min(0.65, 0.20 + sensitivity * 0.045)));
                        for (int index = 0; index < tileCount; index++)
                        {
                            if (!validTiles[index]) continue;
                            int tileX = index % columns;
                            int tileY = index / columns;
                            double rho = (tileX + 0.5) * cosine + (tileY + 0.5) * sine;
                            if (Math.Abs(rho - candidate.RhoTiles) > lineHalfWidth) continue;

                            double localContrast;
                            if (!TryCalculateObjectDetectionLineTextureLocalContrast(
                                tileDarkDensity, validTiles, columns, rows,
                                tileX, tileY, cosine, sine, flankOffset, out localContrast)) continue;
                            if (localContrast <= 0) continue;
                            double score = candidate.Score * localContrast / Math.Max(0.08, candidate.Contrast);
                            if (score <= scores[index]) continue;
                            scores[index] = score;
                            if (localContrast >= localThreshold)
                            {
                                byte* row = anomalyData + ((long)tileY * anomalyStep);
                                row[tileX] = 255;
                            }
                        }
                    }

                    if (diagnosticLayer == ObjectDetectionLineTextureDiagnosticLayer.DirectionScore)
                    {
                        diagnosticPatchesByObject[objectIndex] = new ObjectDetectionLineTextureDiagnosticPatch
                        {
                            Bounds = crop,
                            InspectionPolygon = corners.ToArray(),
                            Heatmap = CreateObjectDetectionLineTextureDiagnosticBitmap(
                                null, scores, validTiles, rows, columns)
                        };
                    }

                    Cv.Cv2.MorphologyEx(thresholdMap, closedMap, Cv.MorphTypes.Close, closeKernel);
                    using (var labels = new Cv.Mat())
                    using (var stats = new Cv.Mat())
                    using (var centroids = new Cv.Mat())
                    {
                        int componentCount = Cv.Cv2.ConnectedComponentsWithStats(
                            closedMap,
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
                                if (scores[index] >= Math.Max(1.0, sensitivity * 0.45))
                                {
                                    cells.Add(new ObjectDetectionLineTextureCell
                                    {
                                        ObjectNumber = detected.Number,
                                        Bounds = new Rectangle(crop.X + tileX * tileSize, crop.Y + tileY * tileSize,
                                            Math.Min(tileSize, crop.Width - tileX * tileSize),
                                            Math.Min(tileSize, crop.Height - tileY * tileSize)),
                                        Score = scores[index],
                                        IsAnomaly = true
                                    });
                                }
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
                    textureCalculation.Stop();
                    Interlocked.Add(ref textureTicks, textureCalculation.ElapsedTicks);
                }
                progress?.Report("紋理暗紋密度掃描 ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
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
                    foreach (ObjectDetectionLineTextureDiagnosticPatch patch in diagnosticPatchesByObject)
                    {
                        DisposeObjectDetectionLineTextureDiagnosticPatch(patch);
                    }
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
                    foreach (ObjectDetectionLineTextureDiagnosticPatch patch in diagnosticPatchesByObject)
                    {
                        DisposeObjectDetectionLineTextureDiagnosticPatch(patch);
                    }
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
                DiagnosticLayer = diagnosticLayer,
                DiagnosticPatchesByObject = new Dictionary<int, ObjectDetectionLineTextureDiagnosticPatch>(),
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
                if (diagnosticPatchesByObject[index] != null)
                {
                    result.DiagnosticPatchesByObject[number] = diagnosticPatchesByObject[index];
                }
                if (patchesByObject[index] != null)
                {
                    result.ProcessedPatches.Add(patchesByObject[index]);
                    patchesByObject[index] = null;
                }
            }
            return result;
        }

        private static List<ObjectDetectionLineTextureCandidate> FindObjectDetectionLineTextureCandidates(
            float[] density,
            bool[] validTiles,
            int rows,
            int columns,
            int tileSize,
            double sensitivity)
        {
            var candidates = new List<ObjectDetectionLineTextureCandidate>();
            const int rhoBinsPerTile = 2;
            const int centerRadiusBins = 1;
            const double angleStepDegrees = 0.5;
            int rhoOffset = (rows + columns + 8) * rhoBinsPerTile;
            int rhoBinCount = rhoOffset * 2 + 1;
            double candidateThreshold = 3.5 + Math.Max(0.5, Math.Min(8.0, sensitivity));
            for (int angleIndex = 0; angleIndex < 180.0 / angleStepDegrees; angleIndex++)
            {
                double angleDegrees = angleIndex * angleStepDegrees;
                double angleRadians = angleDegrees * Math.PI / 180.0;
                double cosine = Math.Cos(angleRadians);
                double sine = Math.Sin(angleRadians);
                var sums = new double[rhoBinCount];
                var counts = new double[rhoBinCount];
                for (int tileY = 0; tileY < rows; tileY++)
                {
                    for (int tileX = 0; tileX < columns; tileX++)
                    {
                        int tileIndex = tileY * columns + tileX;
                        if (!validTiles[tileIndex]) continue;
                        double rho = (tileX + 0.5) * cosine + (tileY + 0.5) * sine;
                        int bin = (int)Math.Round(rho * rhoBinsPerTile) + rhoOffset;
                        if (bin < 0 || bin >= rhoBinCount) continue;
                        sums[bin] += density[tileIndex];
                        counts[bin] += 1;
                    }
                }

                var contrasts = new List<double>();
                var validBins = new List<int>();
                var centerCounts = new double[rhoBinCount];
                var contrastByBin = new double[rhoBinCount];
                var countByBin = new double[rhoBinCount];
                int flankOffset = Math.Max(3,
                    (int)Math.Round((24.0 / tileSize) * rhoBinsPerTile));
                for (int bin = flankOffset + centerRadiusBins;
                    bin < rhoBinCount - flankOffset - centerRadiusBins; bin++)
                {
                    double centerSum = 0;
                    double centerCount = 0;
                    double leftSum = 0;
                    double leftCount = 0;
                    double rightSum = 0;
                    double rightCount = 0;
                    for (int offset = -centerRadiusBins; offset <= centerRadiusBins; offset++)
                    {
                        centerSum += sums[bin + offset];
                        centerCount += counts[bin + offset];
                        leftSum += sums[bin - flankOffset + offset];
                        leftCount += counts[bin - flankOffset + offset];
                        rightSum += sums[bin + flankOffset + offset];
                        rightCount += counts[bin + flankOffset + offset];
                    }
                    centerCount /= (centerRadiusBins * 2 + 1);
                    leftCount /= (centerRadiusBins * 2 + 1);
                    rightCount /= (centerRadiusBins * 2 + 1);
                    if (centerCount < 8 || leftCount < centerCount * 0.65 || rightCount < centerCount * 0.65) continue;
                    double centerMean = centerSum / Math.Max(1, centerCount * (centerRadiusBins * 2 + 1));
                    double leftMean = leftSum / Math.Max(1, leftCount * (centerRadiusBins * 2 + 1));
                    double rightMean = rightSum / Math.Max(1, rightCount * (centerRadiusBins * 2 + 1));
                    double contrast = centerMean - (leftMean + rightMean) * 0.5;
                    centerCounts[bin] = centerCount;
                    contrastByBin[bin] = contrast;
                    countByBin[bin] = centerCount;
                    contrasts.Add(contrast);
                    validBins.Add(bin);
                }
                if (validBins.Count < 8) continue;

                double median = CalculateObjectDetectionFrequencyMedian(contrasts);
                double mad = CalculateObjectDetectionFrequencyMedian(
                    contrasts.Select(value => Math.Abs(value - median)).ToList());
                double scale = Math.Max(0.002, mad * 1.4826);
                double maximumSupport = validBins.Max(bin => centerCounts[bin]);
                double minimumSupport = Math.Max(8, maximumSupport * 0.20);
                foreach (int bin in validBins)
                {
                    double support = countByBin[bin];
                    if (support < minimumSupport) continue;
                    double score = Math.Max(0, contrastByBin[bin] - median) / scale *
                        Math.Sqrt(Math.Min(1.0, support / Math.Max(1.0, maximumSupport)));
                    if (score < candidateThreshold) continue;
                    bool localMaximum = true;
                    for (int offset = -2; offset <= 2; offset++)
                    {
                        if (offset == 0) continue;
                        int neighborBin = bin + offset;
                        if (neighborBin >= 0 && neighborBin < rhoBinCount &&
                            contrastByBin[neighborBin] > contrastByBin[bin])
                        {
                            localMaximum = false;
                            break;
                        }
                    }
                    if (!localMaximum) continue;
                    candidates.Add(new ObjectDetectionLineTextureCandidate
                    {
                        AngleDegrees = angleDegrees,
                        AngleRadians = angleRadians,
                        RhoTiles = (bin - rhoOffset) / (double)rhoBinsPerTile,
                        Score = score,
                        Contrast = contrastByBin[bin]
                    });
                }
            }

            candidates.Sort((first, second) => second.Score.CompareTo(first.Score));
            var selected = new List<ObjectDetectionLineTextureCandidate>();
            foreach (ObjectDetectionLineTextureCandidate candidate in candidates)
            {
                bool duplicate = selected.Any(existing =>
                {
                    double difference = Math.Abs(candidate.AngleDegrees - existing.AngleDegrees);
                    difference = Math.Min(difference, 180.0 - difference);
                    return difference <= 2.0 && Math.Abs(candidate.RhoTiles - existing.RhoTiles) <= 2.5;
                });
                if (duplicate) continue;
                selected.Add(candidate);
                if (selected.Count >= 24) break;
            }
            return selected;
        }

        private static bool TryCalculateObjectDetectionLineTextureLocalContrast(
            float[] density,
            bool[] validTiles,
            int columns,
            int rows,
            int tileX,
            int tileY,
            double cosine,
            double sine,
            double flankOffset,
            out double contrast)
        {
            double centerSum = 0;
            double leftSum = 0;
            double rightSum = 0;
            int sampleCount = 0;
            double tangentX = -sine;
            double tangentY = cosine;
            for (int step = -2; step <= 2; step++)
            {
                double centerX = tileX + tangentX * step;
                double centerY = tileY + tangentY * step;
                float center;
                float left;
                float right;
                if (!TrySampleObjectDetectionLineTextureGrid(
                    density, validTiles, columns, rows, centerX, centerY, out center) ||
                    !TrySampleObjectDetectionLineTextureGrid(
                    density, validTiles, columns, rows,
                    centerX - cosine * flankOffset, centerY - sine * flankOffset, out left) ||
                    !TrySampleObjectDetectionLineTextureGrid(
                    density, validTiles, columns, rows,
                    centerX + cosine * flankOffset, centerY + sine * flankOffset, out right))
                {
                    continue;
                }
                centerSum += center;
                leftSum += left;
                rightSum += right;
                sampleCount++;
            }
            contrast = 0;
            if (sampleCount < 3) return false;
            contrast = centerSum / sampleCount - (leftSum + rightSum) / (2.0 * sampleCount);
            return true;
        }

        private static unsafe Bitmap CreateObjectDetectionLineTextureDiagnosticBitmap(
            float[] floatValues,
            double[] doubleValues,
            bool[] validTiles,
            int rows,
            int columns)
        {
            const int maximumOutputPixels = 1500000;
            int scale = 1;
            while (((long)(rows + scale - 1) / scale) * ((long)(columns + scale - 1) / scale) > maximumOutputPixels)
            {
                scale++;
            }

            int outputRows = (rows + scale - 1) / scale;
            int outputColumns = (columns + scale - 1) / scale;
            int outputCount = checked(outputRows * outputColumns);
            var outputValues = new float[outputCount];
            var outputValid = new bool[outputCount];
            float minimum = float.MaxValue;
            float maximum = float.MinValue;

            for (int outputY = 0; outputY < outputRows; outputY++)
            {
                int sourceY0 = outputY * scale;
                int sourceY1 = Math.Min(rows, sourceY0 + scale);
                for (int outputX = 0; outputX < outputColumns; outputX++)
                {
                    int sourceX0 = outputX * scale;
                    int sourceX1 = Math.Min(columns, sourceX0 + scale);
                    double sum = 0;
                    int count = 0;
                    for (int sourceY = sourceY0; sourceY < sourceY1; sourceY++)
                    {
                        for (int sourceX = sourceX0; sourceX < sourceX1; sourceX++)
                        {
                            int sourceIndex = sourceY * columns + sourceX;
                            if (!validTiles[sourceIndex]) continue;
                            sum += doubleValues == null ? floatValues[sourceIndex] : doubleValues[sourceIndex];
                            count++;
                        }
                    }

                    if (count == 0) continue;
                    float value = (float)(sum / count);
                    int outputIndex = outputY * outputColumns + outputX;
                    outputValues[outputIndex] = value;
                    outputValid[outputIndex] = true;
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }
            }

            var bitmap = new Bitmap(outputColumns, outputRows, PixelFormat.Format32bppArgb);
            BitmapData bitmapData = null;
            try
            {
                bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, outputColumns, outputRows),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);
                for (int y = 0; y < outputRows; y++)
                {
                    long rowOffset = bitmapData.Stride >= 0
                        ? (long)y * bitmapData.Stride
                        : (long)(outputRows - 1 - y) * Math.Abs(bitmapData.Stride);
                    byte* destination = (byte*)bitmapData.Scan0.ToPointer() + rowOffset;
                    for (int x = 0; x < outputColumns; x++)
                    {
                        int index = y * outputColumns + x;
                        byte* pixel = destination + (x * 4);
                        if (!outputValid[index])
                        {
                            pixel[0] = pixel[1] = pixel[2] = pixel[3] = 0;
                            continue;
                        }

                        double normalized = maximum > minimum
                            ? Math.Max(0, Math.Min(1, (outputValues[index] - minimum) / (maximum - minimum)))
                            : 0;
                        double blue = Math.Max(0, Math.Min(1, 1.5 - Math.Abs(4 * normalized - 1)));
                        double green = Math.Max(0, Math.Min(1, 1.5 - Math.Abs(4 * normalized - 2)));
                        double red = Math.Max(0, Math.Min(1, 1.5 - Math.Abs(4 * normalized - 3)));
                        pixel[0] = (byte)Math.Round(blue * 255);
                        pixel[1] = (byte)Math.Round(green * 255);
                        pixel[2] = (byte)Math.Round(red * 255);
                        pixel[3] = (byte)(48 + Math.Round(normalized * 132));
                    }
                }
                bitmap.UnlockBits(bitmapData);
                bitmapData = null;
                return bitmap;
            }
            catch
            {
                if (bitmapData != null)
                {
                    bitmap.UnlockBits(bitmapData);
                    bitmapData = null;
                }
                bitmap.Dispose();
                throw;
            }
        }

        private static bool TrySampleObjectDetectionLineTextureGrid(
            float[] values,
            bool[] validTiles,
            int columns,
            int rows,
            double x,
            double y,
            out float value)
        {
            value = 0;
            if (x < 0 || y < 0 || x > columns - 1 || y > rows - 1) return false;
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(columns - 1, x0 + 1);
            int y1 = Math.Min(rows - 1, y0 + 1);
            double fx = x - x0;
            double fy = y - y0;
            double weightedSum = 0;
            double validWeight = 0;
            AddObjectDetectionLineTextureSample(values, validTiles, columns, x0, y0,
                (1 - fx) * (1 - fy), ref weightedSum, ref validWeight);
            AddObjectDetectionLineTextureSample(values, validTiles, columns, x1, y0,
                fx * (1 - fy), ref weightedSum, ref validWeight);
            AddObjectDetectionLineTextureSample(values, validTiles, columns, x0, y1,
                (1 - fx) * fy, ref weightedSum, ref validWeight);
            AddObjectDetectionLineTextureSample(values, validTiles, columns, x1, y1,
                fx * fy, ref weightedSum, ref validWeight);
            if (validWeight < 0.65) return false;
            value = (float)(weightedSum / validWeight);
            return true;
        }

        private static void AddObjectDetectionLineTextureSample(
            float[] values,
            bool[] validTiles,
            int columns,
            int x,
            int y,
            double weight,
            ref double weightedSum,
            ref double validWeight)
        {
            if (weight <= 0) return;
            int index = y * columns + x;
            if (!validTiles[index]) return;
            weightedSum += values[index] * weight;
            validWeight += weight;
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
                            if (result.DiagnosticLayer != ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates &&
                                result.DiagnosticLayer == GetCurrentObjectDetectionLineTextureDisplayLayer())
                            {
                                ObjectDetectionLineTextureDiagnosticPatch diagnosticPatch;
                                if (result.DiagnosticPatchesByObject.TryGetValue(entry.Key, out diagnosticPatch) &&
                                    diagnosticPatch != null && diagnosticPatch.Heatmap != null &&
                                    diagnosticPatch.Bounds.IntersectsWith(visible))
                                {
                                    GraphicsState heatmapState = graphics.Save();
                                    try
                                    {
                                        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                                        graphics.PixelOffsetMode = PixelOffsetMode.Half;
                                        graphics.DrawImage(diagnosticPatch.Heatmap, new RectangleF(
                                            offset.X + diagnosticPatch.Bounds.X * zoom,
                                            offset.Y + diagnosticPatch.Bounds.Y * zoom,
                                            diagnosticPatch.Bounds.Width * zoom,
                                            diagnosticPatch.Bounds.Height * zoom));
                                    }
                                    finally { graphics.Restore(heatmapState); }
                                }
                            }
                            else if (result.DiagnosticLayer == ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates &&
                                GetCurrentObjectDetectionLineTextureDisplayLayer() == ObjectDetectionLineTextureDiagnosticLayer.FinalCandidates)
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
            if (result == null) return;
            if (result.ProcessedPatches != null)
            {
                foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
                {
                    DisposeObjectDetectionDefectProcessedPatch(patch);
                }
                result.ProcessedPatches.Clear();
            }
            if (result.DiagnosticPatchesByObject != null)
            {
                foreach (ObjectDetectionLineTextureDiagnosticPatch patch in result.DiagnosticPatchesByObject.Values)
                {
                    DisposeObjectDetectionLineTextureDiagnosticPatch(patch);
                }
                result.DiagnosticPatchesByObject.Clear();
            }
        }

        private static void DisposeObjectDetectionLineTextureDiagnosticPatch(
            ObjectDetectionLineTextureDiagnosticPatch patch)
        {
            if (patch == null || patch.Heatmap == null) return;
            patch.Heatmap.Dispose();
            patch.Heatmap = null;
        }
    }
}
