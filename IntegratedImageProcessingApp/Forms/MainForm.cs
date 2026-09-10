using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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

        private const string LoadImageMenuText = "讀取圖片";
        private const string RoiMenuText = "指定 ROI";
        private const string SetRoiMenuText = "  設定 ROI";
        private const string CancelRoiMenuText = "  取消 ROI";

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

            leftImageTabControl.SelectedIndexChanged += VisibleImageTabControl_SelectedIndexChanged;
            rightImageTabControl.SelectedIndexChanged += VisibleImageTabControl_SelectedIndexChanged;

            leftOriginalDisplayControl.RoiSelected += ImageDisplayControl_RoiSelected;
            rightOriginalDisplayControl.RoiSelected += ImageDisplayControl_RoiSelected;
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
                target.ApplyViewState(source.ViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void VisibleImageTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncVisibleImageDisplaysFromLeft();
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
                rightVisible.ApplyViewState(leftVisible.ViewState);
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
            else if (selectedFunction == RoiMenuText || selectedFunction == SetRoiMenuText)
            {
                parameterPlaceholderLabel.Text = "展開「指定 ROI」後點選「設定 ROI」，在左邊或右邊的原圖拖曳矩形。確認後 ROI 只會保留在右邊原圖，並寫入 SystemParameters.ini。";
            }
            else if (selectedFunction == CancelRoiMenuText)
            {
                parameterPlaceholderLabel.Text = "取消目前保存的 ROI，並更新 SystemParameters.ini。";
            }
            else
            {
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
            else if (selectedFunction == SetRoiMenuText)
            {
                BeginRoiSelection();
            }
            else if (selectedFunction == CancelRoiMenuText)
            {
                CancelSavedRoi();
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
                    functionListBox.Items.Insert(roiIndex + 1, SetRoiMenuText);
                    functionListBox.Items.Insert(roiIndex + 2, CancelRoiMenuText);
                    roiMenuExpanded = true;
                }
            }
        }

        private void RemoveRoiSubMenuItems()
        {
            functionListBox.Items.Remove(SetRoiMenuText);
            functionListBox.Items.Remove(CancelRoiMenuText);
            roiMenuExpanded = false;
        }

        private void BeginRoiSelection()
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
            rightOriginalDisplayControl.SetRoiOverlay(e.Roi);
            systemParameters.RoiEnabled = true;
            systemParameters.Roi = e.Roi;
            SaveSystemParameters();
            statusLabel.Text = string.Format(
                "已保留 ROI：X={0}, Y={1}, W={2}, H={3}",
                e.Roi.X,
                e.Roi.Y,
                e.Roi.Width,
                e.Roi.Height);
        }

        private void CancelSavedRoi()
        {
            rightOriginalDisplayControl.ClearRoiOverlay();
            systemParameters.RoiEnabled = false;
            systemParameters.Roi = Rectangle.Empty;
            SaveSystemParameters();
            statusLabel.Text = "已取消 ROI 設定";
        }

        private async Task RestoreSystemParametersAsync()
        {
            if (!string.IsNullOrWhiteSpace(systemParameters.LastImagePath) && File.Exists(systemParameters.LastImagePath))
            {
                try
                {
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    rightImageTabControl.SelectedTab = rightOriginalTabPage;
                    await leftOriginalDisplayControl.LoadImageFromFileAsync(systemParameters.LastImagePath, CancellationToken.None);
                    await rightOriginalDisplayControl.LoadImageFromFileAsync(systemParameters.LastImagePath, CancellationToken.None);
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
        }

        private void RestoreSavedRoiOverlay()
        {
            if (!systemParameters.RoiEnabled || systemParameters.Roi.Width <= 0 || systemParameters.Roi.Height <= 0)
            {
                return;
            }

            rightOriginalDisplayControl.SetRoiOverlay(systemParameters.Roi);
            statusLabel.Text = string.Format(
                "已還原 ROI：X={0}, Y={1}, W={2}, H={3}",
                systemParameters.Roi.X,
                systemParameters.Roi.Y,
                systemParameters.Roi.Width,
                systemParameters.Roi.Height);
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
                    systemParameters.LastImagePath = dialog.FileName;
                    systemParameters.RoiEnabled = false;
                    systemParameters.Roi = Rectangle.Empty;

                    await leftOriginalDisplayControl.LoadImageFromFileAsync(dialog.FileName, CancellationToken.None);
                    await rightOriginalDisplayControl.LoadImageFromFileAsync(dialog.FileName, CancellationToken.None);
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
                e.Bounds.Left + (IsRoiSubMenuItem(functionListBox.Items[e.Index].ToString()) ? 30 : 12),
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

        private static bool IsRoiSubMenuItem(string menuText)
        {
            return menuText == SetRoiMenuText || menuText == CancelRoiMenuText;
        }
    }
}
