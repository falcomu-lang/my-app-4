using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm : Form
    {
        private ImageDisplayControl leftOriginalDisplayControl;
        private ImageDisplayControl leftProcessedDisplayControl;
        private ImageDisplayControl leftObjectsDisplayControl;
        private ImageDisplayControl leftDebugDisplayControl;
        private ImageDisplayControl rightOriginalDisplayControl;
        private ImageDisplayControl rightProcessedDisplayControl;
        private ImageDisplayControl rightObjectsDisplayControl;
        private ImageDisplayControl rightDebugDisplayControl;
        private ToolTip objectAreaToolTip;
        private ImageDisplayControl objectAreaTooltipDisplay;
        private ImagePointerMovedEventArgs objectAreaTooltipPointer;
        private bool isControlKeyDown;
        private bool isLoadingImage;
        private bool isSyncingImageView;
        private System.Windows.Forms.Timer synchronizedImagePanTimer;
        private ImageDisplayControl pendingSynchronizedImagePanTarget;
        private readonly SystemParameterIniService systemParameterService;
        private SystemParameterSettings systemParameters;
        private bool roiMenuExpanded;
        private string expandedRoiText;
        private int selectedRoiIndex = -1;
        private bool imageProcessingMenuExpanded;
        private bool isUpdatingFunctionListText;
        // SelectionChanged handles a newly selected item.  Keep this marker so
        // the matching MouseClick does not execute the same A-key command twice.
        private int aKeyProcessedFunctionListIndex = -1;
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKeyCode);
        private string expandedImageProcessingStepText;
        private TreeView imageProcessingFlowTreeView;
        private FlowLayoutPanel imageProcessingParameterPanel;
        private int selectedImageProcessingStepIndex = -1;
        private string selectedImageProcessingGroupId;
        // Selection is only for editing/inspection. These fields identify the
        // processed result that is currently painted in the viewers.
        private int displayedImageProcessingStepIndex = -1;
        private string displayedImageProcessingGroupId;
        private string displayedImageRelationGroupId;
        private string displayedImageRelationSourceType = "Original";
        private string displayedImageRelationSourceId;
        private readonly Dictionary<string, string> visibleImageProcessingStepIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> visibleImageProcessingGroupIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private bool explicitProcessedImageUpdateRequested;
        private readonly HashSet<string> expandedImageProcessingGroupIds = new HashSet<string>(StringComparer.Ordinal);
        private bool isUpdatingImageProcessingFlowTree;
        private bool isLoadingImageProcessingParameters;
        private System.Windows.Forms.Timer imageProcessingDebounceTimer;
        private ContextMenuStrip imageProcessingStepContextMenu;
        private Bitmap latestProcessedImage;
        private readonly Dictionary<string, Bitmap> processedImageCache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private readonly Dictionary<string, Bitmap> largeProcessedOverlayCache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private readonly HashSet<string> pendingLargeProcessedOverlayTiles = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> pendingLargeProcessedViewportOverlays = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> pendingLargeProcessedOverviewOverlays = new HashSet<string>(StringComparer.Ordinal);
        private readonly LargeImageMaskCache largeImageMaskCache = new LargeImageMaskCache();
        // One full-size OpenCV edge operation already uses several source-size
        // buffers. Serializing it prevents parameter changes from starting a
        // second competing Sobel/Canny operation before the first one exits.
        private readonly SemaphoreSlim largeNativeProcessingGate = new SemaphoreSlim(1, 1);
        private readonly LargeRoiGrayscaleCache largeRoiGrayscaleCache = new LargeRoiGrayscaleCache();
        // This is deliberately separate from LargeImageSource's WIC tiles.
        // Tiles own responsive rendering; this Mat owns fast ROI processing.
        // The preprocessing output remains a full-resolution OpenCV Mat and
        // is exposed to the viewers through a memory-backed tile source.
        private int imageSourceGeneration;
        private Dictionary<string, string> pendingImageProcessingParameters;
        private Label parameterApplyStatusLabel;
        private Stopwatch parameterApplyStopwatch;
        private bool parameterApplyInProgress;
        private bool parameterApplyIsPreprocessing;
        private long lastImageProcessingElapsedMilliseconds;
        private long lastObjectJudgementElapsedMilliseconds;
        private readonly object backgroundStatusLock = new object();
        private System.Threading.Timer backgroundStatusTimer;
        private string pendingBackgroundStatusText;
        private long lastDisplayProcessingElapsedMilliseconds;
        private bool includeImageProcessingTimeOnNextDisplay;
        private bool imageProcessingDisplayPending;
        // Execution-only timing: a saved profile starts without a stale runtime value.
        private readonly Dictionary<ImageProcessingStepSettings, long> imageProcessingStepElapsedMilliseconds =
            new Dictionary<ImageProcessingStepSettings, long>();
        private bool processedImageDirty = true;
        private string latestProcessedImageCacheKey;
        private bool imageProcessingExecutionRequested;
        private bool displayRefreshPendingWhileSuppressed;
        private bool hasSharedImageViewState;
        private ImageViewState sharedImageViewState;
        private bool isImageViewerMaximized;
        private bool isLeftImageViewerMaximized;
        private bool hasMaximizedImageViewerViewState;
        private ImageViewState maximizedImageViewerViewState;
        private bool hasProcessedImageViewState;
        private ImageViewState processedImageViewState;

        private bool IsImageDisplayUpdateSuppressed
        {
            get
            {
                return suppressImageDisplayCheckBox != null &&
                    suppressImageDisplayCheckBox.Checked;
            }
        }

        private bool SkipImageDisplayUpdate()
        {
            if (!IsImageDisplayUpdateSuppressed)
            {
                return false;
            }

            displayRefreshPendingWhileSuppressed = true;
            return true;
        }

        private void SuppressImageDisplayCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ImageDisplayControl.SuppressViewUpdates = IsImageDisplayUpdateSuppressed;
            if (centerPanel != null)
            {
                centerPanel.Visible = !IsImageDisplayUpdateSuppressed;
            }

            if (IsImageDisplayUpdateSuppressed)
            {
                displayRefreshPendingWhileSuppressed = true;
                statusLabel.Text = "已暫停所有畫面準備與更新，影像仍會繼續處理";
                return;
            }

            if (displayRefreshPendingWhileSuppressed)
            {
                displayRefreshPendingWhileSuppressed = false;
                RestoreCachedImageDisplaysAfterSuppression();
                RefreshAllImageDisplaysAfterSuppression();
            }

            statusLabel.Text = "已恢復畫面準備與更新";
        }

        private void RestoreCachedImageDisplaysAfterSuppression()
        {
            if (IsImageDisplayUpdateSuppressed)
            {
                return;
            }

            lock (largePreprocessedImageLock)
            {
                if (largePreprocessedImageSource != null && !preprocessedImageDirty)
                {
                    leftPreprocessedDisplayControl.SetSharedLargeImageSource(
                        largePreprocessedImageSource,
                        true);
                    rightPreprocessedDisplayControl.SetSharedLargeImageSource(
                        largePreprocessedImageSource,
                        true);
                }
            }

            if (latestPreprocessedImage != null && !preprocessedImageDirty)
            {
                leftPreprocessedDisplayControl.SetDisplayImage(
                    new Bitmap(latestPreprocessedImage),
                    true);
                rightPreprocessedDisplayControl.SetDisplayImage(
                    new Bitmap(latestPreprocessedImage),
                    true);
            }

            ApplyLatestProcessedImageToVisibleTabs();
            if (rightOriginalDisplayControl != null &&
                rightOriginalDisplayControl.IsLargeImageMode &&
                imageProcessingExecutionRequested &&
                !processedImageDirty)
            {
                PrepareLargeProcessedPreview();
            }
        }

        private void RefreshAllImageDisplaysAfterSuppression()
        {
            if (IsImageDisplayUpdateSuppressed)
            {
                return;
            }

            // Repaint existing cached overlays/results without starting a new
            // image-processing request.  The next explicit command can still
            // replace these images when its parameters have changed.
            if (leftPreprocessedDisplayControl != null)
            {
                leftPreprocessedDisplayControl.InvalidateImageView();
            }
            if (rightPreprocessedDisplayControl != null)
            {
                rightPreprocessedDisplayControl.InvalidateImageView();
            }
            if (leftProcessedDisplayControl != null)
            {
                leftProcessedDisplayControl.InvalidateImageView();
            }
            if (rightProcessedDisplayControl != null)
            {
                rightProcessedDisplayControl.InvalidateImageView();
            }
            InvalidateBlockProcessingDisplays();
            if (leftObjectsDisplayControl != null)
            {
                leftObjectsDisplayControl.InvalidateImageView();
            }
            if (rightObjectsDisplayControl != null)
            {
                rightObjectsDisplayControl.InvalidateImageView();
            }
            if (isObjectDetectionParameterImageLayout)
            {
                RefreshObjectDetectionFlatFieldDisplay();
                RefreshObjectDetectionDefectDisplay();
                if (objectDetectionFlatFieldPreviewDisplayControl != null)
                {
                    objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
                }
            }

            foreach (ImageDisplayControl displayControl in new ImageDisplayControl[]
            {
                leftOriginalDisplayControl,
                rightOriginalDisplayControl,
                leftPreprocessedDisplayControl,
                rightPreprocessedDisplayControl,
                leftProcessedDisplayControl,
                rightProcessedDisplayControl,
                leftBlockProcessingDisplayControl,
                rightBlockProcessingDisplayControl,
                leftObjectsDisplayControl,
                rightObjectsDisplayControl,
                objectDetectionMeasurementDisplayControl,
                objectDetectionFlatFieldPreviewDisplayControl,
                objectDetectionDefectDisplayControl,
                leftDebugDisplayControl,
                rightDebugDisplayControl
            })
            {
                if (displayControl != null)
                {
                    displayControl.ResumeImageViewPreparation();
                }
            }
        }

        private const string DetectionParameterLoadMenuText = "檢測參數讀取";
        private const string LoadImageMenuText = "讀取圖片";
        private const string RoiMenuText = "指定 ROI";
        private const string FindObjectFlowMenuText = "找尋物件流程";
        private const string ImageProcessingMenuText = "影像處理";
        private const string ObjectJudgementMenuText = "整合成區塊";
        private const string ObjectDefinitionMenuText = "物件定義";
        private const string ObjectDetectionParameterMenuText = "檢測參數設定";
        private const string ObjectDetectionResultReviewMenuText = "參數結果確認";
        private const string DeleteImageProcessingStepMenuText = "      刪除";
        private const string MoveUpImageProcessingStepMenuText = "      上移";
        private const string MoveDownImageProcessingStepMenuText = "      下移";
        private const int MaxLargeProcessedOverlayCacheCount = 512;
        private const int LargeProcessedOverlayTileSize = 128;
        private const int LargeProcessedMaskChunkSize = 1024;
        // Keep single-pass OpenCV for normal ROIs, but avoid allocating
        // full-height temporary Mats for production-size images.
        private const long MaxSinglePassLargeRoiPixels = 128L * 1024L * 1024L;
        private const int MaxPendingLargeProcessedOverlayTiles = 2;
        private const int MaxPendingCachedMaskOverlayTiles = 16;
        private const int MaxPendingLargeProcessedViewportOverlays = 4;
        private static readonly string[] KernelSizeOptions = new[] { "3", "5", "7", "9", "11", "13", "15" };
        private static readonly string[] CannyKernelSizeOptions = new[] { "3", "5", "7" };
        private static readonly string[] SobelKernelSizeOptions = new[] { "1", "3", "5", "7" };

        private enum FunctionMenuIcon
        {
            None,
            Folder,
            Flow,
            Roi,
            Process,
            Relation,
            Object,
            Brightness,
            Filter,
            Edge,
            Geometry,
            Measure,
            Save
        }

        public MainForm()
        {
            systemParameterService = new SystemParameterIniService(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemParameters.ini"));
            systemParameters = systemParameterService.Load();
            LoadSavedObjectDetectionFlatFieldProfiles();

            InitializeComponent();
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;
            KeyUp += MainForm_KeyUp;
            objectAreaToolTip = new ToolTip();
            objectAreaToolTip.AutoPopDelay = 30000;
            objectAreaToolTip.InitialDelay = 0;
            objectAreaToolTip.ReshowDelay = 0;
            objectAreaToolTip.ShowAlways = true;
            if (components != null)
            {
                components.Add(objectAreaToolTip);
            }
            synchronizedImagePanTimer = new System.Windows.Forms.Timer();
            synchronizedImagePanTimer.Interval = 16;
            synchronizedImagePanTimer.Tick += SynchronizedImagePanTimer_Tick;
            if (components != null)
            {
                components.Add(synchronizedImagePanTimer);
            }
            ImageDisplayControl.SuppressViewUpdates = false;
            NormalizeObjectJudgementDefaultNames();
            NormalizeObjectDefinitionDefaultNames();
            functionListBox.SelectionMode = SelectionMode.MultiExtended;
            functionListBox.MouseUp += FunctionListBox_MouseUp;
            if (!functionListBox.Items.Contains(DetectionParameterLoadMenuText))
            {
                int loadImageIndex = functionListBox.Items.IndexOf(LoadImageMenuText);
                functionListBox.Items.Insert(loadImageIndex < 0 ? 0 : loadImageIndex, DetectionParameterLoadMenuText);
            }
            if (!functionListBox.Items.Contains(ImageRelationMenuText))
            {
                int processingIndex = functionListBox.Items.IndexOf(ImageProcessingMenuText);
                functionListBox.Items.Insert(processingIndex < 0 ? functionListBox.Items.Count : processingIndex + 1, ImageRelationMenuText);
            }
            if (!functionListBox.Items.Contains(ObjectJudgementMenuText))
            {
                int relationIndex = functionListBox.Items.IndexOf(ImageRelationMenuText);
                functionListBox.Items.Insert(relationIndex < 0 ? functionListBox.Items.Count : relationIndex + 1, ObjectJudgementMenuText);
            }
            if (!functionListBox.Items.Contains(ObjectDefinitionMenuText))
            {
                int objectJudgementIndex = functionListBox.Items.IndexOf(ObjectJudgementMenuText);
                functionListBox.Items.Insert(
                    objectJudgementIndex < 0 ? functionListBox.Items.Count : objectJudgementIndex + 1,
                    ObjectDefinitionMenuText);
            }
            if (!functionListBox.Items.Contains(ObjectDetectionParameterMenuText))
            {
                int objectDefinitionIndex = functionListBox.Items.IndexOf(ObjectDefinitionMenuText);
                functionListBox.Items.Insert(
                    objectDefinitionIndex < 0 ? functionListBox.Items.Count : objectDefinitionIndex + 1,
                    ObjectDetectionParameterMenuText);
            }
            if (!functionListBox.Items.Contains(ObjectDetectionResultReviewMenuText))
            {
                int objectDetectionParameterIndex = functionListBox.Items.IndexOf(ObjectDetectionParameterMenuText);
                functionListBox.Items.Insert(
                    objectDetectionParameterIndex < 0 ? functionListBox.Items.Count : objectDetectionParameterIndex + 1,
                    ObjectDetectionResultReviewMenuText);
            }
            MoveFunctionListItemAfter(FindObjectFlowMenuText, RoiMenuText);
            MoveFunctionListItemAfter(ImagePreprocessingMenuText, FindObjectFlowMenuText);
            MoveFunctionListItemAfter(ImageProcessingMenuText, ImagePreprocessingMenuText);
            MoveFunctionListItemAfter(ImageRelationMenuText, ImageProcessingMenuText);
            MoveFunctionListItemAfter(ObjectJudgementMenuText, ImageRelationMenuText);
            MoveFunctionListItemAfter(ObjectDefinitionMenuText, ObjectJudgementMenuText);
            RebuildVisibleImageRelations();
            RebuildVisibleObjectJudgements();
            RebuildVisibleObjectDefinitions();
            RebuildVisibleObjectDetectionParameters();

            if (!IsRunningInDesigner())
            {
                InitializeImageDisplayControls();
            }
        }

        private bool IsRunningInDesigner()
        {
            if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return true;
            }

            string processName = Process.GetCurrentProcess().ProcessName;
            return string.Equals(processName, "devenv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "XDesProc", StringComparison.OrdinalIgnoreCase);
        }

        private void MoveFunctionListItemAfter(string itemText, string previousItemText)
        {
            int itemIndex = functionListBox.Items.IndexOf(itemText);
            if (itemIndex >= 0)
            {
                functionListBox.Items.RemoveAt(itemIndex);
            }

            int previousIndex = functionListBox.Items.IndexOf(previousItemText);
            int insertIndex = previousIndex < 0
                ? functionListBox.Items.Count
                : previousIndex + 1;
            functionListBox.Items.Insert(insertIndex, itemText);
        }

        private void InitializeImageDisplayControls()
        {
            AddPreprocessedImageTabs();
            AddBlockProcessingImageTabs();
            leftOriginalDisplayControl = CreateImageDisplayControl(leftOriginalDisplayHostPanel, "左側 原圖");
            leftPreprocessedDisplayControl = CreateImageDisplayControl(leftPreprocessedDisplayHostPanel, "左側 前處理");
            leftProcessedDisplayControl = CreateImageDisplayControl(leftProcessedDisplayHostPanel, "左側 處理後");
            leftBlockProcessingDisplayControl = CreateImageDisplayControl(leftBlockProcessingDisplayHostPanel, "左側 區塊處理");
            leftObjectsDisplayControl = CreateImageDisplayControl(leftObjectsDisplayHostPanel, "左側 區塊結果");
            leftDebugDisplayControl = CreateImageDisplayControl(leftDebugDisplayHostPanel, "左側 debug");
            rightOriginalDisplayControl = CreateImageDisplayControl(rightOriginalDisplayHostPanel, "右側 原圖");
            rightPreprocessedDisplayControl = CreateImageDisplayControl(rightPreprocessedDisplayHostPanel, "右側 前處理");
            rightProcessedDisplayControl = CreateImageDisplayControl(rightProcessedDisplayHostPanel, "右側 處理後");
            rightBlockProcessingDisplayControl = CreateImageDisplayControl(rightBlockProcessingDisplayHostPanel, "右側 區塊處理");
            rightObjectsDisplayControl = CreateImageDisplayControl(rightObjectsDisplayHostPanel, "右側 區塊結果");
            rightDebugDisplayControl = CreateImageDisplayControl(rightDebugDisplayHostPanel, "右側 debug");
            EnsureObjectDetectionMeasurementDisplay();
            EnsureObjectDetectionFlatFieldPreviewDisplay();
            EnsureObjectDetectionDefectDisplay();

            WireImageDisplaySynchronization();
        }

        private void AddPreprocessedImageTabs()
        {
            if (leftPreprocessedTabPage != null && rightPreprocessedTabPage != null)
            {
                return;
            }

            leftPreprocessedTabPage = CreateImageTabPage("leftPreprocessedTabPage", "前處理", out leftPreprocessedDisplayHostPanel);
            rightPreprocessedTabPage = CreateImageTabPage("rightPreprocessedTabPage", "前處理", out rightPreprocessedDisplayHostPanel);
            leftImageTabControl.TabPages.Insert(leftImageTabControl.TabPages.IndexOf(leftProcessedTabPage), leftPreprocessedTabPage);
            rightImageTabControl.TabPages.Insert(rightImageTabControl.TabPages.IndexOf(rightProcessedTabPage), rightPreprocessedTabPage);
        }

        private static TabPage CreateImageTabPage(string name, string text, out Panel hostPanel)
        {
            var tabPage = new TabPage();
            tabPage.Name = name;
            tabPage.Text = text;
            tabPage.BackColor = Color.FromArgb(250, 251, 253);
            tabPage.Padding = new Padding(12);
            hostPanel = new Panel();
            hostPanel.BackColor = Color.White;
            hostPanel.BorderStyle = BorderStyle.FixedSingle;
            hostPanel.Dock = DockStyle.Fill;
            tabPage.Controls.Add(hostPanel);
            return tabPage;
        }

        private static ImageDisplayControl CreateImageDisplayControl(Control host, string title)
        {
            host.Controls.Clear();

            var displayControl = new ImageDisplayControl();
            displayControl.Dock = DockStyle.Fill;
            displayControl.TitleText = title;
            host.Controls.Add(displayControl);
            return displayControl;
        }

        private void WireImageDisplaySynchronization()
        {
            leftOriginalDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftPreprocessedDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftProcessedDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftBlockProcessingDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftObjectsDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftDebugDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            objectDetectionMeasurementDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            objectDetectionFlatFieldPreviewDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightOriginalDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightPreprocessedDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightProcessedDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightBlockProcessingDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightObjectsDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightDebugDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;

            leftOriginalDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftPreprocessedDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftProcessedDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftBlockProcessingDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftObjectsDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftDebugDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            objectDetectionMeasurementDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            objectDetectionFlatFieldPreviewDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightOriginalDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightPreprocessedDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightProcessedDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightBlockProcessingDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightObjectsDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightDebugDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;

            leftImageTabControl.SelectedIndexChanged += VisibleImageTabControl_SelectedIndexChanged;
            rightImageTabControl.SelectedIndexChanged += VisibleImageTabControl_SelectedIndexChanged;
            leftImageTabControl.MouseDoubleClick += ImageTabControl_MouseDoubleClick;
            rightImageTabControl.MouseDoubleClick += ImageTabControl_MouseDoubleClick;

            leftOriginalDisplayControl.RoiSelected += ImageDisplayControl_RoiSelected;
            rightOriginalDisplayControl.RoiSelected += ImageDisplayControl_RoiSelected;
            leftOriginalDisplayControl.RoiEdited += ImageDisplayControl_RoiEdited;
            rightOriginalDisplayControl.RoiEdited += ImageDisplayControl_RoiEdited;
            leftProcessedDisplayControl.LargeImageOverlayPaint += ProcessedDisplayControl_LargeImageOverlayPaint;
            leftBlockProcessingDisplayControl.LargeImageOverlayPaint += BlockProcessingDisplayControl_LargeImageOverlayPaint;
            leftObjectsDisplayControl.LargeImageOverlayPaint += ObjectDefinitionDisplayControl_LargeImageOverlayPaint;
            leftObjectsDisplayControl.ImagePointerMoved += ObjectDefinitionDisplayControl_ImagePointerMoved;
            objectDetectionMeasurementDisplayControl.LargeImageOverlayPaint +=
                ObjectDetectionMeasurementDisplayControl_LargeImageOverlayPaint;
            rightProcessedDisplayControl.LargeImageOverlayPaint += ProcessedDisplayControl_LargeImageOverlayPaint;
            rightBlockProcessingDisplayControl.LargeImageOverlayPaint += BlockProcessingDisplayControl_LargeImageOverlayPaint;
            rightObjectsDisplayControl.LargeImageOverlayPaint += ObjectDefinitionDisplayControl_LargeImageOverlayPaint;
            rightObjectsDisplayControl.ImagePointerMoved += ObjectDefinitionDisplayControl_ImagePointerMoved;
            imageProcessingDebounceTimer = new System.Windows.Forms.Timer();
            imageProcessingDebounceTimer.Interval = 200;
            imageProcessingDebounceTimer.Tick += ImageProcessingDebounceTimer_Tick;
        }

        private List<int> GetSelectedImageProcessingStepIndexes()
        {
            var stepIndexes = new List<int>();
            foreach (object selectedItem in functionListBox.SelectedItems)
            {
                int stepIndex = GetImageProcessingStepIndex(selectedItem as string);
                if (stepIndex >= 0 && !stepIndexes.Contains(stepIndex))
                {
                    stepIndexes.Add(stepIndex);
                }
            }

            stepIndexes.Sort();
            return stepIndexes;
        }

        private void BeginParameterApplyStatus(bool preprocessing)
        {
            ResetPipelineTiming();
            parameterApplyStopwatch = Stopwatch.StartNew();
            parameterApplyInProgress = true;
            parameterApplyIsPreprocessing = preprocessing;
            if (!preprocessing)
            {
                imageProcessingDisplayPending = false;
            }
            SetParameterApplyStatus("影像處理中...");
        }

        private void ResetPipelineTiming()
        {
            lastPreprocessingElapsedMilliseconds = 0;
            lastImageProcessingElapsedMilliseconds = 0;
            lastObjectJudgementElapsedMilliseconds = 0;
            lastDisplayProcessingElapsedMilliseconds = 0;
            includeImageProcessingTimeOnNextDisplay = false;
            imageProcessingDisplayPending = false;
        }

        private string BuildPipelineTimingText(
            bool includeImageProcessing,
            bool includeObjectJudgement,
            string displayTimeText = null)
        {
            long preprocessing = Math.Max(0, lastPreprocessingElapsedMilliseconds);
            long imageProcessing = includeImageProcessing
                ? Math.Max(0, lastImageProcessingElapsedMilliseconds)
                : 0;
            long objectJudgement = includeObjectJudgement
                ? Math.Max(0, lastObjectJudgementElapsedMilliseconds)
                : 0;
            string display = string.IsNullOrWhiteSpace(displayTimeText)
                ? Math.Max(0, lastDisplayProcessingElapsedMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms"
                : displayTimeText;
            // "影像處理全部時間" is the algorithm pipeline only. Display
            // conversion/rendering is reported separately and is not included.
            long total = preprocessing + imageProcessing + objectJudgement;

            string text = string.Format(
                CultureInfo.InvariantCulture,
                "影像處理全部時間：{0} ms || 影像前處理時間：{1} ms",
                total,
                preprocessing);
            if (includeImageProcessing)
            {
                text += string.Format(
                    CultureInfo.InvariantCulture,
                    " || 影像處理時間：{0} ms",
                    imageProcessing);
            }
            if (includeObjectJudgement)
            {
                text += string.Format(
                    CultureInfo.InvariantCulture,
                    " || 整合成區塊處理：{0} ms",
                    objectJudgement);
            }
            return text + " || 顯示時間：" + display;
        }

        private void SetParameterApplyStatus(string text)
        {
            if (parameterApplyStatusLabel != null && !parameterApplyStatusLabel.IsDisposed)
            {
                parameterApplyStatusLabel.Text = text;
            }
        }

        private void ReportBackgroundStatus(string text)
        {
            if (string.IsNullOrEmpty(text) || IsDisposed)
            {
                return;
            }

            lock (backgroundStatusLock)
            {
                pendingBackgroundStatusText = text;
                if (backgroundStatusTimer != null)
                {
                    return;
                }

                backgroundStatusTimer = new System.Threading.Timer(
                    delegate { FlushBackgroundStatus(); },
                    null,
                    250,
                    System.Threading.Timeout.Infinite);
            }
        }

        private void FlushBackgroundStatus()
        {
            string text;
            lock (backgroundStatusLock)
            {
                text = pendingBackgroundStatusText;
                pendingBackgroundStatusText = null;
                if (backgroundStatusTimer != null)
                {
                    backgroundStatusTimer.Dispose();
                    backgroundStatusTimer = null;
                }
            }

            if (string.IsNullOrEmpty(text) || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (!IsDisposed)
                    {
                        statusLabel.Text = text;
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // The form can close while a background operation is finishing.
            }
        }

        private void CompleteParameterApplyStatus(bool displayPending = false)
        {
            if (!parameterApplyInProgress || parameterApplyStopwatch == null)
            {
                return;
            }

            long applyElapsedMilliseconds = parameterApplyStopwatch.ElapsedMilliseconds;
            parameterApplyStopwatch.Stop();
            parameterApplyInProgress = false;
            string operationLabel = parameterApplyIsPreprocessing ? "前處理時間" : "影像處理時間";
            long operationElapsed = parameterApplyIsPreprocessing
                ? lastPreprocessingElapsedMilliseconds
                : lastImageProcessingElapsedMilliseconds;
            bool waitForDisplay = !parameterApplyIsPreprocessing && displayPending;
            imageProcessingDisplayPending = waitForDisplay;
            // If a real preview was measured by the display path, preserve it.
            // Otherwise derive only the unmeasured display remainder. For an
            // image-processing request the apply stopwatch can also include
            // preprocessing, so remove that stage before deriving display time.
            if (waitForDisplay)
            {
                lastDisplayProcessingElapsedMilliseconds = 0;
            }
            else if (lastDisplayProcessingElapsedMilliseconds <= 0)
            {
                long measuredStages = operationElapsed +
                    (parameterApplyIsPreprocessing ? 0 : lastPreprocessingElapsedMilliseconds);
                lastDisplayProcessingElapsedMilliseconds = Math.Max(
                    0,
                    applyElapsedMilliseconds - measuredStages);
            }
            string displayTimeText = waitForDisplay
                ? "待顯示"
                : lastDisplayProcessingElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms";
            string operationName = parameterApplyIsPreprocessing
                ? (selectedImagePreprocessingStepIndex >= 0 &&
                   selectedImagePreprocessingStepIndex < systemParameters.ImagePreprocessingSteps.Count
                    ? CreateImagePreprocessingStepText(selectedImagePreprocessingStepIndex + 1).Trim()
                    : GetTimingGroupName(selectedImagePreprocessingGroupId, true))
                : (selectedImageProcessingStepIndex >= 0 &&
                   selectedImageProcessingStepIndex < systemParameters.ImageProcessingSteps.Count
                    ? CreateImageProcessingStepText(selectedImageProcessingStepIndex + 1).Trim()
                    : GetTimingGroupName(selectedImageProcessingGroupId, false));
            statusLabel.Text = operationName + ": " + BuildPipelineTimingText(
                !parameterApplyIsPreprocessing,
                false,
                displayTimeText);
            SetParameterApplyStatus(string.Format(
                CultureInfo.InvariantCulture,
                "完成：{0}\r\n{1}：{2} ms || 預覽圖處理時間：{3}",
                BuildPipelineTimingText(
                    !parameterApplyIsPreprocessing,
                    false,
                    displayTimeText),
                operationLabel,
                operationElapsed,
                displayTimeText));
        }

        private string GetTimingGroupName(string groupId, bool preprocessing)
        {
            ImageProcessingGroupSettings group = preprocessing
                ? FindImagePreprocessingGroup(groupId)
                : FindImageProcessingGroup(groupId);
            string name = group == null || string.IsNullOrWhiteSpace(group.DisplayName)
                ? "未命名群組"
                : group.DisplayName;
            return (preprocessing ? "前處理群組(" : "群組(") + name + ")";
        }

        private void ApplyPendingImageProcessingParameters()
        {
            if (selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count ||
                pendingImageProcessingParameters == null)
            {
                return;
            }

            string committed = FormatImageProcessingParameters(pendingImageProcessingParameters);
            if (committed == systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters)
            {
                return;
            }

            CapturePreprocessedImageViewState();
            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters = committed;
            SaveSystemParameters();
            imageProcessingExecutionRequested = true;
            BeginParameterApplyStatus(false);
            MarkProcessedImageDirty();
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = "已套用處理" + (selectedImageProcessingStepIndex + 1) + " 參數";
        }

        private string GetImageProcessingParameterValue(string key, string defaultValue)
        {
            Dictionary<string, string> parameters = pendingImageProcessingParameters ??
                ParseImageProcessingParameters(GetSelectedImageProcessingStepParameters());
            string value;
            if (key == "CoreWidth" &&
                !parameters.ContainsKey("CoreWidth") &&
                parameters.TryGetValue("EdgeWidth", out value))
            {
                return value;
            }

            return parameters.TryGetValue(key, out value) ? value : defaultValue;
        }

        private string GetSelectedImageProcessingStepParameters()
        {
            return selectedImageProcessingStepIndex >= 0 && selectedImageProcessingStepIndex < systemParameters.ImageProcessingSteps.Count
                ? systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters
                : string.Empty;
        }

        private void SaveImageProcessingParameter(string key, string value)
        {
            if (string.IsNullOrEmpty(key) ||
                selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            Dictionary<string, string> parameters = ParseImageProcessingParameters(systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters);
            bool removedLegacyCoreWidth = key == "CoreWidth" && parameters.Remove("EdgeWidth");
            string previousValue;
            if (parameters.TryGetValue(key, out previousValue) &&
                string.Equals(previousValue, value ?? string.Empty, StringComparison.Ordinal) &&
                !removedLegacyCoreWidth)
            {
                return;
            }

            CapturePreprocessedImageViewState();
            parameters[key] = value ?? string.Empty;
            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters = FormatImageProcessingParameters(parameters);
            SaveSystemParameters();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已更新處理" + (selectedImageProcessingStepIndex + 1) + " 參數";
        }

        private void SetPendingImageProcessingParameter(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (pendingImageProcessingParameters == null)
            {
                pendingImageProcessingParameters = ParseImageProcessingParameters(GetSelectedImageProcessingStepParameters());
            }

            if (key == "CoreWidth")
            {
                pendingImageProcessingParameters.Remove("EdgeWidth");
            }

            pendingImageProcessingParameters[key] = value ?? string.Empty;
        }

        private static Dictionary<string, string> ParseImageProcessingParameters(string parameterText)
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(parameterText))
            {
                return parameters;
            }

            string[] pairs = parameterText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string pair in pairs)
            {
                int separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = pair.Substring(0, separator).Trim();
                string value = pair.Substring(separator + 1).Trim();
                parameters[key] = value;
            }

            return parameters;
        }

        private static string FormatImageProcessingParameters(Dictionary<string, string> parameters)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                parts.Add(parameter.Key + "=" + parameter.Value);
            }

            return string.Join(";", parts.ToArray());
        }

        private static string CreateDefaultImageProcessingParameters(string method)
        {
            if (method == "Polarity Edge")
            {
                return "Polarity=Any;ContrastThreshold=20;CoreWidth=3;Smoothing=1;GaussianSigma=0.5;SearchDirection=Any;BorderType=Reflect";
            }

            if (method == "Canny Edge")
            {
                return "LowThreshold=50;HighThreshold=150;KernelSize=3;L2Gradient=false;GaussianBlurSize=5;GaussianSigma=1.4";
            }

            if (method == "Sobel Edge")
            {
                return "Direction=Any;KernelSize=3;Threshold=30";
            }

            if (method == "Global Threshold")
            {
                return "ThresholdMode=Single;Threshold=128;LowerThreshold=0;UpperThreshold=255;MaxValue=255;ThresholdType=Binary";
            }

            if (method == "Adaptive Threshold")
            {
                return "MaxValue=255;AdaptiveMethod=GaussianC;ThresholdType=Binary;BlockSize=11;C=2";
            }

            if (method == "Otsu Threshold")
            {
                return "MaxValue=255;ThresholdType=Binary";
            }

            return string.Empty;
        }

        private static bool IsEdgeDetectionMethod(string method)
        {
            return method == "Polarity Edge" ||
                method == "Canny Edge" ||
                method == "Sobel Edge";
        }

        private static bool IsThresholdMethod(string method)
        {
            return method == "Global Threshold" ||
                method == "Adaptive Threshold" ||
                method == "Otsu Threshold";
        }

        private static bool IsBinaryMaskProcessingMethod(string method)
        {
            return IsEdgeDetectionMethod(method) || IsThresholdMethod(method);
        }

        private bool HasPreviewableImageProcessingStep()
        {
            return FindFirstPreviewableImageProcessingStepIndex() >= 0;
        }

        private List<ImageProcessingStepSettings> GetSelectedImageProcessingSteps()
        {
            var steps = new List<ImageProcessingStepSettings>();
            if (selectedImageProcessingStepIndex >= 0 &&
                selectedImageProcessingStepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                ImageProcessingStepSettings selectedStep =
                    systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex];
                return GetImageProcessingExecutionChain(selectedStep);
            }

            if (!string.IsNullOrWhiteSpace(selectedImageProcessingGroupId))
            {
                CollectImageProcessingGroupSteps(selectedImageProcessingGroupId, steps);
            }

            return steps;
        }

        private List<ImageProcessingStepSettings> GetDisplayedImageProcessingSteps()
        {
            var steps = new List<ImageProcessingStepSettings>();
            if (displayedImageProcessingStepIndex >= 0 &&
                displayedImageProcessingStepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                return GetImageProcessingExecutionChain(
                    systemParameters.ImageProcessingSteps[displayedImageProcessingStepIndex]);
            }

            if (!string.IsNullOrWhiteSpace(displayedImageProcessingGroupId))
            {
                CollectImageProcessingGroupSteps(displayedImageProcessingGroupId, steps);
            }

            return steps;
        }

        private List<ImageProcessingStepSettings> GetImageProcessingExecutionChain(
            ImageProcessingStepSettings selectedStep)
        {
            var chain = new List<ImageProcessingStepSettings>();
            if (selectedStep == null)
            {
                return chain;
            }

            if (string.IsNullOrWhiteSpace(selectedStep.GroupId))
            {
                chain.Add(selectedStep);
                return chain;
            }

            var groupSteps = new List<ImageProcessingStepSettings>();
            CollectImageProcessingGroupSteps(selectedStep.GroupId, groupSteps);
            foreach (ImageProcessingStepSettings groupStep in groupSteps)
            {
                if (groupStep == null)
                {
                    continue;
                }

                chain.Add(groupStep);
                if (ReferenceEquals(groupStep, selectedStep) ||
                    string.Equals(groupStep.Id, selectedStep.Id, StringComparison.Ordinal))
                {
                    break;
                }
            }

            return chain;
        }

        private void CollectImageProcessingGroupSteps(string groupId, List<ImageProcessingStepSettings> steps)
        {
            foreach (ImageProcessingStepSettings step in systemParameters.ImageProcessingSteps)
            {
                if (string.Equals(step.GroupId, groupId, StringComparison.Ordinal))
                {
                    steps.Add(step);
                }
            }

            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(group.ParentGroupId, groupId, StringComparison.Ordinal))
                {
                    CollectImageProcessingGroupSteps(group.Id, steps);
                }
            }
        }

        private bool HasSelectedPreviewableImageProcessingSteps()
        {
            if (!string.IsNullOrWhiteSpace(activeImageRelationGroupId))
            {
                return HasSelectedImageRelationGroupPreviewableSteps();
            }

            List<ImageProcessingStepSettings> steps = GetSelectedImageProcessingSteps();
            if (steps.Count == 0)
            {
                return false;
            }

            foreach (ImageProcessingStepSettings step in steps)
            {
                if (!IsBinaryMaskProcessingMethod(step.Method))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasDisplayedPreviewableImageProcessingSteps()
        {
            if (!string.IsNullOrWhiteSpace(displayedImageRelationGroupId))
            {
                List<ImageRelationSettings> relations =
                    GetImageRelationGroupRelations(displayedImageRelationGroupId);
                if (relations.Count == 0)
                {
                    return false;
                }

                foreach (ImageRelationSettings relation in relations)
                {
                    List<ImageProcessingStepSettings> relationSteps =
                        GetImageProcessingStepsForRelation(relation);
                    if (relationSteps.Count == 0 ||
                        relationSteps.Any(step => !IsBinaryMaskProcessingMethod(step.Method)))
                    {
                        return false;
                    }
                }

                return true;
            }

            List<ImageProcessingStepSettings> steps = GetDisplayedImageProcessingSteps();
            return steps.Count > 0 &&
                steps.All(step => IsBinaryMaskProcessingMethod(step.Method));
        }

        private void MarkProcessedImageDirty(bool clearCachedResults = true)
        {
            // Capture the view that is actually on screen before processing
            // invalidates sources or replaces preview images. This keeps the
            // current zoom/pan stable even when processing starts from another
            // tab or switches between bitmap and large-image sources.
            CaptureSharedImageViewStateFromVisibleControls();
            CaptureProcessedImageViewState();
            processedImageDirty = true;
            if (!clearCachedResults)
            {
                return;
            }

            imageProcessingStepElapsedMilliseconds.Clear();
            ClearLargeProcessedOverlayCache();
            ClearProcessedBinaryMaskCache();
            ClearProcessedImageCache();
            latestProcessedImageCacheKey = null;
            if (latestProcessedImage != null)
            {
                latestProcessedImage.Dispose();
                latestProcessedImage = null;
            }
        }

        private bool HasCachedProcessedImageForCurrentSelection()
        {
            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage)
            {
                return false;
            }

            if (!rightOriginalDisplayControl.IsLargeImageMode)
            {
                string cacheKey = CreateProcessedImageCacheKey();
                if (string.IsNullOrWhiteSpace(cacheKey))
                {
                    return false;
                }

                return (string.Equals(
                            latestProcessedImageCacheKey,
                            cacheKey,
                            StringComparison.Ordinal) &&
                        latestProcessedImage != null) ||
                    processedImageCache.ContainsKey(cacheKey);
            }

            List<ImageProcessingStepSettings> steps = GetDisplayedImageProcessingSteps();
            if (systemParameters.RoiRegions.Count == 0 || steps.Count == 0 ||
                steps.Any(step => step == null || !IsBinaryMaskProcessingMethod(step.Method)))
            {
                return false;
            }

            foreach (Rectangle roi in systemParameters.RoiRegions.Select(region => region.Bounds))
            {
                if (roi.Width <= 0 || roi.Height <= 0)
                {
                    return false;
                }

                foreach (ImageProcessingStepSettings step in steps)
                {
                    string maskKey = CreateLargeProcessedMaskKey(roi, step);
                    if (!largeImageMaskCache.Contains(maskKey))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void MarkProcessedPreviewDirty()
        {
            processedImageDirty = true;
        }

        private void ClearProcessedImageCache()
        {
            foreach (Bitmap image in processedImageCache.Values)
            {
                image.Dispose();
            }

            processedImageCache.Clear();
        }

        private void ClearLargeProcessedOverlayCache()
        {
            ClearLargeProcessedOverlayBitmapsOnly();
            largeImageMaskCache.Clear();
        }

        private void ClearLargeRoiGrayCache()
        {
            ClearLargeRelationSourceCache();
            largeRoiGrayscaleCache.Clear();
        }

        private byte[,] GetOrCreateLargeRoiGrayCache(LargeImageSource source, Rectangle roi)
        {
            return largeRoiGrayscaleCache.GetOrCreateRoiGray(
                source,
                roi,
                delegate(LargeImageSource cacheSource, Rectangle cacheRoi)
                {
                    return cacheSource.CreateGrayRegionFromTiles(cacheRoi);
                });
        }

        private bool[,] CreateLargeEdgeMask(byte[,] gray, string method, Dictionary<string, string> parameters)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var mask = OpenCvImageProcessingService.CreateBinaryMask(
                source,
                method,
                parameters,
                "Canny Edge"))
            {
                return ConvertOpenCvBinaryMask(mask);
            }
        }
        private static bool[,] CreateOpenCvFilteredMask(
            Cv.Mat edgeMask, int maxGap, int minEdgeLength, string edgeSelection, Cv.Mat strength)
        {
            int width = edgeMask.Width;
            int height = edgeMask.Height;
            if (maxGap > 0)
            {
                int kernelSize = EnsureOdd(Math.Max(3, (maxGap * 2) + 1));
                using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(Cv.MorphShapes.Rect, new Cv.Size(kernelSize, kernelSize)))
                {
                    Cv.Cv2.MorphologyEx(edgeMask, edgeMask, Cv.MorphTypes.Close, kernel);
                }
            }

            bool requiresSelection = !string.Equals(edgeSelection, "All", StringComparison.OrdinalIgnoreCase);
            if (minEdgeLength <= 1 && !requiresSelection)
            {
                return ConvertOpenCvBinaryMask(edgeMask);
            }

            using (var labels = new Cv.Mat())
            using (var stats = new Cv.Mat())
            using (var centroids = new Cv.Mat())
            {
                int labelCount = Cv.Cv2.ConnectedComponentsWithStats(
                    edgeMask,
                    labels,
                    stats,
                    centroids,
                    Cv.PixelConnectivity.Connectivity8,
                    Cv.MatType.CV_32SC1);
                var accepted = new bool[labelCount];
                var areas = new int[labelCount];
                for (int label = 1; label < labelCount; label++)
                {
                    areas[label] = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area);
                    accepted[label] = areas[label] >= minEdgeLength;
                }

                var result = new bool[width, height];
                int[] row = new int[width];
                float[] strengthRow = strength != null && !strength.Empty() ? new float[width] : null;
                var totalStrength = strengthRow == null ? null : new double[labelCount];
                long stride = labels.Step();
                if (!requiresSelection && totalStrength == null)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(labels.Data + (int)(y * stride), row, 0, width);
                        for (int x = 0; x < width; x++)
                        {
                            int label = row[x];
                            result[x, y] = label > 0 && label < accepted.Length && accepted[label];
                        }
                    }

                    return result;
                }

                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(labels.Data + (int)(y * stride), row, 0, width);
                    if (strengthRow != null)
                    {
                        Marshal.Copy(strength.Data + (int)(y * strength.Step()), strengthRow, 0, width);
                    }

                    for (int x = 0; x < width; x++)
                    {
                        int label = row[x];
                        if (label > 0 && label < accepted.Length && accepted[label] && totalStrength != null)
                        {
                            totalStrength[label] += strengthRow[x];
                        }
                    }
                }

                int selectedLabel = GetOpenCvSelectedLabel(edgeSelection, accepted, areas, totalStrength);
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(labels.Data + (int)(y * stride), row, 0, width);
                    for (int x = 0; x < width; x++)
                    {
                        int label = row[x];
                        result[x, y] = label > 0 && label < accepted.Length && accepted[label] &&
                            (selectedLabel == 0 || label == selectedLabel);
                    }
                }

                return result;
            }
        }

        private static int GetOpenCvSelectedLabel(string edgeSelection, bool[] accepted, int[] areas, double[] totalStrength)
        {
            if (string.Equals(edgeSelection, "All", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int selected = 0;
            for (int label = 1; label < accepted.Length; label++)
            {
                if (!accepted[label])
                {
                    continue;
                }

                if (selected == 0 ||
                    (string.Equals(edgeSelection, "Last", StringComparison.OrdinalIgnoreCase) && label > selected) ||
                    (string.Equals(edgeSelection, "Longest", StringComparison.OrdinalIgnoreCase) && areas[label] > areas[selected]) ||
                    (string.Equals(edgeSelection, "Strongest", StringComparison.OrdinalIgnoreCase) &&
                        (totalStrength == null ? areas[label] : totalStrength[label]) >
                        (totalStrength == null ? areas[selected] : totalStrength[selected])))
                {
                    selected = label;
                }
            }

            return selected;
        }

        private static bool[,] ConvertOpenCvBinaryMask(Cv.Mat source)
        {
            int width = source.Width;
            int height = source.Height;
            var result = new bool[width, height];
            byte[] row = new byte[width];
            long stride = source.Step();
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(source.Data + (int)(y * stride), row, 0, width);
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = row[x] != 0;
                }
            }

            return result;
        }

        private static int[,] CreateOpenCvSobelMagnitudes(
            Cv.Mat gradientX, Cv.Mat gradientY, string direction, string outputMode, double scale, int delta)
        {
            int width = gradientX.Width;
            int height = gradientX.Height;
            float[] valuesX = ReadOpenCvFloatMat(gradientX);
            float[] valuesY = ReadOpenCvFloatMat(gradientY);
            var magnitudes = new int[width, height];
            bool xOnly = string.Equals(outputMode, "XOnly", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase);
            bool yOnly = string.Equals(outputMode, "YOnly", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    double value = xOnly ? Math.Abs(valuesX[index]) :
                        yOnly ? Math.Abs(valuesY[index]) : Math.Sqrt((valuesX[index] * valuesX[index]) + (valuesY[index] * valuesY[index]));
                    magnitudes[x, y] = ClampInt((int)Math.Round((value * scale) + delta), 0, int.MaxValue);
                }
            }

            return magnitudes;
        }

        private static float[] CreateOpenCvDirectedGradient(Cv.Mat gradientX, Cv.Mat gradientY, string direction)
        {
            float[] valuesX = ReadOpenCvFloatMat(gradientX);
            float[] valuesY = ReadOpenCvFloatMat(gradientY);
            var result = new float[valuesX.Length];
            if (string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase))
            {
                Array.Copy(valuesX, result, result.Length);
            }
            else if (string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase))
            {
                Array.Copy(valuesY, result, result.Length);
            }
            else
            {
                for (int index = 0; index < result.Length; index++)
                {
                    result[index] = Math.Abs(valuesX[index]) >= Math.Abs(valuesY[index]) ? valuesX[index] : valuesY[index];
                }
            }

            return result;
        }

        private static float[] ReadOpenCvFloatMat(Cv.Mat source)
        {
            int width = source.Width;
            int height = source.Height;
            var values = new float[checked(width * height)];
            float[] row = new float[width];
            long stride = source.Step();
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(source.Data + (int)(y * stride), row, 0, width);
                Array.Copy(row, 0, values, y * width, width);
            }

            return values;
        }

        private static int[,] CreateOpenCvMagnitudeForFiltering(Cv.Mat blurred, int kernelSize, bool l2Gradient)
        {
            int width = blurred.Width;
            int height = blurred.Height;
            using (var gradientX = new Cv.Mat())
            using (var gradientY = new Cv.Mat())
            using (var magnitude = new Cv.Mat())
            {
                int aperture = EnsureOdd(Math.Max(3, kernelSize));
                Cv.Cv2.Sobel(blurred, gradientX, Cv.MatType.CV_32FC1, 1, 0, aperture);
                Cv.Cv2.Sobel(blurred, gradientY, Cv.MatType.CV_32FC1, 0, 1, aperture);
                if (l2Gradient)
                {
                    Cv.Cv2.Magnitude(gradientX, gradientY, magnitude);
                }
                else
                {
                    Cv.Cv2.Absdiff(gradientX, Cv.Scalar.All(0), gradientX);
                    Cv.Cv2.Absdiff(gradientY, Cv.Scalar.All(0), gradientY);
                    Cv.Cv2.Add(gradientX, gradientY, magnitude);
                }

                float[] values = ReadOpenCvFloatMat(magnitude);
                var result = new int[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        result[x, y] = ClampInt((int)Math.Round(values[(y * width) + x]), 0, int.MaxValue);
                    }
                }

                return result;
            }
        }

        private void ClearLargeProcessedOverlayBitmapsOnly()
        {
            foreach (Bitmap overlay in largeProcessedOverlayCache.Values)
            {
                overlay.Dispose();
            }

            largeProcessedOverlayCache.Clear();
            pendingLargeProcessedOverlayTiles.Clear();
            pendingLargeProcessedViewportOverlays.Clear();
            pendingLargeProcessedOverviewOverlays.Clear();
        }

        private void ClearProcessedPreviewImages()
        {
            MarkProcessedImageDirty();
            isSyncingImageView = true;
            try
            {
                leftProcessedDisplayControl.ClearRoiOverlay();
                rightProcessedDisplayControl.ClearRoiOverlay();
                leftProcessedDisplayControl.ClearImage();
                rightProcessedDisplayControl.ClearImage();
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ScheduleProcessedImageUpdateIfVisible()
        {
            if (imageProcessingDebounceTimer == null)
            {
                return;
            }

            // A large-image result is retained as an OpenCV mask, so prepare it
            // after parameter changes even when the user is currently viewing
            // the original tab.  Otherwise the update is silently skipped.
            if (!rightOriginalDisplayControl.IsLargeImageMode &&
                !IsAnyProcessedTabVisible() &&
                !explicitProcessedImageUpdateRequested)
            {
                return;
            }

            imageProcessingDebounceTimer.Stop();
            imageProcessingDebounceTimer.Start();
        }

        private async void ImageProcessingDebounceTimer_Tick(object sender, EventArgs e)
        {
            imageProcessingDebounceTimer.Stop();
            await UpdateVisibleProcessedImageIfNeededAsync();
        }

        private void UpdateVisibleProcessedImageIfNeeded()
        {
            if (!rightOriginalDisplayControl.IsLargeImageMode &&
                !IsAnyProcessedTabVisible() &&
                !explicitProcessedImageUpdateRequested)
            {
                return;
            }

            BeginInvoke(new Action(async () => await UpdateVisibleProcessedImageIfNeededAsync()));
        }

        private void RequestExplicitProcessedImageUpdate()
        {
            if (imageProcessingDebounceTimer != null)
            {
                imageProcessingDebounceTimer.Stop();
            }

            // The user explicitly selected "處理". Do not route this request
            // through the parameter-edit debounce timer, which is allowed to
            // collapse repeated changes but must not discard a command.
            UpdateVisibleProcessedImageIfNeeded();
        }

        private async Task UpdateVisibleProcessedImageIfNeededAsync()
        {
            if (!imageProcessingExecutionRequested)
            {
                explicitProcessedImageUpdateRequested = false;
                return;
            }

            if (!HasSelectedPreviewableImageProcessingSteps())
            {
                processedImageDirty = false;
                explicitProcessedImageUpdateRequested = false;
                CompleteParameterApplyStatus();
                return;
            }

            if (ShouldUseRelationPreprocessedSource() && HasConfiguredImagePreprocessingSteps() && preprocessedImageDirty)
            {
                RequestPreprocessedImageUpdate();
                return;
            }

            // An explicit context-menu/A-key command is allowed to prepare
            // the result while the processed tab is hidden. Only visible tabs
            // are updated below, so background displays remain untouched.
            explicitProcessedImageUpdateRequested = false;

            // Large images use LargeImageSource plus a cached ROI mask instead of
            // latestProcessedImage. Treat that pipeline as complete once it has
            // been prepared; otherwise every refresh would start it again.
            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                if (processedImageDirty)
                {
                    PrepareLargeProcessedPreview();
                    processedImageDirty = false;
                }

                return;
            }

            if (processedImageDirty || latestProcessedImage == null)
            {
                string cacheKey = CreateProcessedImageCacheKey();
                Bitmap cachedImage;
                Bitmap processedImage;
                if (processedImageCache.TryGetValue(cacheKey, out cachedImage))
                {
                    processedImage = new Bitmap(cachedImage);
                }
                else
                {
                    statusLabel.Text = "影像處理運算中...";
                    processedImage = await Task.Run(() => CreateCurrentProcessedImage());
                    if (processedImage != null)
                    {
                        processedImageCache[cacheKey] = new Bitmap(processedImage);
                    }
                }
                if (processedImage == null)
                {
                    ClearProcessedPreviewImages();
                    statusLabel.Text = "請先載入圖片、指定 ROI，並選擇可預覽的處理方法";
                    return;
                }

                if (latestProcessedImage != null)
                {
                    latestProcessedImage.Dispose();
                }

                latestProcessedImage = processedImage;
                latestProcessedImageCacheKey = cacheKey;
                processedImageDirty = false;
            }

            bool wasDisplayPending = imageProcessingDisplayPending && IsAnyProcessedTabVisible();
            Stopwatch displayStopwatch = wasDisplayPending
                ? Stopwatch.StartNew()
                : null;
            ApplyLatestProcessedImageToVisibleTabs();
            RestoreProcessedImageViewState();
            RestorePreprocessedImageViewState();
            if (displayStopwatch != null)
            {
                displayStopwatch.Stop();
                imageProcessingDisplayPending = false;
                lastDisplayProcessingElapsedMilliseconds = Math.Max(
                    1,
                    displayStopwatch.ElapsedMilliseconds);
                UpdateProcessingTimingStatus(true);
                SetParameterApplyStatus(string.Format(
                    CultureInfo.InvariantCulture,
                    "完成：影像處理時間：{0} ms\r\n預覽圖處理時間：{1} ms",
                    lastImageProcessingElapsedMilliseconds,
                    lastDisplayProcessingElapsedMilliseconds));
            }
            else
            {
                SetParameterApplyStatus("產生預覽圖中...");
                statusLabel.Text = "影像處理完成";
                CompleteParameterApplyStatus(!IsAnyProcessedTabVisible());
            }
        }

        private string CreateProcessedImageCacheKey()
        {
            if (!string.IsNullOrWhiteSpace(activeImageRelationGroupId))
            {
                var relationParts = new List<string>
                {
                    systemParameters.LastImagePath ?? string.Empty,
                    "relation-group",
                    activeImageRelationGroupId
                };
                foreach (ImageRelationSettings relation in GetImageRelationGroupRelations(activeImageRelationGroupId))
                {
                    relationParts.Add(relation.Id ?? string.Empty);
                    relationParts.Add(relation.SourceType ?? string.Empty);
                    relationParts.Add(relation.SourceId ?? string.Empty);
                    relationParts.Add(relation.ProcessingType ?? string.Empty);
                    relationParts.Add(relation.ProcessingId ?? string.Empty);
                    foreach (ImageProcessingStepSettings step in GetImageProcessingStepsForRelation(relation))
                    {
                        relationParts.Add(step.Method ?? string.Empty);
                        relationParts.Add(step.Parameters ?? string.Empty);
                    }
                }
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = roiRegion.Bounds;
                    relationParts.Add(roi.X + "," + roi.Y + "," + roi.Width + "," + roi.Height);
                }

                return string.Join("|", relationParts.ToArray());
            }

            List<ImageProcessingStepSettings> selectedSteps = GetSelectedImageProcessingSteps();
            if (selectedSteps.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>
            {
                systemParameters.LastImagePath ?? string.Empty
            };
            foreach (ImageProcessingStepSettings step in selectedSteps)
            {
                parts.Add(step.Method ?? string.Empty);
                parts.Add(step.Parameters ?? string.Empty);
            }
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle roi = roiRegion.Bounds;
                parts.Add(roi.X + "," + roi.Y + "," + roi.Width + "," + roi.Height);
            }

            return string.Join("|", parts.ToArray());
        }

        private bool IsAnyProcessedTabVisible()
        {
            return (leftImageTabControl.Visible && leftImageTabControl.SelectedTab == leftProcessedTabPage) ||
                (rightImageTabControl.Visible && rightImageTabControl.SelectedTab == rightProcessedTabPage);
        }

        private bool IsAnyBlockProcessingTabVisible()
        {
            return (leftImageTabControl.Visible && leftImageTabControl.SelectedTab == leftBlockProcessingTabPage) ||
                (rightImageTabControl.Visible && rightImageTabControl.SelectedTab == rightBlockProcessingTabPage);
        }

        private void ApplyLatestProcessedImageToVisibleTabs()
        {
            if (latestProcessedImage == null || SkipImageDisplayUpdate())
            {
                return;
            }

            bool updateAllDependencyPreviews = objectDefinitionDependencyPreviewRequested;
            isSyncingImageView = true;
            try
            {
                if (updateAllDependencyPreviews || leftImageTabControl.SelectedTab == leftProcessedTabPage)
                {
                    leftProcessedDisplayControl.SetDisplayImage(new Bitmap(latestProcessedImage), true);
                    Rectangle? roi = GetSelectedRoi();
                    if (roi.HasValue)
                    {
                        leftProcessedDisplayControl.SetRoiOverlay(roi.Value);
                    }
                }

                if (updateAllDependencyPreviews || rightImageTabControl.SelectedTab == rightProcessedTabPage)
                {
                    rightProcessedDisplayControl.SetDisplayImage(new Bitmap(latestProcessedImage), true);
                    Rectangle? roi = GetSelectedRoi();
                    if (roi.HasValue)
                    {
                        rightProcessedDisplayControl.SetRoiOverlay(roi.Value);
                    }
                }
            }
            finally
            {
                isSyncingImageView = false;
            }

            ApplySharedImageViewStateToVisibleControls();
            if (updateAllDependencyPreviews)
            {
                objectDefinitionDependencyPreviewRequested = false;
            }
        }

        private Bitmap CreateCurrentProcessedImage()
        {
            if (!string.IsNullOrWhiteSpace(activeImageRelationGroupId))
            {
                return CreateCurrentRelationGroupProcessedImage();
            }

            List<ImageProcessingStepSettings> selectedSteps = GetSelectedImageProcessingSteps();
            if (systemParameters.RoiRegions.Count == 0 || !HasSelectedPreviewableImageProcessingSteps())
            {
                return null;
            }

            using (Bitmap sourceImage = ShouldUseRelationPreprocessedSource() && latestPreprocessedImage != null
                ? new Bitmap(latestPreprocessedImage)
                : rightOriginalDisplayControl.CloneImage())
            {
                if (sourceImage == null)
                {
                    return null;
                }

                var result = new Bitmap(sourceImage);
                using (Cv.Mat sourceGray = CreateOpenCvGrayMat(sourceImage))
                {
                    foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                    {
                        Rectangle roi = Rectangle.Intersect(
                            roiRegion.Bounds,
                            new Rectangle(0, 0, sourceImage.Width, sourceImage.Height));
                        if (roi.Width <= 0 || roi.Height <= 0)
                        {
                            continue;
                        }

                        using (Cv.Mat roiInput = new Cv.Mat(
                            sourceGray,
                            new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height)))
                        {
                            using (Cv.Mat combinedMask = CreateCombinedImageProcessingGroupMask(
                                roiInput,
                                roi,
                                selectedSteps,
                                CreateImageProcessingSourceNamespace()))
                            {
                                PaintRedOverlayImage(
                                    result,
                                    roi,
                                    ConvertOpenCvBinaryMask(combinedMask));
                            }
                        }
                    }
                }

                return result;
            }
        }

        private bool ShouldUseRelationPreprocessedSource()
        {
            return string.Equals(activeImageRelationSourceType, "Step", StringComparison.Ordinal) ||
                string.Equals(activeImageRelationSourceType, "Group", StringComparison.Ordinal);
        }

        private bool ShouldUseDisplayedRelationPreprocessedSource()
        {
            return string.Equals(displayedImageRelationSourceType, "Step", StringComparison.Ordinal) ||
                string.Equals(displayedImageRelationSourceType, "Group", StringComparison.Ordinal);
        }

        private static bool[,] CreateEdgeMask(Bitmap image, string method, Dictionary<string, string> parameters)
        {
            return CreateEdgeMask(CreateGrayValues(image), method, parameters);
        }

        private static bool[,] CreateEdgeMask(byte[,] gray, string method, Dictionary<string, string> parameters)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            using (var mask = OpenCvImageProcessingService.CreateBinaryMask(
                source,
                method,
                parameters,
                "Polarity Edge"))
            {
                return ConvertOpenCvBinaryMask(mask);
            }
        }

        private static byte[,] CreateGrayValues(Bitmap image)
        {
            return CreateGrayValues(image, new Rectangle(0, 0, image.Width, image.Height));
        }

        private static byte[,] CreateGrayValues(Bitmap image, Rectangle region)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            Rectangle bounds = Rectangle.Intersect(region, new Rectangle(0, 0, image.Width, image.Height));
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return new byte[0, 0];
            }

            PixelFormat format = image.PixelFormat;
            int bytesPerPixel = Image.GetPixelFormatSize(format) / 8;
            if (bytesPerPixel != 1 && bytesPerPixel != 3 && bytesPerPixel != 4)
            {
                using (var readable = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
                {
                    using (Graphics graphics = Graphics.FromImage(readable))
                    {
                        graphics.DrawImageUnscaled(image, 0, 0);
                    }

                    return CreateGrayValues(readable, bounds);
                }
            }

            var gray = new byte[bounds.Width, bounds.Height];
            BitmapData data = image.LockBits(bounds, ImageLockMode.ReadOnly, format);
            try
            {
                int stride = data.Stride;
                int absoluteStride = Math.Abs(stride);
                int rowBytes = checked(bounds.Width * bytesPerPixel);
                byte[] row = new byte[rowBytes];
                for (int y = 0; y < bounds.Height; y++)
                {
                    IntPtr rowPointer = stride >= 0
                        ? data.Scan0 + (y * stride)
                        : data.Scan0 + ((bounds.Height - 1 - y) * absoluteStride);
                    Marshal.Copy(rowPointer, row, 0, rowBytes);
                    for (int x = 0; x < bounds.Width; x++)
                    {
                        int offset = x * bytesPerPixel;
                        gray[x, y] = bytesPerPixel == 1
                            ? row[offset]
                            : (byte)((row[offset] + row[offset + 1] + row[offset + 2]) / 3);
                    }
                }
            }
            finally
            {
                image.UnlockBits(data);
            }

            return gray;
        }

        private static void PaintRedOverlayImage(Bitmap result, Rectangle roi, bool[,] mask)
        {
            for (int y = 0; y < roi.Height; y++)
            {
                for (int x = 0; x < roi.Width; x++)
                {
                    if (mask[x, y])
                    {
                        result.SetPixel(roi.X + x, roi.Y + y, Color.Red);
                    }
                }
            }

        }

        private static int GetIntParameter(Dictionary<string, string> parameters, string key, int defaultValue)
        {
            string value;
            int parsedValue;
            return parameters.TryGetValue(key, out value) && int.TryParse(value, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static int GetPolarityCoreWidth(Dictionary<string, string> parameters)
        {
            return GetIntParameter(
                parameters,
                "CoreWidth",
                GetIntParameter(parameters, "EdgeWidth", 3));
        }

        private static double GetDoubleParameter(Dictionary<string, string> parameters, string key, double defaultValue)
        {
            string value;
            double parsedValue;
            return parameters.TryGetValue(key, out value) &&
                double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static bool GetBoolParameter(Dictionary<string, string> parameters, string key, bool defaultValue)
        {
            string value;
            bool parsedValue;
            return parameters.TryGetValue(key, out value) && bool.TryParse(value, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static string GetStringParameter(Dictionary<string, string> parameters, string key, string defaultValue)
        {
            string value;
            return parameters.TryGetValue(key, out value) ? value : defaultValue;
        }

        private static int ClampInt(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private void BeginAddRoiSelection()
        {
            bool enabled = false;
            if (leftImageTabControl.SelectedTab == leftOriginalTabPage && leftOriginalDisplayControl.HasImage)
            {
                enabled = leftOriginalDisplayControl.BeginRoiSelection() || enabled;
            }

            if (rightImageTabControl.SelectedTab == rightOriginalTabPage && rightOriginalDisplayControl.HasImage)
            {
                enabled = rightOriginalDisplayControl.BeginRoiSelection() || enabled;
            }

            if (!enabled)
            {
                statusLabel.Text = "請先切到左邊或右邊的原圖，並載入圖片後再指定 ROI";
                MessageBox.Show(
                    this,
                    "請先載入圖片，並在左邊或右邊的「原圖」分頁指定 ROI。",
                    "指定 ROI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            statusLabel.Text = "請在目前顯示的原圖上拖曳矩形指定 ROI";
        }

        private void ImageDisplayControl_RoiSelected(object sender, RoiSelectedEventArgs e)
        {
            leftOriginalDisplayControl.CancelRoiSelection();
            rightOriginalDisplayControl.CancelRoiSelection();

            DialogResult result = MessageBox.Show(
                this,
                "是否要保留此 ROI？",
                "指定 ROI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                statusLabel.Text = "已取消 ROI";
                return;
            }

            rightImageTabControl.SelectedTab = rightOriginalTabPage;
            systemParameters.RoiRegions.Add(new RoiRegionSettings { Bounds = e.Roi });
            selectedRoiIndex = systemParameters.RoiRegions.Count - 1;
            SyncLegacyRoiFromSelectedRoi();
            SaveSystemParameters();
            RebuildVisibleRoiItems();
            functionListBox.SelectedItem = CreateRoiText(selectedRoiIndex + 1);
            ApplySelectedRoiOverlay();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = string.Format(
                "已新增 ROI {0}：X={1}, Y={2}, W={3}, H={4}",
                selectedRoiIndex + 1,
                e.Roi.X,
                e.Roi.Y,
                e.Roi.Width,
                e.Roi.Height);
        }

        private void ApplySelectedRoiOverlay()
        {
            Rectangle? roi = GetSelectedRoi();
            if (!roi.HasValue)
            {
                leftOriginalDisplayControl.ClearRoiOverlay();
                rightOriginalDisplayControl.ClearRoiOverlay();
                leftPreprocessedDisplayControl.ClearRoiOverlay();
                rightPreprocessedDisplayControl.ClearRoiOverlay();
                leftProcessedDisplayControl.ClearRoiOverlay();
                rightProcessedDisplayControl.ClearRoiOverlay();
                leftBlockProcessingDisplayControl.ClearRoiOverlay();
                rightBlockProcessingDisplayControl.ClearRoiOverlay();
                return;
            }

            leftOriginalDisplayControl.SetRoiOverlay(roi.Value);
            rightOriginalDisplayControl.SetRoiOverlay(roi.Value);
            leftPreprocessedDisplayControl.SetRoiOverlay(roi.Value);
            rightPreprocessedDisplayControl.SetRoiOverlay(roi.Value);
            leftProcessedDisplayControl.SetRoiOverlay(roi.Value);
            rightProcessedDisplayControl.SetRoiOverlay(roi.Value);
            leftBlockProcessingDisplayControl.SetRoiOverlay(roi.Value);
            rightBlockProcessingDisplayControl.SetRoiOverlay(roi.Value);
        }

        private Rectangle? GetSelectedRoi()
        {
            if (systemParameters.RoiRegions.Count == 0)
            {
                return null;
            }

            if (selectedRoiIndex < 0 || selectedRoiIndex >= systemParameters.RoiRegions.Count)
            {
                selectedRoiIndex = 0;
            }

            return systemParameters.RoiRegions[selectedRoiIndex].Bounds;
        }

        private void SyncLegacyRoiFromSelectedRoi()
        {
            Rectangle? roi = GetSelectedRoi();
            systemParameters.RoiEnabled = roi.HasValue;
            systemParameters.Roi = roi.HasValue ? roi.Value : Rectangle.Empty;
        }

        private async Task RestoreSystemParametersAsync()
        {
            if (!string.IsNullOrWhiteSpace(systemParameters.LastImagePath) && File.Exists(systemParameters.LastImagePath))
            {
                try
                {
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    rightImageTabControl.SelectedTab = rightOriginalTabPage;
                    await LoadImageIntoOriginalDisplaysAsync(systemParameters.LastImagePath, CancellationToken.None);
                    SyncVisibleImageDisplaysFromLeft();
                    statusLabel.Text = "已還原上次圖片：" + Path.GetFileName(systemParameters.LastImagePath);
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "還原上次圖片失敗";
                    Debug.WriteLine(ex);
                }
            }

            RestoreSavedRoiOverlay();
            // Loading restores only the image and ROI. Processing begins only
            // after the user explicitly selects a processing step or group.
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            processedImageDirty = false;
            QueueConfiguredRoiImagePreparation();
        }

        private void RestoreSavedRoiOverlay()
        {
            selectedRoiIndex = systemParameters.RoiRegions.Count > 0 ? 0 : -1;
            SyncLegacyRoiFromSelectedRoi();
            if (!systemParameters.RoiEnabled || systemParameters.Roi.Width <= 0 || systemParameters.Roi.Height <= 0)
            {
                return;
            }

            ApplySelectedRoiOverlay();
            statusLabel.Text = string.Format(
                "已還原 ROI 1：X={0}, Y={1}, W={2}, H={3}",
                systemParameters.Roi.X,
                systemParameters.Roi.Y,
                systemParameters.Roi.Width,
                systemParameters.Roi.Height);
        }

        private void QueueConfiguredRoiImagePreparation()
        {
            if (!rightOriginalDisplayControl.IsLargeImageMode)
            {
                return;
            }

            LargeImageSource source = rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source == null)
            {
                return;
            }

            Rectangle imageBounds = new Rectangle(0, 0, source.Width, source.Height);
            var configuredRois = systemParameters.RoiRegions
                .Select(region => Rectangle.Intersect(region.Bounds, imageBounds))
                .Where(roi => roi.Width > 0 && roi.Height > 0)
                .ToList();
            if (configuredRois.Count == 0)
            {
                source.ReleaseReference();
                return;
            }

            statusLabel.Text = "正在產生ROI的影像準備";
            Task.Run(
                delegate
                {
                    Stopwatch roiPreparationStopwatch = Stopwatch.StartNew();
                    try
                    {
                        // The first call creates the one shared OpenCV gray
                        // source. Every configured ROI afterwards is a cheap
                        // header-only view over the same native image.
                        foreach (Rectangle roi in configuredRois)
                        {
                            using (Cv.Mat ignored = GetOrCreateLargeRoiOpenCvGrayCache(source, roi))
                            {
                            }
                        }

                        long elapsedMilliseconds = roiPreparationStopwatch.ElapsedMilliseconds;
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    statusLabel.Text = "ROI 處理時間：" +
                                        elapsedMilliseconds.ToString(CultureInfo.InvariantCulture) +
                                        " ms（已準備 " +
                                        configuredRois.Count.ToString(CultureInfo.InvariantCulture) + " 個 ROI）";
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    finally
                    {
                        source.ReleaseReference();
                    }
                });
        }

        private async Task PrepareProcessedPreviewFromSettingsAsync()
        {
            int previewStepIndex = FindFirstPreviewableImageProcessingStepIndex();
            SyncLegacyRoiFromSelectedRoi();
            if (previewStepIndex < 0 || !systemParameters.RoiEnabled || !rightOriginalDisplayControl.HasImage)
            {
                return;
            }

            selectedImageProcessingStepIndex = previewStepIndex;
            MarkProcessedImageDirty();
            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                statusLabel.Text = "影像處理運算中...";
                PrepareLargeProcessedPreview();
                processedImageDirty = false;
                statusLabel.Text = "已準備大圖處理後影像：處理" + (previewStepIndex + 1);
                return;
            }

            statusLabel.Text = "影像處理運算中...";
            Bitmap processedImage = await Task.Run(() => CreateCurrentProcessedImage());
            if (processedImage == null)
            {
                return;
            }

            latestProcessedImage = processedImage;
            latestProcessedImageCacheKey = CreateProcessedImageCacheKey();
            processedImageDirty = false;
            isSyncingImageView = true;
            try
            {
                leftProcessedDisplayControl.SetDisplayImage(new Bitmap(latestProcessedImage), true);
                leftProcessedDisplayControl.SetRoiOverlay(systemParameters.Roi);
                rightProcessedDisplayControl.SetDisplayImage(new Bitmap(latestProcessedImage), true);
                rightProcessedDisplayControl.SetRoiOverlay(systemParameters.Roi);
            }
            finally
            {
                isSyncingImageView = false;
            }

            ApplySharedImageViewStateToVisibleControls();
            statusLabel.Text = "已準備處理後影像：處理" + (previewStepIndex + 1);
        }

        private int FindFirstPreviewableImageProcessingStepIndex()
        {
            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                if (IsBinaryMaskProcessingMethod(systemParameters.ImageProcessingSteps[index].Method))
                {
                    return index;
                }
            }

            return -1;
        }

        private void SaveSystemParameters()
        {
            systemParameterService.Save(systemParameters);
        }

        private async Task OpenImageAsync()
        {
            if (isLoadingImage)
            {
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "讀取圖片";
                dialog.Filter = "圖片檔案|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有檔案|*.*";
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                isLoadingImage = true;
                string fileName = Path.GetFileName(dialog.FileName);
                Size previousImageSize = leftOriginalDisplayControl.ImageSize;

                try
                {
                    statusLabel.Text = "讀取圖片中：" + fileName;
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    rightImageTabControl.SelectedTab = rightOriginalTabPage;
                    systemParameters.LastImagePath = dialog.FileName;
                    selectedImageProcessingStepIndex = -1;
                    selectedImageProcessingGroupId = null;
                    RebuildVisibleRoiItems();
                    MarkPreprocessedImageDirty();

                    await LoadImageIntoOriginalDisplaysAsync(dialog.FileName, CancellationToken.None);
                    SyncVisibleImageDisplaysFromLeft();
                    SaveSystemParameters();

                    Size loadedImageSize = leftOriginalDisplayControl.ImageSize;
                    statusLabel.Text = "已讀取圖片：" + fileName;
                    if (!previousImageSize.IsEmpty &&
                        !loadedImageSize.IsEmpty &&
                        previousImageSize != loadedImageSize)
                    {
                        statusLabel.Text += string.Format(
                            "；ROI 設定已保留，影像尺寸由 {0}x{1} 變更為 {2}x{3}，請確認 ROI 座標。",
                            previousImageSize.Width,
                            previousImageSize.Height,
                            loadedImageSize.Width,
                            loadedImageSize.Height);
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "讀取圖片失敗";
                    MessageBox.Show(
                        this,
                        "讀取圖片失敗：" + ex.Message,
                        "讀取圖片",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    isLoadingImage = false;
                }
            }
        }

        private async Task LoadImageIntoOriginalDisplaysAsync(string filePath, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref imageSourceGeneration);
            ClearObjectDetectionDefectCoreResults();
            ClearObjectDetectionFlatFieldMaskOverlays();
            ClearObjectDetectionFlatFieldCorrectionPreview();
            Size imageSize = await Task.Run(() => LargeImageSource.ReadImageSize(filePath), cancellationToken);
            // A processing Mat belongs to one source file only.  Keep the
            // display controls tile-backed, but release the prior source Mat
            // before a newly selected image becomes active.
            ClearLargeRoiGrayCache();
            long sourcePixels = (long)imageSize.Width * imageSize.Height;
            if (sourcePixels > 50000000L)
            {
                var sharedSource = await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return new LargeImageSource(filePath);
                    },
                    cancellationToken);
                try
                {
                    leftOriginalDisplayControl.SetSharedLargeImageSource(sharedSource);
                    rightOriginalDisplayControl.SetSharedLargeImageSource(sharedSource);
                    leftPreprocessedDisplayControl.SetSharedLargeImageSource(sharedSource);
                    rightPreprocessedDisplayControl.SetSharedLargeImageSource(sharedSource);
                    leftBlockProcessingDisplayControl.SetSharedLargeImageSource(sharedSource);
                    rightBlockProcessingDisplayControl.SetSharedLargeImageSource(sharedSource);
                    leftObjectsDisplayControl.SetSharedLargeImageSource(sharedSource);
                    rightObjectsDisplayControl.SetSharedLargeImageSource(sharedSource);
                    statusLabel.Text = "已載入大圖共用切圖來源";

                    // Warm the shared full-resolution grayscale OpenCV source
                    // while the image is being loaded. Later preprocessing and
                    // edge processing reuse this Mat instead of reading the
                    // image file again on their first execution.
                    LargeImageSource warmSource = sharedSource.AddReference();
                    int warmGeneration = imageSourceGeneration;
                    _ = Task.Run(
                        delegate
                        {
                            try
                            {
                                if (warmGeneration == imageSourceGeneration)
                                {
                                    using (Cv.Mat ignored = GetOrCreateLargeRoiOpenCvGrayCache(
                                        warmSource,
                                        new Rectangle(0, 0, warmSource.Width, warmSource.Height)))
                                    {
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("OpenCV source warm-up failed: " + ex);
                            }
                            finally
                            {
                                warmSource.ReleaseReference();
                            }
                        });
                }
                finally
                {
                    sharedSource.ReleaseReference();
                }

                RefreshObjectDetectionFlatFieldDisplay();
                RequestPreprocessedImageUpdate();

                return;
            }

            Bitmap loadedBitmap = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var image = Image.FromStream(stream, false, false))
                    {
                        return new Bitmap(image);
                    }
                },
                cancellationToken);
            try
            {
                leftOriginalDisplayControl.SetDisplayImage(loadedBitmap, false);
                rightOriginalDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
                leftPreprocessedDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
                rightPreprocessedDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
                leftBlockProcessingDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
                rightBlockProcessingDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
                leftObjectsDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
                rightObjectsDisplayControl.SetDisplayImage(new Bitmap(loadedBitmap), false);
            }
            finally
            {
                // SetDisplayImage takes ownership of the bitmap passed to it.
                // The first bitmap is owned by the left control after the call.
                loadedBitmap = null;
            }

            RefreshObjectDetectionFlatFieldDisplay();
            RequestPreprocessedImageUpdate();
        }

        private void FunctionListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color backColor = selected ? Color.FromArgb(224, 229, 236) : functionListBox.BackColor;
            Color foreColor = Color.FromArgb(39, 46, 56);

            using (Brush backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            string menuText = functionListBox.Items[e.Index].ToString();
            FunctionMenuIcon icon = GetFunctionMenuIcon(menuText);
            bool isFindObjectFlowChild = IsFindObjectFlowMenuItem(menuText);
            if (icon != FunctionMenuIcon.None)
            {
                Rectangle iconBounds = new Rectangle(
                    e.Bounds.Left + (isFindObjectFlowChild ? 26 : 10),
                    e.Bounds.Top + 7,
                    16,
                    16);
                DrawFunctionMenuIcon(e.Graphics, icon, iconBounds, selected);
            }

            int textLeft = e.Bounds.Left + (icon == FunctionMenuIcon.None
                ? GetFunctionMenuIndent(menuText)
                : isFindObjectFlowChild ? 52 : 36);
            Rectangle textBounds = new Rectangle(
                textLeft,
                e.Bounds.Top,
                Math.Max(0, e.Bounds.Right - textLeft - 4),
                e.Bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                menuText.Trim(),
                e.Font,
                textBounds,
                foreColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private static FunctionMenuIcon GetFunctionMenuIcon(string menuText)
        {
            switch (menuText)
            {
                case FindObjectFlowMenuText:
                    return FunctionMenuIcon.Flow;
                case DetectionParameterLoadMenuText:
                    return FunctionMenuIcon.Measure;
                case LoadImageMenuText:
                    return FunctionMenuIcon.Folder;
                case RoiMenuText:
                    return FunctionMenuIcon.Roi;
                case ImagePreprocessingMenuText:
                    return FunctionMenuIcon.Filter;
                case ImageProcessingMenuText:
                    return FunctionMenuIcon.Process;
                case ImageRelationMenuText:
                    return FunctionMenuIcon.Relation;
                case ObjectJudgementMenuText:
                    return FunctionMenuIcon.Object;
                case ObjectDefinitionMenuText:
                    return FunctionMenuIcon.Object;
                case ObjectDetectionParameterMenuText:
                    return FunctionMenuIcon.Measure;
                case ObjectDetectionResultReviewMenuText:
                    return FunctionMenuIcon.Measure;
                case "亮度 / 對比":
                    return FunctionMenuIcon.Brightness;
                case "濾波與銳化":
                    return FunctionMenuIcon.Filter;
                case "邊緣偵測":
                    return FunctionMenuIcon.Edge;
                case "幾何校正":
                    return FunctionMenuIcon.Geometry;
                case "量測工具":
                    return FunctionMenuIcon.Measure;
                case "輸出設定":
                    return FunctionMenuIcon.Save;
                default:
                    return FunctionMenuIcon.None;
            }
        }

        private static bool IsFindObjectFlowMenuItem(string menuText)
        {
            return menuText == ImagePreprocessingMenuText ||
                menuText == ImageProcessingMenuText ||
                menuText == ImageRelationMenuText ||
                menuText == ObjectJudgementMenuText ||
                menuText == ObjectDefinitionMenuText;
        }

        private static void DrawFunctionMenuIcon(Graphics graphics, FunctionMenuIcon icon, Rectangle bounds, bool selected)
        {
            Color color = selected ? Color.FromArgb(46, 105, 156) : Color.FromArgb(75, 103, 132);
            using (var pen = new Pen(color, 1.6f))
            using (var brush = new SolidBrush(Color.FromArgb(38, color)))
            {
                switch (icon)
                {
                    case FunctionMenuIcon.Folder:
                        graphics.FillRectangle(brush, bounds.Left + 1, bounds.Top + 5, 14, 9);
                        graphics.DrawRectangle(pen, bounds.Left + 1, bounds.Top + 5, 14, 9);
                        graphics.DrawLine(pen, bounds.Left + 2, bounds.Top + 5, bounds.Left + 6, bounds.Top + 5);
                        graphics.DrawLine(pen, bounds.Left + 3, bounds.Top + 3, bounds.Left + 7, bounds.Top + 3);
                        break;
                    case FunctionMenuIcon.Flow:
                        graphics.DrawLine(pen, bounds.Left + 3, bounds.Top + 4, bounds.Left + 12, bounds.Top + 4);
                        graphics.DrawLine(pen, bounds.Left + 3, bounds.Top + 4, bounds.Left + 3, bounds.Top + 12);
                        graphics.DrawLine(pen, bounds.Left + 3, bounds.Top + 12, bounds.Left + 12, bounds.Top + 12);
                        graphics.DrawLine(pen, bounds.Left + 12, bounds.Top + 4, bounds.Left + 12, bounds.Top + 12);
                        graphics.DrawLine(pen, bounds.Left + 5, bounds.Top + 8, bounds.Left + 10, bounds.Top + 8);
                        break;
                    case FunctionMenuIcon.Roi:
                        graphics.DrawRectangle(pen, bounds.Left + 3, bounds.Top + 3, 10, 10);
                        graphics.DrawLine(pen, bounds.Left, bounds.Top + 5, bounds.Left + 3, bounds.Top + 5);
                        graphics.DrawLine(pen, bounds.Left + 11, bounds.Top + 5, bounds.Right, bounds.Top + 5);
                        graphics.DrawLine(pen, bounds.Left + 5, bounds.Top, bounds.Left + 5, bounds.Top + 3);
                        graphics.DrawLine(pen, bounds.Left + 5, bounds.Top + 13, bounds.Left + 5, bounds.Bottom);
                        break;
                    case FunctionMenuIcon.Process:
                        graphics.DrawRectangle(pen, bounds.Left + 2, bounds.Top + 2, 8, 8);
                        graphics.DrawRectangle(pen, bounds.Left + 6, bounds.Top + 6, 8, 8);
                        graphics.DrawLine(pen, bounds.Left + 1, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
                        break;
                    case FunctionMenuIcon.Relation:
                        graphics.DrawEllipse(pen, bounds.Left + 1, bounds.Top + 4, 9, 8);
                        graphics.DrawEllipse(pen, bounds.Left + 6, bounds.Top + 4, 9, 8);
                        graphics.DrawLine(pen, bounds.Left + 6, bounds.Top + 6, bounds.Left + 10, bounds.Top + 10);
                        graphics.DrawLine(pen, bounds.Left + 6, bounds.Top + 10, bounds.Left + 10, bounds.Top + 6);
                        break;
                    case FunctionMenuIcon.Object:
                        graphics.DrawEllipse(pen, bounds.Left + 2, bounds.Top + 2, 12, 12);
                        graphics.DrawEllipse(pen, bounds.Left + 6, bounds.Top + 6, 4, 4);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Top, bounds.Left + 8, bounds.Top + 2);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 2, bounds.Left + 8, bounds.Bottom);
                        break;
                    case FunctionMenuIcon.Brightness:
                        graphics.DrawEllipse(pen, bounds.Left + 5, bounds.Top + 5, 6, 6);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Top, bounds.Left + 8, bounds.Top + 3);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 3, bounds.Left + 8, bounds.Bottom);
                        graphics.DrawLine(pen, bounds.Left, bounds.Top + 8, bounds.Left + 3, bounds.Top + 8);
                        graphics.DrawLine(pen, bounds.Right - 3, bounds.Top + 8, bounds.Right, bounds.Top + 8);
                        break;
                    case FunctionMenuIcon.Filter:
                        graphics.DrawLine(pen, bounds.Left + 1, bounds.Top + 2, bounds.Right - 1, bounds.Top + 2);
                        graphics.DrawLine(pen, bounds.Left + 1, bounds.Top + 2, bounds.Left + 6, bounds.Top + 8);
                        graphics.DrawLine(pen, bounds.Right - 1, bounds.Top + 2, bounds.Left + 10, bounds.Top + 8);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Top + 8, bounds.Left + 8, bounds.Bottom - 1);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 1, bounds.Left + 11, bounds.Bottom - 3);
                        break;
                    case FunctionMenuIcon.Edge:
                        graphics.DrawLine(pen, bounds.Left + 2, bounds.Bottom - 2, bounds.Left + 7, bounds.Top + 2);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 2, bounds.Left + 14, bounds.Top + 2);
                        break;
                    case FunctionMenuIcon.Geometry:
                        graphics.DrawPolygon(pen, new[]
                        {
                            new Point(bounds.Left + 4, bounds.Top + 1), new Point(bounds.Right - 2, bounds.Top + 5),
                            new Point(bounds.Right - 5, bounds.Bottom - 1), new Point(bounds.Left + 1, bounds.Bottom - 5)
                        });
                        break;
                    case FunctionMenuIcon.Measure:
                        graphics.DrawLine(pen, bounds.Left + 1, bounds.Bottom - 3, bounds.Right - 1, bounds.Top + 3);
                        graphics.DrawLine(pen, bounds.Left + 4, bounds.Bottom - 5, bounds.Left + 6, bounds.Bottom - 2);
                        graphics.DrawLine(pen, bounds.Left + 8, bounds.Top + 5, bounds.Left + 10, bounds.Top + 8);
                        break;
                    case FunctionMenuIcon.Save:
                        graphics.DrawRectangle(pen, bounds.Left + 2, bounds.Top + 1, 12, 14);
                        graphics.DrawRectangle(pen, bounds.Left + 5, bounds.Top + 9, 6, 5);
                        graphics.DrawLine(pen, bounds.Left + 5, bounds.Top + 2, bounds.Left + 11, bounds.Top + 2);
                        break;
                }
            }
        }

        private static int GetFunctionMenuIndent(string menuText)
        {
            if (!string.IsNullOrEmpty(menuText))
            {
                int leadingSpaces = menuText.Length - menuText.TrimStart().Length;
                if (leadingSpaces > 0)
                {
                    return 12 + (leadingSpaces * 9);
                }
            }

            if (IsImageProcessingStepCommandMenuItem(menuText))
            {
                return 66;
            }

            if (IsImageProcessingStepMenuItem(menuText) || IsRoiMenuItem(menuText))
            {
                return 48;
            }

            return 12;
        }

        private static bool IsImageProcessingStepCommandMenuItem(string menuText)
        {
            return menuText == DeleteImageProcessingStepMenuText ||
                menuText == MoveUpImageProcessingStepMenuText ||
                menuText == MoveDownImageProcessingStepMenuText;
        }

        private static bool IsImageProcessingStepMenuItem(string menuText)
        {
            if (string.IsNullOrEmpty(menuText))
            {
                return false;
            }

            string trimmedText = menuText.Trim();
            int prefixLength = "處理".Length;
            int suffixStartIndex = trimmedText.IndexOf('(');
            int suffixEndIndex = trimmedText.IndexOf(')', suffixStartIndex + 1);
            return trimmedText.StartsWith("處理", StringComparison.Ordinal) &&
                suffixStartIndex > prefixLength &&
                suffixEndIndex > suffixStartIndex;
        }
    }
}
