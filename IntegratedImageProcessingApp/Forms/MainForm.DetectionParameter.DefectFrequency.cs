using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ObjectDetectionFrequencyCell
        {
            public int ObjectNumber { get; set; }

            public Rectangle Bounds { get; set; }

            public double Energy { get; set; }

            public double Score { get; set; }

            public bool IsAnomaly { get; set; }
        }

        private sealed class ObjectDetectionFrequencyResult
        {
            public string Signature { get; set; }

            public int ScanHeight { get; set; }

            public double Sensitivity { get; set; }

            public double MedianEnergy { get; set; }

            public double EnergyScale { get; set; }

            public long ElapsedMilliseconds { get; set; }

            public List<ObjectDetectionFrequencyCell> Cells { get; set; }
        }

        private readonly Dictionary<string, ObjectDetectionFrequencyResult>
            objectDetectionFrequencyResults =
                new Dictionary<string, ObjectDetectionFrequencyResult>(StringComparer.Ordinal);

        private NumericUpDown objectDetectionFrequencyScanHeightInput;
        private NumericUpDown objectDetectionFrequencySensitivityInput;
        private CheckBox objectDetectionFrequencyEnabledCheckBox;
        private CheckBox objectDetectionFrequencyShowHeatmapCheckBox;
        private CheckBox objectDetectionFrequencyShowBoxesCheckBox;
        private Label objectDetectionFrequencyWindowHint;
        private Label objectDetectionFrequencyStatusLabel;
        private Button objectDetectionFrequencyRunButton;
        private bool objectDetectionFrequencyAnalysisRunning;
        private string objectDetectionFrequencyDraftParameterId;

        private TabPage BuildObjectDetectionDefectFrequencyTab(
            ObjectDetectionParameterSettings parameter)
        {
            var page = new TabPage("頻域異常");
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 251, 253)
            };
            objectDetectionFrequencyDraftParameterId = parameter.Id;

            var sourceInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 7, 8, 2),
                Text = "分析來源：平場校正後灰階影像\r\n掃描範圍沿用已套用的物件 ROI 與缺陷檢測範圍。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(55, 63, 76)
            };
            var enabledPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30
            };
            objectDetectionFrequencyEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "是否使用",
                Checked = parameter.DefectFrequencyEnabled,
                Location = new Point(8, 5)
            };
            objectDetectionFrequencyEnabledCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyEnabled(
                    parameter,
                    objectDetectionFrequencyEnabledCheckBox.Checked);
            };
            enabledPanel.Controls.Add(objectDetectionFrequencyEnabledCheckBox);

            var scanGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 144,
                Text = "掃描設定",
                Padding = new Padding(8, 16, 8, 4)
            };
            var scanLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            scanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            scanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            for (int row = 0; row < 4; row++)
            {
                scanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            }

            objectDetectionFrequencyScanHeightInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 8,
                Maximum = 1000,
                Increment = 1,
                Value = Math.Max(8, Math.Min(1000, parameter.DefectFrequencyScanHeight)),
                ThousandsSeparator = true
            };
            objectDetectionFrequencyWindowHint = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            objectDetectionFrequencySensitivityInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1.0m,
                Maximum = 10.0m,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Value = (decimal)Math.Max(1.0, Math.Min(10.0, parameter.DefectFrequencySensitivity))
            };
            var baselineInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "基準由有效區塊自動估算；暖色偏高、藍色偏低，外框為異常。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 83, 95)
            };

            scanLayout.Controls.Add(CreateDefectCoreLabel("掃描高度 (px)"), 0, 0);
            scanLayout.Controls.Add(objectDetectionFrequencyScanHeightInput, 1, 0);
            scanLayout.Controls.Add(CreateDefectCoreLabel("自動視窗與間距"), 0, 1);
            scanLayout.Controls.Add(objectDetectionFrequencyWindowHint, 1, 1);
            scanLayout.Controls.Add(CreateDefectCoreLabel("異常敏感度"), 0, 2);
            scanLayout.Controls.Add(objectDetectionFrequencySensitivityInput, 1, 2);
            scanLayout.Controls.Add(baselineInfo, 0, 3);
            scanLayout.SetColumnSpan(baselineInfo, 2);
            scanGroup.Controls.Add(scanLayout);

            var displayOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(5, 6, 0, 0),
                Margin = Padding.Empty
            };
            objectDetectionFrequencyShowHeatmapCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示能量熱圖",
                Checked = parameter.DefectFrequencyShowHeatmap,
                Margin = new Padding(2, 1, 14, 1)
            };
            objectDetectionFrequencyShowBoxesCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示異常框",
                Checked = parameter.DefectFrequencyShowAnomalyBoxes,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionFrequencyShowHeatmapCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyDisplayOptions(parameter);
            };
            objectDetectionFrequencyShowBoxesCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyDisplayOptions(parameter);
            };
            displayOptions.Controls.Add(objectDetectionFrequencyShowHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionFrequencyShowBoxesCheckBox);

            objectDetectionFrequencyStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 4, 8, 2),
                Text = parameter.DefectFrequencyEnabled
                    ? "尚未執行頻域掃描。"
                    : "頻域分析已停用。",
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
            var cancelButton = new Button
            {
                Text = "取消",
                Width = 76,
                Height = 27,
                Margin = new Padding(3, 0, 0, 0)
            };
            var applyButton = new Button
            {
                Text = "套用",
                Width = 76,
                Height = 27,
                Margin = new Padding(3, 0, 0, 0)
            };
            objectDetectionFrequencyRunButton = new Button
            {
                Text = "開始分析",
                Width = 96,
                Height = 27,
                Margin = new Padding(3, 0, 0, 0)
            };
            cancelButton.Click += delegate { CancelObjectDetectionFrequencySettings(parameter); };
            applyButton.Click += delegate { ApplyObjectDetectionFrequencySettings(parameter); };
            objectDetectionFrequencyRunButton.Click += async delegate
            {
                ApplyObjectDetectionFrequencySettings(parameter);
                await RunObjectDetectionFrequencyAnalysisAsync(parameter.Id);
            };
            actionBar.Controls.Add(cancelButton);
            actionBar.Controls.Add(applyButton);
            actionBar.Controls.Add(objectDetectionFrequencyRunButton);

            objectDetectionFrequencyScanHeightInput.ValueChanged += delegate
            {
                UpdateObjectDetectionFrequencyWindowHint();
            };
            content.Controls.Add(actionBar);
            content.Controls.Add(objectDetectionFrequencyStatusLabel);
            content.Controls.Add(displayOptions);
            content.Controls.Add(scanGroup);
            content.Controls.Add(sourceInfo);
            content.Controls.Add(enabledPanel);
            page.Controls.Add(content);
            UpdateObjectDetectionFrequencyWindowHint();
            UpdateObjectDetectionFrequencyControlsEnabled(parameter.DefectFrequencyEnabled);
            return page;
        }

        private void UpdateObjectDetectionFrequencyEnabled(
            ObjectDetectionParameterSettings parameter,
            bool enabled)
        {
            if (parameter == null ||
                !string.Equals(objectDetectionFrequencyDraftParameterId, parameter.Id,
                    StringComparison.Ordinal))
            {
                return;
            }

            parameter.DefectFrequencyEnabled = enabled;
            SaveSystemParameters();
            UpdateObjectDetectionFrequencyControlsEnabled(enabled);
            if (!enabled)
            {
                objectDetectionFrequencyResults.Remove(parameter.Id);
                if (objectDetectionFrequencyStatusLabel != null)
                {
                    objectDetectionFrequencyStatusLabel.Text = "頻域分析已停用。";
                }
            }
            else if (objectDetectionFrequencyStatusLabel != null)
            {
                objectDetectionFrequencyStatusLabel.Text = "頻域分析已啟用，按「開始分析」更新結果。";
            }

            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectFrequencyDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
            SetObjectDetectionDefectRegionStatus(
                enabled
                    ? "頻域異常分析已啟用。"
                    : "頻域異常分析已停用，頻域標記已清除。");
        }

        private void UpdateObjectDetectionFrequencyControlsEnabled(bool enabled)
        {
            if (objectDetectionFrequencyScanHeightInput != null)
            {
                objectDetectionFrequencyScanHeightInput.Enabled = enabled;
            }
            if (objectDetectionFrequencySensitivityInput != null)
            {
                objectDetectionFrequencySensitivityInput.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowHeatmapCheckBox != null)
            {
                objectDetectionFrequencyShowHeatmapCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowBoxesCheckBox != null)
            {
                objectDetectionFrequencyShowBoxesCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyRunButton != null)
            {
                objectDetectionFrequencyRunButton.Enabled = enabled &&
                    !objectDetectionFrequencyAnalysisRunning;
            }
        }

        private void UpdateObjectDetectionFrequencyWindowHint()
        {
            if (objectDetectionFrequencyWindowHint == null ||
                objectDetectionFrequencyScanHeightInput == null)
            {
                return;
            }
            int size = (int)objectDetectionFrequencyScanHeightInput.Value;
            objectDetectionFrequencyWindowHint.Text =
                size.ToString(CultureInfo.CurrentCulture) + " × " +
                size.ToString(CultureInfo.CurrentCulture) + " px；間距 " +
                Math.Max(1, size / 2).ToString(CultureInfo.CurrentCulture) + " px";
        }

        private void ApplyObjectDetectionFrequencySettings(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                !string.Equals(objectDetectionFrequencyDraftParameterId, parameter.Id,
                    StringComparison.Ordinal) ||
                objectDetectionFrequencyScanHeightInput == null ||
                objectDetectionFrequencySensitivityInput == null)
            {
                return;
            }

            parameter.DefectFrequencyScanHeight = (int)objectDetectionFrequencyScanHeightInput.Value;
            parameter.DefectFrequencySensitivity = Decimal.ToDouble(
                objectDetectionFrequencySensitivityInput.Value);
            parameter.DefectFrequencyShowHeatmap =
                objectDetectionFrequencyShowHeatmapCheckBox.Checked;
            parameter.DefectFrequencyShowAnomalyBoxes =
                objectDetectionFrequencyShowBoxesCheckBox.Checked;
            SaveSystemParameters();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectFrequencyDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
            if (objectDetectionFrequencyStatusLabel != null)
            {
                objectDetectionFrequencyStatusLabel.Text = "設定已套用，請重新分析以更新頻域結果。";
            }
            SetObjectDetectionDefectRegionStatus(
                "頻域異常設定已套用；掃描高度 " +
                parameter.DefectFrequencyScanHeight.ToString(CultureInfo.CurrentCulture) +
                " px，敏感度 " +
                parameter.DefectFrequencySensitivity.ToString("0.0", CultureInfo.CurrentCulture) +
                "。設定變更後請重新分析。 ");
        }

        private void CancelObjectDetectionFrequencySettings(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                !string.Equals(objectDetectionFrequencyDraftParameterId, parameter.Id,
                    StringComparison.Ordinal))
            {
                return;
            }
            objectDetectionFrequencyScanHeightInput.Value = Math.Max(
                objectDetectionFrequencyScanHeightInput.Minimum,
                Math.Min(objectDetectionFrequencyScanHeightInput.Maximum,
                    parameter.DefectFrequencyScanHeight));
            objectDetectionFrequencySensitivityInput.Value = (decimal)Math.Max(
                (double)objectDetectionFrequencySensitivityInput.Minimum,
                Math.Min((double)objectDetectionFrequencySensitivityInput.Maximum,
                    parameter.DefectFrequencySensitivity));
            objectDetectionFrequencyShowHeatmapCheckBox.Checked =
                parameter.DefectFrequencyShowHeatmap;
            objectDetectionFrequencyShowBoxesCheckBox.Checked =
                parameter.DefectFrequencyShowAnomalyBoxes;
            SetObjectDetectionDefectRegionStatus("已取消頻域異常設定變更。 ");
        }

        private void UpdateObjectDetectionFrequencyDisplayOptions(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionFrequencyShowHeatmapCheckBox == null ||
                objectDetectionFrequencyShowBoxesCheckBox == null)
            {
                return;
            }
            parameter.DefectFrequencyShowHeatmap =
                objectDetectionFrequencyShowHeatmapCheckBox.Checked;
            parameter.DefectFrequencyShowAnomalyBoxes =
                objectDetectionFrequencyShowBoxesCheckBox.Checked;
            SaveSystemParameters();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectFrequencyDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
        }

        private async Task RunObjectDetectionFrequencyAnalysisAsync(string parameterId)
        {
            if (objectDetectionFrequencyAnalysisRunning)
            {
                return;
            }
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                SetObjectDetectionDefectRegionStatus("找不到頻域分析所屬的檢測參數。 ");
                return;
            }
            if (!parameter.DefectFrequencyEnabled)
            {
                SetObjectDetectionDefectRegionStatus("頻域異常分析目前已停用。 ");
                return;
            }
            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                SetObjectDetectionDefectRegionStatus("請先框選並套用缺陷檢測範圍，再執行頻域分析。 ");
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
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

            objectDetectionFrequencyAnalysisRunning = true;
            if (objectDetectionFrequencyRunButton != null)
            {
                objectDetectionFrequencyRunButton.Enabled = false;
            }
            if (objectDetectionDefectCoreTabs != null)
            {
                objectDetectionDefectCoreTabs.Enabled = false;
            }
            if (leftImageTabControl != null)
            {
                leftImageTabControl.SelectedTab = GetObjectDetectionDefectDisplayTabPage(
                    ObjectDetectionDefectFrequencyDisplayIndex);
                RefreshObjectDetectionDefectDisplay();
            }

            try
            {
                SetObjectDetectionDefectRegionStatus("正在準備平場校正影像與 ROI... ");
                await Task.Yield();
                LargeImageSource source = await PrepareObjectDetectionFrequencySourceAsync(parameter);
                if (source == null)
                {
                    return;
                }

                List<ObjectDefinitionDetectedObject> objects =
                    SnapshotCompletedObjectDefinitionObjects(definition, definitionSignature);
                if (objects.Count == 0)
                {
                    SetObjectDetectionDefectRegionStatus("沒有已完成定義的 ROI 物件可供頻域掃描。 ");
                    return;
                }

                Rectangle imageBounds = new Rectangle(0, 0, source.Width, source.Height);
                RectangleF normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit((float)parameter.DefectInspectionRegionLeft),
                    ClampUnit((float)parameter.DefectInspectionRegionTop),
                    ClampUnit((float)parameter.DefectInspectionRegionRight),
                    ClampUnit((float)parameter.DefectInspectionRegionBottom));
                int scanHeight = Math.Max(8, Math.Min(1000, parameter.DefectFrequencyScanHeight));
                double sensitivity = Math.Max(1.0, Math.Min(10.0, parameter.DefectFrequencySensitivity));
                int capturedImageGeneration = imageSourceGeneration;
                int capturedFlatFieldGeneration = objectDetectionFlatFieldEvaluationGeneration;
                string signature = CreateObjectDetectionFrequencySignature(
                    parameter,
                    definitionSignature,
                    capturedImageGeneration,
                    capturedFlatFieldGeneration);
                LargeImageSource sourceReference = source.AddReference();
                var progress = new Progress<string>(message =>
                {
                    if (!IsDisposed && string.Equals(activeObjectDetectionParameterId,
                        parameter.Id, StringComparison.Ordinal))
                    {
                        SetObjectDetectionDefectRegionStatus(message);
                    }
                });

                Stopwatch stopwatch = Stopwatch.StartNew();
                ObjectDetectionFrequencyResult result = await Task.Run(delegate
                {
                    try
                    {
                        return ScanObjectDetectionFrequencyRegions(
                            sourceReference,
                            objects,
                            normalizedRegion,
                            imageBounds,
                            scanHeight,
                            sensitivity,
                            progress);
                    }
                    finally
                    {
                        sourceReference.ReleaseReference();
                    }
                });
                stopwatch.Stop();
                result.Signature = signature;
                result.ScanHeight = scanHeight;
                result.Sensitivity = sensitivity;
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                if (IsDisposed || capturedImageGeneration != imageSourceGeneration ||
                    capturedFlatFieldGeneration != objectDetectionFlatFieldEvaluationGeneration ||
                    !string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal) ||
                    !string.Equals(CreateObjectDefinitionProcessingSignature(definition),
                        definitionSignature, StringComparison.Ordinal) ||
                    !string.Equals(CreateObjectDetectionFrequencySignature(
                        parameter,
                        definitionSignature,
                        capturedImageGeneration,
                        capturedFlatFieldGeneration), signature, StringComparison.Ordinal))
                {
                    SetObjectDetectionDefectRegionStatus("掃描期間影像或設定已變更，結果未套用；請重新分析。 ");
                    return;
                }

                objectDetectionFrequencyResults[parameter.Id] = result;
                ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                    ObjectDetectionDefectFrequencyDisplayIndex);
                if (display != null)
                {
                    display.InvalidateImageView();
                }
                int anomalyCount = result.Cells.Count(cell => cell.IsAnomaly);
                string summary = "頻域掃描完成：" +
                    result.Cells.Count.ToString("N0", CultureInfo.CurrentCulture) + " 個區塊；可疑 " +
                    anomalyCount.ToString("N0", CultureInfo.CurrentCulture) + " 個；耗時 " +
                    result.ElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms。";
                if (objectDetectionFrequencyStatusLabel != null)
                {
                    objectDetectionFrequencyStatusLabel.Text = summary;
                }
                if (statusLabel != null)
                {
                    statusLabel.Text = parameter.DisplayName + "：" + summary;
                }
                SetObjectDetectionDefectRegionStatus(summary + "結果只作為頻域異常提示，不會納入缺陷整合判定。 ");
            }
            catch (OutOfMemoryException)
            {
                SetObjectDetectionDefectRegionStatus("頻域掃描記憶體不足；請提高掃描高度以減少掃描區塊。 ");
            }
            catch (Exception exception)
            {
                SetObjectDetectionDefectRegionStatus("頻域掃描失敗：" + exception.Message);
            }
            finally
            {
                objectDetectionFrequencyAnalysisRunning = false;
                if (!IsDisposed)
                {
                    if (objectDetectionFrequencyRunButton != null)
                    {
                        objectDetectionFrequencyRunButton.Enabled =
                            parameter != null && parameter.DefectFrequencyEnabled;
                    }
                    if (objectDetectionDefectCoreTabs != null &&
                        !objectDetectionDefectCoreTabs.IsDisposed)
                    {
                        objectDetectionDefectCoreTabs.Enabled = true;
                    }
                }
            }
        }

        private async Task<LargeImageSource> PrepareObjectDetectionFrequencySourceAsync(
            ObjectDetectionParameterSettings parameter)
        {
            if (IsCurrentObjectDetectionFlatFieldImage(parameter) &&
                objectDetectionFlatFieldCorrectedLargeSource != null)
            {
                return objectDetectionFlatFieldCorrectedLargeSource;
            }

            bool hasCurrentProfile = objectDetectionFlatFieldSmoothedProfile != null &&
                string.Equals(objectDetectionFlatFieldProfileParameterId, parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionFlatFieldProfileImageGeneration == imageSourceGeneration &&
                objectDetectionFlatFieldProfileMaskGeneration == objectDetectionFlatFieldEvaluationGeneration;
            bool hasSavedProfile = !string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) &&
                string.Equals(parameter.FlatFieldSavedSettingsSignature,
                    CreateObjectDetectionFlatFieldSettingsSignature(parameter), StringComparison.Ordinal);
            if (!hasCurrentProfile && !hasSavedProfile)
            {
                SetObjectDetectionDefectRegionStatus("缺少有效的平場校正值，頻域分析尚未開始。 ");
                return null;
            }
            if (objectDetectionFlatFieldResultLabel == null)
            {
                SetObjectDetectionDefectRegionStatus("平場校正結果尚未準備完成。 ");
                return null;
            }

            if (objectDetectionDefectBasePreparationPending)
            {
                int waitCount = 0;
                while (objectDetectionDefectBasePreparationPending && waitCount++ < 1800)
                {
                    await Task.Delay(100);
                }
            }
            else
            {
                await ShowSavedObjectDetectionFlatFieldCalibration(
                    parameter,
                    objectDetectionFlatFieldResultLabel,
                    null,
                    hasCurrentProfile);
            }

            LargeImageSource source = objectDetectionFlatFieldCorrectedLargeSource;
            if (source == null && IsCurrentObjectDetectionFlatFieldImage(parameter))
            {
                source = TryCreateSmallObjectDetectionDefectSource();
                if (source != null)
                {
                    objectDetectionFlatFieldCorrectedLargeSource = source;
                }
            }
            if (!IsCurrentObjectDetectionFlatFieldImage(parameter) || source == null)
            {
                SetObjectDetectionDefectRegionStatus("無法取得目前影像的平場校正來源。 ");
                return null;
            }
            return source;
        }

        private static ObjectDetectionFrequencyResult ScanObjectDetectionFrequencyRegions(
            LargeImageSource source,
            IList<ObjectDefinitionDetectedObject> objects,
            RectangleF normalizedRegion,
            Rectangle imageBounds,
            int requestedWindowSize,
            double sensitivity,
            IProgress<string> progress)
        {
            var result = new ObjectDetectionFrequencyResult
            {
                Cells = new List<ObjectDetectionFrequencyCell>()
            };
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                ObjectDefinitionDetectedObject detectedObject = objects[objectIndex];
                PointF[] corners = CreateObjectDetectionDefectRegionImageCorners(
                    detectedObject,
                    normalizedRegion);
                int left = (int)Math.Floor(corners.Min(point => point.X));
                int top = (int)Math.Floor(corners.Min(point => point.Y));
                int right = (int)Math.Ceiling(corners.Max(point => point.X));
                int bottom = (int)Math.Ceiling(corners.Max(point => point.Y));
                Rectangle crop = Rectangle.Intersect(
                    Rectangle.Intersect(
                        Rectangle.FromLTRB(left, top, right, bottom),
                        detectedObject.Bounds),
                    imageBounds);
                if (crop.Width < 8 || crop.Height < 8)
                {
                    continue;
                }

                using (Cv.Mat gray = CreateObjectDetectionDefectGrayRegionMat(source, crop))
                using (var polygonMask = new Cv.Mat(
                    crop.Height,
                    crop.Width,
                    Cv.MatType.CV_8UC1,
                    Cv.Scalar.Black))
                {
                    Cv.Point[] polygon = corners.Select(point => new Cv.Point(
                        (int)Math.Round(point.X - crop.X),
                        (int)Math.Round(point.Y - crop.Y))).ToArray();
                    Cv.Cv2.FillPoly(polygonMask, new[] { polygon }, Cv.Scalar.White);
                    int windowSize = Math.Min(
                        requestedWindowSize,
                        Math.Min(crop.Width, crop.Height));
                    if (windowSize < 8)
                    {
                        continue;
                    }
                    int step = Math.Max(1, windowSize / 2);
                    List<int> xStarts = CreateFrequencyScanStarts(crop.Width, windowSize, step);
                    List<int> yStarts = CreateFrequencyScanStarts(crop.Height, windowSize, step);
                    foreach (int y in yStarts)
                    {
                        foreach (int x in xStarts)
                        {
                            var localWindow = new Rectangle(x, y, windowSize, windowSize);
                            using (Cv.Mat windowMask = polygonMask.SubMat(
                                new Cv.Rect(localWindow.X, localWindow.Y,
                                    localWindow.Width, localWindow.Height)))
                            {
                                if (Cv.Cv2.CountNonZero(windowMask) != windowSize * windowSize)
                                {
                                    continue;
                                }
                            }

                            using (Cv.Mat tile = gray.SubMat(new Cv.Rect(
                                localWindow.X,
                                localWindow.Y,
                                localWindow.Width,
                                localWindow.Height)))
                            {
                                result.Cells.Add(new ObjectDetectionFrequencyCell
                                {
                                    ObjectNumber = detectedObject.Number,
                                    Bounds = new Rectangle(
                                        crop.X + localWindow.X,
                                        crop.Y + localWindow.Y,
                                        localWindow.Width,
                                        localWindow.Height),
                                    Energy = CalculateObjectDetectionFrequencyEnergy(tile)
                                });
                            }
                        }
                    }
                }
                progress?.Report(
                    "頻域掃描 ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
                    objects.Count.ToString(CultureInfo.CurrentCulture) + "... ");
            }

            if (result.Cells.Count == 0)
            {
                return result;
            }

            double median = CalculateObjectDetectionFrequencyMedian(
                result.Cells.Select(cell => cell.Energy).ToList());
            double medianAbsoluteDeviation = CalculateObjectDetectionFrequencyMedian(
                result.Cells.Select(cell => Math.Abs(cell.Energy - median)).ToList());
            double scale = Math.Max(
                0.25,
                Math.Max(medianAbsoluteDeviation * 1.4826, Math.Abs(median) * 0.05));
            foreach (ObjectDetectionFrequencyCell cell in result.Cells)
            {
                cell.Score = Math.Abs(cell.Energy - median) / scale;
                cell.IsAnomaly = cell.Score >= sensitivity;
            }
            result.MedianEnergy = median;
            result.EnergyScale = scale;
            return result;
        }

        private static List<int> CreateFrequencyScanStarts(int extent, int windowSize, int step)
        {
            var starts = new List<int>();
            if (extent < windowSize)
            {
                return starts;
            }
            for (int start = 0; start <= extent - windowSize; start += step)
            {
                starts.Add(start);
            }
            int last = extent - windowSize;
            if (starts.Count == 0 || starts[starts.Count - 1] != last)
            {
                starts.Add(last);
            }
            return starts;
        }

        private static double CalculateObjectDetectionFrequencyEnergy(Cv.Mat tile)
        {
            if (tile == null || tile.Empty() || tile.Type() != Cv.MatType.CV_8UC1)
            {
                throw new ArgumentException("頻域分析需要有效的 8-bit 灰階區塊。", "tile");
            }

            using (var floatTile = new Cv.Mat())
            using (var centered = new Cv.Mat())
            using (var hann = new Cv.Mat())
            using (var windowed = new Cv.Mat())
            using (var spectrum = new Cv.Mat())
            {
                double mean = Cv.Cv2.Mean(tile).Val0;
                tile.ConvertTo(floatTile, Cv.MatType.CV_32FC1);
                Cv.Cv2.Subtract(floatTile, Cv.Scalar.All(mean), centered);
                Cv.Cv2.CreateHanningWindow(
                    hann,
                    new Cv.Size(tile.Width, tile.Height),
                    Cv.MatType.CV_32FC1);
                Cv.Cv2.Multiply(centered, hann, windowed);
                Cv.Cv2.Dft(windowed, spectrum, Cv.DftFlags.ComplexOutput);
                Cv.Mat[] planes = Cv.Cv2.Split(spectrum);
                try
                {
                    using (var realPower = new Cv.Mat())
                    using (var imaginaryPower = new Cv.Mat())
                    using (var power = new Cv.Mat())
                    {
                        Cv.Cv2.Multiply(planes[0], planes[0], realPower);
                        Cv.Cv2.Multiply(planes[1], planes[1], imaginaryPower);
                        Cv.Cv2.Add(realPower, imaginaryPower, power);
                        double pixelCount = (double)tile.Width * tile.Height;
                        return Cv.Cv2.Sum(power).Val0 / (pixelCount * pixelCount);
                    }
                }
                finally
                {
                    foreach (Cv.Mat plane in planes)
                    {
                        plane.Dispose();
                    }
                }
            }
        }

        private static double CalculateObjectDetectionFrequencyMedian(List<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }
            values.Sort();
            int middle = values.Count / 2;
            return values.Count % 2 == 0
                ? (values[middle - 1] + values[middle]) * 0.5
                : values[middle];
        }

        private string CreateObjectDetectionFrequencySignature(
            ObjectDetectionParameterSettings parameter,
            string definitionSignature,
            int imageGeneration,
            int flatFieldGeneration)
        {
            return string.Join("|", new[]
            {
                parameter.Id ?? string.Empty,
                parameter.DefectFrequencyEnabled ? "1" : "0",
                definitionSignature ?? string.Empty,
                imageGeneration.ToString(CultureInfo.InvariantCulture),
                flatFieldGeneration.ToString(CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionId ?? string.Empty,
                parameter.DefectInspectionRegionLeft.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionTop.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionRight.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionBottom.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectFrequencyScanHeight.ToString(CultureInfo.InvariantCulture),
                parameter.DefectFrequencySensitivity.ToString("R", CultureInfo.InvariantCulture),
                parameter.FlatFieldSavedSettingsSignature ?? string.Empty,
                CreateObjectDetectionFlatFieldSettingsSignature(parameter)
            });
        }

        private bool TryGetCurrentObjectDetectionFrequencyResult(
            ObjectDetectionParameterSettings parameter,
            out ObjectDetectionFrequencyResult result)
        {
            result = null;
            if (parameter == null || !parameter.DefectFrequencyEnabled ||
                !objectDetectionFrequencyResults.TryGetValue(parameter.Id, out result))
            {
                return false;
            }
            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                result = null;
                return false;
            }
            string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
            string expectedSignature = CreateObjectDetectionFrequencySignature(
                parameter,
                definitionSignature,
                imageSourceGeneration,
                objectDetectionFlatFieldEvaluationGeneration);
            if (!string.Equals(result.Signature, expectedSignature, StringComparison.Ordinal))
            {
                result = null;
                return false;
            }
            return true;
        }

        private void DrawObjectDetectionFrequencyAnomalies(
            Graphics graphics,
            float zoom,
            PointF offset,
            Rectangle visibleSourceRect)
        {
            if (graphics == null || zoom <= 0 || !isObjectDetectionParameterImageLayout)
            {
                return;
            }
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDetectionFrequencyResult result;
            if (!TryGetCurrentObjectDetectionFrequencyResult(parameter, out result) ||
                result.Cells == null || result.Cells.Count == 0)
            {
                return;
            }

            RectangleF visibleBounds = visibleSourceRect;
            bool showHeatmap = parameter.DefectFrequencyShowHeatmap;
            bool showBoxes = parameter.DefectFrequencyShowAnomalyBoxes;
            int[] heatmapAlpha = { 16, 24, 32, 40, 48 };
            var highEnergyBrushes = new SolidBrush[heatmapAlpha.Length];
            var lowEnergyBrushes = new SolidBrush[heatmapAlpha.Length];
            for (int index = 0; index < heatmapAlpha.Length; index++)
            {
                highEnergyBrushes[index] = new SolidBrush(
                    Color.FromArgb(heatmapAlpha[index], 255, 190, 0));
                lowEnergyBrushes[index] = new SolidBrush(
                    Color.FromArgb(heatmapAlpha[index], 50, 145, 255));
            }
            using (var anomalyOutline = new Pen(Color.OrangeRed, Math.Max(1.0f, Math.Min(3.0f, zoom * 1.5f))))
            {
                try
                {
                    foreach (ObjectDetectionFrequencyCell cell in result.Cells)
                    {
                        if (!cell.Bounds.IntersectsWith(Rectangle.Ceiling(visibleBounds)))
                        {
                            continue;
                        }
                        var destination = new RectangleF(
                            offset.X + cell.Bounds.X * zoom,
                            offset.Y + cell.Bounds.Y * zoom,
                            Math.Max(1f, cell.Bounds.Width * zoom),
                            Math.Max(1f, cell.Bounds.Height * zoom));
                        if (showHeatmap)
                        {
                            int level = (int)Math.Floor(
                                cell.Score / Math.Max(1.0, result.Sensitivity) * heatmapAlpha.Length);
                            level = Math.Max(0, Math.Min(heatmapAlpha.Length - 1, level));
                            SolidBrush fill = cell.Energy >= result.MedianEnergy
                                ? highEnergyBrushes[level]
                                : lowEnergyBrushes[level];
                            graphics.FillRectangle(fill, destination);
                        }
                        if (showBoxes && cell.IsAnomaly)
                        {
                            graphics.DrawRectangle(
                                anomalyOutline,
                                destination.X,
                                destination.Y,
                                destination.Width,
                                destination.Height);
                        }
                    }
                }
                finally
                {
                    foreach (SolidBrush brush in highEnergyBrushes)
                    {
                        brush.Dispose();
                    }
                    foreach (SolidBrush brush in lowEnergyBrushes)
                    {
                        brush.Dispose();
                    }
                }
            }
        }
    }
}
