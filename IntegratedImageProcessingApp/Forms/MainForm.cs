using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

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
        private readonly SystemParameterIniService systemParameterService;
        private SystemParameterSettings systemParameters;
        private bool roiMenuExpanded;
        private string expandedRoiText;
        private int selectedRoiIndex = -1;
        private bool imageProcessingMenuExpanded;
        private string expandedImageProcessingStepText;
        private TreeView imageProcessingFlowTreeView;
        private FlowLayoutPanel imageProcessingParameterPanel;
        private int selectedImageProcessingStepIndex = -1;
        private bool isUpdatingImageProcessingFlowTree;
        private bool isLoadingImageProcessingParameters;
        private System.Windows.Forms.Timer imageProcessingDebounceTimer;
        private Bitmap latestProcessedImage;
        private readonly Dictionary<string, Bitmap> largeProcessedOverlayCache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private readonly HashSet<string> pendingLargeProcessedOverlayTiles = new HashSet<string>(StringComparer.Ordinal);
        private readonly object largeProcessedMaskLock = new object();
        private bool[,] latestLargeProcessedMask;
        private Rectangle latestLargeProcessedMaskRoi;
        private string latestLargeProcessedMaskKey;
        private bool isLargeProcessedMaskBuilding;
        private int largeProcessedMaskGeneration;
        private bool processedImageDirty = true;
        private bool hasSharedImageViewState;
        private ImageViewState sharedImageViewState;

        private const string LoadImageMenuText = "讀取圖片";
        private const string RoiMenuText = "指定 ROI";
        private const string AddRoiMenuText = "  新增 ROI";
        private const string DeleteRoiMenuText = "      刪除";
        private const string ImageProcessingMenuText = "影像處理";
        private const string AddImageProcessingMenuText = "  新增影像處理";
        private const string DeleteImageProcessingStepMenuText = "      刪除";
        private const string MoveUpImageProcessingStepMenuText = "      上移";
        private const string MoveDownImageProcessingStepMenuText = "      下移";
        private const int MaxLargeProcessedOverlayCacheCount = 128;
        private const int LargeProcessedOverlayTileSize = 128;
        private const int LargeProcessedMaskChunkSize = 1024;
        // The algorithm must see the complete ROI so connected edges and
        // component filtering have the same result as a single full-image run.
        private const long MaxSinglePassLargeRoiPixels = long.MaxValue;
        private const int MaxPendingLargeProcessedOverlayTiles = 2;
        private static readonly string[] KernelSizeOptions = new[] { "3", "5", "7", "9", "11", "13", "15" };

        public MainForm()
        {
            systemParameterService = new SystemParameterIniService(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemParameters.ini"));
            systemParameters = systemParameterService.Load();

            InitializeComponent();

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
            leftOriginalDisplayControl = CreateImageDisplayControl(leftOriginalDisplayHostPanel, "左側 原圖");
            leftProcessedDisplayControl = CreateImageDisplayControl(leftProcessedDisplayHostPanel, "左側 處理後");
            leftObjectsDisplayControl = CreateImageDisplayControl(leftObjectsDisplayHostPanel, "左側 物件結果");
            leftDebugDisplayControl = CreateImageDisplayControl(leftDebugDisplayHostPanel, "左側 debug");
            rightOriginalDisplayControl = CreateImageDisplayControl(rightOriginalDisplayHostPanel, "右側 原圖");
            rightProcessedDisplayControl = CreateImageDisplayControl(rightProcessedDisplayHostPanel, "右側 處理後");
            rightObjectsDisplayControl = CreateImageDisplayControl(rightObjectsDisplayHostPanel, "右側 物件結果");
            rightDebugDisplayControl = CreateImageDisplayControl(rightDebugDisplayHostPanel, "右側 debug");

            WireImageDisplaySynchronization();
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
            leftProcessedDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftObjectsDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            leftDebugDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightOriginalDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightProcessedDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightObjectsDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;
            rightDebugDisplayControl.ViewChanged += ImageDisplayControl_ViewChanged;

            leftOriginalDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftProcessedDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftObjectsDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            leftDebugDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightOriginalDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightProcessedDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightObjectsDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;
            rightDebugDisplayControl.FitViewRequested += ImageDisplayControl_FitViewRequested;

            leftImageTabControl.SelectedIndexChanged += VisibleImageTabControl_SelectedIndexChanged;
            rightImageTabControl.SelectedIndexChanged += VisibleImageTabControl_SelectedIndexChanged;

            leftOriginalDisplayControl.RoiSelected += ImageDisplayControl_RoiSelected;
            rightOriginalDisplayControl.RoiSelected += ImageDisplayControl_RoiSelected;
            leftProcessedDisplayControl.LargeImageOverlayPaint += ProcessedDisplayControl_LargeImageOverlayPaint;
            rightProcessedDisplayControl.LargeImageOverlayPaint += ProcessedDisplayControl_LargeImageOverlayPaint;
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
                sharedImageViewState = source.ViewState;
                hasSharedImageViewState = true;
                target.ApplyViewState(source.ViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void VisibleImageTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVisibleProcessedImageIfNeeded();
            BeginInvoke(new Action(ApplySharedImageViewStateToVisibleControls));
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
            if (isSyncingImageView)
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
            if (isSyncingImageView)
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

            if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
            {
                return leftProcessedDisplayControl;
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

            if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
            {
                return rightProcessedDisplayControl;
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
            functionListBox.SelectedIndex = 0;
            statusLabel.Text = "介面框架準備就緒";
            BeginInvoke(new Action(async () => await RestoreSystemParametersAsync()));
        }

        private void FunctionListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedFunction = functionListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedFunction))
            {
                rightPanelTitleLabel.Text = "參數設定";
                return;
            }

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
            else if (selectedFunction == AddRoiMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "按下「新增 ROI」後，在左邊或右邊的原圖拖曳矩形。確認後會加入 ROI 清單並寫入 SystemParameters.ini。";
            }
            else if (IsRoiMenuItem(selectedFunction))
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = selectedFunction.Trim() + " 是目前選用的 ROI。影像處理會套用這個 ROI。";
            }
            else if (IsRoiCommandMenuItem(selectedFunction))
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "刪除目前選到的 ROI。";
            }
            else if (selectedFunction == ImageProcessingMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "展開「影像處理」後，可新增影像處理流程。";
            }
            else if (selectedFunction == AddImageProcessingMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "按下「新增影像處理」會新增一個尚未決定演算法的處理步驟。";
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

            statusLabel.Text = "目前選擇：" + selectedFunction;
        }

        private async void FunctionListBox_MouseClick(object sender, MouseEventArgs e)
        {
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
            else if (selectedFunction == AddRoiMenuText)
            {
                BeginAddRoiSelection();
            }
            else if (IsRoiMenuItem(selectedFunction))
            {
                ToggleRoiItemMenu(selectedFunction);
            }
            else if (IsRoiCommandMenuItem(selectedFunction))
            {
                HandleRoiCommand(selectedFunction);
            }
            else if (selectedFunction == ImageProcessingMenuText)
            {
                ToggleImageProcessingMenu();
            }
            else if (selectedFunction == AddImageProcessingMenuText)
            {
                AddImageProcessingStep();
            }
            else if (IsImageProcessingStepMenuItem(selectedFunction))
            {
                ToggleImageProcessingStepMenu(selectedFunction);
            }
            else if (IsImageProcessingStepCommandMenuItem(selectedFunction))
            {
                HandleImageProcessingStepCommand(selectedFunction);
            }
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
                    functionListBox.Items.Insert(roiIndex + 1, AddRoiMenuText);
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
                if (itemText == AddRoiMenuText || IsRoiMenuItem(itemText) || IsRoiCommandMenuItem(itemText))
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
            functionListBox.Items.Insert(insertIndex, AddRoiMenuText);
            insertIndex++;
            for (int index = 0; index < systemParameters.RoiRegions.Count; index++)
            {
                functionListBox.Items.Insert(insertIndex, CreateRoiText(index + 1));
                insertIndex++;
            }
        }

        private void ToggleRoiItemMenu(string roiText)
        {
            selectedRoiIndex = GetRoiIndex(roiText);
            ApplySelectedRoiOverlay();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();

            if (expandedRoiText == roiText)
            {
                RemoveRoiCommandMenuItems();
                return;
            }

            RemoveRoiCommandMenuItems();

            int roiItemIndex = functionListBox.Items.IndexOf(roiText);
            if (roiItemIndex >= 0)
            {
                functionListBox.Items.Insert(roiItemIndex + 1, DeleteRoiMenuText);
                expandedRoiText = roiText;
            }
        }

        private void RemoveRoiCommandMenuItems()
        {
            functionListBox.Items.Remove(DeleteRoiMenuText);
            expandedRoiText = null;
        }

        private void HandleRoiCommand(string selectedFunction)
        {
            if (selectedFunction == DeleteRoiMenuText)
            {
                DeleteSelectedRoi();
            }
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

        private static bool IsRoiCommandMenuItem(string menuText)
        {
            return menuText == DeleteRoiMenuText;
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
                    functionListBox.Items.Insert(imageProcessingIndex + 1, AddImageProcessingMenuText);
                    imageProcessingMenuExpanded = true;
                    RebuildVisibleImageProcessingSteps();
                }
            }
        }

        private void AddImageProcessingStep()
        {
            RemoveImageProcessingStepCommandMenuItems();

            systemParameters.ImageProcessingSteps.Add(new ImageProcessingStepSettings());
            SaveSystemParameters();
            string stepText = CreateImageProcessingStepText(systemParameters.ImageProcessingSteps.Count);
            int insertIndex = GetImageProcessingStepInsertIndex();
            functionListBox.Items.Insert(insertIndex, stepText);
            functionListBox.SelectedItem = stepText;
            statusLabel.Text = "已新增" + stepText.Trim();
        }

        private int GetImageProcessingStepInsertIndex()
        {
            int insertIndex = functionListBox.Items.IndexOf(AddImageProcessingMenuText) + 1;
            while (insertIndex < functionListBox.Items.Count &&
                IsImageProcessingStepMenuItem(functionListBox.Items[insertIndex] as string))
            {
                insertIndex++;
            }

            return insertIndex;
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
                if (itemText == AddImageProcessingMenuText || IsImageProcessingStepMenuItem(itemText))
                {
                    functionListBox.Items.RemoveAt(index);
                }
            }

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
            DialogResult result = MessageBox.Show(
                this,
                "是否要刪除該項處理？",
                "刪除影像處理",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                statusLabel.Text = "已取消刪除";
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();
            functionListBox.Items.Remove(stepText);
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                systemParameters.ImageProcessingSteps.RemoveAt(stepIndex);
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
                ToggleImageProcessingStepMenu(stepText);
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            systemParameters.ImageProcessingSteps.RemoveAt(stepIndex);
            systemParameters.ImageProcessingSteps.Insert(targetIndex, step);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            string movedStepText = CreateImageProcessingStepText(targetIndex + 1);
            functionListBox.SelectedItem = movedStepText;
            ToggleImageProcessingStepMenu(movedStepText);
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
            functionListBox.Items.Insert(insertIndex, AddImageProcessingMenuText);
            insertIndex++;
            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                functionListBox.Items.Insert(insertIndex, CreateImageProcessingStepText(index + 1));
                insertIndex++;
            }
        }

        private int GetImageProcessingStepIndex(string stepText)
        {
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

        private string CreateImageProcessingStepText(int stepNumber)
        {
            string method = string.Empty;
            int stepIndex = stepNumber - 1;
            if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                method = systemParameters.ImageProcessingSteps[stepIndex].Method;
            }

            string methodText = string.IsNullOrWhiteSpace(method) ? "未決定" : method;
            return "    處理" + stepNumber + "(" + methodText + ")";
        }

        private void ShowImageProcessingFlowTree(string stepText)
        {
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
            if (IsEdgeDetectionMethod(method))
            {
                ShowImageProcessingParameterPanel(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 參數";
            }
            else
            {
                HideImageProcessingParameterPanel();
                imageProcessingFlowTreeView.Visible = true;
                SelectImageProcessingFlowNode(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 流程";
            }

            statusLabel.Text = "目前選擇：" + stepText.Trim();
        }

        private void HideImageProcessingFlowTree()
        {
            selectedImageProcessingStepIndex = -1;
            parameterPlaceholderLabel.Visible = true;
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
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
            AddImageProcessingFlowNode("Morphology", "Erode", "Dilate", "Open", "Close", "Fill Hole");
            AddImageProcessingFlowNode("Contour Analysis", "Find Contours", "Contour Hierarchy");
            AddImageProcessingFlowNode("Feature Filter", "Area Filter", "Width / Height Filter", "Circularity Filter", "Position Filter");
            AddImageProcessingFlowNode("Object Selector", "Largest Object", "Smallest Object", "Object By Index", "Best Match Object");
            AddImageProcessingFlowNode("Object Result", "Bounding Box", "Center Point", "Area", "Width / Height", "Angle", "Count");
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
            if (IsEdgeDetectionMethod(method))
            {
                ShowImageProcessingParameterPanel(method);
                rightPanelTitleLabel.Text = "處理" + (selectedImageProcessingStepIndex + 1) + "(" + method + ") 參數";
            }

            statusLabel.Text = "已設定處理" + (selectedImageProcessingStepIndex + 1) + "：" + method;
        }

        private void UpdateVisibleImageProcessingStepText(int stepIndex)
        {
            string oldStepText = CreateImageProcessingStepText(stepIndex + 1);
            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                string itemText = functionListBox.Items[itemIndex] as string;
                if (GetImageProcessingStepIndex(itemText) == stepIndex)
                {
                    functionListBox.Items[itemIndex] = oldStepText;
                    functionListBox.SelectedIndex = itemIndex;
                    break;
                }
            }
        }

        private void ShowImageProcessingParameterPanel(string method)
        {
            EnsureImageProcessingParameterPanel();
            imageProcessingFlowTreeView.Visible = false;
            parameterPlaceholderLabel.Visible = false;
            imageProcessingParameterPanel.Visible = true;
            imageProcessingParameterPanel.Controls.Clear();

            isLoadingImageProcessingParameters = true;
            try
            {
                AddParameterHeader(method);
                AddReselectMethodButton();

                if (method == "Polarity Edge")
                {
                    AddComboParameter("Polarity", "邊緣方向", new[] { "BrightToDark", "DarkToBright", "Any" }, "Any");
                    AddNumericParameter("ContrastThreshold", "對比門檻", "20");
                    AddNumericParameter("EdgeWidth", "邊緣寬度", "3");
                    AddNumericParameter("Smoothing", "平滑", "1");
                    AddComboParameter("SearchDirection", "搜尋方向", new[] { "Horizontal", "Vertical", "Any" }, "Any");
                    AddComboParameter("EdgeSelection", "邊緣選取", new[] { "First", "Last", "Strongest", "All" }, "Strongest");
                    AddCheckParameter("SubPixel", "次像素定位", false);
                    AddNumericParameter("MinEdgeLength", "最小邊緣長度", "10");
                    AddNumericParameter("MaxGap", "最大斷點間距", "2");
                }
                else if (method == "Canny Edge")
                {
                    AddNumericParameter("LowThreshold", "低門檻", "50");
                    AddNumericParameter("HighThreshold", "高門檻", "150");
                    AddComboParameter("KernelSize", "核心大小", KernelSizeOptions, "3");
                    AddCheckParameter("L2Gradient", "L2 梯度", false);
                    AddComboParameter("GaussianBlurSize", "高斯模糊大小", KernelSizeOptions, "5");
                    AddNumericParameter("GaussianSigma", "高斯 Sigma", "1.4");
                    AddComboParameter("EdgeSelection", "邊緣選取", new[] { "All", "Strongest", "Longest" }, "All");
                    AddNumericParameter("MinEdgeLength", "最小邊緣長度", "10");
                    AddNumericParameter("MaxGap", "最大斷點間距", "2");
                }
                else if (method == "Sobel Edge")
                {
                    AddComboParameter("Direction", "方向", new[] { "Both", "X", "Y" }, "Both");
                    AddComboParameter("KernelSize", "核心大小", KernelSizeOptions, "3");
                    AddNumericParameter("Scale", "Scale", "1");
                    AddNumericParameter("Delta", "Delta", "0");
                    AddComboParameter("OutputMode", "輸出模式", new[] { "Magnitude", "Absolute", "XOnly", "YOnly" }, "Magnitude");
                    AddNumericParameter("Threshold", "邊緣門檻", "30");
                    AddComboParameter("EdgeSelection", "邊緣選取", new[] { "All", "Strongest", "Longest" }, "All");
                    AddNumericParameter("MinEdgeLength", "最小邊緣長度", "10");
                    AddNumericParameter("MaxGap", "最大斷點間距", "2");
                }
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
            var numericUpDown = new NumericUpDown();
            numericUpDown.Width = parameterPanel.Width - 18;
            numericUpDown.Minimum = minimum;
            numericUpDown.Maximum = maximum;
            numericUpDown.Increment = increment;
            numericUpDown.DecimalPlaces = key == "Scale" || key == "GaussianSigma" ? 2 : 0;
            numericUpDown.Value = Clamp(value, minimum, maximum);
            numericUpDown.Tag = key;
            numericUpDown.ValueChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    var control = (NumericUpDown)sender;
                    SaveImageProcessingParameter(control.Tag as string, control.Value.ToString(CultureInfo.InvariantCulture));
                }
            };
            numericUpDown.MouseWheel += NumericParameter_MouseWheel;

            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(numericUpDown);
        }

        private void AddComboParameter(string key, string labelText, string[] values, string defaultValue)
        {
            var label = CreateParameterLabel(labelText);
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
                    SaveImageProcessingParameter(((ComboBox)sender).Tag as string, ((ComboBox)sender).SelectedItem as string);
                }
            };

            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(comboBox);
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
                    SaveImageProcessingParameter(((CheckBox)sender).Tag as string, ((CheckBox)sender).Checked ? "true" : "false");
                }
            };
            imageProcessingParameterPanel.Controls.Add(checkBox);
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
            else if (key == "EdgeWidth")
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
            Dictionary<string, string> parameters = ParseImageProcessingParameters(GetSelectedImageProcessingStepParameters());
            string value;
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
            parameters[key] = value ?? string.Empty;
            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters = FormatImageProcessingParameters(parameters);
            SaveSystemParameters();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已更新處理" + (selectedImageProcessingStepIndex + 1) + " 參數";
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
                return "Polarity=Any;ContrastThreshold=20;EdgeWidth=3;Smoothing=1;SearchDirection=Any;EdgeSelection=Strongest;SubPixel=false;MinEdgeLength=10;MaxGap=2";
            }

            if (method == "Canny Edge")
            {
                return "LowThreshold=50;HighThreshold=150;KernelSize=3;L2Gradient=false;GaussianBlurSize=5;GaussianSigma=1.4;EdgeSelection=All;MinEdgeLength=10;MaxGap=2";
            }

            if (method == "Sobel Edge")
            {
                return "Direction=Both;KernelSize=3;Scale=1;Delta=0;OutputMode=Magnitude;Threshold=30;EdgeSelection=All;MinEdgeLength=10;MaxGap=2";
            }

            return string.Empty;
        }

        private static bool IsEdgeDetectionMethod(string method)
        {
            return method == "Polarity Edge" ||
                method == "Canny Edge" ||
                method == "Sobel Edge";
        }

        private bool HasPreviewableImageProcessingStep()
        {
            return FindFirstPreviewableImageProcessingStepIndex() >= 0;
        }

        private void MarkProcessedImageDirty()
        {
            processedImageDirty = true;
            ClearLargeProcessedOverlayCache();
            if (latestProcessedImage != null)
            {
                latestProcessedImage.Dispose();
                latestProcessedImage = null;
            }
        }

        private void ClearLargeProcessedOverlayCache()
        {
            ClearLargeProcessedOverlayBitmapsOnly();
            lock (largeProcessedMaskLock)
            {
                latestLargeProcessedMask = null;
                latestLargeProcessedMaskRoi = Rectangle.Empty;
                latestLargeProcessedMaskKey = null;
                isLargeProcessedMaskBuilding = false;
                largeProcessedMaskGeneration++;
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
            if (!IsAnyProcessedTabVisible() || imageProcessingDebounceTimer == null)
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
            if (!IsAnyProcessedTabVisible())
            {
                return;
            }

            BeginInvoke(new Action(async () => await UpdateVisibleProcessedImageIfNeededAsync()));
        }

        private async Task UpdateVisibleProcessedImageIfNeededAsync()
        {
            if (!IsAnyProcessedTabVisible())
            {
                return;
            }

            // Large images use LargeImageSource plus a cached ROI mask instead of
            // latestProcessedImage. Treat that pipeline as complete once it has
            // been prepared; otherwise every refresh would start it again.
            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                if (processedImageDirty)
                {
                    statusLabel.Text = "影像處理運算中...";
                    PrepareLargeProcessedPreview();
                    processedImageDirty = false;
                }

                return;
            }

            if (processedImageDirty || latestProcessedImage == null)
            {
                statusLabel.Text = "影像處理運算中...";

                Bitmap processedImage = await Task.Run(() => CreateCurrentProcessedImage());
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

            ApplyLatestProcessedImageToVisibleTabs();
            statusLabel.Text = "影像處理完成";
        }

        private bool IsAnyProcessedTabVisible()
        {
            return leftImageTabControl.SelectedTab == leftProcessedTabPage ||
                rightImageTabControl.SelectedTab == rightProcessedTabPage;
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

            Rectangle? roi = GetSelectedRoi();
            if (!roi.HasValue || selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count ||
                !IsEdgeDetectionMethod(systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method))
            {
                ClearProcessedPreviewImages();
                return;
            }

            isSyncingImageView = true;
            LargeImageSource sharedSource = null;
            try
            {
                sharedSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (sharedSource == null)
                {
                    ClearProcessedPreviewImages();
                    return;
                }

                if (!leftProcessedDisplayControl.IsLargeImageMode)
                {
                    leftProcessedDisplayControl.SetSharedLargeImageSource(sharedSource);
                }

                if (!rightProcessedDisplayControl.IsLargeImageMode)
                {
                    rightProcessedDisplayControl.SetSharedLargeImageSource(sharedSource);
                }

                leftProcessedDisplayControl.SetRoiOverlay(roi.Value);
                rightProcessedDisplayControl.SetRoiOverlay(roi.Value);
                StartLargeProcessedMaskBuild(sharedSource, roi.Value, systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex]);
            }
            finally
            {
                if (sharedSource != null)
                {
                    sharedSource.ReleaseReference();
                }

                isSyncingImageView = false;
            }

            ApplySharedImageViewStateToVisibleControls();
        }

        private void StartLargeProcessedMaskBuild(LargeImageSource source, Rectangle roi, ImageProcessingStepSettings step)
        {
            if (source == null || roi.Width <= 0 || roi.Height <= 0 || step == null || !IsEdgeDetectionMethod(step.Method))
            {
                return;
            }

            string maskKey = CreateLargeProcessedMaskKey(roi, step);
            lock (largeProcessedMaskLock)
            {
                if (string.Equals(latestLargeProcessedMaskKey, maskKey, StringComparison.Ordinal) &&
                    latestLargeProcessedMask != null && !isLargeProcessedMaskBuilding)
                {
                    return;
                }

                if (isLargeProcessedMaskBuilding && string.Equals(latestLargeProcessedMaskKey, maskKey, StringComparison.Ordinal))
                {
                    return;
                }

                latestLargeProcessedMask = null;
                latestLargeProcessedMaskRoi = roi;
                latestLargeProcessedMaskKey = maskKey;
                isLargeProcessedMaskBuilding = true;
                largeProcessedMaskGeneration++;
            }

            int generation;
            lock (largeProcessedMaskLock)
            {
                generation = largeProcessedMaskGeneration;
            }

            string method = step.Method;
            string parameters = step.Parameters;
            LargeImageSource sharedSource = source.AddReference();
            statusLabel.Text = "影像處理運算中...建立 ROI Mask";

            Task.Run(
                delegate
                {
                    bool[,] mask = null;
                    try
                    {
                        Dictionary<string, string> parsedParameters = ParseImageProcessingParameters(parameters);

                        // Most ROIs are small compared with the source image. Process
                        // those in one pass so the edge algorithm runs once and does
                        // not pay the per-chunk bitmap/array setup cost.
                        if ((long)roi.Width * roi.Height <= MaxSinglePassLargeRoiPixels)
                        {
                            using (Bitmap roiImage = sharedSource.CreateRegionBitmapFromTiles(roi))
                            {
                                mask = CreateEdgeMask(roiImage, method, parsedParameters);
                            }

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
                                        !string.Equals(latestLargeProcessedMaskKey, maskKey, StringComparison.Ordinal))
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
                                                        !string.Equals(latestLargeProcessedMaskKey, maskKey, StringComparison.Ordinal))
                                                    {
                                                        return;
                                                    }

                                                    // Publish completed chunks immediately. The paint path only reads
                                                    // the already-written cells, so large ROIs become visible while
                                                    // the remaining chunks continue in the background.
                                                    if (publishPartialMask)
                                                    {
                                                        latestLargeProcessedMask = mask;
                                                        latestLargeProcessedMaskRoi = roi;
                                                    }
                                                }

                                                statusLabel.Text = "影像處理運算中...ROI Mask " +
                                                    progress.ToString(CultureInfo.InvariantCulture) + "/" +
                                                    totalChunks.ToString(CultureInfo.InvariantCulture);
                                                if (publishPartialMask)
                                                {
                                                    leftProcessedDisplayControl.InvalidateImageView();
                                                    rightProcessedDisplayControl.InvalidateImageView();
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
                                            !string.Equals(latestLargeProcessedMaskKey, maskKey, StringComparison.Ordinal))
                                        {
                                            return;
                                        }

                                        latestLargeProcessedMask = mask;
                                        latestLargeProcessedMaskRoi = roi;
                                        isLargeProcessedMaskBuilding = false;
                                        mask = null;
                                    }

                                    statusLabel.Text = "大圖 ROI Mask 建立完成";
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
                                    lock (largeProcessedMaskLock)
                                    {
                                        if (generation == largeProcessedMaskGeneration)
                                        {
                                            isLargeProcessedMaskBuilding = false;
                                        }
                                    }

                                    statusLabel.Text = "大圖 ROI Mask 建立失敗：" + ex.Message;
                                    leftProcessedDisplayControl.InvalidateImageView();
                                    rightProcessedDisplayControl.InvalidateImageView();
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
                                !string.Equals(latestLargeProcessedMaskKey, maskKey, StringComparison.Ordinal))
                            {
                                return;
                            }

                            latestLargeProcessedMask = mask;
                            latestLargeProcessedMaskRoi = roi;
                            isLargeProcessedMaskBuilding = false;
                        }

                        statusLabel.Text = "大圖 ROI Mask 建立完成";
                        leftProcessedDisplayControl.InvalidateImageView();
                        rightProcessedDisplayControl.InvalidateImageView();
                    }));
        }

        private string CreateLargeProcessedMaskKey(Rectangle roi, ImageProcessingStepSettings step)
        {
            return string.Join(
                "|",
                step.Method,
                step.Parameters,
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

            int edgeWidth = EnsureOdd(GetIntParameter(parameters, "EdgeWidth", 3));
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
            Rectangle? selectedRoi = GetSelectedRoi();
            if (!selectedRoi.HasValue ||
                selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex];
            if (!IsEdgeDetectionMethod(step.Method))
            {
                return;
            }

            Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, selectedRoi.Value);
            if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
            {
                return;
            }

            bool[,] mask;
            Rectangle maskRoi;
            bool isBuilding;
            lock (largeProcessedMaskLock)
            {
                mask = latestLargeProcessedMask;
                maskRoi = latestLargeProcessedMaskRoi;
                isBuilding = isLargeProcessedMaskBuilding;
            }

            if (mask == null || !maskRoi.Equals(selectedRoi.Value))
            {
                if (!isBuilding)
                {
                    StartLargeProcessedMaskBuild(e.Source, selectedRoi.Value, step);
                }

                return;
            }

            int startTileX = (visibleRoi.Left / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            int endTileX = ((visibleRoi.Right + LargeProcessedOverlayTileSize - 1) / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            int startTileY = (visibleRoi.Top / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;
            int endTileY = ((visibleRoi.Bottom + LargeProcessedOverlayTileSize - 1) / LargeProcessedOverlayTileSize) * LargeProcessedOverlayTileSize;

            for (int tileY = startTileY; tileY < endTileY; tileY += LargeProcessedOverlayTileSize)
            {
                for (int tileX = startTileX; tileX < endTileX; tileX += LargeProcessedOverlayTileSize)
                {
                    Rectangle tileRect = Rectangle.Intersect(
                        visibleRoi,
                        new Rectangle(tileX, tileY, LargeProcessedOverlayTileSize, LargeProcessedOverlayTileSize));
                    if (tileRect.Width <= 0 || tileRect.Height <= 0)
                    {
                        continue;
                    }

                    Bitmap overlay;
                    string cacheKey = CreateLargeProcessedOverlayCacheKey(tileRect, step);
                    if (TryGetLargeProcessedOverlayFromCache(cacheKey, out overlay))
                    {
                        DrawLargeProcessedOverlayTile(e.Graphics, overlay, tileRect, e.Zoom, e.Offset);
                    }
                    else
                    {
                        overlay = CreateRedOverlayTileFromMask(mask, maskRoi, tileRect);
                        if (largeProcessedOverlayCache.Count >= MaxLargeProcessedOverlayCacheCount)
                        {
                            ClearLargeProcessedOverlayBitmapsOnly();
                        }

                        largeProcessedOverlayCache[cacheKey] = overlay;
                        DrawLargeProcessedOverlayTile(e.Graphics, overlay, tileRect, e.Zoom, e.Offset);
                    }
                }
            }
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

        private string CreateLargeProcessedOverlayCacheKey(Rectangle tileRect, ImageProcessingStepSettings step)
        {
            return string.Join(
                "|",
                step.Method,
                step.Parameters,
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
            statusLabel.Text = "影像處理運算中...等待 " + pendingLargeProcessedOverlayTiles.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊";

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
                                    if (largeProcessedOverlayCache.Count >= MaxLargeProcessedOverlayCacheCount)
                                    {
                                        ClearLargeProcessedOverlayBitmapsOnly();
                                    }

                                    largeProcessedOverlayCache[cacheKey] = overlay;
                                    overlay = null;
                                    statusLabel.Text = pendingLargeProcessedOverlayTiles.Count > 0
                                        ? "影像處理運算中...等待 " + pendingLargeProcessedOverlayTiles.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊"
                                        : "影像處理完成，已顯示 " + largeProcessedOverlayCache.Count.ToString(CultureInfo.InvariantCulture) + " 個區塊";
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
            try
            {
                graphics.DrawImage(overlay, destination, source, GraphicsUnit.Pixel);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private Bitmap CreateCurrentProcessedImage()
        {
            Rectangle? selectedRoi = GetSelectedRoi();
            if (!selectedRoi.HasValue ||
                selectedRoi.Value.Width <= 0 ||
                selectedRoi.Value.Height <= 0 ||
                selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return null;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex];
            if (!IsEdgeDetectionMethod(step.Method))
            {
                return null;
            }

            using (Bitmap sourceImage = rightOriginalDisplayControl.CloneImage())
            {
                if (sourceImage == null)
                {
                    return null;
                }

                Rectangle roi = Rectangle.Intersect(selectedRoi.Value, new Rectangle(0, 0, sourceImage.Width, sourceImage.Height));
                if (roi.Width <= 0 || roi.Height <= 0)
                {
                    return null;
                }

                using (Bitmap roiImage = sourceImage.Clone(roi, PixelFormat.Format24bppRgb))
                {
                    bool[,] mask = CreateEdgeMask(roiImage, step.Method, ParseImageProcessingParameters(step.Parameters));
                    return CreateRedOverlayImage(sourceImage, roi, mask);
                }
            }
        }

        private static bool[,] CreateEdgeMask(Bitmap image, string method, Dictionary<string, string> parameters)
        {
            byte[,] gray = CreateGrayValues(image);
            if (method == "Canny Edge")
            {
                int lowThreshold = GetIntParameter(parameters, "LowThreshold", 50);
                int highThreshold = GetIntParameter(parameters, "HighThreshold", 150);
                int kernelSize = GetIntParameter(parameters, "KernelSize", 3);
                bool l2Gradient = GetBoolParameter(parameters, "L2Gradient", false);
                int gaussianBlurSize = GetIntParameter(parameters, "GaussianBlurSize", 5);
                double gaussianSigma = GetDoubleParameter(parameters, "GaussianSigma", 1.4);
                string edgeSelection = GetStringParameter(parameters, "EdgeSelection", "All");
                int minEdgeLength = GetIntParameter(parameters, "MinEdgeLength", 10);
                int maxGap = GetIntParameter(parameters, "MaxGap", 2);
                return CreateSimpleCannyMask(
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

            if (method == "Sobel Edge")
            {
                int kernelSize = GetIntParameter(parameters, "KernelSize", 3);
                double scale = GetDoubleParameter(parameters, "Scale", 1);
                int delta = GetIntParameter(parameters, "Delta", 0);
                string direction = GetStringParameter(parameters, "Direction", "Both");
                string outputMode = GetStringParameter(parameters, "OutputMode", "Magnitude");
                int threshold = GetIntParameter(parameters, "Threshold", 30);
                string edgeSelection = GetStringParameter(parameters, "EdgeSelection", "All");
                int minEdgeLength = GetIntParameter(parameters, "MinEdgeLength", 10);
                int maxGap = GetIntParameter(parameters, "MaxGap", 2);
                return CreateSobelMask(
                    gray,
                    threshold,
                    direction,
                    kernelSize,
                    scale,
                    delta,
                    outputMode,
                    edgeSelection,
                    minEdgeLength,
                    maxGap);
            }

            int contrastThreshold = GetIntParameter(parameters, "ContrastThreshold", 20);
            int edgeWidth = GetIntParameter(parameters, "EdgeWidth", 3);
            int smoothing = GetIntParameter(parameters, "Smoothing", 1);
            string polarity = GetStringParameter(parameters, "Polarity", "Any");
            string searchDirection = GetStringParameter(parameters, "SearchDirection", "Any");
            string polarityEdgeSelection = GetStringParameter(parameters, "EdgeSelection", "Strongest");
            int polarityMinEdgeLength = GetIntParameter(parameters, "MinEdgeLength", 10);
            int polarityMaxGap = GetIntParameter(parameters, "MaxGap", 2);
            bool subPixel = GetBoolParameter(parameters, "SubPixel", false);
            return CreatePolarityEdgeMask(
                gray,
                contrastThreshold,
                edgeWidth,
                smoothing,
                polarity,
                searchDirection,
                polarityEdgeSelection,
                polarityMinEdgeLength,
                polarityMaxGap,
                subPixel);
        }

        private static byte[,] CreateGrayValues(Bitmap image)
        {
            var gray = new byte[image.Width, image.Height];
            using (var readable = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(readable))
                {
                    graphics.DrawImageUnscaled(image, 0, 0);
                }

                BitmapData data = readable.LockBits(new Rectangle(0, 0, readable.Width, readable.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    int rowBytes = readable.Width * 4;
                    byte[] row = new byte[rowBytes];
                    for (int y = 0; y < readable.Height; y++)
                    {
                        Marshal.Copy(data.Scan0 + (y * stride), row, 0, rowBytes);
                        for (int x = 0; x < readable.Width; x++)
                        {
                            int offset = x * 4;
                            gray[x, y] = (byte)((row[offset + 2] + row[offset + 1] + row[offset]) / 3);
                        }
                    }
                }
                finally
                {
                    readable.UnlockBits(data);
                }
            }

            return gray;
        }

        private static bool[,] CreateSimpleCannyMask(
            byte[,] gray,
            int lowThreshold,
            int highThreshold,
            int kernelSize,
            bool l2Gradient,
            int gaussianBlurSize,
            double gaussianSigma,
            string edgeSelection,
            int minEdgeLength,
            int maxGap)
        {
            byte[,] blurredGray = ApplyGaussianBlur(gray, gaussianBlurSize, gaussianSigma);
            int[,] magnitudes = CreateSobelMagnitudes(blurredGray, kernelSize, l2Gradient, "Both");
            int width = gray.GetLength(0);
            int height = gray.GetLength(1);
            var result = new bool[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    bool strong = magnitudes[x, y] >= highThreshold;
                    bool weakConnected = magnitudes[x, y] >= lowThreshold && HasNeighborAboveThreshold(magnitudes, x, y, highThreshold);
                    if (strong || weakConnected)
                    {
                        result[x, y] = true;
                    }
                }
            }

            result = BridgeSmallGaps(result, maxGap);
            result = FilterEdgeComponents(result, magnitudes, edgeSelection, minEdgeLength);
            return result;
        }

        private static bool[,] CreateSobelMask(byte[,] gray, int threshold, string direction)
        {
            return CreateSobelMask(gray, threshold, direction, 3, 1, 0, "Magnitude", "All", 0, 0);
        }

        private static bool[,] CreateSobelMask(
            byte[,] gray,
            int threshold,
            string direction,
            int kernelSize,
            double scale,
            int delta,
            string outputMode,
            string edgeSelection,
            int minEdgeLength,
            int maxGap)
        {
            int width = gray.GetLength(0);
            int height = gray.GetLength(1);
            string magnitudeDirection = string.Equals(outputMode, "XOnly", StringComparison.OrdinalIgnoreCase)
                ? "X"
                : string.Equals(outputMode, "YOnly", StringComparison.OrdinalIgnoreCase) ? "Y" : direction;
            bool l2Gradient = string.Equals(outputMode, "Magnitude", StringComparison.OrdinalIgnoreCase);
            int[,] magnitudes = CreateSobelMagnitudes(gray, kernelSize, l2Gradient, magnitudeDirection);
            var mask = new bool[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int scaledMagnitude = ClampInt((int)Math.Round((magnitudes[x, y] * scale) + delta), 0, int.MaxValue);
                    mask[x, y] = scaledMagnitude >= threshold;
                    magnitudes[x, y] = scaledMagnitude;
                }
            }

            mask = BridgeSmallGaps(mask, maxGap);
            return FilterEdgeComponents(mask, magnitudes, edgeSelection, minEdgeLength);
        }

        private static bool[,] CreatePolarityEdgeMask(
            byte[,] gray,
            int contrastThreshold,
            int edgeWidth,
            int smoothing,
            string polarity,
            string searchDirection,
            string edgeSelection,
            int minEdgeLength,
            int maxGap,
            bool subPixel)
        {
            byte[,] workingGray = ApplyGaussianBlur(gray, smoothing, Math.Max(0.1, smoothing / 2.0));
            int kernelSize = Math.Max(3, edgeWidth | 1);
            int[,] magnitudes = CreateSobelMagnitudes(workingGray, kernelSize, subPixel, searchDirection);
            int width = gray.GetLength(0);
            int height = gray.GetLength(1);
            var mask = new bool[width, height];
            int radius = Math.Max(1, edgeWidth / 2);
            for (int y = radius; y < height - radius; y++)
            {
                for (int x = radius; x < width - radius; x++)
                {
                    int before = GetAverageGray(workingGray, x, y, radius, searchDirection, true);
                    int after = GetAverageGray(workingGray, x, y, radius, searchDirection, false);
                    int contrast = after - before;
                    bool polarityAccepted =
                        string.Equals(polarity, "Any", StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(polarity, "BrightToDark", StringComparison.OrdinalIgnoreCase) && contrast < 0) ||
                        (string.Equals(polarity, "DarkToBright", StringComparison.OrdinalIgnoreCase) && contrast > 0);
                    mask[x, y] = polarityAccepted && Math.Abs(contrast) >= contrastThreshold;
                    magnitudes[x, y] = Math.Max(magnitudes[x, y], Math.Abs(contrast));
                }
            }

            mask = BridgeSmallGaps(mask, maxGap);
            return FilterEdgeComponents(mask, magnitudes, edgeSelection, minEdgeLength);
        }

        private static int GetAverageGray(byte[,] gray, int x, int y, int radius, string direction, bool before)
        {
            int sum = 0;
            int count = 0;
            for (int offset = 1; offset <= radius; offset++)
            {
                int sampleX = x;
                int sampleY = y;
                int signedOffset = before ? -offset : offset;
                if (string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase))
                {
                    sampleX = x + signedOffset;
                }
                else if (string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase))
                {
                    sampleY = y + signedOffset;
                }
                else
                {
                    sampleX = x + signedOffset;
                    sampleY = y + signedOffset;
                }

                sampleX = ClampInt(sampleX, 0, gray.GetLength(0) - 1);
                sampleY = ClampInt(sampleY, 0, gray.GetLength(1) - 1);
                sum += gray[sampleX, sampleY];
                count++;
            }

            return count > 0 ? sum / count : gray[x, y];
        }

        private static int[,] CreateSobelMagnitudes(byte[,] gray, int kernelSize, bool l2Gradient, string direction)
        {
            int width = gray.GetLength(0);
            int height = gray.GetLength(1);
            var magnitudes = new int[width, height];
            int radius = Math.Max(1, kernelSize / 2);
            for (int y = radius; y < height - radius; y++)
            {
                for (int x = radius; x < width - radius; x++)
                {
                    int gx = 0;
                    int gy = 0;
                    for (int offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        for (int offsetX = -radius; offsetX <= radius; offsetX++)
                        {
                            int value = gray[x + offsetX, y + offsetY];
                            gx += value * offsetX;
                            gy += value * offsetY;
                        }
                    }

                    if (string.Equals(direction, "X", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase))
                    {
                        magnitudes[x, y] = Math.Abs(gx);
                    }
                    else if (string.Equals(direction, "Y", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase))
                    {
                        magnitudes[x, y] = Math.Abs(gy);
                    }
                    else if (l2Gradient)
                    {
                        magnitudes[x, y] = (int)Math.Sqrt((gx * gx) + (gy * gy));
                    }
                    else
                    {
                        magnitudes[x, y] = Math.Abs(gx) + Math.Abs(gy);
                    }
                }
            }

            return magnitudes;
        }

        private static byte[,] ApplyGaussianBlur(byte[,] gray, int blurSize, double sigma)
        {
            int width = gray.GetLength(0);
            int height = gray.GetLength(1);
            int radius = Math.Max(0, blurSize / 2);
            if (radius == 0)
            {
                return gray;
            }

            double sigmaFactor = Math.Max(0.1, sigma);
            var blurred = new byte[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double weightedSum = 0;
                    double weightSum = 0;
                    for (int offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        for (int offsetX = -radius; offsetX <= radius; offsetX++)
                        {
                            int sampleX = ClampInt(x + offsetX, 0, width - 1);
                            int sampleY = ClampInt(y + offsetY, 0, height - 1);
                            double distance = (offsetX * offsetX) + (offsetY * offsetY);
                            double weight = Math.Exp(-distance / (2 * sigmaFactor * sigmaFactor));
                            weightedSum += gray[sampleX, sampleY] * weight;
                            weightSum += weight;
                        }
                    }

                    blurred[x, y] = (byte)ClampInt((int)Math.Round(weightedSum / weightSum), 0, 255);
                }
            }

            return blurred;
        }

        private static bool HasNeighborAboveThreshold(int[,] magnitudes, int x, int y, int threshold)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if ((offsetX != 0 || offsetY != 0) && magnitudes[x + offsetX, y + offsetY] >= threshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool[,] BridgeSmallGaps(bool[,] mask, int maxGap)
        {
            if (maxGap <= 0)
            {
                return mask;
            }

            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            var bridged = (bool[,])mask.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        continue;
                    }

                    if (HasEdgePairWithinGap(mask, x, y, maxGap, true) ||
                        HasEdgePairWithinGap(mask, x, y, maxGap, false))
                    {
                        bridged[x, y] = true;
                    }
                }
            }

            return bridged;
        }

        private static bool HasEdgePairWithinGap(bool[,] mask, int x, int y, int maxGap, bool horizontal)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            for (int gap = 1; gap <= maxGap; gap++)
            {
                int beforeX = horizontal ? x - gap : x;
                int beforeY = horizontal ? y : y - gap;
                int afterX = horizontal ? x + gap : x;
                int afterY = horizontal ? y : y + gap;
                if (beforeX >= 0 && beforeY >= 0 && afterX < width && afterY < height &&
                    mask[beforeX, beforeY] && mask[afterX, afterY])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool[,] FilterEdgeComponents(bool[,] mask, int[,] magnitudes, string edgeSelection, int minEdgeLength)
        {
            var components = GetEdgeComponents(mask, magnitudes, Math.Max(0, minEdgeLength));
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            var filtered = new bool[width, height];
            if (components.Count == 0)
            {
                return filtered;
            }

            if (string.Equals(edgeSelection, "Strongest", StringComparison.OrdinalIgnoreCase))
            {
                components.Sort((left, right) => right.Strength.CompareTo(left.Strength));
                PaintComponent(filtered, components[0]);
                return filtered;
            }

            if (string.Equals(edgeSelection, "First", StringComparison.OrdinalIgnoreCase))
            {
                components.Sort(CompareComponentsByPosition);
                PaintComponent(filtered, components[0]);
                return filtered;
            }

            if (string.Equals(edgeSelection, "Last", StringComparison.OrdinalIgnoreCase))
            {
                components.Sort(CompareComponentsByPosition);
                PaintComponent(filtered, components[components.Count - 1]);
                return filtered;
            }

            if (string.Equals(edgeSelection, "Longest", StringComparison.OrdinalIgnoreCase))
            {
                components.Sort((left, right) => right.Points.Count.CompareTo(left.Points.Count));
                PaintComponent(filtered, components[0]);
                return filtered;
            }

            foreach (EdgeComponent component in components)
            {
                PaintComponent(filtered, component);
            }

            return filtered;
        }

        private static List<EdgeComponent> GetEdgeComponents(bool[,] mask, int[,] magnitudes, int minEdgeLength)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            var visited = new bool[width, height];
            var components = new List<EdgeComponent>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[x, y] || visited[x, y])
                    {
                        continue;
                    }

                    EdgeComponent component = CollectEdgeComponent(mask, magnitudes, visited, x, y);
                    if (component.Points.Count >= minEdgeLength)
                    {
                        components.Add(component);
                    }
                }
            }

            return components;
        }

        private static int CompareComponentsByPosition(EdgeComponent left, EdgeComponent right)
        {
            Point leftPoint = left.FirstPoint;
            Point rightPoint = right.FirstPoint;
            int yComparison = leftPoint.Y.CompareTo(rightPoint.Y);
            return yComparison != 0 ? yComparison : leftPoint.X.CompareTo(rightPoint.X);
        }

        private static EdgeComponent CollectEdgeComponent(bool[,] mask, int[,] magnitudes, bool[,] visited, int startX, int startY)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            var component = new EdgeComponent();
            var queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                component.Points.Add(point);
                component.Strength += magnitudes[point.X, point.Y];
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        int nextX = point.X + offsetX;
                        int nextY = point.Y + offsetY;
                        if (nextX >= 0 && nextY >= 0 && nextX < width && nextY < height &&
                            mask[nextX, nextY] && !visited[nextX, nextY])
                        {
                            visited[nextX, nextY] = true;
                            queue.Enqueue(new Point(nextX, nextY));
                        }
                    }
                }
            }

            return component;
        }

        private static void PaintComponent(bool[,] target, EdgeComponent component)
        {
            foreach (Point point in component.Points)
            {
                target[point.X, point.Y] = true;
            }
        }

        private static bool HasNeighbor(bool[,] mask, int x, int y)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if ((offsetX != 0 || offsetY != 0) && mask[x + offsetX, y + offsetY])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Bitmap CreateRedOverlayImage(Bitmap sourceImage, Rectangle roi, bool[,] mask)
        {
            var result = new Bitmap(sourceImage);
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

            return result;
        }

        private static int GetIntParameter(Dictionary<string, string> parameters, string key, int defaultValue)
        {
            string value;
            int parsedValue;
            return parameters.TryGetValue(key, out value) && int.TryParse(value, out parsedValue)
                ? parsedValue
                : defaultValue;
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

        private class EdgeComponent
        {
            public EdgeComponent()
            {
                Points = new List<Point>();
            }

            public List<Point> Points { get; private set; }

            public int Strength { get; set; }

            public Point FirstPoint
            {
                get
                {
                    Point firstPoint = Points.Count > 0 ? Points[0] : Point.Empty;
                    foreach (Point point in Points)
                    {
                        if (point.Y < firstPoint.Y || (point.Y == firstPoint.Y && point.X < firstPoint.X))
                        {
                            firstPoint = point;
                        }
                    }

                    return firstPoint;
                }
            }
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
                rightOriginalDisplayControl.ClearRoiOverlay();
                leftProcessedDisplayControl.ClearRoiOverlay();
                rightProcessedDisplayControl.ClearRoiOverlay();
                return;
            }

            rightOriginalDisplayControl.SetRoiOverlay(roi.Value);
            if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
            {
                leftProcessedDisplayControl.SetRoiOverlay(roi.Value);
            }

            if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
            {
                rightProcessedDisplayControl.SetRoiOverlay(roi.Value);
            }
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
            await PrepareProcessedPreviewFromSettingsAsync();
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
                if (IsEdgeDetectionMethod(systemParameters.ImageProcessingSteps[index].Method))
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
                    leftProcessedDisplayControl.ClearRoiOverlay();
                    rightProcessedDisplayControl.ClearRoiOverlay();
                    systemParameters.LastImagePath = dialog.FileName;
                    systemParameters.RoiEnabled = false;
                    systemParameters.Roi = Rectangle.Empty;
                    systemParameters.RoiRegions.Clear();
                    selectedRoiIndex = -1;
                    RebuildVisibleRoiItems();
                    MarkProcessedImageDirty();

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
            Size imageSize = await Task.Run(() => LargeImageSource.ReadImageSize(filePath), cancellationToken);
            long sourcePixels = (long)imageSize.Width * imageSize.Height;
            if (sourcePixels > 50000000L)
            {
                var sharedSource = await Task.Run(() => new LargeImageSource(filePath), cancellationToken);
                try
                {
                    leftOriginalDisplayControl.SetSharedLargeImageSource(sharedSource);
                    rightOriginalDisplayControl.SetSharedLargeImageSource(sharedSource);
                    statusLabel.Text = "已載入大圖共用切圖來源";
                }
                finally
                {
                    sharedSource.ReleaseReference();
                }

                return;
            }

            await leftOriginalDisplayControl.LoadImageFromFileAsync(filePath, cancellationToken);
            await rightOriginalDisplayControl.LoadImageFromFileAsync(filePath, cancellationToken);
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

            Rectangle textBounds = new Rectangle(
                e.Bounds.Left + GetFunctionMenuIndent(functionListBox.Items[e.Index].ToString()),
                e.Bounds.Top,
                e.Bounds.Width - 24,
                e.Bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                functionListBox.Items[e.Index].ToString().Trim(),
                e.Font,
                textBounds,
                foreColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private static int GetFunctionMenuIndent(string menuText)
        {
            if (IsImageProcessingStepCommandMenuItem(menuText) || IsRoiCommandMenuItem(menuText))
            {
                return 66;
            }

            if (IsImageProcessingStepMenuItem(menuText) || IsRoiMenuItem(menuText))
            {
                return 48;
            }

            if (menuText == AddRoiMenuText ||
                menuText == AddImageProcessingMenuText)
            {
                return 30;
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
            return trimmedText.StartsWith("處理", StringComparison.Ordinal) &&
                suffixStartIndex > prefixLength &&
                trimmedText.EndsWith(")", StringComparison.Ordinal);
        }
    }
}
