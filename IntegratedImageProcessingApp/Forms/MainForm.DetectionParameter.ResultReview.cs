using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ResultReviewParameterChoice
        {
            public ObjectDetectionParameterSettings Parameter { get; set; }
            public string DisplayName { get; set; }

            public override string ToString()
            {
                return DisplayName ?? string.Empty;
            }
        }

        private sealed class ResultReviewMeasurementContext
        {
            public ObjectDetectionParameterSettings Parameter { get; set; }
            public IDictionary<int, ObjectDetectionMeasurementStatistics> StatisticsByRecord { get; set; }
        }

        private sealed class ResultReviewMeasurementRowContext
        {
            public ObjectDetectionParameterSettings Parameter { get; set; }
            public ObjectDetectionMeasurementRecordSettings Record { get; set; }
            public ObjectDetectionParameterSettings MaskParameter { get; set; }
            public ObjectDefinitionDetectedObject DetectedObject { get; set; }
            public int ObjectNumber { get; set; }
            public ObjectDetectionMeasurementStatistics Statistics { get; set; }
            public bool StatisticsHighlightsVisible { get; set; }
        }

        private Panel objectDetectionResultReviewPanel;
        private ComboBox objectDetectionResultReviewParameterComboBox;
        private Button objectDetectionResultReviewLoadImageButton;
        private Button objectDetectionResultReviewRunButton;
        private Label objectDetectionResultReviewImageLabel;
        private Label objectDetectionResultReviewStatusLabel;
        private TabControl objectDetectionResultReviewResultsTabs;
        private DataGridView objectDetectionResultReviewMeasurementsGrid;
        private CheckBox objectDetectionResultReviewClipLinesToMaskCheckBox;
        private DataGridView objectDetectionResultReviewConditionsGrid;
        private DataGridView objectDetectionResultReviewDefectsGrid;
        private bool isObjectDetectionResultReviewMode;
        private bool isObjectDetectionResultReviewRunning;
        private Size objectDetectionResultReviewReferenceImageSize;
        private string objectDetectionResultReviewImagePath;
        private SizeType[] objectDetectionResultReviewColumnTypes;
        private float[] objectDetectionResultReviewColumnWidths;
        private string objectDetectionResultReviewPendingDefinitionId;
        private TaskCompletionSource<string> objectDetectionResultReviewDefinitionCompletion;
        private long? objectDetectionResultReviewImageLoadMilliseconds;
        private bool? objectDetectionResultReviewImageLoadSucceeded;
        private long? objectDetectionResultReviewImageProcessingMilliseconds;
        private long? objectDetectionResultReviewMeasurementMilliseconds;
        private long? objectDetectionResultReviewMeasurementDisplayMilliseconds;
        private long? objectDetectionResultReviewDefectMilliseconds;
        private long? objectDetectionResultReviewDefectDisplayMilliseconds;
        private string objectDetectionResultReviewActiveTimingStage;
        private ResultReviewMeasurementRowContext objectDetectionResultReviewSelectedMeasurement;
        private Timer objectDetectionResultReviewHighlightTimer;
        private string objectDetectionResultReviewPreviousParameterId;
        private int objectDetectionResultReviewPreviousObjectNumber;

        private void ShowObjectDetectionResultReviewPanel()
        {
            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            HideObjectDefinitionParameterPanel();
            HideObjectDetectionParameterPanel();
            EnsureObjectDetectionMeasurementDisplay();
            EnsureObjectDetectionFlatFieldPreviewDisplay();
            EnsureObjectDetectionDefectDisplay();
            SetObjectDetectionParameterDisplayMode(true);

            if (!isObjectDetectionResultReviewMode)
            {
                objectDetectionResultReviewPreviousParameterId = activeObjectDetectionParameterId;
                objectDetectionResultReviewPreviousObjectNumber = selectedObjectDetectionNumber;
                objectDetectionResultReviewSelectedMeasurement = null;
                objectDetectionResultReviewColumnTypes = mainLayoutPanel.ColumnStyles
                    .Cast<ColumnStyle>().Select(style => style.SizeType).ToArray();
                objectDetectionResultReviewColumnWidths = mainLayoutPanel.ColumnStyles
                    .Cast<ColumnStyle>().Select(style => style.Width).ToArray();
                string referencePath = systemParameters.LastImagePath;
                if (!string.IsNullOrWhiteSpace(referencePath) && File.Exists(referencePath))
                {
                    try
                    {
                        objectDetectionResultReviewReferenceImageSize =
                            LargeImageSource.ReadImageSize(referencePath);
                    }
                    catch
                    {
                        objectDetectionResultReviewReferenceImageSize = Size.Empty;
                    }
                }
                isObjectDetectionResultReviewMode = true;
            }

            leftImageTabControl.SuspendLayout();
            imageLayoutPanel.SuspendLayout();
            mainLayoutPanel.SuspendLayout();
            try
            {
                leftImageTabControl.TabPages.Clear();
                leftImageTabControl.TabPages.Add(objectDetectionMeasurementTabPage);
                leftImageTabControl.TabPages.Add(objectDetectionFlatFieldPreviewTabPage);
                foreach (TabPage page in objectDetectionDefectDisplayTabPages)
                {
                    leftImageTabControl.TabPages.Add(page);
                }

                objectDetectionMeasurementTabPage.Text = "量測位置";
                objectDetectionDefectDisplayTabPages[4].Text = "整合";
                leftImageTabControl.SelectedTab = objectDetectionMeasurementTabPage;
                objectDetectionMeasurementDisplayControl.TitleText = "左側 量測位置";
                objectDetectionFlatFieldPreviewDisplayControl.TitleText = "左側 平場校正";
                for (int index = 0; index < objectDetectionDefectDisplayControls.Length; index++)
                {
                    objectDetectionDefectDisplayControls[index].TitleText =
                        "左側 " + (index == 4 ? "整合" : ObjectDetectionDefectDisplayNames[index]);
                }

                rightImageTabControl.Visible = false;
                if (objectDetectionParameterTabControl != null)
                {
                    objectDetectionParameterTabControl.Visible = false;
                }
                rightPanel.Visible = false;
                mainLayoutPanel.ColumnStyles[0].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[0].Width = 20F;
                mainLayoutPanel.ColumnStyles[1].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[1].Width = 80F;
                mainLayoutPanel.ColumnStyles[2].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[2].Width = 0F;

                if (objectDetectionResultReviewPanel == null || objectDetectionResultReviewPanel.IsDisposed)
                {
                    objectDetectionResultReviewPanel = CreateObjectDetectionResultReviewPanel();
                }
                if (!imageLayoutPanel.Controls.Contains(objectDetectionResultReviewPanel))
                {
                    imageLayoutPanel.Controls.Add(objectDetectionResultReviewPanel, 1, 0);
                }
                objectDetectionResultReviewPanel.Visible = true;
                objectDetectionResultReviewPanel.BringToFront();
                leftImageTabControl.Visible = true;
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
                imageLayoutPanel.ResumeLayout(true);
                leftImageTabControl.ResumeLayout(true);
            }

            PopulateObjectDetectionResultReviewParameters();
            if (string.IsNullOrWhiteSpace(objectDetectionResultReviewImagePath) &&
                !string.IsNullOrWhiteSpace(systemParameters.LastImagePath) &&
                File.Exists(systemParameters.LastImagePath))
            {
                objectDetectionResultReviewImagePath = systemParameters.LastImagePath;
            }
            if (objectDetectionResultReviewImageLabel != null &&
                !string.IsNullOrWhiteSpace(objectDetectionResultReviewImagePath))
            {
                objectDetectionResultReviewImageLabel.Text =
                    Path.GetFileName(objectDetectionResultReviewImagePath) +
                    (objectDetectionResultReviewReferenceImageSize.Width > 0 &&
                     objectDetectionResultReviewReferenceImageSize.Height > 0
                        ? "  (" +
                            objectDetectionResultReviewReferenceImageSize.Width.ToString(
                                "N0", CultureInfo.CurrentCulture) + " x " +
                            objectDetectionResultReviewReferenceImageSize.Height.ToString(
                                "N0", CultureInfo.CurrentCulture) + ")"
                        : string.Empty);
            }
            RefreshObjectDetectionFlatFieldDisplay();
            RefreshObjectDetectionMeasurementDisplay();
            RefreshObjectDetectionDefectDisplay();
            UpdateObjectDetectionResultReviewTimingMemo();
            statusLabel.Text = "參數結果確認：選擇檢測參數與同規格圖片後，執行確認。";
        }

        private void ExitObjectDetectionResultReviewMode()
        {
            if (!isObjectDetectionResultReviewMode)
            {
                return;
            }

            if (objectDetectionResultReviewPanel != null)
            {
                imageLayoutPanel.Controls.Remove(objectDetectionResultReviewPanel);
                objectDetectionResultReviewPanel.Dispose();
                objectDetectionResultReviewPanel = null;
            }

            objectDetectionMeasurementTabPage.Text = "待量測";
            objectDetectionDefectDisplayTabPages[4].Text = ObjectDetectionDefectDisplayNames[4];
            rightPanel.Visible = true;
            for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count; index++)
            {
                if (objectDetectionResultReviewColumnTypes != null &&
                    index < objectDetectionResultReviewColumnTypes.Length)
                {
                    mainLayoutPanel.ColumnStyles[index].SizeType = objectDetectionResultReviewColumnTypes[index];
                    mainLayoutPanel.ColumnStyles[index].Width = objectDetectionResultReviewColumnWidths[index];
                }
            }
            isObjectDetectionResultReviewMode = false;
            objectDetectionResultReviewImagePath = null;
            objectDetectionResultReviewReferenceImageSize = Size.Empty;
            if (objectDetectionResultReviewHighlightTimer != null)
            {
                objectDetectionResultReviewHighlightTimer.Stop();
                objectDetectionResultReviewHighlightTimer.Dispose();
                objectDetectionResultReviewHighlightTimer = null;
            }
            objectDetectionResultReviewSelectedMeasurement = null;
            activeObjectDetectionParameterId = objectDetectionResultReviewPreviousParameterId;
            selectedObjectDetectionNumber = objectDetectionResultReviewPreviousObjectNumber;
            objectDetectionResultReviewPreviousParameterId = null;
            objectDetectionResultReviewPreviousObjectNumber = 0;
            SetObjectDetectionParameterDisplayMode(false);
        }

        private Panel CreateObjectDetectionResultReviewPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(248, 250, 252),
                AutoScroll = true,
                Name = "objectDetectionResultReviewPanel"
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            header.Controls.Add(new Label
            {
                Text = "檢測參數",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            }, 0, 0);

            objectDetectionResultReviewParameterComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(6, 5, 6, 5)
            };
            header.Controls.Add(objectDetectionResultReviewParameterComboBox, 1, 0);

            objectDetectionResultReviewLoadImageButton = new Button
            {
                Text = "載入圖片",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 4, 4, 4)
            };
            objectDetectionResultReviewLoadImageButton.Click +=
                async delegate { await LoadObjectDetectionResultReviewImageAsync(); };
            header.Controls.Add(objectDetectionResultReviewLoadImageButton, 2, 0);

            objectDetectionResultReviewImageLabel = new Label
            {
                Text = "尚未選擇檢測圖片",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 83, 95),
                Padding = new Padding(5, 0, 5, 0)
            };
            header.Controls.Add(objectDetectionResultReviewImageLabel, 3, 0);

            objectDetectionResultReviewRunButton = new Button
            {
                Text = "執行確認",
                Dock = DockStyle.Fill,
                Margin = new Padding(4)
            };
            objectDetectionResultReviewRunButton.Click +=
                async delegate { await RunObjectDetectionResultReviewAsync(); };
            header.Controls.Add(objectDetectionResultReviewRunButton, 4, 0);

            objectDetectionResultReviewStatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "選擇檢測參數與同規格圖片後，可執行尺寸與缺陷確認。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                Padding = new Padding(2, 0, 2, 0),
                AutoEllipsis = true
            };
            objectDetectionResultReviewResultsTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "objectDetectionResultReviewResultsTabs"
            };

            objectDetectionResultReviewMeasurementsGrid = CreateObjectDetectionResultReviewGrid();
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Object", "物件", 8);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Number", "編號", 7);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Name", "量測名稱", 13);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Mode", "模式", 9);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Direction", "方向", 8);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Count", "線數", 7);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Minimum", "最小", 11);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Average", "平均", 11);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Maximum", "最大", 11);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Unit", "單位", 6);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewMeasurementsGrid, "Result", "狀態", 20);
            objectDetectionResultReviewMeasurementsGrid.CellClick +=
                ObjectDetectionResultReviewMeasurementsGrid_CellClick;
            var measurementTabLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            measurementTabLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            measurementTabLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            measurementTabLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            objectDetectionResultReviewClipLinesToMaskCheckBox = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = "量測線依 MASK 範圍裁切",
                AutoSize = false,
                Margin = new Padding(4, 2, 0, 0)
            };
            objectDetectionResultReviewClipLinesToMaskCheckBox.CheckedChanged += delegate
            {
                if (objectDetectionMeasurementDisplayControl != null)
                {
                    objectDetectionMeasurementDisplayControl.InvalidateImageView();
                }
            };
            measurementTabLayout.Controls.Add(objectDetectionResultReviewMeasurementsGrid, 0, 0);
            measurementTabLayout.Controls.Add(objectDetectionResultReviewClipLinesToMaskCheckBox, 0, 1);
            objectDetectionResultReviewResultsTabs.TabPages.Add(
                CreateObjectDetectionResultReviewTab("量測資料", measurementTabLayout));

            objectDetectionResultReviewConditionsGrid = CreateObjectDetectionResultReviewGrid();
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "Number", "編號", 5);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "Name", "條件名稱", 10);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "AExpression", "A 規計算式", 15);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "ASpecification", "A 規規格", 12);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "AResult", "A 規結果", 16);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "BExpression", "B 規計算式", 15);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "BSpecification", "B 規規格", 12);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewConditionsGrid, "BResult", "B 規結果", 15);
            objectDetectionResultReviewResultsTabs.TabPages.Add(
                CreateObjectDetectionResultReviewTab(
                    "尺寸良品判定",
                    CreateObjectDetectionResultReviewGoodJudgementContent(
                        objectDetectionResultReviewConditionsGrid)));

            objectDetectionResultReviewDefectsGrid = CreateObjectDetectionResultReviewGrid();
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewDefectsGrid, "Source", "來源", 24);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewDefectsGrid, "Count", "缺陷數", 13);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewDefectsGrid, "Elapsed", "運算時間 (ms)", 18);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewDefectsGrid, "Status", "結果", 20);
            AddObjectDetectionResultReviewTextColumn(objectDetectionResultReviewDefectsGrid, "Detail", "說明", 25);
            objectDetectionResultReviewResultsTabs.TabPages.Add(
                CreateObjectDetectionResultReviewTab("缺陷判定", objectDetectionResultReviewDefectsGrid));

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(objectDetectionResultReviewStatusLabel, 0, 1);
            layout.Controls.Add(objectDetectionResultReviewResultsTabs, 0, 2);
            panel.Controls.Add(layout);
            objectDetectionResultReviewParameterComboBox.SelectedIndexChanged +=
                ObjectDetectionResultReviewParameterComboBox_SelectedIndexChanged;
            return panel;
        }

        private static TabPage CreateObjectDetectionResultReviewTab(string title, Control content)
        {
            var page = new TabPage(title);
            page.Controls.Add(content);
            return page;
        }

        private static DataGridView CreateObjectDetectionResultReviewGrid()
        {
            return new DataGridView
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
                BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
        }

        private static void AddObjectDetectionResultReviewTextColumn(
            DataGridView grid, string name, string header, float fillWeight)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void PopulateObjectDetectionResultReviewParameters()
        {
            if (objectDetectionResultReviewParameterComboBox == null)
            {
                return;
            }

            string preferredId = activeObjectDetectionParameterId;
            objectDetectionResultReviewParameterComboBox.BeginUpdate();
            try
            {
                objectDetectionResultReviewParameterComboBox.Items.Clear();
                foreach (ObjectDetectionParameterSettings parameter in systemParameters.ObjectDetectionParameters)
                {
                objectDetectionResultReviewParameterComboBox.Items.Add(
                        new ResultReviewParameterChoice
                        {
                            Parameter = parameter,
                            DisplayName = GetObjectDetectionParameterDisplayName(
                                parameter,
                                systemParameters.ObjectDetectionParameters.IndexOf(parameter))
                        });
                }

                int selectedIndex = -1;
                for (int index = 0; index < objectDetectionResultReviewParameterComboBox.Items.Count; index++)
                {
                    var choice = objectDetectionResultReviewParameterComboBox.Items[index] as ResultReviewParameterChoice;
                    if (choice != null && string.Equals(choice.Parameter.Id, preferredId, StringComparison.Ordinal))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
                if (selectedIndex < 0 && objectDetectionResultReviewParameterComboBox.Items.Count > 0)
                {
                    selectedIndex = 0;
                }
                objectDetectionResultReviewParameterComboBox.SelectedIndex = selectedIndex;
            }
            finally
            {
                objectDetectionResultReviewParameterComboBox.EndUpdate();
            }

            if (objectDetectionResultReviewParameterComboBox.SelectedIndex < 0)
            {
                objectDetectionResultReviewStatusLabel.Text = "目前沒有檢測參數可供確認。";
                objectDetectionResultReviewRunButton.Enabled = false;
            }
        }

        private void ObjectDetectionResultReviewParameterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var choice = objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (choice == null || choice.Parameter == null)
            {
                return;
            }

            activeObjectDetectionParameterId = choice.Parameter.Id;
            ClearObjectDetectionResultReviewResults();
            ResetObjectDetectionResultReviewProcessingTimings();
            UpdateObjectDetectionResultReviewTimingMemo();
            RefreshObjectDetectionFlatFieldDisplay();
            RefreshObjectDetectionMeasurementDisplay();
            RefreshObjectDetectionDefectDisplay();
            objectDetectionResultReviewStatusLabel.Text =
                string.IsNullOrWhiteSpace(choice.Parameter.ObjectDefinitionId)
                    ? "此檢測參數尚未關聯物件定義結果。"
                    : "目前參數：" + choice.Parameter.DisplayName + "；請載入同規格圖片後執行確認。";
        }

        private async Task LoadObjectDetectionResultReviewImageAsync()
        {
            if (isObjectDetectionResultReviewRunning || isLoadingImage)
            {
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "載入結果確認圖片";
                dialog.Filter = "圖片檔案|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有檔案|*.*";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                Stopwatch imageLoadStopwatch = Stopwatch.StartNew();
                objectDetectionResultReviewImageLoadMilliseconds = null;
                objectDetectionResultReviewImageLoadSucceeded = null;
                ResetObjectDetectionResultReviewProcessingTimings();
                UpdateObjectDetectionResultReviewTimingMemo();

                Size selectedSize;
                try
                {
                    selectedSize = await Task.Run(() => LargeImageSource.ReadImageSize(dialog.FileName));
                }
                catch (Exception exception)
                {
                    imageLoadStopwatch.Stop();
                    objectDetectionResultReviewImageLoadMilliseconds = imageLoadStopwatch.ElapsedMilliseconds;
                    objectDetectionResultReviewImageLoadSucceeded = false;
                    UpdateObjectDetectionResultReviewTimingMemo();
                    SetObjectDetectionResultReviewStatus("圖片讀取失敗：" + exception.Message);
                    return;
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    imageLoadStopwatch.Stop();
                    return;
                }

                if (objectDetectionResultReviewReferenceImageSize.Width > 0 &&
                    objectDetectionResultReviewReferenceImageSize.Height > 0 &&
                    selectedSize != objectDetectionResultReviewReferenceImageSize)
                {
                    imageLoadStopwatch.Stop();
                    objectDetectionResultReviewImageLoadMilliseconds = imageLoadStopwatch.ElapsedMilliseconds;
                    objectDetectionResultReviewImageLoadSucceeded = false;
                    UpdateObjectDetectionResultReviewTimingMemo();
                    objectDetectionResultReviewStatusLabel.Text = "圖片規格不符，未載入。";
                    MessageBox.Show(
                        this,
                        "此圖片尺寸為 " + selectedSize.Width.ToString(CultureInfo.InvariantCulture) + " x " +
                        selectedSize.Height.ToString(CultureInfo.InvariantCulture) + "，參考規格為 " +
                        objectDetectionResultReviewReferenceImageSize.Width.ToString(CultureInfo.InvariantCulture) + " x " +
                        objectDetectionResultReviewReferenceImageSize.Height.ToString(CultureInfo.InvariantCulture) + "。\r\n" +
                        "請選擇與目前檢測規格相同尺寸的圖片。",
                        "圖片規格不符",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                isLoadingImage = true;
                SetObjectDetectionResultReviewControlsEnabled(false);
                try
                {
                    objectDetectionResultReviewStatusLabel.Text = "正在載入圖片...";
                    systemParameters.LastImagePath = dialog.FileName;
                    await LoadImageIntoOriginalDisplaysAsync(
                        dialog.FileName,
                        System.Threading.CancellationToken.None);
                    imageLoadStopwatch.Stop();
                    objectDetectionResultReviewImageLoadMilliseconds = imageLoadStopwatch.ElapsedMilliseconds;
                    objectDetectionResultReviewImageLoadSucceeded = true;
                    UpdateObjectDetectionResultReviewTimingMemo();
                    if (!IsObjectDetectionResultReviewUiAvailable())
                    {
                        return;
                    }
                    objectDetectionResultReviewImagePath = dialog.FileName;
                    ClearObjectDetectionResultReviewResults();
                    if (objectDetectionResultReviewReferenceImageSize.IsEmpty)
                    {
                        objectDetectionResultReviewReferenceImageSize = selectedSize;
                    }
                    objectDetectionResultReviewImageLabel.Text =
                        Path.GetFileName(dialog.FileName) + "  (" +
                        selectedSize.Width.ToString("N0", CultureInfo.CurrentCulture) + " x " +
                        selectedSize.Height.ToString("N0", CultureInfo.CurrentCulture) + ")";
                    objectDetectionResultReviewStatusLabel.Text = "圖片已載入；請執行確認以更新所有結果。";
                    RefreshObjectDetectionFlatFieldDisplay();
                    RefreshObjectDetectionMeasurementDisplay();
                    RefreshObjectDetectionDefectDisplay();
                }
                catch (Exception exception)
                {
                    if (imageLoadStopwatch.IsRunning)
                    {
                        imageLoadStopwatch.Stop();
                    }
                    objectDetectionResultReviewImageLoadMilliseconds = imageLoadStopwatch.ElapsedMilliseconds;
                    objectDetectionResultReviewImageLoadSucceeded = false;
                    UpdateObjectDetectionResultReviewTimingMemo();
                    SetObjectDetectionResultReviewStatus("圖片載入失敗：" + exception.Message);
                }
                finally
                {
                    isLoadingImage = false;
                    SetObjectDetectionResultReviewControlsEnabled(true);
                }
            }
        }

        private async Task RunObjectDetectionResultReviewAsync()
        {
            if (isObjectDetectionResultReviewRunning)
            {
                return;
            }

            var choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            ObjectDetectionParameterSettings parameter = choice == null ? null : choice.Parameter;
            if (parameter == null)
            {
                objectDetectionResultReviewStatusLabel.Text = "請先選擇檢測參數。";
                return;
            }
            if (string.IsNullOrWhiteSpace(objectDetectionResultReviewImagePath) ||
                !string.Equals(systemParameters.LastImagePath, objectDetectionResultReviewImagePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                objectDetectionResultReviewStatusLabel.Text = "請先載入本次要確認的檢測圖片。";
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null || !HasConfiguredObjectDefinitionSource(definition))
            {
                objectDetectionResultReviewStatusLabel.Text = "此參數沒有可用的物件定義來源，無法執行確認。";
                return;
            }
            if (systemParameters.RoiRegions == null ||
                !systemParameters.RoiRegions.Any(region => region.Bounds.Width > 0 && region.Bounds.Height > 0))
            {
                objectDetectionResultReviewStatusLabel.Text = "目前參數沒有有效 ROI，無法執行確認。";
                return;
            }

            isObjectDetectionResultReviewRunning = true;
            SetObjectDetectionResultReviewControlsEnabled(false);
            ClearObjectDetectionResultReviewResults();
            ResetObjectDetectionResultReviewProcessingTimings();
            UpdateObjectDetectionResultReviewTimingMemo();
            try
            {
                activeObjectDetectionParameterId = parameter.Id;
                selectedObjectDetectionNumber = -1;
                long imageProcessingMilliseconds = 0;
                StartObjectDetectionResultReviewTimingStage("影像處理");
                objectDetectionResultReviewStatusLabel.Text = "正在依參數執行物件定義處理...";
                statusLabel.Text = parameter.DisplayName + " 結果確認：物件搜尋中...";

                string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
                if (!HasCompletedObjectDefinitionResult(definition, definitionSignature))
                {
                    objectDetectionResultReviewPendingDefinitionId = definition.Id;
                    objectDetectionResultReviewDefinitionCompletion =
                        new TaskCompletionSource<string>();
                    StartObjectDefinitionProcessing(definition.Id);
                    await WaitForObjectDetectionResultReviewDefinitionAsync(
                        definition,
                        definitionSignature,
                        objectDetectionResultReviewDefinitionCompletion);
                    imageProcessingMilliseconds += Math.Max(0, objectDefinitionProcessingElapsedMilliseconds);
                }
                objectDetectionResultReviewImageProcessingMilliseconds = imageProcessingMilliseconds;
                UpdateObjectDetectionResultReviewTimingMemo();
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    return;
                }

                List<ObjectDefinitionDetectedObject> objects =
                    SnapshotCompletedObjectDefinitionObjects(definition, definitionSignature);
                if (objects.Count == 0)
                {
                    throw new InvalidOperationException("物件定義沒有找到可檢測的物件。");
                }

                selectedObjectDetectionNumber = objects[0].Number;
                Stopwatch measurementDisplayStopwatch = Stopwatch.StartNew();
                RefreshObjectDetectionMeasurementDisplay();
                measurementDisplayStopwatch.Stop();
                objectDetectionResultReviewMeasurementDisplayMilliseconds =
                    (objectDetectionResultReviewMeasurementDisplayMilliseconds ?? 0) +
                    measurementDisplayStopwatch.ElapsedMilliseconds;
                RefreshObjectDetectionFlatFieldDisplay();
                objectDetectionResultReviewStatusLabel.Text =
                    "已找到 " + objects.Count.ToString("N0", CultureInfo.CurrentCulture) +
                    " 個物件，正在載入參數內保存的 MASK 與平場校正值...";
                using (var flatFieldOperationStatus = new Label())
                {
                    await ApplyObjectDetectionFlatFieldMaskAsync(
                        parameter,
                        flatFieldOperationStatus,
                        null,
                        false,
                        true,
                        elapsed =>
                        {
                            imageProcessingMilliseconds += elapsed;
                            objectDetectionResultReviewImageProcessingMilliseconds = imageProcessingMilliseconds;
                            UpdateObjectDetectionResultReviewTimingMemo();
                        },
                        elapsed =>
                        {
                            imageProcessingMilliseconds += (long)Math.Round(elapsed);
                            objectDetectionResultReviewImageProcessingMilliseconds = imageProcessingMilliseconds;
                            UpdateObjectDetectionResultReviewTimingMemo();
                        });
                    if (!IsObjectDetectionResultReviewUiAvailable())
                    {
                        return;
                    }
                    if (!IsCurrentObjectDetectionFlatFieldImage(parameter))
                    {
                        bool hasSavedProfile = !string.IsNullOrWhiteSpace(
                            parameter.FlatFieldSavedProfileData);
                        bool savedProfileSettingsMatch = hasSavedProfile &&
                            string.Equals(parameter.FlatFieldSavedSettingsSignature,
                                CreateObjectDetectionFlatFieldSettingsSignature(parameter),
                                StringComparison.Ordinal);
                        string operationStatus = flatFieldOperationStatus.Text ?? string.Empty;
                        string failureReason = !hasSavedProfile
                            ? "此參數沒有保存平場校正曲線。"
                            : !savedProfileSettingsMatch
                                ? "保存的平場校正曲線與目前設定不符。"
                                : operationStatus.StartsWith("來源 MASK：", StringComparison.Ordinal)
                                    ? "MASK 已套用，但保存的平場校正影像未完成。"
                                    : string.IsNullOrWhiteSpace(operationStatus)
                                        ? "無法載入此參數保存的平場校正值。"
                                        : operationStatus;
                        throw new InvalidOperationException(failureReason);
                    }

                    int appliedSourceMaskCount;
                    int appliedUseMaskCount;
                    lock (objectDetectionFlatFieldMaskLock)
                    {
                        appliedSourceMaskCount = objectDetectionFlatFieldMaskOverlays.Count;
                        appliedUseMaskCount = objectDetectionFlatFieldUseMaskOverlays.Count;
                    }
                    objectDetectionResultReviewStatusLabel.Text =
                        "平場校正完成：來源 MASK " +
                        appliedSourceMaskCount.ToString("N0", CultureInfo.CurrentCulture) + "/" +
                        objects.Count.ToString("N0", CultureInfo.CurrentCulture) +
                        " 個 ROI；使用位置 MASK " +
                        appliedUseMaskCount.ToString("N0", CultureInfo.CurrentCulture) + " 個 ROI。";
                }

                objectDetectionResultReviewImageProcessingMilliseconds = imageProcessingMilliseconds;
                StartObjectDetectionResultReviewTimingStage("缺陷檢測計算");
                objectDetectionResultReviewStatusLabel.Text =
                    "平場校正已套用，正在執行已啟用的缺陷條件...";
                statusLabel.Text = parameter.DisplayName + " 結果確認：缺陷檢測中...";

                leftImageTabControl.SelectedTab = objectDetectionDefectDisplayTabPages[4];
                await RunObjectDetectionDefectProcessingAsync(
                    parameter.Id,
                    null,
                    elapsed =>
                    {
                        objectDetectionResultReviewDefectMilliseconds = elapsed;
                        UpdateObjectDetectionResultReviewTimingMemo();
                    },
                    elapsed =>
                    {
                        objectDetectionResultReviewDefectDisplayMilliseconds = elapsed;
                        UpdateObjectDetectionResultReviewTimingMemo();
                    });
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    return;
                }

                StopObjectDetectionResultReviewTimingStage();
                StartObjectDetectionResultReviewTimingStage("尺寸量測計算");
                objectDetectionResultReviewStatusLabel.Text = "正在計算尺寸量測與良品條件...";
                statusLabel.Text = parameter.DisplayName + " 結果確認：尺寸判定中...";
                await BuildObjectDetectionResultReviewResultsAsync(parameter, definition, objects);
                StopObjectDetectionResultReviewTimingStage();
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    return;
                }
                leftImageTabControl.SelectedTab = objectDetectionMeasurementTabPage;
                measurementDisplayStopwatch = Stopwatch.StartNew();
                RefreshObjectDetectionMeasurementDisplay();
                measurementDisplayStopwatch.Stop();
                objectDetectionResultReviewMeasurementDisplayMilliseconds =
                    (objectDetectionResultReviewMeasurementDisplayMilliseconds ?? 0) +
                    measurementDisplayStopwatch.ElapsedMilliseconds;
                RefreshObjectDetectionFlatFieldDisplay();
                Stopwatch defectDisplayStopwatch = Stopwatch.StartNew();
                RefreshObjectDetectionDefectDisplay();
                defectDisplayStopwatch.Stop();
                objectDetectionResultReviewDefectDisplayMilliseconds =
                    (objectDetectionResultReviewDefectDisplayMilliseconds ?? 0) +
                    defectDisplayStopwatch.ElapsedMilliseconds;
                objectDetectionResultReviewStatusLabel.Text =
                    BuildObjectDetectionResultReviewSummary(parameter, objects.Count);
                statusLabel.Text = parameter.DisplayName + " 結果確認完成";
            }
            catch (Exception exception)
            {
                StopObjectDetectionResultReviewTimingStage();
                SetObjectDetectionResultReviewStatus("結果確認未完成：" + exception.Message);
                statusLabel.Text = "結果確認未完成：" + exception.Message;
            }
            finally
            {
                StopObjectDetectionResultReviewTimingStage();
                objectDetectionResultReviewPendingDefinitionId = null;
                objectDetectionResultReviewDefinitionCompletion = null;
                isObjectDetectionResultReviewRunning = false;
                SetObjectDetectionResultReviewControlsEnabled(true);
                UpdateObjectDetectionResultReviewTimingMemo();
            }
        }

        private void ResetObjectDetectionResultReviewProcessingTimings()
        {
            objectDetectionResultReviewImageProcessingMilliseconds = null;
            objectDetectionResultReviewMeasurementMilliseconds = null;
            objectDetectionResultReviewMeasurementDisplayMilliseconds = null;
            objectDetectionResultReviewDefectMilliseconds = null;
            objectDetectionResultReviewDefectDisplayMilliseconds = null;
            objectDetectionResultReviewActiveTimingStage = null;
        }

        private void StartObjectDetectionResultReviewTimingStage(string stage)
        {
            objectDetectionResultReviewActiveTimingStage = stage;
            UpdateObjectDetectionResultReviewTimingMemo();
        }

        private void StopObjectDetectionResultReviewTimingStage()
        {
            objectDetectionResultReviewActiveTimingStage = null;
            UpdateObjectDetectionResultReviewTimingMemo();
        }

        private void UpdateObjectDetectionResultReviewTimingMemo()
        {
            if (debugTimingMemo == null || debugTimingMemo.IsDisposed)
            {
                return;
            }

            ResetDebugTimingMemo();
            AppendDebugTimingMemo("參數結果確認處理時間");
            string imageLoadText = !objectDetectionResultReviewImageLoadMilliseconds.HasValue
                ? "尚未讀取"
                : objectDetectionResultReviewImageLoadMilliseconds.Value.ToString("N0", CultureInfo.CurrentCulture) +
                    " ms" + (objectDetectionResultReviewImageLoadSucceeded == false ? "（未完成）" : string.Empty);
            AppendDebugTimingMemo("讀取圖片：" + imageLoadText);

            long completedMilliseconds =
                (objectDetectionResultReviewImageProcessingMilliseconds ?? 0) +
                (objectDetectionResultReviewMeasurementMilliseconds ?? 0) +
                (objectDetectionResultReviewDefectMilliseconds ?? 0);
            bool allStagesComplete = objectDetectionResultReviewImageProcessingMilliseconds.HasValue &&
                objectDetectionResultReviewMeasurementMilliseconds.HasValue &&
                objectDetectionResultReviewDefectMilliseconds.HasValue;
            string totalText = allStagesComplete
                ? completedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms"
                : objectDetectionResultReviewActiveTimingStage != null
                    ? "執行中（已完成 " + completedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms）"
                    : completedMilliseconds > 0
                        ? "未完成（已完成 " + completedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms）"
                        : "尚未執行";
            AppendDebugTimingMemo("影像處理總時間：" + totalText);
            AppendDebugTimingMemo("  影像處理：" + FormatObjectDetectionResultReviewStageTime(
                objectDetectionResultReviewImageProcessingMilliseconds, "影像處理"));
            AppendDebugTimingMemo("  尺寸量測計算：" + FormatObjectDetectionResultReviewStageTime(
                objectDetectionResultReviewMeasurementMilliseconds, "尺寸量測計算"));
            AppendDebugTimingMemo("  缺陷檢測計算：" + FormatObjectDetectionResultReviewStageTime(
                objectDetectionResultReviewDefectMilliseconds, "缺陷檢測計算"));

            long displayMilliseconds = objectDetectionResultReviewImageLoadMilliseconds.GetValueOrDefault() +
                objectDetectionResultReviewMeasurementDisplayMilliseconds.GetValueOrDefault() +
                objectDetectionResultReviewDefectDisplayMilliseconds.GetValueOrDefault();
            bool displayComplete = objectDetectionResultReviewImageLoadMilliseconds.HasValue &&
                objectDetectionResultReviewMeasurementDisplayMilliseconds.HasValue &&
                objectDetectionResultReviewDefectDisplayMilliseconds.HasValue;
            string displayTotalText = displayComplete
                ? displayMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms"
                : objectDetectionResultReviewActiveTimingStage != null
                    ? "執行中（已完成 " + displayMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms）"
                    : displayMilliseconds > 0
                        ? "未完成（已完成 " + displayMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms）"
                        : "尚未執行";
            AppendDebugTimingMemo("顯示處理總時間：" + displayTotalText);
            AppendDebugTimingMemo("  讀取圖片：" + imageLoadText);
            AppendDebugTimingMemo("  尺寸量測顯示：" + FormatObjectDetectionResultReviewStageTime(
                objectDetectionResultReviewMeasurementDisplayMilliseconds, "尺寸量測顯示"));
            AppendDebugTimingMemo("  缺陷檢測顯示：" + FormatObjectDetectionResultReviewStageTime(
                objectDetectionResultReviewDefectDisplayMilliseconds, "缺陷檢測顯示"));
        }

        private string FormatObjectDetectionResultReviewStageTime(long? elapsedMilliseconds, string stage)
        {
            if (elapsedMilliseconds.HasValue)
            {
                return elapsedMilliseconds.Value.ToString("N0", CultureInfo.CurrentCulture) + " ms";
            }
            return string.Equals(objectDetectionResultReviewActiveTimingStage, stage, StringComparison.Ordinal)
                ? "處理中..."
                : "尚未執行";
        }

        private async Task WaitForObjectDetectionResultReviewDefinitionAsync(
            ObjectDefinitionSettings definition,
            string signature,
            TaskCompletionSource<string> completion)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (!HasCompletedObjectDefinitionResult(definition, signature))
            {
                if (elapsed.Elapsed > TimeSpan.FromMinutes(30))
                {
                    throw new TimeoutException("物件定義處理超過 30 分鐘，請檢查影像大小與來源設定。");
                }
                if (completion.Task.IsCompleted)
                {
                    string error = await completion.Task;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                        ? "物件定義處理未能完成，請查看左下角 MEMO 的錯誤訊息。"
                        : error);
                }
                if (elapsed.Elapsed > TimeSpan.FromSeconds(2) &&
                    !string.Equals(pendingObjectDefinitionProcessingSignature, signature, StringComparison.Ordinal) &&
                    !string.Equals(activeObjectDefinitionProcessingSignature, signature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("物件定義處理未能啟動，請確認影像來源與 ROI 設定。");
                }
                await Task.WhenAny(completion.Task, Task.Delay(100));
            }
        }

        private async Task BuildObjectDetectionResultReviewResultsAsync(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionSettings definition,
            IList<ObjectDefinitionDetectedObject> objects)
        {
            objectDetectionResultReviewMeasurementsGrid.Rows.Clear();
            objectDetectionResultReviewConditionsGrid.Rows.Clear();
            objectDetectionResultReviewDefectsGrid.Rows.Clear();
            long calculationMilliseconds = 0;
            long displayMilliseconds = 0;

            List<ObjectDetectionMeasurementRecordSettings> records = (parameter.MeasurementRecords ??
                new List<ObjectDetectionMeasurementRecordSettings>())
                .Where(record => record != null).OrderBy(record => record.Number).ToList();
            var contexts = new Dictionary<int, ResultReviewMeasurementContext>();
            int originalSelection = selectedObjectDetectionNumber;
            try
            {
                foreach (ObjectDefinitionDetectedObject detectedObject in objects)
                {
                    var statsByRecord = new Dictionary<int, ObjectDetectionMeasurementStatistics>();
                    foreach (ObjectDetectionMeasurementRecordSettings record in records)
                    {
                        ObjectDetectionParameterSettings maskParameter =
                            CreateObjectDetectionResultReviewMaskParameter(parameter, record);
                        if (!IsObjectDetectionResultReviewUiAvailable())
                        {
                            return;
                        }
                        objectDetectionResultReviewStatusLabel.Text =
                            "正在量測物件 " + detectedObject.Number.ToString(CultureInfo.CurrentCulture) +
                            "，量測編號 " + record.Number.ToString(CultureInfo.CurrentCulture) + "...";
                        await Task.Yield();
                        if (!IsObjectDetectionResultReviewUiAvailable())
                        {
                            return;
                        }

                        ObjectDetectionMeasurementStatistics statistics;
                        string error;
                        bool succeeded;
                        Stopwatch calculationStopwatch = Stopwatch.StartNew();
                        try
                        {
                            succeeded = TryCalculateObjectDetectionMeasurementRecord(
                                parameter,
                                record,
                                detectedObject,
                                maskParameter,
                                out statistics,
                                out error,
                                true);
                        }
                        catch (Exception exception)
                        {
                            statistics = null;
                            error = exception.Message;
                            succeeded = false;
                        }
                        calculationStopwatch.Stop();
                        calculationMilliseconds += calculationStopwatch.ElapsedMilliseconds;

                        if (succeeded && statistics != null)
                        {
                            statsByRecord[record.Number] = statistics;
                            Stopwatch rowDisplayStopwatch = Stopwatch.StartNew();
                            int rowIndex = objectDetectionResultReviewMeasurementsGrid.Rows.Add(
                                detectedObject.Number, record.Number, record.Name,
                                string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                                    ? "平行線" : "單線",
                                string.Equals(record.Direction, "Vertical", StringComparison.OrdinalIgnoreCase)
                                    ? "垂直" : "水平",
                                string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                                    ? Math.Max(2, record.LineCount).ToString(CultureInfo.CurrentCulture) : "1",
                                FormatObjectDetectionReviewValue(statistics.Minimum),
                                FormatObjectDetectionReviewValue(statistics.Average),
                                FormatObjectDetectionReviewValue(statistics.Maximum),
                                statistics.Unit, "完成");
                            objectDetectionResultReviewMeasurementsGrid.Rows[rowIndex].Tag =
                                new ResultReviewMeasurementRowContext
                                {
                                    Parameter = parameter,
                                    Record = record,
                                    MaskParameter = maskParameter,
                                    DetectedObject = detectedObject,
                                    ObjectNumber = detectedObject.Number,
                                    Statistics = statistics
                                };
                            rowDisplayStopwatch.Stop();
                            displayMilliseconds += rowDisplayStopwatch.ElapsedMilliseconds;
                        }
                        else
                        {
                            Stopwatch rowDisplayStopwatch = Stopwatch.StartNew();
                            int rowIndex = objectDetectionResultReviewMeasurementsGrid.Rows.Add(
                                detectedObject.Number, record.Number, record.Name,
                                string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                                    ? "平行線" : "單線",
                                string.Equals(record.Direction, "Vertical", StringComparison.OrdinalIgnoreCase)
                                    ? "垂直" : "水平",
                                string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                                    ? Math.Max(2, record.LineCount).ToString(CultureInfo.CurrentCulture) : "1",
                                string.Empty, string.Empty, string.Empty,
                                IsObjectDetectionCameraPrecisionEnabled(parameter) ? "mm" : "px",
                                "無法量測：" + error);
                            objectDetectionResultReviewMeasurementsGrid.Rows[rowIndex].Tag =
                                new ResultReviewMeasurementRowContext
                                {
                                    Parameter = parameter,
                                    Record = record,
                                    MaskParameter = maskParameter,
                                    DetectedObject = detectedObject,
                                    ObjectNumber = detectedObject.Number
                                };
                            rowDisplayStopwatch.Stop();
                            displayMilliseconds += rowDisplayStopwatch.ElapsedMilliseconds;
                        }
                    }
                    contexts[detectedObject.Number] = new ResultReviewMeasurementContext
                    {
                        Parameter = parameter,
                        StatisticsByRecord = statsByRecord
                    };
                }
            }
            finally
            {
                selectedObjectDetectionNumber = originalSelection;
            }

            Stopwatch conditionStopwatch = Stopwatch.StartNew();
            CalculateObjectDetectionResultReviewGoodJudgements(parameter, objects, contexts);
            conditionStopwatch.Stop();
            calculationMilliseconds += conditionStopwatch.ElapsedMilliseconds;
            Stopwatch resultDisplayStopwatch = Stopwatch.StartNew();
            RefreshObjectDetectionResultReviewGoodJudgementView();
            AddObjectDetectionResultReviewDefectRows(parameter, definition);
            ApplyObjectDetectionResultReviewVerdictStyles();
            resultDisplayStopwatch.Stop();
            displayMilliseconds += resultDisplayStopwatch.ElapsedMilliseconds;
            objectDetectionResultReviewMeasurementMilliseconds = calculationMilliseconds;
            objectDetectionResultReviewMeasurementDisplayMilliseconds =
                (objectDetectionResultReviewMeasurementDisplayMilliseconds ?? 0) + displayMilliseconds;
            UpdateObjectDetectionResultReviewTimingMemo();
        }

        private void ObjectDetectionResultReviewMeasurementsGrid_CellClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (isObjectDetectionResultReviewRunning || e.RowIndex < 0 ||
                objectDetectionResultReviewMeasurementsGrid == null ||
                e.RowIndex >= objectDetectionResultReviewMeasurementsGrid.Rows.Count)
            {
                return;
            }

            var rowContext = objectDetectionResultReviewMeasurementsGrid.Rows[e.RowIndex].Tag
                as ResultReviewMeasurementRowContext;
            if (rowContext == null || rowContext.Parameter == null ||
                rowContext.Record == null || rowContext.DetectedObject == null)
            {
                return;
            }

            if (objectDetectionResultReviewHighlightTimer == null)
            {
                objectDetectionResultReviewHighlightTimer = new Timer { Interval = 10000 };
                objectDetectionResultReviewHighlightTimer.Tick += delegate
                {
                    objectDetectionResultReviewHighlightTimer.Stop();
                    if (objectDetectionResultReviewSelectedMeasurement != null)
                    {
                        objectDetectionResultReviewSelectedMeasurement.StatisticsHighlightsVisible = false;
                    }
                    if (objectDetectionMeasurementDisplayControl != null)
                    {
                        objectDetectionMeasurementDisplayControl.InvalidateImageView();
                    }
                };
            }

            objectDetectionResultReviewHighlightTimer.Stop();
            rowContext.StatisticsHighlightsVisible = rowContext.Statistics != null;
            objectDetectionResultReviewSelectedMeasurement = rowContext;
            if (rowContext.StatisticsHighlightsVisible)
            {
                objectDetectionResultReviewHighlightTimer.Start();
            }

            if (leftImageTabControl != null &&
                leftImageTabControl.TabPages.Contains(objectDetectionMeasurementTabPage))
            {
                leftImageTabControl.SelectedTab = objectDetectionMeasurementTabPage;
            }
            RefreshObjectDetectionMeasurementDisplay();
            if (objectDetectionMeasurementDisplayControl != null)
            {
                objectDetectionMeasurementDisplayControl.InvalidateImageView();
            }
        }

        private ObjectDetectionParameterSettings CreateObjectDetectionResultReviewMaskParameter(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionMeasurementRecordSettings record)
        {
            if (parameter == null || record == null)
            {
                return null;
            }

            return new ObjectDetectionParameterSettings
            {
                Id = parameter.Id,
                ObjectDefinitionId = parameter.ObjectDefinitionId,
                SourceMaskMode = record.SourceMaskMode,
                SourceMaskPrimaryType = record.SourceMaskPrimaryType,
                SourceMaskPrimaryId = record.SourceMaskPrimaryId,
                SourceMaskPrimaryNamespace = record.SourceMaskPrimaryNamespace,
                SourceMaskOperation = record.SourceMaskOperation,
                SourceMaskSecondaryType = record.SourceMaskSecondaryType,
                SourceMaskSecondaryId = record.SourceMaskSecondaryId,
                SourceMaskSecondaryNamespace = record.SourceMaskSecondaryNamespace
            };
        }

        private bool TryEvaluateObjectDetectionGoodJudgementExpression(
            string expression,
            ResultReviewMeasurementContext context,
            out double value,
            out string error)
        {
            value = 0;
            error = string.Empty;
            string validationMessage;
            if (!ValidateObjectDetectionGoodJudgementExpression(
                expression, context.Parameter, out validationMessage))
            {
                error = validationMessage;
                return false;
            }
            try
            {
                var parser = new ObjectDetectionGoodJudgementExpressionParser(
                    expression, context.StatisticsByRecord);
                value = parser.Parse();
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryEvaluateObjectDetectionGoodJudgementSpecification(
            string specification, double value, out bool passed)
        {
            passed = false;
            string normalized = Regex.Replace(specification ?? string.Empty, @"\s+", string.Empty);
            if (!ValidateObjectDetectionGoodJudgementSpecification(normalized))
            {
                return false;
            }
            string[] operands = Regex.Split(normalized, @"<=|>=|<|>");
            MatchCollection operators = Regex.Matches(normalized, @"<=|>=|<|>");
            if (operands.Length != operators.Count + 1 || operators.Count < 1 || operators.Count > 2)
            {
                return false;
            }

            var values = new double[operands.Length];
            for (int index = 0; index < operands.Length; index++)
            {
                if (string.Equals(operands[index], "x", StringComparison.OrdinalIgnoreCase))
                {
                    values[index] = value;
                }
                else if (!double.TryParse(operands[index], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out values[index]))
                {
                    return false;
                }
            }

            passed = true;
            for (int index = 0; index < operators.Count; index++)
            {
                string operation = operators[index].Value;
                bool comparison = operation == "<" ? values[index] < values[index + 1] :
                    operation == "<=" ? values[index] <= values[index + 1] :
                    operation == ">" ? values[index] > values[index + 1] :
                    values[index] >= values[index + 1];
                passed &= comparison;
            }
            return true;
        }

        private void AddObjectDetectionResultReviewDefectRows(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionSettings definition)
        {
            EnsureObjectDetectionDefectCores(parameter);
            bool allCoreResultsReady = true;
            int totalComponents = 0;
            foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores.Take(4))
            {
                bool required = string.Equals(core.CoreKey, "FlatField", StringComparison.Ordinal) || core.Enabled;
                if (!required)
                {
                    objectDetectionResultReviewDefectsGrid.Rows.Add(
                        GetObjectDetectionDefectCoreLabel(GetObjectDetectionDefectCoreIndex(core.CoreKey)),
                        "-", "-", "未啟用", "此條件未參與本次檢測");
                    continue;
                }

                ObjectDetectionDefectCoreResult result;
                if (!TryGetObjectDetectionDefectCoreResult(parameter, core.CoreKey, out result))
                {
                    allCoreResultsReady = false;
                    objectDetectionResultReviewDefectsGrid.Rows.Add(
                        GetObjectDetectionDefectCoreLabel(GetObjectDetectionDefectCoreIndex(core.CoreKey)),
                        "-", "-", "未完成", "請確認檢測範圍與平場校正值");
                    continue;
                }

                totalComponents += result.DetectedComponentCount;
                objectDetectionResultReviewDefectsGrid.Rows.Add(
                    GetObjectDetectionDefectCoreLabel(GetObjectDetectionDefectCoreIndex(core.CoreKey)),
                    result.DetectedComponentCount, result.TotalElapsedMilliseconds,
                    result.DetectedComponentCount > 0 ? "檢出缺陷" : "未檢出",
                    "OpenCV 元件數：" + result.DetectedComponentCount.ToString("N0", CultureInfo.CurrentCulture));
            }

            if (allCoreResultsReady)
            {
                int integratedCount = GetObjectDetectionDefectIntegrationGroups(parameter).Count;
                objectDetectionResultReviewDefectsGrid.Rows.Add(
                    "整合結果", integratedCount, string.Empty,
                    integratedCount > 0 ? "檢出缺陷" : "未檢出",
                    "各條件合併後的缺陷群數；單核心元件合計 " +
                    totalComponents.ToString("N0", CultureInfo.CurrentCulture));
            }
            else
            {
                objectDetectionResultReviewDefectsGrid.Rows.Add(
                    "整合結果", "-", string.Empty, "待確認", "尚有啟用核心未完成");
            }
        }

        private string BuildObjectDetectionResultReviewSummary(
            ObjectDetectionParameterSettings parameter, int objectCount)
        {
            bool sizeHasRules = parameter.GoodJudgementRules != null &&
                parameter.GoodJudgementRules.Any(rule => rule != null && rule.Enabled);
            bool sizeFailed = objectDetectionResultReviewGoodJudgementResults.Any(result =>
                result.Grade == ResultReviewGoodJudgementGrade.Ng);
            bool sizeUnknown = !sizeHasRules || objectDetectionResultReviewGoodJudgementResults.Count == 0 ||
                objectDetectionResultReviewGoodJudgementResults.Any(result =>
                    result.Grade == ResultReviewGoodJudgementGrade.Pending);
            bool hasDefectConfig = parameter.DefectInspectionRegionConfigured &&
                string.Equals(parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId, StringComparison.Ordinal);
            DataGridViewRow integratedRow = objectDetectionResultReviewDefectsGrid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(row => string.Equals(
                    Convert.ToString(row.Cells[0].Value, CultureInfo.CurrentCulture),
                    "整合結果", StringComparison.Ordinal));
            bool defectReady = integratedRow != null &&
                !string.Equals(Convert.ToString(integratedRow.Cells[3].Value, CultureInfo.CurrentCulture),
                    "待確認", StringComparison.Ordinal);
            int integratedCount;
            bool hasDefects = integratedRow != null &&
                int.TryParse(Convert.ToString(integratedRow.Cells[1].Value, CultureInfo.InvariantCulture),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out integratedCount) &&
                integratedCount > 0;

            string overall = sizeFailed || (defectReady && hasDefects) ? "不良" :
                sizeHasRules && !sizeUnknown && hasDefectConfig && defectReady && !hasDefects
                    ? "良品" : "待確認";
            int aGradeCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.A);
            int bGradeCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.B);
            int ngGradeCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.Ng);
            int pendingGradeCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.Pending);
            return "整體判定：" + overall + "　|　檢測參數：" + parameter.DisplayName +
                "　|　物件數：" + objectCount.ToString("N0", CultureInfo.CurrentCulture) +
                "　|　尺寸 A/B/NG/待確認：" +
                aGradeCount.ToString(CultureInfo.CurrentCulture) + "/" +
                bGradeCount.ToString(CultureInfo.CurrentCulture) + "/" +
                ngGradeCount.ToString(CultureInfo.CurrentCulture) + "/" +
                pendingGradeCount.ToString(CultureInfo.CurrentCulture) +
                "（" + (sizeFailed ? "NG" : sizeUnknown ? "待確認" : "符合") + "）" +
                "　|　缺陷：" + (!hasDefectConfig ? "未設定檢測範圍" : !defectReady
                    ? "待確認" : hasDefects ? "有缺陷" : "未檢出");
        }

        private static string FormatObjectDetectionReviewValue(double value)
        {
            return value.ToString("0.####", CultureInfo.CurrentCulture);
        }

        private void ApplyObjectDetectionResultReviewVerdictStyles()
        {
            ApplyObjectDetectionResultReviewGoodJudgementStyles();
            foreach (DataGridViewRow row in objectDetectionResultReviewDefectsGrid.Rows)
            {
                string verdict = Convert.ToString(row.Cells[3].Value, CultureInfo.CurrentCulture);
                row.DefaultCellStyle.ForeColor = verdict == "檢出缺陷" ? Color.Firebrick :
                    verdict == "未檢出" ? Color.ForestGreen : Color.FromArgb(75, 83, 95);
            }
        }

        private void SetObjectDetectionResultReviewControlsEnabled(bool enabled)
        {
            if (objectDetectionResultReviewParameterComboBox != null &&
                !objectDetectionResultReviewParameterComboBox.IsDisposed)
            {
                objectDetectionResultReviewParameterComboBox.Enabled = enabled &&
                    objectDetectionResultReviewParameterComboBox.Items.Count > 0;
            }
            if (objectDetectionResultReviewLoadImageButton != null &&
                !objectDetectionResultReviewLoadImageButton.IsDisposed)
            {
                objectDetectionResultReviewLoadImageButton.Enabled = enabled;
            }
            if (objectDetectionResultReviewRunButton != null &&
                !objectDetectionResultReviewRunButton.IsDisposed)
            {
                objectDetectionResultReviewRunButton.Enabled = enabled &&
                    objectDetectionResultReviewParameterComboBox != null &&
                    objectDetectionResultReviewParameterComboBox.SelectedIndex >= 0;
            }
        }

        private bool IsObjectDetectionResultReviewUiAvailable()
        {
            return isObjectDetectionResultReviewMode &&
                objectDetectionResultReviewPanel != null &&
                !objectDetectionResultReviewPanel.IsDisposed &&
                objectDetectionResultReviewStatusLabel != null &&
                !objectDetectionResultReviewStatusLabel.IsDisposed;
        }

        private void SetObjectDetectionResultReviewStatus(string text)
        {
            if (IsObjectDetectionResultReviewUiAvailable())
            {
                objectDetectionResultReviewStatusLabel.Text = text ?? string.Empty;
            }
        }

        private void ClearObjectDetectionResultReviewResults()
        {
            if (!IsObjectDetectionResultReviewUiAvailable())
            {
                return;
            }
            objectDetectionResultReviewMeasurementsGrid.Rows.Clear();
            objectDetectionResultReviewConditionsGrid.Rows.Clear();
            objectDetectionResultReviewDefectsGrid.Rows.Clear();
            ClearObjectDetectionResultReviewGoodJudgementState();
            objectDetectionResultReviewSelectedMeasurement = null;
            if (objectDetectionResultReviewClipLinesToMaskCheckBox != null)
            {
                objectDetectionResultReviewClipLinesToMaskCheckBox.Checked = false;
            }
            if (objectDetectionResultReviewHighlightTimer != null)
            {
                objectDetectionResultReviewHighlightTimer.Stop();
            }
            RefreshObjectDetectionMeasurementDisplay();
            objectDetectionResultReviewStatusLabel.Text = "尚未執行結果確認。";
        }

        private sealed class ObjectDetectionGoodJudgementExpressionParser
        {
            private readonly string expression;
            private readonly IDictionary<int, ObjectDetectionMeasurementStatistics> statisticsByRecord;
            private int position;

            public ObjectDetectionGoodJudgementExpressionParser(
                string expression,
                IDictionary<int, ObjectDetectionMeasurementStatistics> statisticsByRecord)
            {
                this.expression = Regex.Replace(expression ?? string.Empty, @"\s+", string.Empty);
                this.statisticsByRecord = statisticsByRecord;
            }

            public double Parse()
            {
                double value = ParseAdditive();
                if (position != expression.Length)
                {
                    throw new FormatException("計算式中有無法識別的內容。");
                }
                return value;
            }

            private double ParseAdditive()
            {
                double value = ParseMultiplicative();
                while (position < expression.Length &&
                    (expression[position] == '+' || expression[position] == '-'))
                {
                    char operation = expression[position++];
                    double right = ParseMultiplicative();
                    value = operation == '+' ? value + right : value - right;
                }
                return value;
            }

            private double ParseMultiplicative()
            {
                double value = ParseUnary();
                while (position < expression.Length &&
                    (expression[position] == '*' || expression[position] == '/' || expression[position] == '%'))
                {
                    char operation = expression[position++];
                    double right = ParseUnary();
                    if ((operation == '/' || operation == '%') && right == 0)
                    {
                        throw new DivideByZeroException("計算式不可除以 0。");
                    }
                    value = operation == '*' ? value * right :
                        operation == '/' ? value / right : value % right;
                }
                return value;
            }

            private double ParseUnary()
            {
                if (position < expression.Length && expression[position] == '+')
                {
                    position++;
                    return ParseUnary();
                }
                if (position < expression.Length && expression[position] == '-')
                {
                    position++;
                    return -ParseUnary();
                }
                return ParsePrimary();
            }

            private double ParsePrimary()
            {
                if (position >= expression.Length)
                {
                    throw new FormatException("計算式不完整。");
                }
                char current = expression[position];
                if (char.IsDigit(current) || current == '.')
                {
                    return ParseNumber();
                }
                if (current == '(')
                {
                    int number;
                    if (TryConsumeMeasurementReference(out number))
                    {
                        return ResolveStatistics(number).Average;
                    }
                    position++;
                    double nested = ParseAdditive();
                    Require(')');
                    return nested;
                }
                if (char.IsLetter(current))
                {
                    return ParseFunction();
                }
                throw new FormatException("計算式中有不支援的符號。");
            }

            private double ParseFunction()
            {
                int nameStart = position;
                while (position < expression.Length && char.IsLetter(expression[position]))
                {
                    position++;
                }
                string name = expression.Substring(nameStart, position - nameStart);
                if (!string.Equals(name, "min", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "avg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "max", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("不支援的函式：" + name);
                }
                Require('(');

                if (position < expression.Length && expression[position] == '(')
                {
                    var values = new List<double>();
                    do
                    {
                        values.Add(ParseAdditive());
                        if (position < expression.Length && expression[position] == ',')
                        {
                            position++;
                        }
                        else if (position >= expression.Length || expression[position] != '(')
                        {
                            break;
                        }
                    }
                    while (position < expression.Length);
                    Require(')');
                    if (string.Equals(name, "min", StringComparison.OrdinalIgnoreCase))
                    {
                        return values.Min();
                    }
                    if (string.Equals(name, "max", StringComparison.OrdinalIgnoreCase))
                    {
                        return values.Max();
                    }
                    return values.Average();
                }

                int start = position;
                while (position < expression.Length && char.IsDigit(expression[position]))
                {
                    position++;
                }
                int recordNumber;
                if (start == position ||
                    !int.TryParse(expression.Substring(start, position - start),
                        NumberStyles.None, CultureInfo.InvariantCulture, out recordNumber) ||
                    position >= expression.Length || expression[position] != ')')
                {
                    throw new FormatException(name + " 的用法為 " +
                        name.ToUpperInvariant() + "(量測編號)。");
                }
                position++;
                ObjectDetectionMeasurementStatistics statistics = ResolveStatistics(recordNumber);
                if (string.Equals(name, "min", StringComparison.OrdinalIgnoreCase))
                {
                    return statistics.Minimum;
                }
                if (string.Equals(name, "max", StringComparison.OrdinalIgnoreCase))
                {
                    return statistics.Maximum;
                }
                return statistics.Average;
            }

            private bool TryConsumeMeasurementReference(out int number)
            {
                number = 0;
                int end = position + 1;
                while (end < expression.Length && char.IsDigit(expression[end]))
                {
                    end++;
                }
                if (end == position + 1 || end >= expression.Length || expression[end] != ')' ||
                    !int.TryParse(expression.Substring(position + 1, end - position - 1),
                        NumberStyles.None, CultureInfo.InvariantCulture, out number))
                {
                    return false;
                }
                position = end + 1;
                return true;
            }

            private ObjectDetectionMeasurementStatistics ResolveStatistics(int number)
            {
                ObjectDetectionMeasurementStatistics statistics;
                if (statisticsByRecord == null || !statisticsByRecord.TryGetValue(number, out statistics))
                {
                    throw new InvalidOperationException(
                        "量測編號 " + number.ToString(CultureInfo.InvariantCulture) + " 沒有可用的量測值。");
                }
                return statistics;
            }

            private double ParseNumber()
            {
                int start = position;
                bool decimalPointSeen = false;
                while (position < expression.Length)
                {
                    char current = expression[position];
                    if (char.IsDigit(current))
                    {
                        position++;
                    }
                    else if (current == '.' && !decimalPointSeen)
                    {
                        decimalPointSeen = true;
                        position++;
                    }
                    else
                    {
                        break;
                    }
                }
                double value;
                if (!double.TryParse(expression.Substring(start, position - start),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    throw new FormatException("計算式中的數值格式不正確。");
                }
                return value;
            }

            private void Require(char expected)
            {
                if (position >= expression.Length || expression[position] != expected)
                {
                    throw new FormatException("計算式括號或參數格式不正確。");
                }
                position++;
            }
        }
    }
}
