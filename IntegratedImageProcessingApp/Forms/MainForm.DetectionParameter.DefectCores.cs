using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private TabControl objectDetectionDefectCoreTabs;
        private int selectedObjectDetectionDefectCoreIndex;
        private string objectDetectionDefectDraftParameterId;
        private System.Collections.Generic.List<ObjectDetectionDefectCoreSettings>
            objectDetectionDefectCoreDrafts;

        private sealed class DefectCorePreprocessOption
        {
            public DefectCorePreprocessOption(string text, string value)
            {
                Text = text;
                Value = value;
            }

            public string Text { get; private set; }

            public string Value { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class DefectCoreEnhancementOption
        {
            public DefectCoreEnhancementOption(string text, string value)
            {
                Text = text;
                Value = value;
            }

            public string Text { get; private set; }

            public string Value { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private enum DefectCoreDisplayOption
        {
            Mask,
            DarkDefectBoxes,
            BrightDefectBoxes
        }

        private TabControl BuildObjectDetectionDefectCoreTabs(
            ObjectDetectionParameterSettings parameter)
        {
            EnsureObjectDetectionDefectCores(parameter);
            EnsureObjectDetectionDefectCoreDrafts(parameter);
            var tabs = new TabControl
            {
                Dock = DockStyle.Top,
                Height = 430,
                Name = "objectDetectionDefectCoreTabs"
            };
            objectDetectionDefectCoreTabs = tabs;
            PopulateObjectDetectionDefectCoreTabs(tabs, parameter);
            tabs.SelectedIndex = Math.Max(0, Math.Min(
                selectedObjectDetectionDefectCoreIndex,
                tabs.TabPages.Count - 1));
            tabs.SelectedIndexChanged += delegate
            {
                if (tabs.SelectedIndex < ObjectDetectionDefectCoreKeys.Length)
                {
                    selectedObjectDetectionDefectCoreIndex = tabs.SelectedIndex;
                }
                SelectObjectDetectionDefectDisplayForCore(tabs.SelectedIndex);
                ObjectDetectionParameterSettings activeParameter =
                    FindObjectDetectionParameter(activeObjectDetectionParameterId);
                if (activeParameter != null)
                {
                    RefreshObjectDetectionDefectIntegrationResults(activeParameter);
                }
                if (objectDetectionDefectDisplayControl != null)
                {
                    objectDetectionDefectDisplayControl.InvalidateImageView();
                }
                UpdateObjectDetectionDefectProcessingStatus();
            };

            return tabs;
        }

        private void PopulateObjectDetectionDefectCoreTabs(
            TabControl tabs,
            ObjectDetectionParameterSettings parameter)
        {
            TabPage[] oldPages = tabs.TabPages.Cast<TabPage>().ToArray();
            tabs.TabPages.Clear();
            foreach (TabPage oldPage in oldPages)
            {
                oldPage.Dispose();
            }
            string[] labels = { "平場校正影像", "條件一", "條件二", "條件三" };
            for (int index = 0; index < labels.Length; index++)
            {
                ObjectDetectionDefectCoreSettings core = objectDetectionDefectCoreDrafts[index];
                var page = new TabPage(labels[index]);
                var scrollPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = false,
                    BackColor = Color.FromArgb(250, 251, 253)
                };

                Control commandBar = BuildDefectCoreCommandBar(parameter, index);
                Control areaGroup = BuildDefectCoreMinimumAreaGroup(parameter, core);
                Control morphologyGroup = BuildDefectCoreMorphologyGroup(core);
                Control thresholdGroup = BuildDefectCoreThresholdGroup(core);
                Control preprocessingGroup = BuildDefectCorePreprocessingGroup(core);
                Control enhancementGroup = BuildDefectCoreEnhancementGroup(core);
                Control contrastGroup = BuildDefectCoreContrastGroup(parameter, core, index == 0);

                scrollPanel.Controls.Add(areaGroup);
                scrollPanel.Controls.Add(morphologyGroup);
                scrollPanel.Controls.Add(thresholdGroup);
                scrollPanel.Controls.Add(enhancementGroup);
                scrollPanel.Controls.Add(preprocessingGroup);
                scrollPanel.Controls.Add(contrastGroup);
                if (index > 0)
                {
                    scrollPanel.Controls.Add(
                        BuildDefectCoreEnabledPanel(parameter, index, core));
                }
                scrollPanel.Controls.Add(commandBar);
                page.Controls.Add(scrollPanel);
                tabs.TabPages.Add(page);
            }
            tabs.TabPages.Add(BuildObjectDetectionDefectFrequencyTab(parameter));
            tabs.TabPages.Add(BuildObjectDetectionDefectIntegrationTab(parameter));
            ResizeObjectDetectionDefectCoreTabsToContent();
        }

        private void EnsureObjectDetectionDefectCoreDrafts(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null)
            {
                return;
            }
            if (objectDetectionDefectCoreDrafts != null &&
                string.Equals(objectDetectionDefectDraftParameterId, parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionDefectCoreDrafts.Count == 4)
            {
                return;
            }

            EnsureObjectDetectionDefectCores(parameter);
            objectDetectionDefectDraftParameterId = parameter.Id;
            objectDetectionDefectCoreDrafts = parameter.DefectDetectionCores
                .Take(4)
                .Select(CloneObjectDetectionDefectCoreSettings)
                .ToList();
        }

        private Control BuildDefectCoreCommandBar(
            ObjectDetectionParameterSettings parameter,
            int coreIndex)
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 118,
                Padding = new Padding(2, 2, 2, 2)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));

            var displayOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoScroll = false
            };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            var cancel = new Button { Text = "取消", Width = 78, Height = 28, Margin = new Padding(3, 0, 0, 0) };
            var apply = new Button { Text = "套用", Width = 78, Height = 28, Margin = new Padding(3, 0, 0, 0) };
            cancel.Click += delegate { CancelObjectDetectionDefectCoreDraft(parameter, coreIndex); };
            apply.Click += async delegate { await ApplyObjectDetectionDefectCoreDraftAsync(parameter, coreIndex); };

            ObjectDetectionDefectCoreSettings core = objectDetectionDefectCoreDrafts[coreIndex];
            var showMask = new CheckBox
            {
                Text = "顯示 MASK",
                AutoSize = true,
                Checked = core.ShowMask,
                Margin = new Padding(2, 2, 0, 0)
            };
            var showDarkDefectBoxes = new CheckBox
            {
                Text = "顯示紅框(暗檢出)",
                AutoSize = true,
                Checked = core.ShowRedBoxes,
                Margin = new Padding(2, 2, 0, 0)
            };
            var showBrightDefectBoxes = new CheckBox
            {
                Text = "顯示橘框(亮檢出)",
                AutoSize = true,
                Checked = core.ShowOrangeBoxes,
                Margin = new Padding(2, 2, 0, 0)
            };
            showMask.CheckedChanged += delegate
            {
                UpdateObjectDetectionDefectCoreDisplayOption(
                    parameter,
                    coreIndex,
                    DefectCoreDisplayOption.Mask,
                    showMask.Checked);
            };
            showDarkDefectBoxes.CheckedChanged += delegate
            {
                UpdateObjectDetectionDefectCoreDisplayOption(
                    parameter,
                    coreIndex,
                    DefectCoreDisplayOption.DarkDefectBoxes,
                    showDarkDefectBoxes.Checked);
            };
            showBrightDefectBoxes.CheckedChanged += delegate
            {
                UpdateObjectDetectionDefectCoreDisplayOption(
                    parameter,
                    coreIndex,
                    DefectCoreDisplayOption.BrightDefectBoxes,
                    showBrightDefectBoxes.Checked);
            };
            displayOptions.Controls.Add(showMask);
            displayOptions.Controls.Add(showDarkDefectBoxes);
            displayOptions.Controls.Add(showBrightDefectBoxes);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(apply);
            layout.Controls.Add(displayOptions, 0, 0);
            layout.Controls.Add(buttons, 0, 1);
            bar.Controls.Add(layout);
            return bar;
        }

        private Control BuildDefectCoreEnabledPanel(
            ObjectDetectionParameterSettings parameter,
            int coreIndex,
            ObjectDetectionDefectCoreSettings core)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30
            };
            var useCondition = new CheckBox
            {
                AutoSize = true,
                Text = "是否使用",
                Checked = core.Enabled,
                Location = new Point(8, 5)
            };
            useCondition.CheckedChanged += delegate
            {
                UpdateObjectDetectionDefectCoreEnabled(
                    parameter,
                    coreIndex,
                    useCondition.Checked);
            };
            panel.Controls.Add(useCondition);
            return panel;
        }

        private void UpdateObjectDetectionDefectCoreEnabled(
            ObjectDetectionParameterSettings parameter,
            int coreIndex,
            bool enabled)
        {
            if (parameter == null || coreIndex < 1 || coreIndex >= 4)
            {
                return;
            }

            EnsureObjectDetectionDefectCores(parameter);
            objectDetectionDefectCoreDrafts[coreIndex].Enabled = enabled;
            parameter.DefectDetectionCores[coreIndex].Enabled = enabled;
            SaveSystemParameters();
            if (!enabled)
            {
                RemoveObjectDetectionDefectCoreResult(
                    parameter.Id,
                    parameter.DefectDetectionCores[coreIndex].CoreKey);
            }

            ImageDisplayControl coreDisplay = GetObjectDetectionDefectDisplayControl(coreIndex);
            if (coreDisplay != null)
            {
                coreDisplay.InvalidateImageView();
            }
            ImageDisplayControl combinedDisplay = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectIntegratedDisplayIndex);
            if (combinedDisplay != null)
            {
                combinedDisplay.InvalidateImageView();
            }
            SetObjectDetectionDefectRegionStatus(
                GetObjectDetectionDefectCoreLabel(coreIndex) +
                (enabled
                    ? "已啟用；執行檢測後會納入綜合結果。"
                    : "已停用，不會納入全部檢測與綜合結果。"));
        }

        private void UpdateObjectDetectionDefectCoreDisplayOption(
            ObjectDetectionParameterSettings parameter,
            int coreIndex,
            DefectCoreDisplayOption option,
            bool enabled)
        {
            if (parameter == null || coreIndex < 0 || coreIndex >= 4)
            {
                return;
            }

            EnsureObjectDetectionDefectCores(parameter);
            ObjectDetectionDefectCoreSettings persistedCore =
                parameter.DefectDetectionCores[coreIndex];
            switch (option)
            {
                case DefectCoreDisplayOption.Mask:
                    objectDetectionDefectCoreDrafts[coreIndex].ShowMask = enabled;
                    persistedCore.ShowMask = enabled;
                    break;
                case DefectCoreDisplayOption.DarkDefectBoxes:
                    objectDetectionDefectCoreDrafts[coreIndex].ShowRedBoxes = enabled;
                    persistedCore.ShowRedBoxes = enabled;
                    break;
                case DefectCoreDisplayOption.BrightDefectBoxes:
                    objectDetectionDefectCoreDrafts[coreIndex].ShowOrangeBoxes = enabled;
                    persistedCore.ShowOrangeBoxes = enabled;
                    break;
            }

            SaveSystemParameters();
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(coreIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
        }

        private async Task ApplyObjectDetectionDefectCoreDraftAsync(
            ObjectDetectionParameterSettings parameter,
            int coreIndex)
        {
            if (parameter == null || coreIndex < 0 || coreIndex >= 4 ||
                objectDetectionDefectCoreDrafts == null ||
                coreIndex >= objectDetectionDefectCoreDrafts.Count ||
                objectDetectionDefectProcessingRequested)
            {
                return;
            }

            ObjectDetectionDefectCoreSettings draft = objectDetectionDefectCoreDrafts[coreIndex];
            if ((draft.MaximumWidthMillimeters > 0 &&
                    draft.MinimumWidthMillimeters > draft.MaximumWidthMillimeters) ||
                (draft.MaximumHeightMillimeters > 0 &&
                    draft.MinimumHeightMillimeters > draft.MaximumHeightMillimeters))
            {
                MessageBox.Show(
                    "X 或 Y 尺寸的最小值不可大於最大值。",
                    "物件尺寸篩選",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            EnsureObjectDetectionDefectCores(parameter);
            parameter.DefectDetectionCores[coreIndex] =
                CloneObjectDetectionDefectCoreSettings(objectDetectionDefectCoreDrafts[coreIndex]);
            SaveSystemParameters();
            selectedObjectDetectionDefectCoreIndex = coreIndex;
            if (objectDetectionDefectCoreTabs != null)
            {
                objectDetectionDefectCoreTabs.SelectedIndex = coreIndex;
            }
            SelectObjectDetectionDefectDisplayForCore(coreIndex);
            SetObjectDetectionDefectRegionStatus(
                "已套用「" + GetObjectDetectionDefectCoreLabel(coreIndex) + "」設定，正在更新對應結果圖...");
            await RunObjectDetectionDefectProcessingAsync(
                parameter.Id,
                parameter.DefectDetectionCores[coreIndex].CoreKey);
        }

        private void CancelObjectDetectionDefectCoreDraft(
            ObjectDetectionParameterSettings parameter,
            int coreIndex)
        {
            if (parameter == null || coreIndex < 0 || coreIndex >= 4)
            {
                return;
            }
            EnsureObjectDetectionDefectCores(parameter);
            EnsureObjectDetectionDefectCoreDrafts(parameter);
            objectDetectionDefectCoreDrafts[coreIndex] =
                CloneObjectDetectionDefectCoreSettings(parameter.DefectDetectionCores[coreIndex]);
            int selectedIndex = objectDetectionDefectCoreTabs == null
                ? coreIndex
                : objectDetectionDefectCoreTabs.SelectedIndex;
            PopulateObjectDetectionDefectCoreTabs(objectDetectionDefectCoreTabs, parameter);
            objectDetectionDefectCoreTabs.SelectedIndex = Math.Max(0, Math.Min(
                selectedIndex,
                objectDetectionDefectCoreTabs.TabPages.Count - 1));
        }

        private static string GetObjectDetectionDefectCoreLabel(int index)
        {
            string[] labels = { "平場校正影像", "條件一", "條件二", "條件三" };
            return index >= 0 && index < labels.Length ? labels[index] : "缺陷核心";
        }

        private void SelectObjectDetectionDefectDisplayForCore(int coreIndex)
        {
            TabPage page = GetObjectDetectionDefectDisplayTabPage(coreIndex);
            if (isObjectDetectionParameterImageLayout && leftImageTabControl != null &&
                page != null && leftImageTabControl.TabPages.Contains(page))
            {
                leftImageTabControl.SelectedTab = page;
                RefreshObjectDetectionDefectDisplay();
            }
        }

        private void ResizeObjectDetectionDefectCoreTabsToContent()
        {
            TabControl tabs = objectDetectionDefectCoreTabs;
            if (tabs == null || tabs.IsDisposed || tabs.TabPages.Count == 0)
            {
                return;
            }

            int maximumPageContentHeight = 0;
            foreach (TabPage page in tabs.TabPages)
            {
                Panel content = page.Controls.Count == 0
                    ? null
                    : page.Controls[0] as Panel;
                if (content == null)
                {
                    continue;
                }

                int pageContentHeight = content.Padding.Vertical;
                foreach (Control section in content.Controls)
                {
                    pageContentHeight += section.Height + section.Margin.Vertical;
                }
                maximumPageContentHeight = Math.Max(maximumPageContentHeight, pageContentHeight);
            }

            if (maximumPageContentHeight <= 0)
            {
                return;
            }

            int tabHeaderHeight = tabs.Height - tabs.DisplayRectangle.Height;
            tabs.Height = tabHeaderHeight + maximumPageContentHeight;
        }

        private string GetSelectedObjectDetectionDefectCoreKey()
        {
            int index = objectDetectionDefectCoreTabs == null
                ? selectedObjectDetectionDefectCoreIndex
                : objectDetectionDefectCoreTabs.SelectedIndex;
            if (index >= ObjectDetectionDefectCoreKeys.Length)
            {
                index = Math.Max(0, Math.Min(
                    selectedObjectDetectionDefectCoreIndex,
                    ObjectDetectionDefectCoreKeys.Length - 1));
            }
            switch (index)
            {
                case 1: return "Contrast1";
                case 2: return "Contrast2";
                case 3: return "Contrast3";
                default: return "FlatField";
            }
        }

        private static void EnsureObjectDetectionDefectCores(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter.DefectDetectionCores == null)
            {
                parameter.DefectDetectionCores = new System.Collections.Generic.List<ObjectDetectionDefectCoreSettings>();
            }

            string[] keys = { "FlatField", "Contrast1", "Contrast2", "Contrast3" };
            double[] gains = { 1.0, 1.25, 1.5, 2.0 };
            for (int index = 0; index < keys.Length; index++)
            {
                while (parameter.DefectDetectionCores.Count <= index)
                {
                    parameter.DefectDetectionCores.Add(new ObjectDetectionDefectCoreSettings());
                }

                ObjectDetectionDefectCoreSettings core = parameter.DefectDetectionCores[index];
                if (core == null)
                {
                    core = new ObjectDetectionDefectCoreSettings();
                    parameter.DefectDetectionCores[index] = core;
                }

                core.CoreKey = keys[index];
                core.Id = (parameter.Id ?? string.Empty) + ":DefectCore:" + keys[index];
                if (index == 0)
                {
                    core.ContrastGain = gains[index];
                }
            }
        }

        private Control BuildDefectCoreContrastGroup(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionDefectCoreSettings core,
            bool isFlatFieldCore)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = isFlatFieldCore ? 98 : 123,
                Text = "影像來源與對比",
                Padding = new Padding(8, 16, 8, 4)
            };
            int rowCount = isFlatFieldCore ? 3 : 4;
            var layout = CreateDefectCoreTable(rowCount, 2);
            layout.RowStyles.Clear();
            for (int row = 0; row < rowCount; row++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            }

            var source = new Label
            {
                Dock = DockStyle.Fill,
                Text = "平場校正後影像",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var gainLabel = CreateDefectCoreLabel("對比倍率");
            NumericUpDown gain = CreateDefectCoreNumber(
                0.1m,
                5m,
                0.05m,
                2,
                (decimal)Math.Max(0.1, Math.Min(5.0, core.ContrastGain)));
            gain.Enabled = !isFlatFieldCore && core.ContrastAdjustmentEnabled;
            CheckBox contrastEnabled = null;
            int gainRow = 1;
            int pivotRow = 2;
            if (!isFlatFieldCore)
            {
                contrastEnabled = new CheckBox
                {
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Text = "啟用對比調整",
                    Checked = core.ContrastAdjustmentEnabled,
                    Margin = new Padding(0, 2, 0, 0)
                };
                layout.Controls.Add(contrastEnabled, 0, 1);
                layout.SetColumnSpan(contrastEnabled, 2);
                gainRow = 2;
                pivotRow = 3;
            }
            var pivot = new Label
            {
                Dock = DockStyle.Fill,
                Text = "基準灰階：平場目標值 " + parameter.FlatFieldTargetGray.ToString(CultureInfo.CurrentCulture),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            layout.Controls.Add(source, 0, 0);
            layout.SetColumnSpan(source, 2);
            layout.Controls.Add(gainLabel, 0, gainRow);
            layout.Controls.Add(gain, 1, gainRow);
            layout.Controls.Add(pivot, 0, pivotRow);
            layout.SetColumnSpan(pivot, 2);
            group.Controls.Add(layout);

            if (contrastEnabled != null)
            {
                CheckBox enabledControl = contrastEnabled;
                contrastEnabled.CheckedChanged += delegate
                {
                    core.ContrastAdjustmentEnabled = enabledControl.Checked;
                    gain.Enabled = enabledControl.Checked;
                };
            }
            gain.ValueChanged += delegate
            {
                core.ContrastGain = Decimal.ToDouble(gain.Value);
            };
            return group;
        }

        private Control BuildDefectCorePreprocessingGroup(
            ObjectDetectionDefectCoreSettings core)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 216,
                Text = "前處理",
                Padding = new Padding(8, 16, 8, 4)
            };
            var layout = CreateDefectCoreTable(6, 2);
            layout.RowStyles.Clear();
            for (int row = 0; row < 6; row++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            }

            var method = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            method.Items.Add(new DefectCorePreprocessOption("不處理", "None"));
            method.Items.Add(new DefectCorePreprocessOption("Gaussian 模糊", "GaussianBlur"));
            method.Items.Add(new DefectCorePreprocessOption("Median 模糊", "MedianBlur"));
            SelectDefectCorePreprocessOption(method, core.PreprocessMethod);
            layout.Controls.Add(CreateDefectCoreLabel("處理方式"), 0, 0);
            layout.Controls.Add(method, 1, 0);

            NumericUpDown gaussianWidth = CreateOddDefectCoreNumber(core.GaussianKernelWidth, 1);
            NumericUpDown gaussianHeight = CreateOddDefectCoreNumber(core.GaussianKernelHeight, 1);
            NumericUpDown sigmaX = CreateDefectCoreNumber(0m, 100m, 0.25m, 2,
                (decimal)Math.Max(0, Math.Min(100, core.GaussianSigmaX)));
            NumericUpDown sigmaY = CreateDefectCoreNumber(0m, 100m, 0.25m, 2,
                (decimal)Math.Max(0, Math.Min(100, core.GaussianSigmaY)));
            NumericUpDown medianSize = CreateOddDefectCoreNumber(core.MedianKernelSize, 3);

            Label gaussianWidthLabel = CreateDefectCoreLabel("Gaussian 核心寬度");
            Label gaussianHeightLabel = CreateDefectCoreLabel("Gaussian 核心高度");
            Label sigmaXLabel = CreateDefectCoreLabel("Sigma X（0 自動）");
            Label sigmaYLabel = CreateDefectCoreLabel("Sigma Y（0 自動）");
            Label medianSizeLabel = CreateDefectCoreLabel("Median 核心大小");
            layout.Controls.Add(gaussianWidthLabel, 0, 1);
            layout.Controls.Add(gaussianWidth, 1, 1);
            layout.Controls.Add(gaussianHeightLabel, 0, 2);
            layout.Controls.Add(gaussianHeight, 1, 2);
            layout.Controls.Add(sigmaXLabel, 0, 3);
            layout.Controls.Add(sigmaX, 1, 3);
            layout.Controls.Add(sigmaYLabel, 0, 4);
            layout.Controls.Add(sigmaY, 1, 4);
            layout.Controls.Add(medianSizeLabel, 0, 5);
            layout.Controls.Add(medianSize, 1, 5);
            group.Controls.Add(layout);

            Action updateMode = delegate
            {
                DefectCorePreprocessOption option =
                    method.SelectedItem as DefectCorePreprocessOption;
                string mode = option == null ? "None" : option.Value;
                bool gaussianEnabled = string.Equals(mode, "GaussianBlur", StringComparison.Ordinal);
                bool medianEnabled = string.Equals(mode, "MedianBlur", StringComparison.Ordinal);
                gaussianWidthLabel.Enabled = gaussianEnabled;
                gaussianWidth.Enabled = gaussianEnabled;
                gaussianHeightLabel.Enabled = gaussianEnabled;
                gaussianHeight.Enabled = gaussianEnabled;
                sigmaXLabel.Enabled = gaussianEnabled;
                sigmaX.Enabled = gaussianEnabled;
                sigmaYLabel.Enabled = gaussianEnabled;
                sigmaY.Enabled = gaussianEnabled;
                medianSizeLabel.Enabled = medianEnabled;
                medianSize.Enabled = medianEnabled;
            };
            updateMode();
            method.SelectedIndexChanged += delegate
            {
                DefectCorePreprocessOption option =
                    method.SelectedItem as DefectCorePreprocessOption;
                core.PreprocessMethod = option == null ? "None" : option.Value;
                updateMode();
            };
            BindDefectCoreNumber(gaussianWidth, delegate(int value) { core.GaussianKernelWidth = value; });
            BindDefectCoreNumber(gaussianHeight, delegate(int value) { core.GaussianKernelHeight = value; });
            BindDefectCoreNumber(sigmaX, delegate(double value) { core.GaussianSigmaX = value; });
            BindDefectCoreNumber(sigmaY, delegate(double value) { core.GaussianSigmaY = value; });
            BindDefectCoreNumber(medianSize, delegate(int value) { core.MedianKernelSize = value; });
            return group;
        }

        private Control BuildDefectCoreEnhancementGroup(
            ObjectDetectionDefectCoreSettings core)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 178,
                Text = "淡色缺陷增強",
                Padding = new Padding(8, 16, 8, 4)
            };
            var layout = CreateDefectCoreTable(6, 2);
            layout.RowStyles.Clear();
            for (int row = 0; row < 6; row++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            }

            var method = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            method.Items.Add(new DefectCoreEnhancementOption("不處理", "None"));
            method.Items.Add(new DefectCoreEnhancementOption("局部背景差分", "LocalBackgroundDifference"));
            method.Items.Add(new DefectCoreEnhancementOption("CLAHE 局部對比", "CLAHE"));
            SelectDefectCoreEnhancementOption(method, core.DefectEnhancementMethod);

            NumericUpDown backgroundKernel = CreateOddDefectCoreNumber(
                core.LocalBackgroundKernelSize,
                3);
            NumericUpDown enhancementGain = CreateDefectCoreNumber(
                0.1m,
                10m,
                0.1m,
                1,
                (decimal)Math.Max(0.1, Math.Min(10.0, core.LocalBackgroundGain)));
            NumericUpDown claheClipLimit = CreateDefectCoreNumber(
                0.1m,
                40m,
                0.1m,
                1,
                (decimal)Math.Max(0.1, Math.Min(40.0, core.ClaheClipLimit)));
            NumericUpDown claheTileGridSize = CreateDefectCoreNumber(
                2m,
                32m,
                1m,
                0,
                Math.Max(2, Math.Min(32, core.ClaheTileGridSize)));
            Label hint = CreateDefectCoreLabel("局部背景差分以平場目標灰階為中心；CLAHE 可提升局部對比，也可能放大雜訊。");

            layout.Controls.Add(CreateDefectCoreLabel("增強方式"), 0, 0);
            layout.Controls.Add(method, 1, 0);
            layout.Controls.Add(CreateDefectCoreLabel("背景估算核心 (px)"), 0, 1);
            layout.Controls.Add(backgroundKernel, 1, 1);
            layout.Controls.Add(CreateDefectCoreLabel("缺陷強化倍率"), 0, 2);
            layout.Controls.Add(enhancementGain, 1, 2);
            layout.Controls.Add(CreateDefectCoreLabel("CLAHE Clip Limit"), 0, 3);
            layout.Controls.Add(claheClipLimit, 1, 3);
            layout.Controls.Add(CreateDefectCoreLabel("CLAHE Tile Grid (格數)"), 0, 4);
            layout.Controls.Add(claheTileGridSize, 1, 4);
            layout.Controls.Add(hint, 0, 5);
            layout.SetColumnSpan(hint, 2);
            group.Controls.Add(layout);

            Action updateMode = delegate
            {
                DefectCoreEnhancementOption option =
                    method.SelectedItem as DefectCoreEnhancementOption;
                bool localBackgroundEnabled = option != null && string.Equals(
                    option.Value,
                    "LocalBackgroundDifference",
                    StringComparison.Ordinal);
                bool claheEnabled = option != null && string.Equals(
                    option.Value,
                    "CLAHE",
                    StringComparison.Ordinal);
                backgroundKernel.Enabled = localBackgroundEnabled;
                enhancementGain.Enabled = localBackgroundEnabled;
                claheClipLimit.Enabled = claheEnabled;
                claheTileGridSize.Enabled = claheEnabled;
            };
            updateMode();
            method.SelectedIndexChanged += delegate
            {
                DefectCoreEnhancementOption option =
                    method.SelectedItem as DefectCoreEnhancementOption;
                core.DefectEnhancementMethod = option == null ? "None" : option.Value;
                updateMode();
            };
            BindDefectCoreNumber(backgroundKernel, delegate(int value)
            {
                core.LocalBackgroundKernelSize = value;
            });
            BindDefectCoreNumber(enhancementGain, delegate(double value)
            {
                core.LocalBackgroundGain = value;
            });
            BindDefectCoreNumber(claheClipLimit, delegate(double value)
            {
                core.ClaheClipLimit = value;
            });
            BindDefectCoreNumber(claheTileGridSize, delegate(int value)
            {
                core.ClaheTileGridSize = value;
            });
            return group;
        }

        private Control BuildDefectCoreThresholdGroup(
            ObjectDetectionDefectCoreSettings core)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 91,
                Text = "暗部／亮部門檻",
                Padding = new Padding(8, 16, 8, 4)
            };
            var layout = CreateDefectCoreTable(2, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            var darkEnabled = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = "暗部（灰階 ≤）",
                Checked = core.DarkThresholdEnabled,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var brightEnabled = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = "亮部（灰階 ≥）",
                Checked = core.BrightThresholdEnabled,
                TextAlign = ContentAlignment.MiddleLeft
            };
            NumericUpDown darkThreshold = CreateDefectCoreNumber(0m, 255m, 1m, 0,
                Math.Max(0, Math.Min(255, core.DarkThreshold)));
            NumericUpDown brightThreshold = CreateDefectCoreNumber(0m, 255m, 1m, 0,
                Math.Max(0, Math.Min(255, core.BrightThreshold)));
            darkThreshold.Enabled = darkEnabled.Checked;
            brightThreshold.Enabled = brightEnabled.Checked;
            layout.Controls.Add(darkEnabled, 0, 0);
            layout.Controls.Add(darkThreshold, 1, 0);
            layout.Controls.Add(brightEnabled, 0, 1);
            layout.Controls.Add(brightThreshold, 1, 1);
            group.Controls.Add(layout);

            darkEnabled.CheckedChanged += delegate
            {
                core.DarkThresholdEnabled = darkEnabled.Checked;
                darkThreshold.Enabled = darkEnabled.Checked;
            };
            brightEnabled.CheckedChanged += delegate
            {
                core.BrightThresholdEnabled = brightEnabled.Checked;
                brightThreshold.Enabled = brightEnabled.Checked;
            };
            BindDefectCoreNumber(darkThreshold, delegate(int value) { core.DarkThreshold = value; });
            BindDefectCoreNumber(brightThreshold, delegate(int value) { core.BrightThreshold = value; });
            return group;
        }

        private Control BuildDefectCoreMorphologyGroup(
            ObjectDetectionDefectCoreSettings core)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 112,
                Text = "MASK 侵蝕／膨脹",
                Padding = new Padding(8, 16, 8, 4)
            };
            var layout = CreateDefectCoreTable(3, 4);
            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            var erodeEnabled = new CheckBox
            {
                AutoSize = true,
                Text = "侵蝕 (E)",
                Checked = core.ErodeEnabled,
                Anchor = AnchorStyles.Left
            };
            NumericUpDown erodeKernel = CreateOddDefectCoreNumber(core.ErodeKernelSize, 1);
            NumericUpDown erodeIterations = CreateDefectCoreNumber(0m, 20m, 1m, 0,
                Math.Max(0, Math.Min(20, core.ErodeIterations)));
            var dilateEnabled = new CheckBox
            {
                AutoSize = true,
                Text = "膨脹 (D)",
                Checked = core.DilateEnabled,
                Anchor = AnchorStyles.Left
            };
            NumericUpDown dilateKernel = CreateOddDefectCoreNumber(core.DilateKernelSize, 1);
            NumericUpDown dilateIterations = CreateDefectCoreNumber(0m, 20m, 1m, 0,
                Math.Max(0, Math.Min(20, core.DilateIterations)));
            layout.Controls.Add(erodeEnabled, 0, 0);
            layout.Controls.Add(erodeKernel, 1, 0);
            layout.Controls.Add(CreateDefectCoreLabel("迭代"), 2, 0);
            layout.Controls.Add(erodeIterations, 3, 0);
            layout.Controls.Add(dilateEnabled, 0, 1);
            layout.Controls.Add(dilateKernel, 1, 1);
            layout.Controls.Add(CreateDefectCoreLabel("迭代"), 2, 1);
            layout.Controls.Add(dilateIterations, 3, 1);
            var orderHint = CreateDefectCoreLabel(
                "勾選才執行；暗、亮 MASK 分別處理，順序 E 後 D；迭代 0 仍略過。");
            layout.Controls.Add(orderHint, 0, 2);
            layout.SetColumnSpan(orderHint, 4);
            group.Controls.Add(layout);

            erodeEnabled.CheckedChanged += delegate
            {
                core.ErodeEnabled = erodeEnabled.Checked;
                erodeKernel.Enabled = erodeEnabled.Checked;
                erodeIterations.Enabled = erodeEnabled.Checked;
            };
            dilateEnabled.CheckedChanged += delegate
            {
                core.DilateEnabled = dilateEnabled.Checked;
                dilateKernel.Enabled = dilateEnabled.Checked;
                dilateIterations.Enabled = dilateEnabled.Checked;
            };
            erodeKernel.Enabled = core.ErodeEnabled;
            erodeIterations.Enabled = core.ErodeEnabled;
            dilateKernel.Enabled = core.DilateEnabled;
            dilateIterations.Enabled = core.DilateEnabled;
            BindDefectCoreNumber(erodeKernel, delegate(int value) { core.ErodeKernelSize = value; });
            BindDefectCoreNumber(erodeIterations, delegate(int value) { core.ErodeIterations = value; });
            BindDefectCoreNumber(dilateKernel, delegate(int value) { core.DilateKernelSize = value; });
            BindDefectCoreNumber(dilateIterations, delegate(int value) { core.DilateIterations = value; });
            return group;
        }

        private Control BuildDefectCoreMinimumAreaGroup(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionDefectCoreSettings core)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 142,
                Text = parameter != null && parameter.CameraPrecisionConfigured
                    ? "物件篩選（X/Y mm；依攝影機精度換算）"
                    : "物件篩選（X/Y mm；未設定精度時暫用 1 mm/pixel）",
                Padding = new Padding(8, 16, 8, 4)
            };
            var layout = CreateDefectCoreTable(4, 5);
            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var minimumArea = CreateDefectCoreNumber(0m, 1000000000m, 1m, 0,
                (decimal)Math.Max(0, Math.Min(1000000000, core.MinimumArea)));
            layout.Controls.Add(CreateDefectCoreLabel("最小面積 px²"), 0, 0);
            layout.Controls.Add(minimumArea, 1, 0);
            layout.SetColumnSpan(minimumArea, 2);
            layout.Controls.Add(CreateDefectCoreLabel("0 不限制"), 3, 0);
            layout.SetColumnSpan(layout.GetControlFromPosition(3, 0), 2);
            layout.Controls.Add(CreateDefectCoreLabel("方向"), 0, 1);
            layout.Controls.Add(CreateDefectCoreLabel("最小 mm（0 不限）"), 1, 1);
            layout.Controls.Add(CreateDefectCoreLabel("約 pixel"), 2, 1);
            layout.Controls.Add(CreateDefectCoreLabel("最大 mm（0 不限）"), 3, 1);
            layout.Controls.Add(CreateDefectCoreLabel("約 pixel"), 4, 1);

            double xScale = GetObjectDetectionDefectDimensionScale(
                parameter == null ? 1.0 : parameter.CameraXMillimetersPerPixel);
            double yScale = GetObjectDetectionDefectDimensionScale(
                parameter == null ? 1.0 : parameter.CameraYMillimetersPerPixel);
            NumericUpDown minimumWidth = CreateDefectCoreNumber(
                0m, 1000000m, 0.1m, 3, (decimal)Math.Max(0, Math.Min(1000000, core.MinimumWidthMillimeters)));
            NumericUpDown maximumWidth = CreateDefectCoreNumber(
                0m, 1000000m, 0.1m, 3, (decimal)Math.Max(0, Math.Min(1000000, core.MaximumWidthMillimeters)));
            NumericUpDown minimumHeight = CreateDefectCoreNumber(
                0m, 1000000m, 0.1m, 3, (decimal)Math.Max(0, Math.Min(1000000, core.MinimumHeightMillimeters)));
            NumericUpDown maximumHeight = CreateDefectCoreNumber(
                0m, 1000000m, 0.1m, 3, (decimal)Math.Max(0, Math.Min(1000000, core.MaximumHeightMillimeters)));
            Label minimumWidthPixels = CreateDefectCoreLabel(string.Empty);
            Label maximumWidthPixels = CreateDefectCoreLabel(string.Empty);
            Label minimumHeightPixels = CreateDefectCoreLabel(string.Empty);
            Label maximumHeightPixels = CreateDefectCoreLabel(string.Empty);
            layout.Controls.Add(CreateDefectCoreLabel("X 寬"), 0, 2);
            layout.Controls.Add(minimumWidth, 1, 2);
            layout.Controls.Add(minimumWidthPixels, 2, 2);
            layout.Controls.Add(maximumWidth, 3, 2);
            layout.Controls.Add(maximumWidthPixels, 4, 2);
            layout.Controls.Add(CreateDefectCoreLabel("Y 高"), 0, 3);
            layout.Controls.Add(minimumHeight, 1, 3);
            layout.Controls.Add(minimumHeightPixels, 2, 3);
            layout.Controls.Add(maximumHeight, 3, 3);
            layout.Controls.Add(maximumHeightPixels, 4, 3);
            group.Controls.Add(layout);

            Action updatePixelHints = delegate
            {
                minimumWidthPixels.Text = FormatMillimetersToPixels(minimumWidth.Value, xScale);
                maximumWidthPixels.Text = FormatMillimetersToPixels(maximumWidth.Value, xScale);
                minimumHeightPixels.Text = FormatMillimetersToPixels(minimumHeight.Value, yScale);
                maximumHeightPixels.Text = FormatMillimetersToPixels(maximumHeight.Value, yScale);
            };
            updatePixelHints();
            minimumWidth.ValueChanged += delegate { updatePixelHints(); };
            maximumWidth.ValueChanged += delegate { updatePixelHints(); };
            minimumHeight.ValueChanged += delegate { updatePixelHints(); };
            maximumHeight.ValueChanged += delegate { updatePixelHints(); };
            BindDefectCoreNumber(minimumArea, delegate(int value) { core.MinimumArea = value; });
            BindDefectCoreNumber(minimumWidth, delegate(double value) { core.MinimumWidthMillimeters = value; });
            BindDefectCoreNumber(maximumWidth, delegate(double value) { core.MaximumWidthMillimeters = value; });
            BindDefectCoreNumber(minimumHeight, delegate(double value) { core.MinimumHeightMillimeters = value; });
            BindDefectCoreNumber(maximumHeight, delegate(double value) { core.MaximumHeightMillimeters = value; });
            return group;
        }

        private static double GetObjectDetectionDefectDimensionScale(double millimetersPerPixel)
        {
            return double.IsNaN(millimetersPerPixel) || double.IsInfinity(millimetersPerPixel) ||
                millimetersPerPixel <= 0.0 ? 1.0 : millimetersPerPixel;
        }

        private static string FormatMillimetersToPixels(decimal millimeters, double millimetersPerPixel)
        {
            if (millimeters <= 0)
            {
                return "--";
            }
            double pixels = Decimal.ToDouble(millimeters) /
                GetObjectDetectionDefectDimensionScale(millimetersPerPixel);
            return pixels.ToString("0.##", CultureInfo.CurrentCulture) + " px";
        }

        private static TableLayoutPanel CreateDefectCoreTable(int rows, int columns)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = rows,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            if (columns == 2)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53f));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47f));
            }
            else
            {
                for (int index = 0; index < columns; index++)
                {
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                }
            }

            return layout;
        }

        private static Label CreateDefectCoreLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(55, 63, 76)
            };
        }

        private static NumericUpDown CreateDefectCoreNumber(
            decimal minimum,
            decimal maximum,
            decimal increment,
            int decimalPlaces,
            decimal value)
        {
            var number = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = minimum,
                Maximum = maximum,
                Increment = increment,
                DecimalPlaces = decimalPlaces,
                ThousandsSeparator = true
            };
            number.Value = Math.Max(minimum, Math.Min(maximum, value));
            return number;
        }

        private static NumericUpDown CreateOddDefectCoreNumber(int value, int minimum)
        {
            int normalized = Math.Min(99, Math.Max(minimum, value));
            if (normalized % 2 == 0)
            {
                normalized++;
            }

            var number = CreateDefectCoreNumber(minimum, 99m, 2m, 0, normalized);
            bool normalizing = false;
            number.ValueChanged += delegate
            {
                if (normalizing || number.Value % 2 != 0)
                {
                    return;
                }

                normalizing = true;
                number.Value = number.Value < number.Maximum
                    ? number.Value + 1
                    : number.Value - 1;
                normalizing = false;
            };
            return number;
        }

        private void BindDefectCoreNumber(NumericUpDown control, Action<int> setValue)
        {
            control.ValueChanged += delegate
            {
                setValue((int)control.Value);
            };
        }

        private void BindDefectCoreNumber(NumericUpDown control, Action<double> setValue)
        {
            control.ValueChanged += delegate
            {
                setValue(Decimal.ToDouble(control.Value));
            };
        }

        private static void SelectDefectCorePreprocessOption(
            ComboBox comboBox,
            string value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                DefectCorePreprocessOption option =
                    comboBox.Items[index] as DefectCorePreprocessOption;
                if (option != null && string.Equals(option.Value, value, StringComparison.Ordinal))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            comboBox.SelectedIndex = 0;
        }

        private static void SelectDefectCoreEnhancementOption(
            ComboBox comboBox,
            string value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                DefectCoreEnhancementOption option =
                    comboBox.Items[index] as DefectCoreEnhancementOption;
                if (option != null && string.Equals(option.Value, value, StringComparison.Ordinal))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            comboBox.SelectedIndex = 0;
        }
    }
}
