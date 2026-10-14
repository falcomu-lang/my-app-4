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
        private static readonly int[] ObjectDetectionFrequencyVerticalStretchPercents =
        {
            25, 50, 75, 100, 200, 300
        };

        private static int GetObjectDetectionFrequencyVerticalStretchIndex(int percent)
        {
            int index = Array.IndexOf(ObjectDetectionFrequencyVerticalStretchPercents, percent);
            return index < 0 ? 3 : index;
        }

        private static int GetObjectDetectionFrequencyVerticalStretchPercent(int selectedIndex)
        {
            return selectedIndex >= 0 &&
                selectedIndex < ObjectDetectionFrequencyVerticalStretchPercents.Length
                ? ObjectDetectionFrequencyVerticalStretchPercents[selectedIndex]
                : 100;
        }

        private static int GetObjectDetectionFrequencyAnalysisHeight(int sourceHeight, int percent)
        {
            return Math.Max(1, (int)Math.Round(
                sourceHeight * percent / 100.0,
                MidpointRounding.AwayFromZero));
        }

        private static bool IsSupportedObjectDetectionFrequencyVerticalStretchPercent(int percent)
        {
            return Array.IndexOf(ObjectDetectionFrequencyVerticalStretchPercents, percent) >= 0;
        }

        private static string FormatObjectDetectionFrequencyVerticalStretch(int percent)
        {
            return (percent / 100.0).ToString("0.##", CultureInfo.CurrentCulture) + "X";
        }

        private sealed class ObjectDetectionFrequencyCell
        {
            public int ObjectNumber { get; set; }

            public Rectangle Bounds { get; set; }

            public Rectangle ValidBounds { get; set; }

            public PointF[] ImageCorners { get; set; }

            public int GridColumn { get; set; }

            public int GridRow { get; set; }

            public double MeanGray { get; set; }

            public double LowerQuartileBlockMean { get; set; }

            public double Energy { get; set; }

            public double Score { get; set; }

            public bool IsAnomaly { get; set; }

            public double NeighborReferenceGray { get; set; }

            public double NeighborDeltaGray { get; set; }

            public double NeighborReferenceLowerQuartile { get; set; }

            public double NeighborDarkDeltaGray { get; set; }

            public double NeighborScore { get; set; }

            public bool IsNeighborAnomaly { get; set; }

            public bool IsHealthyReference { get; set; }

            public bool HasIndependentCoreDefect { get; set; }
        }

        private sealed class ObjectDetectionFrequencyResult
        {
            public string Signature { get; set; }

            public int ScanHeight { get; set; }

            public int VerticalStretchPercent { get; set; }

            public double Sensitivity { get; set; }

            public double NeighborSensitivity { get; set; }

            public int NeighborReferenceRadius { get; set; }

            public int NeighborReferenceCellCount { get; set; }

            public int NeighborEvaluatedCellCount { get; set; }

            public long NeighborComparisonMilliseconds { get; set; }

            public double MedianEnergy { get; set; }

            public double EnergyScale { get; set; }

            public long ElapsedMilliseconds { get; set; }

            public long SourcePreparationMilliseconds { get; set; }

            public long TotalElapsedMilliseconds { get; set; }

            public long CellLoopMilliseconds { get; set; }

            public long PreviewGenerationMilliseconds { get; set; }

            public long StatisticsMilliseconds { get; set; }

            public long ScannedWindowCount { get; set; }

            public Dictionary<int, PointF[]> ObjectPolygons { get; set; }

            public Dictionary<int, List<ObjectDetectionFrequencyCell>> CellsByObject { get; set; }

            public List<ObjectDetectionDefectProcessedPatch> ProcessedPatches { get; set; }

            public List<ObjectDetectionFrequencyCell> Cells { get; set; }
        }

        private sealed class ObjectDetectionFrequencyWorkspace : IDisposable
        {
            public ObjectDetectionFrequencyWorkspace(
                Cv.Mat image,
                Cv.Mat mask,
                int windowSize,
                int verticalStretchPercent)
            {
                if (image == null || image.Empty() || image.Type() != Cv.MatType.CV_8UC1 ||
                    mask == null || mask.Empty() || mask.Type() != Cv.MatType.CV_8UC1 ||
                    image.Cols != mask.Cols || image.Rows != mask.Rows ||
                    windowSize < 8 || windowSize > image.Cols || windowSize > image.Rows ||
                    !IsSupportedObjectDetectionFrequencyVerticalStretchPercent(verticalStretchPercent))
                {
                    throw new ArgumentException("頻域掃描需要有效且尺寸相同的灰階影像與 MASK。");
                }

                ImageData = image.Data;
                ImageStep = image.Step();
                Image = image;
                MaskData = mask.Data;
                MaskStep = mask.Step();
                ImageWidth = image.Cols;
                ImageHeight = image.Rows;
                WindowWidth = windowSize;
                WindowHeight = GetObjectDetectionFrequencyAnalysisHeight(
                    windowSize,
                    verticalStretchPercent);
                VerticalStretchPercent = verticalStretchPercent;
                HannWindow = new Cv.Mat();
                try
                {
                    Cv.Cv2.CreateHanningWindow(
                        HannWindow,
                        new Cv.Size(WindowWidth, WindowHeight),
                        Cv.MatType.CV_32FC1);
                    HannData = HannWindow.Data;
                    HannStep = HannWindow.Step();
                    if (VerticalStretchPercent != 100)
                    {
                        ResizedWindow = new Cv.Mat(
                            WindowHeight,
                            WindowWidth,
                            Cv.MatType.CV_8UC1);
                    }
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public IntPtr ImageData { get; private set; }

            public Cv.Mat Image { get; private set; }

            public long ImageStep { get; private set; }

            public IntPtr MaskData { get; private set; }

            public long MaskStep { get; private set; }

            public int ImageWidth { get; private set; }

            public int ImageHeight { get; private set; }

            public int WindowWidth { get; private set; }

            public int WindowHeight { get; private set; }

            public int VerticalStretchPercent { get; private set; }

            public Cv.Mat HannWindow { get; private set; }

            public Cv.Mat ResizedWindow { get; private set; }

            public IntPtr HannData { get; private set; }

            public long HannStep { get; private set; }

            public void Dispose()
            {
                if (ResizedWindow != null)
                {
                    ResizedWindow.Dispose();
                    ResizedWindow = null;
                }
                if (HannWindow != null)
                {
                    HannWindow.Dispose();
                    HannWindow = null;
                }
            }
        }

        private readonly Dictionary<string, ObjectDetectionFrequencyResult>
            objectDetectionFrequencyResults =
                new Dictionary<string, ObjectDetectionFrequencyResult>(StringComparer.Ordinal);

        private NumericUpDown objectDetectionFrequencyScanHeightInput;
        private ComboBox objectDetectionFrequencyVerticalStretchComboBox;
        private NumericUpDown objectDetectionFrequencySensitivityInput;
        private CheckBox objectDetectionFrequencyContrastEnabledCheckBox;
        private NumericUpDown objectDetectionFrequencyContrastGainInput;
        private CheckBox objectDetectionFrequencyNeighborEnabledCheckBox;
        private NumericUpDown objectDetectionFrequencyNeighborRadiusInput;
        private NumericUpDown objectDetectionFrequencyNeighborSensitivityInput;
        private CheckBox objectDetectionFrequencyEnabledCheckBox;
        private CheckBox objectDetectionFrequencyShowHeatmapCheckBox;
        private CheckBox objectDetectionFrequencyShowBoxesCheckBox;
        private CheckBox objectDetectionFrequencyShowNeighborHeatmapCheckBox;
        private CheckBox objectDetectionFrequencyShowNeighborBoxesCheckBox;
        private CheckBox objectDetectionFrequencyShowReferenceCellsCheckBox;
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

            var sourceGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 133,
                Text = "影像來源與對比",
                Padding = new Padding(8, 16, 8, 4)
            };
            var sourceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            for (int row = 0; row < 4; row++)
            {
                sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            }
            var sourceLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "平場校正後影像",
                TextAlign = ContentAlignment.MiddleLeft
            };
            objectDetectionFrequencyContrastEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Text = "啟用對比調整",
                Checked = parameter.DefectFrequencyContrastEnabled,
                Margin = new Padding(0, 2, 0, 0)
            };
            objectDetectionFrequencyContrastGainInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0.1m,
                Maximum = 5m,
                Increment = 0.05m,
                DecimalPlaces = 2,
                Value = (decimal)Math.Max(0.1, Math.Min(5.0, parameter.DefectFrequencyContrastGain))
            };
            var sourceBaselineLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "基準灰階：平場目標值 " + parameter.FlatFieldTargetGray.ToString(CultureInfo.CurrentCulture),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            sourceLayout.Controls.Add(sourceLabel, 0, 0);
            sourceLayout.SetColumnSpan(sourceLabel, 2);
            sourceLayout.Controls.Add(objectDetectionFrequencyContrastEnabledCheckBox, 0, 1);
            sourceLayout.SetColumnSpan(objectDetectionFrequencyContrastEnabledCheckBox, 2);
            sourceLayout.Controls.Add(CreateDefectCoreLabel("對比倍率"), 0, 2);
            sourceLayout.Controls.Add(objectDetectionFrequencyContrastGainInput, 1, 2);
            sourceLayout.Controls.Add(sourceBaselineLabel, 0, 3);
            sourceLayout.SetColumnSpan(sourceBaselineLabel, 2);
            sourceGroup.Controls.Add(sourceLayout);
            objectDetectionFrequencyContrastEnabledCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyContrastControlsEnabled(
                    objectDetectionFrequencyEnabledCheckBox != null &&
                    objectDetectionFrequencyEnabledCheckBox.Checked);
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
                Height = 198,
                Text = "掃描設定",
                Padding = new Padding(8, 16, 8, 4)
            };
            var scanLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            scanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            scanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            for (int row = 0; row < 5; row++)
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
            objectDetectionFrequencyVerticalStretchComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            objectDetectionFrequencyVerticalStretchComboBox.Items.AddRange(new object[]
            {
                "0.25X",
                "0.5X",
                "0.75X",
                "1X（不改變）",
                "2X",
                "3X"
            });
            objectDetectionFrequencyVerticalStretchComboBox.SelectedIndex =
                GetObjectDetectionFrequencyVerticalStretchIndex(
                    parameter.DefectFrequencyVerticalStretchPercent);
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
            scanLayout.Controls.Add(CreateDefectCoreLabel("垂直縮放倍率"), 0, 2);
            scanLayout.Controls.Add(objectDetectionFrequencyVerticalStretchComboBox, 1, 2);
            scanLayout.Controls.Add(CreateDefectCoreLabel("區域能量敏感度"), 0, 3);
            scanLayout.Controls.Add(objectDetectionFrequencySensitivityInput, 1, 3);
            scanLayout.Controls.Add(baselineInfo, 0, 4);
            scanLayout.SetColumnSpan(baselineInfo, 2);
            scanGroup.Controls.Add(scanLayout);

            var neighborGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 101,
                Text = "鄰近良品基準比較",
                Padding = new Padding(8, 16, 8, 4)
            };
            var neighborLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            neighborLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            neighborLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            neighborLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            neighborLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            neighborLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            objectDetectionFrequencyNeighborEnabledCheckBox = new CheckBox
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Text = "啟用鄰近良品比較",
                Checked = parameter.DefectFrequencyNeighborComparisonEnabled,
                Margin = new Padding(0, 2, 0, 0)
            };
            objectDetectionFrequencyNeighborRadiusInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 2,
                Maximum = 20,
                Value = Math.Max(2, Math.Min(20,
                    parameter.DefectFrequencyNeighborReferenceRadius))
            };
            objectDetectionFrequencyNeighborSensitivityInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1.0m,
                Maximum = 10.0m,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Value = (decimal)Math.Max(1.0, Math.Min(10.0,
                    parameter.DefectFrequencyNeighborSensitivity))
            };
            neighborLayout.Controls.Add(objectDetectionFrequencyNeighborEnabledCheckBox, 0, 0);
            neighborLayout.SetColumnSpan(objectDetectionFrequencyNeighborEnabledCheckBox, 2);
            neighborLayout.Controls.Add(CreateDefectCoreLabel("健康參考搜尋半徑（格）"), 0, 1);
            neighborLayout.Controls.Add(objectDetectionFrequencyNeighborRadiusInput, 1, 1);
            neighborLayout.Controls.Add(CreateDefectCoreLabel("比較敏感度"), 0, 2);
            neighborLayout.Controls.Add(objectDetectionFrequencyNeighborSensitivityInput, 1, 2);
            neighborGroup.Controls.Add(neighborLayout);
            objectDetectionFrequencyNeighborEnabledCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyNeighborControlsEnabled(
                    objectDetectionFrequencyEnabledCheckBox != null &&
                    objectDetectionFrequencyEnabledCheckBox.Checked);
            };

            var displayOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
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
            objectDetectionFrequencyShowNeighborHeatmapCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示鄰格熱圖",
                Checked = parameter.DefectFrequencyShowNeighborHeatmap,
                Margin = new Padding(2, 1, 14, 1)
            };
            objectDetectionFrequencyShowNeighborBoxesCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示鄰格異常框",
                Checked = parameter.DefectFrequencyShowNeighborAnomalyBoxes,
                Margin = new Padding(2, 1, 14, 1)
            };
            objectDetectionFrequencyShowReferenceCellsCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "顯示健康參考格",
                Checked = parameter.DefectFrequencyShowHealthyReferenceCells,
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
            objectDetectionFrequencyShowNeighborHeatmapCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyDisplayOptions(parameter);
            };
            objectDetectionFrequencyShowNeighborBoxesCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyDisplayOptions(parameter);
            };
            objectDetectionFrequencyShowReferenceCellsCheckBox.CheckedChanged += delegate
            {
                UpdateObjectDetectionFrequencyDisplayOptions(parameter);
            };
            displayOptions.Controls.Add(objectDetectionFrequencyShowHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionFrequencyShowBoxesCheckBox);
            displayOptions.Controls.Add(objectDetectionFrequencyShowNeighborHeatmapCheckBox);
            displayOptions.Controls.Add(objectDetectionFrequencyShowNeighborBoxesCheckBox);
            displayOptions.Controls.Add(objectDetectionFrequencyShowReferenceCellsCheckBox);
            displayOptions.Height = 62;

            objectDetectionFrequencyStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 68,
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
            objectDetectionFrequencyVerticalStretchComboBox.SelectedIndexChanged += delegate
            {
                UpdateObjectDetectionFrequencyWindowHint();
            };
            content.Controls.Add(actionBar);
            content.Controls.Add(objectDetectionFrequencyStatusLabel);
            content.Controls.Add(displayOptions);
            content.Controls.Add(neighborGroup);
            content.Controls.Add(scanGroup);
            content.Controls.Add(sourceGroup);
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
                RemoveObjectDetectionFrequencyResult(parameter.Id);
                if (objectDetectionFrequencyStatusLabel != null)
                {
                    objectDetectionFrequencyStatusLabel.Text = "頻域分析已停用。";
                }
            }
            else if (objectDetectionFrequencyStatusLabel != null)
            {
                objectDetectionFrequencyStatusLabel.Text = "頻域分析已啟用，按「開始分析」更新結果。";
            }

            RefreshObjectDetectionDefectIntegrationResults(parameter);
            InvalidateObjectDetectionDefectIntegrationDisplay();

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
            if (objectDetectionFrequencyContrastEnabledCheckBox != null)
            {
                objectDetectionFrequencyContrastEnabledCheckBox.Enabled = enabled;
            }
            UpdateObjectDetectionFrequencyContrastControlsEnabled(enabled);
            UpdateObjectDetectionFrequencyNeighborControlsEnabled(enabled);
            if (objectDetectionFrequencyVerticalStretchComboBox != null)
            {
                objectDetectionFrequencyVerticalStretchComboBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowHeatmapCheckBox != null)
            {
                objectDetectionFrequencyShowHeatmapCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowBoxesCheckBox != null)
            {
                objectDetectionFrequencyShowBoxesCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowNeighborHeatmapCheckBox != null)
            {
                objectDetectionFrequencyShowNeighborHeatmapCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowNeighborBoxesCheckBox != null)
            {
                objectDetectionFrequencyShowNeighborBoxesCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyShowReferenceCellsCheckBox != null)
            {
                objectDetectionFrequencyShowReferenceCellsCheckBox.Enabled = enabled;
            }
            if (objectDetectionFrequencyRunButton != null)
            {
                objectDetectionFrequencyRunButton.Enabled = enabled &&
                    !objectDetectionFrequencyAnalysisRunning;
            }
        }

        private void UpdateObjectDetectionFrequencyContrastControlsEnabled(bool frequencyEnabled)
        {
            if (objectDetectionFrequencyContrastGainInput != null)
            {
                objectDetectionFrequencyContrastGainInput.Enabled = frequencyEnabled &&
                    objectDetectionFrequencyContrastEnabledCheckBox != null &&
                    objectDetectionFrequencyContrastEnabledCheckBox.Checked;
            }
        }

        private void UpdateObjectDetectionFrequencyNeighborControlsEnabled(bool frequencyEnabled)
        {
            bool comparisonEnabled = frequencyEnabled &&
                objectDetectionFrequencyNeighborEnabledCheckBox != null &&
                objectDetectionFrequencyNeighborEnabledCheckBox.Checked;
            if (objectDetectionFrequencyNeighborEnabledCheckBox != null)
            {
                objectDetectionFrequencyNeighborEnabledCheckBox.Enabled = frequencyEnabled;
            }
            if (objectDetectionFrequencyNeighborRadiusInput != null)
            {
                objectDetectionFrequencyNeighborRadiusInput.Enabled = comparisonEnabled;
            }
            if (objectDetectionFrequencyNeighborSensitivityInput != null)
            {
                objectDetectionFrequencyNeighborSensitivityInput.Enabled = comparisonEnabled;
            }
        }

        private void RemoveObjectDetectionFrequencyResult(string parameterId)
        {
            ObjectDetectionFrequencyResult previousResult;
            if (!string.IsNullOrWhiteSpace(parameterId) &&
                objectDetectionFrequencyResults.TryGetValue(parameterId, out previousResult))
            {
                objectDetectionFrequencyResults.Remove(parameterId);
                DisposeObjectDetectionFrequencyResult(previousResult);
            }
            InvalidateObjectDetectionDefectIntegrationCache(parameterId);
        }

        private void InvalidateObjectDetectionDefectIntegrationDisplay()
        {
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectIntegratedDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
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
            int stretchPercent = objectDetectionFrequencyVerticalStretchComboBox == null
                ? 100
                : GetObjectDetectionFrequencyVerticalStretchPercent(
                    objectDetectionFrequencyVerticalStretchComboBox.SelectedIndex);
            string originalSize = size.ToString(CultureInfo.CurrentCulture) + " × " +
                size.ToString(CultureInfo.CurrentCulture);
            string analysisSize = size.ToString(CultureInfo.CurrentCulture) + " × " +
                GetObjectDetectionFrequencyAnalysisHeight(size, stretchPercent)
                    .ToString(CultureInfo.CurrentCulture);
            objectDetectionFrequencyWindowHint.Text = stretchPercent == 100
                ? "分析格 " + originalSize + " px；間距 " +
                    Math.Max(1, size / 2).ToString(CultureInfo.CurrentCulture) + " px"
                : "原格 " + originalSize + " → 分析 " + analysisSize + " px（" +
                    FormatObjectDetectionFrequencyVerticalStretch(stretchPercent) + "）；間距 " +
                    Math.Max(1, size / 2).ToString(CultureInfo.CurrentCulture) + " px";
        }

        private void ApplyObjectDetectionFrequencySettings(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                !string.Equals(objectDetectionFrequencyDraftParameterId, parameter.Id,
                    StringComparison.Ordinal) ||
                objectDetectionFrequencyScanHeightInput == null ||
                objectDetectionFrequencySensitivityInput == null ||
                objectDetectionFrequencyVerticalStretchComboBox == null)
            {
                return;
            }

            parameter.DefectFrequencyScanHeight = (int)objectDetectionFrequencyScanHeightInput.Value;
            parameter.DefectFrequencyVerticalStretchPercent =
                GetObjectDetectionFrequencyVerticalStretchPercent(
                    objectDetectionFrequencyVerticalStretchComboBox.SelectedIndex);
            parameter.DefectFrequencySensitivity = Decimal.ToDouble(
                objectDetectionFrequencySensitivityInput.Value);
            parameter.DefectFrequencyContrastEnabled =
                objectDetectionFrequencyContrastEnabledCheckBox.Checked;
            parameter.DefectFrequencyContrastGain = Decimal.ToDouble(
                objectDetectionFrequencyContrastGainInput.Value);
            parameter.DefectFrequencyNeighborComparisonEnabled =
                objectDetectionFrequencyNeighborEnabledCheckBox.Checked;
            parameter.DefectFrequencyNeighborReferenceRadius =
                Decimal.ToInt32(objectDetectionFrequencyNeighborRadiusInput.Value);
            parameter.DefectFrequencyNeighborSensitivity = Decimal.ToDouble(
                objectDetectionFrequencyNeighborSensitivityInput.Value);
            parameter.DefectFrequencyShowHeatmap =
                objectDetectionFrequencyShowHeatmapCheckBox.Checked;
            parameter.DefectFrequencyShowAnomalyBoxes =
                objectDetectionFrequencyShowBoxesCheckBox.Checked;
            parameter.DefectFrequencyShowNeighborHeatmap =
                objectDetectionFrequencyShowNeighborHeatmapCheckBox.Checked;
            parameter.DefectFrequencyShowNeighborAnomalyBoxes =
                objectDetectionFrequencyShowNeighborBoxesCheckBox.Checked;
            parameter.DefectFrequencyShowHealthyReferenceCells =
                objectDetectionFrequencyShowReferenceCellsCheckBox.Checked;
            SaveSystemParameters();
            RemoveObjectDetectionFrequencyResult(parameter.Id);
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            InvalidateObjectDetectionDefectIntegrationDisplay();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectFrequencyDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
            if (objectDetectionFrequencyStatusLabel != null)
            {
                objectDetectionFrequencyStatusLabel.Text =
                    "設定已套用；按「開始分析」後，左圖會顯示實際送入頻域掃描的影像。";
            }
            SetObjectDetectionDefectRegionStatus(
                "頻域異常設定已套用；掃描高度 " +
                parameter.DefectFrequencyScanHeight.ToString(CultureInfo.CurrentCulture) +
                " px，垂直縮放 " +
                FormatObjectDetectionFrequencyVerticalStretch(
                    parameter.DefectFrequencyVerticalStretchPercent) +
                "，敏感度 " +
                parameter.DefectFrequencySensitivity.ToString("0.0", CultureInfo.CurrentCulture) +
                "。來源為平場校正後灰階影像；對比調整「" +
                (parameter.DefectFrequencyContrastEnabled ? "已啟用，倍率 " +
                    parameter.DefectFrequencyContrastGain.ToString("0.00", CultureInfo.CurrentCulture) : "未啟用") +
                "；設定變更後請重新分析。 ");
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
            if (objectDetectionFrequencyVerticalStretchComboBox != null)
            {
                objectDetectionFrequencyVerticalStretchComboBox.SelectedIndex =
                    GetObjectDetectionFrequencyVerticalStretchIndex(
                        parameter.DefectFrequencyVerticalStretchPercent);
            }
            objectDetectionFrequencyContrastEnabledCheckBox.Checked =
                parameter.DefectFrequencyContrastEnabled;
            objectDetectionFrequencyContrastGainInput.Value = (decimal)Math.Max(
                (double)objectDetectionFrequencyContrastGainInput.Minimum,
                Math.Min((double)objectDetectionFrequencyContrastGainInput.Maximum,
                    parameter.DefectFrequencyContrastGain));
            objectDetectionFrequencyNeighborEnabledCheckBox.Checked =
                parameter.DefectFrequencyNeighborComparisonEnabled;
            objectDetectionFrequencyNeighborRadiusInput.Value = Math.Max(
                objectDetectionFrequencyNeighborRadiusInput.Minimum,
                Math.Min(objectDetectionFrequencyNeighborRadiusInput.Maximum,
                    parameter.DefectFrequencyNeighborReferenceRadius));
            objectDetectionFrequencyNeighborSensitivityInput.Value = (decimal)Math.Max(
                (double)objectDetectionFrequencyNeighborSensitivityInput.Minimum,
                Math.Min((double)objectDetectionFrequencyNeighborSensitivityInput.Maximum,
                    parameter.DefectFrequencyNeighborSensitivity));
            objectDetectionFrequencyShowHeatmapCheckBox.Checked =
                parameter.DefectFrequencyShowHeatmap;
            objectDetectionFrequencyShowBoxesCheckBox.Checked =
                parameter.DefectFrequencyShowAnomalyBoxes;
            objectDetectionFrequencyShowNeighborHeatmapCheckBox.Checked =
                parameter.DefectFrequencyShowNeighborHeatmap;
            objectDetectionFrequencyShowNeighborBoxesCheckBox.Checked =
                parameter.DefectFrequencyShowNeighborAnomalyBoxes;
            objectDetectionFrequencyShowReferenceCellsCheckBox.Checked =
                parameter.DefectFrequencyShowHealthyReferenceCells;
            UpdateObjectDetectionFrequencyControlsEnabled(
                objectDetectionFrequencyEnabledCheckBox != null &&
                objectDetectionFrequencyEnabledCheckBox.Checked);
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
            parameter.DefectFrequencyShowNeighborHeatmap =
                objectDetectionFrequencyShowNeighborHeatmapCheckBox.Checked;
            parameter.DefectFrequencyShowNeighborAnomalyBoxes =
                objectDetectionFrequencyShowNeighborBoxesCheckBox.Checked;
            parameter.DefectFrequencyShowHealthyReferenceCells =
                objectDetectionFrequencyShowReferenceCellsCheckBox.Checked;
            SaveSystemParameters();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectFrequencyDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
        }

        private async Task RunObjectDetectionFrequencyAnalysisAsync(
            string parameterId,
            bool isCoordinatedRun = false,
            CancellationToken cancellationToken = default(CancellationToken),
            int capturedRecipeGeneration = -1,
            bool includeIndependentCoreDefects = true)
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

            if (!cancellationToken.CanBeCanceled)
            {
                cancellationToken = detectionRecipeCancellationTokenSource.Token;
            }
            if (capturedRecipeGeneration < 0)
            {
                capturedRecipeGeneration = Interlocked.CompareExchange(
                    ref detectionRecipeGeneration, 0, 0);
            }
            EnsureDetectionRecipeGenerationIsCurrent(capturedRecipeGeneration, cancellationToken);

            objectDetectionFrequencyAnalysisRunning = true;
            if (!isCoordinatedRun && objectDetectionFrequencyRunButton != null)
            {
                objectDetectionFrequencyRunButton.Enabled = false;
            }
            if (!isCoordinatedRun && objectDetectionDefectCoreTabs != null)
            {
                objectDetectionDefectCoreTabs.Enabled = false;
            }
            if (!isCoordinatedRun && leftImageTabControl != null)
            {
                leftImageTabControl.SelectedTab = GetObjectDetectionDefectDisplayTabPage(
                    ObjectDetectionDefectFrequencyDisplayIndex);
                RefreshObjectDetectionDefectDisplay();
            }

            try
            {
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                SetObjectDetectionDefectRegionStatus("正在準備平場校正影像與 ROI... ");
                await Task.Yield();
                EnsureDetectionRecipeGenerationIsCurrent(capturedRecipeGeneration, cancellationToken);
                Stopwatch sourcePreparationStopwatch = Stopwatch.StartNew();
                LargeImageSource source = await PrepareObjectDetectionFrequencySourceAsync(parameter);
                sourcePreparationStopwatch.Stop();
                EnsureDetectionRecipeGenerationIsCurrent(capturedRecipeGeneration, cancellationToken);
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
                bool contrastEnabled = parameter.DefectFrequencyContrastEnabled;
                double contrastGain = Math.Max(0.1, Math.Min(5.0, parameter.DefectFrequencyContrastGain));
                int contrastPivotGray = Math.Max(1, Math.Min(255, parameter.FlatFieldTargetGray));
                int verticalStretchPercent =
                    IsSupportedObjectDetectionFrequencyVerticalStretchPercent(
                        parameter.DefectFrequencyVerticalStretchPercent)
                    ? parameter.DefectFrequencyVerticalStretchPercent
                    : 100;
                var undersizedObjects = objects
                    .Select(detectedObject => new
                    {
                        Object = detectedObject,
                        RegionSize = GetObjectDetectionFrequencyRegionSize(
                            detectedObject,
                            normalizedRegion)
                    })
                    .Where(item => item.RegionSize.Width < scanHeight ||
                        item.RegionSize.Height < scanHeight)
                    .ToList();
                if (undersizedObjects.Count > 0)
                {
                    string objectList = string.Join("、", undersizedObjects
                        .Take(5)
                        .Select(item => "ROI " + item.Object.Number.ToString(CultureInfo.CurrentCulture) +
                            " (" + item.RegionSize.Width.ToString(CultureInfo.CurrentCulture) + " × " +
                            item.RegionSize.Height.ToString(CultureInfo.CurrentCulture) + " px)"));
                    string message = objectList + " 的檢測範圍小於完整掃描格 " +
                        scanHeight.ToString(CultureInfo.CurrentCulture) + " × " +
                        scanHeight.ToString(CultureInfo.CurrentCulture) +
                        " px；本次未執行。請降低掃描高度或擴大檢測範圍。";
                    if (objectDetectionFrequencyStatusLabel != null)
                    {
                        objectDetectionFrequencyStatusLabel.Text = message;
                    }
                    if (statusLabel != null)
                    {
                        statusLabel.Text = parameter.DisplayName + "：" + message;
                    }
                    SetObjectDetectionDefectRegionStatus(message + " ");
                    return;
                }
                double sensitivity = Math.Max(1.0, Math.Min(10.0, parameter.DefectFrequencySensitivity));
                bool runParallel = parameter.DefectParallelExecutionEnabled;
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
                    if (!IsDisposed && !cancellationToken.IsCancellationRequested &&
                        capturedRecipeGeneration == Interlocked.CompareExchange(
                            ref detectionRecipeGeneration, 0, 0) &&
                        string.Equals(activeObjectDetectionParameterId,
                        parameter.Id, StringComparison.Ordinal))
                    {
                        SetObjectDetectionDefectRegionStatus(message);
                    }
                });

                Stopwatch scanStopwatch = Stopwatch.StartNew();
                SetObjectDetectionDefectRegionStatus(runParallel
                    ? "平行頻域掃描中：各物件 ROI 同時處理... "
                    : "頻域掃描中：各物件 ROI 依序處理... ");
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
                            verticalStretchPercent,
                            sensitivity,
                            contrastEnabled,
                            contrastGain,
                            contrastPivotGray,
                            runParallel,
                            progress,
                            cancellationToken);
                    }
                    finally
                    {
                        sourceReference.ReleaseReference();
                    }
                });
                scanStopwatch.Stop();
                totalStopwatch.Stop();
                if (result.Cells == null || result.Cells.Count == 0)
                {
                    DisposeObjectDetectionFrequencyResult(result);
                    SetObjectDetectionDefectRegionStatus(
                        "檢測範圍內找不到完整且有效的掃描格；請確認ROI位置，或降低掃描高度。 ");
                    return;
                }
                Stopwatch neighborComparisonStopwatch = Stopwatch.StartNew();
                CalculateObjectDetectionFrequencyNeighborComparison(
                    parameter,
                    result,
                    includeIndependentCoreDefects);
                neighborComparisonStopwatch.Stop();
                result.NeighborComparisonMilliseconds = neighborComparisonStopwatch.ElapsedMilliseconds;
                result.Signature = signature;
                result.ScanHeight = scanHeight;
                result.VerticalStretchPercent = verticalStretchPercent;
                result.Sensitivity = sensitivity;
                result.SourcePreparationMilliseconds = sourcePreparationStopwatch.ElapsedMilliseconds;
                result.ElapsedMilliseconds = scanStopwatch.ElapsedMilliseconds;
                result.TotalElapsedMilliseconds = totalStopwatch.ElapsedMilliseconds +
                    result.NeighborComparisonMilliseconds;

            if (IsDisposed || cancellationToken.IsCancellationRequested ||
                    capturedRecipeGeneration != Interlocked.CompareExchange(
                        ref detectionRecipeGeneration, 0, 0) ||
                    capturedImageGeneration != imageSourceGeneration ||
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
                    DisposeObjectDetectionFrequencyResult(result);
                    return;
                }

                RemoveObjectDetectionFrequencyResult(parameter.Id);
                objectDetectionFrequencyResults[parameter.Id] = result;
                InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
                RefreshObjectDetectionDefectIntegrationResults(parameter);
                InvalidateObjectDetectionDefectIntegrationDisplay();
                ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                    ObjectDetectionDefectFrequencyDisplayIndex);
                if (display != null)
                {
                    display.InvalidateImageView();
                }
                string summary = UpdateObjectDetectionFrequencyResultSummary(result);
                if (statusLabel != null)
                {
                    statusLabel.Text = string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}：頻域完成，掃描 {1:N0} 格，運算總耗時 {2:N0} ms",
                        parameter.DisplayName,
                        result.ScannedWindowCount,
                        result.TotalElapsedMilliseconds);
                }
                SetObjectDetectionDefectRegionStatus(summary +
                    (IsObjectDetectionDefectFrequencyIntegrationRequired(parameter)
                        ? "頻域異常已納入缺陷整合與結果判定。 "
                        : "目前未納入缺陷整合判定。 "));
            }
            catch (OutOfMemoryException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    SetObjectDetectionDefectRegionStatus("頻域掃描記憶體不足；請提高掃描高度以減少掃描區塊。 ");
                }
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    SetObjectDetectionDefectRegionStatus("頻域掃描失敗：" + exception.Message);
                }
            }
            finally
            {
                objectDetectionFrequencyAnalysisRunning = false;
                if (!IsDisposed)
                {
                    if (!isCoordinatedRun && objectDetectionFrequencyRunButton != null &&
                        !objectDetectionFrequencyRunButton.IsDisposed)
                    {
                        ObjectDetectionParameterSettings currentParameter =
                            FindObjectDetectionParameter(activeObjectDetectionParameterId);
                        objectDetectionFrequencyRunButton.Enabled =
                            currentParameter != null && currentParameter.DefectFrequencyEnabled;
                    }
                    if (!isCoordinatedRun && objectDetectionDefectCoreTabs != null &&
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
            int verticalStretchPercent,
            double sensitivity,
            bool contrastEnabled,
            double contrastGain,
            int contrastPivotGray,
            bool runParallel,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            var cellsByObjectIndex = new List<ObjectDetectionFrequencyCell>[objects.Count];
            var polygonsByObjectIndex = new PointF[objects.Count][];
            var processedPatchesByObjectIndex = new ObjectDetectionDefectProcessedPatch[objects.Count];
            long cellLoopElapsedTicks = 0;
            long previewGenerationElapsedTicks = 0;
            long scannedWindowCount = 0;
            Action<int> scanObject = delegate(int objectIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObjectDefinitionDetectedObject detectedObject = objects[objectIndex];
                ObjectDetectionMeasurementFrame frame =
                    CreateObjectDetectionMeasurementFrame(detectedObject);
                float localRegionLeft = (normalizedRegion.Left - 0.5f) * frame.Width;
                float localRegionTop = (normalizedRegion.Top - 0.5f) * frame.Height;
                int regionWidth = Math.Max(0, (int)Math.Floor(normalizedRegion.Width * frame.Width));
                int regionHeight = Math.Max(0, (int)Math.Floor(normalizedRegion.Height * frame.Height));
                PointF[] corners = CreateObjectDetectionDefectRegionImageCorners(
                    detectedObject,
                    normalizedRegion);
                var objectCells = new List<ObjectDetectionFrequencyCell>();
                cellsByObjectIndex[objectIndex] = objectCells;
                polygonsByObjectIndex[objectIndex] = corners;
                int left = (int)Math.Floor(corners.Min(point => point.X));
                int top = (int)Math.Floor(corners.Min(point => point.Y));
                int right = (int)Math.Ceiling(corners.Max(point => point.X));
                int bottom = (int)Math.Ceiling(corners.Max(point => point.Y));
                Rectangle crop = Rectangle.Intersect(
                    Rectangle.FromLTRB(left, top, right, bottom),
                    imageBounds);
                if (crop.Width < 1 || crop.Height < 1 || regionWidth < requestedWindowSize ||
                    regionHeight < requestedWindowSize)
                {
                    return;
                }

                using (Cv.Mat gray = CreateObjectDetectionDefectGrayRegionMat(source, crop))
                using (Cv.Mat adjustedGray = CreateObjectDetectionFrequencyContrastMat(
                    gray,
                    contrastEnabled,
                    contrastGain,
                    contrastPivotGray))
                using (var polygonMask = new Cv.Mat(
                    crop.Height,
                    crop.Width,
                    Cv.MatType.CV_8UC1,
                    Cv.Scalar.Black))
                using (var rectifiedGray = new Cv.Mat())
                using (var rectifiedMask = new Cv.Mat())
                using (Cv.Mat transform = CreateObjectDetectionFrequencyAffineTransform(
                    frame,
                    crop,
                    localRegionLeft,
                    localRegionTop))
                {
                    Cv.Mat analysisGray = adjustedGray ?? gray;
                    Stopwatch previewStopwatch = Stopwatch.StartNew();
                    Bitmap processedPreview = CreateObjectDetectionDefectPreviewBitmap(analysisGray);
                    previewStopwatch.Stop();
                    Interlocked.Add(
                        ref previewGenerationElapsedTicks,
                        previewStopwatch.ElapsedTicks);
                    processedPatchesByObjectIndex[objectIndex] = new ObjectDetectionDefectProcessedPatch
                    {
                        Bounds = crop,
                        InspectionPolygon = corners.ToArray(),
                        ProcessedImage = processedPreview
                    };
                    Cv.Point[] polygon = corners.Select(point => new Cv.Point(
                        (int)Math.Round(point.X - crop.X),
                        (int)Math.Round(point.Y - crop.Y))).ToArray();
                    Cv.Cv2.FillPoly(polygonMask, new[] { polygon }, Cv.Scalar.White);
                    Cv.Cv2.WarpAffine(
                        analysisGray,
                        rectifiedGray,
                        transform,
                        new Cv.Size(regionWidth, regionHeight),
                        Cv.InterpolationFlags.Nearest,
                        Cv.BorderTypes.Constant,
                        Cv.Scalar.Black);
                    Cv.Cv2.WarpAffine(
                        polygonMask,
                        rectifiedMask,
                        transform,
                        new Cv.Size(regionWidth, regionHeight),
                        Cv.InterpolationFlags.Nearest,
                        Cv.BorderTypes.Constant,
                        Cv.Scalar.Black);
                    int windowSize = requestedWindowSize;
                    int step = Math.Max(1, windowSize / 2);
                    List<int> xStarts = CreateFrequencyScanStarts(regionWidth, windowSize, step);
                    List<int> yStarts = CreateFrequencyScanStarts(regionHeight, windowSize, step);
                    using (var workspace = new ObjectDetectionFrequencyWorkspace(
                        rectifiedGray,
                        rectifiedMask,
                        windowSize,
                        verticalStretchPercent))
                    {
                        long objectScannedWindowCount = 0;
                        var usedWindows = new HashSet<long>();
                        Stopwatch cellLoopStopwatch = Stopwatch.StartNew();
                        for (int gridRow = 0; gridRow < yStarts.Count; gridRow++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int y = yStarts[gridRow];
                            for (int gridColumn = 0; gridColumn < xStarts.Count; gridColumn++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                int x = xStarts[gridColumn];
                                objectScannedWindowCount++;
                                int windowX;
                                int windowY;
                                double energy;
                                double meanGray;
                                double lowerQuartileBlockMean;
                                if (!TryFindObjectDetectionFrequencyWindow(
                                    workspace,
                                    x,
                                    y,
                                    windowSize,
                                    step,
                                    usedWindows,
                                    out windowX,
                                    out windowY,
                                    out energy,
                                    out meanGray,
                                    out lowerQuartileBlockMean))
                                {
                                    continue;
                                }

                                float localLeft = localRegionLeft + windowX;
                                float localTop = localRegionTop + windowY;
                                PointF[] cellCorners =
                                {
                                    frame.ToImage(localLeft, localTop),
                                    frame.ToImage(localLeft + windowSize, localTop),
                                    frame.ToImage(localLeft + windowSize, localTop + windowSize),
                                    frame.ToImage(localLeft, localTop + windowSize)
                                };
                                Rectangle cellBounds = GetObjectDetectionFrequencyBounds(cellCorners);
                                Rectangle validBounds = Rectangle.Intersect(cellBounds, imageBounds);
                                if (validBounds.Width <= 0 || validBounds.Height <= 0)
                                {
                                    continue;
                                }
                                objectCells.Add(new ObjectDetectionFrequencyCell
                                {
                                    ObjectNumber = detectedObject.Number,
                                    Bounds = cellBounds,
                                    ValidBounds = validBounds,
                                    ImageCorners = cellCorners,
                                    GridColumn = gridColumn,
                                    GridRow = gridRow,
                                    MeanGray = meanGray,
                                    LowerQuartileBlockMean = lowerQuartileBlockMean,
                                    Energy = energy
                                });
                            }
                        }
                        cellLoopStopwatch.Stop();
                        Interlocked.Add(ref cellLoopElapsedTicks, cellLoopStopwatch.ElapsedTicks);
                        Interlocked.Add(ref scannedWindowCount, objectScannedWindowCount);
                    }
                }
                progress?.Report(
                    "頻域掃描 ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
                    objects.Count.ToString(CultureInfo.CurrentCulture) + " 完成... ");
            };

            if (runParallel && objects.Count > 1)
            {
                try
                {
                    Parallel.For(
                        0,
                        objects.Count,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = objects.Count,
                            CancellationToken = cancellationToken
                        },
                        scanObject);
                }
                catch
                {
                    foreach (ObjectDetectionDefectProcessedPatch patch in processedPatchesByObjectIndex)
                    {
                        DisposeObjectDetectionDefectProcessedPatch(patch);
                    }
                    throw;
                }
            }
            else
            {
                try
                {
                    for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                    {
                        scanObject(objectIndex);
                    }
                }
                catch
                {
                    foreach (ObjectDetectionDefectProcessedPatch patch in processedPatchesByObjectIndex)
                    {
                        DisposeObjectDetectionDefectProcessedPatch(patch);
                    }
                    throw;
                }
            }

            var result = new ObjectDetectionFrequencyResult
            {
                Cells = new List<ObjectDetectionFrequencyCell>(),
                ObjectPolygons = new Dictionary<int, PointF[]>(),
                CellsByObject = new Dictionary<int, List<ObjectDetectionFrequencyCell>>(),
                ProcessedPatches = new List<ObjectDetectionDefectProcessedPatch>()
            };
            result.CellLoopMilliseconds = ConvertObjectDetectionFrequencyTicksToMilliseconds(
                cellLoopElapsedTicks);
            result.PreviewGenerationMilliseconds = ConvertObjectDetectionFrequencyTicksToMilliseconds(
                previewGenerationElapsedTicks);
            result.ScannedWindowCount = scannedWindowCount;
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                List<ObjectDetectionFrequencyCell> objectCells = cellsByObjectIndex[objectIndex];
                if (objectCells == null)
                {
                    continue;
                }
                int objectNumber = objects[objectIndex].Number;
                result.ObjectPolygons[objectNumber] = polygonsByObjectIndex[objectIndex];
                result.CellsByObject[objectNumber] = objectCells;
                result.Cells.AddRange(objectCells);
                if (processedPatchesByObjectIndex[objectIndex] != null)
                {
                    result.ProcessedPatches.Add(processedPatchesByObjectIndex[objectIndex]);
                    processedPatchesByObjectIndex[objectIndex] = null;
                }
            }

            if (result.Cells.Count == 0)
            {
                return result;
            }

            Stopwatch statisticsStopwatch = Stopwatch.StartNew();
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
            statisticsStopwatch.Stop();
            result.StatisticsMilliseconds = statisticsStopwatch.ElapsedMilliseconds;
            return result;
        }

        private void CalculateObjectDetectionFrequencyNeighborComparison(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionFrequencyResult result,
            bool includeIndependentCoreDefects)
        {
            if (parameter == null || result == null || result.Cells == null)
            {
                return;
            }

            result.NeighborSensitivity = Math.Max(1.0, Math.Min(
                10.0,
                parameter.DefectFrequencyNeighborSensitivity));
            result.NeighborReferenceRadius = Math.Max(2, Math.Min(
                20,
                parameter.DefectFrequencyNeighborReferenceRadius));
            result.NeighborReferenceCellCount = 0;
            result.NeighborEvaluatedCellCount = 0;
            foreach (ObjectDetectionFrequencyCell cell in result.Cells)
            {
                cell.NeighborReferenceGray = 0;
                cell.NeighborDeltaGray = 0;
                cell.NeighborReferenceLowerQuartile = 0;
                cell.NeighborDarkDeltaGray = 0;
                cell.NeighborScore = 0;
                cell.IsNeighborAnomaly = false;
                cell.IsHealthyReference = false;
                cell.HasIndependentCoreDefect = false;
            }
            if (!parameter.DefectFrequencyNeighborComparisonEnabled)
            {
                return;
            }

            if (includeIndependentCoreDefects)
            {
                EnsureObjectDetectionDefectCores(parameter);
                const int bucketSize = 512;
                var independentDefectsByObject =
                    new Dictionary<int, Dictionary<long, List<ObjectDetectionDefectContour>>>();
                foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores
                    .Take(ObjectDetectionDefectCoreKeys.Length)
                    .Where(item => item != null && item.Enabled))
                {
                    ObjectDetectionDefectCoreResult coreResult;
                    if (TryGetObjectDetectionDefectCoreResult(parameter, core.CoreKey, out coreResult) &&
                        coreResult.Contours != null)
                    {
                        foreach (ObjectDetectionDefectContour contour in coreResult.Contours)
                        {
                            if (contour == null || contour.ObjectNumber <= 0)
                            {
                                continue;
                            }
                            Rectangle bounds = Rectangle.Ceiling(contour.Bounds);
                            if (bounds.Width <= 0 || bounds.Height <= 0)
                            {
                                continue;
                            }
                            Dictionary<long, List<ObjectDetectionDefectContour>> objectBuckets;
                            if (!independentDefectsByObject.TryGetValue(
                                contour.ObjectNumber,
                                out objectBuckets))
                            {
                                objectBuckets = new Dictionary<long, List<ObjectDetectionDefectContour>>();
                                independentDefectsByObject.Add(contour.ObjectNumber, objectBuckets);
                            }
                            int firstBucketX = (int)Math.Floor(bounds.Left / (double)bucketSize);
                            int lastBucketX = (int)Math.Floor((bounds.Right - 1) / (double)bucketSize);
                            int firstBucketY = (int)Math.Floor(bounds.Top / (double)bucketSize);
                            int lastBucketY = (int)Math.Floor((bounds.Bottom - 1) / (double)bucketSize);
                            for (int bucketY = firstBucketY; bucketY <= lastBucketY; bucketY++)
                            {
                                for (int bucketX = firstBucketX; bucketX <= lastBucketX; bucketX++)
                                {
                                    long key = GetObjectDetectionFrequencySpatialBucketKey(bucketX, bucketY);
                                    List<ObjectDetectionDefectContour> bucket;
                                    if (!objectBuckets.TryGetValue(key, out bucket))
                                    {
                                        bucket = new List<ObjectDetectionDefectContour>();
                                        objectBuckets.Add(key, bucket);
                                    }
                                    bucket.Add(contour);
                                }
                            }
                        }
                    }
                }

                foreach (ObjectDetectionFrequencyCell cell in result.Cells)
                {
                    Dictionary<long, List<ObjectDetectionDefectContour>> objectBuckets;
                    if (!independentDefectsByObject.TryGetValue(cell.ObjectNumber, out objectBuckets))
                    {
                        continue;
                    }
                    int firstBucketX = (int)Math.Floor(cell.ValidBounds.Left / (double)bucketSize);
                    int lastBucketX = (int)Math.Floor((cell.ValidBounds.Right - 1) / (double)bucketSize);
                    int firstBucketY = (int)Math.Floor(cell.ValidBounds.Top / (double)bucketSize);
                    int lastBucketY = (int)Math.Floor((cell.ValidBounds.Bottom - 1) / (double)bucketSize);
                    var candidates = new HashSet<ObjectDetectionDefectContour>();
                    for (int bucketY = firstBucketY; bucketY <= lastBucketY; bucketY++)
                    {
                        for (int bucketX = firstBucketX; bucketX <= lastBucketX; bucketX++)
                        {
                            List<ObjectDetectionDefectContour> bucket;
                            if (objectBuckets.TryGetValue(
                                GetObjectDetectionFrequencySpatialBucketKey(bucketX, bucketY),
                                out bucket))
                            {
                                candidates.UnionWith(bucket);
                            }
                        }
                    }
                    cell.HasIndependentCoreDefect = candidates.Any(contour =>
                        cell.ValidBounds.IntersectsWith(Rectangle.Ceiling(contour.Bounds)));
                }
            }

            var usedReferenceCells = new HashSet<ObjectDetectionFrequencyCell>();
            foreach (IGrouping<int, ObjectDetectionFrequencyCell> objectCells in result.Cells
                .GroupBy(cell => cell.ObjectNumber))
            {
                var cellsByGrid = objectCells.ToDictionary(
                    cell => ((long)cell.GridRow << 32) | (uint)cell.GridColumn,
                    cell => cell);
                foreach (ObjectDetectionFrequencyCell cell in objectCells)
                {
                    var references = new List<ObjectDetectionFrequencyCell>();
                    for (int rowOffset = -result.NeighborReferenceRadius;
                        rowOffset <= result.NeighborReferenceRadius;
                        rowOffset++)
                    {
                        int row = cell.GridRow + rowOffset;
                        if (row < 0)
                        {
                            continue;
                        }
                        for (int columnOffset = -result.NeighborReferenceRadius;
                            columnOffset <= result.NeighborReferenceRadius;
                            columnOffset++)
                        {
                            if (Math.Abs(rowOffset) < 2 && Math.Abs(columnOffset) < 2)
                            {
                                continue;
                            }
                            int column = cell.GridColumn + columnOffset;
                            if (column < 0)
                            {
                                continue;
                            }
                            ObjectDetectionFrequencyCell reference;
                            long key = ((long)row << 32) | (uint)column;
                            if (!cellsByGrid.TryGetValue(key, out reference) ||
                                reference.IsAnomaly || reference.HasIndependentCoreDefect)
                            {
                                continue;
                            }
                            references.Add(reference);
                        }
                    }

                    if (references.Count < 4)
                    {
                        continue;
                    }
                    references = references
                        .OrderBy(reference => Math.Max(
                            Math.Abs(reference.GridColumn - cell.GridColumn),
                            Math.Abs(reference.GridRow - cell.GridRow)))
                        .Take(24)
                        .ToList();
                    if (references.Count < 4)
                    {
                        continue;
                    }

                    double referenceMean = CalculateObjectDetectionFrequencyMedian(
                        references.Select(reference => reference.MeanGray).ToList());
                    double meanScale = CalculateObjectDetectionFrequencyRobustScale(
                        references.Select(reference => reference.MeanGray).ToList(),
                        referenceMean);
                    double referenceLowerQuartile = CalculateObjectDetectionFrequencyMedian(
                        references.Select(reference => reference.LowerQuartileBlockMean).ToList());
                    double lowerQuartileScale = CalculateObjectDetectionFrequencyRobustScale(
                        references.Select(reference => reference.LowerQuartileBlockMean).ToList(),
                        referenceLowerQuartile);
                    cell.NeighborReferenceGray = referenceMean;
                    cell.NeighborDeltaGray = cell.MeanGray - referenceMean;
                    cell.NeighborReferenceLowerQuartile = referenceLowerQuartile;
                    cell.NeighborDarkDeltaGray = referenceLowerQuartile -
                        cell.LowerQuartileBlockMean;
                    double meanScore = Math.Abs(cell.NeighborDeltaGray) / meanScale;
                    double darkSubBlockScore = Math.Max(0, cell.NeighborDarkDeltaGray) /
                        lowerQuartileScale;
                    cell.NeighborScore = Math.Max(meanScore, darkSubBlockScore);
                    cell.IsNeighborAnomaly = cell.NeighborScore >= result.NeighborSensitivity;
                    result.NeighborEvaluatedCellCount++;
                    foreach (ObjectDetectionFrequencyCell reference in references)
                    {
                        reference.IsHealthyReference = true;
                        usedReferenceCells.Add(reference);
                    }
                }
            }
            result.NeighborReferenceCellCount = usedReferenceCells.Count;
        }

        private static long GetObjectDetectionFrequencySpatialBucketKey(int x, int y)
        {
            return ((long)y << 32) | (uint)x;
        }

        private static double CalculateObjectDetectionFrequencyRobustScale(
            List<double> values,
            double median)
        {
            double medianAbsoluteDeviation = CalculateObjectDetectionFrequencyMedian(
                values.Select(value => Math.Abs(value - median)).ToList());
            return Math.Max(2.0, medianAbsoluteDeviation * 1.4826);
        }

        private static Cv.Mat CreateObjectDetectionFrequencyContrastMat(
            Cv.Mat source,
            bool enabled,
            double gain,
            int pivotGray)
        {
            if (!enabled)
            {
                return null;
            }

            var adjusted = new Cv.Mat();
            try
            {
                double safeGain = double.IsNaN(gain) || double.IsInfinity(gain)
                    ? 1.0
                    : Math.Max(0.1, Math.Min(5.0, gain));
                int safePivot = Math.Max(1, Math.Min(255, pivotGray));
                double beta = safePivot * (1.0 - safeGain);
                source.ConvertTo(adjusted, Cv.MatType.CV_8UC1, safeGain, beta);
                return adjusted;
            }
            catch
            {
                adjusted.Dispose();
                throw;
            }
        }

        private static void DisposeObjectDetectionFrequencyResult(
            ObjectDetectionFrequencyResult result)
        {
            if (result == null || result.ProcessedPatches == null)
            {
                return;
            }
            foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
            {
                DisposeObjectDetectionDefectProcessedPatch(patch);
            }
            result.ProcessedPatches.Clear();
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

        private static Size GetObjectDetectionFrequencyRegionSize(
            ObjectDefinitionDetectedObject detectedObject,
            RectangleF normalizedRegion)
        {
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(detectedObject);
            return new Size(
                Math.Max(0, (int)Math.Floor(normalizedRegion.Width * frame.Width)),
                Math.Max(0, (int)Math.Floor(normalizedRegion.Height * frame.Height)));
        }

        private static Cv.Mat CreateObjectDetectionFrequencyAffineTransform(
            ObjectDetectionMeasurementFrame frame,
            Rectangle crop,
            float localRegionLeft,
            float localRegionTop)
        {
            PointF origin = frame.ToLocal(crop.X + 0.5f, crop.Y + 0.5f);
            PointF xStep = frame.ToLocal(crop.X + 1.5f, crop.Y + 0.5f);
            PointF yStep = frame.ToLocal(crop.X + 0.5f, crop.Y + 1.5f);
            Cv.Point2f[] sourcePoints =
            {
                new Cv.Point2f(0, 0),
                new Cv.Point2f(1, 0),
                new Cv.Point2f(0, 1)
            };
            Cv.Point2f[] destinationPoints =
            {
                ToDestinationPoint(origin, localRegionLeft, localRegionTop),
                ToDestinationPoint(xStep, localRegionLeft, localRegionTop),
                ToDestinationPoint(yStep, localRegionLeft, localRegionTop)
            };
            return Cv.Cv2.GetAffineTransform(sourcePoints, destinationPoints);
        }

        private static Cv.Point2f ToDestinationPoint(
            PointF localPoint,
            float localRegionLeft,
            float localRegionTop)
        {
            return new Cv.Point2f(
                localPoint.X - localRegionLeft - 0.5f,
                localPoint.Y - localRegionTop - 0.5f);
        }

        private static Rectangle GetObjectDetectionFrequencyBounds(PointF[] corners)
        {
            if (corners == null || corners.Length == 0)
            {
                return Rectangle.Empty;
            }
            return Rectangle.FromLTRB(
                (int)Math.Floor(corners.Min(point => point.X)),
                (int)Math.Floor(corners.Min(point => point.Y)),
                (int)Math.Ceiling(corners.Max(point => point.X)),
                (int)Math.Ceiling(corners.Max(point => point.Y)));
        }

        private static bool TryFindObjectDetectionFrequencyWindow(
            ObjectDetectionFrequencyWorkspace workspace,
            int preferredX,
            int preferredY,
            int windowSize,
            int searchRadius,
            HashSet<long> usedWindows,
            out int windowX,
            out int windowY,
            out double energy,
            out double meanGray,
            out double lowerQuartileBlockMean)
        {
            windowX = 0;
            windowY = 0;
            energy = 0.0;
            meanGray = 0.0;
            lowerQuartileBlockMean = 0.0;
            if (workspace == null || usedWindows == null)
            {
                return false;
            }

            int maxX = workspace.ImageWidth - windowSize;
            int maxY = workspace.ImageHeight - windowSize;
            int centerX = maxX / 2;
            int centerY = maxY / 2;
            int directionX = Math.Sign(centerX - preferredX);
            int directionY = Math.Sign(centerY - preferredY);
            for (int radius = 0; radius <= Math.Max(0, searchRadius); radius++)
            {
                var candidates = new List<Point>(3);
                if (radius == 0)
                {
                    candidates.Add(new Point(preferredX, preferredY));
                }
                else
                {
                    int inwardX = preferredX + (directionX * radius);
                    int inwardY = preferredY + (directionY * radius);
                    candidates.Add(new Point(inwardX, preferredY));
                    candidates.Add(new Point(preferredX, inwardY));
                    candidates.Add(new Point(inwardX, inwardY));
                }

                foreach (Point candidate in candidates)
                {
                    if (candidate.X < 0 || candidate.Y < 0 || candidate.X > maxX ||
                        candidate.Y > maxY)
                    {
                        continue;
                    }
                    long key = ((long)candidate.Y << 32) | (uint)candidate.X;
                    if (usedWindows.Contains(key))
                    {
                        continue;
                    }
                    if (!TryCalculateObjectDetectionFrequencyEnergy(
                        workspace,
                        candidate.X,
                        candidate.Y,
                        windowSize,
                        out energy,
                        out meanGray,
                        out lowerQuartileBlockMean))
                    {
                        continue;
                    }

                    usedWindows.Add(key);
                    windowX = candidate.X;
                    windowY = candidate.Y;
                    return true;
                }
            }
            return false;
        }

        private static unsafe bool TryCalculateObjectDetectionFrequencyEnergy(
            ObjectDetectionFrequencyWorkspace workspace,
            int startX,
            int startY,
            int windowSize,
            out double energy,
            out double meanGray,
            out double lowerQuartileBlockMean)
        {
            energy = 0.0;
            meanGray = 0.0;
            lowerQuartileBlockMean = 0.0;
            if (workspace == null || windowSize <= 0 || startX < 0 || startY < 0 ||
                windowSize != workspace.WindowWidth ||
                startX > workspace.ImageWidth - windowSize ||
                startY > workspace.ImageHeight - windowSize)
            {
                return false;
            }

            byte* image = (byte*)workspace.ImageData.ToPointer();
            byte* mask = (byte*)workspace.MaskData.ToPointer();
            byte* hannBytes = (byte*)workspace.HannData.ToPointer();
            for (int row = 0; row < windowSize; row++)
            {
                byte* imageRow = image + (long)(startY + row) * workspace.ImageStep + startX;
                byte* maskRow = mask + (long)(startY + row) * workspace.MaskStep + startX;
                for (int column = 0; column < windowSize; column++)
                {
                    if (maskRow[column] == 0)
                    {
                        return false;
                    }
                }
            }

            Cv.Mat sourceWindow = null;
            try
            {
                byte* energyImage = image + (long)startY * workspace.ImageStep + startX;
                long energyImageStep = workspace.ImageStep;
                int energyWidth = windowSize;
                int energyHeight = windowSize;
                int validPixels = windowSize * windowSize;
                if (workspace.VerticalStretchPercent != 100)
                {
                    sourceWindow = new Cv.Mat(
                        workspace.Image,
                        new Cv.Rect(startX, startY, windowSize, windowSize));
                    Cv.Cv2.Resize(
                        sourceWindow,
                        workspace.ResizedWindow,
                        new Cv.Size(workspace.WindowWidth, workspace.WindowHeight),
                        0,
                        0,
                        workspace.VerticalStretchPercent < 100
                            ? Cv.InterpolationFlags.Area
                            : Cv.InterpolationFlags.Linear);
                    energyImage = (byte*)workspace.ResizedWindow.Data.ToPointer();
                    energyImageStep = workspace.ResizedWindow.Step();
                    energyWidth = workspace.WindowWidth;
                    energyHeight = workspace.WindowHeight;
                    validPixels = energyWidth * energyHeight;
                }

                int blockColumns = Math.Min(4, energyWidth);
                int blockRows = Math.Min(4, energyHeight);
                var blockSums = new double[blockColumns * blockRows];
                var blockCounts = new int[blockSums.Length];
                double pixelSum = 0.0;
                for (int row = 0; row < energyHeight; row++)
                {
                    byte* imageRow = energyImage + (long)row * energyImageStep;
                    int blockRow = Math.Min(blockRows - 1, row * blockRows / energyHeight);
                    for (int column = 0; column < energyWidth; column++)
                    {
                        int blockColumn = Math.Min(blockColumns - 1, column * blockColumns / energyWidth);
                        int blockIndex = blockRow * blockColumns + blockColumn;
                        byte value = imageRow[column];
                        pixelSum += value;
                        blockSums[blockIndex] += value;
                        blockCounts[blockIndex]++;
                    }
                }

                float mean = (float)(pixelSum / validPixels);
                meanGray = mean;
                var blockMeans = new List<double>(blockSums.Length);
                for (int index = 0; index < blockSums.Length; index++)
                {
                    if (blockCounts[index] > 0)
                    {
                        blockMeans.Add(blockSums[index] / blockCounts[index]);
                    }
                }
                blockMeans.Sort();
                int lowerQuartileCount = Math.Max(1, (int)Math.Ceiling(blockMeans.Count * 0.25));
                lowerQuartileBlockMean = blockMeans.Take(lowerQuartileCount).Average();
                double squaredEnergySum = 0.0;
                for (int row = 0; row < energyHeight; row++)
                {
                    byte* imageRow = energyImage + (long)row * energyImageStep;
                    float* hannRow = (float*)(hannBytes + (long)row * workspace.HannStep);
                    for (int column = 0; column < energyWidth; column++)
                    {
                        float centered = imageRow[column] - mean;
                        float windowed = centered * hannRow[column];
                        squaredEnergySum += windowed * windowed;
                    }
                }

                energy = squaredEnergySum / validPixels;
                return true;
            }
            finally
            {
                if (sourceWindow != null)
                {
                    sourceWindow.Dispose();
                }
            }
        }

        private static long ConvertObjectDetectionFrequencyTicksToMilliseconds(long ticks)
        {
            return ticks <= 0
                ? 0
                : (long)(ticks * 1000.0 / Stopwatch.Frequency);
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

        private string UpdateObjectDetectionFrequencyResultSummary(
            ObjectDetectionFrequencyResult result)
        {
            if (result == null || objectDetectionFrequencyStatusLabel == null)
            {
                return string.Empty;
            }
            int anomalyCount = result.Cells == null
                ? 0
                : result.Cells.Count(cell => cell.IsAnomaly);
            int neighborAnomalyCount = result.Cells == null
                ? 0
                : result.Cells.Count(cell => cell.IsNeighborAnomaly);
            string summary = string.Format(
                CultureInfo.CurrentCulture,
                "ROI {0}；掃描 {1:N0} 格（有效 {2:N0}），能量可疑 {3:N0}、鄰格可疑 {11:N0}；分析格垂直 {10}\r\n運算總耗時 {4:N0} ms；來源準備 {5:N0} ms，掃描 {6:N0} ms\r\nROI 格迴圈累計 {7:N0} ms；預覽累計 {8:N0} ms；統計 {9:N0} ms；鄰格比較 {12:N0} ms",
                result.CellsByObject == null ? 0 : result.CellsByObject.Count,
                result.ScannedWindowCount,
                result.Cells == null ? 0 : result.Cells.Count,
                anomalyCount,
                result.TotalElapsedMilliseconds,
                result.SourcePreparationMilliseconds,
                result.ElapsedMilliseconds,
                result.CellLoopMilliseconds,
                result.PreviewGenerationMilliseconds,
                result.StatisticsMilliseconds,
                FormatObjectDetectionFrequencyVerticalStretch(result.VerticalStretchPercent),
                neighborAnomalyCount,
                result.NeighborComparisonMilliseconds);
            objectDetectionFrequencyStatusLabel.Text = summary;
            return summary;
        }

        private string CreateObjectDetectionFrequencySignature(
            ObjectDetectionParameterSettings parameter,
            string definitionSignature,
            int imageGeneration,
            int flatFieldGeneration)
        {
            string neighborCoreSignatures = string.Empty;
            if (parameter.DefectFrequencyNeighborComparisonEnabled)
            {
                EnsureObjectDetectionDefectCores(parameter);
                neighborCoreSignatures = string.Join(";", parameter.DefectDetectionCores
                    .Take(ObjectDetectionDefectCoreKeys.Length)
                    .Where(core => core != null && core.Enabled)
                    .Select(core => CreateObjectDetectionDefectCoreSignature(
                        parameter,
                        core,
                        definitionSignature)));
            }
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
                parameter.DefectFrequencyVerticalStretchPercent.ToString(CultureInfo.InvariantCulture),
                parameter.DefectFrequencySensitivity.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectFrequencyNeighborComparisonEnabled ? "1" : "0",
                parameter.DefectFrequencyNeighborReferenceRadius.ToString(CultureInfo.InvariantCulture),
                parameter.DefectFrequencyNeighborSensitivity.ToString("R", CultureInfo.InvariantCulture),
                neighborCoreSignatures,
                parameter.DefectFrequencyContrastEnabled ? "1" : "0",
                parameter.DefectFrequencyContrastGain.ToString("R", CultureInfo.InvariantCulture),
                parameter.FlatFieldTargetGray.ToString(CultureInfo.InvariantCulture),
                parameter.FlatFieldSavedSettingsSignature ?? string.Empty,
                CreateObjectDetectionFlatFieldSettingsSignature(parameter)
            });
        }

        private bool TryGetCurrentObjectDetectionFrequencyResult(
            ObjectDetectionParameterSettings parameter,
            out ObjectDetectionFrequencyResult result)
        {
            result = null;
            if (IsActiveObjectDetectionResultReviewSnapshot(parameter))
            {
                result = objectDetectionResultReviewSnapshot.FrequencyResult;
                return result != null && parameter.DefectFrequencyEnabled;
            }
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
                RemoveObjectDetectionFrequencyResult(parameter.Id);
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
                RemoveObjectDetectionFrequencyResult(parameter.Id);
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
            ObjectDetectionParameterSettings parameter = GetObjectDetectionParameterForDisplay();
            ObjectDetectionFrequencyResult result;
            if (!TryGetCurrentObjectDetectionFrequencyResult(parameter, out result) ||
                (result.Cells == null || result.Cells.Count == 0) &&
                (result.ProcessedPatches == null || result.ProcessedPatches.Count == 0))
            {
                return;
            }

            RectangleF visibleBounds = visibleSourceRect;
            if (result.ProcessedPatches != null)
            {
                foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
                {
                    if (patch == null || patch.ProcessedImage == null ||
                        !patch.Bounds.IntersectsWith(Rectangle.Ceiling(visibleBounds)))
                    {
                        continue;
                    }
                    GraphicsState patchState = graphics.Save();
                    try
                    {
                        if (patch.InspectionPolygon != null && patch.InspectionPolygon.Length >= 3)
                        {
                            PointF[] screenPolygon = patch.InspectionPolygon.Select(point => new PointF(
                                offset.X + point.X * zoom,
                                offset.Y + point.Y * zoom)).ToArray();
                            using (var clip = new GraphicsPath())
                            {
                                clip.AddPolygon(screenPolygon);
                                graphics.SetClip(clip, CombineMode.Intersect);
                            }
                        }
                        var destination = new RectangleF(
                            offset.X + patch.Bounds.X * zoom,
                            offset.Y + patch.Bounds.Y * zoom,
                            patch.Bounds.Width * zoom,
                            patch.Bounds.Height * zoom);
                        graphics.DrawImage(patch.ProcessedImage, destination);
                    }
                    finally
                    {
                        graphics.Restore(patchState);
                    }
                }
            }

            bool showHeatmap = parameter.DefectFrequencyShowHeatmap;
            bool showBoxes = parameter.DefectFrequencyShowAnomalyBoxes;
            bool showNeighborHeatmap = parameter.DefectFrequencyShowNeighborHeatmap;
            bool showNeighborBoxes = parameter.DefectFrequencyShowNeighborAnomalyBoxes;
            bool showHealthyReferenceCells = parameter.DefectFrequencyShowHealthyReferenceCells;
            int[] heatmapAlpha = { 16, 24, 32, 40, 48 };
            int[] neighborHeatmapAlpha = { 18, 28, 40, 54, 70 };
            var highEnergyBrushes = new SolidBrush[heatmapAlpha.Length];
            var lowEnergyBrushes = new SolidBrush[heatmapAlpha.Length];
            for (int index = 0; index < heatmapAlpha.Length; index++)
            {
                highEnergyBrushes[index] = new SolidBrush(
                    Color.FromArgb(heatmapAlpha[index], 255, 190, 0));
                lowEnergyBrushes[index] = new SolidBrush(
                    Color.FromArgb(heatmapAlpha[index], 50, 145, 255));
            }
            var darkNeighborBrushes = new SolidBrush[neighborHeatmapAlpha.Length];
            var brightNeighborBrushes = new SolidBrush[neighborHeatmapAlpha.Length];
            for (int index = 0; index < neighborHeatmapAlpha.Length; index++)
            {
                darkNeighborBrushes[index] = new SolidBrush(
                    Color.FromArgb(neighborHeatmapAlpha[index], 235, 55, 145));
                brightNeighborBrushes[index] = new SolidBrush(
                    Color.FromArgb(neighborHeatmapAlpha[index], 255, 155, 35));
            }
            using (var anomalyOutline = new Pen(Color.OrangeRed, Math.Max(1.0f, Math.Min(3.0f, zoom * 1.5f))))
            using (var darkNeighborOutline = new Pen(Color.DeepPink, Math.Max(1.2f, Math.Min(3.5f, zoom * 1.7f))))
            using (var brightNeighborOutline = new Pen(Color.Gold, Math.Max(1.2f, Math.Min(3.5f, zoom * 1.7f))))
            using (var referenceOutline = new Pen(Color.FromArgb(150, 0, 115, 125), Math.Max(1.0f, Math.Min(2.0f, zoom))))
            {
                referenceOutline.DashStyle = DashStyle.Dash;
                try
                {
                    foreach (KeyValuePair<int, List<ObjectDetectionFrequencyCell>> objectEntry in
                        result.CellsByObject)
                    {
                        PointF[] imagePolygon;
                        if (result.ObjectPolygons == null ||
                            !result.ObjectPolygons.TryGetValue(objectEntry.Key, out imagePolygon) ||
                            imagePolygon == null || imagePolygon.Length < 3)
                        {
                            continue;
                        }

                        PointF[] screenPolygon = imagePolygon.Select(point => new PointF(
                            offset.X + point.X * zoom,
                            offset.Y + point.Y * zoom)).ToArray();
                        using (var clipPath = new GraphicsPath())
                        {
                            clipPath.AddPolygon(screenPolygon);
                            GraphicsState state = graphics.Save();
                            try
                            {
                                graphics.SetClip(clipPath, CombineMode.Intersect);
                                foreach (ObjectDetectionFrequencyCell cell in objectEntry.Value)
                                {
                                    if (!cell.Bounds.IntersectsWith(Rectangle.Ceiling(visibleBounds)))
                                    {
                                        continue;
                                    }
                                    PointF[] cellPolygon = cell.ImageCorners == null ||
                                        cell.ImageCorners.Length < 3
                                        ? new[]
                                        {
                                            new PointF(cell.Bounds.Left, cell.Bounds.Top),
                                            new PointF(cell.Bounds.Right, cell.Bounds.Top),
                                            new PointF(cell.Bounds.Right, cell.Bounds.Bottom),
                                            new PointF(cell.Bounds.Left, cell.Bounds.Bottom)
                                        }
                                        : cell.ImageCorners;
                                    PointF[] screenCellPolygon = cellPolygon.Select(point => new PointF(
                                        offset.X + point.X * zoom,
                                        offset.Y + point.Y * zoom)).ToArray();
                                    if (showHeatmap)
                                    {
                                        int level = (int)Math.Floor(
                                            cell.Score / Math.Max(1.0, result.Sensitivity) * heatmapAlpha.Length);
                                        level = Math.Max(0, Math.Min(heatmapAlpha.Length - 1, level));
                                        SolidBrush fill = cell.Energy >= result.MedianEnergy
                                            ? highEnergyBrushes[level]
                                            : lowEnergyBrushes[level];
                                        graphics.FillPolygon(fill, screenCellPolygon);
                                    }
                                    if (showNeighborHeatmap && cell.NeighborScore >= 1.0)
                                    {
                                        int level = (int)Math.Floor(
                                            cell.NeighborScore / Math.Max(1.0, result.NeighborSensitivity) *
                                            neighborHeatmapAlpha.Length);
                                        level = Math.Max(0, Math.Min(neighborHeatmapAlpha.Length - 1, level));
                                        bool darkDeviation = cell.NeighborDeltaGray < 0 ||
                                            cell.NeighborDarkDeltaGray > 0;
                                        graphics.FillPolygon(
                                            darkDeviation ? darkNeighborBrushes[level] : brightNeighborBrushes[level],
                                            screenCellPolygon);
                                    }
                                    if (showHealthyReferenceCells && cell.IsHealthyReference)
                                    {
                                        graphics.DrawPolygon(referenceOutline, screenCellPolygon);
                                    }
                                    if (showBoxes && cell.IsAnomaly)
                                    {
                                        graphics.DrawPolygon(anomalyOutline, screenCellPolygon);
                                    }
                                    if (showNeighborBoxes && cell.IsNeighborAnomaly)
                                    {
                                        graphics.DrawPolygon(
                                            cell.NeighborDeltaGray < 0 || cell.NeighborDarkDeltaGray > 0
                                                ? darkNeighborOutline
                                                : brightNeighborOutline,
                                            screenCellPolygon);
                                    }
                                }
                            }
                            finally
                            {
                                graphics.Restore(state);
                            }
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
                    foreach (SolidBrush brush in darkNeighborBrushes)
                    {
                        brush.Dispose();
                    }
                    foreach (SolidBrush brush in brightNeighborBrushes)
                    {
                        brush.Dispose();
                    }
                }
            }
        }
    }
}
