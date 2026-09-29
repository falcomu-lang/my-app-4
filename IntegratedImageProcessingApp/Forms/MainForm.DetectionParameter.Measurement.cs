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
        private TabPage objectDetectionMeasurementTabPage;
        private Panel objectDetectionMeasurementDisplayHostPanel;
        private ImageDisplayControl objectDetectionMeasurementDisplayControl;

        private ComboBox objectDetectionMeasurementModeComboBox;
        private ComboBox objectDetectionMeasurementDirectionComboBox;
        private ComboBox objectDetectionMeasurementLengthModeComboBox;
        private NumericUpDown objectDetectionMeasurementLineCountBox;
        private TextBox objectDetectionMeasurementNameTextBox;
        private Label objectDetectionMeasurementToolStatusLabel;
        private Button objectDetectionMeasurementDrawButton;
        private DataGridView objectDetectionMeasurementRecordsGrid;
        private CheckBox objectDetectionMeasurementClipLinesCheckBox;
        private string objectDetectionMeasurementAppliedRecordId;
        private string objectDetectionMeasurementClipCacheParameterId;
        private string objectDetectionMeasurementClipCacheRecordId;
        private string objectDetectionMeasurementClipCacheSignature;
        private int objectDetectionMeasurementClipCacheObjectNumber = -1;
        private int objectDetectionMeasurementClipCacheGeneration = -1;
        private Rectangle objectDetectionMeasurementClipCacheObjectBounds;
        private List<ObjectDetectionImageLine> objectDetectionMeasurementClippedLines;
        private List<double> objectDetectionMeasurementClippedLineLengths;
        private List<List<ObjectDetectionImageLine>> objectDetectionMeasurementClippedLineSegments;
        private string objectDetectionMeasurementRenderOverlayCacheKey;
        private System.Drawing.Drawing2D.GraphicsPath objectDetectionMeasurementRenderPrimaryPath;
        private System.Drawing.Drawing2D.GraphicsPath objectDetectionMeasurementRenderGuidePath;
        private System.Drawing.Drawing2D.GraphicsPath objectDetectionMeasurementRenderEndpointPath;
        private bool objectDetectionMeasurementCreateNewRecord;
        private bool objectDetectionMeasurementIsDrawing;
        private int objectDetectionMeasurementDrawingStage;
        private Point objectDetectionMeasurementDrawStart;
        private Point objectDetectionMeasurementDrawCurrent;
        private Timer objectDetectionMeasurementHighlightTimer;
        private bool objectDetectionMeasurementHighlightVisible;
        private Timer objectDetectionMeasurementResultHighlightTimer;
        private bool objectDetectionMeasurementResultHighlightsVisible;
        private string objectDetectionMeasurementResultParameterId;
        private int objectDetectionMeasurementResultObjectNumber = -1;
        private Rectangle objectDetectionMeasurementResultObjectBounds;
        private ObjectDetectionImageLine objectDetectionMeasurementMinimumResultLine;
        private ObjectDetectionImageLine objectDetectionMeasurementMaximumResultLine;
        private readonly ObjectDetectionMeasurementGeometry pendingObjectDetectionMeasurementGeometry =
            new ObjectDetectionMeasurementGeometry();

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
            objectDetectionMeasurementHighlightTimer = new Timer
            {
                Interval = 180
            };
            objectDetectionMeasurementHighlightTimer.Tick += delegate
            {
                objectDetectionMeasurementHighlightVisible = false;
                objectDetectionMeasurementHighlightTimer.Stop();
                if (objectDetectionMeasurementDisplayControl != null)
                {
                    objectDetectionMeasurementDisplayControl.InvalidateImageView();
                }
            };
            if (components != null)
            {
                components.Add(objectDetectionMeasurementHighlightTimer);
            }

            objectDetectionMeasurementResultHighlightTimer = new Timer
            {
                Interval = 10000
            };
            objectDetectionMeasurementResultHighlightTimer.Tick += delegate
            {
                objectDetectionMeasurementResultHighlightsVisible = false;
                objectDetectionMeasurementResultHighlightTimer.Stop();
                if (objectDetectionMeasurementDisplayControl != null)
                {
                    objectDetectionMeasurementDisplayControl.InvalidateImageView();
                }
            };
            if (components != null)
            {
                components.Add(objectDetectionMeasurementResultHighlightTimer);
            }
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

        private static string NormalizeObjectDetectionMeasurementLengthMode(string mode)
        {
            return string.Equals(mode, "IgnoreGaps", StringComparison.Ordinal)
                ? "IgnoreGaps"
                : "FirstContinuous";
        }

        private static string GetObjectDetectionMeasurementLengthModeText(string mode)
        {
            return string.Equals(
                NormalizeObjectDetectionMeasurementLengthMode(mode),
                "IgnoreGaps",
                StringComparison.Ordinal)
                ? "無視中間斷線的量測長度"
                : "第一個碰到的連續量測長度";
        }

        private string GetObjectDetectionMeasurementLengthMode()
        {
            string selected = objectDetectionMeasurementLengthModeComboBox == null
                ? null
                : objectDetectionMeasurementLengthModeComboBox.SelectedItem as string;
            return selected == "無視中間斷線的量測長度"
                ? "IgnoreGaps"
                : "FirstContinuous";
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

        private static int GetNextObjectDetectionMeasurementNumber(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || parameter.MeasurementRecords == null)
            {
                return 1;
            }

            int maximum = parameter.MeasurementRecords
                .Where(record => record != null && record.Number > 0)
                .Select(record => record.Number)
                .DefaultIfEmpty(0)
                .Max();
            return maximum == int.MaxValue ? int.MaxValue : maximum + 1;
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
                objectDetectionMeasurementLengthModeComboBox = null;
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
                    Height = 276,
                    Text = "量測工具",
                    Padding = new Padding(8)
                };
                var measurementToolLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 8,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                measurementToolLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
                measurementToolLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
                for (int row = 0; row < 8; row++)
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

                objectDetectionMeasurementLengthModeComboBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                objectDetectionMeasurementLengthModeComboBox.Items.Add(
                    "第一個碰到的連續量測長度");
                objectDetectionMeasurementLengthModeComboBox.Items.Add(
                    "無視中間斷線的量測長度");
                objectDetectionMeasurementLengthModeComboBox.SelectedItem =
                    GetObjectDetectionMeasurementLengthModeText(
                        parameter.MeasurementLengthMode);

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
                measurementToolLayout.Controls.Add(new Label
                {
                    Text = "量測邏輯",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 4);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementLengthModeComboBox, 1, 4);

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
                    objectDetectionMeasurementAppliedRecordId = null;
                    ClearObjectDetectionMeasurementClipCache();
                    objectDetectionMeasurementCreateNewRecord = true;
                    objectDetectionMeasurementIsDrawing = false;
                    objectDetectionMeasurementDrawingStage = 0;
                    objectDetectionMeasurementDrawButton.Text = "開始畫線";
                    objectDetectionMeasurementClipLinesCheckBox.Enabled = false;
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
                }, 0, 5);
                measurementToolLayout.Controls.Add(buttonPanel, 1, 5);

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
                }, 0, 7);
                measurementToolLayout.Controls.Add(objectDetectionMeasurementToolStatusLabel, 1, 7);

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
                    parameter.MeasurementLengthMode =
                        GetObjectDetectionMeasurementLengthMode();
                    parameter.MeasurementName = GetObjectDetectionMeasurementName(parameter);
                    UpsertObjectDetectionMeasurementRecord(
                        parameter,
                        parameter.MeasurementName,
                        mode,
                        GetObjectDetectionMeasurementDirection(),
                        lineCount);
                    ObjectDetectionMeasurementRecordSettings appliedRecord =
                        GetActiveObjectDetectionMeasurementRecord(parameter);
                    if (objectDetectionMeasurementClipLinesCheckBox != null)
                    {
                        objectDetectionMeasurementClipLinesCheckBox.Enabled = appliedRecord != null;
                    }
                    if (parameter.MeasurementClipLinesToMask && appliedRecord != null)
                    {
                        PrepareObjectDetectionMeasurementClipLines(parameter, appliedRecord);
                    }
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
                measurementToolLayout.Controls.Add(measurementApplyCancelPanel, 1, 6);
                measurementToolGroup.Controls.Add(measurementToolLayout);

                var measurementRecordsGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 224,
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
                        Name = "MeasurementNumber",
                        HeaderText = "編號",
                        FillWeight = 10
                    });
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
                        Name = "MeasurementLengthMode",
                        HeaderText = "量測邏輯",
                        FillWeight = 32
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
                objectDetectionMeasurementRecordsGrid.CellMouseDown += delegate(
                    object sender,
                    DataGridViewCellMouseEventArgs e)
                {
                    if (e.Button != MouseButtons.Right || e.RowIndex < 0 ||
                        parameter.MeasurementRecords == null)
                    {
                        return;
                    }

                    objectDetectionMeasurementRecordsGrid.ClearSelection();
                    objectDetectionMeasurementRecordsGrid.Rows[e.RowIndex].Selected = true;
                    object idValue = objectDetectionMeasurementRecordsGrid.Rows[e.RowIndex]
                        .Cells["MeasurementId"]
                        .Value;
                    string recordId = Convert.ToString(idValue, CultureInfo.InvariantCulture);
                     if (string.IsNullOrWhiteSpace(recordId))
                     {
                         return;
                     }

                     var menu = new ContextMenuStrip();
                     menu.Items.Add(
                         "編輯",
                         null,
                         delegate
                         {
                             RenameObjectDetectionMeasurementRecord(parameter, recordId);
                         });
                     menu.Items.Add(new ToolStripSeparator());
                     menu.Items.Add(
                         "運算",
                         null,
                         delegate
                         {
                             CalculateObjectDetectionMeasurementRecord(parameter, recordId);
                         });
                     menu.Items.Add(new ToolStripSeparator());
                     menu.Items.Add(
                         "刪除",
                         null,
                         delegate
                         {
                             DeleteObjectDetectionMeasurementRecord(parameter, recordId);
                         });
                     Point menuLocation = Cursor.Position;
                     menuLocation.X += 8;
                     menuLocation.Y += 8;
                     menu.Show(menuLocation);
                 };
                 objectDetectionMeasurementRecordsGrid.CellClick += delegate(
                     object sender,
                     DataGridViewCellEventArgs e)
                 {
                     if (e.RowIndex < 0)
                     {
                         return;
                     }

                     LoadObjectDetectionMeasurementRecord(parameter, e.RowIndex);
                     FlashObjectDetectionMeasurementRecord();
                 };
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
                var measurementRecordFooter = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 58
                };
                objectDetectionMeasurementClipLinesCheckBox = new CheckBox
                {
                    Dock = DockStyle.Top,
                    Height = 28,
                    Text = "量測線依 MASK 範圍裁切",
                    Checked = parameter.MeasurementClipLinesToMask,
                    Enabled = false,
                    AutoSize = false
                };
                objectDetectionMeasurementClipLinesCheckBox.CheckedChanged += delegate
                {
                    ObjectDetectionMeasurementClipLinesCheckBox_CheckedChanged(parameter);
                };
                measurementRecordFooter.Controls.Add(saveMeasurementRecordButton);
                measurementRecordFooter.Controls.Add(objectDetectionMeasurementClipLinesCheckBox);
                measurementRecordsGroup.Controls.Add(objectDetectionMeasurementRecordsGrid);
                measurementRecordsGroup.Controls.Add(measurementRecordFooter);
                RefreshObjectDetectionMeasurementRecordsGrid(parameter);
                ObjectDetectionMeasurementRecordSettings displayedMeasurementRecord =
                    GetActiveObjectDetectionMeasurementRecord(parameter);
                if (displayedMeasurementRecord != null)
                {
                    objectDetectionMeasurementAppliedRecordId = displayedMeasurementRecord.Id;
                    objectDetectionMeasurementClipLinesCheckBox.Enabled = true;
                    if (parameter.MeasurementClipLinesToMask)
                    {
                        PrepareObjectDetectionMeasurementClipLines(
                            parameter,
                            displayedMeasurementRecord);
                    }
                }

                objectDetectionObjectNumberPanel =
                    CreateObjectDetectionObjectNumberPanel(parameter);

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



    }
}
