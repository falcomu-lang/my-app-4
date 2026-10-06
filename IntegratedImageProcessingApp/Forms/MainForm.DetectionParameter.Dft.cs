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
        private sealed class ObjectDetectionDftCell
        {
            public int ObjectNumber { get; set; }

            public Rectangle Bounds { get; set; }

            public double Directionality { get; set; }

            public double DominantLineAngleDegrees { get; set; }

            public double Score { get; set; }

            public bool IsAnomaly { get; set; }
        }

        private sealed class ObjectDetectionDftResult
        {
            public string Signature { get; set; }

            public double MedianDirectionality { get; set; }

            public double DirectionalityScale { get; set; }

            public double Sensitivity { get; set; }

            public long SourcePreparationMilliseconds { get; set; }

            public long TotalElapsedMilliseconds { get; set; }

            public long ScanElapsedMilliseconds { get; set; }

            public long TransformElapsedMilliseconds { get; set; }

            public long PreviewGenerationMilliseconds { get; set; }

            public long StatisticsMilliseconds { get; set; }

            public long ScannedWindowCount { get; set; }

            public Dictionary<int, PointF[]> ObjectPolygons { get; set; }

            public Dictionary<int, List<ObjectDetectionDftCell>> CellsByObject { get; set; }

            public List<ObjectDetectionDftCell> Cells { get; set; }

            public List<ObjectDetectionDefectProcessedPatch> ProcessedPatches { get; set; }
        }

        private readonly Dictionary<string, ObjectDetectionDftResult> objectDetectionDftResults =
            new Dictionary<string, ObjectDetectionDftResult>(StringComparer.Ordinal);

        private CheckBox objectDetectionDftEnabledCheckBox;
        private CheckBox objectDetectionDftContrastEnabledCheckBox;
        private NumericUpDown objectDetectionDftContrastGainInput;
        private ComboBox objectDetectionDftEnhancementMethodInput;
        private NumericUpDown objectDetectionDftLocalBackgroundKernelInput;
        private NumericUpDown objectDetectionDftLocalBackgroundGainInput;
        private NumericUpDown objectDetectionDftClaheClipLimitInput;
        private NumericUpDown objectDetectionDftClaheTileGridSizeInput;
        private NumericUpDown objectDetectionDftWindowSizeInput;
        private NumericUpDown objectDetectionDftMinimumPeriodInput;
        private NumericUpDown objectDetectionDftMaximumPeriodInput;
        private NumericUpDown objectDetectionDftSensitivityInput;
        private NumericUpDown objectDetectionDftDirectionalityThresholdInput;
        private CheckBox objectDetectionDftShowHeatmapCheckBox;
        private CheckBox objectDetectionDftShowBoxesCheckBox;
        private Label objectDetectionDftStatusLabel;
        private Button objectDetectionDftRunButton;
        private string objectDetectionDftDraftParameterId;
        private bool objectDetectionDftAnalysisRunning;

        private TabPage BuildObjectDetectionDftTab(ObjectDetectionParameterSettings parameter)
        {
            var page = new TabPage("DFT");
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 251, 253)
            };
            objectDetectionDftDraftParameterId = parameter.Id;

            var sourceInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 7, 8, 2),
                Text = "分析來源：平場校正後灰階影像\r\n以局部 2D DFT 比較各方向頻譜能量，檢出方向性異常。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(55, 63, 76)
            };

            var enabledPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
            objectDetectionDftEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "是否使用 DFT",
                Checked = parameter.DefectDftEnabled,
                Location = new Point(8, 5)
            };
            objectDetectionDftEnabledCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionDftEnabled(parameter, objectDetectionDftEnabledCheckBox.Checked);
            };
            enabledPanel.Controls.Add(objectDetectionDftEnabledCheckBox);

            var sourceGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 130,
                Text = "影像來源與對比",
                Padding = new Padding(8, 16, 8, 4)
            };
            var sourceLayout = CreateDefectCoreTable(4, 2);
            sourceLayout.RowStyles.Clear();
            for (int row = 0; row < 4; row++)
            {
                sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            }
            sourceLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "平場校正後影像",
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            sourceLayout.SetColumnSpan(sourceLayout.GetControlFromPosition(0, 0), 2);
            objectDetectionDftContrastEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "啟用對比調整",
                Checked = parameter.DefectDftContrastEnabled,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 2, 0, 0)
            };
            sourceLayout.Controls.Add(objectDetectionDftContrastEnabledCheckBox, 0, 1);
            sourceLayout.SetColumnSpan(objectDetectionDftContrastEnabledCheckBox, 2);
            sourceLayout.Controls.Add(CreateDefectCoreLabel("對比倍率"), 0, 2);
            objectDetectionDftContrastGainInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0.1m,
                Maximum = 5.0m,
                Increment = 0.05m,
                DecimalPlaces = 2,
                Value = (decimal)Math.Max(0.1, Math.Min(5.0, parameter.DefectDftContrastGain))
            };
            sourceLayout.Controls.Add(objectDetectionDftContrastGainInput, 1, 2);
            sourceGroup.Controls.Add(sourceLayout);

            var enhancementGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 155,
                Text = "淡色缺陷增強",
                Padding = new Padding(8, 16, 8, 4)
            };
            var enhancementLayout = CreateDefectCoreTable(5, 2);
            enhancementLayout.RowStyles.Clear();
            for (int row = 0; row < 5; row++)
            {
                enhancementLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            }
            objectDetectionDftEnhancementMethodInput = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            objectDetectionDftEnhancementMethodInput.Items.Add(new DefectCoreEnhancementOption("不處理", "None"));
            objectDetectionDftEnhancementMethodInput.Items.Add(new DefectCoreEnhancementOption("局部背景差異", "LocalBackgroundDifference"));
            objectDetectionDftEnhancementMethodInput.Items.Add(new DefectCoreEnhancementOption("CLAHE", "CLAHE"));
            SelectDftEnhancementMethod(parameter.DefectDftEnhancementMethod);
            enhancementLayout.Controls.Add(CreateDefectCoreLabel("處理方式"), 0, 0);
            enhancementLayout.Controls.Add(objectDetectionDftEnhancementMethodInput, 1, 0);
            enhancementLayout.Controls.Add(CreateDefectCoreLabel("局部背景核心 / 增益"), 0, 1);
            var localSettings = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            objectDetectionDftLocalBackgroundKernelInput = CreateDftNumeric(3, 501, 2, 31, 0);
            objectDetectionDftLocalBackgroundGainInput = CreateDftNumeric(0.1m, 10m, 0.1m, 1.5m, 2);
            localSettings.Controls.Add(objectDetectionDftLocalBackgroundKernelInput);
            localSettings.Controls.Add(new Label { AutoSize = true, Text = " px  ", Padding = new Padding(0, 7, 0, 0) });
            localSettings.Controls.Add(objectDetectionDftLocalBackgroundGainInput);
            enhancementLayout.Controls.Add(localSettings, 1, 1);
            enhancementLayout.Controls.Add(CreateDefectCoreLabel("CLAHE Clip / 格數"), 0, 2);
            var claheSettings = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            objectDetectionDftClaheClipLimitInput = CreateDftNumeric(0.1m, 40m, 0.1m, 2m, 2);
            objectDetectionDftClaheTileGridSizeInput = CreateDftNumeric(2, 32, 1, 8, 0);
            claheSettings.Controls.Add(objectDetectionDftClaheClipLimitInput);
            claheSettings.Controls.Add(new Label { AutoSize = true, Text = " / ", Padding = new Padding(0, 7, 0, 0) });
            claheSettings.Controls.Add(objectDetectionDftClaheTileGridSizeInput);
            enhancementLayout.Controls.Add(claheSettings, 1, 2);
            var enhancementHint = new Label
            {
                Dock = DockStyle.Fill,
                Text = "前處理只作用於 DFT，不會改變其他條件影像。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            enhancementLayout.Controls.Add(enhancementHint, 0, 3);
            enhancementLayout.SetColumnSpan(enhancementHint, 2);
            enhancementLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "局部背景差異使用核心大小與增益；CLAHE 使用 Clip Limit 與格數。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                AutoEllipsis = true
            }, 0, 4);
            enhancementLayout.SetColumnSpan(enhancementLayout.GetControlFromPosition(0, 4), 2);
            enhancementGroup.Controls.Add(enhancementLayout);

            var scanGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 190,
                Text = "DFT 掃描與判定",
                Padding = new Padding(8, 16, 8, 4)
            };
            var scanLayout = CreateDefectCoreTable(5, 2);
            scanLayout.RowStyles.Clear();
            for (int row = 0; row < 5; row++)
            {
                scanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            }
            objectDetectionDftWindowSizeInput = CreateDftNumeric(
                32, 1024, 16, Math.Max(32, Math.Min(1024, parameter.DefectDftWindowSize)), 0);
            objectDetectionDftMinimumPeriodInput = CreateDftNumeric(
                2, 512, 1, Math.Max(2, Math.Min(512, parameter.DefectDftMinimumPeriodPixels)), 0);
            objectDetectionDftMaximumPeriodInput = CreateDftNumeric(
                2, 512, 1, Math.Max(2, Math.Min(512, parameter.DefectDftMaximumPeriodPixels)), 0);
            objectDetectionDftSensitivityInput = CreateDftNumeric(
                1m, 10m, 0.5m, (decimal)Math.Max(1, Math.Min(10, parameter.DefectDftSensitivity)), 1);
            objectDetectionDftDirectionalityThresholdInput = CreateDftNumeric(
                1m, 10m, 0.05m,
                (decimal)Math.Max(1, Math.Min(10, parameter.DefectDftDirectionalityThreshold)), 2);
            AddDftScanRow(scanLayout, 0, "區塊邊長 (px)", objectDetectionDftWindowSizeInput);
            AddDftScanRow(scanLayout, 1, "頻率週期範圍 (px)",
                CreateDftRangePanel(objectDetectionDftMinimumPeriodInput, objectDetectionDftMaximumPeriodInput));
            AddDftScanRow(scanLayout, 2, "方向能量比下限", objectDetectionDftDirectionalityThresholdInput);
            AddDftScanRow(scanLayout, 3, "異常敏感度", objectDetectionDftSensitivityInput);
            var scanHint = new Label
            {
                Dock = DockStyle.Fill,
                Text = "自動比較 8 個方向；區塊以 50% 重疊掃描，週期範圍排除整體亮度與極細雜訊。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                AutoEllipsis = true
            };
            scanLayout.Controls.Add(scanHint, 0, 4);
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
            objectDetectionDftShowHeatmapCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示方向能量圖",
                Checked = parameter.DefectDftShowHeatmap,
                Margin = new Padding(2, 1, 14, 1)
            };
            objectDetectionDftShowBoxesCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示異常框",
                Checked = parameter.DefectDftShowAnomalyBoxes,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionDftShowHeatmapCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionDftDisplayOptions(parameter);
            };
            objectDetectionDftShowBoxesCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionDftDisplayOptions(parameter);
            };
            displayOptions.Controls.Add(objectDetectionDftShowHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionDftShowBoxesCheckBox);

            objectDetectionDftStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(8, 4, 8, 2),
                Text = parameter.DefectDftEnabled ? "尚未執行 DFT 分析。" : "DFT 分析已停用。",
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
            objectDetectionDftRunButton = new Button { Text = "開始 DFT 分析", Width = 112, Height = 27, Margin = new Padding(3, 0, 0, 0) };
            cancelButton.Click += delegate { CancelObjectDetectionDftSettings(parameter); };
            applyButton.Click += delegate { ApplyObjectDetectionDftSettings(parameter); };
            objectDetectionDftRunButton.Click += async delegate
            {
                if (ApplyObjectDetectionDftSettings(parameter))
                {
                    await RunObjectDetectionDftAnalysisAsync(parameter.Id);
                }
            };
            actionBar.Controls.Add(cancelButton);
            actionBar.Controls.Add(applyButton);
            actionBar.Controls.Add(objectDetectionDftRunButton);

            var pivotLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "基準灰階：平場目標值 " + parameter.FlatFieldTargetGray.ToString(CultureInfo.CurrentCulture),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            sourceLayout.Controls.Add(pivotLabel, 0, 3);
            sourceLayout.SetColumnSpan(pivotLabel, 2);
            objectDetectionDftContrastEnabledCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionDftControlsEnabled(parameter.DefectDftEnabled);
            };
            objectDetectionDftEnhancementMethodInput.SelectedIndexChanged += delegate
            {
                UpdateObjectDetectionDftControlsEnabled(parameter.DefectDftEnabled);
            };

            content.Controls.Add(actionBar);
            content.Controls.Add(objectDetectionDftStatusLabel);
            content.Controls.Add(displayOptions);
            content.Controls.Add(scanGroup);
            content.Controls.Add(enhancementGroup);
            content.Controls.Add(sourceGroup);
            content.Controls.Add(sourceInfo);
            content.Controls.Add(enabledPanel);
            page.Controls.Add(content);
            UpdateObjectDetectionDftControlsEnabled(parameter.DefectDftEnabled);
            return page;
        }

        private static NumericUpDown CreateDftNumeric(
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

        private static void AddDftScanRow(TableLayoutPanel layout, int row, string label, Control input)
        {
            layout.Controls.Add(CreateDefectCoreLabel(label), 0, row);
            input.Dock = DockStyle.Fill;
            layout.Controls.Add(input, 1, row);
        }

        private static Control CreateDftRangePanel(NumericUpDown minimum, NumericUpDown maximum)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            minimum.Width = 78;
            maximum.Width = 78;
            panel.Controls.Add(minimum);
            panel.Controls.Add(new Label { AutoSize = true, Text = " 至 ", Padding = new Padding(0, 7, 0, 0) });
            panel.Controls.Add(maximum);
            return panel;
        }

        private void SelectDftEnhancementMethod(string method)
        {
            if (objectDetectionDftEnhancementMethodInput == null)
            {
                return;
            }
            for (int index = 0; index < objectDetectionDftEnhancementMethodInput.Items.Count; index++)
            {
                DefectCoreEnhancementOption option =
                    objectDetectionDftEnhancementMethodInput.Items[index] as DefectCoreEnhancementOption;
                if (option != null && string.Equals(option.Value, method, StringComparison.OrdinalIgnoreCase))
                {
                    objectDetectionDftEnhancementMethodInput.SelectedIndex = index;
                    return;
                }
            }
            objectDetectionDftEnhancementMethodInput.SelectedIndex = 0;
        }

        private void UpdateObjectDetectionDftControlsEnabled(bool enabled)
        {
            bool contrastOn = enabled && objectDetectionDftContrastEnabledCheckBox != null &&
                objectDetectionDftContrastEnabledCheckBox.Checked;
            if (objectDetectionDftContrastEnabledCheckBox != null)
            {
                objectDetectionDftContrastEnabledCheckBox.Enabled = enabled;
            }
            if (objectDetectionDftContrastGainInput != null)
            {
                objectDetectionDftContrastGainInput.Enabled = contrastOn;
            }
            if (objectDetectionDftEnhancementMethodInput != null)
            {
                objectDetectionDftEnhancementMethodInput.Enabled = enabled;
            }
            DefectCoreEnhancementOption selected = objectDetectionDftEnhancementMethodInput == null
                ? null
                : objectDetectionDftEnhancementMethodInput.SelectedItem as DefectCoreEnhancementOption;
            bool local = enabled && selected != null && selected.Value == "LocalBackgroundDifference";
            bool clahe = enabled && selected != null && selected.Value == "CLAHE";
            if (objectDetectionDftLocalBackgroundKernelInput != null) objectDetectionDftLocalBackgroundKernelInput.Enabled = local;
            if (objectDetectionDftLocalBackgroundGainInput != null) objectDetectionDftLocalBackgroundGainInput.Enabled = local;
            if (objectDetectionDftClaheClipLimitInput != null) objectDetectionDftClaheClipLimitInput.Enabled = clahe;
            if (objectDetectionDftClaheTileGridSizeInput != null) objectDetectionDftClaheTileGridSizeInput.Enabled = clahe;
            foreach (Control input in new Control[]
            {
                objectDetectionDftWindowSizeInput,
                objectDetectionDftMinimumPeriodInput,
                objectDetectionDftMaximumPeriodInput,
                objectDetectionDftSensitivityInput,
                objectDetectionDftDirectionalityThresholdInput,
                objectDetectionDftShowHeatmapCheckBox,
                objectDetectionDftShowBoxesCheckBox
            })
            {
                if (input != null) input.Enabled = enabled;
            }
            if (objectDetectionDftRunButton != null)
            {
                objectDetectionDftRunButton.Enabled = enabled && !objectDetectionDftAnalysisRunning;
            }
        }

        private void UpdateObjectDetectionDftEnabled(ObjectDetectionParameterSettings parameter, bool enabled)
        {
            if (parameter == null)
            {
                return;
            }
            parameter.DefectDftEnabled = enabled;
            SaveSystemParameters();
            if (!enabled)
            {
                RemoveObjectDetectionDftResult(parameter.Id);
            }
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectDftDisplayIndex);
            if (display != null) display.InvalidateImageView();
            ImageDisplayControl integrated = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectIntegratedDisplayIndex);
            if (integrated != null) integrated.InvalidateImageView();
            UpdateObjectDetectionDftControlsEnabled(enabled);
            SetObjectDetectionDefectRegionStatus(enabled
                ? "DFT 方向性分析已啟用；需執行分析後才有結果。"
                : "DFT 分析已停用，DFT 標記已清除。 ");
        }

        private bool ApplyObjectDetectionDftSettings(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || !string.Equals(objectDetectionDftDraftParameterId, parameter.Id, StringComparison.Ordinal) ||
                objectDetectionDftWindowSizeInput == null || objectDetectionDftMaximumPeriodInput == null ||
                objectDetectionDftMinimumPeriodInput == null)
            {
                return false;
            }
            if (objectDetectionDftMaximumPeriodInput.Value <= objectDetectionDftMinimumPeriodInput.Value)
            {
                MessageBox.Show(this, "週期範圍上限必須大於下限。", "DFT 設定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            parameter.DefectDftWindowSize = (int)objectDetectionDftWindowSizeInput.Value;
            parameter.DefectDftMinimumPeriodPixels = (int)objectDetectionDftMinimumPeriodInput.Value;
            parameter.DefectDftMaximumPeriodPixels = (int)objectDetectionDftMaximumPeriodInput.Value;
            parameter.DefectDftSensitivity = Decimal.ToDouble(objectDetectionDftSensitivityInput.Value);
            parameter.DefectDftDirectionalityThreshold = Decimal.ToDouble(objectDetectionDftDirectionalityThresholdInput.Value);
            parameter.DefectDftContrastEnabled = objectDetectionDftContrastEnabledCheckBox.Checked;
            parameter.DefectDftContrastGain = Decimal.ToDouble(objectDetectionDftContrastGainInput.Value);
            DefectCoreEnhancementOption selected = objectDetectionDftEnhancementMethodInput.SelectedItem as DefectCoreEnhancementOption;
            parameter.DefectDftEnhancementMethod = selected == null ? "None" : selected.Value;
            parameter.DefectDftLocalBackgroundKernelSize = (int)objectDetectionDftLocalBackgroundKernelInput.Value;
            parameter.DefectDftLocalBackgroundGain = Decimal.ToDouble(objectDetectionDftLocalBackgroundGainInput.Value);
            parameter.DefectDftClaheClipLimit = Decimal.ToDouble(objectDetectionDftClaheClipLimitInput.Value);
            parameter.DefectDftClaheTileGridSize = (int)objectDetectionDftClaheTileGridSizeInput.Value;
            parameter.DefectDftShowHeatmap = objectDetectionDftShowHeatmapCheckBox.Checked;
            parameter.DefectDftShowAnomalyBoxes = objectDetectionDftShowBoxesCheckBox.Checked;
            RemoveObjectDetectionDftResult(parameter.Id);
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            SaveSystemParameters();
            UpdateObjectDetectionDftControlsEnabled(parameter.DefectDftEnabled);
            ImageDisplayControl dftDisplay = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectDftDisplayIndex);
            if (dftDisplay != null)
            {
                dftDisplay.InvalidateImageView();
            }
            ImageDisplayControl integratedDisplay = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectIntegratedDisplayIndex);
            if (integratedDisplay != null)
            {
                integratedDisplay.InvalidateImageView();
            }
            SetObjectDetectionDefectRegionStatus(
                "DFT 設定已套用；區塊 " + parameter.DefectDftWindowSize.ToString(CultureInfo.CurrentCulture) +
                " px，週期 " + parameter.DefectDftMinimumPeriodPixels.ToString(CultureInfo.CurrentCulture) + "–" +
                parameter.DefectDftMaximumPeriodPixels.ToString(CultureInfo.CurrentCulture) + " px。請執行 DFT 分析更新結果。 ");
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            return true;
        }

        private void CancelObjectDetectionDftSettings(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionDftWindowSizeInput == null)
            {
                return;
            }
            objectDetectionDftWindowSizeInput.Value = Math.Max(32, Math.Min(1024, parameter.DefectDftWindowSize));
            objectDetectionDftMinimumPeriodInput.Value = Math.Max(2, Math.Min(512, parameter.DefectDftMinimumPeriodPixels));
            objectDetectionDftMaximumPeriodInput.Value = Math.Max(2, Math.Min(512, parameter.DefectDftMaximumPeriodPixels));
            objectDetectionDftSensitivityInput.Value = (decimal)Math.Max(1, Math.Min(10, parameter.DefectDftSensitivity));
            objectDetectionDftDirectionalityThresholdInput.Value = (decimal)Math.Max(1, Math.Min(10, parameter.DefectDftDirectionalityThreshold));
            objectDetectionDftContrastEnabledCheckBox.Checked = parameter.DefectDftContrastEnabled;
            objectDetectionDftContrastGainInput.Value = (decimal)Math.Max(0.1, Math.Min(5, parameter.DefectDftContrastGain));
            SelectDftEnhancementMethod(parameter.DefectDftEnhancementMethod);
            objectDetectionDftLocalBackgroundKernelInput.Value = Math.Max(3, Math.Min(501, parameter.DefectDftLocalBackgroundKernelSize | 1));
            objectDetectionDftLocalBackgroundGainInput.Value = (decimal)Math.Max(0.1, Math.Min(10, parameter.DefectDftLocalBackgroundGain));
            objectDetectionDftClaheClipLimitInput.Value = (decimal)Math.Max(0.1, Math.Min(40, parameter.DefectDftClaheClipLimit));
            objectDetectionDftClaheTileGridSizeInput.Value = Math.Max(2, Math.Min(32, parameter.DefectDftClaheTileGridSize));
            objectDetectionDftShowHeatmapCheckBox.Checked = parameter.DefectDftShowHeatmap;
            objectDetectionDftShowBoxesCheckBox.Checked = parameter.DefectDftShowAnomalyBoxes;
            UpdateObjectDetectionDftControlsEnabled(parameter.DefectDftEnabled);
            SetObjectDetectionDefectRegionStatus("已取消 DFT 設定變更。 ");
        }

        private void UpdateObjectDetectionDftDisplayOptions(ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionDftShowHeatmapCheckBox == null || objectDetectionDftShowBoxesCheckBox == null)
            {
                return;
            }
            parameter.DefectDftShowHeatmap = objectDetectionDftShowHeatmapCheckBox.Checked;
            parameter.DefectDftShowAnomalyBoxes = objectDetectionDftShowBoxesCheckBox.Checked;
            SaveSystemParameters();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectDftDisplayIndex);
            if (display != null) display.InvalidateImageView();
        }

        private void RemoveObjectDetectionDftResult(string parameterId)
        {
            ObjectDetectionDftResult previous;
            if (!string.IsNullOrWhiteSpace(parameterId) && objectDetectionDftResults.TryGetValue(parameterId, out previous))
            {
                objectDetectionDftResults.Remove(parameterId);
                DisposeObjectDetectionDftResult(previous);
            }
        }

        private async Task RunObjectDetectionDftAnalysisAsync(string parameterId, bool calledFromResultReview = false)
        {
            if (objectDetectionDftAnalysisRunning) return;
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null || !parameter.DefectDftEnabled)
            {
                SetObjectDetectionDefectRegionStatus("DFT 分析目前未啟用。 ");
                return;
            }
            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId, parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                SetObjectDetectionDefectRegionStatus("請先框選並套用缺陷檢測範圍，再執行 DFT。 ");
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

            objectDetectionDftAnalysisRunning = true;
            UpdateObjectDetectionDftControlsEnabled(parameter.DefectDftEnabled);
            if (!calledFromResultReview && objectDetectionDefectCoreTabs != null) objectDetectionDefectCoreTabs.Enabled = false;
            if (!calledFromResultReview && leftImageTabControl != null)
            {
                leftImageTabControl.SelectedTab = GetObjectDetectionDefectDisplayTabPage(ObjectDetectionDefectDftDisplayIndex);
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
                    SetObjectDetectionDefectRegionStatus("沒有已完成定義的 ROI 物件可供 DFT 分析。 ");
                    return;
                }
                Rectangle imageBounds = new Rectangle(0, 0, source.Width, source.Height);
                RectangleF normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit((float)parameter.DefectInspectionRegionLeft),
                    ClampUnit((float)parameter.DefectInspectionRegionTop),
                    ClampUnit((float)parameter.DefectInspectionRegionRight),
                    ClampUnit((float)parameter.DefectInspectionRegionBottom));
                string signature = CreateObjectDetectionDftSignature(
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
                    ? "平行 DFT 分析中：各物件 ROI 同時處理... "
                    : "DFT 分析中：各物件 ROI 依序處理... ");
                Stopwatch scan = Stopwatch.StartNew();
                ObjectDetectionDftResult result = await Task.Run(delegate
                {
                    try
                    {
                        return ScanObjectDetectionDftRegions(
                            sourceReference,
                            objects,
                            normalizedRegion,
                            imageBounds,
                            parameter,
                            parameter.FlatFieldTargetGray,
                            parameter.DefectParallelExecutionEnabled,
                            progress);
                    }
                    finally
                    {
                        sourceReference.ReleaseReference();
                    }
                });
                scan.Stop();
                total.Stop();
                result.Signature = signature;
                result.SourcePreparationMilliseconds = sourcePreparation.ElapsedMilliseconds;
                result.ScanElapsedMilliseconds = scan.ElapsedMilliseconds;
                result.TotalElapsedMilliseconds = total.ElapsedMilliseconds;
                result.Sensitivity = parameter.DefectDftSensitivity;

                if (IsDisposed || !string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal) ||
                    !string.Equals(CreateObjectDefinitionProcessingSignature(definition), definitionSignature, StringComparison.Ordinal) ||
                    !string.Equals(CreateObjectDetectionDftSignature(
                        parameter, definitionSignature, imageSourceGeneration, objectDetectionFlatFieldEvaluationGeneration), signature, StringComparison.Ordinal))
                {
                    SetObjectDetectionDefectRegionStatus("分析期間影像或設定已變更，DFT 結果未套用；請重新分析。 ");
                    DisposeObjectDetectionDftResult(result);
                    return;
                }

                RemoveObjectDetectionDftResult(parameter.Id);
                objectDetectionDftResults[parameter.Id] = result;
                InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
                RefreshObjectDetectionDefectIntegrationResults(parameter);
                ImageDisplayControl dftDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectDftDisplayIndex);
                if (dftDisplay != null) dftDisplay.InvalidateImageView();
                ImageDisplayControl integratedDisplay = GetObjectDetectionDefectDisplayControl(ObjectDetectionDefectIntegratedDisplayIndex);
                if (integratedDisplay != null) integratedDisplay.InvalidateImageView();
                int anomalies = result.Cells.Count(cell => cell.IsAnomaly);
                string summary = string.Format(
                    CultureInfo.CurrentCulture,
                    "掃描 {0:N0} 格，方向異常 {1:N0} 格；總耗時 {2:N0} ms（來源準備 {3:N0}、DFT 掃描 {4:N0} ms）\r\nDFT 累計 {5:N0} ms；預覽 {6:N0} ms；統計 {7:N0} ms",
                    result.ScannedWindowCount,
                    anomalies,
                    result.TotalElapsedMilliseconds,
                    result.SourcePreparationMilliseconds,
                    result.ScanElapsedMilliseconds,
                    result.TransformElapsedMilliseconds,
                    result.PreviewGenerationMilliseconds,
                    result.StatisticsMilliseconds);
                if (objectDetectionDftStatusLabel != null) objectDetectionDftStatusLabel.Text = summary;
                SetObjectDetectionDefectRegionStatus(summary +
                    (IsObjectDetectionDefectDftIntegrationRequired(parameter)
                        ? "DFT 已納入綜合結果。 "
                        : "目前未納入綜合判定。 "));
            }
            catch (OutOfMemoryException)
            {
                SetObjectDetectionDefectRegionStatus("DFT 分析記憶體不足；請減少物件 ROI 數量或增大區塊尺寸。 ");
            }
            catch (Exception exception)
            {
                SetObjectDetectionDefectRegionStatus("DFT 分析失敗：" + exception.Message);
            }
            finally
            {
                objectDetectionDftAnalysisRunning = false;
                if (!IsDisposed)
                {
                    UpdateObjectDetectionDftControlsEnabled(parameter != null && parameter.DefectDftEnabled);
                    if (!calledFromResultReview && objectDetectionDefectCoreTabs != null && !objectDetectionDefectCoreTabs.IsDisposed)
                    {
                        objectDetectionDefectCoreTabs.Enabled = true;
                    }
                }
            }
        }

        private static ObjectDetectionDftResult ScanObjectDetectionDftRegions(
            LargeImageSource source,
            IList<ObjectDefinitionDetectedObject> objects,
            RectangleF normalizedRegion,
            Rectangle imageBounds,
            ObjectDetectionParameterSettings parameter,
            int pivotGray,
            bool runParallel,
            IProgress<string> progress)
        {
            var cellsByObject = new List<ObjectDetectionDftCell>[objects.Count];
            var polygonsByObject = new PointF[objects.Count][];
            var patchesByObject = new ObjectDetectionDefectProcessedPatch[objects.Count];
            long transformTicks = 0;
            long previewTicks = 0;
            long windowsScanned = 0;
            Action<int> scanObject = objectIndex =>
            {
                ObjectDefinitionDetectedObject detected = objects[objectIndex];
                PointF[] corners = CreateObjectDetectionDefectRegionImageCorners(detected, normalizedRegion);
                polygonsByObject[objectIndex] = corners;
                var objectCells = new List<ObjectDetectionDftCell>();
                cellsByObject[objectIndex] = objectCells;
                int left = (int)Math.Floor(corners.Min(point => point.X));
                int top = (int)Math.Floor(corners.Min(point => point.Y));
                int right = (int)Math.Ceiling(corners.Max(point => point.X));
                int bottom = (int)Math.Ceiling(corners.Max(point => point.Y));
                Rectangle crop = Rectangle.Intersect(
                    Rectangle.Intersect(Rectangle.FromLTRB(left, top, right, bottom), detected.Bounds), imageBounds);
                if (crop.Width < 32 || crop.Height < 32) return;

                using (Cv.Mat gray = CreateObjectDetectionDefectGrayRegionMat(source, crop))
                using (var contrasted = new Cv.Mat())
                using (var polygonMask = new Cv.Mat(crop.Height, crop.Width, Cv.MatType.CV_8UC1, Cv.Scalar.Black))
                {
                    double gain = parameter.DefectDftContrastEnabled
                        ? Math.Max(0.1, Math.Min(5.0, parameter.DefectDftContrastGain)) : 1.0;
                    double beta = Math.Max(1, Math.Min(255, pivotGray)) * (1.0 - gain);
                    gray.ConvertTo(contrasted, Cv.MatType.CV_8UC1, gain, beta);
                    Cv.Mat enhanced = null;
                    try
                    {
                        Stopwatch enhancement = Stopwatch.StartNew();
                        var enhancementSettings = new ObjectDetectionDefectCoreSettings
                        {
                            DefectEnhancementMethod = parameter.DefectDftEnhancementMethod,
                            LocalBackgroundKernelSize = parameter.DefectDftLocalBackgroundKernelSize,
                            LocalBackgroundGain = parameter.DefectDftLocalBackgroundGain,
                            ClaheClipLimit = parameter.DefectDftClaheClipLimit,
                            ClaheTileGridSize = parameter.DefectDftClaheTileGridSize
                        };
                        if (string.Equals(parameter.DefectDftEnhancementMethod, "LocalBackgroundDifference", StringComparison.OrdinalIgnoreCase))
                        {
                            enhanced = ApplyObjectDetectionDefectEnhancement(contrasted, enhancementSettings, pivotGray);
                        }
                        else if (string.Equals(parameter.DefectDftEnhancementMethod, "CLAHE", StringComparison.OrdinalIgnoreCase))
                        {
                            enhanced = ApplyObjectDetectionDefectClahe(contrasted, enhancementSettings);
                        }
                        enhancement.Stop();

                        Cv.Mat dftImage = enhanced ?? contrasted;
                        Stopwatch preview = Stopwatch.StartNew();
                        Bitmap previewBitmap = CreateObjectDetectionDefectPreviewBitmap(dftImage);
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
                        int windowSize = Math.Min(parameter.DefectDftWindowSize, Math.Min(crop.Width, crop.Height));
                        if (windowSize < 32) return;
                        int step = Math.Max(1, windowSize / 2);
                        List<int> xStarts = CreateFrequencyScanStarts(crop.Width, windowSize, step);
                        List<int> yStarts = CreateFrequencyScanStarts(crop.Height, windowSize, step);
                        using (var workspace = new ObjectDetectionFrequencyWorkspace(dftImage, polygonMask, windowSize))
                        using (var input = new Cv.Mat(windowSize, windowSize, Cv.MatType.CV_32FC1))
                        using (var spectrum = new Cv.Mat())
                        {
                            long objectWindowCount = 0;
                            Stopwatch scanLoop = Stopwatch.StartNew();
                            foreach (int y in yStarts)
                            {
                                foreach (int x in xStarts)
                                {
                                    objectWindowCount++;
                                    double directionality;
                                    double angle;
                                    Stopwatch transform = Stopwatch.StartNew();
                                    bool valid = TryCalculateObjectDetectionDftDirectionality(
                                        workspace,
                                        input,
                                        spectrum,
                                        x,
                                        y,
                                        windowSize,
                                        parameter.DefectDftMinimumPeriodPixels,
                                        parameter.DefectDftMaximumPeriodPixels,
                                        out directionality,
                                        out angle);
                                    transform.Stop();
                                    Interlocked.Add(ref transformTicks, transform.ElapsedTicks);
                                    if (!valid) continue;
                                    objectCells.Add(new ObjectDetectionDftCell
                                    {
                                        ObjectNumber = detected.Number,
                                        Bounds = new Rectangle(crop.X + x, crop.Y + y, windowSize, windowSize),
                                        Directionality = directionality,
                                        DominantLineAngleDegrees = angle
                                    });
                                }
                            }
                            scanLoop.Stop();
                            Interlocked.Add(ref windowsScanned, objectWindowCount);
                        }
                    }
                    finally
                    {
                        if (enhanced != null) enhanced.Dispose();
                    }
                }
                progress?.Report("DFT 掃描 ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
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

            var result = new ObjectDetectionDftResult
            {
                Cells = new List<ObjectDetectionDftCell>(),
                CellsByObject = new Dictionary<int, List<ObjectDetectionDftCell>>(),
                ObjectPolygons = new Dictionary<int, PointF[]>(),
                ProcessedPatches = new List<ObjectDetectionDefectProcessedPatch>(),
                ScannedWindowCount = windowsScanned,
                TransformElapsedMilliseconds = ConvertObjectDetectionFrequencyTicksToMilliseconds(transformTicks),
                PreviewGenerationMilliseconds = ConvertObjectDetectionFrequencyTicksToMilliseconds(previewTicks)
            };
            for (int index = 0; index < objects.Count; index++)
            {
                List<ObjectDetectionDftCell> cells = cellsByObject[index];
                if (cells == null) continue;
                int number = objects[index].Number;
                result.CellsByObject[number] = cells;
                result.ObjectPolygons[number] = polygonsByObject[index];
                result.Cells.AddRange(cells);
                if (patchesByObject[index] != null)
                {
                    result.ProcessedPatches.Add(patchesByObject[index]);
                    patchesByObject[index] = null;
                }
            }
            if (result.Cells.Count == 0) return result;

            Stopwatch statistics = Stopwatch.StartNew();
            double median = CalculateObjectDetectionFrequencyMedian(result.Cells.Select(cell => cell.Directionality).ToList());
            double mad = CalculateObjectDetectionFrequencyMedian(
                result.Cells.Select(cell => Math.Abs(cell.Directionality - median)).ToList());
            double scale = Math.Max(0.05, mad * 1.4826);
            foreach (ObjectDetectionDftCell cell in result.Cells)
            {
                cell.Score = Math.Max(0, cell.Directionality - median) / scale;
                cell.IsAnomaly = cell.Directionality >= parameter.DefectDftDirectionalityThreshold &&
                    cell.Score >= parameter.DefectDftSensitivity;
            }
            result.MedianDirectionality = median;
            result.DirectionalityScale = scale;
            statistics.Stop();
            result.StatisticsMilliseconds = statistics.ElapsedMilliseconds;
            return result;
        }

        private static unsafe bool TryCalculateObjectDetectionDftDirectionality(
            ObjectDetectionFrequencyWorkspace workspace,
            Cv.Mat input,
            Cv.Mat spectrum,
            int startX,
            int startY,
            int windowSize,
            int minimumPeriodPixels,
            int maximumPeriodPixels,
            out double directionality,
            out double dominantLineAngleDegrees)
        {
            directionality = 0;
            dominantLineAngleDegrees = 0;
            byte* image = (byte*)workspace.ImageData.ToPointer();
            byte* mask = (byte*)workspace.MaskData.ToPointer();
            byte* hann = (byte*)workspace.HannData.ToPointer();
            double sum = 0;
            int validPixels = 0;
            for (int row = 0; row < windowSize; row++)
            {
                byte* imageRow = image + (long)(startY + row) * workspace.ImageStep + startX;
                byte* maskRow = mask + (long)(startY + row) * workspace.MaskStep + startX;
                for (int column = 0; column < windowSize; column++)
                {
                    if (maskRow[column] == 0) continue;
                    sum += imageRow[column];
                    validPixels++;
                }
            }
            if (validPixels < (windowSize * windowSize * 0.35)) return false;

            float mean = (float)(sum / validPixels);
            float* inputData = (float*)input.Data.ToPointer();
            long inputStep = input.Step() / sizeof(float);
            float* hannData = (float*)hann;
            long hannStep = workspace.HannStep / sizeof(float);
            for (int row = 0; row < windowSize; row++)
            {
                byte* imageRow = image + (long)(startY + row) * workspace.ImageStep + startX;
                byte* maskRow = mask + (long)(startY + row) * workspace.MaskStep + startX;
                float* inputRow = inputData + (long)row * inputStep;
                float* hannRow = hannData + (long)row * hannStep;
                for (int column = 0; column < windowSize; column++)
                {
                    float centered = maskRow[column] == 0 ? 0 : imageRow[column] - mean;
                    inputRow[column] = centered * hannRow[column];
                }
            }

            Cv.Cv2.Dft(input, spectrum, Cv.DftFlags.ComplexOutput);
            const int sectorCount = 8;
            var sectorPower = new double[sectorCount];
            var sectorCountByBin = new int[sectorCount];
            float* spectrumData = (float*)spectrum.Data.ToPointer();
            long spectrumStep = spectrum.Step() / sizeof(float);
            int minPeriod = Math.Max(2, Math.Min(windowSize - 1, minimumPeriodPixels));
            int maxPeriod = Math.Max(minPeriod + 1, Math.Min(windowSize, maximumPeriodPixels));
            for (int row = 0; row < windowSize; row++)
            {
                float* spectrumRow = spectrumData + (long)row * spectrumStep;
                int fy = row <= windowSize / 2 ? row : row - windowSize;
                for (int column = 0; column < windowSize; column++)
                {
                    int fx = column <= windowSize / 2 ? column : column - windowSize;
                    double radius = Math.Sqrt((fx * fx) + (fy * fy));
                    if (radius < 1.0) continue;
                    double period = windowSize / radius;
                    if (period < minPeriod || period > maxPeriod) continue;
                    double angle = Math.Atan2(fy, fx);
                    if (angle < 0) angle += Math.PI;
                    if (angle >= Math.PI) angle -= Math.PI;
                    int sector = Math.Min(sectorCount - 1, (int)(angle * sectorCount / Math.PI));
                    float real = spectrumRow[column * 2];
                    float imaginary = spectrumRow[(column * 2) + 1];
                    double power = (real * real) + (imaginary * imaginary);
                    sectorPower[sector] += power;
                    sectorCountByBin[sector]++;
                }
            }

            var populated = new List<double>();
            int dominantSector = -1;
            double largestAverage = 0;
            for (int sector = 0; sector < sectorCount; sector++)
            {
                if (sectorCountByBin[sector] == 0) continue;
                double average = sectorPower[sector] / sectorCountByBin[sector];
                populated.Add(average);
                if (average > largestAverage)
                {
                    largestAverage = average;
                    dominantSector = sector;
                }
            }
            if (populated.Count < sectorCount - 1 || dominantSector < 0) return false;
            double baseline = CalculateObjectDetectionFrequencyMedian(populated);
            double totalPower = populated.Sum();
            double floor = Math.Max(1e-12, totalPower / populated.Count * 1e-6);
            directionality = largestAverage / Math.Max(floor, baseline);
            dominantLineAngleDegrees = (((dominantSector + 0.5) * 180.0 / sectorCount) + 90.0) % 180.0;
            return !double.IsNaN(directionality) && !double.IsInfinity(directionality);
        }

        private string CreateObjectDetectionDftSignature(
            ObjectDetectionParameterSettings parameter,
            string definitionSignature,
            int imageGeneration,
            int flatFieldGeneration)
        {
            return string.Join("|", new[]
            {
                parameter.Id ?? string.Empty,
                parameter.DefectDftEnabled ? "1" : "0",
                definitionSignature ?? string.Empty,
                imageGeneration.ToString(CultureInfo.InvariantCulture),
                flatFieldGeneration.ToString(CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionId ?? string.Empty,
                parameter.DefectInspectionRegionLeft.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionTop.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionRight.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionBottom.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectDftWindowSize.ToString(CultureInfo.InvariantCulture),
                parameter.DefectDftMinimumPeriodPixels.ToString(CultureInfo.InvariantCulture),
                parameter.DefectDftMaximumPeriodPixels.ToString(CultureInfo.InvariantCulture),
                parameter.DefectDftSensitivity.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectDftDirectionalityThreshold.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectDftContrastEnabled ? "1" : "0",
                parameter.DefectDftContrastGain.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectDftEnhancementMethod ?? string.Empty,
                parameter.DefectDftLocalBackgroundKernelSize.ToString(CultureInfo.InvariantCulture),
                parameter.DefectDftLocalBackgroundGain.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectDftClaheClipLimit.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectDftClaheTileGridSize.ToString(CultureInfo.InvariantCulture),
                parameter.FlatFieldTargetGray.ToString(CultureInfo.InvariantCulture),
                parameter.FlatFieldSavedSettingsSignature ?? string.Empty,
                CreateObjectDetectionFlatFieldSettingsSignature(parameter)
            });
        }

        private bool IsObjectDetectionDefectDftIntegrationRequired(ObjectDetectionParameterSettings parameter)
        {
            return parameter != null && parameter.DefectIntegrationIncludeDft && parameter.DefectDftEnabled;
        }

        private bool TryGetCurrentObjectDetectionDftResult(
            ObjectDetectionParameterSettings parameter,
            out ObjectDetectionDftResult result)
        {
            result = null;
            if (parameter == null || !parameter.DefectDftEnabled ||
                !objectDetectionDftResults.TryGetValue(parameter.Id, out result)) return false;
            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                RemoveObjectDetectionDftResult(parameter.Id);
                result = null;
                return false;
            }
            string signature = CreateObjectDetectionDftSignature(
                parameter,
                CreateObjectDefinitionProcessingSignature(definition),
                imageSourceGeneration,
                objectDetectionFlatFieldEvaluationGeneration);
            if (!string.Equals(result.Signature, signature, StringComparison.Ordinal))
            {
                RemoveObjectDetectionDftResult(parameter.Id);
                result = null;
                return false;
            }
            return true;
        }

        private void DrawObjectDetectionDftAnomalies(
            Graphics graphics,
            float zoom,
            PointF offset,
            Rectangle visibleSourceRect)
        {
            if (graphics == null || zoom <= 0 || !isObjectDetectionParameterImageLayout) return;
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDetectionDftResult result;
            if (!TryGetCurrentObjectDetectionDftResult(parameter, out result)) return;
            RectangleF visible = visibleSourceRect;
            if (result.ProcessedPatches != null)
            {
                foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
                {
                    if (patch == null || patch.ProcessedImage == null || !patch.Bounds.IntersectsWith(Rectangle.Ceiling(visible))) continue;
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

            using (var anomalyPen = new Pen(Color.OrangeRed, Math.Max(1f, Math.Min(3f, zoom * 1.5f))))
            using (var highBrush = new SolidBrush(Color.FromArgb(44, 255, 196, 0)))
            using (var lowBrush = new SolidBrush(Color.FromArgb(26, 80, 170, 255)))
            {
                foreach (KeyValuePair<int, List<ObjectDetectionDftCell>> entry in result.CellsByObject)
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
                        foreach (ObjectDetectionDftCell cell in entry.Value)
                        {
                            if (!cell.Bounds.IntersectsWith(Rectangle.Ceiling(visible))) continue;
                            var rect = new RectangleF(
                                offset.X + cell.Bounds.X * zoom,
                                offset.Y + cell.Bounds.Y * zoom,
                                Math.Max(1, cell.Bounds.Width * zoom),
                                Math.Max(1, cell.Bounds.Height * zoom));
                            if (parameter.DefectDftShowHeatmap)
                            {
                                int alpha = Math.Max(8, Math.Min(64, 8 + (int)(cell.Score * 5)));
                                using (var fill = new SolidBrush(Color.FromArgb(
                                    alpha,
                                    cell.IsAnomaly ? Color.Gold : Color.DeepSkyBlue)))
                                {
                                    graphics.FillRectangle(fill, rect);
                                }
                            }
                            if (parameter.DefectDftShowAnomalyBoxes && cell.IsAnomaly)
                            {
                                graphics.DrawRectangle(anomalyPen, rect.X, rect.Y, rect.Width, rect.Height);
                            }
                        }
                    }
                    finally { graphics.Restore(state); }
                }
            }
        }

        private static void DisposeObjectDetectionDftResult(ObjectDetectionDftResult result)
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
