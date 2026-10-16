using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private enum ResultReviewDefectGrade
        {
            Pending,
            A,
            Ng
        }

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

        private sealed class ResultReviewMeasurementRowResult
        {
            public ObjectDetectionMeasurementRecordSettings Record { get; set; }
            public ObjectDetectionParameterSettings MaskParameter { get; set; }
            public ObjectDefinitionDetectedObject DetectedObject { get; set; }
            public ObjectDetectionMeasurementStatistics Statistics { get; set; }
            public string Error { get; set; }
        }

        private sealed class ResultReviewMeasurementCalculationResult
        {
            public List<ResultReviewMeasurementRowResult> Rows { get; set; }
            public IDictionary<int, ResultReviewMeasurementContext> Contexts { get; set; }
            public long CalculationMilliseconds { get; set; }
        }

        private sealed class ObjectDetectionResultReviewSnapshot : IDisposable
        {
            public string ParameterId { get; set; }
            public string ImagePath { get; set; }
            public string SettingsSignature { get; set; }
            public int ImageContentGeneration { get; set; }
            public int ImageSourceGeneration { get; set; }
            public ObjectDetectionParameterSettings Parameter { get; set; }
            public ObjectDefinitionSettings Definition { get; set; }
            public List<ObjectDefinitionDetectedObject> Objects { get; set; }
            public Dictionary<string, ObjectDetectionDefectCoreResult> CoreResults { get; set; }
            public Dictionary<string, Cv.Mat> MeasurementMasks { get; set; }
            public ObjectDetectionFrequencyResult FrequencyResult { get; set; }
            public ObjectDetectionFrequencyResult Frequency2Result { get; set; }
            public List<ObjectDetectionDefectIntegrationGroup> IntegrationGroups { get; set; }
            public LargeImageSource OriginalLargeSource { get; set; }
            public LargeImageSource CorrectedLargeSource { get; set; }
            public Bitmap OriginalBitmap { get; set; }
            public Bitmap CorrectedBitmap { get; set; }
            public List<ObjectDetectionFlatFieldMaskOverlay> SourceMasks { get; set; }
            public List<ObjectDetectionFlatFieldMaskOverlay> UseMasks { get; set; }
            public bool UseMaskConfigured { get; set; }
            public int ImageWidth { get; set; }
            public int ImageHeight { get; set; }
            public ImageViewState MeasurementViewState { get; set; }
            public ImageViewState FlatFieldViewState { get; set; }
            public ImageViewState[] DefectViewStates { get; set; }

            public void Dispose()
            {
                if (CoreResults != null)
                {
                    DisposeObjectDetectionDefectCoreResults(CoreResults);
                    CoreResults = null;
                }
                if (FrequencyResult != null)
                {
                    DisposeObjectDetectionFrequencyResult(FrequencyResult);
                    FrequencyResult = null;
                }
                if (Frequency2Result != null)
                {
                    DisposeObjectDetectionFrequencyResult(Frequency2Result);
                    Frequency2Result = null;
                }
                if (MeasurementMasks != null)
                {
                    foreach (Cv.Mat mask in MeasurementMasks.Values)
                    {
                        if (mask != null)
                        {
                            mask.Dispose();
                        }
                    }
                    MeasurementMasks.Clear();
                    MeasurementMasks = null;
                }
                if (OriginalLargeSource != null)
                {
                    OriginalLargeSource.ReleaseReference();
                    OriginalLargeSource = null;
                }
                if (CorrectedLargeSource != null)
                {
                    CorrectedLargeSource.ReleaseReference();
                    CorrectedLargeSource = null;
                }
                if (OriginalBitmap != null)
                {
                    OriginalBitmap.Dispose();
                    OriginalBitmap = null;
                }
                if (CorrectedBitmap != null)
                {
                    CorrectedBitmap.Dispose();
                    CorrectedBitmap = null;
                }
                DisposeObjectDetectionFlatFieldMasks(SourceMasks);
                DisposeObjectDetectionFlatFieldMasks(UseMasks);
                SourceMasks = null;
                UseMasks = null;
            }
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
        private Panel objectDetectionResultReviewDefectObjectButtonsHost;
        private TableLayoutPanel objectDetectionResultReviewDefectObjectButtonsPanel;
        private int? objectDetectionResultReviewSelectedDefectObjectNumber;
        private bool isObjectDetectionResultReviewMode;
        private bool isObjectDetectionResultReviewRunning;
        private Size objectDetectionResultReviewReferenceImageSize;
        private string objectDetectionResultReviewImagePath;
        private int? objectDetectionResultReviewImageGeneration;
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
        private long? objectDetectionResultReviewConcurrentComputationMilliseconds;
        private long? objectDetectionResultReviewPostParallelCalculationMilliseconds;
        private Stopwatch objectDetectionResultReviewConcurrentComputationStopwatch;
        private string objectDetectionResultReviewActiveTimingStage;
        private ResultReviewMeasurementRowContext objectDetectionResultReviewSelectedMeasurement;
        private Timer objectDetectionResultReviewHighlightTimer;
        private string objectDetectionResultReviewPreviousParameterId;
        private int objectDetectionResultReviewPreviousObjectNumber;
        private string objectDetectionResultReviewSelectedParameterId;
        private ObjectDetectionResultReviewSnapshot objectDetectionResultReviewSnapshot;
        private string objectDetectionResultReviewLastSuccessfulSettingsSignature;
        private string objectDetectionResultReviewLastSuccessfulParameterId;
        private int? objectDetectionResultReviewLastSuccessfulImageGeneration;
        private bool isPopulatingObjectDetectionResultReviewParameters;
        private TabPage objectDetectionResultReviewSelectedImageTabPage;
        private readonly Dictionary<ImageDisplayControl, ImageViewState>
            objectDetectionResultReviewPendingViewRestores =
                new Dictionary<ImageDisplayControl, ImageViewState>();

        private void ShowObjectDetectionResultReviewPanel()
        {
            bool isRestoringExistingReviewPanel = objectDetectionResultReviewPanel != null &&
                !objectDetectionResultReviewPanel.IsDisposed;
            string previousReviewParameterId = objectDetectionResultReviewSelectedParameterId;
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
                string referencePath = objectDetectionResultReviewReferenceImageSize.IsEmpty
                    ? systemParameters.LastImagePath
                    : null;
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
                objectDetectionDefectDisplayTabPages[ObjectDetectionDefectIntegratedDisplayIndex].Text = "整合";
                leftImageTabControl.SelectedTab = objectDetectionResultReviewSelectedImageTabPage != null &&
                    leftImageTabControl.TabPages.Contains(objectDetectionResultReviewSelectedImageTabPage)
                        ? objectDetectionResultReviewSelectedImageTabPage
                        : objectDetectionMeasurementTabPage;
                objectDetectionMeasurementDisplayControl.TitleText = "左側 量測位置";
                objectDetectionFlatFieldPreviewDisplayControl.TitleText = "左側 平場校正";
                for (int index = 0; index < objectDetectionDefectDisplayControls.Length; index++)
                {
                    objectDetectionDefectDisplayControls[index].TitleText =
                        "左側 " + (index == ObjectDetectionDefectIntegratedDisplayIndex
                            ? "整合"
                            : ObjectDetectionDefectDisplayNames[index]);
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

            string currentImagePath = systemParameters.LastImagePath;
            bool hasCurrentImage = !string.IsNullOrWhiteSpace(currentImagePath) &&
                File.Exists(currentImagePath);
            bool hadReviewImage = !string.IsNullOrWhiteSpace(objectDetectionResultReviewImagePath);
            bool imageWasAddedWhileAway = isRestoringExistingReviewPanel &&
                !hadReviewImage && hasCurrentImage;
            bool imageChanged = hadReviewImage &&
                !string.Equals(
                    objectDetectionResultReviewImagePath,
                    currentImagePath,
                    StringComparison.OrdinalIgnoreCase);
            bool imageGenerationChanged = objectDetectionResultReviewImageGeneration.HasValue &&
                objectDetectionResultReviewImageGeneration.Value != imageContentGeneration;
            bool imageSourceChanged = imageWasAddedWhileAway || imageChanged || imageGenerationChanged;
            if (hasCurrentImage)
            {
                objectDetectionResultReviewImagePath = currentImagePath;
                if (objectDetectionResultReviewReferenceImageSize.IsEmpty || imageSourceChanged)
                {
                    try
                    {
                        objectDetectionResultReviewReferenceImageSize =
                            LargeImageSource.ReadImageSize(currentImagePath);
                    }
                    catch
                    {
                        objectDetectionResultReviewReferenceImageSize = Size.Empty;
                    }
                }
                if (imageSourceChanged)
                {
                    ClearObjectDetectionResultReviewResults();
                    ResetObjectDetectionResultReviewProcessingTimings();
                    objectDetectionResultReviewStatusLabel.Text =
                        "目前圖片已變更，請執行確認以更新結果。";
                }
            }

            PopulateObjectDetectionResultReviewParameters();
            bool parameterChangedWhileAway = !string.Equals(
                previousReviewParameterId,
                objectDetectionResultReviewSelectedParameterId,
                StringComparison.Ordinal);
            ObjectDetectionParameterSettings currentReviewParameter =
                FindObjectDetectionParameter(objectDetectionResultReviewSelectedParameterId);
            UpdateObjectDetectionDefectDisplayTabVisibility(currentReviewParameter);
            ObjectDefinitionSettings currentReviewDefinition = currentReviewParameter == null ||
                string.IsNullOrWhiteSpace(currentReviewParameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(currentReviewParameter.ObjectDefinitionId);
            bool snapshotSettingsChanged = objectDetectionResultReviewSnapshot != null &&
                (!string.Equals(
                    objectDetectionResultReviewSnapshot.SettingsSignature,
                    CreateObjectDetectionResultReviewSettingsSignature(
                        currentReviewParameter,
                        currentReviewDefinition),
                    StringComparison.Ordinal) ||
                 !string.Equals(
                    objectDetectionResultReviewSnapshot.ParameterId,
                    objectDetectionResultReviewSelectedParameterId,
                    StringComparison.Ordinal));
            if (snapshotSettingsChanged)
            {
                ClearObjectDetectionResultReviewResults();
                ResetObjectDetectionResultReviewProcessingTimings();
                objectDetectionResultReviewStatusLabel.Text =
                    "檢測參數內容已變更，請重新執行結果確認。";
            }
            ResultReviewGoodJudgementObject selectedGoodJudgementObject =
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber.HasValue
                    ? objectDetectionResultReviewGoodJudgementResults.FirstOrDefault(result =>
                        result.ObjectNumber == objectDetectionResultReviewSelectedGoodJudgementObjectNumber.Value)
                    : null;
            if (selectedGoodJudgementObject != null)
            {
                selectedObjectDetectionNumber = selectedGoodJudgementObject.ObjectNumber;
            }
            else
            {
                selectedObjectDetectionNumber = -1;
            }
            if (leftImageTabControl != null &&
                IsObjectDetectionDefectDisplayTab(leftImageTabControl.SelectedTab))
            {
                int selectedDefectDisplayIndex =
                    GetObjectDetectionDefectDisplayIndex(leftImageTabControl.SelectedTab);
                ImageDisplayControl selectedDefectDisplay =
                    GetObjectDetectionDefectDisplayControl(selectedDefectDisplayIndex);
                if (selectedDefectDisplay != null)
                {
                    selectedDefectDisplay.InvalidateImageView();
                }
            }

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
            if (objectDetectionResultReviewSnapshot != null && !imageSourceChanged &&
                !parameterChangedWhileAway && !snapshotSettingsChanged)
            {
                RestoreObjectDetectionResultReviewSnapshotDisplays();
            }
            else if (!isRestoringExistingReviewPanel || imageSourceChanged ||
                parameterChangedWhileAway || snapshotSettingsChanged)
            {
                RefreshObjectDetectionFlatFieldDisplay();
                RefreshObjectDetectionMeasurementDisplay();
                RefreshObjectDetectionDefectDisplay();
            }
            UpdateObjectDetectionResultReviewTimingMemo();
        }

        private void ExitObjectDetectionResultReviewMode()
        {
            if (!isObjectDetectionResultReviewMode)
            {
                return;
            }

            if (objectDetectionResultReviewPanel != null)
            {
                objectDetectionResultReviewPanel.Visible = false;
            }
            CaptureObjectDetectionResultReviewSnapshotViewStates();
            if (leftImageTabControl != null)
            {
                objectDetectionResultReviewSelectedImageTabPage = leftImageTabControl.SelectedTab;
            }

            objectDetectionMeasurementTabPage.Text = "待量測";
            objectDetectionDefectDisplayTabPages[ObjectDetectionDefectIntegratedDisplayIndex].Text =
                ObjectDetectionDefectDisplayNames[ObjectDetectionDefectIntegratedDisplayIndex];
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
            if (objectDetectionResultReviewHighlightTimer != null)
            {
                objectDetectionResultReviewHighlightTimer.Stop();
                objectDetectionResultReviewHighlightTimer.Dispose();
                objectDetectionResultReviewHighlightTimer = null;
            }
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
                Text = "選擇檢測參數並載入同規格圖片後，系統會自動執行尺寸與缺陷確認。",
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
                CreateObjectDetectionResultReviewTab(
                    "缺陷判定",
                    CreateObjectDetectionResultReviewDefectContent(objectDetectionResultReviewDefectsGrid)));

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

        private Control CreateObjectDetectionResultReviewDefectContent(DataGridView grid)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "物件序號：綠色 A 規　紅色 NG　灰色待確認（再點取消選取）",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                Padding = new Padding(4, 0, 2, 0),
                Margin = Padding.Empty
            }, 0, 0);

            objectDetectionResultReviewDefectObjectButtonsHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = Padding.Empty
            };
            objectDetectionResultReviewDefectObjectButtonsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                Height = 34,
                Margin = Padding.Empty,
                Padding = new Padding(2)
            };
            for (int column = 0; column < 3; column++)
            {
                objectDetectionResultReviewDefectObjectButtonsPanel.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            objectDetectionResultReviewDefectObjectButtonsHost.Controls.Add(
                objectDetectionResultReviewDefectObjectButtonsPanel);
            layout.Controls.Add(objectDetectionResultReviewDefectObjectButtonsHost, 0, 1);
            layout.Controls.Add(grid, 0, 2);
            return layout;
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

            string preferredId = string.IsNullOrWhiteSpace(objectDetectionResultReviewSelectedParameterId)
                ? activeObjectDetectionParameterId
                : objectDetectionResultReviewSelectedParameterId;
            string previousReviewParameterId = objectDetectionResultReviewSelectedParameterId;
            bool hadResults = objectDetectionResultReviewGoodJudgementResults.Count > 0 ||
                (objectDetectionResultReviewMeasurementsGrid != null &&
                 objectDetectionResultReviewMeasurementsGrid.Rows.Count > 0) ||
                (objectDetectionResultReviewConditionsGrid != null &&
                 objectDetectionResultReviewConditionsGrid.Rows.Count > 0) ||
                (objectDetectionResultReviewDefectsGrid != null &&
                 objectDetectionResultReviewDefectsGrid.Rows.Count > 0);
            isPopulatingObjectDetectionResultReviewParameters = true;
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
                if (selectedIndex >= 0)
                {
                    var targetChoice = objectDetectionResultReviewParameterComboBox.Items[selectedIndex]
                        as ResultReviewParameterChoice;
                    if (targetChoice != null && targetChoice.Parameter != null)
                    {
                        objectDetectionResultReviewSelectedParameterId = targetChoice.Parameter.Id;
                    }
                }
                objectDetectionResultReviewParameterComboBox.SelectedIndex = selectedIndex;
            }
            finally
            {
                objectDetectionResultReviewParameterComboBox.EndUpdate();
                isPopulatingObjectDetectionResultReviewParameters = false;
            }

            var selectedChoice = objectDetectionResultReviewParameterComboBox.SelectedItem
                as ResultReviewParameterChoice;
            if (selectedChoice != null && selectedChoice.Parameter != null)
            {
                objectDetectionResultReviewSelectedParameterId = selectedChoice.Parameter.Id;
                activeObjectDetectionParameterId = selectedChoice.Parameter.Id;
                if (hadResults && !string.Equals(
                    previousReviewParameterId,
                    selectedChoice.Parameter.Id,
                    StringComparison.Ordinal))
                {
                    ClearObjectDetectionResultReviewResults();
                    ResetObjectDetectionResultReviewProcessingTimings();
                    objectDetectionResultReviewStatusLabel.Text =
                        "檢測參數已變更，請重新執行結果確認。";
                }
            }

            if (objectDetectionResultReviewParameterComboBox.SelectedIndex < 0)
            {
                objectDetectionResultReviewStatusLabel.Text = "目前沒有檢測參數可供確認。";
                objectDetectionResultReviewRunButton.Enabled = false;
            }
        }

        private void ObjectDetectionResultReviewParameterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isPopulatingObjectDetectionResultReviewParameters)
            {
                return;
            }

            var choice = objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (choice == null || choice.Parameter == null)
            {
                return;
            }

            if (string.Equals(
                objectDetectionResultReviewSelectedParameterId,
                choice.Parameter.Id,
                StringComparison.Ordinal))
            {
                return;
            }

            objectDetectionResultReviewSelectedParameterId = choice.Parameter.Id;
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
                    : "目前參數：" + choice.Parameter.DisplayName + "；載入同規格圖片後會自動執行確認。";
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
                bool imageLoadedSuccessfully = false;
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
                    objectDetectionResultReviewStatusLabel.Text = "圖片已載入，準備依目前參數更新所有結果...";
                    RefreshObjectDetectionFlatFieldDisplay();
                    RefreshObjectDetectionMeasurementDisplay();
                    RefreshObjectDetectionDefectDisplay();
                    imageLoadedSuccessfully = true;
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

                if (imageLoadedSuccessfully && IsObjectDetectionResultReviewUiAvailable())
                {
                    objectDetectionResultReviewStatusLabel.Text =
                        "圖片已載入，正在使用目前選取的檢測參數執行尺寸與缺陷判定...";
                    await RunObjectDetectionResultReviewAsync();
                }
            }
        }

        private async Task RunObjectDetectionResultReviewAsync()
        {
            if (isObjectDetectionResultReviewRunning)
            {
                return;
            }

            int capturedRecipeGeneration = System.Threading.Interlocked.CompareExchange(
                ref detectionRecipeGeneration, 0, 0);
            int capturedImageGeneration = System.Threading.Interlocked.CompareExchange(
                ref imageSourceGeneration, 0, 0);
            System.Threading.CancellationToken cancellationToken =
                detectionRecipeCancellationTokenSource.Token;

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
            ObjectDetectionFrequencySettings preflightFrequency2 =
                parameter.DefectFrequency2Settings ?? new ObjectDetectionFrequencySettings();
            bool preflightFrequency1Enabled = parameter.DefectFrequencyEnabled &&
                (parameter.DefectIntegrationIncludeFrequency ||
                    parameter.DefectFrequencyNeighborComparisonEnabled);
            bool preflightFrequency2Enabled = preflightFrequency2.Enabled &&
                (parameter.DefectIntegrationIncludeFrequency2 ||
                    preflightFrequency2.NeighborComparisonEnabled);
            string roiGroupAssignmentError;
            if (!TryValidateObjectDetectionRoiGroupAssignments(
                parameter,
                null,
                true,
                preflightFrequency1Enabled,
                preflightFrequency2Enabled,
                out roiGroupAssignmentError))
            {
                objectDetectionResultReviewStatusLabel.Text = roiGroupAssignmentError;
                return;
            }

            string currentSettingsSignature =
                CreateObjectDetectionResultReviewSettingsSignature(parameter, definition);
            bool settingsChangedSinceLastSuccessfulRun =
                objectDetectionResultReviewLastSuccessfulImageGeneration.HasValue &&
                (objectDetectionResultReviewLastSuccessfulImageGeneration.Value != imageContentGeneration ||
                 !string.Equals(
                    objectDetectionResultReviewLastSuccessfulParameterId,
                    parameter.Id,
                    StringComparison.Ordinal) ||
                 !string.Equals(
                    objectDetectionResultReviewLastSuccessfulSettingsSignature,
                    currentSettingsSignature,
                    StringComparison.Ordinal));

            isObjectDetectionResultReviewRunning = true;
            SetObjectDetectionResultReviewControlsEnabled(false);
            ClearObjectDetectionResultReviewResults();
            ResetObjectDetectionResultReviewProcessingTimings();
            UpdateObjectDetectionResultReviewTimingMemo();
            LargeImageSource measurementLargeSource = null;
            Bitmap measurementOriginalImage = null;
            Task<ResultReviewMeasurementCalculationResult> measurementTask = null;
            try
            {
                activeObjectDetectionParameterId = parameter.Id;
                objectDetectionResultReviewSelectedParameterId = parameter.Id;
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
                        objectDetectionResultReviewDefinitionCompletion,
                        cancellationToken,
                        capturedRecipeGeneration);
                    EnsureDetectionRecipeGenerationIsCurrent(
                        capturedRecipeGeneration,
                        cancellationToken);
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
                EnsureObjectDetectionDefectCores(parameter);
                Dictionary<string, HashSet<int>> reviewRoiGroupMembers;
                if (!TryValidateObjectDetectionDefectRoiGroupMembers(
                    parameter,
                    parameter.DefectDetectionCores
                        .Take(ObjectDetectionDefectCoreKeys.Length)
                        .Where(core => core != null && core.Enabled),
                    preflightFrequency1Enabled,
                    preflightFrequency2Enabled,
                    objects,
                    out reviewRoiGroupMembers,
                    out roiGroupAssignmentError))
                {
                    objectDetectionResultReviewStatusLabel.Text = roiGroupAssignmentError;
                    return;
                }

                selectedObjectDetectionNumber = -1;
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
                    EnsureDetectionRecipeGenerationIsCurrent(
                        capturedRecipeGeneration,
                        cancellationToken);
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
                if (rightOriginalDisplayControl == null ||
                    !rightOriginalDisplayControl.HasImage)
                {
                    throw new InvalidOperationException("尚未載入結果確認用的原始影像。");
                }
                if (rightOriginalDisplayControl.IsLargeImageMode)
                {
                    measurementLargeSource =
                        rightOriginalDisplayControl.GetSharedLargeImageSource();
                }
                else
                {
                    measurementOriginalImage = rightOriginalDisplayControl.CloneImage();
                }
                if (measurementLargeSource == null && measurementOriginalImage == null)
                {
                    throw new InvalidOperationException("無法固定結果確認用的原始影像來源。");
                }

                objectDetectionResultReviewStatusLabel.Text =
                    "平場校正完成，尺寸量測將依物件平行處理，並與缺陷檢測同時運算...";
                statusLabel.Text = parameter.DisplayName + " 結果確認：缺陷與尺寸並行運算中...";

                leftImageTabControl.SelectedTab =
                    objectDetectionDefectDisplayTabPages[ObjectDetectionDefectIntegratedDisplayIndex];
                objectDetectionResultReviewConcurrentComputationStopwatch = Stopwatch.StartNew();
                StartObjectDetectionResultReviewTimingStage("尺寸量測與缺陷檢測並行運算");
                measurementTask = Task.Run(
                    () => CalculateObjectDetectionResultReviewMeasurements(
                        parameter,
                        objects,
                        measurementLargeSource,
                        measurementOriginalImage,
                        cancellationToken,
                        capturedRecipeGeneration,
                        capturedImageGeneration));
                bool includeFrequency = parameter.DefectFrequencyEnabled &&
                    (parameter.DefectIntegrationIncludeFrequency ||
                        parameter.DefectFrequencyNeighborComparisonEnabled);
                ObjectDetectionFrequencySettings frequency2Settings =
                    parameter.DefectFrequency2Settings ?? new ObjectDetectionFrequencySettings();
                bool includeFrequency2 = frequency2Settings.Enabled &&
                    (parameter.DefectIntegrationIncludeFrequency2 ||
                        frequency2Settings.NeighborComparisonEnabled);
                ObjectDetectionFrequencyResult frequencyResult;
                ObjectDetectionFrequencyResult frequency2Result;
                bool frequencyAlreadyReady = !includeFrequency ||
                    !settingsChangedSinceLastSuccessfulRun &&
                    TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult);
                bool frequency2AlreadyReady = !includeFrequency2 ||
                    !settingsChangedSinceLastSuccessfulRun &&
                    TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequency2Result, 1);

                if (parameter.DefectParallelExecutionEnabled)
                {
                    objectDetectionResultReviewStatusLabel.Text =
                        "正在平行執行一般缺陷條件" +
                        ((includeFrequency && !frequencyAlreadyReady) ||
                            (includeFrequency2 && !frequency2AlreadyReady)
                            ? "、頻域異常分析" : string.Empty) +
                        "...";
                    Stopwatch parallelDefectStopwatch = Stopwatch.StartNew();
                    var defectTasks = new List<Task>
                    {
                        RunObjectDetectionDefectProcessingAsync(
                            parameter.Id,
                            null,
                            null,
                            elapsed =>
                            {
                                objectDetectionResultReviewDefectDisplayMilliseconds = elapsed;
                                UpdateObjectDetectionResultReviewTimingMemo();
                            },
                            true)
                    };
                    if (includeFrequency && !frequencyAlreadyReady)
                    {
                        defectTasks.Add(RunObjectDetectionFrequencyAnalysisAsync(
                            parameter.Id,
                            true,
                            includeIndependentCoreDefects: false,
                            frequencyIndex: 0));
                    }
                    if (includeFrequency2 && !frequency2AlreadyReady)
                    {
                        defectTasks.Add(RunObjectDetectionFrequencyAnalysisAsync(
                            parameter.Id,
                            true,
                            includeIndependentCoreDefects: false,
                            frequencyIndex: 1));
                    }
                    await Task.WhenAll(defectTasks);
                    EnsureDetectionRecipeGenerationIsCurrent(
                        capturedRecipeGeneration,
                        cancellationToken);
                    parallelDefectStopwatch.Stop();
                    if (!IsObjectDetectionResultReviewUiAvailable())
                    {
                        await measurementTask;
                        return;
                    }
                    objectDetectionResultReviewDefectMilliseconds =
                        parallelDefectStopwatch.ElapsedMilliseconds;
                    UpdateObjectDetectionResultReviewTimingMemo();
                    if (includeFrequency || includeFrequency2)
                    {
                        leftImageTabControl.SelectedTab =
                            objectDetectionDefectDisplayTabPages[ObjectDetectionDefectIntegratedDisplayIndex];
                        InvalidateObjectDetectionDefectIntegrationDisplay();
                    }
                }
                else
                {
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
                        },
                        true);
                    EnsureDetectionRecipeGenerationIsCurrent(
                        capturedRecipeGeneration,
                        cancellationToken);
                    if (!IsObjectDetectionResultReviewUiAvailable())
                    {
                        await measurementTask;
                        return;
                    }

                    if (includeFrequency || includeFrequency2)
                    {
                        if (!frequencyAlreadyReady)
                        {
                            objectDetectionResultReviewStatusLabel.Text =
                                "一般缺陷條件完成，正在執行納入整合的頻域異常分析...";
                            await RunObjectDetectionFrequencyAnalysisAsync(
                                parameter.Id,
                                true,
                                includeIndependentCoreDefects: false,
                                frequencyIndex: 0);
                            EnsureDetectionRecipeGenerationIsCurrent(
                                capturedRecipeGeneration,
                                cancellationToken);
                            if (!IsObjectDetectionResultReviewUiAvailable())
                            {
                                await measurementTask;
                                return;
                            }
                            if (TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult))
                            {
                                objectDetectionResultReviewDefectMilliseconds =
                                    (objectDetectionResultReviewDefectMilliseconds ?? 0) +
                                    frequencyResult.TotalElapsedMilliseconds;
                                UpdateObjectDetectionResultReviewTimingMemo();
                            }
                        }
                        if (includeFrequency2 && !frequency2AlreadyReady)
                        {
                            objectDetectionResultReviewStatusLabel.Text =
                                "一般缺陷條件完成，正在執行頻域異常二...";
                            await RunObjectDetectionFrequencyAnalysisAsync(
                                parameter.Id,
                                true,
                                includeIndependentCoreDefects: false,
                                frequencyIndex: 1);
                            EnsureDetectionRecipeGenerationIsCurrent(
                                capturedRecipeGeneration,
                                cancellationToken);
                            if (!IsObjectDetectionResultReviewUiAvailable())
                            {
                                await measurementTask;
                                return;
                            }
                            if (TryGetCurrentObjectDetectionFrequencyResult(
                                parameter,
                                out frequency2Result,
                                1))
                            {
                                objectDetectionResultReviewDefectMilliseconds =
                                    (objectDetectionResultReviewDefectMilliseconds ?? 0) +
                                    frequency2Result.TotalElapsedMilliseconds;
                                UpdateObjectDetectionResultReviewTimingMemo();
                            }
                        }
                        leftImageTabControl.SelectedTab =
                            objectDetectionDefectDisplayTabPages[ObjectDetectionDefectIntegratedDisplayIndex];
                        InvalidateObjectDetectionDefectIntegrationDisplay();
                    }
                }

                if (includeFrequency && parameter.DefectFrequencyNeighborComparisonEnabled &&
                    TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult))
                {
                    Stopwatch neighborComparisonStopwatch = Stopwatch.StartNew();
                    CalculateObjectDetectionFrequencyNeighborComparison(
                        parameter,
                        frequencyResult,
                        true);
                    neighborComparisonStopwatch.Stop();
                    frequencyResult.NeighborComparisonMilliseconds +=
                        neighborComparisonStopwatch.ElapsedMilliseconds;
                    frequencyResult.TotalElapsedMilliseconds +=
                        neighborComparisonStopwatch.ElapsedMilliseconds;
                    UpdateObjectDetectionFrequencyResultSummary(frequencyResult);
                    InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
                    RefreshObjectDetectionDefectIntegrationResults(parameter);
                    InvalidateObjectDetectionDefectIntegrationDisplay();
                    ImageDisplayControl frequencyDisplay = GetObjectDetectionDefectDisplayControl(
                        ObjectDetectionDefectFrequencyDisplayIndex);
                    if (frequencyDisplay != null)
                    {
                        frequencyDisplay.InvalidateImageView();
                    }
                }
                if (includeFrequency2 && frequency2Settings.NeighborComparisonEnabled &&
                    TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequency2Result, 1))
                {
                    Stopwatch neighborComparisonStopwatch = Stopwatch.StartNew();
                    CalculateObjectDetectionFrequencyNeighborComparison(
                        CreateObjectDetectionFrequencyParameterView(parameter, 1),
                        frequency2Result,
                        true);
                    neighborComparisonStopwatch.Stop();
                    frequency2Result.NeighborComparisonMilliseconds +=
                        neighborComparisonStopwatch.ElapsedMilliseconds;
                    frequency2Result.TotalElapsedMilliseconds +=
                        neighborComparisonStopwatch.ElapsedMilliseconds;
                    UpdateObjectDetectionFrequencyResultSummary(frequency2Result, 1);
                    InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
                    RefreshObjectDetectionDefectIntegrationResults(parameter);
                    InvalidateObjectDetectionDefectIntegrationDisplay();
                    ImageDisplayControl frequencyDisplay = GetObjectDetectionDefectDisplayControl(
                        ObjectDetectionDefectFrequency2DisplayIndex);
                    if (frequencyDisplay != null)
                    {
                        frequencyDisplay.InvalidateImageView();
                    }
                }

                ResultReviewMeasurementCalculationResult measurementCalculation =
                    await measurementTask;
                EnsureDetectionRecipeGenerationIsCurrent(
                    capturedRecipeGeneration,
                    cancellationToken);
                if (capturedImageGeneration != System.Threading.Interlocked.CompareExchange(
                    ref imageSourceGeneration, 0, 0))
                {
                    throw new InvalidOperationException("結果確認期間原始影像已變更，請重新執行確認。");
                }
                if (objectDetectionResultReviewConcurrentComputationStopwatch != null)
                {
                    objectDetectionResultReviewConcurrentComputationStopwatch.Stop();
                    objectDetectionResultReviewConcurrentComputationMilliseconds =
                        objectDetectionResultReviewConcurrentComputationStopwatch.ElapsedMilliseconds;
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    StopObjectDetectionResultReviewTimingStage();
                    return;
                }
                objectDetectionResultReviewStatusLabel.Text = "尺寸與缺陷計算完成，正在更新判定結果...";
                long measurementDisplayMilliseconds;
                long judgementCalculationMilliseconds =
                    ApplyObjectDetectionResultReviewMeasurements(
                        parameter,
                        definition,
                        objects,
                        measurementCalculation,
                        out measurementDisplayMilliseconds);
                objectDetectionResultReviewMeasurementMilliseconds =
                    measurementCalculation.CalculationMilliseconds + judgementCalculationMilliseconds;
                objectDetectionResultReviewPostParallelCalculationMilliseconds =
                    judgementCalculationMilliseconds;
                objectDetectionResultReviewMeasurementDisplayMilliseconds =
                    (objectDetectionResultReviewMeasurementDisplayMilliseconds ?? 0) +
                    measurementDisplayMilliseconds;
                UpdateObjectDetectionResultReviewTimingMemo();
                StopObjectDetectionResultReviewTimingStage();
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
                objectDetectionResultReviewImageGeneration = imageContentGeneration;
                CaptureObjectDetectionResultReviewSnapshot(parameter, definition, objects);
                RestoreObjectDetectionResultReviewSnapshotDisplays();
                objectDetectionResultReviewLastSuccessfulSettingsSignature =
                    currentSettingsSignature;
                objectDetectionResultReviewLastSuccessfulParameterId = parameter.Id;
                objectDetectionResultReviewLastSuccessfulImageGeneration = imageContentGeneration;
                statusLabel.Text = parameter.DisplayName + " 結果確認完成";
            }
            catch (Exception exception)
            {
                if (measurementTask != null)
                {
                    try
                    {
                        await measurementTask;
                    }
                    catch
                    {
                    }
                }
                StopObjectDetectionResultReviewTimingStage();
                if (!cancellationToken.IsCancellationRequested &&
                    capturedRecipeGeneration == System.Threading.Interlocked.CompareExchange(
                        ref detectionRecipeGeneration, 0, 0))
                {
                    SetObjectDetectionResultReviewStatus("結果確認未完成：" + exception.Message);
                    statusLabel.Text = "結果確認未完成：" + exception.Message;
                }
            }
            finally
            {
                if (objectDetectionResultReviewConcurrentComputationStopwatch != null &&
                    objectDetectionResultReviewConcurrentComputationStopwatch.IsRunning)
                {
                    objectDetectionResultReviewConcurrentComputationStopwatch.Stop();
                    objectDetectionResultReviewConcurrentComputationMilliseconds =
                        objectDetectionResultReviewConcurrentComputationStopwatch.ElapsedMilliseconds;
                }
                if (measurementOriginalImage != null)
                {
                    measurementOriginalImage.Dispose();
                }
                if (measurementLargeSource != null)
                {
                    measurementLargeSource.ReleaseReference();
                }
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
            objectDetectionResultReviewConcurrentComputationMilliseconds = null;
            objectDetectionResultReviewPostParallelCalculationMilliseconds = null;
            objectDetectionResultReviewConcurrentComputationStopwatch = null;
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

            long parallelStageMilliseconds = objectDetectionResultReviewConcurrentComputationMilliseconds ??
                (objectDetectionResultReviewConcurrentComputationStopwatch != null &&
                 objectDetectionResultReviewConcurrentComputationStopwatch.IsRunning
                    ? objectDetectionResultReviewConcurrentComputationStopwatch.ElapsedMilliseconds
                    : Math.Max(
                        objectDetectionResultReviewMeasurementMilliseconds.GetValueOrDefault(),
                        objectDetectionResultReviewDefectMilliseconds.GetValueOrDefault()));
            long completedMilliseconds =
                (objectDetectionResultReviewImageProcessingMilliseconds ?? 0) +
                parallelStageMilliseconds +
                objectDetectionResultReviewPostParallelCalculationMilliseconds.GetValueOrDefault();
            bool allStagesComplete = objectDetectionResultReviewImageProcessingMilliseconds.HasValue &&
                objectDetectionResultReviewMeasurementMilliseconds.HasValue &&
                objectDetectionResultReviewDefectMilliseconds.HasValue &&
                objectDetectionResultReviewConcurrentComputationMilliseconds.HasValue &&
                objectDetectionResultReviewPostParallelCalculationMilliseconds.HasValue;
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
            AppendDebugTimingMemo("  尺寸/缺陷並行牆鐘：" +
                (objectDetectionResultReviewConcurrentComputationMilliseconds.HasValue
                    ? objectDetectionResultReviewConcurrentComputationMilliseconds.Value.ToString(
                        "N0", CultureInfo.CurrentCulture) + " ms"
                    : objectDetectionResultReviewConcurrentComputationStopwatch != null &&
                      objectDetectionResultReviewConcurrentComputationStopwatch.IsRunning
                        ? objectDetectionResultReviewConcurrentComputationStopwatch.ElapsedMilliseconds
                            .ToString("N0", CultureInfo.CurrentCulture) + " ms（執行中）"
                        : "尚未執行"));

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
                : string.Equals(objectDetectionResultReviewActiveTimingStage,
                    "尺寸量測與缺陷檢測並行運算", StringComparison.Ordinal) &&
                  (string.Equals(stage, "尺寸量測計算", StringComparison.Ordinal) ||
                   string.Equals(stage, "缺陷檢測計算", StringComparison.Ordinal))
                    ? "平行運算中..."
                : "尚未執行";
        }

        private async Task WaitForObjectDetectionResultReviewDefinitionAsync(
            ObjectDefinitionSettings definition,
            string signature,
            TaskCompletionSource<string> completion,
            System.Threading.CancellationToken cancellationToken,
            int recipeGeneration)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (!HasCompletedObjectDefinitionResult(definition, signature))
            {
                EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
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
                await Task.WhenAny(
                    completion.Task,
                    Task.Delay(100, cancellationToken));
            }
        }

        private void EnsureObjectDetectionResultReviewImageGenerationIsCurrent(int generation)
        {
            if (generation != System.Threading.Interlocked.CompareExchange(
                ref imageSourceGeneration, 0, 0))
            {
                throw new InvalidOperationException("結果確認期間原始影像已變更，請重新執行確認。");
            }
        }

        private ResultReviewMeasurementCalculationResult CalculateObjectDetectionResultReviewMeasurements(
            ObjectDetectionParameterSettings parameter,
            IList<ObjectDefinitionDetectedObject> objects,
            LargeImageSource largeSource,
            Bitmap original,
            System.Threading.CancellationToken cancellationToken,
            int recipeGeneration,
            int imageGeneration)
        {
            List<ObjectDetectionMeasurementRecordSettings> records = (parameter.MeasurementRecords ??
                new List<ObjectDetectionMeasurementRecordSettings>())
                .Where(record => record != null).OrderBy(record => record.Number).ToList();
            var result = new ResultReviewMeasurementCalculationResult
            {
                Rows = new List<ResultReviewMeasurementRowResult>(),
                Contexts = new Dictionary<int, ResultReviewMeasurementContext>()
            };
            var rowsByObject = new List<ResultReviewMeasurementRowResult>[objects.Count];
            var contextsByObject = new ResultReviewMeasurementContext[objects.Count];
            int maxDegreeOfParallelism = Math.Min(
                objects.Count,
                Math.Max(1, Math.Min(4, Environment.ProcessorCount)));
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            Stopwatch calculationStopwatch = Stopwatch.StartNew();
            Parallel.For(0, objects.Count, parallelOptions, objectIndex =>
            {
                ObjectDefinitionDetectedObject detectedObject = objects[objectIndex];
                EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
                EnsureObjectDetectionResultReviewImageGenerationIsCurrent(imageGeneration);
                var statsByRecord = new Dictionary<int, ObjectDetectionMeasurementStatistics>();
                var objectRows = new List<ResultReviewMeasurementRowResult>(records.Count);
                foreach (ObjectDetectionMeasurementRecordSettings record in records)
                {
                    EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
                    EnsureObjectDetectionResultReviewImageGenerationIsCurrent(imageGeneration);
                    ObjectDetectionParameterSettings maskParameter =
                        CreateObjectDetectionResultReviewMaskParameter(parameter, record);
                    ObjectDetectionMeasurementStatistics statistics = null;
                    string error;
                    bool succeeded;
                    try
                    {
                        succeeded = TryCalculateObjectDetectionMeasurementRecord(
                            parameter,
                            record,
                            detectedObject,
                            maskParameter,
                            largeSource,
                            original,
                            out statistics,
                            out error,
                            true,
                            false);
                    }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                        succeeded = false;
                    }
                    EnsureObjectDetectionResultReviewImageGenerationIsCurrent(imageGeneration);

                    if (succeeded && statistics != null)
                    {
                        statsByRecord[record.Number] = statistics;
                    }
                    objectRows.Add(new ResultReviewMeasurementRowResult
                    {
                        Record = record,
                        MaskParameter = maskParameter,
                        DetectedObject = detectedObject,
                        Statistics = succeeded ? statistics : null,
                        Error = succeeded ? string.Empty : error
                    });
                }

                rowsByObject[objectIndex] = objectRows;
                contextsByObject[objectIndex] = new ResultReviewMeasurementContext
                {
                    Parameter = parameter,
                    StatisticsByRecord = statsByRecord
                };
            });

            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                result.Rows.AddRange(rowsByObject[objectIndex]);
                result.Contexts[objects[objectIndex].Number] = contextsByObject[objectIndex];
            }

            calculationStopwatch.Stop();
            result.CalculationMilliseconds = calculationStopwatch.ElapsedMilliseconds;
            return result;
        }

        private long ApplyObjectDetectionResultReviewMeasurements(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionSettings definition,
            IList<ObjectDefinitionDetectedObject> objects,
            ResultReviewMeasurementCalculationResult calculation,
            out long displayMilliseconds)
        {
            Stopwatch displayStopwatch = Stopwatch.StartNew();
            objectDetectionResultReviewMeasurementsGrid.Rows.Clear();
            objectDetectionResultReviewConditionsGrid.Rows.Clear();
            objectDetectionResultReviewDefectsGrid.Rows.Clear();
            foreach (ResultReviewMeasurementRowResult row in calculation.Rows)
            {
                ObjectDetectionMeasurementRecordSettings record = row.Record;
                ObjectDetectionMeasurementStatistics statistics = row.Statistics;
                int rowIndex;
                if (statistics != null)
                {
                    rowIndex = objectDetectionResultReviewMeasurementsGrid.Rows.Add(
                        row.DetectedObject.Number, record.Number, record.Name,
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
                }
                else
                {
                    rowIndex = objectDetectionResultReviewMeasurementsGrid.Rows.Add(
                        row.DetectedObject.Number, record.Number, record.Name,
                        string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                            ? "平行線" : "單線",
                        string.Equals(record.Direction, "Vertical", StringComparison.OrdinalIgnoreCase)
                            ? "垂直" : "水平",
                        string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                            ? Math.Max(2, record.LineCount).ToString(CultureInfo.CurrentCulture) : "1",
                        string.Empty, string.Empty, string.Empty,
                        IsObjectDetectionCameraPrecisionEnabled(parameter) ? "mm" : "px",
                        "無法量測：" + row.Error);
                }
                objectDetectionResultReviewMeasurementsGrid.Rows[rowIndex].Tag =
                    new ResultReviewMeasurementRowContext
                    {
                        Parameter = parameter,
                        Record = record,
                        MaskParameter = row.MaskParameter,
                        DetectedObject = row.DetectedObject,
                        ObjectNumber = row.DetectedObject.Number,
                        Statistics = statistics
                    };
            }
            displayStopwatch.Stop();

            Stopwatch conditionStopwatch = Stopwatch.StartNew();
            CalculateObjectDetectionResultReviewGoodJudgements(
                parameter, objects, calculation.Contexts);
            conditionStopwatch.Stop();
            Stopwatch resultDisplayStopwatch = Stopwatch.StartNew();
            RefreshObjectDetectionResultReviewGoodJudgementView();
            AddObjectDetectionResultReviewDefectRows(parameter, definition);
            ApplyObjectDetectionResultReviewVerdictStyles();
            resultDisplayStopwatch.Stop();
            displayMilliseconds = displayStopwatch.ElapsedMilliseconds +
                resultDisplayStopwatch.ElapsedMilliseconds;
            return conditionStopwatch.ElapsedMilliseconds;
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
            RefreshObjectDetectionResultReviewDefectObjectButtons();
            RenderObjectDetectionResultReviewDefectRows(
                parameter,
                objectDetectionResultReviewSelectedDefectObjectNumber);
        }

        private void RefreshObjectDetectionResultReviewDefectObjectButtons()
        {
            if (objectDetectionResultReviewDefectObjectButtonsPanel == null ||
                objectDetectionResultReviewDefectObjectButtonsPanel.IsDisposed)
            {
                return;
            }

            objectDetectionResultReviewDefectObjectButtonsPanel.SuspendLayout();
            try
            {
                foreach (Control control in objectDetectionResultReviewDefectObjectButtonsPanel.Controls
                    .Cast<Control>().ToList())
                {
                    objectDetectionResultReviewDefectObjectButtonsPanel.Controls.Remove(control);
                    control.Dispose();
                }
                objectDetectionResultReviewDefectObjectButtonsPanel.RowStyles.Clear();
                objectDetectionResultReviewDefectObjectButtonsPanel.ColumnStyles.Clear();

                List<ResultReviewGoodJudgementObject> objects =
                    objectDetectionResultReviewGoodJudgementResults;
                int columnCount = Math.Max(1, objects
                    .Select(item => item.GridColumn).DefaultIfEmpty(0).Max() + 1);
                int rowCount = Math.Max(1, objects
                    .Select(item => item.GridRow).DefaultIfEmpty(0).Max() + 1);
                Dictionary<int, ResultReviewDefectGrade> defectGrades =
                    GetObjectDetectionResultReviewDefectObjectGrades();
                objectDetectionResultReviewDefectObjectButtonsPanel.ColumnCount = columnCount;
                objectDetectionResultReviewDefectObjectButtonsPanel.RowCount = rowCount;
                objectDetectionResultReviewDefectObjectButtonsPanel.Height = rowCount * 34 + 4;
                for (int column = 0; column < columnCount; column++)
                {
                    objectDetectionResultReviewDefectObjectButtonsPanel.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100F / columnCount));
                }
                for (int row = 0; row < rowCount; row++)
                {
                    objectDetectionResultReviewDefectObjectButtonsPanel.RowStyles.Add(
                        new RowStyle(SizeType.Absolute, 34F));
                }

                for (int index = 0; index < objects.Count; index++)
                {
                    int objectNumber = objects[index].ObjectNumber;
                    ResultReviewDefectGrade grade;
                    if (!defectGrades.TryGetValue(objectNumber, out grade))
                    {
                        grade = ResultReviewDefectGrade.Pending;
                    }
                    var button = new Button
                    {
                        Text = objectNumber.ToString(CultureInfo.CurrentCulture),
                        Tag = objectNumber,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(2),
                        FlatStyle = FlatStyle.Flat,
                        UseVisualStyleBackColor = false,
                        BackColor = GetObjectDetectionResultReviewDefectGradeColor(grade),
                        ForeColor = Color.White,
                        AccessibleName = "物件 " + objectNumber.ToString(CultureInfo.CurrentCulture),
                        AccessibleDescription = GetObjectDetectionResultReviewDefectGradeText(grade)
                    };
                    bool selected = objectDetectionResultReviewSelectedDefectObjectNumber == objectNumber;
                    button.FlatAppearance.BorderColor = selected
                        ? Color.DodgerBlue
                        : Color.FromArgb(190, 195, 202);
                    button.FlatAppearance.BorderSize = selected ? 3 : 1;
                    button.Click += ObjectDetectionResultReviewDefectObjectButton_Click;
                    objectDetectionResultReviewDefectObjectButtonsPanel.Controls.Add(
                        button,
                        objects[index].GridColumn,
                        objects[index].GridRow);
                }
            }
            finally
            {
                objectDetectionResultReviewDefectObjectButtonsPanel.ResumeLayout(true);
            }
        }

        private Dictionary<int, ResultReviewDefectGrade>
            GetObjectDetectionResultReviewDefectObjectGrades()
        {
            var grades = objectDetectionResultReviewGoodJudgementResults
                .ToDictionary(
                    result => result.ObjectNumber,
                    result => ResultReviewDefectGrade.Pending);
            var choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            ObjectDetectionParameterSettings parameter = choice == null ? null : choice.Parameter;
            if (parameter == null ||
                !parameter.DefectInspectionRegionConfigured ||
                !string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal))
            {
                return grades;
            }

            EnsureObjectDetectionDefectCores(parameter);
            bool hasEnabledAnalysis = false;
            foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores
                .Take(ObjectDetectionDefectCoreKeys.Length))
            {
                if (core == null || !core.Enabled)
                {
                    continue;
                }

                hasEnabledAnalysis = true;
                if (!core.DarkThresholdEnabled && !core.BrightThresholdEnabled)
                {
                    return grades;
                }

                ObjectDetectionDefectCoreResult coreResult;
                if (!TryGetObjectDetectionDefectCoreResult(parameter, core.CoreKey, out coreResult) ||
                    coreResult == null)
                {
                    return grades;
                }
            }

            if (IsObjectDetectionDefectFrequencyIntegrationRequired(parameter, 0))
            {
                hasEnabledAnalysis = true;
                ObjectDetectionFrequencyResult frequencyResult;
                if (!TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult) ||
                    frequencyResult == null)
                {
                    return grades;
                }
            }

            if (IsObjectDetectionDefectFrequencyIntegrationRequired(parameter, 1))
            {
                hasEnabledAnalysis = true;
                ObjectDetectionFrequencyResult frequencyResult;
                if (!TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult, 1) ||
                    frequencyResult == null)
                {
                    return grades;
                }
            }

            if (!hasEnabledAnalysis)
            {
                return grades;
            }

            HashSet<int> defectiveObjects = new HashSet<int>(
                GetObjectDetectionDefectIntegrationGroups(parameter)
                    .Where(group => group != null)
                    .Select(group => group.ObjectNumber));
            foreach (int objectNumber in grades.Keys.ToList())
            {
                grades[objectNumber] = defectiveObjects.Contains(objectNumber)
                    ? ResultReviewDefectGrade.Ng
                    : ResultReviewDefectGrade.A;
            }

            return grades;
        }

        private static Color GetObjectDetectionResultReviewDefectGradeColor(
            ResultReviewDefectGrade grade)
        {
            switch (grade)
            {
                case ResultReviewDefectGrade.A:
                    return Color.FromArgb(46, 125, 50);
                case ResultReviewDefectGrade.Ng:
                    return Color.FromArgb(198, 40, 40);
                default:
                    return Color.FromArgb(117, 117, 117);
            }
        }

        private static string GetObjectDetectionResultReviewDefectGradeText(
            ResultReviewDefectGrade grade)
        {
            switch (grade)
            {
                case ResultReviewDefectGrade.A:
                    return "A 規：未檢出整合缺陷";
                case ResultReviewDefectGrade.Ng:
                    return "NG：檢出整合缺陷";
                default:
                    return "待確認：缺陷範圍、有效門檻或必要分析尚未完成";
            }
        }

        private void ObjectDetectionResultReviewDefectObjectButton_Click(object sender, EventArgs e)
        {
            if (isObjectDetectionResultReviewRunning)
            {
                return;
            }

            var button = sender as Button;
            var choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (button == null || !(button.Tag is int) || choice == null || choice.Parameter == null)
            {
                return;
            }

            int objectNumber = (int)button.Tag;
            bool clearSelection = objectDetectionResultReviewSelectedDefectObjectNumber == objectNumber;
            if (clearSelection)
            {
                objectDetectionResultReviewSelectedDefectObjectNumber = null;
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber = null;
                selectedObjectDetectionNumber = -1;
                ClearObjectDetectionResultReviewMeasurementHighlight();
            }
            else
            {
                objectDetectionResultReviewSelectedDefectObjectNumber = objectNumber;
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber = objectNumber;
                selectedObjectDetectionNumber = objectNumber;
                ClearObjectDetectionResultReviewMeasurementHighlight();
            }

            RefreshObjectDetectionResultReviewDefectObjectButtons();
            RefreshObjectDetectionResultReviewGoodJudgementView();
            RenderObjectDetectionResultReviewDefectRows(
                choice.Parameter,
                objectDetectionResultReviewSelectedDefectObjectNumber);
            RefreshObjectDetectionResultReviewSelectedObjectDisplays();
            if (!clearSelection)
            {
                FocusObjectDetectionResultReviewObject(objectNumber);
            }
        }

        private void FocusObjectDetectionResultReviewObject(int objectNumber)
        {
            var choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            ObjectDefinitionSettings definition = choice == null || choice.Parameter == null ||
                string.IsNullOrWhiteSpace(choice.Parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(choice.Parameter.ObjectDefinitionId);
            ObjectDefinitionDetectedObject detectedObject;
            if (definition != null && TryGetCompletedObjectDefinitionObject(
                definition,
                objectNumber,
                out detectedObject) &&
                detectedObject.Bounds.Width > 0 && detectedObject.Bounds.Height > 0)
            {
                FocusObjectDetectionImage(detectedObject.Bounds);
            }
        }

        private void RenderObjectDetectionResultReviewDefectRows(
            ObjectDetectionParameterSettings parameter,
            int? selectedObjectNumber)
        {
            objectDetectionResultReviewDefectsGrid.Rows.Clear();
            EnsureObjectDetectionDefectCores(parameter);
            bool allRequiredResultsReady = true;
            int totalComponents = 0;
            foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores
                .Take(ObjectDetectionDefectCoreKeys.Length))
            {
                bool required = core.Enabled;
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
                    allRequiredResultsReady = false;
                    objectDetectionResultReviewDefectsGrid.Rows.Add(
                        GetObjectDetectionDefectCoreLabel(GetObjectDetectionDefectCoreIndex(core.CoreKey)),
                        "-", "-", "未完成", "請確認檢測範圍與平場校正值");
                    continue;
                }

                totalComponents += result.DetectedComponentCount;
                int componentCount = selectedObjectNumber.HasValue
                    ? CountObjectDetectionResultReviewDefectComponents(result, selectedObjectNumber.Value)
                    : result.DetectedComponentCount;
                objectDetectionResultReviewDefectsGrid.Rows.Add(
                    GetObjectDetectionDefectCoreLabel(GetObjectDetectionDefectCoreIndex(core.CoreKey)),
                    componentCount,
                    selectedObjectNumber.HasValue ? (object)"-" : result.TotalElapsedMilliseconds,
                    componentCount > 0 ? "檢出缺陷" : "未檢出",
                    (selectedObjectNumber.HasValue
                        ? "物件 " + selectedObjectNumber.Value.ToString("N0", CultureInfo.CurrentCulture) + "；"
                        : string.Empty) +
                    "OpenCV 元件數：" + componentCount.ToString("N0", CultureInfo.CurrentCulture));
            }

            if (parameter.DefectIntegrationIncludeFrequency ||
                parameter.DefectFrequencyNeighborComparisonEnabled)
            {
                if (!parameter.DefectFrequencyEnabled)
                {
                    objectDetectionResultReviewDefectsGrid.Rows.Add(
                        "頻域異常", "-", "-", "未啟用", "頻域分析已停用，未產生掃描結果");
                }
                else
                {
                    ObjectDetectionFrequencyResult frequencyResult;
                    if (TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult))
                    {
                        int energyAnomalyCount = frequencyResult.Cells == null
                            ? 0
                            : frequencyResult.Cells.Count(cell => cell != null && cell.IsAnomaly &&
                                (!selectedObjectNumber.HasValue ||
                                    cell.ObjectNumber == selectedObjectNumber.Value));
                        int neighborAnomalyCount = frequencyResult.Cells == null
                            ? 0
                            : frequencyResult.Cells.Count(cell => cell != null && cell.IsNeighborAnomaly &&
                                (!selectedObjectNumber.HasValue ||
                                    cell.ObjectNumber == selectedObjectNumber.Value));
                        int anomalyCount = frequencyResult.Cells == null
                            ? 0
                            : frequencyResult.Cells.Count(cell => cell != null &&
                                (cell.IsAnomaly || cell.IsNeighborAnomaly) &&
                                (!selectedObjectNumber.HasValue ||
                                    cell.ObjectNumber == selectedObjectNumber.Value));
                        objectDetectionResultReviewDefectsGrid.Rows.Add(
                            "頻域異常",
                            anomalyCount,
                            selectedObjectNumber.HasValue
                                ? (object)"-"
                                : frequencyResult.TotalElapsedMilliseconds,
                            anomalyCount > 0 ? "檢出異常格" : "未檢出",
                            "能量異常 " + energyAnomalyCount.ToString("N0", CultureInfo.CurrentCulture) +
                            " 格；鄰格比較 " + neighborAnomalyCount.ToString("N0", CultureInfo.CurrentCulture) +
                            " 格；僅勾選納入頻域異常時才併入整合結果");
                    }
                    else
                    {
                        allRequiredResultsReady = false;
                        objectDetectionResultReviewDefectsGrid.Rows.Add(
                            "頻域異常", "-", "-", "未完成", "目前影像的頻域分析尚未完成或已過期");
                    }
                }
            }

            ObjectDetectionFrequencySettings frequency2Settings =
                parameter.DefectFrequency2Settings ?? new ObjectDetectionFrequencySettings();
            if (parameter.DefectIntegrationIncludeFrequency2 ||
                frequency2Settings.NeighborComparisonEnabled)
            {
                if (!frequency2Settings.Enabled)
                {
                    objectDetectionResultReviewDefectsGrid.Rows.Add(
                        "頻域異常二", "-", "-", "未啟用", "第二組頻域分析已停用，未產生掃描結果");
                }
                else
                {
                    ObjectDetectionFrequencyResult frequency2Result;
                    if (TryGetCurrentObjectDetectionFrequencyResult(
                        parameter,
                        out frequency2Result,
                        1))
                    {
                        int energyAnomalyCount = frequency2Result.Cells == null
                            ? 0
                            : frequency2Result.Cells.Count(cell => cell != null && cell.IsAnomaly &&
                                (!selectedObjectNumber.HasValue ||
                                    cell.ObjectNumber == selectedObjectNumber.Value));
                        int neighborAnomalyCount = frequency2Result.Cells == null
                            ? 0
                            : frequency2Result.Cells.Count(cell => cell != null && cell.IsNeighborAnomaly &&
                                (!selectedObjectNumber.HasValue ||
                                    cell.ObjectNumber == selectedObjectNumber.Value));
                        int anomalyCount = frequency2Result.Cells == null
                            ? 0
                            : frequency2Result.Cells.Count(cell => cell != null &&
                                (cell.IsAnomaly || cell.IsNeighborAnomaly) &&
                                (!selectedObjectNumber.HasValue ||
                                    cell.ObjectNumber == selectedObjectNumber.Value));
                        objectDetectionResultReviewDefectsGrid.Rows.Add(
                            "頻域異常二",
                            anomalyCount,
                            selectedObjectNumber.HasValue
                                ? (object)"-"
                                : frequency2Result.TotalElapsedMilliseconds,
                            anomalyCount > 0 ? "檢出異常格" : "未檢出",
                            "能量異常 " + energyAnomalyCount.ToString("N0", CultureInfo.CurrentCulture) +
                            " 格；鄰格比較 " + neighborAnomalyCount.ToString("N0", CultureInfo.CurrentCulture) +
                            " 格；僅勾選納入頻域異常二時才併入整合結果");
                    }
                    else
                    {
                        allRequiredResultsReady = false;
                        objectDetectionResultReviewDefectsGrid.Rows.Add(
                            "頻域異常二", "-", "-", "未完成", "目前影像的第二組頻域分析尚未完成或已過期");
                    }
                }
            }

            if (allRequiredResultsReady)
            {
                IEnumerable<ObjectDetectionDefectIntegrationGroup> groups =
                    GetObjectDetectionDefectIntegrationGroups(parameter);
                int integratedCount = selectedObjectNumber.HasValue
                    ? groups.Count(group => group.ObjectNumber == selectedObjectNumber.Value)
                    : groups.Count();
                objectDetectionResultReviewDefectsGrid.Rows.Add(
                    "整合結果", integratedCount, string.Empty,
                    integratedCount > 0 ? "檢出缺陷" : "未檢出",
                    (selectedObjectNumber.HasValue
                        ? "物件 " + selectedObjectNumber.Value.ToString("N0", CultureInfo.CurrentCulture) + "；"
                        : "各條件合併後的缺陷群數；") +
                    "單核心元件合計 " +
                    (selectedObjectNumber.HasValue
                        ? SumObjectDetectionResultReviewDefectComponents(parameter, selectedObjectNumber.Value)
                        : totalComponents).ToString("N0", CultureInfo.CurrentCulture));
            }
            else
            {
                objectDetectionResultReviewDefectsGrid.Rows.Add(
                    "整合結果", "-", string.Empty, "待確認", "尚有必要的缺陷檢測尚未完成");
            }

            ApplyObjectDetectionResultReviewVerdictStyles();
        }

        private static int CountObjectDetectionResultReviewDefectComponents(
            ObjectDetectionDefectCoreResult result,
            int objectNumber)
        {
            return result == null || result.Contours == null
                ? 0
                : result.Contours
                    .Where(contour => contour != null && contour.ObjectNumber == objectNumber)
                    .Select(contour => (contour.IsBright ? "B" : "D") + ":" +
                        contour.ComponentLabel.ToString(CultureInfo.InvariantCulture))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
        }

        private int SumObjectDetectionResultReviewDefectComponents(
            ObjectDetectionParameterSettings parameter,
            int objectNumber)
        {
            int total = 0;
            foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores
                .Take(ObjectDetectionDefectCoreKeys.Length))
            {
                if (!core.Enabled)
                {
                    continue;
                }

                ObjectDetectionDefectCoreResult result;
                if (TryGetObjectDetectionDefectCoreResult(parameter, core.CoreKey, out result))
                {
                    total += CountObjectDetectionResultReviewDefectComponents(result, objectNumber);
                }
            }
            return total;
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

        private bool IsActiveObjectDetectionResultReviewSnapshot(
            ObjectDetectionParameterSettings parameter)
        {
            return isObjectDetectionResultReviewMode &&
                objectDetectionResultReviewSnapshot != null &&
                parameter != null &&
                string.Equals(
                    objectDetectionResultReviewSnapshot.ParameterId,
                    parameter.Id,
                    StringComparison.Ordinal);
        }

        private ObjectDetectionParameterSettings GetObjectDetectionParameterForDisplay()
        {
            if (isObjectDetectionResultReviewMode && objectDetectionResultReviewSnapshot != null)
            {
                return objectDetectionResultReviewSnapshot.Parameter;
            }
            return FindObjectDetectionParameter(activeObjectDetectionParameterId);
        }

        private string CreateObjectDetectionResultReviewSettingsSignature(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionSettings definition)
        {
            if (parameter == null || definition == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendObjectDetectionResultReviewSignatureValue(builder, parameter, new HashSet<object>());
            builder.Append('|');
            AppendObjectDetectionResultReviewSignatureValue(builder, definition, new HashSet<object>());
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(digest).Replace("-", string.Empty);
            }
        }

        private static void AppendObjectDetectionResultReviewSignatureValue(
            StringBuilder builder,
            object value,
            HashSet<object> visited)
        {
            if (value == null)
            {
                builder.Append("null;");
                return;
            }

            Type type = value.GetType();
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum ||
                type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid) ||
                type.IsValueType)
            {
                string scalar = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                builder.Append(type.FullName).Append(':').Append(scalar.Length)
                    .Append(':').Append(scalar).Append(';');
                return;
            }

            if (!type.IsValueType && !visited.Add(value))
            {
                builder.Append("ref;");
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                builder.Append(type.FullName).Append('[');
                foreach (object item in enumerable)
                {
                    AppendObjectDetectionResultReviewSignatureValue(builder, item, visited);
                }
                builder.Append("];" );
                visited.Remove(value);
                return;
            }

            builder.Append(type.FullName).Append('{');
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(item => item.CanRead && item.GetIndexParameters().Length == 0)
                .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                builder.Append(property.Name).Append('=');
                try
                {
                    AppendObjectDetectionResultReviewSignatureValue(
                        builder,
                        property.GetValue(value, null),
                        visited);
                }
                catch
                {
                    builder.Append("unavailable;");
                }
            }
            builder.Append("};");
            visited.Remove(value);
        }

        private static object CloneObjectDetectionResultReviewModel(object source)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum ||
                type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid) ||
                type.IsValueType)
            {
                return source;
            }

            Array sourceArray = source as Array;
            if (sourceArray != null)
            {
                Type elementType = type.GetElementType();
                Array copy = Array.CreateInstance(elementType, sourceArray.Length);
                for (int index = 0; index < sourceArray.Length; index++)
                {
                    copy.SetValue(
                        CloneObjectDetectionResultReviewModel(sourceArray.GetValue(index)),
                        index);
                }
                return copy;
            }

            IList sourceList = source as IList;
            if (sourceList != null)
            {
                IList copy = Activator.CreateInstance(type) as IList;
                if (copy == null)
                {
                    throw new InvalidOperationException("無法複製結果確認清單資料。");
                }
                foreach (object item in sourceList)
                {
                    copy.Add(CloneObjectDetectionResultReviewModel(item));
                }
                return copy;
            }

            object clone = Activator.CreateInstance(type);
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(item => item.CanRead && item.GetIndexParameters().Length == 0))
            {
                object value = property.GetValue(source, null);
                if (property.CanWrite)
                {
                    property.SetValue(
                        clone,
                        CloneObjectDetectionResultReviewModel(value),
                        null);
                }
                else
                {
                    IList sourceItems = value as IList;
                    IList targetItems = property.GetValue(clone, null) as IList;
                    if (sourceItems != null && targetItems != null)
                    {
                        foreach (object item in sourceItems)
                        {
                            targetItems.Add(CloneObjectDetectionResultReviewModel(item));
                        }
                    }
                }
            }
            return clone;
        }

        private static List<ObjectDefinitionDetectedObject> CloneObjectDetectionResultReviewObjects(
            IEnumerable<ObjectDefinitionDetectedObject> objects)
        {
            return objects == null
                ? new List<ObjectDefinitionDetectedObject>()
                : objects.Select(item => (ObjectDefinitionDetectedObject)
                    CloneObjectDetectionResultReviewModel(item)).ToList();
        }

        private void CaptureObjectDetectionResultReviewSnapshot(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionSettings definition,
            IList<ObjectDefinitionDetectedObject> objects)
        {
            if (parameter == null || definition == null || objects == null)
            {
                return;
            }

            var snapshot = new ObjectDetectionResultReviewSnapshot
            {
                ParameterId = parameter.Id,
                ImagePath = objectDetectionResultReviewImagePath,
                ImageContentGeneration = imageContentGeneration,
                ImageSourceGeneration = System.Threading.Interlocked.CompareExchange(
                    ref imageSourceGeneration, 0, 0),
                Parameter = (ObjectDetectionParameterSettings)
                    CloneObjectDetectionResultReviewModel(parameter),
                Definition = (ObjectDefinitionSettings)
                    CloneObjectDetectionResultReviewModel(definition),
                Objects = CloneObjectDetectionResultReviewObjects(objects),
                DefectViewStates = GetAllObjectDetectionDefectDisplayControls()
                    .Select(display => display == null ? default(ImageViewState) : display.ViewState)
                    .ToArray()
            };

            try
            {
                snapshot.SettingsSignature = CreateObjectDetectionResultReviewSettingsSignature(
                    snapshot.Parameter,
                    snapshot.Definition);
                if (rightOriginalDisplayControl != null && rightOriginalDisplayControl.HasImage)
                {
                    if (rightOriginalDisplayControl.IsLargeImageMode)
                    {
                        snapshot.OriginalLargeSource =
                            rightOriginalDisplayControl.GetSharedLargeImageSource();
                    }
                    else
                    {
                        snapshot.OriginalBitmap = rightOriginalDisplayControl.CloneImage();
                    }
                }
                if (objectDetectionFlatFieldCorrectedLargeSource != null)
                {
                    snapshot.CorrectedLargeSource =
                        objectDetectionFlatFieldCorrectedLargeSource.AddReference();
                }
                else if (objectDetectionFlatFieldPreviewDisplayControl != null &&
                    objectDetectionFlatFieldPreviewDisplayControl.HasImage &&
                    !objectDetectionFlatFieldPreviewDisplayControl.IsLargeImageMode)
                {
                    snapshot.CorrectedBitmap =
                        objectDetectionFlatFieldPreviewDisplayControl.CloneImage();
                }

                LargeImageSource dimensionSource = snapshot.OriginalLargeSource ??
                    snapshot.CorrectedLargeSource;
                if (dimensionSource != null)
                {
                    snapshot.ImageWidth = dimensionSource.Width;
                    snapshot.ImageHeight = dimensionSource.Height;
                }
                else if (snapshot.OriginalBitmap != null)
                {
                    snapshot.ImageWidth = snapshot.OriginalBitmap.Width;
                    snapshot.ImageHeight = snapshot.OriginalBitmap.Height;
                }
                else if (snapshot.CorrectedBitmap != null)
                {
                    snapshot.ImageWidth = snapshot.CorrectedBitmap.Width;
                    snapshot.ImageHeight = snapshot.CorrectedBitmap.Height;
                }

                CaptureObjectDetectionResultReviewMeasurementMasks(snapshot);

                if (objectDetectionMeasurementDisplayControl != null)
                {
                    snapshot.MeasurementViewState = objectDetectionMeasurementDisplayControl.ViewState;
                }
                if (objectDetectionFlatFieldPreviewDisplayControl != null)
                {
                    snapshot.FlatFieldViewState = objectDetectionFlatFieldPreviewDisplayControl.ViewState;
                }
                lock (objectDetectionFlatFieldMaskLock)
                {
                    snapshot.SourceMasks = objectDetectionFlatFieldMaskOverlays;
                    snapshot.UseMasks = objectDetectionFlatFieldUseMaskOverlays;
                    snapshot.UseMaskConfigured = objectDetectionFlatFieldUseMaskConfigured;
                    objectDetectionFlatFieldMaskOverlays =
                        new List<ObjectDetectionFlatFieldMaskOverlay>();
                    objectDetectionFlatFieldUseMaskOverlays =
                        new List<ObjectDetectionFlatFieldMaskOverlay>();
                    objectDetectionFlatFieldUseMaskConfigured = false;
                }
                snapshot.IntegrationGroups = (List<ObjectDetectionDefectIntegrationGroup>)
                    CloneObjectDetectionResultReviewModel(
                        GetObjectDetectionDefectIntegrationGroups(parameter));

                Dictionary<string, ObjectDetectionDefectCoreResult> coreResults;
                if (objectDetectionDefectCoreResults.TryGetValue(parameter.Id, out coreResults))
                {
                    snapshot.CoreResults = coreResults;
                    objectDetectionDefectCoreResults.Remove(parameter.Id);
                }
                ObjectDetectionFrequencyResult frequencyResult;
                if (objectDetectionFrequencyResults.TryGetValue(parameter.Id, out frequencyResult))
                {
                    snapshot.FrequencyResult = frequencyResult;
                    objectDetectionFrequencyResults.Remove(parameter.Id);
                }
                if (objectDetectionFrequencyResults.TryGetValue(
                    GetObjectDetectionFrequencyResultKey(parameter.Id, 1),
                    out frequencyResult))
                {
                    snapshot.Frequency2Result = frequencyResult;
                    objectDetectionFrequencyResults.Remove(
                        GetObjectDetectionFrequencyResultKey(parameter.Id, 1));
                }
                ClearObjectDetectionResultReviewSnapshot();
                objectDetectionResultReviewSnapshot = snapshot;
                objectDetectionDefectIntegrationCache.Remove(parameter.Id);
            }
            catch
            {
                snapshot.Dispose();
                throw;
            }
        }

        private void CaptureObjectDetectionResultReviewMeasurementMasks(
            ObjectDetectionResultReviewSnapshot snapshot)
        {
            if (snapshot == null || objectDetectionResultReviewMeasurementsGrid == null)
            {
                return;
            }

            snapshot.MeasurementMasks = new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
            for (int rowIndex = 0;
                rowIndex < objectDetectionResultReviewMeasurementsGrid.Rows.Count;
                rowIndex++)
            {
                ResultReviewMeasurementRowContext context =
                    objectDetectionResultReviewMeasurementsGrid.Rows[rowIndex].Tag
                        as ResultReviewMeasurementRowContext;
                if (context == null || context.MaskParameter == null ||
                    context.DetectedObject == null)
                {
                    continue;
                }

                Rectangle bounds = context.DetectedObject.Bounds;
                string key = CreateObjectDetectionMeasurementMaskCacheKey(
                    context.MaskParameter,
                    context.ObjectNumber,
                    bounds,
                    snapshot.ImageSourceGeneration);
                if (snapshot.MeasurementMasks.ContainsKey(key))
                {
                    continue;
                }

                Cv.Mat mask;
                if (TryGetObjectDetectionMeasurementMask(
                    context.MaskParameter,
                    context.ObjectNumber,
                    bounds,
                    snapshot.OriginalLargeSource,
                    snapshot.OriginalBitmap,
                    out mask,
                    true,
                    true))
                {
                    snapshot.MeasurementMasks[key] = mask;
                }
            }
        }

        private void CaptureObjectDetectionResultReviewSnapshotViewStates()
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            if (snapshot == null)
            {
                return;
            }
            if (objectDetectionMeasurementDisplayControl != null)
            {
                snapshot.MeasurementViewState = objectDetectionMeasurementDisplayControl.ViewState;
            }
            if (objectDetectionFlatFieldPreviewDisplayControl != null)
            {
                snapshot.FlatFieldViewState = objectDetectionFlatFieldPreviewDisplayControl.ViewState;
            }
            ImageDisplayControl[] displays = GetAllObjectDetectionDefectDisplayControls();
            snapshot.DefectViewStates = displays
                .Select(display => display == null ? default(ImageViewState) : display.ViewState)
                .ToArray();
        }

        private void RestoreObjectDetectionResultReviewSnapshotDisplays()
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            if (snapshot == null || !isObjectDetectionResultReviewMode)
            {
                return;
            }

            activeObjectDetectionParameterId = snapshot.ParameterId;
            SetObjectDetectionResultReviewBaseImage(
                objectDetectionMeasurementDisplayControl,
                snapshot.OriginalLargeSource,
                snapshot.OriginalBitmap);
            SetObjectDetectionResultReviewBaseImage(
                objectDetectionFlatFieldPreviewDisplayControl,
                snapshot.CorrectedLargeSource,
                snapshot.CorrectedBitmap);
            ImageDisplayControl[] defectDisplays = GetAllObjectDetectionDefectDisplayControls();
            for (int index = 0; index < defectDisplays.Length; index++)
            {
                SetObjectDetectionResultReviewBaseImage(
                    defectDisplays[index],
                    snapshot.CorrectedLargeSource,
                    snapshot.CorrectedBitmap);
                if (snapshot.DefectViewStates != null && index < snapshot.DefectViewStates.Length)
                {
                    defectDisplays[index].ApplyViewState(snapshot.DefectViewStates[index]);
                }
                defectDisplays[index].InvalidateImageView();
                if (objectDetectionDefectDisplayParameterIds != null &&
                    index < objectDetectionDefectDisplayParameterIds.Length)
                {
                    objectDetectionDefectDisplayParameterIds[index] = snapshot.ParameterId;
                    objectDetectionDefectDisplayImageGenerations[index] = imageSourceGeneration;
                }
            }

            if (objectDetectionMeasurementDisplayControl != null)
            {
                objectDetectionMeasurementDisplayControl.ApplyViewState(snapshot.MeasurementViewState);
                objectDetectionMeasurementDisplayControl.InvalidateImageView();
            }
            if (objectDetectionFlatFieldPreviewDisplayControl != null)
            {
                objectDetectionFlatFieldPreviewDisplayControl.ApplyViewState(snapshot.FlatFieldViewState);
                objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
            }
            objectDetectionFlatFieldPreviewWidth = snapshot.ImageWidth;
            objectDetectionFlatFieldPreviewHeight = snapshot.ImageHeight;
            objectDetectionFlatFieldPreviewIsCorrected = true;
            objectDetectionFlatFieldCorrectedImageGeneration = imageSourceGeneration;
            objectDetectionFlatFieldPreviewImageGeneration = imageSourceGeneration;
            objectDetectionDefectDisplayParameterId = snapshot.ParameterId;
            objectDetectionDefectDisplayImageGeneration = imageSourceGeneration;
            ShowObjectDetectionDefectDisplayPlaceholder(null);
        }

        private static void SetObjectDetectionResultReviewBaseImage(
            ImageDisplayControl display,
            LargeImageSource largeSource,
            Bitmap bitmap)
        {
            if (display == null || display.IsDisposed)
            {
                return;
            }
            if (largeSource != null)
            {
                display.SetSharedLargeImageSource(largeSource, true);
            }
            else if (bitmap != null)
            {
                display.SetDisplayImage(new Bitmap(bitmap), true);
            }
            else
            {
                display.ClearImage();
            }
        }

        private void ClearObjectDetectionResultReviewSnapshot()
        {
            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            objectDetectionResultReviewSnapshot = null;
            if (snapshot != null)
            {
                snapshot.Dispose();
            }
        }

        private void ClearObjectDetectionResultReviewResults()
        {
            ClearObjectDetectionResultReviewSnapshot();
            if (!IsObjectDetectionResultReviewUiAvailable())
            {
                return;
            }
            objectDetectionResultReviewMeasurementsGrid.Rows.Clear();
            objectDetectionResultReviewConditionsGrid.Rows.Clear();
            objectDetectionResultReviewDefectsGrid.Rows.Clear();
            objectDetectionResultReviewImageGeneration = null;
            objectDetectionResultReviewSelectedDefectObjectNumber = null;
            if (objectDetectionResultReviewDefectObjectButtonsPanel != null &&
                !objectDetectionResultReviewDefectObjectButtonsPanel.IsDisposed)
            {
                foreach (Control control in objectDetectionResultReviewDefectObjectButtonsPanel.Controls
                    .Cast<Control>().ToList())
                {
                    objectDetectionResultReviewDefectObjectButtonsPanel.Controls.Remove(control);
                    control.Dispose();
                }
            }
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
