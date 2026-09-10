using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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

        public MainForm()
        {
            InitializeComponent();
            InitializeDesignImageDisplayPlaceholders();

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

        private void InitializeDesignImageDisplayPlaceholders()
        {
            ConfigureDesignImageDisplayPlaceholder(leftOriginalDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(leftProcessedDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(leftObjectsDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(leftDebugDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(rightOriginalDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(rightProcessedDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(rightObjectsDisplayHostPanel);
            ConfigureDesignImageDisplayPlaceholder(rightDebugDisplayHostPanel);
        }

        private void ConfigureDesignImageDisplayPlaceholder(Control host)
        {
            host.Controls.Clear();
            host.BackColor = Color.White;
            host.Paint -= ImageDisplayHostPanel_Paint;
            host.Paint += ImageDisplayHostPanel_Paint;
            host.Resize -= ImageDisplayHostPanel_Resize;
            host.Resize += ImageDisplayHostPanel_Resize;
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

        private void ImageDisplayHostPanel_Resize(object sender, EventArgs e)
        {
            var host = sender as Control;
            if (host != null)
            {
                host.Invalidate();
            }
        }

        private void ImageDisplayHostPanel_Paint(object sender, PaintEventArgs e)
        {
            var host = sender as Control;
            if (host == null)
            {
                return;
            }

            DrawImageDisplayPlaceholder(e.Graphics, host.ClientRectangle);
        }

        private static void DrawImageDisplayPlaceholder(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var headerBounds = new Rectangle(0, 0, bounds.Width, 42);
            var footerBounds = new Rectangle(0, Math.Max(42, bounds.Height - 42), bounds.Width, 42);
            var imageBounds = Rectangle.FromLTRB(0, headerBounds.Bottom, bounds.Width, footerBounds.Top);
            imageBounds.Inflate(-1, -1);
            var buttonBounds = new Rectangle(Math.Max(10, bounds.Width - 100), footerBounds.Top + 7, 90, 28);

            using (var grayBrush = new SolidBrush(Color.FromArgb(232, 235, 240)))
            using (var whiteBrush = new SolidBrush(Color.White))
            using (var borderPen = new Pen(Color.FromArgb(170, 176, 185)))
            using (var textBrush = new SolidBrush(Color.Black))
            using (var boldFont = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold))
            using (var font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Regular))
            {
                graphics.FillRectangle(grayBrush, headerBounds);
                graphics.FillRectangle(whiteBrush, imageBounds);
                graphics.DrawRectangle(borderPen, imageBounds);
                graphics.FillRectangle(grayBrush, footerBounds);
                graphics.DrawRectangle(borderPen, buttonBounds);

                graphics.DrawString("圖片顯示", boldFont, textBrush, new PointF(10, 11));
                graphics.DrawString("0 x 0", font, textBrush, new PointF(Math.Max(10, bounds.Width - 55), 11));
                graphics.DrawString("尚未載入圖片", font, textBrush, new PointF(10, footerBounds.Top + 12));
                graphics.DrawString("重設視圖", font, textBrush, new PointF(buttonBounds.Left + 16, buttonBounds.Top + 6));
            }
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
            parameterPlaceholderLabel.Text = "這裡會顯示「" + selectedFunction + "」的參數設定與選項。";
            statusLabel.Text = "目前選擇：" + selectedFunction;
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
