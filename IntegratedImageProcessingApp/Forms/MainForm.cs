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
            CreateDesignImageDisplayPlaceholder(host, "圖片顯示");
        }

        private static void CreateDesignImageDisplayPlaceholder(Control host, string title)
        {
            var topPanel = new Panel();
            var titleLabel = new Label();
            var resolutionLabel = new Label();
            var viewPanel = new Panel();
            var bottomPanel = new Panel();
            var statusLabel = new Label();
            var fitButton = new Button();

            topPanel.BackColor = Color.FromArgb(232, 235, 240);
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 42;
            topPanel.Padding = new Padding(10, 8, 10, 8);

            titleLabel.AutoSize = true;
            titleLabel.Dock = DockStyle.Left;
            titleLabel.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold);
            titleLabel.ForeColor = Color.Black;
            titleLabel.Text = title;

            resolutionLabel.AutoSize = true;
            resolutionLabel.Dock = DockStyle.Right;
            resolutionLabel.ForeColor = Color.Black;
            resolutionLabel.Text = "0 x 0";

            viewPanel.BackColor = Color.White;
            viewPanel.BorderStyle = BorderStyle.FixedSingle;
            viewPanel.Dock = DockStyle.Fill;

            bottomPanel.BackColor = Color.FromArgb(232, 235, 240);
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 42;
            bottomPanel.Padding = new Padding(10, 7, 10, 7);

            statusLabel.AutoSize = true;
            statusLabel.Dock = DockStyle.Left;
            statusLabel.ForeColor = Color.Black;
            statusLabel.Text = "尚未載入圖片";

            fitButton.Dock = DockStyle.Right;
            fitButton.FlatStyle = FlatStyle.Flat;
            fitButton.ForeColor = Color.Black;
            fitButton.Text = "重設視圖";
            fitButton.Width = 90;

            topPanel.Controls.Add(resolutionLabel);
            topPanel.Controls.Add(titleLabel);
            bottomPanel.Controls.Add(fitButton);
            bottomPanel.Controls.Add(statusLabel);
            host.Controls.Add(viewPanel);
            host.Controls.Add(bottomPanel);
            host.Controls.Add(topPanel);
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
