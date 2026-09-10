using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;

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

        public MainForm()
        {
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
            parameterPlaceholderLabel.Text = selectedFunction == "讀取圖片"
                ? "點選左側「讀取圖片」後，選擇要載入的圖片。"
                : "這裡會顯示「" + selectedFunction + "」的參數設定與選項。";
            statusLabel.Text = "目前選擇：" + selectedFunction;
        }

        private async void FunctionListBox_MouseClick(object sender, MouseEventArgs e)
        {
            int clickedIndex = functionListBox.IndexFromPoint(e.Location);
            if (clickedIndex == 0)
            {
                await OpenImageAsync();
            }
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

                    await leftOriginalDisplayControl.LoadImageFromFileAsync(dialog.FileName, CancellationToken.None);
                    await rightOriginalDisplayControl.LoadImageFromFileAsync(dialog.FileName, CancellationToken.None);
                    SyncVisibleImageDisplaysFromLeft();

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
                e.Bounds.Left + 12,
                e.Bounds.Top,
                e.Bounds.Width - 24,
                e.Bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                functionListBox.Items[e.Index].ToString(),
                e.Font,
                textBounds,
                foreColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }
}
