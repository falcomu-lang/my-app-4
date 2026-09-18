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
        private bool isLoadingImage;
        private bool isSyncingImageView;
        private DateTime lastSynchronousImagePanRefreshUtc = DateTime.MinValue;
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
        private readonly object largeProcessedMaskLock = new object();
        private readonly Dictionary<string, bool[,]> largeProcessedMasks = new Dictionary<string, bool[,]>(StringComparer.Ordinal);
        // Raw Canny output stays in OpenCV memory.  For large ROIs this avoids
        // making a second full-image managed mask just to paint red overlays.
        private readonly Dictionary<string, Cv.Mat> largeProcessedBinaryMasks = new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
        private readonly HashSet<string> largeProcessedMaskBuildKeys = new HashSet<string>(StringComparer.Ordinal);
        // One full-size OpenCV edge operation already uses several source-size
        // buffers. Serializing it prevents parameter changes from starting a
        // second competing Sobel/Canny operation before the first one exits.
        private readonly SemaphoreSlim largeNativeProcessingGate = new SemaphoreSlim(1, 1);
        private readonly object largeRoiGrayCacheLock = new object();
        private LargeImageSource largeRoiGrayCacheSource;
        private Rectangle largeRoiGrayCacheRoi;
        private byte[,] largeRoiGrayCache;
        private Cv.Mat largeRoiOpenCvGrayCache;
        // This is deliberately separate from LargeImageSource's WIC tiles.
        // Tiles own responsive rendering; this Mat owns fast ROI processing.
        private LargeImageSource largeOpenCvSourceGrayCacheSource;
        private Cv.Mat largeOpenCvSourceGrayCache;
        // The preprocessing output remains a full-resolution OpenCV Mat and
        // is exposed to the viewers through a memory-backed tile source.
        private int imageSourceGeneration;
        private int largeProcessedMaskGeneration;
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
        private bool imageProcessingExecutionRequested;
        private bool hasSharedImageViewState;
        private ImageViewState sharedImageViewState;
        private bool isImageViewerMaximized;
        private bool isLeftImageViewerMaximized;
        private bool hasMaximizedImageViewerViewState;
        private ImageViewState maximizedImageViewerViewState;
        private bool hasProcessedImageViewState;
        private ImageViewState processedImageViewState;

        private const string LoadImageMenuText = "讀取圖片";
        private const string RoiMenuText = "指定 ROI";
        private const string ImageProcessingMenuText = "影像處理";
        private const string ObjectJudgementMenuText = "整合成區塊";
        private const string ObjectDefinitionMenuText = "物件定義";
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

            InitializeComponent();
            NormalizeObjectJudgementDefaultNames();
            NormalizeObjectDefinitionDefaultNames();
            functionListBox.SelectionMode = SelectionMode.MultiExtended;
            functionListBox.MouseUp += FunctionListBox_MouseUp;
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
            RebuildVisibleImageRelations();
            RebuildVisibleObjectJudgements();
            RebuildVisibleObjectDefinitions();

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
            leftProcessedDisplayControl.LargeImageOverlayPaint += ProcessedDisplayControl_LargeImageOverlayPaint;
            leftBlockProcessingDisplayControl.LargeImageOverlayPaint += BlockProcessingDisplayControl_LargeImageOverlayPaint;
            leftObjectsDisplayControl.LargeImageOverlayPaint += ObjectDefinitionDisplayControl_LargeImageOverlayPaint;
            rightProcessedDisplayControl.LargeImageOverlayPaint += ProcessedDisplayControl_LargeImageOverlayPaint;
            rightBlockProcessingDisplayControl.LargeImageOverlayPaint += BlockProcessingDisplayControl_LargeImageOverlayPaint;
            rightObjectsDisplayControl.LargeImageOverlayPaint += ObjectDefinitionDisplayControl_LargeImageOverlayPaint;
            imageProcessingDebounceTimer = new System.Windows.Forms.Timer();
            imageProcessingDebounceTimer.Interval = 200;
            imageProcessingDebounceTimer.Tick += ImageProcessingDebounceTimer_Tick;
        }

        private void ImageDisplayControl_ViewChanged(object sender, EventArgs e)
        {
            if (isSyncingImageView)
            {
                return;
            }

            var source = sender as ImageDisplayControl;
            if (source == null || !source.HasImage)
            {
                return;
            }

            if (isImageViewerMaximized)
            {
                ImageDisplayControl active = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (ReferenceEquals(source, active))
                {
                    maximizedImageViewerViewState = source.ViewState;
                    hasMaximizedImageViewerViewState = true;
                }

                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            ImageDisplayControl target = null;

            if (ReferenceEquals(source, leftVisible))
            {
                target = rightVisible;
            }
            else if (ReferenceEquals(source, rightVisible))
            {
                target = leftVisible;
            }

            if (target == null || !target.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                ImageViewState viewState = source.ViewState;
                bool isPanning = source.IsPanning;
                sharedImageViewState = viewState;
                hasSharedImageViewState = true;
                target.ApplyViewState(viewState, isPanning);

                if (isPanning)
                {
                    DateTime now = DateTime.UtcNow;
                    if ((now - lastSynchronousImagePanRefreshUtc).TotalMilliseconds >= 16)
                    {
                        lastSynchronousImagePanRefreshUtc = now;
                        source.RefreshImageViewNow();
                        target.RefreshImageViewNow();
                    }
                }
                else
                {
                    lastSynchronousImagePanRefreshUtc = DateTime.MinValue;
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void VisibleImageTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            RequestPreprocessedImageUpdate();
            UpdateVisibleProcessedImageIfNeeded();
            if (ReferenceEquals((sender as TabControl)?.SelectedTab, leftBlockProcessingTabPage) ||
                ReferenceEquals((sender as TabControl)?.SelectedTab, rightBlockProcessingTabPage))
            {
                InvalidateBlockProcessingDisplays();
            }
            UpdateObjectDefinitionDisplayTimingIfNeeded();
            if (isImageViewerMaximized)
            {
                ApplyMaximizedSideViewState(sender as TabControl);
                return;
            }

            BeginInvoke(new Action(ApplySharedImageViewStateToVisibleControls));
        }

        private void ApplyMaximizedSideViewState(TabControl changedTabControl)
        {
            bool changedMaximizedSide = (isLeftImageViewerMaximized && ReferenceEquals(changedTabControl, leftImageTabControl)) ||
                (!isLeftImageViewerMaximized && ReferenceEquals(changedTabControl, rightImageTabControl));
            if (!changedMaximizedSide || !hasMaximizedImageViewerViewState)
            {
                return;
            }

            ImageDisplayControl active = isLeftImageViewerMaximized
                ? GetVisibleLeftImageDisplayControl()
                : GetVisibleRightImageDisplayControl();
            if (active == null || !active.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                active.ApplyViewState(maximizedImageViewerViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ImageTabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null || !IsImageTabHeader(tabControl, e.Location))
            {
                return;
            }

            bool maximizeLeft = ReferenceEquals(tabControl, leftImageTabControl);
            if (isImageViewerMaximized && isLeftImageViewerMaximized != maximizeLeft)
            {
                return;
            }

            SetImageViewerMaximized(!isImageViewerMaximized, maximizeLeft);
        }

        private static bool IsImageTabHeader(TabControl tabControl, Point location)
        {
            for (int index = 0; index < tabControl.TabCount; index++)
            {
                if (tabControl.GetTabRect(index).Contains(location))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetImageViewerMaximized(bool maximize, bool maximizeLeft)
        {
            bool wasLeftMaximized = isLeftImageViewerMaximized;
            if (maximize && isImageViewerMaximized && wasLeftMaximized == maximizeLeft)
            {
                return;
            }

            imageLayoutPanel.SuspendLayout();
            try
            {
                isImageViewerMaximized = maximize;
                isLeftImageViewerMaximized = maximize && maximizeLeft;
                if (maximize)
                {
                    leftImageTabControl.Visible = maximizeLeft;
                    rightImageTabControl.Visible = !maximizeLeft;
                    imageLayoutPanel.SetColumnSpan(maximizeLeft ? leftImageTabControl : rightImageTabControl, 2);
                    ImageDisplayControl active = maximizeLeft
                        ? GetVisibleLeftImageDisplayControl()
                        : GetVisibleRightImageDisplayControl();
                    if (active != null && active.HasImage)
                    {
                        maximizedImageViewerViewState = active.ViewState;
                        hasMaximizedImageViewerViewState = true;
                    }
                    else
                    {
                        hasMaximizedImageViewerViewState = false;
                    }
                }
                else
                {
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                    imageLayoutPanel.SetColumnSpan(rightImageTabControl, 1);
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = true;
                    hasMaximizedImageViewerViewState = false;
                }
            }
            finally
            {
                imageLayoutPanel.ResumeLayout(true);
            }

            if (maximize)
            {
                ImageDisplayControl active = maximizeLeft
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (active != null)
                {
                    active.InvalidateImageView();
                }

                statusLabel.Text = maximizeLeft ? "左側影像已放大顯示" : "右側影像已放大顯示";
                return;
            }

            ImageDisplayControl restoreSource = wasLeftMaximized
                ? GetVisibleLeftImageDisplayControl()
                : GetVisibleRightImageDisplayControl();
            SynchronizeImageViewFrom(restoreSource);
            statusLabel.Text = "已還原雙側影像顯示";
        }

        private void SynchronizeImageViewFrom(ImageDisplayControl source)
        {
            if (source == null || !source.HasImage)
            {
                return;
            }

            ImageDisplayControl target = ReferenceEquals(source, GetVisibleLeftImageDisplayControl())
                ? GetVisibleRightImageDisplayControl()
                : GetVisibleLeftImageDisplayControl();
            if (target == null || !target.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                sharedImageViewState = source.ViewState;
                hasSharedImageViewState = true;
                target.ApplyViewState(sharedImageViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ImageDisplayControl_FitViewRequested(object sender, EventArgs e)
        {
            if (isSyncingImageView)
            {
                return;
            }

            var source = sender as ImageDisplayControl;
            if (source == null || !source.HasImage)
            {
                return;
            }

            if (isImageViewerMaximized)
            {
                ImageDisplayControl active = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (ReferenceEquals(source, active))
                {
                    maximizedImageViewerViewState = source.ViewState;
                    hasMaximizedImageViewerViewState = true;
                }

                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            ImageDisplayControl target = null;

            if (ReferenceEquals(source, leftVisible))
            {
                target = rightVisible;
            }
            else if (ReferenceEquals(source, rightVisible))
            {
                target = leftVisible;
            }

            isSyncingImageView = true;
            try
            {
                if (target != null && target.HasImage)
                {
                    target.ResetViewToFit(false);
                }

                sharedImageViewState = source.ViewState;
                hasSharedImageViewState = true;
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void SyncVisibleImageDisplaysFromLeft()
        {
            if (isSyncingImageView || isImageViewerMaximized)
            {
                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            if (leftVisible == null || rightVisible == null || !leftVisible.HasImage || !rightVisible.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                sharedImageViewState = leftVisible.ViewState;
                hasSharedImageViewState = true;
                rightVisible.ApplyViewState(leftVisible.ViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ApplySharedImageViewStateToVisibleControls()
        {
            if (isSyncingImageView || isImageViewerMaximized)
            {
                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            if (leftVisible == null || rightVisible == null)
            {
                return;
            }

            if (!hasSharedImageViewState)
            {
                if (leftVisible.HasImage)
                {
                    sharedImageViewState = leftVisible.ViewState;
                    hasSharedImageViewState = true;
                }
                else if (rightVisible.HasImage)
                {
                    sharedImageViewState = rightVisible.ViewState;
                    hasSharedImageViewState = true;
                }
            }

            if (!hasSharedImageViewState)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                if (leftVisible.HasImage)
                {
                    leftVisible.ApplyViewState(sharedImageViewState);
                }

                if (rightVisible.HasImage)
                {
                    rightVisible.ApplyViewState(sharedImageViewState);
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private ImageDisplayControl GetVisibleLeftImageDisplayControl()
        {
            if (leftImageTabControl.SelectedTab == leftOriginalTabPage)
            {
                return leftOriginalDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftPreprocessedTabPage)
            {
                return leftPreprocessedDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
            {
                return leftProcessedDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftBlockProcessingTabPage)
            {
                return leftBlockProcessingDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftObjectsTabPage)
            {
                return leftObjectsDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftDebugTabPage)
            {
                return leftDebugDisplayControl;
            }

            return null;
        }

        private ImageDisplayControl GetVisibleRightImageDisplayControl()
        {
            if (rightImageTabControl.SelectedTab == rightOriginalTabPage)
            {
                return rightOriginalDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightPreprocessedTabPage)
            {
                return rightPreprocessedDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
            {
                return rightProcessedDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightBlockProcessingTabPage)
            {
                return rightBlockProcessingDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightObjectsTabPage)
            {
                return rightObjectsDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightDebugTabPage)
            {
                return rightDebugDisplayControl;
            }

            return null;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Maximized;
            functionListBox.SelectedIndex = 0;
            statusLabel.Text = "介面框架準備就緒";
            BeginInvoke(new Action(async () => await RestoreSystemParametersAsync()));
        }

        private void FunctionListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingFunctionListText ||
                isRebuildingImagePreprocessingMenu ||
                isRebuildingObjectJudgementMenu ||
                isRebuildingObjectDefinitionMenu)
            {
                return;
            }

            string selectedFunction = functionListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedFunction))
            {
                ResetRightFunctionPanel();
                rightPanelTitleLabel.Text = "參數設定";
                return;
            }

            ResetRightFunctionPanel();
            rightPanelTitleLabel.Text = selectedFunction + " 參數";
            if (selectedFunction == LoadImageMenuText)
            {
                parameterPlaceholderLabel.Text = "點選左側「讀取圖片」後，選擇要載入的圖片。";
            }
            else if (selectedFunction == RoiMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "展開「指定 ROI」後可新增 ROI。";
            }
            else if (IsRoiMenuItem(selectedFunction))
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = selectedFunction.Trim() + " 是目前選用的 ROI。影像處理會套用這個 ROI。";
            }
            else if (selectedFunction == ImageProcessingMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "展開「影像處理」後，可新增影像處理流程。";
            }
            else if (selectedFunction == ImageRelationMenuText)
            {
                parameterPlaceholderLabel.Text = "右鍵選擇「新增關聯」，設定來源影像與影像處理項目。";
            }
            else if (selectedFunction == ObjectJudgementMenuText)
            {
                parameterPlaceholderLabel.Text = "這裡會顯示整合成區塊的設定與結果。";
            }
            else if (selectedFunction == ObjectDefinitionMenuText)
            {
                parameterPlaceholderLabel.Text = "右鍵選擇「新增物件組」，建立物件組項目。";
            }
            else if (GetObjectJudgementGroupId(selectedFunction) != null)
            {
                parameterPlaceholderLabel.Text = "目前選擇物件群組，可展開或收合其中的區塊。";
                if (IsAKeyDown())
                {
                    ProcessObjectJudgementGroup(GetObjectJudgementGroupId(selectedFunction));
                    aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                }
            }
            else
            {
                string objectDefinitionId;
                int objectDefinitionProcessingIndex;
                if (TryGetObjectDefinitionProcessingLocation(
                        functionListBox.SelectedIndex,
                        out objectDefinitionId,
                        out objectDefinitionProcessingIndex))
                {
                    ShowObjectDefinitionProcessingParameterPanel(
                        objectDefinitionId,
                        objectDefinitionProcessingIndex);
                    if (IsAKeyDown())
                    {
                        ProcessObjectDefinitionProcessing(
                            objectDefinitionId,
                            GetObjectDefinitionProcessingId(functionListBox.SelectedIndex));
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                    return;
                }

                objectDefinitionId = GetObjectDefinitionId(
                    functionListBox.SelectedIndex,
                    selectedFunction);
                if (!string.IsNullOrEmpty(objectDefinitionId))
                {
                    ShowObjectDefinitionParameterPanel(objectDefinitionId);
                    if (IsAKeyDown())
                    {
                        ProcessObjectDefinition(objectDefinitionId);
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                    return;
                }

                int objectIndex;
                int processingIndex;
                if (TryGetObjectJudgementProcessingLocation(
                        functionListBox.SelectedIndex,
                        selectedFunction,
                        out objectIndex,
                        out processingIndex))
                {
                    ShowObjectJudgementProcessingParameterPanel(objectIndex, processingIndex);
                    if (IsAKeyDown())
                    {
                        ProcessObjectJudgementProcessing(objectIndex, processingIndex);
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                }
                else if (GetObjectJudgementIndex(selectedFunction) >= 0)
                {
                    int selectedObjectIndex = GetObjectJudgementIndex(selectedFunction);
                    ShowObjectJudgementParameterPanel(selectedObjectIndex);
                    if (IsAKeyDown())
                    {
                        ProcessObjectJudgement(selectedObjectIndex);
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                }
                else if (GetObjectDefinitionIndex(selectedFunction) >= 0)
                {
                    ShowObjectDefinitionParameterPanel(
                        GetObjectDefinitionId(functionListBox.SelectedIndex, selectedFunction));
                }
                else if (GetImageRelationGroupId(selectedFunction) != null)
                {
                    string imageRelationGroupId = GetImageRelationGroupId(selectedFunction);
                    ShowImageRelationGroup(selectedFunction);
                    if (IsAKeyDown())
                    {
                        ProcessImageRelationGroup(imageRelationGroupId);
                    }
                }
                else if (GetImageRelationIndex(selectedFunction) >= 0)
                {
                    activeImageRelationGroupId = null;
                    selectedImageRelationGroupId = null;
                    activeImageRelationSourceType = "Original";
                    activeImageRelationSourceId = null;
                    selectedImageRelationIndex = GetImageRelationIndex(selectedFunction);
                    ShowImageRelationParameterPanel(selectedImageRelationIndex);
                    if (IsAKeyDown())
                    {
                        ProcessImageRelation(selectedImageRelationIndex);
                    }
                }
                else if (selectedFunction == ImagePreprocessingMenuText)
                {
                    HideImageProcessingFlowTree();
                    parameterPlaceholderLabel.Text = "右鍵「影像前處理」可新增前處理流程。";
                }
                else if (string.Equals(selectedFunction, OriginalPreprocessingSourceText, StringComparison.Ordinal))
                {
                    activeImageRelationGroupId = null;
                    activeImageRelationSourceType = "Original";
                    activeImageRelationSourceId = null;
                    parameterPlaceholderLabel.Text = "目前影像來源：原始影像。直接執行影像處理時會使用原圖。";
                }
                else if (IsImagePreprocessingStepMenuItem(selectedFunction))
                {
                    ShowImagePreprocessingFlowTree(selectedFunction);
                }
                else if (GetImagePreprocessingGroupId(selectedFunction) != null)
                {
                    ShowImagePreprocessingGroup(selectedFunction);
                }
                else if (GetImageProcessingGroupId(selectedFunction) != null)
                {
                    ShowImageProcessingGroup(selectedFunction);
                }
                else if (IsImageProcessingStepMenuItem(selectedFunction))
                {
                    ShowImageProcessingFlowTree(selectedFunction);
                }
                else if (IsImageProcessingStepCommandMenuItem(selectedFunction))
                {
                    HideImageProcessingFlowTree();
                    parameterPlaceholderLabel.Text = "這裡會放置影像處理步驟的編輯命令。";
                }
                else
                {
                    HideImageProcessingFlowTree();
                    parameterPlaceholderLabel.Text = "這裡會顯示「" + selectedFunction + "」的參數設定與選項。";
                }
            }

            statusLabel.Text = "目前選擇：" + selectedFunction;
        }

        private void ResetRightFunctionPanel()
        {
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            HideObjectDefinitionParameterPanel();
            HideImageProcessingFlowTree();
            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.BringToFront();
        }

        private async void FunctionListBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int clickedIndex = functionListBox.IndexFromPoint(e.Location);
            if (clickedIndex < 0)
            {
                return;
            }

            string selectedFunction = functionListBox.Items[clickedIndex] as string;
            if (selectedFunction == LoadImageMenuText)
            {
                await OpenImageAsync();
            }
            else if (selectedFunction == RoiMenuText)
            {
                ToggleRoiMenu();
            }
            else if (IsRoiMenuItem(selectedFunction))
            {
                selectedRoiIndex = GetRoiIndex(selectedFunction);
                ApplySelectedRoiOverlay();
                statusLabel.Text = "目前選擇：" + selectedFunction.Trim();
            }
            else if (IsImageProcessingStepCommandMenuItem(selectedFunction) &&
                IsImageProcessingStepCommandForClickedStep(clickedIndex))
            {
                HandleImageProcessingStepCommand(selectedFunction);
            }
            else if (selectedFunction == ImageProcessingMenuText)
            {
                ToggleImageProcessingMenu();
            }
            else if (selectedFunction == ImagePreprocessingMenuText)
            {
                ToggleImagePreprocessingMenu();
            }
            else if (selectedFunction == ImageRelationMenuText)
            {
                ToggleImageRelationMenu();
            }
            else if (selectedFunction == ObjectJudgementMenuText)
            {
                ToggleObjectJudgementMenu();
            }
            else if (selectedFunction == ObjectDefinitionMenuText)
            {
                ToggleObjectDefinitionMenu();
            }
            else if (string.Equals(selectedFunction, OriginalPreprocessingSourceText, StringComparison.Ordinal))
            {
                activeImageRelationSourceType = "Original";
                activeImageRelationSourceId = null;
                RestorePreprocessedDisplaysToOriginalSource();
                statusLabel.Text = "目前選擇：原始影像";
            }
            else if (GetImageRelationGroupId(selectedFunction) != null)
            {
                // Only a normal left click changes expansion. Ctrl/Shift are
                // reserved for selecting multiple relation groups.
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                    functionListBox.SelectedIndices.Count <= 1)
                {
                    ToggleImageRelationGroup(selectedFunction);
                }
            }
            string objectDefinitionId;
            int objectDefinitionProcessingIndex;
            if (TryGetObjectDefinitionProcessingLocation(
                         clickedIndex,
                         out objectDefinitionId,
                         out objectDefinitionProcessingIndex))
            {
                string processingId = GetObjectDefinitionProcessingId(clickedIndex);
                if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                {
                    ProcessObjectDefinitionProcessing(objectDefinitionId, processingId);
                }

                aKeyProcessedFunctionListIndex = -1;
            }
            else if (!string.IsNullOrEmpty(objectDefinitionId = GetObjectDefinitionId(
                         clickedIndex,
                         selectedFunction)))
            {
                if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                {
                    ProcessObjectDefinition(objectDefinitionId);
                }

                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                    functionListBox.SelectedIndices.Count <= 1)
                {
                    ToggleObjectDefinition(objectDefinitionId);
                }

                aKeyProcessedFunctionListIndex = -1;
            }
            else
            {
                int objectProcessingOwnerIndex;
                int objectProcessingIndex;
                if (TryGetObjectJudgementProcessingLocation(
                        clickedIndex,
                        selectedFunction,
                        out objectProcessingOwnerIndex,
                        out objectProcessingIndex))
                {
                    if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                    {
                        ProcessObjectJudgementProcessing(
                            objectProcessingOwnerIndex,
                            objectProcessingIndex);
                    }

                    aKeyProcessedFunctionListIndex = -1;
                }
                else
                {
                    string objectJudgementGroupId = GetObjectJudgementGroupId(selectedFunction);
                    if (!string.IsNullOrEmpty(objectJudgementGroupId))
                    {
                        if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                        {
                            ProcessObjectJudgementGroup(objectJudgementGroupId);
                        }

                        // A group click expands or collapses its children. Ctrl-click
                        // multi-selection is handled by the ListBox and must not
                        // rebuild the list between selected items.
                        if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                            functionListBox.SelectedIndices.Count <= 1)
                        {
                            ToggleObjectJudgementGroup(objectJudgementGroupId);
                        }
                        aKeyProcessedFunctionListIndex = -1;
                    }
                    else
                    {
                    int objectIndex = GetObjectJudgementIndex(selectedFunction);
                    if (objectIndex >= 0)
                    {
                        if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                        {
                            ProcessObjectJudgement(objectIndex);
                        }

                        // A block click only selects the block. Its processing
                        // children are expanded by a normal left click. Ctrl/Shift
                        // are reserved for multi-selection and must not rebuild
                        // the list between selected blocks.
                        if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                            functionListBox.SelectedIndices.Count <= 1)
                        {
                            ToggleObjectJudgement(objectIndex);
                        }
                        aKeyProcessedFunctionListIndex = -1;
                    }
                    else if (GetObjectDefinitionIndex(selectedFunction) >= 0)
                    {
                        ShowObjectDefinitionParameterPanel(
                            GetObjectDefinitionId(functionListBox.SelectedIndex, selectedFunction));
                    }
                    }
                }
            }
            if (IsImagePreprocessingStepMenuItem(selectedFunction))
            {
                if (IsAKeyDown())
                {
                    ProcessImagePreprocessingStep(selectedFunction);
                }
            }
            else if (GetImagePreprocessingGroupId(selectedFunction) != null)
            {
                if (IsAKeyDown())
                {
                    ProcessImagePreprocessingGroup(
                        GetImagePreprocessingGroupId(selectedFunction));
                }
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None)
                {
                    ToggleImagePreprocessingGroup(selectedFunction);
                }
            }
            else if (GetImageProcessingGroupId(selectedFunction) != null)
            {
                if (IsAKeyDown())
                {
                    ProcessImageProcessingGroup(GetImageProcessingGroupId(selectedFunction));
                }
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None)
                {
                    ToggleImageProcessingGroup(selectedFunction);
                }
            }
            else if (IsImageProcessingStepMenuItem(selectedFunction))
            {
                if (IsAKeyDown())
                {
                    ProcessImageProcessingStep(selectedFunction);
                }
            }
        }

        private void CaptureSharedImageViewStateFromVisibleControls()
        {
            ImageDisplayControl source;
            if (isImageViewerMaximized)
            {
                source = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
            }
            else
            {
                source = GetVisibleLeftImageDisplayControl();
                if (source == null || !source.HasImage)
                {
                    source = GetVisibleRightImageDisplayControl();
                }
            }

            if (source == null || !source.HasImage)
            {
                return;
            }

            sharedImageViewState = source.ViewState;
            hasSharedImageViewState = true;
            if (isImageViewerMaximized)
            {
                maximizedImageViewerViewState = sharedImageViewState;
                hasMaximizedImageViewerViewState = true;
            }
        }

        private static bool IsAKeyDown()
        {
            return (GetAsyncKeyState((int)Keys.A) & 0x8000) != 0;
        }

        private void FunctionListBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int clickedIndex = functionListBox.IndexFromPoint(e.Location);
            if (clickedIndex < 0)
            {
                return;
            }

            string stepText = functionListBox.Items[clickedIndex] as string;
            if (stepText == RoiMenuText)
            {
                ShowRoiMenuContextMenu(e.Location);
                return;
            }

            if (IsRoiMenuItem(stepText))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowRoiItemContextMenu(stepText, e.Location);
                return;
            }

            if (stepText == ImageProcessingMenuText)
            {
                ShowImageProcessingMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ImageRelationMenuText)
            {
                ShowImageRelationMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ObjectJudgementMenuText)
            {
                ShowObjectJudgementMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ObjectDefinitionMenuText)
            {
                ShowObjectDefinitionMenuContextMenu(e.Location);
                return;
            }

            string objectDefinitionId;
            int objectDefinitionProcessingIndex;
            if (TryGetObjectDefinitionProcessingLocation(
                    clickedIndex,
                    out objectDefinitionId,
                    out objectDefinitionProcessingIndex))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectDefinitionProcessingContextMenu(
                    objectDefinitionId,
                    GetObjectDefinitionProcessingId(clickedIndex),
                    e.Location);
                return;
            }

            objectDefinitionId = GetObjectDefinitionId(clickedIndex, stepText);
            if (!string.IsNullOrEmpty(objectDefinitionId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectDefinitionItemContextMenu(objectDefinitionId, e.Location);
                return;
            }

            string objectJudgementGroupId = GetObjectJudgementGroupId(stepText);
            if (!string.IsNullOrEmpty(objectJudgementGroupId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                List<string> selectedObjectJudgementGroupIds =
                    GetSelectedObjectJudgementGroupIds();
                if (selectedObjectJudgementGroupIds.Count >= 2)
                {
                    ShowObjectJudgementGroupMultiSelectContextMenu(
                        selectedObjectJudgementGroupIds,
                        e.Location);
                }
                else
                {
                    ShowObjectJudgementGroupContextMenu(objectJudgementGroupId, e.Location);
                }
                return;
            }

            int objectProcessingOwnerIndex;
            int objectProcessingIndex;
            if (TryGetObjectJudgementProcessingLocation(
                    clickedIndex,
                    stepText,
                    out objectProcessingOwnerIndex,
                    out objectProcessingIndex))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectJudgementProcessingItemContextMenu(
                    objectProcessingOwnerIndex,
                    objectProcessingIndex,
                    e.Location);
                return;
            }

            int objectJudgementIndex = GetObjectJudgementIndex(stepText);
            if (objectJudgementIndex >= 0)
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                List<int> selectedObjectJudgementIndexes = GetSelectedObjectJudgementIndexes();
                if (selectedObjectJudgementIndexes.Count >= 2)
                {
                    ShowObjectJudgementMultiSelectContextMenu(
                        selectedObjectJudgementIndexes,
                        e.Location);
                }
                else
                {
                    ShowObjectJudgementItemContextMenu(objectJudgementIndex, e.Location);
                }
                return;
            }

            string clickedRelationGroupId = GetImageRelationGroupId(stepText);
            if (!string.IsNullOrEmpty(clickedRelationGroupId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowImageRelationGroupContextMenu(clickedRelationGroupId, e.Location);
                return;
            }

            int relationIndex = GetImageRelationIndex(stepText);
            if (relationIndex >= 0)
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                List<int> selectedRelationIndexes = GetSelectedImageRelationIndexes();
                if (selectedRelationIndexes.Count >= 2)
                {
                    ShowImageRelationMultiSelectContextMenu(selectedRelationIndexes, e.Location);
                    return;
                }

                ShowImageRelationItemContextMenu(relationIndex, e.Location);
                return;
            }

            if (stepText == ImagePreprocessingMenuText)
            {
                ShowImagePreprocessingMenuContextMenu(e.Location);
                return;
            }

            string clickedPreprocessingGroupId = GetImagePreprocessingGroupId(stepText);
            if (!string.IsNullOrEmpty(clickedPreprocessingGroupId))
            {
                ShowImagePreprocessingGroupContextMenu(clickedPreprocessingGroupId, e.Location);
                return;
            }

            if (IsImagePreprocessingStepMenuItem(stepText))
            {
                List<int> selectedPreprocessingSteps = GetSelectedImagePreprocessingStepIndexes();
                if (selectedPreprocessingSteps.Count >= 2)
                {
                    ShowImagePreprocessingMultiSelectContextMenu(selectedPreprocessingSteps, e.Location);
                    return;
                }

                ShowImagePreprocessingStepContextMenu(stepText, e.Location);
                return;
            }

            string clickedGroupId = GetImageProcessingGroupId(stepText);
            if (!IsImageProcessingStepMenuItem(stepText) && string.IsNullOrEmpty(clickedGroupId))
            {
                return;
            }

            if (!functionListBox.SelectedIndices.Contains(clickedIndex))
            {
                functionListBox.ClearSelected();
                functionListBox.SelectedIndex = clickedIndex;
            }

            List<int> selectedStepIndexes = GetSelectedImageProcessingStepIndexes();
            List<string> selectedGroupIds = GetSelectedImageProcessingGroupIds();
            if (selectedStepIndexes.Count + selectedGroupIds.Count >= 2)
            {
                ShowImageProcessingGroupContextMenu(selectedStepIndexes, selectedGroupIds, e.Location);
                return;
            }

            if (IsImageProcessingStepMenuItem(stepText))
            {
                ShowImageProcessingStepContextMenu(stepText, e.Location);
                return;
            }

            if (!string.IsNullOrEmpty(clickedGroupId))
            {
                ShowImageProcessingGroupItemContextMenu(clickedGroupId, e.Location);
            }
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

        private void ShowImageRelationMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增關聯", null, delegate { AddImageRelation(); });
            menu.Show(functionListBox, location);
        }

        private void ShowImageRelationParameterPanel(int relationIndex)
        {
            if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count) return;
            parameterPlaceholderLabel.Visible = false;
            HideImageProcessingParameterPanel();
            if (imageProcessingFlowTreeView != null) imageProcessingFlowTreeView.Visible = false;
            if (imagePreprocessingFlowTreeView != null) imagePreprocessingFlowTreeView.Visible = false;
            if (imageRelationParameterPanel != null) parameterPanel.Controls.Remove(imageRelationParameterPanel);
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            imageRelationParameterPanel = panel;
            panel.Controls.Add(new Label { Text = "來源影像", Left = 8, Top = 12, Width = 250 });
            var source = new ComboBox { Left = 8, Top = 34, Width = parameterPanel.Width - 18, DropDownStyle = ComboBoxStyle.DropDownList };
            source.Items.Add(new RelationChoice { DisplayText = "原始影像", Type = "Original", Id = string.Empty });
            for (int index = 0; index < systemParameters.ImagePreprocessingSteps.Count; index++)
            {
                ImageProcessingStepSettings step = systemParameters.ImagePreprocessingSteps[index];
                string displayName = string.IsNullOrWhiteSpace(step.DisplayName) ? "前處理" + (index + 1) : step.DisplayName;
                source.Items.Add(new RelationChoice { DisplayText = displayName + " (" + step.Method + ")", Type = "Step", Id = step.Id });
            }
            foreach (ImageProcessingGroupSettings group in systemParameters.ImagePreprocessingGroups)
                source.Items.Add(new RelationChoice { DisplayText = group.DisplayName, Type = "Group", Id = group.Id });
            source.SelectedIndex = FindRelationChoiceIndex(source, relation.SourceType, relation.SourceId);
            panel.Controls.Add(source);
            panel.Controls.Add(new Label { Text = "影像處理", Left = 8, Top = 72, Width = 250 });
            var target = new ComboBox { Left = 8, Top = 94, Width = parameterPanel.Width - 18, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[index];
                string displayName = string.IsNullOrWhiteSpace(step.DisplayName) ? "處理" + (index + 1) : step.DisplayName;
                target.Items.Add(new RelationChoice { DisplayText = displayName + " (" + step.Method + ")", Type = "Step", Id = step.Id });
            }
            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
                target.Items.Add(new RelationChoice { DisplayText = group.DisplayName, Type = "Group", Id = group.Id });
            target.SelectedIndex = FindRelationChoiceIndex(target, relation.ProcessingType, relation.ProcessingId);
            panel.Controls.Add(target);
            var apply = new Button { Text = "套用", Left = 8, Top = 135, Width = parameterPanel.Width - 18 };
            apply.Click += delegate
            {
                SaveRelationChoice(relation, source.SelectedItem, true);
                SaveRelationChoice(relation, target.SelectedItem, false);
                SaveSystemParameters();
                statusLabel.Text = "已套用影像關聯";
            };
            panel.Controls.Add(apply);
            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
        }

        private void HideImageRelationParameterPanel()
        {
            if (imageRelationParameterPanel != null)
            {
                imageRelationParameterPanel.Visible = false;
            }
        }

        private static int FindRelationChoiceIndex(ComboBox combo, string type, string id)
        {
            for (int index = 0; index < combo.Items.Count; index++)
            {
                RelationChoice choice = combo.Items[index] as RelationChoice;
                if (choice != null && choice.Type == (type ?? string.Empty) && choice.Id == (id ?? string.Empty)) return index;
            }
            return combo.Items.Count > 0 ? 0 : -1;
        }

        private static void SaveRelationChoice(ImageRelationSettings relation, object choice, bool source)
        {
            RelationChoice selected = choice as RelationChoice;
            if (selected == null) return;
            if (source) { relation.SourceType = selected.Type; relation.SourceId = selected.Id; }
            else { relation.ProcessingType = selected.Type; relation.ProcessingId = selected.Id; }
        }

        private void AddImageRelation()
        {
            systemParameters.ImageRelations.Add(new ImageRelationSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "關聯" + (systemParameters.ImageRelations.Count + 1).ToString(CultureInfo.InvariantCulture),
                SourceType = "Original",
                SourceId = string.Empty,
                ProcessingType = "Step",
                ProcessingId = string.Empty,
                GroupId = string.Empty
            });
            SaveSystemParameters();
            imageRelationMenuExpanded = true;
            RebuildVisibleImageRelations();
            statusLabel.Text = "已新增影像關聯，請選擇來源與處理項目";
        }

        private void ToggleImageRelationMenu()
        {
            imageRelationMenuExpanded = !imageRelationMenuExpanded;
            RebuildVisibleImageRelations();
            statusLabel.Text = imageRelationMenuExpanded ? "已展開影像關聯" : "已收合影像關聯";
        }

        private void RebuildVisibleImageRelations()
        {
            int relationMenuIndex = functionListBox.Items.IndexOf(ImageRelationMenuText);
            if (relationMenuIndex < 0)
            {
                return;
            }

            int removeIndex = relationMenuIndex + 1;
            while (removeIndex < functionListBox.Items.Count)
            {
                string text = functionListBox.Items[removeIndex] as string;
                if (string.IsNullOrEmpty(text) || !text.StartsWith("    ", StringComparison.Ordinal))
                {
                    break;
                }

                functionListBox.Items.RemoveAt(removeIndex);
            }

            if (!imageRelationMenuExpanded)
            {
                return;
            }

            int insertIndex = relationMenuIndex + 1;
            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                if (!string.IsNullOrWhiteSpace(relation.GroupId))
                {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名" : relation.DisplayName;
                functionListBox.Items.Insert(insertIndex++, "    " + displayName);
            }

            foreach (ImageRelationGroupSettings group in systemParameters.ImageRelationGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.ParentGroupId))
                {
                    continue;
                }

                InsertVisibleImageRelationGroup(group, ref insertIndex, 0);
            }
        }

        private void InsertVisibleImageRelationGroup(ImageRelationGroupSettings group, ref int insertIndex, int depth)
        {
            functionListBox.Items.Insert(insertIndex++, CreateImageRelationGroupText(group, depth));
            if (!expandedImageRelationGroupIds.Contains(group.Id))
            {
                return;
            }

            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                if (string.Equals(relation.GroupId, group.Id, StringComparison.Ordinal))
                {
                    string displayName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名" : relation.DisplayName;
                    functionListBox.Items.Insert(insertIndex++, new string(' ', 6 + (depth * 2)) + displayName);
                }
            }

            foreach (ImageRelationGroupSettings child in systemParameters.ImageRelationGroups)
            {
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    InsertVisibleImageRelationGroup(child, ref insertIndex, depth + 1);
                }
            }
        }

        private int GetImageRelationIndex(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("    ", StringComparison.Ordinal)) return -1;
            string name = text.Trim();
            for (int index = 0; index < systemParameters.ImageRelations.Count; index++)
            {
                if (string.Equals(systemParameters.ImageRelations[index].DisplayName, name, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private void ShowImageRelationItemContextMenu(int relationIndex, Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessImageRelation(relationIndex); });
            menu.Items.Add("上移", null, delegate { MoveImageRelation(relationIndex, -1); });
            menu.Items.Add("下移", null, delegate { MoveImageRelation(relationIndex, 1); });
            menu.Items.Add("命名", null, delegate { RenameImageRelation(relationIndex); });
            menu.Items.Add("刪除", null, delegate
            {
                if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count)
                {
                    return;
                }

                ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
                if (!ConfirmDeleteImageRelation(relation))
                {
                    return;
                }

                systemParameters.ImageRelations.RemoveAt(relationIndex);
                SaveSystemParameters();
                RebuildVisibleImageRelations();
            });
            menu.Show(functionListBox, location);
        }

        private bool ConfirmDeleteImageRelation(ImageRelationSettings relation)
        {
            if (relation == null)
            {
                return false;
            }

            var affectedObjectNames = new List<string>();
            var directlyUsingObjects = new List<ObjectJudgementSettings>();
            foreach (ObjectJudgementSettings objectJudgement in systemParameters.ObjectJudgements)
            {
                string objectName = string.IsNullOrWhiteSpace(objectJudgement.DisplayName)
                    ? "未命名區塊"
                    : objectJudgement.DisplayName;
                bool isDirectReference =
                    string.Equals(objectJudgement.RelationType, "Relation", StringComparison.Ordinal) &&
                    string.Equals(objectJudgement.RelationId, relation.Id, StringComparison.Ordinal);

                bool isGroupReference = false;
                if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
                {
                    ImageRelationGroupSettings group = FindImageRelationGroup(objectJudgement.RelationId);
                    if (group != null)
                    {
                        foreach (ImageRelationSettings groupedRelation in GetImageRelationGroupRelations(group.Id))
                        {
                            if (string.Equals(groupedRelation.Id, relation.Id, StringComparison.Ordinal))
                            {
                                isGroupReference = true;
                                break;
                            }
                        }
                    }
                }

                if (isDirectReference || isGroupReference)
                {
                    affectedObjectNames.Add(objectName);
                    if (isDirectReference)
                    {
                        directlyUsingObjects.Add(objectJudgement);
                    }
                }
            }

            string relationName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名關聯" : relation.DisplayName;
            string message;
            string title;
            if (affectedObjectNames.Count == 0)
            {
                message = "是否要刪除關聯「" + relationName + "」？";
                title = "刪除影像關聯";
            }
            else
            {
                message = "關聯「" + relationName + "」已被以下整合成區塊使用：\r\n" +
                    string.Join("、", affectedObjectNames.ToArray()) +
                    "\r\n\r\n刪除後，直接使用此關聯的區塊設定會被清除；使用關聯群組的區塊結果也會受到影響。\r\n是否仍要刪除？";
                title = "刪除前關聯警告";
            }

            if (MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return false;
            }

            foreach (ObjectJudgementSettings objectJudgement in directlyUsingObjects)
            {
                objectJudgement.RelationType = string.Empty;
                objectJudgement.RelationId = string.Empty;
            }

            return true;
        }

        private void MoveImageRelation(int relationIndex, int direction)
        {
            int target = relationIndex + direction;
            if (relationIndex < 0 || target < 0 || target >= systemParameters.ImageRelations.Count) return;
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            systemParameters.ImageRelations.RemoveAt(relationIndex);
            systemParameters.ImageRelations.Insert(target, relation);
            SaveSystemParameters();
            RebuildVisibleImageRelations();
        }

        private void ProcessImageRelation(int relationIndex)
        {
            if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count) return;
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            if (string.IsNullOrWhiteSpace(relation.SourceType))
            {
                statusLabel.Text = "影像關聯尚未選擇來源影像";
                return;
            }
            activeImageRelationSourceType = relation.SourceType;
            activeImageRelationSourceId = relation.SourceId;
            activeImageRelationGroupId = null;
            selectedImageRelationGroupId = null;
            preprocessingExecutionRequestedByImageRelation =
                !string.Equals(relation.SourceType, "Original", StringComparison.Ordinal);
            if (!string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
            {
                // A relation is an explicit processing request, so it may start
                // preparation of its selected preprocessing source even though
                // normal startup intentionally does not run preprocessing.
                preprocessingExecutionRequested = preprocessingExecutionRequestedByImageRelation;
            }
            else
            {
                preprocessingExecutionRequested = false;
                RestorePreprocessedDisplaysToOriginalSource();
            }
            if (string.IsNullOrWhiteSpace(relation.ProcessingId))
            {
                statusLabel.Text = "影像關聯尚未選擇影像處理項目";
                return;
            }

            if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal))
            {
                if (!systemParameters.ImageProcessingGroups.Any(group => string.Equals(group.Id, relation.ProcessingId, StringComparison.Ordinal)))
                {
                    statusLabel.Text = "影像關聯的處理群組不存在";
                    return;
                }
                ProcessImageProcessingGroup(relation.ProcessingId);
                return;
            }

            int stepIndex = systemParameters.ImageProcessingSteps.FindIndex(step => string.Equals(step.Id, relation.ProcessingId, StringComparison.Ordinal));
            if (stepIndex < 0)
            {
                statusLabel.Text = "影像關聯的處理項目不存在";
                return;
            }
            ProcessImageProcessingStep(CreateImageProcessingStepText(stepIndex + 1));
        }

        private void RenameImageRelation(int relationIndex)
        {
            if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count) return;
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            string name = PromptForText("關聯名稱", "請輸入關聯名稱：", relation.DisplayName);
            if (name == null) return;
            relation.DisplayName = string.IsNullOrWhiteSpace(name) ? relation.DisplayName : name.Trim();
            SaveSystemParameters();
            RebuildVisibleImageRelations();
        }

        private string PromptForText(string title, string message, string value)
        {
            using (var dialog = new Form { Text = title, Width = 360, Height = 135, StartPosition = FormStartPosition.CenterParent })
            using (var input = new TextBox { Left = 12, Top = 30, Width = 320, Text = value ?? string.Empty })
            using (var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, Left = 176, Top = 65, Width = 75 })
            using (var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 257, Top = 65, Width = 75 })
            {
                dialog.Controls.Add(new Label { Text = message, Left = 12, Top = 8, Width = 320 });
                dialog.Controls.Add(input); dialog.Controls.Add(ok); dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
            }
        }

        private bool ConfirmDeleteWithImageRelations(
            string itemDescription,
            HashSet<string> preprocessingStepIds,
            HashSet<string> preprocessingGroupIds,
            HashSet<string> processingStepIds,
            HashSet<string> processingGroupIds)
        {
            var affectedRelations = new List<ImageRelationSettings>();
            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                bool usesDeletedPreprocessingSource =
                    string.Equals(relation.SourceType, "Step", StringComparison.Ordinal) &&
                    preprocessingStepIds.Contains(relation.SourceId) ||
                    string.Equals(relation.SourceType, "Group", StringComparison.Ordinal) &&
                    preprocessingGroupIds.Contains(relation.SourceId);
                bool usesDeletedProcessingTarget =
                    string.Equals(relation.ProcessingType, "Step", StringComparison.Ordinal) &&
                    processingStepIds.Contains(relation.ProcessingId) ||
                    string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal) &&
                    processingGroupIds.Contains(relation.ProcessingId);

                if (usesDeletedPreprocessingSource || usesDeletedProcessingTarget)
                {
                    affectedRelations.Add(relation);
                }
            }

            var relationNames = new List<string>();
            foreach (ImageRelationSettings relation in affectedRelations)
            {
                relationNames.Add("關聯「" + (string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名關聯" : relation.DisplayName) + "」");
            }

            string message;
/*
            if (affectedRelations.Count == 0)
            {
                message = "是否要刪除" + itemDescription + "？";
            }
            else
            {
                message = "" + itemDescription + " 已建立影像關聯：\r\n" +
                    string.Join("、", relationNames.ToArray()) +\n"\r\n刪除後，受影響的關聯也會一併刪除。\r\n是否要繼續？";
            }

*/
            message = affectedRelations.Count == 0
                ? "是否要刪除" + itemDescription + "？"
                : itemDescription + " 已建立影像關聯：\r\n" +
                    string.Join("、", relationNames.ToArray()) +
                    "\r\n\r\n刪除後，受影響的關聯也會一併刪除。\r\n是否要繼續？";

            if (MessageBox.Show(
                    this,
                    message,
                    affectedRelations.Count == 0 ? "刪除確認" : "刪除前關聯確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return false;
            }

            foreach (ImageRelationSettings relation in affectedRelations)
            {
                systemParameters.ImageRelations.Remove(relation);
            }

            if (affectedRelations.Count > 0)
            {
                selectedImageRelationIndex = -1;
                activeImageRelationSourceType = "Original";
                activeImageRelationSourceId = null;
                RebuildVisibleImageRelations();
            }

            return true;
        }

        private void ShowImageProcessingMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("新增影像處理", null, delegate { AddImageProcessingStep(); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowRoiMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("新增 ROI", null, delegate { BeginAddRoiSelection(); });
            menu.Items.Add("顯示全部", null, delegate { ShowAllRoiOverlays(); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowRoiItemContextMenu(string roiText, Point location)
        {
            int roiIndex = GetRoiIndex(roiText);
            if (roiIndex < 0 || roiIndex >= systemParameters.RoiRegions.Count)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("刪除", null, delegate
            {
                expandedRoiText = roiText;
                DeleteSelectedRoi();
            });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private List<string> GetSelectedImageProcessingGroupIds()
        {
            var groupIds = new List<string>();
            foreach (object selectedItem in functionListBox.SelectedItems)
            {
                string groupId = GetImageProcessingGroupId(selectedItem as string);
                if (!string.IsNullOrEmpty(groupId) && !groupIds.Contains(groupId))
                {
                    groupIds.Add(groupId);
                }
            }

            return groupIds;
        }

        private void ShowImageProcessingGroupContextMenu(List<int> stepIndexes, List<string> groupIds, Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("分組", null, delegate { CreateImageProcessingGroup(stepIndexes, groupIds); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowImageProcessingStepContextMenu(string stepText, Point location)
        {
            CloseImageProcessingStepContextMenu();

            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("處理", null, delegate
            {
                ProcessImageProcessingStep(stepText);
            });
            menu.Items.Add("上移", null, delegate
            {
                expandedImageProcessingStepText = stepText;
                MoveImageProcessingStep(-1);
            });
            menu.Items.Add("下移", null, delegate
            {
                expandedImageProcessingStepText = stepText;
                MoveImageProcessingStep(1);
            });
            menu.Items.Add("命名", null, delegate { RenameImageProcessingStep(stepText); });
            menu.Items.Add("刪除", null, delegate
            {
                expandedImageProcessingStepText = stepText;
                DeleteImageProcessingStep();
            });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowImageProcessingGroupItemContextMenu(string groupId, Point location)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("處理", null, delegate { ProcessImageProcessingGroup(group.Id); });
            menu.Items.Add("上移", null, delegate { MoveImageProcessingGroup(group.Id, -1); });
            menu.Items.Add("下移", null, delegate { MoveImageProcessingGroup(group.Id, 1); });
            menu.Items.Add("命名", null, delegate { RenameImageProcessingGroup(group.Id); });
            menu.Items.Add("新增處理", null, delegate { AddImageProcessingStepToGroup(group.Id); });
            menu.Items.Add("解除群組", null, delegate { UngroupImageProcessingGroup(group.Id); });
            menu.Items.Add("刪除", null, delegate { DeleteImageProcessingGroup(group.Id); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void CloseImageProcessingStepContextMenu()
        {
            ContextMenuStrip menu = imageProcessingStepContextMenu;
            imageProcessingStepContextMenu = null;
            if (menu != null && !menu.IsDisposed)
            {
                menu.Close();
                if (!menu.IsDisposed)
                {
                    menu.Dispose();
                }
            }
        }

        private void CreateImageProcessingGroup(List<int> stepIndexes, List<string> groupIds)
        {
            int stepCount = stepIndexes == null ? 0 : stepIndexes.Count;
            int groupCount = groupIds == null ? 0 : groupIds.Count;
            if (stepCount + groupCount < 2)
            {
                return;
            }

            string displayName;
            if (!TryGetImageProcessingStepName(string.Empty, out displayName))
            {
                return;
            }

            var group = new ImageProcessingGroupSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "未命名群組" : displayName
            };
            systemParameters.ImageProcessingGroups.Add(group);
            foreach (int stepIndex in stepIndexes)
            {
                if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
                {
                    systemParameters.ImageProcessingSteps[stepIndex].GroupId = group.Id;
                }
            }

            foreach (string groupId in groupIds)
            {
                ImageProcessingGroupSettings childGroup = FindImageProcessingGroup(groupId);
                if (childGroup != null)
                {
                    childGroup.ParentGroupId = group.Id;
                }
            }

            RemoveEmptyImageProcessingGroups();
            SaveSystemParameters();
            MarkProcessedImageDirty();
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = CreateImageProcessingGroupText(group);
            statusLabel.Text = "已建立群組：" + group.DisplayName;
        }

        private void RenameImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            string name;
            if (!TryGetImageProcessingStepName(group.DisplayName, out name) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            group.DisplayName = name;
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = CreateImageProcessingGroupText(group);
            statusLabel.Text = "已命名群組：" + group.DisplayName;
        }

        private void MoveImageProcessingGroup(string groupId, int direction)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            var siblings = new List<ImageProcessingGroupSettings>();
            foreach (ImageProcessingGroupSettings candidate in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(candidate.ParentGroupId, group.ParentGroupId, StringComparison.Ordinal))
                {
                    siblings.Add(candidate);
                }
            }

            int siblingIndex = siblings.IndexOf(group);
            int targetSiblingIndex = siblingIndex + direction;
            if (siblingIndex < 0 || targetSiblingIndex < 0 || targetSiblingIndex >= siblings.Count)
            {
                return;
            }

            ImageProcessingGroupSettings target = siblings[targetSiblingIndex];
            int groupIndex = systemParameters.ImageProcessingGroups.IndexOf(group);
            systemParameters.ImageProcessingGroups.RemoveAt(groupIndex);
            int targetIndex = systemParameters.ImageProcessingGroups.IndexOf(target);
            systemParameters.ImageProcessingGroups.Insert(
                direction > 0 ? targetIndex + 1 : targetIndex,
                group);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = CreateImageProcessingGroupText(group);
            statusLabel.Text = "已移動群組：" + group.DisplayName;
        }

        private void UngroupImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "是否要解除群組「" + group.DisplayName + "」？群組內的處理項目與子群組會保留。",
                    "解除群組",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            foreach (ImageProcessingStepSettings step in systemParameters.ImageProcessingSteps)
            {
                if (string.Equals(step.GroupId, group.Id, StringComparison.Ordinal))
                {
                    step.GroupId = null;
                }
            }

            foreach (ImageProcessingGroupSettings child in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    child.ParentGroupId = group.ParentGroupId;
                }
            }

            expandedImageProcessingGroupIds.Remove(group.Id);
            systemParameters.ImageProcessingGroups.Remove(group);
            SaveSystemParameters();
            MarkProcessedImageDirty();
            RebuildVisibleImageProcessingSteps();
            statusLabel.Text = "已解除群組：" + group.DisplayName;
        }

        private void DeleteImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            var groupIdsToDelete = new HashSet<string>(StringComparer.Ordinal);
            CollectImageProcessingGroupAndDescendantIds(group.Id, groupIdsToDelete);
            int stepCount = systemParameters.ImageProcessingSteps.Count(step => groupIdsToDelete.Contains(step.GroupId));
            var deletedStepIds = new HashSet<string>(
                systemParameters.ImageProcessingSteps
                    .Where(step => groupIdsToDelete.Contains(step.GroupId))
                    .Select(step => step.Id),
                StringComparer.Ordinal);
            if (!ConfirmDeleteWithImageRelations(
                    "影像處理群組「" + group.DisplayName + "」及其底下的 " + stepCount + " 個處理",
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal),
                    deletedStepIds,
                    groupIdsToDelete))
            {
                return;
            }

            for (int index = systemParameters.ImageProcessingSteps.Count - 1; index >= 0; index--)
            {
                if (groupIdsToDelete.Contains(systemParameters.ImageProcessingSteps[index].GroupId))
                {
                    systemParameters.ImageProcessingSteps.RemoveAt(index);
                }
            }

            for (int index = systemParameters.ImageProcessingGroups.Count - 1; index >= 0; index--)
            {
                if (groupIdsToDelete.Contains(systemParameters.ImageProcessingGroups[index].Id))
                {
                    expandedImageProcessingGroupIds.Remove(systemParameters.ImageProcessingGroups[index].Id);
                    systemParameters.ImageProcessingGroups.RemoveAt(index);
                }
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            SaveSystemParameters();
            MarkProcessedImageDirty();
            ClearProcessedPreviewImages();
            RebuildVisibleImageProcessingSteps();
            statusLabel.Text = "已刪除群組：" + group.DisplayName;
        }

        private void CollectImageProcessingGroupAndDescendantIds(string groupId, HashSet<string> groupIds)
        {
            if (string.IsNullOrEmpty(groupId) || !groupIds.Add(groupId))
            {
                return;
            }

            foreach (ImageProcessingGroupSettings child in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(child.ParentGroupId, groupId, StringComparison.Ordinal))
                {
                    CollectImageProcessingGroupAndDescendantIds(child.Id, groupIds);
                }
            }
        }

        private void RemoveEmptyImageProcessingGroups()
        {
            bool removed;
            do
            {
                removed = false;
                for (int index = systemParameters.ImageProcessingGroups.Count - 1; index >= 0; index--)
                {
                    ImageProcessingGroupSettings group = systemParameters.ImageProcessingGroups[index];
                    bool containsStep = systemParameters.ImageProcessingSteps.Any(step =>
                        string.Equals(step.GroupId, group.Id, StringComparison.Ordinal));
                    bool containsGroup = systemParameters.ImageProcessingGroups.Any(child =>
                        !ReferenceEquals(child, group) &&
                        string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal));
                    if (containsStep || containsGroup)
                    {
                        continue;
                    }

                    expandedImageProcessingGroupIds.Remove(group.Id);
                    systemParameters.ImageProcessingGroups.RemoveAt(index);
                    removed = true;
                }
            }
            while (removed);
        }

        private bool IsImageProcessingStepCommandForClickedStep(int commandIndex)
        {
            for (int index = commandIndex - 1; index >= 0; index--)
            {
                string menuItem = functionListBox.Items[index] as string;
                if (IsImageProcessingStepCommandMenuItem(menuItem))
                {
                    continue;
                }

                return IsImageProcessingStepMenuItem(menuItem);
            }

            return false;
        }

        private void ToggleRoiMenu()
        {
            if (roiMenuExpanded)
            {
                RemoveRoiSubMenuItems();
            }
            else
            {
                int roiIndex = functionListBox.Items.IndexOf(RoiMenuText);
                if (roiIndex >= 0)
                {
                    roiMenuExpanded = true;
                    RebuildVisibleRoiItems();
                }
            }
        }

        private void RemoveRoiSubMenuItems()
        {
            RemoveRoiSubMenuItemsFromListBox();
            roiMenuExpanded = false;
        }

        private void RemoveRoiSubMenuItemsFromListBox()
        {
            for (int index = functionListBox.Items.Count - 1; index >= 0; index--)
            {
                string itemText = functionListBox.Items[index] as string;
                if (IsRoiMenuItem(itemText))
                {
                    functionListBox.Items.RemoveAt(index);
                }
            }

            expandedRoiText = null;
        }

        private void RebuildVisibleRoiItems()
        {
            if (!roiMenuExpanded)
            {
                return;
            }

            RemoveRoiSubMenuItemsFromListBox();

            int insertIndex = functionListBox.Items.IndexOf(RoiMenuText) + 1;
            for (int index = 0; index < systemParameters.RoiRegions.Count; index++)
            {
                functionListBox.Items.Insert(insertIndex, CreateRoiText(index + 1));
                insertIndex++;
            }
        }

        private void ShowAllRoiOverlays()
        {
            var rois = new List<Rectangle>();
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                rois.Add(roiRegion.Bounds);
            }

            leftOriginalDisplayControl.SetRoiOverlays(rois);
            rightOriginalDisplayControl.SetRoiOverlays(rois);
            leftProcessedDisplayControl.SetRoiOverlays(rois);
            rightProcessedDisplayControl.SetRoiOverlays(rois);
            statusLabel.Text = "目前顯示全部 ROI";
        }

        private void DeleteSelectedRoi()
        {
            int roiIndex = GetRoiIndex(expandedRoiText);
            if (roiIndex < 0 || roiIndex >= systemParameters.RoiRegions.Count)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "是否要刪除該項 ROI？",
                "刪除 ROI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                statusLabel.Text = "已取消刪除 ROI";
                return;
            }

            systemParameters.RoiRegions.RemoveAt(roiIndex);
            selectedRoiIndex = Math.Min(roiIndex, systemParameters.RoiRegions.Count - 1);
            SyncLegacyRoiFromSelectedRoi();
            SaveSystemParameters();
            RebuildVisibleRoiItems();
            ApplySelectedRoiOverlay();
            ClearProcessedPreviewImages();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已刪除 ROI " + (roiIndex + 1);
        }

        private static string CreateRoiText(int roiNumber)
        {
            return "    ROI " + roiNumber;
        }

        private static bool IsRoiMenuItem(string menuText)
        {
            if (string.IsNullOrEmpty(menuText))
            {
                return false;
            }

            string trimmedText = menuText.Trim();
            if (!trimmedText.StartsWith("ROI ", StringComparison.Ordinal))
            {
                return false;
            }

            int roiNumber;
            return int.TryParse(trimmedText.Substring("ROI ".Length), out roiNumber);
        }

        private int GetRoiIndex(string roiText)
        {
            if (!IsRoiMenuItem(roiText))
            {
                return -1;
            }

            int roiNumber;
            return int.TryParse(roiText.Trim().Substring("ROI ".Length), out roiNumber)
                ? roiNumber - 1
                : -1;
        }

        private void ToggleImageProcessingMenu()
        {
            if (imageProcessingMenuExpanded)
            {
                RemoveImageProcessingSubMenuItems();
            }
            else
            {
                int imageProcessingIndex = functionListBox.Items.IndexOf(ImageProcessingMenuText);
                if (imageProcessingIndex >= 0)
                {
                    imageProcessingMenuExpanded = true;
                    RebuildVisibleImageProcessingSteps();
                }
            }
        }

        private void AddImageProcessingStep()
        {
            RemoveImageProcessingStepCommandMenuItems();

            systemParameters.ImageProcessingSteps.Add(new ImageProcessingStepSettings
            {
                Id = Guid.NewGuid().ToString("N")
            });
            SaveSystemParameters();
            string stepText = CreateImageProcessingStepText(systemParameters.ImageProcessingSteps.Count);
            imageProcessingMenuExpanded = true;
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = stepText;
            statusLabel.Text = "已新增" + stepText.Trim();
        }

        private void AddImageProcessingStepToGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();

            var step = new ImageProcessingStepSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = group.Id
            };
            systemParameters.ImageProcessingSteps.Add(step);
            int stepIndex = systemParameters.ImageProcessingSteps.Count - 1;

            imageProcessingMenuExpanded = true;
            expandedImageProcessingGroupIds.Add(group.Id);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();

            string selectedStepText = null;
            foreach (object item in functionListBox.Items)
            {
                string itemText = item as string;
                if (IsImageProcessingStepMenuItem(itemText) &&
                    GetImageProcessingStepIndex(itemText) == stepIndex)
                {
                    selectedStepText = itemText;
                    break;
                }
            }

            if (selectedStepText != null)
            {
                functionListBox.SelectedItem = selectedStepText;
            }

            statusLabel.Text = "已新增群組處理，請選擇處理方式";
        }

        private void ToggleImageProcessingStepMenu(string stepText)
        {
            if (expandedImageProcessingStepText == stepText)
            {
                RemoveImageProcessingStepCommandMenuItems();
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();

            int stepIndex = functionListBox.Items.IndexOf(stepText);
            if (stepIndex >= 0)
            {
                functionListBox.Items.Insert(stepIndex + 1, DeleteImageProcessingStepMenuText);
                functionListBox.Items.Insert(stepIndex + 2, MoveUpImageProcessingStepMenuText);
                functionListBox.Items.Insert(stepIndex + 3, MoveDownImageProcessingStepMenuText);
                expandedImageProcessingStepText = stepText;
            }
        }

        private void RemoveImageProcessingSubMenuItems()
        {
            RemoveImageProcessingSubMenuItemsFromListBox();
            imageProcessingMenuExpanded = false;
        }

        private void RemoveImageProcessingSubMenuItemsFromListBox()
        {
            for (int index = functionListBox.Items.Count - 1; index >= 0; index--)
            {
                string itemText = functionListBox.Items[index] as string;
                if (IsImageProcessingStepMenuItem(itemText) ||
                    IsImageProcessingGroupMenuItem(itemText))
                {
                    functionListBox.Items.RemoveAt(index);
                }
            }

            visibleImageProcessingStepIds.Clear();
            visibleImageProcessingGroupIds.Clear();
        }

        private void RemoveImageProcessingStepCommandMenuItems()
        {
            functionListBox.Items.Remove(DeleteImageProcessingStepMenuText);
            functionListBox.Items.Remove(MoveUpImageProcessingStepMenuText);
            functionListBox.Items.Remove(MoveDownImageProcessingStepMenuText);
            expandedImageProcessingStepText = null;
        }

        private void HandleImageProcessingStepCommand(string selectedFunction)
        {
            if (string.IsNullOrEmpty(expandedImageProcessingStepText))
            {
                return;
            }

            if (selectedFunction == DeleteImageProcessingStepMenuText)
            {
                DeleteImageProcessingStep();
            }
            else if (selectedFunction == MoveUpImageProcessingStepMenuText)
            {
                MoveImageProcessingStep(-1);
            }
            else if (selectedFunction == MoveDownImageProcessingStepMenuText)
            {
                MoveImageProcessingStep(1);
            }
        }

        private void DeleteImageProcessingStep()
        {
            string stepText = expandedImageProcessingStepText;
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            var deletedStepIds = new HashSet<string>(StringComparer.Ordinal) { step.Id };
            if (!ConfirmDeleteWithImageRelations(
                    "處理「" + (string.IsNullOrWhiteSpace(step.DisplayName) ? step.Method : step.DisplayName) + "」",
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal),
                    deletedStepIds,
                    new HashSet<string>(StringComparer.Ordinal)))
            {
                statusLabel.Text = "已取消刪除";
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();
            functionListBox.Items.Remove(stepText);
            if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                systemParameters.ImageProcessingSteps.RemoveAt(stepIndex);
                RemoveEmptyImageProcessingGroups();
                SaveSystemParameters();
            }

            selectedImageProcessingStepIndex = Math.Min(selectedImageProcessingStepIndex, systemParameters.ImageProcessingSteps.Count - 1);
            MarkProcessedImageDirty();
            if (!HasPreviewableImageProcessingStep())
            {
                ClearProcessedPreviewImages();
            }
            else
            {
                ScheduleProcessedImageUpdateIfVisible();
            }

            RebuildVisibleImageProcessingSteps();
            statusLabel.Text = "已刪除" + stepText.Trim();
        }

        private void MoveImageProcessingStep(int direction)
        {
            string stepText = expandedImageProcessingStepText;
            RemoveImageProcessingStepCommandMenuItems();

            int stepIndex = GetImageProcessingStepIndex(stepText);
            int targetIndex = stepIndex + direction;
            if (stepIndex < 0 || targetIndex < 0 || targetIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                statusLabel.Text = stepText.Trim() + (direction < 0 ? " 目前已在最上方" : " 目前已在最下方");
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            systemParameters.ImageProcessingSteps.RemoveAt(stepIndex);
            systemParameters.ImageProcessingSteps.Insert(targetIndex, step);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            string movedStepText = CreateImageProcessingStepText(targetIndex + 1);
            functionListBox.SelectedItem = movedStepText;
            statusLabel.Text = "已移動" + movedStepText.Trim();
        }

        private void RebuildVisibleImageProcessingSteps()
        {
            if (!imageProcessingMenuExpanded)
            {
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();
            RemoveImageProcessingSubMenuItemsFromListBox();

            int insertIndex = functionListBox.Items.IndexOf(ImageProcessingMenuText) + 1;
            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[index];
                if (string.IsNullOrWhiteSpace(step.GroupId))
                {
                    functionListBox.Items.Insert(
                        insertIndex,
                        RegisterVisibleImageProcessingStep(step, index + 1, 0));
                    insertIndex++;
                }
            }

            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.IsNullOrWhiteSpace(group.ParentGroupId))
                {
                    InsertImageProcessingGroup(group, 0, ref insertIndex);
                }
            }
        }

        private void InsertImageProcessingGroup(ImageProcessingGroupSettings group, int depth, ref int insertIndex)
        {
            functionListBox.Items.Insert(
                insertIndex,
                RegisterVisibleImageProcessingGroup(group, depth));
            insertIndex++;
            if (!expandedImageProcessingGroupIds.Contains(group.Id))
            {
                return;
            }

            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                if (string.Equals(systemParameters.ImageProcessingSteps[index].GroupId, group.Id, StringComparison.Ordinal))
                {
                    functionListBox.Items.Insert(
                        insertIndex,
                        RegisterVisibleImageProcessingStep(
                            systemParameters.ImageProcessingSteps[index],
                            index + 1,
                            depth + 1));
                    insertIndex++;
                }
            }

            foreach (ImageProcessingGroupSettings childGroup in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    InsertImageProcessingGroup(childGroup, depth + 1, ref insertIndex);
                }
            }
        }

        private ImageProcessingGroupSettings FindImageProcessingGroup(string groupId)
        {
            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(group.Id, groupId, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }

        private void ToggleImageProcessingGroup(string groupText)
        {
            string groupId = GetImageProcessingGroupId(groupText);
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            if (!expandedImageProcessingGroupIds.Remove(groupId))
            {
                expandedImageProcessingGroupIds.Add(groupId);
            }

            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = groupText;
        }

        private static string CreateImageProcessingGroupText(ImageProcessingGroupSettings group, int depth)
        {
            string displayName = string.IsNullOrWhiteSpace(group.DisplayName) ? "未命名群組" : group.DisplayName;
            return new string(' ', 4 + (depth * 2)) + "群組(" + displayName + ")";
        }

        private static string CreateImageProcessingGroupText(ImageProcessingGroupSettings group)
        {
            return CreateImageProcessingGroupText(group, 0);
        }

        private string GetImageProcessingGroupId(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return null;
            }

            string groupId;
            if (visibleImageProcessingGroupIds.TryGetValue(menuText, out groupId) &&
                FindImageProcessingGroup(groupId) != null)
            {
                return groupId;
            }

            string trimmedText = menuText.Trim();
            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(trimmedText, CreateImageProcessingGroupText(group).Trim(), StringComparison.Ordinal))
                {
                    return group.Id;
                }
            }

            return null;
        }

        private static bool IsImageProcessingGroupMenuItem(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return false;
            }

            string trimmedText = menuText.Trim();
            return trimmedText.StartsWith("群組(", StringComparison.Ordinal) &&
                trimmedText.EndsWith(")", StringComparison.Ordinal);
        }

        private int GetImageProcessingStepIndex(string stepText)
        {
            string stepId;
            if (!string.IsNullOrWhiteSpace(stepText) &&
                visibleImageProcessingStepIds.TryGetValue(stepText, out stepId))
            {
                int mappedIndex = systemParameters.ImageProcessingSteps.FindIndex(
                    step => string.Equals(step.Id, stepId, StringComparison.Ordinal));
                if (mappedIndex >= 0)
                {
                    return mappedIndex;
                }
            }

            if (!IsImageProcessingStepMenuItem(stepText))
            {
                return -1;
            }

            string trimmedText = stepText.Trim();
            int prefixLength = "處理".Length;
            int suffixIndex = trimmedText.IndexOf('(');
            if (suffixIndex <= prefixLength)
            {
                return -1;
            }

            int stepNumber;
            return int.TryParse(trimmedText.Substring(prefixLength, suffixIndex - prefixLength), out stepNumber)
                ? stepNumber - 1
                : -1;
        }

        private string RegisterVisibleImageProcessingGroup(
            ImageProcessingGroupSettings group,
            int depth)
        {
            string text = CreateImageProcessingGroupText(group, depth);
            if (group != null && !string.IsNullOrWhiteSpace(group.Id))
            {
                visibleImageProcessingGroupIds[text] = group.Id;
            }

            return text;
        }

        private string CreateImageProcessingStepText(int stepNumber)
        {
            return CreateImageProcessingStepText(stepNumber, false);
        }

        private string CreateImageProcessingStepText(int stepNumber, bool grouped)
        {
            return CreateImageProcessingStepText(stepNumber, grouped ? 1 : 0);
        }

        private string CreateImageProcessingStepText(int stepNumber, int depth)
        {
            string displayName = string.Empty;
            int stepIndex = stepNumber - 1;
            if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
                displayName = string.IsNullOrWhiteSpace(step.DisplayName) ? step.Method : step.DisplayName;
            }

            string stepName = string.IsNullOrWhiteSpace(displayName) ? "未決定" : displayName;
            return new string(' ', 4 + (Math.Max(0, depth) * 2)) + "處理" + stepNumber + "(" + stepName + ")";
        }

        private void RenameImageProcessingStep(string stepText)
        {
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            string name;
            if (!TryGetImageProcessingStepName(step.DisplayName, out name))
            {
                return;
            }

            step.DisplayName = name;
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            string renamedStepText = CreateImageProcessingStepText(stepIndex + 1);
            functionListBox.SelectedItem = renamedStepText;
            statusLabel.Text = "已命名" + renamedStepText.Trim();
        }

        private bool TryGetImageProcessingStepName(string currentName, out string name)
        {
            name = currentName ?? string.Empty;
            using (var dialog = new Form())
            using (var nameBox = new TextBox())
            using (var confirmButton = new Button())
            using (var cancelButton = new Button())
            using (var prompt = new Label())
            {
                dialog.Text = "命名影像處理";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 116);

                prompt.Text = "處理名稱";
                prompt.Location = new Point(14, 16);
                prompt.AutoSize = true;
                nameBox.Location = new Point(14, 38);
                nameBox.Size = new Size(332, 23);
                nameBox.Text = name;
                nameBox.SelectAll();
                confirmButton.Text = "確定";
                confirmButton.DialogResult = DialogResult.OK;
                confirmButton.Location = new Point(190, 76);
                cancelButton.Text = "取消";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(271, 76);
                dialog.AcceptButton = confirmButton;
                dialog.CancelButton = cancelButton;
                dialog.Controls.Add(prompt);
                dialog.Controls.Add(nameBox);
                dialog.Controls.Add(confirmButton);
                dialog.Controls.Add(cancelButton);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                name = nameBox.Text.Trim();
                return true;
            }
        }

        private void ShowImageProcessingFlowTree(string stepText)
        {
            selectedImageProcessingGroupId = null;
            selectedImageProcessingStepIndex = GetImageProcessingStepIndex(stepText);
            if (selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                HideImageProcessingFlowTree();
                return;
            }

            EnsureImageProcessingFlowTreeView();
            parameterPlaceholderLabel.Visible = false;

            string method = systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method;
            if (IsBinaryMaskProcessingMethod(method))
            {
                ShowImageProcessingParameterPanel(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 參數";
            }
            else
            {
                HideImageProcessingParameterPanel();
                imageProcessingFlowTreeView.Visible = true;
                imageProcessingFlowTreeView.BringToFront();
                SelectImageProcessingFlowNode(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 流程";
            }

            statusLabel.Text = "目前選擇：" + stepText.Trim();
        }

        private void ShowImageProcessingGroup(string groupText)
        {
            string groupId = GetImageProcessingGroupId(groupText);
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = groupId;
            HideImageProcessingParameterPanel();
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
            }

            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.Text = "群組結果會疊加顯示群組內每一個處理對原圖的結果。";
            parameterPlaceholderLabel.BringToFront();
            rightPanelTitleLabel.Text = groupText.Trim() + " 結果";
            statusLabel.Text = "目前選擇：" + groupText.Trim();
        }

        private void HideImageProcessingFlowTree()
        {
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            selectedImagePreprocessingStepIndex = -1;
            selectedImagePreprocessingGroupId = null;
            parameterPlaceholderLabel.Visible = true;
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
            }

            if (imagePreprocessingFlowTreeView != null)
            {
                imagePreprocessingFlowTreeView.Visible = false;
            }

            HideImageProcessingParameterPanel();
        }

        private void EnsureImageProcessingFlowTreeView()
        {
            if (imageProcessingFlowTreeView != null)
            {
                return;
            }

            imageProcessingFlowTreeView = new TreeView();
            imageProcessingFlowTreeView.BorderStyle = BorderStyle.None;
            imageProcessingFlowTreeView.Dock = DockStyle.Fill;
            imageProcessingFlowTreeView.FullRowSelect = true;
            imageProcessingFlowTreeView.HideSelection = false;
            imageProcessingFlowTreeView.BackColor = parameterPanel.BackColor;
            imageProcessingFlowTreeView.ForeColor = Color.FromArgb(39, 46, 56);
            imageProcessingFlowTreeView.AfterSelect += ImageProcessingFlowTreeView_AfterSelect;
            parameterPanel.Controls.Add(imageProcessingFlowTreeView);
            imageProcessingFlowTreeView.BringToFront();

            AddImageProcessingFlowNode("Edge Detection", "Polarity Edge", "Canny Edge", "Sobel Edge");
            AddImageProcessingFlowNode("Threshold", "Global Threshold", "Adaptive Threshold", "Otsu Threshold");
            imageProcessingFlowTreeView.ExpandAll();
        }

        private void AddImageProcessingFlowNode(string category, params string[] methods)
        {
            TreeNode categoryNode = imageProcessingFlowTreeView.Nodes.Add(category);
            categoryNode.Tag = category;
            foreach (string method in methods)
            {
                TreeNode methodNode = categoryNode.Nodes.Add(method);
                methodNode.Tag = method;
            }
        }

        private void SelectImageProcessingFlowNode(string method)
        {
            isUpdatingImageProcessingFlowTree = true;
            try
            {
                imageProcessingFlowTreeView.SelectedNode = FindImageProcessingFlowNode(method);
            }
            finally
            {
                isUpdatingImageProcessingFlowTree = false;
            }
        }

        private TreeNode FindImageProcessingFlowNode(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                return null;
            }

            foreach (TreeNode node in imageProcessingFlowTreeView.Nodes)
            {
                TreeNode foundNode = FindImageProcessingFlowNode(node, method);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        private static TreeNode FindImageProcessingFlowNode(TreeNode node, string method)
        {
            if (string.Equals(node.Tag as string, method, StringComparison.Ordinal))
            {
                return node;
            }

            foreach (TreeNode childNode in node.Nodes)
            {
                TreeNode foundNode = FindImageProcessingFlowNode(childNode, method);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        private void ImageProcessingFlowTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (isUpdatingImageProcessingFlowTree ||
                selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            string method = e.Node.Tag as string;
            if (e.Node.Nodes.Count > 0)
            {
                return;
            }

            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method = method;
            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters = CreateDefaultImageProcessingParameters(method);
            SaveSystemParameters();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            UpdateVisibleImageProcessingStepText(selectedImageProcessingStepIndex);
            if (IsBinaryMaskProcessingMethod(method))
            {
                ShowImageProcessingParameterPanel(method);
                rightPanelTitleLabel.Text = "處理" + (selectedImageProcessingStepIndex + 1) + "(" + method + ") 參數";
            }

            statusLabel.Text = "已設定處理" + (selectedImageProcessingStepIndex + 1) + "：" + method;
        }

        private void UpdateVisibleImageProcessingStepText(int stepIndex)
        {
            RefreshVisibleImageProcessingStepText(stepIndex, true);
        }

        private void RefreshVisibleImageProcessingStepText(int stepIndex)
        {
            RefreshVisibleImageProcessingStepText(stepIndex, false);
        }

        private void RefreshVisibleImageProcessingStepText(int stepIndex, bool restoreSelection)
        {
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                string itemText = functionListBox.Items[itemIndex] as string;
                if (GetImageProcessingStepIndex(itemText) == stepIndex)
                {
                    int leadingSpaces = itemText.Length - itemText.TrimStart().Length;
                    int depth = Math.Max(0, (leadingSpaces - 4) / 2);
                    ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
                    visibleImageProcessingStepIds.Remove(itemText);
                    string updatedText = RegisterVisibleImageProcessingStep(step, stepIndex + 1, depth);
                    functionListBox.Items[itemIndex] = updatedText;
                    if (restoreSelection)
                    {
                        functionListBox.SelectedIndex = itemIndex;
                    }

                    break;
                }
            }
        }

        private void ShowImageProcessingParameterPanel(string method)
        {
            EnsureImageProcessingParameterPanel();
            pendingImageProcessingParameters = ParseImageProcessingParameters(GetSelectedImageProcessingStepParameters());
            imageProcessingFlowTreeView.Visible = false;
            parameterPlaceholderLabel.Visible = false;
            imageProcessingParameterPanel.Visible = true;
            imageProcessingParameterPanel.Controls.Clear();
            imageProcessingParameterPanel.BringToFront();

            isLoadingImageProcessingParameters = true;
            try
            {
                AddParameterHeader(method);
                AddReselectMethodButton();

                if (method == "Polarity Edge")
                {
                    AddComboParameter("Polarity", "邊緣方向", new[] { "BrightToDark", "DarkToBright", "Any" }, "Any");
                    AddNumericParameter("ContrastThreshold", "對比門檻", "20");
                    AddComboParameter("CoreWidth", "核心大小", new[] { "3", "5", "7" }, "3");
                    AddNumericParameter("Smoothing", "平滑", "1");
                    AddNumericParameter("GaussianSigma", "Gaussian Sigma", "0.5");
                    AddComboParameter("SearchDirection", "灰階變化方向", new[] { "X", "Y", "Any" }, "Any");
                    AddComboParameter("BorderType", "ROI 邊界處理", new[] { "Reflect", "Replicate", "Constant" }, "Reflect");
                }
                else if (method == "Canny Edge")
                {
                    AddNumericParameter("LowThreshold", "低門檻", "50");
                    AddNumericParameter("HighThreshold", "高門檻", "150");
                    AddComboParameter("KernelSize", "核心大小", CannyKernelSizeOptions, "3");
                    AddCheckParameter("L2Gradient", "L2 梯度", false);
                    AddComboParameter("GaussianBlurSize", "高斯模糊大小", KernelSizeOptions, "5");
                    AddNumericParameter("GaussianSigma", "高斯 Sigma", "1.4");
                }
                else if (method == "Sobel Edge")
                {
                    AddComboParameter("Direction", "灰階變化方向", new[] { "X", "Y", "Any" }, "Any");
                    AddComboParameter("KernelSize", "核心大小", SobelKernelSizeOptions, "3");
                    AddNumericParameter("Threshold", "邊緣門檻", "30");
                }
                else if (method == "Global Threshold")
                {
                    AddComboParameter("ThresholdMode", "門檻模式", new[] { "Single", "Range" }, "Single");
                    AddNumericParameter("Threshold", "門檻值", "128");
                    AddNumericParameter("LowerThreshold", "範圍下限", "0");
                    AddNumericParameter("UpperThreshold", "範圍上限", "255");
                    AddNumericParameter("MaxValue", "輸出最大值", "255");
                    AddComboParameter("ThresholdType", "二值化方向", new[] { "Binary", "BinaryInv" }, "Binary");
                    SetGlobalThresholdParameterVisibility(
                        GetImageProcessingParameterValue("ThresholdMode", "Single"));
                }
                else if (method == "Adaptive Threshold")
                {
                    AddNumericParameter("MaxValue", "輸出最大值", "255");
                    AddComboParameter("AdaptiveMethod", "自適應方法", new[] { "MeanC", "GaussianC" }, "GaussianC");
                    AddComboParameter("ThresholdType", "二值化方向", new[] { "Binary", "BinaryInv" }, "Binary");
                    AddNumericParameter("BlockSize", "區塊大小", "11");
                    AddNumericParameter("C", "常數 C", "2");
                }
                else if (method == "Otsu Threshold")
                {
                    AddNumericParameter("MaxValue", "輸出最大值", "255");
                    AddComboParameter("ThresholdType", "二值化方向", new[] { "Binary", "BinaryInv" }, "Binary");
                }

                AddParameterEditButtons(false);
            }
            finally
            {
                isLoadingImageProcessingParameters = false;
            }
        }

        private void EnsureImageProcessingParameterPanel()
        {
            if (imageProcessingParameterPanel != null)
            {
                return;
            }

            imageProcessingParameterPanel = new FlowLayoutPanel();
            imageProcessingParameterPanel.Dock = DockStyle.Fill;
            imageProcessingParameterPanel.FlowDirection = FlowDirection.TopDown;
            imageProcessingParameterPanel.WrapContents = false;
            imageProcessingParameterPanel.AutoScroll = true;
            imageProcessingParameterPanel.BackColor = parameterPanel.BackColor;
            parameterPanel.Controls.Add(imageProcessingParameterPanel);
            imageProcessingParameterPanel.BringToFront();
        }

        private void HideImageProcessingParameterPanel()
        {
            if (imageProcessingParameterPanel != null)
            {
                imageProcessingParameterPanel.Visible = false;
            }
        }

        private void AddParameterHeader(string method)
        {
            var label = new Label();
            label.AutoSize = false;
            label.Width = parameterPanel.Width - 12;
            label.Height = 32;
            label.Font = new Font(Font, FontStyle.Bold);
            label.Text = method;
            imageProcessingParameterPanel.Controls.Add(label);
        }

        private void AddReselectMethodButton()
        {
            var button = new Button();
            button.Width = parameterPanel.Width - 18;
            button.Height = 30;
            button.Text = "重新選擇方法";
            button.Click += delegate
            {
                HideImageProcessingParameterPanel();
                imageProcessingFlowTreeView.Visible = true;
                rightPanelTitleLabel.Text = "處理" + (selectedImageProcessingStepIndex + 1) + " 流程";
            };
            imageProcessingParameterPanel.Controls.Add(button);
        }

        private void AddNumericParameter(string key, string labelText, string defaultValue)
        {
            decimal defaultNumber = ParseDecimalOrDefault(defaultValue, 0);
            decimal value = ParseDecimalOrDefault(GetImageProcessingParameterValue(key, defaultValue), defaultNumber);
            decimal minimum;
            decimal maximum;
            decimal increment;
            GetNumericParameterRange(key, out minimum, out maximum, out increment);

            var label = CreateParameterLabel(labelText);
            label.Tag = key;
            var numericUpDown = new NumericUpDown();
            numericUpDown.Width = parameterPanel.Width - 18;
            numericUpDown.Minimum = minimum;
            numericUpDown.Maximum = maximum;
            numericUpDown.Increment = increment;
            numericUpDown.DecimalPlaces = key == "Scale" || key == "GaussianSigma" || key == "C" ? 2 : 0;
            numericUpDown.Value = Clamp(value, minimum, maximum);
            numericUpDown.Tag = key;
            numericUpDown.ValueChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    var control = (NumericUpDown)sender;
                    SetPendingImageProcessingParameter(control.Tag as string, control.Value.ToString(CultureInfo.InvariantCulture));
                }
            };
            numericUpDown.MouseWheel += NumericParameter_MouseWheel;

            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(numericUpDown);
        }

        private void AddComboParameter(string key, string labelText, string[] values, string defaultValue)
        {
            var label = CreateParameterLabel(labelText);
            label.Tag = key;
            var comboBox = new ComboBox();
            comboBox.Width = parameterPanel.Width - 18;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(values);
            comboBox.Tag = key;
            comboBox.SelectedItem = GetImageProcessingParameterValue(key, defaultValue);
            if (comboBox.SelectedIndex < 0)
            {
                comboBox.SelectedItem = defaultValue;
            }

            comboBox.SelectedIndexChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    SetPendingImageProcessingParameter(((ComboBox)sender).Tag as string, ((ComboBox)sender).SelectedItem as string);
                }
            };

            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(comboBox);

            if (key == "ThresholdMode")
            {
                comboBox.SelectedIndexChanged += delegate(object sender, EventArgs e)
                {
                    if (!isLoadingImageProcessingParameters)
                    {
                        SetGlobalThresholdParameterVisibility(
                            ((ComboBox)sender).SelectedItem as string);
                    }
                };
            }
        }

        private void SetGlobalThresholdParameterVisibility(string mode)
        {
            bool isRange = string.Equals(mode, "Range", StringComparison.OrdinalIgnoreCase);
            if (imageProcessingParameterPanel == null)
            {
                return;
            }

            foreach (Control control in imageProcessingParameterPanel.Controls)
            {
                string key = control.Tag as string;
                if (key == "Threshold")
                {
                    control.Visible = !isRange;
                }
                else if (key == "LowerThreshold" || key == "UpperThreshold")
                {
                    control.Visible = isRange;
                }
                else if (key == "ThresholdType")
                {
                    control.Visible = !isRange;
                }
            }
        }

        private string RegisterVisibleImageProcessingStep(
            ImageProcessingStepSettings step,
            int stepNumber,
            int depth)
        {
            string text = CreateImageProcessingStepText(stepNumber, depth);
            if (step != null && !string.IsNullOrWhiteSpace(step.Id))
            {
                visibleImageProcessingStepIds[text] = step.Id;
            }

            return text;
        }

        private void AddCheckParameter(string key, string labelText, bool defaultValue)
        {
            var checkBox = new CheckBox();
            checkBox.Width = parameterPanel.Width - 18;
            checkBox.Height = 28;
            checkBox.Text = labelText;
            checkBox.Tag = key;
            checkBox.Checked = string.Equals(GetImageProcessingParameterValue(key, defaultValue ? "true" : "false"), "true", StringComparison.OrdinalIgnoreCase);
            checkBox.CheckedChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    SetPendingImageProcessingParameter(((CheckBox)sender).Tag as string, ((CheckBox)sender).Checked ? "true" : "false");
                }
            };
            imageProcessingParameterPanel.Controls.Add(checkBox);
        }

        private void AddParameterEditButtons(bool preprocessing)
        {
            var apply = new Button();
            apply.Width = parameterPanel.Width - 18;
            apply.Height = 30;
            apply.Text = "套用";
            apply.Margin = new Padding(3, 12, 3, 0);
            apply.Click += delegate
            {
                if (preprocessing)
                {
                    ApplyPendingImagePreprocessingParameters();
                }
                else
                {
                    ApplyPendingImageProcessingParameters();
                }
            };
            imageProcessingParameterPanel.Controls.Add(apply);

            var cancel = new Button();
            cancel.Width = parameterPanel.Width - 18;
            cancel.Height = 30;
            cancel.Text = "取消";
            cancel.Click += delegate
            {
                if (preprocessing)
                {
                    ImageProcessingStepSettings step;
                    if (TryGetSelectedImagePreprocessingStep(out step))
                    {
                        ShowImagePreprocessingParameterPanel(step.Method);
                    }
                    else
                    {
                        HideImageProcessingParameterPanel();
                    }
                }
                else
                {
                    ShowImageProcessingParameterPanel(
                        systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method);
                }
            };
            imageProcessingParameterPanel.Controls.Add(cancel);

            parameterApplyStatusLabel = new Label();
            parameterApplyStatusLabel.Width = parameterPanel.Width - 18;
            parameterApplyStatusLabel.AutoSize = false;
            parameterApplyStatusLabel.Height = 52;
            parameterApplyStatusLabel.Margin = new Padding(3, 8, 3, 0);
            parameterApplyStatusLabel.Text = string.Empty;
            parameterApplyStatusLabel.ForeColor = Color.FromArgb(55, 64, 76);
            imageProcessingParameterPanel.Controls.Add(parameterApplyStatusLabel);
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

        private Label CreateParameterLabel(string text)
        {
            var label = new Label();
            label.AutoSize = false;
            label.Width = parameterPanel.Width - 18;
            label.Height = 22;
            label.Margin = new Padding(3, 8, 3, 0);
            label.Text = text;
            return label;
        }

        private void NumericParameter_MouseWheel(object sender, MouseEventArgs e)
        {
            var numericUpDown = sender as NumericUpDown;
            if (numericUpDown == null)
            {
                return;
            }

            ((HandledMouseEventArgs)e).Handled = true;
            decimal multiplier = (ModifierKeys & Keys.Shift) == Keys.Shift ? 10 : 1;
            decimal delta = e.Delta > 0 ? numericUpDown.Increment * multiplier : -numericUpDown.Increment * multiplier;
            numericUpDown.Value = Clamp(numericUpDown.Value + delta, numericUpDown.Minimum, numericUpDown.Maximum);
        }

        private static void GetNumericParameterRange(string key, out decimal minimum, out decimal maximum, out decimal increment)
        {
            minimum = 0;
            maximum = 99999;
            increment = 1;

            if (key == "ContrastThreshold" || key == "LowThreshold" || key == "HighThreshold" || key == "Threshold")
            {
                maximum = 255;
            }
            else if (key == "CoreWidth")
            {
                minimum = 1;
                maximum = 99;
            }
            else if (key == "Smoothing")
            {
                maximum = 31;
            }
            else if (key == "MaxGap")
            {
                maximum = 999;
            }
            else if (key == "Scale")
            {
                maximum = 100;
                increment = 0.1M;
            }
            else if (key == "GaussianSigma")
            {
                maximum = 100;
                increment = 0.1M;
            }
            else if (key == "MaxValue")
            {
                minimum = 1;
                maximum = 255;
            }
            else if (key == "BlockSize")
            {
                minimum = 3;
                maximum = 999;
            }
            else if (key == "C")
            {
                minimum = -255;
                maximum = 255;
                increment = 0.1M;
            }
            else if (key == "Delta")
            {
                minimum = -255;
                maximum = 255;
            }
        }

        private static decimal ParseDecimalOrDefault(string value, decimal defaultValue)
        {
            decimal parsedValue;
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
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

        private void MarkProcessedImageDirty()
        {
            // Capture the view that is actually on screen before processing
            // invalidates sources or replaces preview images. This keeps the
            // current zoom/pan stable even when processing starts from another
            // tab or switches between bitmap and large-image sources.
            CaptureSharedImageViewStateFromVisibleControls();
            CaptureProcessedImageViewState();
            processedImageDirty = true;
            imageProcessingStepElapsedMilliseconds.Clear();
            ClearLargeProcessedOverlayCache();
            ClearProcessedImageCache();
            if (latestProcessedImage != null)
            {
                latestProcessedImage.Dispose();
                latestProcessedImage = null;
            }
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
            lock (largeProcessedMaskLock)
            {
                foreach (Cv.Mat mask in largeProcessedBinaryMasks.Values)
                {
                    mask.Dispose();
                }

                largeProcessedBinaryMasks.Clear();
                largeProcessedMasks.Clear();
                largeProcessedMaskBuildKeys.Clear();
                largeProcessedMaskGeneration++;
            }
        }

        private void ClearLargeRoiGrayCache()
        {
            ClearLargeRelationSourceCache();
            lock (largeRoiGrayCacheLock)
            {
                if (largeOpenCvSourceGrayCache != null)
                {
                    largeOpenCvSourceGrayCache.Dispose();
                    largeOpenCvSourceGrayCache = null;
                }

                largeOpenCvSourceGrayCacheSource = null;
                if (largeRoiOpenCvGrayCache != null)
                {
                    largeRoiOpenCvGrayCache.Dispose();
                    largeRoiOpenCvGrayCache = null;
                }

                largeRoiGrayCacheSource = null;
                largeRoiGrayCacheRoi = Rectangle.Empty;
                largeRoiGrayCache = null;
            }
        }

        private byte[,] GetOrCreateLargeRoiGrayCache(LargeImageSource source, Rectangle roi)
        {
            lock (largeRoiGrayCacheLock)
            {
                if (ReferenceEquals(largeRoiGrayCacheSource, source) &&
                    largeRoiGrayCacheRoi.Equals(roi) &&
                    largeRoiGrayCache != null)
                {
                    return largeRoiGrayCache;
                }
            }

            byte[,] gray = source.CreateGrayRegionFromTiles(roi);
            lock (largeRoiGrayCacheLock)
            {
                largeRoiGrayCacheSource = source;
                largeRoiGrayCacheRoi = roi;
                largeRoiGrayCache = gray;
                return gray;
            }
        }

        private bool[,] CreateLargeEdgeMask(byte[,] gray, string method, Dictionary<string, string> parameters)
        {
            if (method == "Global Threshold")
            {
                return CreateOpenCvGlobalThresholdMask(
                    gray,
                    GetStringParameter(parameters, "ThresholdMode", "Single"),
                    GetIntParameter(parameters, "Threshold", 128),
                    GetIntParameter(parameters, "LowerThreshold", 0),
                    GetIntParameter(parameters, "UpperThreshold", 255),
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (method == "Adaptive Threshold")
            {
                return CreateOpenCvAdaptiveThresholdMask(
                    gray,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "AdaptiveMethod", "GaussianC"),
                    GetStringParameter(parameters, "ThresholdType", "Binary"),
                    GetIntParameter(parameters, "BlockSize", 11),
                    GetDoubleParameter(parameters, "C", 2));
            }

            if (method == "Otsu Threshold")
            {
                return CreateOpenCvOtsuThresholdMask(
                    gray,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (method == "Sobel Edge")
            {
                return CreateOpenCvSobelMask(
                    gray,
                    GetIntParameter(parameters, "Threshold", 30),
                    GetStringParameter(parameters, "Direction", "Both"),
                    GetIntParameter(parameters, "KernelSize", 3));
            }

            if (method == "Polarity Edge")
            {
                return CreateOpenCvPolarityMask(
                    gray,
                    GetIntParameter(parameters, "ContrastThreshold", 20),
                    GetPolarityCoreWidth(parameters),
                    GetIntParameter(parameters, "Smoothing", 1),
                    GetStringParameter(parameters, "Polarity", "Any"),
                    GetStringParameter(parameters, "SearchDirection", "Any"),
                    GetDoubleParameter(parameters, "GaussianSigma", 0.5),
                    GetStringParameter(parameters, "BorderType", "Reflect"));
            }

            int lowThreshold = GetIntParameter(parameters, "LowThreshold", 50);
            int highThreshold = GetIntParameter(parameters, "HighThreshold", 150);
            int kernelSize = GetIntParameter(parameters, "KernelSize", 3);
            bool l2Gradient = GetBoolParameter(parameters, "L2Gradient", false);
            int gaussianBlurSize = GetIntParameter(parameters, "GaussianBlurSize", 5);
            double gaussianSigma = GetDoubleParameter(parameters, "GaussianSigma", 1.4);
            string edgeSelection = GetStringParameter(parameters, "EdgeSelection", "All");
            int minEdgeLength = GetIntParameter(parameters, "MinEdgeLength", 10);
            int maxGap = GetIntParameter(parameters, "MaxGap", 2);

            return CreateOpenCvCannyMask(
                gray,
                lowThreshold,
                highThreshold,
                kernelSize,
                l2Gradient,
                gaussianBlurSize,
                gaussianSigma,
                edgeSelection,
                minEdgeLength,
                maxGap);
        }

        private static bool[,] CreateBoolMaskFromOpenCvMat(Cv.Mat mask)
        {
            int width = mask.Width;
            int height = mask.Height;
            var result = new bool[width, height];
            var row = new byte[width];
            long stride = mask.Step();
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(mask.Data + checked((int)(y * stride)), row, 0, width);
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = row[x] != 0;
                }
            }

            return result;
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

        private void CaptureProcessedImageViewState()
        {
            ImageDisplayControl source = null;
            if (isImageViewerMaximized)
            {
                ImageDisplayControl active = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (active == leftProcessedDisplayControl || active == rightProcessedDisplayControl)
                {
                    source = active;
                }
            }
            else if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
            {
                source = leftProcessedDisplayControl;
            }
            else if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
            {
                source = rightProcessedDisplayControl;
            }

            if (source == null || !source.HasImage)
            {
                return;
            }

            processedImageViewState = source.ViewState;
            hasProcessedImageViewState = true;
        }

        private void RestoreProcessedImageViewState()
        {
            if (!hasProcessedImageViewState) return;
            isSyncingImageView = true;
            try
            {
                ApplyImageViewState(leftProcessedDisplayControl, processedImageViewState);
                ApplyImageViewState(rightProcessedDisplayControl, processedImageViewState);
                if (isImageViewerMaximized)
                {
                    ImageDisplayControl active = isLeftImageViewerMaximized
                        ? GetVisibleLeftImageDisplayControl()
                        : GetVisibleRightImageDisplayControl();
                    if (active == leftProcessedDisplayControl || active == rightProcessedDisplayControl)
                    {
                        maximizedImageViewerViewState = processedImageViewState;
                        hasMaximizedImageViewerViewState = true;
                    }
                }
                else if (leftImageTabControl.SelectedTab == leftProcessedTabPage ||
                    rightImageTabControl.SelectedTab == rightProcessedTabPage)
                {
                    sharedImageViewState = processedImageViewState;
                    hasSharedImageViewState = true;
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private static void ApplyImageViewState(ImageDisplayControl display, ImageViewState viewState)
        {
            if (display != null && display.HasImage)
            {
                display.ApplyViewState(viewState);
            }
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
                statusLabel.Text = "影像處理運算中...";
                string cacheKey = CreateProcessedImageCacheKey();
                Bitmap cachedImage;
                Bitmap processedImage;
                if (processedImageCache.TryGetValue(cacheKey, out cachedImage))
                {
                    processedImage = new Bitmap(cachedImage);
                }
                else
                {
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
            if (latestProcessedImage == null)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
                {
                    leftProcessedDisplayControl.SetDisplayImage(new Bitmap(latestProcessedImage), true);
                    Rectangle? roi = GetSelectedRoi();
                    if (roi.HasValue)
                    {
                        leftProcessedDisplayControl.SetRoiOverlay(roi.Value);
                    }
                }

                if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
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
        }

        private void PrepareLargeProcessedPreview()
        {
            if (string.IsNullOrWhiteSpace(systemParameters.LastImagePath) || !File.Exists(systemParameters.LastImagePath))
            {
                return;
            }

            List<ImageProcessingStepSettings> selectedSteps = GetSelectedImageProcessingSteps();
            if (systemParameters.RoiRegions.Count == 0 || !HasSelectedPreviewableImageProcessingSteps())
            {
                ClearProcessedPreviewImages();
                return;
            }

            if (!string.IsNullOrWhiteSpace(activeImageRelationGroupId))
            {
                PrepareLargeRelationGroupPreview();
                return;
            }

            if (ShouldUseRelationPreprocessedSource() && HasConfiguredImagePreprocessingSteps() && preprocessedImageDirty)
            {
                RequestPreprocessedImageUpdate();
                return;
            }

            isSyncingImageView = true;
            LargeImageSource sharedSource = null;
            LargeImageSource processingSource = null;
            try
            {
                sharedSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (sharedSource == null)
                {
                    ClearProcessedPreviewImages();
                    return;
                }

                processingSource = GetLargeImageProcessingSource(sharedSource);
                LargeImageSource processedDisplaySource =
                    string.Equals(activeImageRelationSourceType, "Original", StringComparison.Ordinal)
                        ? sharedSource
                        : processingSource;

                leftProcessedDisplayControl.SetSharedLargeImageSource(processedDisplaySource, true);

                rightProcessedDisplayControl.SetSharedLargeImageSource(processedDisplaySource, true);

                Rectangle? selectedRoi = GetSelectedRoi();
                if (selectedRoi.HasValue)
                {
                    leftProcessedDisplayControl.SetRoiOverlay(selectedRoi.Value);
                    rightProcessedDisplayControl.SetRoiOverlay(selectedRoi.Value);
                }

                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    foreach (ImageProcessingStepSettings step in selectedSteps)
                    {
                        StartLargeProcessedMaskBuild(processingSource, roiRegion.Bounds, step);
                    }
                }
            }
            finally
            {
                if (sharedSource != null)
                {
                    sharedSource.ReleaseReference();
                }

                if (processingSource != null)
                {
                    processingSource.ReleaseReference();
                }

                isSyncingImageView = false;
            }

            // A selected step may already have a completed mask in memory.
            // StartLargeProcessedMaskBuild then correctly returns immediately,
            // so explicitly refresh the processed viewers to display that cache.
            leftProcessedDisplayControl.ScheduleImageViewRefresh();
            rightProcessedDisplayControl.ScheduleImageViewRefresh();
            ApplySharedImageViewStateToVisibleControls();
            RestoreProcessedImageViewState();
            RestorePreprocessedImageViewState();
        }

        private LargeImageSource GetLargeImageProcessingSource(LargeImageSource originalSource)
        {
            if (!string.Equals(activeImageRelationSourceType, "Step", StringComparison.Ordinal) &&
                !string.Equals(activeImageRelationSourceType, "Group", StringComparison.Ordinal))
            {
                return originalSource.AddReference();
            }

            lock (largePreprocessedImageLock)
            {
                if (largePreprocessedImageSource != null && !preprocessedImageDirty)
                {
                    return largePreprocessedImageSource.AddReference();
                }
            }

            return originalSource.AddReference();
        }

        private void StartLargeProcessedMaskBuild(LargeImageSource source, Rectangle roi, ImageProcessingStepSettings step)
        {
            if (source == null || roi.Width <= 0 || roi.Height <= 0 || step == null || !IsBinaryMaskProcessingMethod(step.Method))
            {
                return;
            }

            string maskKey = CreateLargeProcessedMaskKey(roi, step);
            int generation;
            lock (largeProcessedMaskLock)
            {
                if (largeProcessedMasks.ContainsKey(maskKey) ||
                    largeProcessedBinaryMasks.ContainsKey(maskKey) ||
                    largeProcessedMaskBuildKeys.Contains(maskKey))
                {
                    return;
                }

                generation = largeProcessedMaskGeneration;
                largeProcessedMaskBuildKeys.Add(maskKey);
            }

            string method = step.Method;
            string parameters = step.Parameters;
            LargeImageSource sharedSource = source.AddReference();
            statusLabel.Text = CanUseNativeEdgeMask(method, null)
                ? "影像處理運算中...使用 OpenCV " + method
                : "影像處理運算中...";

            Task.Run(
                delegate
                {
                    bool[,] mask = null;
                    try
                    {
                        Dictionary<string, string> parsedParameters = ParseImageProcessingParameters(parameters);

                        // Native edge detectors always receive the entire ROI as one
                        // OpenCV Mat. Tiles exist only for display, never as separate
                        // Canny/Polarity/Sobel calculations.
                        if (CanUseNativeEdgeMask(method, parsedParameters))
                        {
                            largeNativeProcessingGate.Wait();
                            try
                            {
                                if (!IsLargeProcessedMaskBuildCurrent(maskKey, generation))
                                {
                                    return;
                                }

                                BeginInvoke(
                                    new Action(
                                        delegate
                                        {
                                            statusLabel.Text = "影像處理運算中...OpenCV 原圖準備";
                                        }));
                                using (Cv.Mat nativeGray = GetOrCreateLargeRoiOpenCvGrayCache(sharedSource, roi))
                                {
                                    if (!IsLargeProcessedMaskBuildCurrent(maskKey, generation))
                                    {
                                        return;
                                    }

                                    BeginInvoke(
                                        new Action(
                                            delegate
                                            {
                                                statusLabel.Text = "影像處理運算中...使用 OpenCV " + method;
                                    }));
                                    Stopwatch imageProcessingStopwatch = Stopwatch.StartNew();
                                    Cv.Mat binaryMask = null;
                                    List<ImageProcessingStepSettings> executionChain =
                                        GetImageProcessingExecutionChain(step);
                                    binaryMask = CreateCombinedImageProcessingGroupMask(
                                        nativeGray,
                                        executionChain);
                                    PublishCompletedLargeProcessedBinaryMask(
                                        binaryMask,
                                        roi,
                                        step,
                                        maskKey,
                                        generation,
                                        imageProcessingStopwatch.ElapsedMilliseconds);
                                    binaryMask = null;
                                }
                            }
                            finally
                            {
                                largeNativeProcessingGate.Release();
                            }

                            return;
                        }

                        if ((long)roi.Width * roi.Height <= MaxSinglePassLargeRoiPixels)
                        {
                            byte[,] gray = GetOrCreateLargeRoiGrayCache(sharedSource, roi);
                            if (!IsLargeProcessedMaskBuildCurrent(maskKey, generation))
                            {
                                return;
                            }

                            mask = CreateLargeEdgeMask(gray, method, parsedParameters);
                            PublishCompletedLargeProcessedMask(mask, roi, maskKey, generation);
                            mask = null;
                            return;
                        }

                        mask = new bool[roi.Width, roi.Height];
                        int padding = GetLargeProcessedChunkPadding(method, parsedParameters);
                        int chunkCountX = (roi.Width + LargeProcessedMaskChunkSize - 1) / LargeProcessedMaskChunkSize;
                        int chunkCountY = (roi.Height + LargeProcessedMaskChunkSize - 1) / LargeProcessedMaskChunkSize;
                        int totalChunks = chunkCountX * chunkCountY;
                        int completedChunks = 0;

                        for (int chunkY = 0; chunkY < chunkCountY; chunkY++)
                        {
                            for (int chunkX = 0; chunkX < chunkCountX; chunkX++)
                            {
                                lock (largeProcessedMaskLock)
                                {
                                    if (generation != largeProcessedMaskGeneration ||
                                        !largeProcessedMaskBuildKeys.Contains(maskKey))
                                    {
                                        return;
                                    }
                                }

                                Rectangle chunkRect = Rectangle.Intersect(
                                    roi,
                                    new Rectangle(
                                        roi.X + (chunkX * LargeProcessedMaskChunkSize),
                                        roi.Y + (chunkY * LargeProcessedMaskChunkSize),
                                        LargeProcessedMaskChunkSize,
                                        LargeProcessedMaskChunkSize));
                                if (chunkRect.Width <= 0 || chunkRect.Height <= 0)
                                {
                                    continue;
                                }

                                Rectangle paddedChunkRect = Rectangle.Intersect(
                                    new Rectangle(0, 0, sharedSource.Width, sharedSource.Height),
                                    Rectangle.FromLTRB(
                                        chunkRect.Left - padding,
                                        chunkRect.Top - padding,
                                        chunkRect.Right + padding,
                                        chunkRect.Bottom + padding));
                                Bitmap chunkImage = null;
                                try
                                {
                                    chunkImage = sharedSource.CreateRegionBitmapFromTiles(paddedChunkRect);
                                }
                                catch (Exception ex)
                                {
                                    if (!(ex is OutOfMemoryException) && !(ex is ArgumentException))
                                    {
                                        throw;
                                    }

                                    Debug.WriteLine(ex);
                                    TryCreateRegionBitmapFromPreview(sharedSource, paddedChunkRect, out chunkImage);
                                }

                                if (chunkImage == null)
                                {
                                    throw new InvalidOperationException("無法建立 ROI 區塊影像");
                                }

                                using (chunkImage)
                                {
                                    bool[,] chunkMask = CreateEdgeMask(chunkImage, method, parsedParameters);
                                    CopyChunkMaskToRoiMask(mask, roi, paddedChunkRect, chunkRect, chunkMask);
                                }

                                completedChunks++;
                                if (completedChunks == 1 || completedChunks == totalChunks || completedChunks % 8 == 0)
                                {
                                    int progress = completedChunks;
                                    bool publishPartialMask = completedChunks == 1 || completedChunks % 8 == 0;
                                    BeginInvoke(
                                        new Action(
                                            delegate
                                            {
                                                lock (largeProcessedMaskLock)
                                                {
                                                    if (generation != largeProcessedMaskGeneration ||
                                                        !largeProcessedMaskBuildKeys.Contains(maskKey))
                                                    {
                                                        return;
                                                    }

                                                    // Publish completed chunks immediately. The paint path only reads
                                                    // the already-written cells, so large ROIs become visible while
                                                    // the remaining chunks continue in the background.
                                                    if (publishPartialMask)
                                                    {
                                                        largeProcessedMasks[maskKey] = mask;
                                                    }
                                                }

                                                ReportBackgroundStatus("影像處理運算中...ROI Mask " +
                                                    progress.ToString(CultureInfo.InvariantCulture) + "/" +
                                                    totalChunks.ToString(CultureInfo.InvariantCulture));
                                                if (publishPartialMask)
                                                {
                                                    leftProcessedDisplayControl.InvalidateImageView();
                                                    rightProcessedDisplayControl.InvalidateImageView();
                                                    InvalidateBlockProcessingDisplays();
                                                }
                                            }));
                                }
                            }
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    lock (largeProcessedMaskLock)
                                    {
                                        if (generation != largeProcessedMaskGeneration ||
                                            !largeProcessedMaskBuildKeys.Contains(maskKey))
                                        {
                                            return;
                                        }

                                        largeProcessedMasks[maskKey] = mask;
                                        largeProcessedMaskBuildKeys.Remove(maskKey);
                                        mask = null;
                                    }

                                    statusLabel.Text = "大圖 ROI Mask 建立完成";
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                    InvalidateBlockProcessingDisplays();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    lock (largeProcessedMaskLock)
                                    {
                                        if (generation == largeProcessedMaskGeneration)
                                        {
                                            largeProcessedMaskBuildKeys.Remove(maskKey);
                                        }
                                    }

                                    statusLabel.Text = "大圖 ROI Mask 建立失敗：" + ex.Message;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                    InvalidateBlockProcessingDisplays();
                                }));
                    }
                    finally
                    {
                        mask = null;
                        sharedSource.ReleaseReference();
                    }
                });
        }

        private void PublishCompletedLargeProcessedMask(bool[,] mask, Rectangle roi, string maskKey, int generation)
        {
            BeginInvoke(
                new Action(
                    delegate
                    {
                        lock (largeProcessedMaskLock)
                        {
                            if (generation != largeProcessedMaskGeneration ||
                                !largeProcessedMaskBuildKeys.Contains(maskKey))
                            {
                                return;
                            }

                            largeProcessedMasks[maskKey] = mask;
                            largeProcessedMaskBuildKeys.Remove(maskKey);
                        }

                        statusLabel.Text = "大圖 ROI Mask 建立完成";
                        leftProcessedDisplayControl.InvalidateImageView();
                        rightProcessedDisplayControl.InvalidateImageView();
                        InvalidateBlockProcessingDisplays();
                    }));
        }

        private string CreateLargeProcessedMaskKey(Rectangle roi, ImageProcessingStepSettings step)
        {
            string parameters = step.Parameters;
            if (string.Equals(step.Method, "Canny Edge", StringComparison.OrdinalIgnoreCase))
            {
                // Only these values affect the raw OpenCV Canny result. Older
                // profiles may still contain selection/length/gap values; they
                // must not force a duplicate full-ROI calculation.
                Dictionary<string, string> parsed = ParseImageProcessingParameters(parameters);
                parameters = string.Join(
                    ";",
                    "LowThreshold=" + GetIntParameter(parsed, "LowThreshold", 50).ToString(CultureInfo.InvariantCulture),
                    "HighThreshold=" + GetIntParameter(parsed, "HighThreshold", 150).ToString(CultureInfo.InvariantCulture),
                    "KernelSize=" + NormalizeCannyKernelSize(GetIntParameter(parsed, "KernelSize", 3)).ToString(CultureInfo.InvariantCulture),
                    "L2Gradient=" + GetBoolParameter(parsed, "L2Gradient", false).ToString(),
                    "GaussianBlurSize=" + GetIntParameter(parsed, "GaussianBlurSize", 5).ToString(CultureInfo.InvariantCulture),
                    "GaussianSigma=" + GetDoubleParameter(parsed, "GaussianSigma", 1.4).ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(
                "|",
                step.Method,
                parameters,
                roi.X.ToString(CultureInfo.InvariantCulture),
                roi.Y.ToString(CultureInfo.InvariantCulture),
                roi.Width.ToString(CultureInfo.InvariantCulture),
                roi.Height.ToString(CultureInfo.InvariantCulture));
        }

        private static int GetLargeProcessedChunkPadding(string method, Dictionary<string, string> parameters)
        {
            if (method == "Canny Edge")
            {
                int gaussianBlurSize = EnsureOdd(GetIntParameter(parameters, "GaussianBlurSize", 5));
                int kernelSize = EnsureOdd(GetIntParameter(parameters, "KernelSize", 3));
                int maxGap = GetIntParameter(parameters, "MaxGap", 2);
                return Math.Max(4, (gaussianBlurSize / 2) + (kernelSize / 2) + maxGap + 4);
            }

            if (method == "Sobel Edge")
            {
                int kernelSize = EnsureOdd(GetIntParameter(parameters, "KernelSize", 3));
                int maxGap = GetIntParameter(parameters, "MaxGap", 2);
                return Math.Max(4, (kernelSize / 2) + maxGap + 4);
            }

            int edgeWidth = EnsureOdd(GetPolarityCoreWidth(parameters));
            int smoothing = EnsureOdd(GetIntParameter(parameters, "Smoothing", 1));
            int polarityMaxGap = GetIntParameter(parameters, "MaxGap", 2);
            return Math.Max(4, (edgeWidth / 2) + (smoothing / 2) + polarityMaxGap + 4);
        }

        private static int EnsureOdd(int value)
        {
            int normalized = Math.Max(1, value);
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }

        private static void CopyChunkMaskToRoiMask(bool[,] roiMask, Rectangle roi, Rectangle processedRect, Rectangle chunkRect, bool[,] chunkMask)
        {
            int chunkWidth = Math.Min(chunkRect.Width, chunkMask.GetLength(0));
            int chunkHeight = Math.Min(chunkRect.Height, chunkMask.GetLength(1));
            int offsetX = chunkRect.X - roi.X;
            int offsetY = chunkRect.Y - roi.Y;
            int maskOffsetX = chunkRect.X - processedRect.X;
            int maskOffsetY = chunkRect.Y - processedRect.Y;
            int roiMaskWidth = roiMask.GetLength(0);
            int roiMaskHeight = roiMask.GetLength(1);
            int chunkMaskWidth = chunkMask.GetLength(0);
            int chunkMaskHeight = chunkMask.GetLength(1);

            for (int y = 0; y < chunkHeight; y++)
            {
                int targetY = offsetY + y;
                if (targetY < 0 || targetY >= roiMaskHeight)
                {
                    continue;
                }

                for (int x = 0; x < chunkWidth; x++)
                {
                    int targetX = offsetX + x;
                    int sourceX = maskOffsetX + x;
                    int sourceY = maskOffsetY + y;
                    if (targetX >= 0 &&
                        targetX < roiMaskWidth &&
                        sourceX >= 0 &&
                        sourceX < chunkMaskWidth &&
                        sourceY >= 0 &&
                        sourceY < chunkMaskHeight)
                    {
                        roiMask[targetX, targetY] = chunkMask[sourceX, sourceY];
                    }
                }
            }
        }

        private void ProcessedDisplayControl_LargeImageOverlayPaint(object sender, LargeImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            bool isPanning = display != null && display.IsPanning;

            if (!string.IsNullOrWhiteSpace(activeImageRelationGroupId))
            {
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    PaintLargeProcessedRelationGroupOverlayForRoi(e, roiRegion.Bounds, isPanning);
                }

                return;
            }

            List<ImageProcessingStepSettings> selectedSteps = GetSelectedImageProcessingSteps();
            if (selectedSteps.Count == 0)
            {
                return;
            }

            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                foreach (ImageProcessingStepSettings step in selectedSteps)
                {
                    if (IsBinaryMaskProcessingMethod(step.Method))
                    {
                        PaintLargeProcessedOverlayForRoi(e, roiRegion.Bounds, step, isPanning);
                    }
                }
            }
        }

        private void PaintLargeProcessedOverlayForRoi(
            LargeImageOverlayPaintEventArgs e, Rectangle roi, ImageProcessingStepSettings step, bool isPanning)
        {
            Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, roi);
            if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
            {
                return;
            }

            bool[,] mask;
            Cv.Mat binaryMask;
            bool isBuilding;
            string maskKey = CreateLargeProcessedMaskKey(roi, step);
            lock (largeProcessedMaskLock)
            {
                largeProcessedMasks.TryGetValue(maskKey, out mask);
                largeProcessedBinaryMasks.TryGetValue(maskKey, out binaryMask);
                isBuilding = largeProcessedMaskBuildKeys.Contains(maskKey);
            }

            if (mask == null && binaryMask == null)
            {
                if (!isBuilding)
                {
                    StartLargeProcessedMaskBuild(e.Source, roi, step);
                }

                return;
            }

            if (!IsAnyProcessedTabVisible() && !IsAnyBlockProcessingTabVisible())
            {
                return;
            }

            if (binaryMask != null)
            {
                if (isPanning)
                {
                    Bitmap overview;
                    if (TryGetLargeProcessedOverlayFromCache("overview|" + maskKey, out overview))
                    {
                        DrawLargeProcessedOverlayRegion(
                            e.Graphics,
                            overview,
                            roi,
                            visibleRoi,
                            e.Zoom,
                            e.Offset);
                    }

                    // Do not create a viewport-sized bitmap for every mouse move.
                    // The exact full-resolution overlay is rebuilt on the first
                    // repaint after panning stops.
                    return;
                }
                PaintLargeProcessedBinaryViewportOverlay(e, roi, visibleRoi, step, maskKey, binaryMask);
                return;
            }

            int startTileX = (visibleRoi.Left / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            int endTileX = ((visibleRoi.Right + LargeProcessedOverlayTileSize - 1) / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            int startTileY = (visibleRoi.Top / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            int endTileY = ((visibleRoi.Bottom + LargeProcessedOverlayTileSize - 1) / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            Point focusPoint = new Point(
                visibleRoi.Left + (visibleRoi.Width / 2),
                visibleRoi.Top + (visibleRoi.Height / 2));

            for (int tileY = startTileY; tileY < endTileY; tileY += LargeProcessedOverlayTileSize)
            {
                for (int tileX = startTileX; tileX < endTileX; tileX += LargeProcessedOverlayTileSize)
                {
                    Rectangle tileRect = Rectangle.Intersect(
                        roi,
                        new Rectangle(tileX, tileY, LargeProcessedOverlayTileSize, LargeProcessedOverlayTileSize));
                    if (tileRect.Width <= 0 || tileRect.Height <= 0)
                    {
                        continue;
                    }

                    Bitmap overlay;
                    string cacheKey = CreateLargeProcessedOverlayCacheKey(tileRect, roi, step);
                    if (TryGetLargeProcessedOverlayFromCache(cacheKey, out overlay))
                    {
                        DrawLargeProcessedOverlayTile(e.Graphics, overlay, tileRect, e.Zoom, e.Offset);
                    }
                    else
                    {
                        if (tileRect.Contains(focusPoint))
                        {
                            // One small tile is cheap to create and makes the current
                            // viewport show a result immediately. The remaining tiles
                            // are still generated off the UI thread.
                            overlay = binaryMask != null
                                ? CreateRedOverlayTileFromBinaryMask(binaryMask, roi, tileRect)
                                : CreateRedOverlayTileFromMask(mask, roi, tileRect);
                            TrimLargeProcessedOverlayCache();
                            largeProcessedOverlayCache[cacheKey] = overlay;
                            DrawLargeProcessedOverlayTile(e.Graphics, overlay, tileRect, e.Zoom, e.Offset);
                        }
                        else
                        {
                            if (binaryMask != null)
                            {
                                QueueLargeProcessedOverlayTileFromBinaryMask(binaryMask, roi, tileRect, cacheKey);
                            }
                            else
                            {
                                QueueLargeProcessedOverlayTileFromMask(mask, roi, tileRect, cacheKey);
                            }
                        }
                    }
                }
            }
        }

        private Cv.Mat GetOrCreateLargeRoiOpenCvGrayCache(LargeImageSource source, Rectangle roi)
        {
            if (!Environment.Is64BitProcess)
            {
                throw new InvalidOperationException(
                    "大圖 OpenCV ROI 處理必須以 64 位元執行。請重新建置目前的 x64 設定後再執行。");
            }

            if (source.IsMemoryBacked)
            {
                return source.CreateGrayscaleMatView(roi);
            }

            lock (largeRoiGrayCacheLock)
            {
                if (ReferenceEquals(largeOpenCvSourceGrayCacheSource, source) &&
                    largeOpenCvSourceGrayCache != null &&
                    !largeOpenCvSourceGrayCache.Empty())
                {
                    return CreateLargeRoiMatView(largeOpenCvSourceGrayCache, roi);
                }
            }

            // Decode outside the cache lock. Startup prewarming must not block
            // a later processing request while OpenCV reads a large source.
            Cv.Mat fullGray = null;
            try
            {
                fullGray = Cv.Cv2.ImRead(source.FilePath, Cv.ImreadModes.Grayscale);
                if (fullGray == null ||
                    fullGray.Empty() ||
                    fullGray.Width != source.Width ||
                    fullGray.Height != source.Height ||
                    fullGray.Type() != Cv.MatType.CV_8UC1)
                {
                    if (fullGray != null)
                    {
                        fullGray.Dispose();
                        fullGray = null;
                    }

                    // Some large LZW TIFF variants are readable by WIC but
                    // rejected by OpenCV's TIFF decoder. Keep the processing
                    // source full-resolution and 8-bit; do not fall back to
                    // the old ROI-tile analysis path.
                    fullGray = CreateFullGrayMatWithWic(source.FilePath, source.Width, source.Height);
                }

                lock (largeRoiGrayCacheLock)
                {
                    if (ReferenceEquals(largeOpenCvSourceGrayCacheSource, source) &&
                        largeOpenCvSourceGrayCache != null &&
                        !largeOpenCvSourceGrayCache.Empty())
                    {
                        fullGray.Dispose();
                        fullGray = null;
                        return CreateLargeRoiMatView(largeOpenCvSourceGrayCache, roi);
                    }

                    if (largeOpenCvSourceGrayCache != null)
                    {
                        largeOpenCvSourceGrayCache.Dispose();
                    }

                    largeOpenCvSourceGrayCache = fullGray;
                    largeOpenCvSourceGrayCacheSource = source;
                    fullGray = null;
                    return CreateLargeRoiMatView(largeOpenCvSourceGrayCache, roi);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OpenCV full-image decode failed: " + ex);
                throw new InvalidOperationException(
                    "OpenCV 無法建立完整灰階原圖，已停止處理，未使用舊的 ROI tile 備援流程。",
                    ex);
            }
            finally
            {
                if (fullGray != null)
                {
                    fullGray.Dispose();
                }
            }
        }

        private static Cv.Mat CreateFullGrayMatWithWic(string filePath, int expectedWidth, int expectedHeight)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                System.Windows.Media.Imaging.BitmapSource source = decoder.Frames[0];
                if (source.PixelWidth != expectedWidth || source.PixelHeight != expectedHeight)
                {
                    throw new InvalidOperationException("WIC 解碼後的影像尺寸與來源不一致。");
                }

                var gray = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                    source,
                    System.Windows.Media.PixelFormats.Gray8,
                    null,
                    0);
                int stride = gray.PixelWidth;
                byte[] row = new byte[stride];
                var result = new Cv.Mat(expectedHeight, expectedWidth, Cv.MatType.CV_8UC1);
                try
                {
                    for (int y = 0; y < expectedHeight; y++)
                    {
                        gray.CopyPixels(
                            new System.Windows.Int32Rect(0, y, expectedWidth, 1),
                            row,
                            stride,
                            0);
                        int rowOffset = checked((int)((long)y * result.Step()));
                        Marshal.Copy(row, 0, IntPtr.Add(result.Data, rowOffset), stride);
                    }

                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }

        private static Cv.Mat CreateLargeRoiMatView(Cv.Mat source, Rectangle roi)
        {
            return new Cv.Mat(source, new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height));
        }

        private void PaintLargeProcessedBinaryViewportOverlay(
            LargeImageOverlayPaintEventArgs e,
            Rectangle roi,
            Rectangle visibleRoi,
            ImageProcessingStepSettings step,
            string maskKey,
            Cv.Mat binaryMask)
        {
            int targetWidth = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(visibleRoi.Width * e.Zoom)));
            int targetHeight = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(visibleRoi.Height * e.Zoom)));
            string cacheKey = string.Join(
                "|",
                "viewport",
                maskKey,
                visibleRoi.X.ToString(CultureInfo.InvariantCulture),
                visibleRoi.Y.ToString(CultureInfo.InvariantCulture),
                visibleRoi.Width.ToString(CultureInfo.InvariantCulture),
                visibleRoi.Height.ToString(CultureInfo.InvariantCulture),
                targetWidth.ToString(CultureInfo.InvariantCulture),
                targetHeight.ToString(CultureInfo.InvariantCulture));
            Bitmap overlay;
            if (TryGetLargeProcessedOverlayFromCache(cacheKey, out overlay))
            {
                DrawLargeProcessedOverlayTile(e.Graphics, overlay, visibleRoi, e.Zoom, e.Offset);
                return;
            }

            // The raw OpenCV mask is the source of truth.  Render the complete
            // visible part of it as one display-sized bitmap at every zoom so a
            // pending source-tile batch can never leave a visible edge segment
            // blank. This affects presentation only; contour calculations keep
            // using the full-resolution native mask. Display conversion is
            // deliberately synchronous here: it is bounded by the viewport,
            // avoids a second queue, and must not make the result blink out
            // while the user pans or resets the view.
            try
            {
                Stopwatch displayStopwatch = Stopwatch.StartNew();
                int maskX = visibleRoi.X - roi.X;
                int maskY = visibleRoi.Y - roi.Y;
                using (var visibleMask = new Cv.Mat(binaryMask,
                    new Cv.Rect(maskX, maskY, visibleRoi.Width, visibleRoi.Height)))
                {
                    overlay = CreateLargeProcessedBinaryViewportOverlay(visibleMask, targetWidth, targetHeight);
                }

                TrimLargeProcessedOverlayCache();
                largeProcessedOverlayCache[cacheKey] = overlay;
                lastDisplayProcessingElapsedMilliseconds = displayStopwatch.ElapsedMilliseconds;
                UpdateProcessingTimingStatus(includeImageProcessingTimeOnNextDisplay);
                includeImageProcessingTimeOnNextDisplay = false;
                CompleteParameterApplyStatus();
                DrawLargeProcessedOverlayTile(e.Graphics, overlay, visibleRoi, e.Zoom, e.Offset);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (TryGetLargeProcessedOverlayFromCache("overview|" + maskKey, out overlay))
                {
                    DrawLargeProcessedOverlayTile(e.Graphics, overlay, roi, e.Zoom, e.Offset);
                }
            }
        }

        private void PaintLargeProcessedBinaryExactTiles(
            LargeImageOverlayPaintEventArgs e,
            Rectangle roi,
            Rectangle visibleRoi,
            ImageProcessingStepSettings step,
            Cv.Mat binaryMask)
        {
            int tileSize = e.Zoom < 0.5f ? 512 : e.Zoom < 1f ? 256 : LargeProcessedOverlayTileSize;
            // Finish the tiles currently on screen before any prefetch work.
            // A queued off-screen ring used to consume the short queue and
            // leave lower visible line segments absent until the user panned.
            int startTileX = (visibleRoi.Left / tileSize) * tileSize;
            int endTileX = ((visibleRoi.Right + tileSize - 1) / tileSize) * tileSize;
            int startTileY = (visibleRoi.Top / tileSize) * tileSize;
            int endTileY = ((visibleRoi.Bottom + tileSize - 1) / tileSize) * tileSize;
            Point focusPoint = new Point(
                visibleRoi.Left + (visibleRoi.Width / 2),
                visibleRoi.Top + (visibleRoi.Height / 2));

            for (int tileY = startTileY; tileY < endTileY; tileY += tileSize)
            {
                for (int tileX = startTileX; tileX < endTileX; tileX += tileSize)
                {
                    Rectangle tileRect = Rectangle.Intersect(
                        roi,
                        new Rectangle(tileX, tileY, tileSize, tileSize));
                    if (tileRect.Width <= 0 || tileRect.Height <= 0)
                    {
                        continue;
                    }

                    string cacheKey = CreateLargeProcessedOverlayCacheKey(tileRect, roi, step);
                    Bitmap tile;
                    if (TryGetLargeProcessedOverlayFromCache(cacheKey, out tile))
                    {
                        DrawLargeProcessedOverlayTile(e.Graphics, tile, tileRect, e.Zoom, e.Offset);
                    }
                    else if (tileRect.Contains(focusPoint))
                    {
                        tile = CreateRedOverlayTileFromBinaryMask(binaryMask, roi, tileRect);
                        TrimLargeProcessedOverlayCache();
                        largeProcessedOverlayCache[cacheKey] = tile;
                        DrawLargeProcessedOverlayTile(e.Graphics, tile, tileRect, e.Zoom, e.Offset);
                    }
                    else
                    {
                        QueueLargeProcessedOverlayTileFromBinaryMask(binaryMask, roi, tileRect, cacheKey);
                    }
                }
            }
        }

        private void QueueLargeProcessedBinaryViewportOverlay(
            Cv.Mat mask,
            Rectangle roi,
            Rectangle visibleRoi,
            int targetWidth,
            int targetHeight,
            string cacheKey)
        {
            if (pendingLargeProcessedViewportOverlays.Contains(cacheKey) ||
                pendingLargeProcessedViewportOverlays.Count >= MaxPendingLargeProcessedViewportOverlays)
            {
                return;
            }

            int maskX = visibleRoi.X - roi.X;
            int maskY = visibleRoi.Y - roi.Y;
            Cv.Mat visibleMask = new Cv.Mat(mask, new Cv.Rect(maskX, maskY, visibleRoi.Width, visibleRoi.Height));
            pendingLargeProcessedViewportOverlays.Add(cacheKey);
            statusLabel.Text = "正在更新處理後顯示...";
            Task.Factory.StartNew(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        using (visibleMask)
                        {
                            overlay = CreateLargeProcessedBinaryViewportOverlay(visibleMask, targetWidth, targetHeight);
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedViewportOverlays.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    statusLabel.Text = "影像處理結果已顯示";
                                    CompleteParameterApplyStatus();
                                    leftProcessedDisplayControl.ScheduleImageViewRefresh();
                                    rightProcessedDisplayControl.ScheduleImageViewRefresh();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        pendingLargeProcessedViewportOverlays.Remove(cacheKey);
                                        statusLabel.Text = "處理後顯示失敗：" + ex.Message;
                                    }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private bool TryGetLargeProcessedOverlayFromCache(string cacheKey, out Bitmap overlay)
        {
            if (largeProcessedOverlayCache.TryGetValue(cacheKey, out overlay))
            {
                try
                {
                    if (overlay != null && overlay.Width > 0 && overlay.Height > 0)
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                }

                largeProcessedOverlayCache.Remove(cacheKey);
            }

            overlay = null;
            return false;
        }

        private static Cv.Mat CreateOpenCvCannyBinaryMask(
            byte[,] gray,
            int lowThreshold,
            int highThreshold,
            int kernelSize,
            bool l2Gradient,
            int gaussianBlurSize,
            double gaussianSigma,
            int minEdgeLength,
            int maxGap)
        {
            using (var source = CreateOpenCvGrayMat(gray))
            {
                return CreateOpenCvCannyBinaryMask(
                    source,
                    lowThreshold,
                    highThreshold,
                    kernelSize,
                    l2Gradient,
                    gaussianBlurSize,
                    gaussianSigma,
                    minEdgeLength,
                    maxGap);
            }
        }

        private static Cv.Mat CreateOpenCvCannyBinaryMask(
            Cv.Mat source,
            int lowThreshold,
            int highThreshold,
            int kernelSize,
            bool l2Gradient,
            int gaussianBlurSize,
            double gaussianSigma,
            int minEdgeLength,
            int maxGap)
        {
            using (var blurred = new Cv.Mat())
            using (var edges = new Cv.Mat())
            {
                int blurSize = EnsureOdd(Math.Max(1, gaussianBlurSize));
                Cv.Cv2.GaussianBlur(
                    source,
                    blurred,
                    new Cv.Size(blurSize, blurSize),
                    Math.Max(0.1, gaussianSigma));
                Cv.Cv2.Canny(
                    blurred,
                    edges,
                    Math.Min(lowThreshold, highThreshold),
                    Math.Max(lowThreshold, highThreshold),
                    NormalizeCannyKernelSize(kernelSize),
                    l2Gradient);

                // Canny is a raw OpenCV edge detector. Length filtering is a
                // contour/feature-filter operation and is intentionally not
                // part of this stage.
                return edges.Clone();
            }
        }

        private static int NormalizeCannyKernelSize(int kernelSize)
        {
            // OpenCV Canny accepts only Sobel apertures 3, 5, or 7.
            if (kernelSize <= 3)
            {
                return 3;
            }

            return kernelSize <= 5 ? 5 : 7;
        }

        private static Cv.Mat CreateOpenCvPolarityBinaryMask(
            Cv.Mat source,
            int contrastThreshold,
            int edgeWidth,
            int smoothing,
            string polarity,
            string searchDirection,
            double gaussianSigma,
            string borderType)
        {
            using (var blurred = new Cv.Mat())
            using (var gradientX = new Cv.Mat())
            using (var gradientY = new Cv.Mat())
            using (var edgeMask = new Cv.Mat())
            using (var secondaryMask = new Cv.Mat())
            {
                int blurSize = EnsureOdd(Math.Max(1, smoothing));
                Cv.Cv2.GaussianBlur(
                    source,
                    blurred,
                    new Cv.Size(blurSize, blurSize),
                    Math.Max(0.1, gaussianSigma),
                    0,
                    GetOpenCvBorderType(borderType));
                int aperture = NormalizeSobelKernelSize(edgeWidth);
                // A 7x7 Sobel can exceed Int16 at scale 1.  Keep the faster,
                // smaller Int16 pipeline and scale both the gradient and the
                // comparison threshold by the same ratio instead of moving
                // every full-image buffer to 32-bit float.
                double gradientScale = aperture == 7 ? 1.0 / 16.0 : 1.0;
                double scaledContrastThreshold = contrastThreshold * gradientScale;
                Cv.MatType gradientType = Cv.MatType.CV_16SC1;

                string normalizedDirection = NormalizePolaritySearchDirection(searchDirection);
                bool horizontalOnly = normalizedDirection == "X";
                bool verticalOnly = normalizedDirection == "Y";
                if (!verticalOnly)
                {
                    Cv.Cv2.Sobel(blurred, gradientX, gradientType, 1, 0, aperture, gradientScale);
                    CreateOpenCvPolarityComparison(gradientX, edgeMask, polarity, scaledContrastThreshold);
                }

                if (!horizontalOnly)
                {
                    Cv.Cv2.Sobel(blurred, gradientY, gradientType, 0, 1, aperture, gradientScale);
                    CreateOpenCvPolarityComparison(
                        gradientY,
                        (horizontalOnly || verticalOnly) ? edgeMask : secondaryMask,
                        polarity,
                        scaledContrastThreshold);
                }

                if (!horizontalOnly && !verticalOnly)
                {
                    Cv.Cv2.BitwiseOr(edgeMask, secondaryMask, edgeMask);
                }

                return edgeMask.Clone();
            }
        }

        private static Cv.Mat CreateOpenCvSobelBinaryMask(
            Cv.Mat source,
            int threshold,
            string direction,
            int kernelSize)
        {
            using (var gradientX = new Cv.Mat())
            using (var gradientY = new Cv.Mat())
            using (var edgeMask = new Cv.Mat())
            using (var secondaryMask = new Cv.Mat())
            {
                int aperture = NormalizeSobelKernelSize(kernelSize);
                double gradientScale = aperture == 7 ? 1.0 / 16.0 : 1.0;
                double scaledThreshold = threshold * gradientScale;
                string normalizedDirection = NormalizePolaritySearchDirection(direction);
                bool xOnly = normalizedDirection == "X";
                bool yOnly = normalizedDirection == "Y";

                if (!yOnly)
                {
                    Cv.Cv2.Sobel(source, gradientX, Cv.MatType.CV_16SC1, 1, 0, aperture, gradientScale);
                    CreateOpenCvPolarityComparison(gradientX, edgeMask, "Any", scaledThreshold);
                }

                if (!xOnly)
                {
                    Cv.Cv2.Sobel(source, gradientY, Cv.MatType.CV_16SC1, 0, 1, aperture, gradientScale);
                    CreateOpenCvPolarityComparison(
                        gradientY,
                        (xOnly || yOnly) ? edgeMask : secondaryMask,
                        "Any",
                        scaledThreshold);
                }

                if (!xOnly && !yOnly)
                {
                    Cv.Cv2.BitwiseOr(edgeMask, secondaryMask, edgeMask);
                }

                return edgeMask.Clone();
            }
        }

        private static void CreateOpenCvPolarityComparison(
            Cv.Mat gradient,
            Cv.Mat destination,
            string polarity,
            double contrastThreshold)
        {
            if (string.Equals(polarity, "BrightToDark", StringComparison.OrdinalIgnoreCase))
            {
                Cv.Cv2.Compare(gradient, -contrastThreshold, destination, Cv.CmpType.LT);
                return;
            }

            if (string.Equals(polarity, "DarkToBright", StringComparison.OrdinalIgnoreCase))
            {
                Cv.Cv2.Compare(gradient, contrastThreshold, destination, Cv.CmpType.GT);
                return;
            }

            // The absolute value is only needed for the Any polarity case.
            // Reuse the gradient buffer instead of allocating another full Mat.
            Cv.Cv2.Absdiff(gradient, Cv.Scalar.All(0), gradient);
            Cv.Cv2.Compare(gradient, contrastThreshold, destination, Cv.CmpType.GT);
        }

        private static string NormalizePolaritySearchDirection(string direction)
        {
            if (string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, "X", StringComparison.OrdinalIgnoreCase))
            {
                return "X";
            }

            if (string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return "Y";
            }

            return "Any";
        }

        private static Cv.BorderTypes GetOpenCvBorderType(string borderType)
        {
            if (string.Equals(borderType, "Replicate", StringComparison.OrdinalIgnoreCase))
            {
                return Cv.BorderTypes.Replicate;
            }

            if (string.Equals(borderType, "Constant", StringComparison.OrdinalIgnoreCase))
            {
                return Cv.BorderTypes.Constant;
            }

            return Cv.BorderTypes.Reflect101;
        }

        private static int NormalizeSobelKernelSize(int kernelSize)
        {
            if (kernelSize <= 1)
            {
                return 1;
            }

            if (kernelSize <= 3)
            {
                return 3;
            }

            return kernelSize <= 5 ? 5 : 7;
        }

        private static Cv.Mat CreateOpenCvFilteredBinaryMask(Cv.Mat edgeMask, int maxGap, int minEdgeLength)
        {
            ApplyOpenCvMaskClosing(edgeMask, maxGap);

            if (minEdgeLength <= 1)
            {
                return edgeMask.Clone();
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
                for (int label = 1; label < labelCount; label++)
                {
                    accepted[label] = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area) >= minEdgeLength;
                }

                var filtered = new Cv.Mat(edgeMask.Rows, edgeMask.Cols, Cv.MatType.CV_8UC1);
                int[] labelsRow = new int[edgeMask.Width];
                byte[] resultRow = new byte[edgeMask.Width];
                long labelsStride = labels.Step();
                long resultStride = filtered.Step();
                for (int y = 0; y < edgeMask.Height; y++)
                {
                    Marshal.Copy(labels.Data + checked((int)(y * labelsStride)), labelsRow, 0, labelsRow.Length);
                    Array.Clear(resultRow, 0, resultRow.Length);
                    for (int x = 0; x < labelsRow.Length; x++)
                    {
                        int label = labelsRow[x];
                        if (label > 0 && label < accepted.Length && accepted[label])
                        {
                            resultRow[x] = 255;
                        }
                    }

                    Marshal.Copy(resultRow, 0, filtered.Data + checked((int)(y * resultStride)), resultRow.Length);
                }

                return filtered;
            }
        }

        private static void ApplyOpenCvMaskClosing(Cv.Mat edgeMask, int maxGap)
        {
            if (maxGap <= 0)
            {
                return;
            }

            int kernelSize = EnsureOdd(Math.Max(3, (maxGap * 2) + 1));
            using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(Cv.MorphShapes.Rect, new Cv.Size(kernelSize, kernelSize)))
            {
                Cv.Cv2.MorphologyEx(edgeMask, edgeMask, Cv.MorphTypes.Close, kernel);
            }
        }

        private void QueueLargeProcessedOverlayTileFromMask(
            bool[,] mask, Rectangle roi, Rectangle tileRect, string cacheKey)
        {
            if (pendingLargeProcessedOverlayTiles.Contains(cacheKey) ||
                pendingLargeProcessedOverlayTiles.Count >= MaxPendingCachedMaskOverlayTiles)
            {
                return;
            }

            pendingLargeProcessedOverlayTiles.Add(cacheKey);
            Task.Run(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        overlay = CreateRedOverlayTileFromMask(mask, roi, tileRect);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    leftProcessedDisplayControl.ScheduleImageViewRefresh();
                                    rightProcessedDisplayControl.ScheduleImageViewRefresh();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(new Action(delegate { pendingLargeProcessedOverlayTiles.Remove(cacheKey); }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }
                    }
            });
        }

        private bool IsLargeProcessedMaskBuildCurrent(string maskKey, int generation)
        {
            lock (largeProcessedMaskLock)
            {
                return generation == largeProcessedMaskGeneration &&
                    largeProcessedMaskBuildKeys.Contains(maskKey);
            }
        }

        private static bool CanUseNativeEdgeMask(string method, Dictionary<string, string> parameters)
        {
            return string.Equals(method, "Canny Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Polarity Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Sobel Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Global Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Adaptive Threshold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Otsu Threshold", StringComparison.OrdinalIgnoreCase);
        }

        private static Cv.Mat CreateOpenCvGlobalThresholdBinaryMask(
            Cv.Mat source,
            string thresholdMode,
            int threshold,
            int lowerThreshold,
            int upperThreshold,
            int maxValue,
            string thresholdType)
        {
            var result = new Cv.Mat();
            if (string.Equals(thresholdMode, "Range", StringComparison.OrdinalIgnoreCase))
            {
                int lower = ClampInt(Math.Min(lowerThreshold, upperThreshold), 0, 255);
                int upper = ClampInt(Math.Max(lowerThreshold, upperThreshold), 0, 255);
                Cv.Cv2.InRange(
                    source,
                    new Cv.Scalar(lower),
                    new Cv.Scalar(upper),
                    result);
                if (ClampInt(maxValue, 1, 255) != 255)
                {
                    Cv.Cv2.Threshold(
                        result,
                        result,
                        0,
                        ClampInt(maxValue, 1, 255),
                        Cv.ThresholdTypes.Binary);
                }

                return result;
            }

            Cv.ThresholdTypes type = string.Equals(thresholdType, "BinaryInv", StringComparison.OrdinalIgnoreCase)
                ? Cv.ThresholdTypes.BinaryInv
                : Cv.ThresholdTypes.Binary;
            Cv.Cv2.Threshold(
                source,
                result,
                ClampInt(threshold, 0, 255),
                ClampInt(maxValue, 1, 255),
                type);
            return result;
        }

        private static Cv.Mat CreateOpenCvAdaptiveThresholdBinaryMask(
            Cv.Mat source,
            int maxValue,
            string adaptiveMethod,
            string thresholdType,
            int blockSize,
            double c)
        {
            var result = new Cv.Mat();
            Cv.AdaptiveThresholdTypes adaptive = string.Equals(adaptiveMethod, "MeanC", StringComparison.OrdinalIgnoreCase)
                ? Cv.AdaptiveThresholdTypes.MeanC
                : Cv.AdaptiveThresholdTypes.GaussianC;
            Cv.ThresholdTypes type = string.Equals(thresholdType, "BinaryInv", StringComparison.OrdinalIgnoreCase)
                ? Cv.ThresholdTypes.BinaryInv
                : Cv.ThresholdTypes.Binary;
            Cv.Cv2.AdaptiveThreshold(
                source,
                result,
                ClampInt(maxValue, 1, 255),
                adaptive,
                type,
                EnsureOdd(Math.Max(3, blockSize)),
                c);
            return result;
        }

        private static Cv.Mat CreateOpenCvOtsuThresholdBinaryMask(
            Cv.Mat source,
            int maxValue,
            string thresholdType)
        {
            var result = new Cv.Mat();
            Cv.ThresholdTypes type = string.Equals(thresholdType, "BinaryInv", StringComparison.OrdinalIgnoreCase)
                ? Cv.ThresholdTypes.BinaryInv
                : Cv.ThresholdTypes.Binary;
            Cv.Cv2.Threshold(
                source,
                result,
                0,
                ClampInt(maxValue, 1, 255),
                type | Cv.ThresholdTypes.Otsu);
            return result;
        }

        private static Cv.Mat CreateNativeLargeEdgeBinaryMask(
            Cv.Mat source,
            string method,
            Dictionary<string, string> parameters)
        {
            if (string.Equals(method, "Global Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return CreateOpenCvGlobalThresholdBinaryMask(
                    source,
                    GetStringParameter(parameters, "ThresholdMode", "Single"),
                    GetIntParameter(parameters, "Threshold", 128),
                    GetIntParameter(parameters, "LowerThreshold", 0),
                    GetIntParameter(parameters, "UpperThreshold", 255),
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (string.Equals(method, "Adaptive Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return CreateOpenCvAdaptiveThresholdBinaryMask(
                    source,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "AdaptiveMethod", "GaussianC"),
                    GetStringParameter(parameters, "ThresholdType", "Binary"),
                    GetIntParameter(parameters, "BlockSize", 11),
                    GetDoubleParameter(parameters, "C", 2));
            }

            if (string.Equals(method, "Otsu Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return CreateOpenCvOtsuThresholdBinaryMask(
                    source,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (string.Equals(method, "Sobel Edge", StringComparison.OrdinalIgnoreCase))
            {
                return CreateOpenCvSobelBinaryMask(
                    source,
                    GetIntParameter(parameters, "Threshold", 30),
                    GetStringParameter(parameters, "Direction", "Any"),
                    GetIntParameter(parameters, "KernelSize", 3));
            }

            if (string.Equals(method, "Polarity Edge", StringComparison.OrdinalIgnoreCase))
            {
                return CreateOpenCvPolarityBinaryMask(
                    source,
                    GetIntParameter(parameters, "ContrastThreshold", 20),
                    GetPolarityCoreWidth(parameters),
                    GetIntParameter(parameters, "Smoothing", 1),
                    GetStringParameter(parameters, "Polarity", "Any"),
                    GetStringParameter(parameters, "SearchDirection", "Any"),
                    GetDoubleParameter(parameters, "GaussianSigma", 0.5),
                    GetStringParameter(parameters, "BorderType", "Reflect"));
            }

            return CreateOpenCvCannyBinaryMask(
                source,
                GetIntParameter(parameters, "LowThreshold", 50),
                GetIntParameter(parameters, "HighThreshold", 150),
                GetIntParameter(parameters, "KernelSize", 3),
                GetBoolParameter(parameters, "L2Gradient", false),
                GetIntParameter(parameters, "GaussianBlurSize", 5),
                GetDoubleParameter(parameters, "GaussianSigma", 1.4),
                GetIntParameter(parameters, "MinEdgeLength", 10),
                GetIntParameter(parameters, "MaxGap", 2));
        }

        private void PublishCompletedLargeProcessedBinaryMask(
            Cv.Mat mask,
            Rectangle roi,
            ImageProcessingStepSettings step,
            string maskKey,
            int generation,
            long processingElapsedMilliseconds)
        {
            BeginInvoke(
                new Action(
                    delegate
                    {
                        lock (largeProcessedMaskLock)
                        {
                            if (generation != largeProcessedMaskGeneration ||
                                !largeProcessedMaskBuildKeys.Contains(maskKey))
                            {
                                mask.Dispose();
                                return;
                            }

                            Cv.Mat previous;
                            if (largeProcessedBinaryMasks.TryGetValue(maskKey, out previous))
                            {
                                previous.Dispose();
                            }

                            largeProcessedBinaryMasks[maskKey] = mask;
                            largeProcessedMaskBuildKeys.Remove(maskKey);
                        }

                        QueueLargeProcessedBinaryOverview(mask, roi, maskKey, generation);

                        lastImageProcessingElapsedMilliseconds = processingElapsedMilliseconds;
                        RecordImageProcessingStepElapsed(step, processingElapsedMilliseconds);
                        // Defer the timing message until the first actual
                        // viewport render. Subsequent cache/display updates
                        // must not imply that the algorithm ran again.
                        includeImageProcessingTimeOnNextDisplay = true;
                        if (IsAnyProcessedTabVisible())
                        {
                            SetParameterApplyStatus("產生預覽圖中...");
                        }
                        else
                        {
                            CompleteParameterApplyStatus(true);
                        }
                        leftProcessedDisplayControl.InvalidateImageView();
                        rightProcessedDisplayControl.InvalidateImageView();
                    }));
        }

        private void RecordImageProcessingStepElapsed(
            ImageProcessingStepSettings step,
            long processingElapsedMilliseconds)
        {
            if (step == null)
            {
                return;
            }

            long existingElapsedMilliseconds;
            imageProcessingStepElapsedMilliseconds.TryGetValue(step, out existingElapsedMilliseconds);
            imageProcessingStepElapsedMilliseconds[step] = existingElapsedMilliseconds + processingElapsedMilliseconds;
        }

        private void UpdateProcessingTimingStatus(bool includeImageProcessingTime)
        {
            imageProcessingDisplayPending = false;
            statusLabel.Text = BuildPipelineTimingText(includeImageProcessingTime, false);
        }

        private void QueueLargeProcessedBinaryOverview(Cv.Mat mask, Rectangle roi, string maskKey, int generation)
        {
            string cacheKey = "overview|" + maskKey;
            if (largeProcessedOverlayCache.ContainsKey(cacheKey) ||
                pendingLargeProcessedOverviewOverlays.Contains(cacheKey))
            {
                return;
            }

            // A full ROI view retains the native buffer without cloning the
            // large image.  The Mat copy constructor maps to ranges in this
            // OpenCvSharp version and throws "empty ranges".
            Cv.Mat overviewMask = new Cv.Mat(mask, new Cv.Rect(0, 0, mask.Width, mask.Height));
            pendingLargeProcessedOverviewOverlays.Add(cacheKey);
            Task.Factory.StartNew(
                delegate
                {
                    Bitmap overview = null;
                    try
                    {
                        using (overviewMask)
                        {
                            float scale = Math.Min(1f, 2048f / Math.Max(overviewMask.Width, overviewMask.Height));
                            overview = CreateLargeProcessedBinaryViewportOverlay(
                                overviewMask,
                                Math.Max(1, (int)Math.Round(overviewMask.Width * scale)),
                                Math.Max(1, (int)Math.Round(overviewMask.Height * scale)));
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverviewOverlays.Remove(cacheKey);
                                    lock (largeProcessedMaskLock)
                                    {
                                        if (generation != largeProcessedMaskGeneration ||
                                            !largeProcessedBinaryMasks.ContainsKey(maskKey))
                                        {
                                            overview.Dispose();
                                            return;
                                        }
                                    }

                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overview;
                                    overview = null;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(new Action(delegate { pendingLargeProcessedOverviewOverlays.Remove(cacheKey); }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overview != null)
                        {
                            overview.Dispose();
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void TrimLargeProcessedOverlayCache()
        {
            while (largeProcessedOverlayCache.Count >= MaxLargeProcessedOverlayCacheCount)
            {
                KeyValuePair<string, Bitmap> oldest = largeProcessedOverlayCache.FirstOrDefault(
                    pair => !pair.Key.StartsWith("overview|", StringComparison.Ordinal));
                if (string.IsNullOrEmpty(oldest.Key))
                {
                    oldest = largeProcessedOverlayCache.First();
                }

                largeProcessedOverlayCache.Remove(oldest.Key);
                oldest.Value.Dispose();
            }
        }

        private string CreateLargeProcessedOverlayCacheKey(Rectangle tileRect, Rectangle roi, ImageProcessingStepSettings step)
        {
            return string.Join(
                "|",
                step.Method,
                step.Parameters,
                roi.X.ToString(CultureInfo.InvariantCulture),
                roi.Y.ToString(CultureInfo.InvariantCulture),
                roi.Width.ToString(CultureInfo.InvariantCulture),
                roi.Height.ToString(CultureInfo.InvariantCulture),
                tileRect.X.ToString(CultureInfo.InvariantCulture),
                tileRect.Y.ToString(CultureInfo.InvariantCulture),
                tileRect.Width.ToString(CultureInfo.InvariantCulture),
                tileRect.Height.ToString(CultureInfo.InvariantCulture));
        }

        private void QueueLargeProcessedOverlayTile(LargeImageSource source, Rectangle tileRect, ImageProcessingStepSettings step, string cacheKey)
        {
            if (pendingLargeProcessedOverlayTiles.Contains(cacheKey))
            {
                return;
            }

            if (pendingLargeProcessedOverlayTiles.Count >= MaxPendingLargeProcessedOverlayTiles)
            {
                return;
            }

            pendingLargeProcessedOverlayTiles.Add(cacheKey);
            string method = step.Method;
            string parameters = step.Parameters;
            LargeImageSource sharedSource = source.AddReference();
            ReportBackgroundStatus("影像處理運算中...等待 " + pendingLargeProcessedOverlayTiles.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊");

            Task.Run(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        Bitmap tile;
                        bool usedPreviewTile = false;
                        if (!sharedSource.TryCreateRegionBitmapFromCachedTile(tileRect, out tile))
                        {
                            usedPreviewTile = TryCreateRegionBitmapFromPreview(sharedSource, tileRect, out tile);
                        }

                        if (tile == null)
                        {
                            sharedSource.QueueTile(
                                tileRect,
                                delegate
                                {
                                    try
                                    {
                                        BeginInvoke(
                                            new Action(
                                                delegate
                                                {
                                                    leftProcessedDisplayControl.InvalidateImageView();
                                                    rightProcessedDisplayControl.InvalidateImageView();
                                                }));
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                    }
                                    catch (InvalidOperationException)
                                    {
                                    }
                                });
                            BeginInvoke(
                                new Action(
                                    delegate
                                    {
                                        pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                        statusLabel.Text = "等待原圖區塊載入後再處理...";
                                        leftProcessedDisplayControl.InvalidateImageView();
                                        rightProcessedDisplayControl.InvalidateImageView();
                                    }));
                            return;
                        }

                        if (usedPreviewTile)
                        {
                            sharedSource.QueueTile(
                                tileRect,
                                delegate
                                {
                                    try
                                    {
                                        BeginInvoke(
                                            new Action(
                                                delegate
                                                {
                                                    Bitmap staleOverlay;
                                                    if (largeProcessedOverlayCache.TryGetValue(cacheKey, out staleOverlay))
                                                    {
                                                        largeProcessedOverlayCache.Remove(cacheKey);
                                                        staleOverlay.Dispose();
                                                    }

                                                    leftProcessedDisplayControl.InvalidateImageView();
                                                    rightProcessedDisplayControl.InvalidateImageView();
                                                }));
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                    }
                                    catch (InvalidOperationException)
                                    {
                                    }
                                });
                        }

                        using (tile)
                        {
                            bool[,] mask = CreateEdgeMask(tile, method, ParseImageProcessingParameters(parameters));
                            overlay = CreateRedOverlayTile(mask);
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    ReportBackgroundStatus(pendingLargeProcessedOverlayTiles.Count > 0
                                        ? "影像處理運算中...等待 " + pendingLargeProcessedOverlayTiles.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊"
                                        : "影像處理完成，已顯示 " + largeProcessedOverlayCache.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊");
                                    if (pendingLargeProcessedOverlayTiles.Count == 0)
                                    {
                                        CompleteParameterApplyStatus();
                                    }
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    statusLabel.Text = "影像處理失敗：" + ex.Message;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
                                }));
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }

                        sharedSource.ReleaseReference();
                    }
                });
        }

        private void QueueLargeProcessedOverlayTileFromBinaryMask(
            Cv.Mat mask, Rectangle roi, Rectangle tileRect, string cacheKey)
        {
            if (pendingLargeProcessedOverlayTiles.Contains(cacheKey) ||
                pendingLargeProcessedOverlayTiles.Count >= MaxPendingCachedMaskOverlayTiles)
            {
                return;
            }

            int maskX = tileRect.X - roi.X;
            int maskY = tileRect.Y - roi.Y;
            Cv.Mat maskTile;
            using (var tileView = new Cv.Mat(mask, new Cv.Rect(maskX, maskY, tileRect.Width, tileRect.Height)))
            {
                maskTile = tileView.Clone();
            }

            pendingLargeProcessedOverlayTiles.Add(cacheKey);
            Task.Run(
                delegate
                {
                    Bitmap overlay = null;
                    try
                    {
                        using (maskTile)
                        {
                            overlay = CreateRedOverlayTileFromBinaryMask(maskTile, Rectangle.Empty,
                                new Rectangle(0, 0, tileRect.Width, tileRect.Height));
                        }

                        BeginInvoke(
                            new Action(
                                delegate
                                {
                                    pendingLargeProcessedOverlayTiles.Remove(cacheKey);
                                    TrimLargeProcessedOverlayCache();
                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    leftProcessedDisplayControl.ScheduleImageViewRefresh();
                                    rightProcessedDisplayControl.ScheduleImageViewRefresh();
                                }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        try
                        {
                            BeginInvoke(new Action(delegate { pendingLargeProcessedOverlayTiles.Remove(cacheKey); }));
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    finally
                    {
                        if (overlay != null)
                        {
                            overlay.Dispose();
                        }
                    }
                });
        }

        private static bool TryCreateRegionBitmapFromPreview(LargeImageSource source, Rectangle sourceRect, out Bitmap region)
        {
            region = null;
            LargeImageSource.PreviewBitmap preview = null;
            try
            {
                preview = source.GetBestPreview(0f);
                if (preview == null || preview.Bitmap == null || preview.Scale <= 0f)
                {
                    return false;
                }

                Rectangle previewRect = Rectangle.FromLTRB(
                    Math.Max(0, Math.Min(preview.Bitmap.Width - 1, (int)Math.Floor(sourceRect.Left * preview.Scale))),
                    Math.Max(0, Math.Min(preview.Bitmap.Height - 1, (int)Math.Floor(sourceRect.Top * preview.Scale))),
                    Math.Max(1, Math.Min(preview.Bitmap.Width, (int)Math.Ceiling(sourceRect.Right * preview.Scale))),
                    Math.Max(1, Math.Min(preview.Bitmap.Height, (int)Math.Ceiling(sourceRect.Bottom * preview.Scale))));
                if (previewRect.Right <= previewRect.Left || previewRect.Bottom <= previewRect.Top)
                {
                    return false;
                }

                region = new Bitmap(sourceRect.Width, sourceRect.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(region))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    graphics.DrawImage(preview.Bitmap, new Rectangle(0, 0, region.Width, region.Height), previewRect, GraphicsUnit.Pixel);
                }

                return true;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
                if (region != null)
                {
                    region.Dispose();
                    region = null;
                }

                return false;
            }
            finally
            {
                if (preview != null)
                {
                    preview.Dispose();
                }
            }
        }

        private static Bitmap CreateRedOverlayTile(bool[,] mask)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            var overlay = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] row = new byte[Math.Abs(stride)];
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    for (int x = 0; x < width; x++)
                    {
                        if (mask[x, y])
                        {
                            int offset = x * 4;
                            row[offset] = 0;
                            row[offset + 1] = 0;
                            row[offset + 2] = 255;
                            row[offset + 3] = 255;
                        }
                    }

                    Marshal.Copy(row, 0, data.Scan0 + (y * stride), row.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private static Bitmap CreateRedOverlayTileFromMask(bool[,] mask, Rectangle maskRoi, Rectangle tileRect)
        {
            int width = tileRect.Width;
            int height = tileRect.Height;
            var overlay = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] row = new byte[Math.Abs(stride)];
                int maskWidth = mask.GetLength(0);
                int maskHeight = mask.GetLength(1);
                int maskStartX = tileRect.X - maskRoi.X;
                int maskStartY = tileRect.Y - maskRoi.Y;
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    int maskY = maskStartY + y;
                    if (maskY >= 0 && maskY < maskHeight)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int maskX = maskStartX + x;
                            if (maskX >= 0 && maskX < maskWidth && mask[maskX, maskY])
                            {
                                int offset = x * 4;
                                row[offset] = 0;
                                row[offset + 1] = 0;
                                row[offset + 2] = 255;
                                row[offset + 3] = 255;
                            }
                        }
                    }

                    Marshal.Copy(row, 0, data.Scan0 + (y * stride), row.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private static Bitmap CreateRedOverlayTileFromBinaryMask(Cv.Mat mask, Rectangle maskRoi, Rectangle tileRect)
        {
            int width = tileRect.Width;
            int height = tileRect.Height;
            var overlay = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = overlay.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] sourceRow = new byte[width];
                byte[] outputRow = new byte[Math.Abs(stride)];
                int maskStartX = tileRect.X - maskRoi.X;
                int maskStartY = tileRect.Y - maskRoi.Y;
                long maskStride = mask.Step();
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(outputRow, 0, outputRow.Length);
                    int maskY = maskStartY + y;
                    Marshal.Copy(
                        IntPtr.Add(mask.Data, checked((int)((maskY * maskStride) + maskStartX))),
                        sourceRow,
                        0,
                        width);
                    for (int x = 0; x < width; x++)
                    {
                        if (sourceRow[x] == 0)
                        {
                            continue;
                        }

                        int offset = x * 4;
                        outputRow[offset + 2] = 255;
                        outputRow[offset + 3] = 255;
                    }

                    Marshal.Copy(outputRow, 0, data.Scan0 + (y * stride), outputRow.Length);
                }
            }
            finally
            {
                overlay.UnlockBits(data);
            }

            return overlay;
        }

        private static Bitmap CreateLargeProcessedBinaryViewportOverlay(Cv.Mat visibleMask, int targetWidth, int targetHeight)
        {
            using (var scaledMask = new Cv.Mat())
            {
                if (visibleMask.Width > targetWidth || visibleMask.Height > targetHeight)
                {
                    Cv.Cv2.Resize(
                        visibleMask,
                        scaledMask,
                        new Cv.Size(targetWidth, targetHeight),
                        0,
                        0,
                        Cv.InterpolationFlags.Area);
                }
                else
                {
                    Cv.Cv2.Resize(
                        visibleMask,
                        scaledMask,
                        new Cv.Size(targetWidth, targetHeight),
                        0,
                        0,
                        Cv.InterpolationFlags.Nearest);
                }

                return CreateRedOverlayTileFromBinaryMask(
                    scaledMask,
                    Rectangle.Empty,
                    new Rectangle(0, 0, targetWidth, targetHeight));
            }
        }

        private static void DrawLargeProcessedOverlayTile(Graphics graphics, Bitmap overlay, Rectangle tileRect, float zoom, PointF offset)
        {
            int overlayWidth;
            int overlayHeight;
            try
            {
                overlayWidth = overlay != null ? overlay.Width : 0;
                overlayHeight = overlay != null ? overlay.Height : 0;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
                return;
            }

            if (overlayWidth <= 0 || overlayHeight <= 0 || tileRect.Width <= 0 || tileRect.Height <= 0 || zoom <= 0f)
            {
                return;
            }

            int left = (int)Math.Floor(offset.X + (tileRect.Left * zoom));
            int top = (int)Math.Floor(offset.Y + (tileRect.Top * zoom));
            int right = (int)Math.Ceiling(offset.X + (tileRect.Right * zoom));
            int bottom = (int)Math.Ceiling(offset.Y + (tileRect.Bottom * zoom));
            if (right <= left)
            {
                right = left + 1;
            }

            if (bottom <= top)
            {
                bottom = top + 1;
            }

            var destination = Rectangle.FromLTRB(left, top, right, bottom);
            var source = new Rectangle(0, 0, overlayWidth, overlayHeight);
            System.Drawing.Drawing2D.InterpolationMode previousInterpolation = graphics.InterpolationMode;
            System.Drawing.Drawing2D.PixelOffsetMode previousPixelOffset = graphics.PixelOffsetMode;
            try
            {
                // Masks are categorical data.  Bicubic/bilinear interpolation
                // invents red values between pixels and makes edge positions
                // look wider or shifted when zoomed.
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(overlay, destination, source, GraphicsUnit.Pixel);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                graphics.PixelOffsetMode = previousPixelOffset;
                graphics.InterpolationMode = previousInterpolation;
            }
        }

        private static void DrawLargeProcessedOverlayRegion(
            Graphics graphics,
            Bitmap overlay,
            Rectangle overlayBounds,
            Rectangle sourceRegion,
            float zoom,
            PointF offset)
        {
            if (overlay == null || overlayBounds.Width <= 0 || overlayBounds.Height <= 0 ||
                sourceRegion.Width <= 0 || sourceRegion.Height <= 0 || zoom <= 0f)
            {
                return;
            }

            Rectangle clippedRegion = Rectangle.Intersect(overlayBounds, sourceRegion);
            if (clippedRegion.Width <= 0 || clippedRegion.Height <= 0)
            {
                return;
            }

            int sourceLeft = Math.Max(
                0,
                Math.Min(overlay.Width - 1,
                    (int)Math.Floor((clippedRegion.Left - overlayBounds.Left) * (double)overlay.Width / overlayBounds.Width)));
            int sourceTop = Math.Max(
                0,
                Math.Min(overlay.Height - 1,
                    (int)Math.Floor((clippedRegion.Top - overlayBounds.Top) * (double)overlay.Height / overlayBounds.Height)));
            int sourceRight = Math.Max(
                sourceLeft + 1,
                Math.Min(overlay.Width,
                    (int)Math.Ceiling((clippedRegion.Right - overlayBounds.Left) * (double)overlay.Width / overlayBounds.Width)));
            int sourceBottom = Math.Max(
                sourceTop + 1,
                Math.Min(overlay.Height,
                    (int)Math.Ceiling((clippedRegion.Bottom - overlayBounds.Top) * (double)overlay.Height / overlayBounds.Height)));

            Rectangle source = Rectangle.FromLTRB(sourceLeft, sourceTop, sourceRight, sourceBottom);
            Rectangle destination = Rectangle.FromLTRB(
                (int)Math.Floor(offset.X + (clippedRegion.Left * zoom)),
                (int)Math.Floor(offset.Y + (clippedRegion.Top * zoom)),
                (int)Math.Ceiling(offset.X + (clippedRegion.Right * zoom)),
                (int)Math.Ceiling(offset.Y + (clippedRegion.Bottom * zoom)));
            if (destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            System.Drawing.Drawing2D.InterpolationMode previousInterpolation = graphics.InterpolationMode;
            System.Drawing.Drawing2D.PixelOffsetMode previousPixelOffset = graphics.PixelOffsetMode;
            try
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(overlay, destination, source, GraphicsUnit.Pixel);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                graphics.PixelOffsetMode = previousPixelOffset;
                graphics.InterpolationMode = previousInterpolation;
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
                            foreach (ImageProcessingStepSettings step in selectedSteps)
                            {
                                using (Cv.Mat binaryMask = CreateNativeLargeEdgeBinaryMask(
                                    roiInput,
                                    step.Method,
                                    ParseImageProcessingParameters(step.Parameters)))
                                {
                                    PaintRedOverlayImage(
                                        result,
                                        roi,
                                        ConvertOpenCvBinaryMask(binaryMask));
                                }
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

        private static bool[,] CreateEdgeMask(Bitmap image, string method, Dictionary<string, string> parameters)
        {
            return CreateEdgeMask(CreateGrayValues(image), method, parameters);
        }

        private static bool[,] CreateEdgeMask(byte[,] gray, string method, Dictionary<string, string> parameters)
        {
            if (method == "Global Threshold")
            {
                return CreateOpenCvGlobalThresholdMask(
                    gray,
                    GetStringParameter(parameters, "ThresholdMode", "Single"),
                    GetIntParameter(parameters, "Threshold", 128),
                    GetIntParameter(parameters, "LowerThreshold", 0),
                    GetIntParameter(parameters, "UpperThreshold", 255),
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (method == "Adaptive Threshold")
            {
                return CreateOpenCvAdaptiveThresholdMask(
                    gray,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "AdaptiveMethod", "GaussianC"),
                    GetStringParameter(parameters, "ThresholdType", "Binary"),
                    GetIntParameter(parameters, "BlockSize", 11),
                    GetDoubleParameter(parameters, "C", 2));
            }

            if (method == "Otsu Threshold")
            {
                return CreateOpenCvOtsuThresholdMask(
                    gray,
                    GetIntParameter(parameters, "MaxValue", 255),
                    GetStringParameter(parameters, "ThresholdType", "Binary"));
            }

            if (method == "Canny Edge")
            {
                return CreateOpenCvCannyMask(
                    gray,
                    GetIntParameter(parameters, "LowThreshold", 50),
                    GetIntParameter(parameters, "HighThreshold", 150),
                    GetIntParameter(parameters, "KernelSize", 3),
                    GetBoolParameter(parameters, "L2Gradient", false),
                    GetIntParameter(parameters, "GaussianBlurSize", 5),
                    GetDoubleParameter(parameters, "GaussianSigma", 1.4),
                    GetStringParameter(parameters, "EdgeSelection", "All"),
                    GetIntParameter(parameters, "MinEdgeLength", 10),
                    GetIntParameter(parameters, "MaxGap", 2));
            }

            if (method == "Sobel Edge")
            {
                return CreateOpenCvSobelMask(
                    gray,
                    GetIntParameter(parameters, "Threshold", 30),
                    GetStringParameter(parameters, "Direction", "Both"),
                    GetIntParameter(parameters, "KernelSize", 3));
            }

            return CreateOpenCvPolarityMask(
                gray,
                GetIntParameter(parameters, "ContrastThreshold", 20),
                GetPolarityCoreWidth(parameters),
                GetIntParameter(parameters, "Smoothing", 1),
                GetStringParameter(parameters, "Polarity", "Any"),
                GetStringParameter(parameters, "SearchDirection", "Any"),
                GetDoubleParameter(parameters, "GaussianSigma", 0.5),
                GetStringParameter(parameters, "BorderType", "Reflect"));
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

                try
                {
                    statusLabel.Text = "讀取圖片中：" + fileName;
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    rightImageTabControl.SelectedTab = rightOriginalTabPage;
                    rightOriginalDisplayControl.ClearRoiOverlay();
                    leftPreprocessedDisplayControl.ClearRoiOverlay();
                    rightPreprocessedDisplayControl.ClearRoiOverlay();
                    leftProcessedDisplayControl.ClearRoiOverlay();
                    rightProcessedDisplayControl.ClearRoiOverlay();
                    leftBlockProcessingDisplayControl.ClearRoiOverlay();
                    rightBlockProcessingDisplayControl.ClearRoiOverlay();
                    systemParameters.LastImagePath = dialog.FileName;
                    systemParameters.RoiEnabled = false;
                    systemParameters.Roi = Rectangle.Empty;
                    systemParameters.RoiRegions.Clear();
                    selectedRoiIndex = -1;
                    selectedImageProcessingStepIndex = -1;
                    selectedImageProcessingGroupId = null;
                    RebuildVisibleRoiItems();
                    MarkPreprocessedImageDirty();

                    await LoadImageIntoOriginalDisplaysAsync(dialog.FileName, CancellationToken.None);
                    SyncVisibleImageDisplaysFromLeft();
                    SaveSystemParameters();

                    statusLabel.Text = "已讀取圖片：" + fileName;
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
            if (icon != FunctionMenuIcon.None)
            {
                Rectangle iconBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 7, 16, 16);
                DrawFunctionMenuIcon(e.Graphics, icon, iconBounds, selected);
            }

            Rectangle textBounds = new Rectangle(
                e.Bounds.Left + (icon == FunctionMenuIcon.None
                    ? GetFunctionMenuIndent(menuText)
                    : 36),
                e.Bounds.Top,
                e.Bounds.Width - 40,
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
