using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private Panel inspectionHistoryPanel;
        private DataGridView inspectionHistoryRecordsGrid;
        private DataGridView inspectionHistorySizeGrid;
        private DataGridView inspectionHistoryDefectGrid;
        private Label inspectionHistoryRecordsEmptyLabel;
        private Label inspectionHistoryDetailHeaderLabel;
        private Label inspectionHistorySizeEmptyLabel;
        private Label inspectionHistoryDefectEmptyLabel;
        private DateTimePicker inspectionHistoryDatePicker;
        private ComboBox inspectionHistoryRecipeFilter;
        private ComboBox inspectionHistoryStatusFilter;
        private ContextMenuStrip inspectionHistoryContextMenu;
        private Label[] inspectionHistorySummaryValueLabels;
        private InspectionHistoryStore inspectionHistoryStore;
        private string inspectionHistorySelectedBatchId;
        private SystemParameterSettings inspectionHistoryPreviousSystemParameters;
        private string inspectionHistoryPreviousActiveParameterId;
        private string inspectionHistoryPreviousReviewParameterId;
        private Size inspectionHistoryPreviousReferenceImageSize;
        private bool isInspectionHistoryRerunActive;
        private bool inspectionHistoryNeedsReviewRefresh;
        private bool isInspectionHistoryActive;
        private SizeType[] inspectionHistoryPreviousColumnTypes;
        private float[] inspectionHistoryPreviousColumnWidths;

        private void ShowInspectionHistoryPanel()
        {
            if (inspectionHistoryStore == null)
            {
                inspectionHistoryStore = new InspectionHistoryStore(AppDomain.CurrentDomain.BaseDirectory);
            }
            if (inspectionHistoryPanel == null || inspectionHistoryPanel.IsDisposed)
            {
                inspectionHistoryPanel = CreateInspectionHistoryPanel();
                centerPanel.Controls.Add(inspectionHistoryPanel);
            }

            if (!isInspectionHistoryActive)
            {
                inspectionHistoryPreviousColumnTypes = new SizeType[mainLayoutPanel.ColumnStyles.Count];
                inspectionHistoryPreviousColumnWidths = new float[mainLayoutPanel.ColumnStyles.Count];
                for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count; index++)
                {
                    inspectionHistoryPreviousColumnTypes[index] = mainLayoutPanel.ColumnStyles[index].SizeType;
                    inspectionHistoryPreviousColumnWidths[index] = mainLayoutPanel.ColumnStyles[index].Width;
                }
                isInspectionHistoryActive = true;
            }

            mainLayoutPanel.SuspendLayout();
            try
            {
                mainLayoutPanel.ColumnStyles[0].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[0].Width = 16F;
                mainLayoutPanel.ColumnStyles[1].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[1].Width = 84F;
                mainLayoutPanel.ColumnStyles[2].SizeType = SizeType.Percent;
                mainLayoutPanel.ColumnStyles[2].Width = 0F;
                rightPanel.Visible = false;
                imageLayoutPanel.Visible = false;
                centerPanel.Visible = true;
                inspectionHistoryPanel.Visible = true;
                inspectionHistoryPanel.BringToFront();
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }

            statusLabel.Text = "目前選擇：歷史資料";
            RefreshInspectionHistory();
        }

        private void ExitInspectionHistoryMode()
        {
            if (!isInspectionHistoryActive)
            {
                return;
            }

            isInspectionHistoryActive = false;
            if (inspectionHistoryPanel != null)
            {
                inspectionHistoryPanel.Visible = false;
            }

            mainLayoutPanel.SuspendLayout();
            try
            {
                imageLayoutPanel.Visible = true;
                rightPanel.Visible = true;
                centerPanel.Visible = !IsImageDisplayUpdateSuppressed;
                if (inspectionHistoryPreviousColumnTypes != null &&
                    inspectionHistoryPreviousColumnWidths != null)
                {
                    for (int index = 0; index < mainLayoutPanel.ColumnStyles.Count &&
                        index < inspectionHistoryPreviousColumnTypes.Length &&
                        index < inspectionHistoryPreviousColumnWidths.Length; index++)
                    {
                        mainLayoutPanel.ColumnStyles[index].SizeType = inspectionHistoryPreviousColumnTypes[index];
                        mainLayoutPanel.ColumnStyles[index].Width = inspectionHistoryPreviousColumnWidths[index];
                    }
                }
            }
            finally
            {
                mainLayoutPanel.ResumeLayout(true);
            }
        }

        private Panel CreateInspectionHistoryPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(15),
                Name = "inspectionHistoryPanel",
                Visible = false
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 8,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 310F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

            var titlePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            titlePanel.Controls.Add(new Label
            {
                Text = "歷史資料",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Microsoft JhengHei UI", 17F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 56, 83),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            });
            titlePanel.Controls.Add(new Label
            {
                Text = "選取一筆大圖檢測紀錄，查看各片判定或使用當時參數重新檢測",
                Dock = DockStyle.Bottom,
                Height = 19,
                Font = new Font("Microsoft JhengHei UI", 8F),
                ForeColor = Color.FromArgb(101, 120, 147),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            });
            content.Controls.Add(titlePanel, 0, 0);

            content.Controls.Add(CreateInspectionHistoryFilterPanel(), 0, 1);
            content.Controls.Add(CreateInspectionHistorySummaryPanel(), 0, 2);

            var recordsHeading = CreateInspectionHistorySectionHeading(
                "檢測紀錄　|　每筆代表一次完整大圖檢測",
                "單擊：下方更新明細　　右鍵：重新檢測");
            content.Controls.Add(recordsHeading, 0, 3);
            content.Controls.Add(CreateInspectionHistoryRecordsPanel(), 0, 4);

            inspectionHistoryDetailHeaderLabel = new Label
            {
                Text = "檢測明細　|　尚未選取檢測紀錄",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 56, 83),
                Padding = new Padding(6, 0, 0, 0)
            };
            content.Controls.Add(inspectionHistoryDetailHeaderLabel, 0, 5);
            content.Controls.Add(CreateInspectionHistoryDetailsPanel(), 0, 6);

            var note = new Label
            {
                Text = "歷史頁不顯示影像預覽；重新檢測會使用當時的歷史參數快照。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(101, 120, 147),
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(7, 0, 0, 0),
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            content.Controls.Add(note, 0, 7);

            panel.Controls.Add(content);
            panel.SizeChanged += delegate
            {
                content.Width = Math.Max(300, panel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            };
            content.Width = Math.Max(300, panel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            return panel;
        }

        private Control CreateInspectionHistoryFilterPanel()
        {
            var filters = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(10, 7, 10, 7),
                BackColor = Color.White,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                BorderStyle = BorderStyle.FixedSingle
            };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            AddHistoryFilterLabel(filters, "檢測日期", 0);
            AddHistoryFilterLabel(filters, "Recipe", 1);
            AddHistoryFilterLabel(filters, "判定狀態", 2);
            AddHistoryFilterLabel(filters, string.Empty, 3);

            var datePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(2, 0, 5, 0),
                Padding = Padding.Empty
            };
            inspectionHistoryDatePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy / MM / dd",
                Value = DateTime.Today,
                Width = 150,
                Height = 28,
                Margin = new Padding(0, 1, 4, 0)
            };
            var previousDayButton = CreateHistorySmallButton("‹");
            previousDayButton.Click += delegate { inspectionHistoryDatePicker.Value = inspectionHistoryDatePicker.Value.AddDays(-1); };
            var nextDayButton = CreateHistorySmallButton("›");
            nextDayButton.Click += delegate { inspectionHistoryDatePicker.Value = inspectionHistoryDatePicker.Value.AddDays(1); };
            datePanel.Controls.Add(inspectionHistoryDatePicker);
            datePanel.Controls.Add(previousDayButton);
            datePanel.Controls.Add(nextDayButton);
            filters.Controls.Add(datePanel, 0, 1);

            inspectionHistoryRecipeFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(4, 1, 7, 0)
            };
            inspectionHistoryRecipeFilter.Items.Add("全部 Recipe");
            inspectionHistoryRecipeFilter.SelectedIndex = 0;
            filters.Controls.Add(inspectionHistoryRecipeFilter, 1, 1);

            inspectionHistoryStatusFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(4, 1, 7, 0)
            };
            inspectionHistoryStatusFilter.Items.AddRange(new object[] { "全部狀態", "含 NG", "含待確認", "全部通過" });
            inspectionHistoryStatusFilter.SelectedIndex = 0;
            filters.Controls.Add(inspectionHistoryStatusFilter, 2, 1);

            var searchButton = new Button
            {
                Text = "查詢",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 1, 0, 0),
                BackColor = Color.FromArgb(25, 116, 194),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            searchButton.FlatAppearance.BorderSize = 0;
            searchButton.Click += InspectionHistorySearchButton_Click;
            filters.Controls.Add(searchButton, 3, 1);
            return filters;
        }

        private void AddHistoryFilterLabel(TableLayoutPanel table, string text, int column)
        {
            table.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Microsoft JhengHei UI", 8F),
                ForeColor = Color.FromArgb(47, 66, 92),
                Margin = new Padding(4, 0, 0, 1)
            }, column, 0);
        }

        private Button CreateHistorySmallButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 28,
                Height = 28,
                Margin = new Padding(0, 1, 2, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 250),
                ForeColor = Color.FromArgb(40, 62, 90)
            };
        }

        private Control CreateInspectionHistorySummaryPanel()
        {
            var summary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0, 7, 0, 0)
            };
            for (int index = 0; index < 5; index++)
            {
                summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            }
            string[] labels = { "當日檢測", "檢測物件", "尺寸通過", "缺陷通過", "綜合通過" };
            for (int index = 0; index < labels.Length; index++)
            {
                Panel card = CreateInspectionHistoryMetricCard(labels[index], index >= 2, index);
                card.Margin = new Padding(index == 0 ? 0 : 4, 0, 4, 0);
                summary.Controls.Add(card, index, 0);
            }
            return summary;
        }

        private Panel CreateInspectionHistoryMetricCard(string labelText, bool isPassMetric, int summaryIndex)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(9, 5, 5, 3)
            };
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                Panel target = sender as Panel;
                if (target != null)
                {
                    using (var pen = new Pen(Color.FromArgb(211, 221, 234)))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, target.Width - 1, target.Height - 1);
                    }
                }
            };
            var cardContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            cardContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            cardContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            cardContent.Controls.Add(new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(100, 119, 145),
                Font = new Font("Microsoft JhengHei UI", 8F),
                Margin = Padding.Empty,
                AutoEllipsis = true
            }, 0, 0);
            var valueLabel = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = isPassMetric ? Color.FromArgb(19, 131, 63) : Color.FromArgb(24, 104, 177),
                Font = new Font("Microsoft JhengHei UI", 15F, FontStyle.Bold),
                Margin = Padding.Empty,
                AutoEllipsis = true
            };
            cardContent.Controls.Add(valueLabel, 0, 1);
            card.Controls.Add(cardContent);
            if (inspectionHistorySummaryValueLabels == null)
            {
                inspectionHistorySummaryValueLabels = new Label[5];
            }
            inspectionHistorySummaryValueLabels[summaryIndex] = valueLabel;
            return card;
        }

        private Control CreateInspectionHistorySectionHeading(string titleText, string helpText)
        {
            var heading = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(6, 5, 5, 0)
            };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            heading.Controls.Add(new Label
            {
                Text = titleText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 56, 83)
            }, 0, 0);
            heading.Controls.Add(new Label
            {
                Text = helpText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft JhengHei UI", 7F),
                ForeColor = Color.FromArgb(101, 120, 147)
            }, 1, 0);
            return heading;
        }

        private Control CreateInspectionHistoryRecordsPanel()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty
            };
            inspectionHistoryRecordsGrid = CreateInspectionHistoryGrid();
            inspectionHistoryRecordsGrid.Dock = DockStyle.Fill;
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("時間", "Time", 10F));
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("影像序號", "Image", 10F));
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("Recipe", "Recipe", 12F));
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("片數", "Pieces", 7F));
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("尺寸判定", "Size", 20F));
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("缺陷判定", "Defect", 20F));
            inspectionHistoryRecordsGrid.Columns.Add(CreateHistoryTextColumn("綜合判定", "Overall", 21F));
            inspectionHistoryRecordsGrid.CellClick += InspectionHistoryRecordsGrid_CellClick;
            inspectionHistoryRecordsGrid.CellMouseDown += InspectionHistoryRecordsGrid_CellMouseDown;
            host.Controls.Add(inspectionHistoryRecordsGrid);
            inspectionHistoryRecordsEmptyLabel = CreateInspectionHistoryEmptyLabel(
                "此日期尚無本機檢測紀錄。");
            inspectionHistoryRecordsEmptyLabel.Dock = DockStyle.Bottom;
            inspectionHistoryRecordsEmptyLabel.Height = 30;
            host.Controls.Add(inspectionHistoryRecordsEmptyLabel);
            inspectionHistoryRecordsEmptyLabel.BringToFront();

            inspectionHistoryContextMenu = new ContextMenuStrip();
            inspectionHistoryContextMenu.Items.Add("使用當時參數重新檢測", null,
                InspectionHistoryRerunMenuItem_Click);
            inspectionHistoryRecordsGrid.ContextMenuStrip = inspectionHistoryContextMenu;
            return host;
        }

        private DataGridView CreateInspectionHistoryGrid()
        {
            return new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(218, 226, 237),
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 31,
                RowTemplate = { Height = 35 },
                Font = new Font("Microsoft JhengHei UI", 8F),
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = Color.FromArgb(234, 240, 247),
                    ForeColor = Color.FromArgb(39, 58, 82),
                    SelectionBackColor = Color.FromArgb(234, 240, 247),
                    SelectionForeColor = Color.FromArgb(39, 58, 82),
                    Font = new Font("Microsoft JhengHei UI", 8F, FontStyle.Bold),
                    Padding = new Padding(3)
                },
                DefaultCellStyle =
                {
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(39, 58, 82),
                    SelectionBackColor = Color.FromArgb(231, 242, 253),
                    SelectionForeColor = Color.FromArgb(39, 58, 82),
                    Padding = new Padding(3)
                }
            };
        }

        private DataGridViewTextBoxColumn CreateHistoryTextColumn(string header, string name, float fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Name = name,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False }
            };
        }

        private Label CreateInspectionHistoryEmptyLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(105, 123, 148),
                Font = new Font("Microsoft JhengHei UI", 8F),
                Visible = true
            };
        }

        private Control CreateInspectionHistoryDetailsPanel()
        {
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei UI", 8F),
                Padding = new Point(12, 5)
            };
            var sizePage = new TabPage("尺寸判定") { BackColor = Color.White, Padding = new Padding(8) };
            var defectPage = new TabPage("缺陷判定") { BackColor = Color.White, Padding = new Padding(8) };

            inspectionHistorySizeGrid = CreateInspectionHistoryGrid();
            inspectionHistorySizeGrid.Dock = DockStyle.Fill;
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("物件序號", "Object", 9F));
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("格位", "Cell", 10F));
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("尺寸項目", "Metric", 16F));
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("實測值", "Measured", 15F));
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("允收範圍", "Range", 17F));
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("條件結果", "Condition", 18F));
            inspectionHistorySizeGrid.Columns.Add(CreateHistoryTextColumn("尺寸判定", "Grade", 15F));

            inspectionHistoryDefectGrid = CreateInspectionHistoryGrid();
            inspectionHistoryDefectGrid.Dock = DockStyle.Fill;
            inspectionHistoryDefectGrid.Columns.Add(CreateHistoryTextColumn("物件序號", "Object", 10F));
            inspectionHistoryDefectGrid.Columns.Add(CreateHistoryTextColumn("格位", "Cell", 12F));
            inspectionHistoryDefectGrid.Columns.Add(CreateHistoryTextColumn("缺陷條件／檢出摘要", "Summary", 38F));
            inspectionHistoryDefectGrid.Columns.Add(CreateHistoryTextColumn("缺陷數量", "Count", 20F));
            inspectionHistoryDefectGrid.Columns.Add(CreateHistoryTextColumn("缺陷判定", "Grade", 20F));

            sizePage.Controls.Add(inspectionHistorySizeGrid);
            defectPage.Controls.Add(inspectionHistoryDefectGrid);
            inspectionHistorySizeEmptyLabel = CreateInspectionHistoryEmptyLabel(
                "選取上方檢測紀錄後顯示尺寸明細。");
            inspectionHistoryDefectEmptyLabel = CreateInspectionHistoryEmptyLabel(
                "選取上方檢測紀錄後顯示缺陷明細。");
            inspectionHistorySizeEmptyLabel.Height = 30;
            inspectionHistoryDefectEmptyLabel.Height = 30;
            sizePage.Controls.Add(inspectionHistorySizeEmptyLabel);
            defectPage.Controls.Add(inspectionHistoryDefectEmptyLabel);
            inspectionHistorySizeEmptyLabel.BringToFront();
            inspectionHistoryDefectEmptyLabel.BringToFront();
            tabs.TabPages.Add(sizePage);
            tabs.TabPages.Add(defectPage);
            return tabs;
        }

        private void UpdateInspectionHistorySummary(IList<InspectionHistoryBatchRecord> records)
        {
            if (inspectionHistorySummaryValueLabels == null) return;
            int batchCount = records == null ? 0 : records.Count;
            int pieceCount = records == null ? 0 : records.Sum(item => item.ObjectCount);
            int sizePass = records == null ? 0 : records.Sum(item => item.SizeACount + item.SizeBCount);
            int defectPass = records == null ? 0 : records.Sum(item => item.DefectACount);
            int combinedPass = records == null ? 0 : records.Sum(item => item.CombinedACount + item.CombinedBCount);
            int[] values = { batchCount, pieceCount, sizePass, defectPass, combinedPass };
            for (int index = 0; index < values.Length; index++)
            {
                if (inspectionHistorySummaryValueLabels[index] != null)
                {
                    inspectionHistorySummaryValueLabels[index].Text = values[index].ToString("N0", CultureInfo.CurrentCulture) +
                        (index == 0 ? " 筆" : " 片");
                }
            }
        }

        private static string FormatHistoryGradeSummary(int pass, int ng, int pending)
        {
            return "通過 " + pass.ToString("N0", CultureInfo.CurrentCulture) +
                "　NG " + ng.ToString("N0", CultureInfo.CurrentCulture) +
                "　待確認 " + pending.ToString("N0", CultureInfo.CurrentCulture);
        }

        private void ResetInspectionHistoryDetails()
        {
            inspectionHistorySelectedBatchId = null;
            if (inspectionHistoryDetailHeaderLabel != null)
            {
                inspectionHistoryDetailHeaderLabel.Text = "檢測明細　|　尚未選取檢測紀錄";
            }
            if (inspectionHistorySizeGrid != null) inspectionHistorySizeGrid.Rows.Clear();
            if (inspectionHistoryDefectGrid != null) inspectionHistoryDefectGrid.Rows.Clear();
            if (inspectionHistorySizeEmptyLabel != null)
            {
                inspectionHistorySizeEmptyLabel.Text = "選取上方檢測紀錄後顯示尺寸明細。";
                inspectionHistorySizeEmptyLabel.Visible = true;
            }
            if (inspectionHistoryDefectEmptyLabel != null)
            {
                inspectionHistoryDefectEmptyLabel.Text = "選取上方檢測紀錄後顯示缺陷明細。";
                inspectionHistoryDefectEmptyLabel.Visible = true;
            }
        }

        private void LoadInspectionHistoryDetails(string batchId)
        {
            InspectionHistoryBatchRecord record = inspectionHistoryStore.LoadBatch(
                inspectionHistoryDatePicker.Value.Date, batchId);
            if (record == null)
            {
                ResetInspectionHistoryDetails();
                return;
            }

            inspectionHistoryDetailHeaderLabel.Text = "檢測明細　|　" +
                record.InspectedAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "　|　" +
                (string.IsNullOrWhiteSpace(record.RecipeName) ? "未指定 Recipe" : record.RecipeName) + "　|　" +
                record.ObjectCount.ToString("N0", CultureInfo.CurrentCulture) + " 片";
            inspectionHistorySizeGrid.Rows.Clear();
            inspectionHistoryDefectGrid.Rows.Clear();

            foreach (InspectionHistoryObjectRecord item in record.Objects ?? new List<InspectionHistoryObjectRecord>())
            {
                string cell = GetInspectionHistoryCellName(item.GridRow, item.GridColumn);
                List<InspectionHistoryDetailRecord> sizeDetails = item.SizeDetails ?? new List<InspectionHistoryDetailRecord>();
                if (sizeDetails.Count == 0)
                {
                    inspectionHistorySizeGrid.Rows.Add(item.ObjectNumber, cell, "尺寸判定", "—", "—",
                        record.JudgementComplete ? "沒有量測明細" : record.UndeterminedReason,
                        FormatInspectionHistoryGrade(item.SizeGrade));
                }
                else
                {
                    foreach (InspectionHistoryDetailRecord detail in sizeDetails)
                    {
                        inspectionHistorySizeGrid.Rows.Add(item.ObjectNumber, cell, detail.Title,
                            detail.Measured, detail.Range, detail.Condition,
                            string.IsNullOrWhiteSpace(detail.Result)
                                ? FormatInspectionHistoryGrade(item.SizeGrade) : detail.Result);
                    }
                }

                List<InspectionHistoryDetailRecord> defectDetails = item.DefectDetails ?? new List<InspectionHistoryDetailRecord>();
                if (defectDetails.Count == 0)
                {
                    inspectionHistoryDefectGrid.Rows.Add(item.ObjectNumber, cell, "整合結果", "0",
                        FormatInspectionHistoryGrade(item.DefectGrade));
                }
                else
                {
                    foreach (InspectionHistoryDetailRecord detail in defectDetails)
                    {
                        inspectionHistoryDefectGrid.Rows.Add(item.ObjectNumber, cell,
                            string.IsNullOrWhiteSpace(detail.Description) ? detail.Title : detail.Description,
                            detail.Measured, string.IsNullOrWhiteSpace(detail.Result)
                                ? FormatInspectionHistoryGrade(item.DefectGrade) : detail.Result);
                    }
                }
            }

            inspectionHistorySizeEmptyLabel.Visible = inspectionHistorySizeGrid.Rows.Count == 0;
            inspectionHistoryDefectEmptyLabel.Visible = inspectionHistoryDefectGrid.Rows.Count == 0;
            if (!record.JudgementComplete)
            {
                inspectionHistorySizeEmptyLabel.Text = "本次判定未完成：" + (record.UndeterminedReason ?? "原因未記錄");
                inspectionHistoryDefectEmptyLabel.Text = "本次判定未完成：" + (record.UndeterminedReason ?? "原因未記錄");
            }
        }

        private static string GetInspectionHistoryCellName(int row, int column)
        {
            int value = Math.Max(0, row) + 1;
            string letters = string.Empty;
            while (value > 0)
            {
                value--;
                letters = (char)('A' + (value % 26)) + letters;
                value /= 26;
            }
            return letters + "-" + (Math.Max(0, column) + 1).ToString("D2", CultureInfo.CurrentCulture);
        }

        private static string FormatInspectionHistoryGrade(string grade)
        {
            switch (grade)
            {
                case "A": return "A規";
                case "B": return "B規";
                case "Ng": return "NG";
                default: return "待確認";
            }
        }

        private static string FormatHistoryNumber(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.###", CultureInfo.CurrentCulture) : "—";
        }

        private static string GetInspectionHistoryConditionState(ResultReviewGoodJudgementConditionState state)
        {
            switch (state)
            {
                case ResultReviewGoodJudgementConditionState.Passed: return "符合";
                case ResultReviewGoodJudgementConditionState.Failed: return "不符合";
                case ResultReviewGoodJudgementConditionState.Skipped: return "未執行";
                case ResultReviewGoodJudgementConditionState.Disabled: return "停用";
                case ResultReviewGoodJudgementConditionState.NotConfigured: return "未設定";
                default: return "待確認";
            }
        }

        private void SaveCameraInspectionHistory(string imageIdentity, string imagePath, long sequenceNumber)
        {
            if (string.IsNullOrWhiteSpace(imageIdentity) ||
                !imageIdentity.StartsWith(CameraMemoryImageIdentityPrefix, StringComparison.Ordinal))
            {
                return;
            }

            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            if (snapshot == null || !string.Equals(snapshot.ImagePath, imageIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SaveInspectionHistoryRecord(
                snapshot,
                GetCameraInspectionCapturedAt(imagePath),
                sequenceNumber,
                imagePath,
                false);
        }

        private void SaveManualObjectDetectionResultReviewHistory()
        {
            if (cameraDeviceSettings == null || !cameraDeviceSettings.RecordResultReviewRuns ||
                isInspectionHistoryRerunActive)
            {
                return;
            }

            ObjectDetectionResultReviewSnapshot snapshot = objectDetectionResultReviewSnapshot;
            if (snapshot == null || !snapshot.JudgementComplete || string.IsNullOrWhiteSpace(snapshot.ImagePath))
            {
                return;
            }

            string imagePath = snapshot.ImagePath;
            if (imagePath.StartsWith(CameraMemoryImageIdentityPrefix, StringComparison.Ordinal))
            {
                if (!string.Equals(latestCameraInspectionFrameIdentity, imagePath, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(latestCameraInspectionFramePath))
                {
                    SetCameraInspectionStatus("檢測已完成，但無法保存歷史資料：找不到相機原圖路徑。");
                    return;
                }
                imagePath = latestCameraInspectionFramePath;
            }

            if (!File.Exists(imagePath) && !IsOwnedCameraInspectionImagePath(imagePath))
            {
                SetCameraInspectionStatus("檢測已完成，但無法保存歷史資料：原始圖片檔不存在。");
                return;
            }

            SaveInspectionHistoryRecord(
                snapshot,
                DateTime.Now,
                -1,
                imagePath,
                !IsOwnedCameraInspectionImagePath(imagePath));
        }

        private void SaveInspectionHistoryRecord(
            ObjectDetectionResultReviewSnapshot snapshot,
            DateTime inspectedAt,
            long sequenceNumber,
            string imagePath,
            bool archiveSourceImage)
        {
            try
            {
                if (snapshot == null || string.IsNullOrWhiteSpace(imagePath))
                {
                    return;
                }

                var record = new InspectionHistoryBatchRecord
                {
                    BatchId = Guid.NewGuid().ToString("N"),
                    InspectedAt = inspectedAt,
                    CameraSequence = sequenceNumber,
                    ParameterId = snapshot.ParameterId,
                    ImagePath = archiveSourceImage
                        ? string.Empty
                        : (inspectionHistoryStore == null
                            ? imagePath : inspectionHistoryStore.GetStoredPath(imagePath)),
                    JudgementComplete = snapshot.JudgementComplete,
                    UndeterminedReason = snapshot.UndeterminedReason,
                    ImageWidth = snapshot.ImageWidth,
                    ImageHeight = snapshot.ImageHeight,
                    RecipeId = systemParameters == null ? string.Empty : systemParameters.ActiveDetectionRecipeId,
                    RecipeName = snapshot.Parameter == null
                        ? "未指定 Recipe"
                        : (string.IsNullOrWhiteSpace(snapshot.Parameter.DisplayName)
                            ? snapshot.Parameter.Id : snapshot.Parameter.DisplayName)
                };

                IList<ObjectDetectionDefectIntegrationGroup> groups = snapshot.IntegrationGroups ??
                    new List<ObjectDetectionDefectIntegrationGroup>();
                Dictionary<int, ResultReviewDefectGrade> defectGrades = snapshot.Parameter == null
                    ? new Dictionary<int, ResultReviewDefectGrade>()
                    : GetInspectionOverviewDefectGrades(snapshot);
                Dictionary<int, Point> gridPositions = snapshot.ObjectGridPositions ??
                    CreateObjectDetectionResultReviewObjectGridPositions(snapshot.Objects);
                var sizeByObject = new Dictionary<int, List<InspectionHistoryDetailRecord>>();
                if (objectDetectionResultReviewMeasurementsGrid != null)
                {
                    foreach (DataGridViewRow row in objectDetectionResultReviewMeasurementsGrid.Rows)
                    {
                        if (row.IsNewRow) continue;
                        int objectNumber;
                        if (row.Cells.Count < 11 || !int.TryParse(Convert.ToString(row.Cells[0].Value), out objectNumber)) continue;
                        List<InspectionHistoryDetailRecord> details;
                        if (!sizeByObject.TryGetValue(objectNumber, out details))
                        {
                            details = new List<InspectionHistoryDetailRecord>();
                            sizeByObject[objectNumber] = details;
                        }
                        string unit = Convert.ToString(row.Cells[9].Value);
                        string measured = "最小 " + Convert.ToString(row.Cells[6].Value) +
                            "／平均 " + Convert.ToString(row.Cells[7].Value) +
                            "／最大 " + Convert.ToString(row.Cells[8].Value) +
                            (string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit);
                        details.Add(new InspectionHistoryDetailRecord
                        {
                            Title = Convert.ToString(row.Cells[2].Value) + " (#" + Convert.ToString(row.Cells[1].Value) + ")",
                            Measured = measured,
                            Condition = Convert.ToString(row.Cells[10].Value),
                            Result = string.Empty,
                            Description = Convert.ToString(row.Cells[3].Value) + "，" +
                                Convert.ToString(row.Cells[4].Value) + "，線數 " + Convert.ToString(row.Cells[5].Value)
                        });
                    }
                }
                foreach (ResultReviewGoodJudgementObject judgement in
                    objectDetectionResultReviewGoodJudgementResults ?? new List<ResultReviewGoodJudgementObject>())
                {
                    List<InspectionHistoryDetailRecord> details;
                    if (!sizeByObject.TryGetValue(judgement.ObjectNumber, out details))
                    {
                        details = new List<InspectionHistoryDetailRecord>();
                        sizeByObject[judgement.ObjectNumber] = details;
                    }
                    foreach (ResultReviewGoodJudgementCondition condition in
                        judgement.Conditions ?? new List<ResultReviewGoodJudgementCondition>())
                    {
                        ObjectDetectionGoodJudgementRuleSettings rule = condition.Rule;
                        if (rule == null) continue;
                        details.Add(new InspectionHistoryDetailRecord
                        {
                            Title = "判定條件 " + rule.Number.ToString(CultureInfo.CurrentCulture) + "：" + rule.Name,
                            Measured = "A規 " + FormatHistoryNumber(condition.AValue) +
                                "；B規 " + FormatHistoryNumber(condition.BValue),
                            Range = "A規 " + (rule.SpecificationExpression ?? string.Empty) +
                                "；B規 " + (rule.AlternativeSpecificationExpression ?? string.Empty),
                            Condition = "A規 " + GetInspectionHistoryConditionState(condition.AState) +
                                "；B規 " + GetInspectionHistoryConditionState(condition.BState),
                            Description = "A規公式：" + (rule.CalculationExpression ?? string.Empty) +
                                "；B規公式：" + (rule.AlternativeCalculationExpression ?? string.Empty)
                        });
                    }
                }

                foreach (ObjectDefinitionDetectedObject detected in snapshot.Objects ?? new List<ObjectDefinitionDetectedObject>())
                {
                    Point position;
                    if (!gridPositions.TryGetValue(detected.Number, out position)) position = Point.Empty;
                    ResultReviewGoodJudgementGrade sizeGrade;
                    if (snapshot.SizeGrades == null || !snapshot.SizeGrades.TryGetValue(detected.Number, out sizeGrade))
                    {
                        sizeGrade = ResultReviewGoodJudgementGrade.Pending;
                    }
                    ResultReviewDefectGrade defectGrade;
                    if (!defectGrades.TryGetValue(detected.Number, out defectGrade)) defectGrade = ResultReviewDefectGrade.Pending;
                    var item = new InspectionHistoryObjectRecord
                    {
                        ObjectNumber = detected.Number,
                        GridRow = position.Y,
                        GridColumn = position.X,
                        BoundsX = detected.Bounds.X,
                        BoundsY = detected.Bounds.Y,
                        BoundsWidth = detected.Bounds.Width,
                        BoundsHeight = detected.Bounds.Height,
                        SizeGrade = sizeGrade.ToString(),
                        DefectGrade = defectGrade.ToString(),
                        CombinedGrade = GetInspectionOverviewCombinedGrade(sizeGrade, defectGrade).ToString(),
                        SizeDetails = sizeByObject.ContainsKey(detected.Number)
                            ? sizeByObject[detected.Number] : new List<InspectionHistoryDetailRecord>(),
                        DefectDetails = new List<InspectionHistoryDetailRecord>()
                    };

                    foreach (ObjectDetectionDefectIntegrationGroup group in groups.Where(candidate =>
                        candidate != null && candidate.ObjectNumber == detected.Number))
                    {
                        string sources = string.Join("、", (group.Candidates ?? new List<ObjectDetectionDefectIntegrationCandidate>())
                            .Select(candidate => GetInspectionHistoryDefectSource(candidate))
                            .Distinct(StringComparer.CurrentCulture));
                        item.DefectDetails.Add(new InspectionHistoryDetailRecord
                        {
                            Title = string.IsNullOrWhiteSpace(sources) ? "整合缺陷" : sources,
                            Measured = "1",
                            Result = "檢出",
                            Description = "缺陷位置 X=" + group.Bounds.X.ToString("0.0", CultureInfo.CurrentCulture) +
                                "，Y=" + group.Bounds.Y.ToString("0.0", CultureInfo.CurrentCulture) +
                                "，寬 " + group.Bounds.Width.ToString("0.0", CultureInfo.CurrentCulture) +
                                "，高 " + group.Bounds.Height.ToString("0.0", CultureInfo.CurrentCulture) +
                                "；來源候選 " + (group.Candidates == null ? 0 : group.Candidates.Count).ToString(CultureInfo.CurrentCulture)
                        });
                    }
                    if (item.DefectDetails.Count == 0)
                    {
                        item.DefectDetails.Add(new InspectionHistoryDetailRecord
                        {
                            Title = "整合結果",
                            Measured = "0",
                            Result = defectGrade == ResultReviewDefectGrade.A ? "未檢出" : "待確認",
                            Description = defectGrade == ResultReviewDefectGrade.A
                                ? "沒有符合整合條件的缺陷。"
                                : (snapshot.UndeterminedReason ?? "缺陷判定未完成。")
                        });
                    }
                    record.Objects.Add(item);
                }

                record.ObjectCount = record.Objects.Count;
                record.SizeACount = record.Objects.Count(item => item.SizeGrade == "A");
                record.SizeBCount = record.Objects.Count(item => item.SizeGrade == "B");
                record.SizeNgCount = record.Objects.Count(item => item.SizeGrade == "Ng");
                record.SizePendingCount = record.Objects.Count(item => item.SizeGrade == "Pending");
                record.DefectACount = record.Objects.Count(item => item.DefectGrade == "A");
                record.DefectNgCount = record.Objects.Count(item => item.DefectGrade == "Ng");
                record.DefectPendingCount = record.Objects.Count(item => item.DefectGrade == "Pending");
                record.CombinedACount = record.Objects.Count(item => item.CombinedGrade == "A");
                record.CombinedBCount = record.Objects.Count(item => item.CombinedGrade == "B");
                record.CombinedNgCount = record.Objects.Count(item => item.CombinedGrade == "Ng");
                record.CombinedPendingCount = record.Objects.Count(item => item.CombinedGrade == "Pending");

                SystemParameterSettings settingsSnapshot = (SystemParameterSettings)
                    CloneObjectDetectionResultReviewModel(systemParameters);
                if (inspectionHistoryStore == null)
                {
                    inspectionHistoryStore = new InspectionHistoryStore(AppDomain.CurrentDomain.BaseDirectory);
                }
                InspectionHistoryStore store = inspectionHistoryStore;
                string fallbackRecipeName = record.RecipeName;
                Task.Run(delegate
                {
                    try
                    {
                        if (archiveSourceImage)
                        {
                            try
                            {
                                string imageDirectory = Path.Combine(
                                    AppDomain.CurrentDomain.BaseDirectory,
                                    "snapshot",
                                    inspectedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                                Directory.CreateDirectory(imageDirectory);
                                string archivedImagePath = Path.Combine(
                                    imageDirectory,
                                    "result_review_" + inspectedAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
                                    "_" + Guid.NewGuid().ToString("N") + Path.GetExtension(imagePath));
                                File.Copy(imagePath, archivedImagePath, false);
                                record.ImagePath = store.GetStoredPath(archivedImagePath);
                            }
                            catch (Exception copyException)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    "Inspection image archive failed; keeping source path: " + copyException);
                                record.ImagePath = store.GetStoredPath(imagePath);
                            }
                        }
                        record.RecipeSnapshotPath = store.SaveRecipeSnapshot(settingsSnapshot);
                        DetectionRecipeCatalogEntry activeRecipe = new DetectionRecipeCatalogService(
                            AppDomain.CurrentDomain.BaseDirectory).LoadParameterList().FirstOrDefault(item =>
                                string.Equals(item.Id, record.RecipeId, StringComparison.Ordinal));
                        if (activeRecipe != null) record.RecipeName = activeRecipe.DisplayName;
                        else record.RecipeName = fallbackRecipeName;
                        store.SaveBatch(record);
                        if (IsHandleCreated && !IsDisposed)
                        {
                            BeginInvoke(new Action(delegate
                            {
                                if (isInspectionHistoryActive) RefreshInspectionHistory();
                            }));
                        }
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine("Inspection history save failed: " + exception);
                        if (IsHandleCreated && !IsDisposed)
                        {
                            try
                            {
                                BeginInvoke(new Action(delegate
                                {
                                    SetCameraInspectionStatus("檢測已完成，但歷史紀錄保存失敗：" + exception.Message);
                                }));
                            }
                            catch (InvalidOperationException) { }
                        }
                    }
                });
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create inspection history record: " + exception);
            }
        }

        private static string GetInspectionHistoryDefectSource(ObjectDetectionDefectIntegrationCandidate candidate)
        {
            if (candidate == null) return "缺陷";
            if (candidate.IsFrequency) return candidate.FrequencyIndex > 0 ? "頻域異常二" : "頻域異常";
            string core = string.IsNullOrWhiteSpace(candidate.CoreKey) ? "缺陷條件" : candidate.CoreKey;
            return candidate.IsBright.HasValue ? core + (candidate.IsBright.Value ? "亮部" : "暗部") : core;
        }

        private static DateTime GetCameraInspectionCapturedAt(string imagePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(imagePath ?? string.Empty);
            const string prefix = "camera_frame_";
            DateTime capturedAt;
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                fileName.Length >= prefix.Length + 19 &&
                DateTime.TryParseExact(
                    fileName.Substring(prefix.Length, 19),
                    "yyyyMMdd_HHmmss_fff",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out capturedAt))
            {
                return capturedAt;
            }
            return DateTime.Now;
        }

        private void StartInspectionHistoryRerun(
            InspectionHistoryBatchRecord record,
            string snapshotPath,
            string imagePath)
        {
            try
            {
                SystemParameterSettings historical = new SystemParameterIniService(snapshotPath).Load();
                if (historical == null || historical.ObjectDetectionParameters == null ||
                    historical.ObjectDetectionParameters.Count == 0)
                {
                    throw new InvalidDataException("歷史 Recipe 快照內沒有檢測參數。");
                }

                ObjectDetectionParameterSettings historicalParameter;
                if (!string.IsNullOrWhiteSpace(record.ParameterId))
                {
                    List<ObjectDetectionParameterSettings> matchingParameters =
                        historical.ObjectDetectionParameters.Where(parameter => parameter != null &&
                            string.Equals(parameter.Id, record.ParameterId, StringComparison.Ordinal)).ToList();
                    if (matchingParameters.Count != 1)
                    {
                        throw new InvalidDataException(
                            "歷史紀錄指定的檢測參數 ID 在參數快照中不存在或重複。為避免使用錯誤參數，已取消重新檢測。");
                    }
                    historicalParameter = matchingParameters[0];
                }
                else if (historical.ObjectDetectionParameters.Count == 1 &&
                    historical.ObjectDetectionParameters[0] != null)
                {
                    // Legacy rows with a single parameter are unambiguous.
                    historicalParameter = historical.ObjectDetectionParameters[0];
                }
                else
                {
                    throw new InvalidDataException(
                        "這筆舊歷史紀錄沒有保存當時使用的檢測參數 ID，而且快照含有多個參數。" +
                        "為避免選錯參數，已取消重新檢測；請先由操作人員確認該次使用的參數。");
                }

                inspectionHistoryPreviousSystemParameters = systemParameters;
                inspectionHistoryPreviousActiveParameterId = activeObjectDetectionParameterId;
                inspectionHistoryPreviousReviewParameterId = objectDetectionResultReviewSelectedParameterId;
                inspectionHistoryPreviousReferenceImageSize = objectDetectionResultReviewReferenceImageSize;
                isInspectionHistoryRerunActive = true;
                historical.LastImagePath = imagePath;
                historical.ActiveDetectionRecipeId = record.RecipeId ?? string.Empty;
                systemParameters = historical;
                activeObjectDetectionParameterId = historicalParameter.Id;
                objectDetectionResultReviewSelectedParameterId = activeObjectDetectionParameterId;
                objectDetectionResultReviewReferenceImageSize = Size.Empty;
                objectDetectionResultReviewImagePath = string.Empty;
                Interlocked.Increment(ref detectionRecipeGeneration);

                int reviewIndex = functionListBox.Items.IndexOf(ObjectDetectionResultReviewMenuText);
                if (reviewIndex < 0) throw new InvalidOperationException("找不到「參數結果確認」功能入口。");
                functionListBox.SelectedIndex = reviewIndex;
                SetCameraInspectionStatus("已載入歷史 Recipe「" + record.RecipeName + "」，正在重新檢測" +
                    (record.CameraSequence > 0
                        ? "影像 #" + record.CameraSequence.ToString(CultureInfo.CurrentCulture)
                        : "參數結果確認圖片") + "。");
                BeginInvoke(new Action(async delegate
                {
                    try { await LoadInspectionHistoryImageAndRunAsync(imagePath, historicalParameter.Id); }
                    catch (Exception exception)
                    {
                        RestoreInspectionHistoryRerunContext();
                        int historyIndex = functionListBox.Items.IndexOf(InspectionHistoryMenuText);
                        if (historyIndex >= 0) functionListBox.SelectedIndex = historyIndex;
                        MessageBox.Show(this, "歷史影像重新檢測失敗：\r\n" + exception.Message,
                            "歷史資料", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            }
            catch (Exception exception)
            {
                RestoreInspectionHistoryRerunContext();
                MessageBox.Show(this, "無法確認歷史檢測參數：\r\n" + exception.Message,
                    "歷史資料", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadInspectionHistoryImageAndRunAsync(string imagePath, string expectedParameterId)
        {
            if (!IsObjectDetectionResultReviewUiAvailable())
            {
                throw new InvalidOperationException("參數結果確認頁面尚未就緒。");
            }
            Size imageSize = await Task.Run(() => LargeImageSource.ReadImageSize(imagePath));
            isLoadingImage = true;
            SetObjectDetectionResultReviewControlsEnabled(false);
            try
            {
                ClearObjectDetectionResultReviewResults();
                systemParameters.LastImagePath = imagePath;
                await LoadImageIntoOriginalDisplaysAsync(imagePath, CancellationToken.None);
                objectDetectionResultReviewImagePath = imagePath;
                objectDetectionResultReviewReferenceImageSize = imageSize;
                objectDetectionResultReviewImageLabel.Text = "歷史影像：" + Path.GetFileName(imagePath) + "  (" +
                    imageSize.Width.ToString("N0", CultureInfo.CurrentCulture) + " x " +
                    imageSize.Height.ToString("N0", CultureInfo.CurrentCulture) + ")";
                RefreshObjectDetectionFlatFieldDisplay();
                RefreshObjectDetectionMeasurementDisplay();
                RefreshObjectDetectionDefectDisplay();
            }
            finally
            {
                isLoadingImage = false;
                SetObjectDetectionResultReviewControlsEnabled(true);
            }

            ResultReviewParameterChoice selectedChoice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (selectedChoice == null || selectedChoice.Parameter == null ||
                !string.Equals(selectedChoice.Parameter.Id, expectedParameterId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "參數結果確認頁面未選到歷史紀錄指定的檢測參數，已取消運算以避免選錯。");
            }

            await RunObjectDetectionResultReviewAsync();
            if (objectDetectionResultReviewSnapshot == null ||
                !string.Equals(
                    objectDetectionResultReviewSnapshot.ParameterId,
                    expectedParameterId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "重新檢測使用的參數 ID 與歷史紀錄不一致，已取消本次結果。");
            }
        }

        private void RestoreInspectionHistoryRerunContext()
        {
            if (!isInspectionHistoryRerunActive) return;
            systemParameters = inspectionHistoryPreviousSystemParameters ?? systemParameters;
            activeObjectDetectionParameterId = inspectionHistoryPreviousActiveParameterId;
            objectDetectionResultReviewSelectedParameterId = inspectionHistoryPreviousReviewParameterId;
            objectDetectionResultReviewReferenceImageSize = inspectionHistoryPreviousReferenceImageSize;
            inspectionHistoryPreviousSystemParameters = null;
            isInspectionHistoryRerunActive = false;
            inspectionHistoryNeedsReviewRefresh = true;
        }

        private void InspectionHistorySearchButton_Click(object sender, EventArgs e)
        {
            RefreshInspectionHistory();
        }

        private void RefreshInspectionHistory()
        {
            if (inspectionHistoryRecordsGrid == null || inspectionHistoryStore == null)
            {
                return;
            }

            try
            {
                DateTime date = inspectionHistoryDatePicker == null
                    ? DateTime.Today : inspectionHistoryDatePicker.Value.Date;
                IList<InspectionHistoryBatchRecord> allRecords = inspectionHistoryStore.LoadBatches(date);
                string previousRecipe = inspectionHistoryRecipeFilter == null
                    ? "全部 Recipe" : Convert.ToString(inspectionHistoryRecipeFilter.SelectedItem);
                try
                {
                    inspectionHistoryRecipeFilter.BeginUpdate();
                    inspectionHistoryRecipeFilter.Items.Clear();
                    inspectionHistoryRecipeFilter.Items.Add("全部 Recipe");
                    foreach (string recipe in allRecords.Select(item => item.RecipeName)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase))
                    {
                        inspectionHistoryRecipeFilter.Items.Add(recipe);
                    }
                    int recipeIndex = inspectionHistoryRecipeFilter.Items.IndexOf(previousRecipe);
                    inspectionHistoryRecipeFilter.SelectedIndex = recipeIndex >= 0 ? recipeIndex : 0;
                }
                finally
                {
                    inspectionHistoryRecipeFilter.EndUpdate();
                }

                string selectedRecipe = Convert.ToString(inspectionHistoryRecipeFilter.SelectedItem);
                string selectedStatus = Convert.ToString(inspectionHistoryStatusFilter.SelectedItem);
                IEnumerable<InspectionHistoryBatchRecord> filtered = allRecords;
                if (!string.IsNullOrWhiteSpace(selectedRecipe) && selectedRecipe != "全部 Recipe")
                {
                    filtered = filtered.Where(item => string.Equals(
                        item.RecipeName, selectedRecipe, StringComparison.OrdinalIgnoreCase));
                }
                if (selectedStatus == "含 NG")
                {
                    filtered = filtered.Where(item => item.SizeNgCount > 0 || item.DefectNgCount > 0 || item.CombinedNgCount > 0);
                }
                else if (selectedStatus == "含待確認")
                {
                    filtered = filtered.Where(item => !item.JudgementComplete || item.SizePendingCount > 0 ||
                        item.DefectPendingCount > 0 || item.CombinedPendingCount > 0);
                }
                else if (selectedStatus == "全部通過")
                {
                    filtered = filtered.Where(item => item.JudgementComplete && item.SizeNgCount == 0 &&
                        item.DefectNgCount == 0 && item.CombinedNgCount == 0 && item.CombinedPendingCount == 0);
                }

                List<InspectionHistoryBatchRecord> records = filtered.ToList();
                inspectionHistoryRecordsGrid.Rows.Clear();
                inspectionHistoryRecordsGrid.Tag = null;
                inspectionHistorySelectedBatchId = null;
                foreach (InspectionHistoryBatchRecord record in records)
                {
                    int row = inspectionHistoryRecordsGrid.Rows.Add(
                        record.InspectedAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture),
                        record.CameraSequence > 0
                            ? "影像 #" + record.CameraSequence.ToString(CultureInfo.CurrentCulture)
                            : "參數確認",
                        string.IsNullOrWhiteSpace(record.RecipeName) ? "未指定 Recipe" : record.RecipeName,
                        record.ObjectCount.ToString(CultureInfo.CurrentCulture) + " 片",
                        FormatHistoryGradeSummary(record.SizeACount + record.SizeBCount,
                            record.SizeNgCount, record.SizePendingCount),
                        FormatHistoryGradeSummary(record.DefectACount,
                            record.DefectNgCount, record.DefectPendingCount),
                        FormatHistoryGradeSummary(record.CombinedACount + record.CombinedBCount,
                            record.CombinedNgCount, record.CombinedPendingCount));
                    inspectionHistoryRecordsGrid.Rows[row].Tag = record.BatchId;
                    inspectionHistoryRecordsGrid.Rows[row].Cells[0].ToolTipText =
                        record.JudgementComplete ? string.Empty : record.UndeterminedReason ?? "判定未完成";
                }

                inspectionHistoryRecordsEmptyLabel.Text = allRecords.Count == 0
                    ? "此日期尚無本機檢測紀錄。"
                    : "目前篩選條件沒有符合的檢測紀錄。";
                inspectionHistoryRecordsEmptyLabel.Visible = records.Count == 0;
                UpdateInspectionHistorySummary(records);
                ResetInspectionHistoryDetails();
            }
            catch (Exception exception)
            {
                inspectionHistoryRecordsGrid.Rows.Clear();
                inspectionHistoryRecordsEmptyLabel.Text = "讀取歷史資料失敗：" + exception.Message;
                inspectionHistoryRecordsEmptyLabel.Visible = true;
                ResetInspectionHistoryDetails();
            }
        }

        private void InspectionHistoryRecordsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= inspectionHistoryRecordsGrid.Rows.Count)
            {
                return;
            }

            string recordId = inspectionHistoryRecordsGrid.Rows[e.RowIndex].Tag as string;
            if (string.IsNullOrWhiteSpace(recordId) ||
                string.Equals(recordId, inspectionHistorySelectedBatchId, StringComparison.Ordinal))
            {
                return;
            }

            inspectionHistorySelectedBatchId = recordId;
            inspectionHistoryRecordsGrid.Tag = recordId;
            LoadInspectionHistoryDetails(recordId);
        }

        private void InspectionHistoryRecordsGrid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
            {
                return;
            }

            inspectionHistoryRecordsGrid.ClearSelection();
            inspectionHistoryRecordsGrid.Rows[e.RowIndex].Selected = true;
            inspectionHistoryRecordsGrid.CurrentCell = inspectionHistoryRecordsGrid.Rows[e.RowIndex].Cells[0];
            string batchId = inspectionHistoryRecordsGrid.Rows[e.RowIndex].Tag as string;
            if (!string.IsNullOrWhiteSpace(batchId) &&
                !string.Equals(batchId, inspectionHistorySelectedBatchId, StringComparison.Ordinal))
            {
                inspectionHistorySelectedBatchId = batchId;
                LoadInspectionHistoryDetails(batchId);
            }
        }

        private void InspectionHistoryRerunMenuItem_Click(object sender, EventArgs e)
        {
            if (inspectionHistoryRecordsGrid == null ||
                inspectionHistoryRecordsGrid.SelectedRows.Count == 0)
            {
                return;
            }

            string batchId = inspectionHistoryRecordsGrid.SelectedRows[0].Tag as string;
            InspectionHistoryBatchRecord record = inspectionHistoryStore.LoadBatch(
                inspectionHistoryDatePicker.Value.Date, batchId);
            if (record == null)
            {
                MessageBox.Show(this, "找不到這筆歷史檢測資料。", "歷史資料",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string imagePath = inspectionHistoryStore.ResolveStoredPath(record.ImagePath);
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show(this,
                    "這筆紀錄沒有保存可重新檢測的原始大圖。\r\n判定紀錄仍可查閱，但無法重新送入參數結果確認。",
                    "沒有保存原圖", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string snapshotPath = inspectionHistoryStore.ResolveStoredPath(record.RecipeSnapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
            {
                MessageBox.Show(this, "找不到這筆紀錄所使用的 Recipe 快照，無法使用當時參數重新檢測。",
                    "Recipe 快照遺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            StartInspectionHistoryRerun(record, snapshotPath, imagePath);
        }
    }
}
