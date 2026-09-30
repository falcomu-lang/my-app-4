using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private DataGridView objectDetectionGoodJudgementRulesGrid;
        private TextBox objectDetectionGoodJudgementNameTextBox;
        private TextBox objectDetectionGoodJudgementCalculationTextBox;
        private TextBox objectDetectionGoodJudgementSpecificationTextBox;
        private TextBox objectDetectionGoodJudgementAlternativeCalculationTextBox;
        private TextBox objectDetectionGoodJudgementAlternativeSpecificationTextBox;
        private CheckBox objectDetectionGoodJudgementEnabledCheckBox;
        private Label objectDetectionGoodJudgementStatusLabel;
        private Form objectDetectionGoodJudgementSyntaxHelpForm;
        private Button objectDetectionGoodJudgementApplyButton;
        private Button objectDetectionGoodJudgementResetButton;
        private Button objectDetectionGoodJudgementSaveButton;
        private Button objectDetectionGoodJudgementMoveUpButton;
        private Button objectDetectionGoodJudgementMoveDownButton;
        private string objectDetectionGoodJudgementEditingRuleId;
        private bool objectDetectionGoodJudgementCreateNewRule;
        private ContextMenuStrip objectDetectionGoodJudgementContextMenu;

        private static int GetNextObjectDetectionGoodJudgementRuleNumber(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || parameter.GoodJudgementRules == null)
            {
                return 1;
            }

            int maximum = parameter.GoodJudgementRules
                .Where(rule => rule != null && rule.Number > 0)
                .Select(rule => rule.Number)
                .DefaultIfEmpty(0)
                .Max();
            return maximum == int.MaxValue ? int.MaxValue : maximum + 1;
        }

        private void BuildObjectDetectionGoodJudgementTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[2];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionGoodJudgementRulesGrid = null;
                objectDetectionGoodJudgementNameTextBox = null;
                objectDetectionGoodJudgementCalculationTextBox = null;
                objectDetectionGoodJudgementSpecificationTextBox = null;
                objectDetectionGoodJudgementAlternativeCalculationTextBox = null;
                objectDetectionGoodJudgementAlternativeSpecificationTextBox = null;
                objectDetectionGoodJudgementEnabledCheckBox = null;
                objectDetectionGoodJudgementStatusLabel = null;
                objectDetectionGoodJudgementApplyButton = null;
                objectDetectionGoodJudgementResetButton = null;
                objectDetectionGoodJudgementSaveButton = null;
                objectDetectionGoodJudgementMoveUpButton = null;
                objectDetectionGoodJudgementMoveDownButton = null;
                objectDetectionGoodJudgementEditingRuleId = string.Empty;
                objectDetectionGoodJudgementCreateNewRule = true;

                var pageLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                pageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
                pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                tabPage.Controls.Add(pageLayout);

                var headerPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    Padding = new Padding(4, 2, 4, 2),
                    Margin = new Padding(0)
                };
                headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
                headerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                var headerLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    Text = "尺寸良品判斷條件",
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(45, 53, 65)
                };
                var syntaxHelpButton = new Button
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2),
                    Text = "語法說明",
                };
                syntaxHelpButton.Click += delegate
                {
                    if (objectDetectionGoodJudgementSyntaxHelpForm == null ||
                        objectDetectionGoodJudgementSyntaxHelpForm.IsDisposed)
                    {
                        objectDetectionGoodJudgementSyntaxHelpForm =
                            CreateObjectDetectionGoodJudgementSyntaxHelpForm();
                        objectDetectionGoodJudgementSyntaxHelpForm.FormClosed += delegate
                        {
                            objectDetectionGoodJudgementSyntaxHelpForm = null;
                        };
                    }

                    if (objectDetectionGoodJudgementSyntaxHelpForm.WindowState ==
                        FormWindowState.Minimized)
                    {
                        objectDetectionGoodJudgementSyntaxHelpForm.WindowState =
                            FormWindowState.Normal;
                    }
                    if (!objectDetectionGoodJudgementSyntaxHelpForm.Visible)
                    {
                        objectDetectionGoodJudgementSyntaxHelpForm.Show();
                    }
                    objectDetectionGoodJudgementSyntaxHelpForm.BringToFront();
                };
                headerPanel.Controls.Add(headerLabel, 0, 0);
                headerPanel.Controls.Add(syntaxHelpButton, 1, 0);
                pageLayout.Controls.Add(headerPanel, 0, 0);

                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    Padding = new Padding(4)
                };
                pageLayout.Controls.Add(contentPanel, 0, 1);

                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 28,
                    Text = "尺寸良品判斷條件來源：" + sourceName +
                        "。計算式可使用量測編號，例如 (1)、(2)、(1)+(2)。",
                    ForeColor = Color.FromArgb(75, 83, 95),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                contentPanel.Controls.Add(sourceLabel);

                var editorGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    Height = 230,
                    Text = "判定條件設定",
                    Padding = new Padding(8)
                };
                var editorLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 7,
                    AutoSize = false
                };
                editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
                editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                for (int row = 0; row < 6; row++)
                {
                    editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                }
                editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                objectDetectionGoodJudgementNameTextBox = new TextBox { Dock = DockStyle.Fill };
                objectDetectionGoodJudgementCalculationTextBox = new TextBox { Dock = DockStyle.Fill };
                objectDetectionGoodJudgementSpecificationTextBox = new TextBox { Dock = DockStyle.Fill };
                objectDetectionGoodJudgementAlternativeCalculationTextBox = new TextBox { Dock = DockStyle.Fill };
                objectDetectionGoodJudgementAlternativeSpecificationTextBox = new TextBox { Dock = DockStyle.Fill };
                objectDetectionGoodJudgementEnabledCheckBox = new CheckBox
                {
                    Dock = DockStyle.Left,
                    AutoSize = true,
                    Text = "啟用此條件",
                    Checked = true
                };
                objectDetectionGoodJudgementStatusLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    ForeColor = Color.FromArgb(75, 83, 95),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                AddObjectDetectionGoodJudgementEditorRow(
                    editorLayout, 0, "條件名稱", objectDetectionGoodJudgementNameTextBox);
                AddObjectDetectionGoodJudgementEditorRow(
                    editorLayout, 1, "A 規計算式", objectDetectionGoodJudgementCalculationTextBox);
                AddObjectDetectionGoodJudgementEditorRow(
                    editorLayout, 2, "A 規規格", objectDetectionGoodJudgementSpecificationTextBox);
                AddObjectDetectionGoodJudgementEditorRow(
                    editorLayout, 3, "B 規計算式", objectDetectionGoodJudgementAlternativeCalculationTextBox);
                AddObjectDetectionGoodJudgementEditorRow(
                    editorLayout, 4, "B 規規格", objectDetectionGoodJudgementAlternativeSpecificationTextBox);
                editorLayout.Controls.Add(objectDetectionGoodJudgementEnabledCheckBox, 1, 5);
                editorLayout.Controls.Add(objectDetectionGoodJudgementStatusLabel, 1, 6);
                editorGroup.Controls.Add(editorLayout);
                contentPanel.Controls.Add(editorGroup);

                var actionPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 38,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(0, 4, 0, 4)
                };
                objectDetectionGoodJudgementApplyButton = new Button
                {
                    Width = 86,
                    Height = 28,
                    Text = "新增"
                };
                objectDetectionGoodJudgementApplyButton.Click += delegate
                {
                    ApplyObjectDetectionGoodJudgementRule(parameter);
                };
                objectDetectionGoodJudgementResetButton = new Button
                {
                    Width = 86,
                    Height = 28,
                    Text = "清除"
                };
                objectDetectionGoodJudgementResetButton.Click += delegate
                {
                    ResetObjectDetectionGoodJudgementEditor(parameter);
                };
                objectDetectionGoodJudgementMoveUpButton = new Button
                {
                    Width = 34,
                    Height = 28,
                    Text = "↑",
                    Enabled = false
                };
                objectDetectionGoodJudgementMoveUpButton.Click += delegate
                {
                    MoveObjectDetectionGoodJudgementRule(parameter, -1);
                };
                objectDetectionGoodJudgementMoveDownButton = new Button
                {
                    Width = 34,
                    Height = 28,
                    Text = "↓",
                    Enabled = false
                };
                objectDetectionGoodJudgementMoveDownButton.Click += delegate
                {
                    MoveObjectDetectionGoodJudgementRule(parameter, 1);
                };
                objectDetectionGoodJudgementSaveButton = new Button
                {
                    Width = 130,
                    Height = 28,
                    Text = "保存判定條件"
                };
                objectDetectionGoodJudgementSaveButton.Click += delegate
                {
                    SaveSystemParameters();
                    objectDetectionGoodJudgementStatusLabel.Text = "尺寸良品判斷條件已保存到參數檔";
                    statusLabel.Text = parameter.DisplayName + " 已保存尺寸良品判斷條件";
                };
                actionPanel.Controls.Add(objectDetectionGoodJudgementApplyButton);
                actionPanel.Controls.Add(objectDetectionGoodJudgementResetButton);
                actionPanel.Controls.Add(objectDetectionGoodJudgementMoveUpButton);
                actionPanel.Controls.Add(objectDetectionGoodJudgementMoveDownButton);
                actionPanel.Controls.Add(objectDetectionGoodJudgementSaveButton);
                contentPanel.Controls.Add(actionPanel);

                var recordsGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    Height = 250,
                    Text = "尺寸良品判斷條件紀錄",
                    Padding = new Padding(8)
                };
                objectDetectionGoodJudgementRulesGrid = CreateObjectDetectionGoodJudgementRulesGrid(parameter);
                recordsGroup.Controls.Add(objectDetectionGoodJudgementRulesGrid);
                contentPanel.Controls.Add(recordsGroup);

                RefreshObjectDetectionGoodJudgementRulesGrid(parameter);
                UpdateObjectDetectionGoodJudgementMoveButtons();
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }

        private static Form CreateObjectDetectionGoodJudgementSyntaxHelpForm()
        {
            var helpForm = new Form
            {
                Text = "尺寸良品判斷條件 - 語法說明",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = true,
                MaximizeBox = true,
                ShowInTaskbar = true,
                MinimumSize = new Size(560, 380),
                Size = new Size(780, 620),
                BackColor = Color.FromArgb(244, 247, 250)
            };
            var syntaxTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window,
                Margin = new Padding(10),
                Text = BuildObjectDetectionGoodJudgementSyntaxHelpText()
            };
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            contentPanel.Controls.Add(syntaxTextBox);
            helpForm.Controls.Add(contentPanel);
            return helpForm;
        }

        private static string BuildObjectDetectionGoodJudgementSyntaxHelpText()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "尺寸良品判斷條件語法與用法",
                "",
                "一、量測編號與基本運算",
                "(1)、(2)、(3) 代表量測資料紀錄的編號。單線量測使用對應編號，例如 (1)。",
                "計算式支援 +、-、*、/、% 與括號，可組合多筆量測結果。",
                "例：(1)+(2)",
                "",
                "二、平行量測統計函式",
                "MIN(n)：第 n 筆平行量測中所有平行線長度的最小值。",
                "AVG(n)：第 n 筆平行量測中所有平行線長度的平均值。",
                "MAX(n)：第 n 筆平行量測中所有平行線長度的最大值。",
                "函式名稱不分大小寫，例如 MIN(1)、min(1)、Avg(1) 意義相同。",
                "這三個函式只適用於量測模式為 Parallel 的紀錄；Single 請使用 (n)。",
                "未命中 MASK 的平行樣本以長度 0 納入統計。",
                "例：AVG(1)；MAX(1)-MIN(1)",
                "",
                "三、舊版跨量測聚合語法",
                "min((1)(2)(3)) 或 min((1),(2),(3))：在多個量測子式中取最小值。",
                "max((1)(2)(3)) 或 max((1),(2),(3))：在多個量測子式中取最大值。",
                "舊版巢狀子式與四則運算寫法保留。",
                "例：max(((2)-(1))((4)-(3)))",
                "例：max((1)(2)(3))+1.5",
                "",
                "四、規格欄位",
                "規格中的 x 代表計算式的結果。支援 x<5、x<=5、5<x<10、10>=x>=5。",
                "先判定 A 規；A 規符合即通過，A 規不符合才接著判定 B 規。",
                "B 規可以獨立使用；A 規計算錯誤或資料不足時會列為待確認，不會當作 A 規不符合。",
                "例：計算式 AVG(1)，規格 x>=10。",
                "",
                "判定條件會在「參數結果確認」頁面，依每一枚物件的量測結果逐一執行。"
            });
        }

        private static void AddObjectDetectionGoodJudgementEditorRow(
            TableLayoutPanel layout,
            int row,
            string labelText,
            Control editor)
        {
            layout.Controls.Add(
                new Label
                {
                    Dock = DockStyle.Fill,
                    Text = labelText,
                    TextAlign = ContentAlignment.MiddleLeft
                },
                0,
                row);
            layout.Controls.Add(editor, 1, row);
        }

        private DataGridView CreateObjectDetectionGoodJudgementRulesGrid(
            ObjectDetectionParameterSettings parameter)
        {
            var grid = new DataGridView
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
                BorderStyle = BorderStyle.FixedSingle
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodJudgementNumber",
                HeaderText = "編號",
                FillWeight = 9
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodJudgementName",
                HeaderText = "條件名稱",
                FillWeight = 18
            });
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "GoodJudgementEnabled",
                HeaderText = "啟用",
                FillWeight = 9
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodJudgementCalculation",
                HeaderText = "A 規計算式",
                FillWeight = 20
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodJudgementSpecification",
                HeaderText = "A 規規格",
                FillWeight = 17
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodJudgementAlternativeCalculation",
                HeaderText = "B 規計算式",
                FillWeight = 20
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodJudgementAlternativeSpecification",
                HeaderText = "B 規規格",
                FillWeight = 17
            });
            grid.ContextMenuStrip = BuildObjectDetectionGoodJudgementContextMenu(parameter);
            grid.CellMouseDown += ObjectDetectionGoodJudgementRulesGrid_CellMouseDown;
            grid.SelectionChanged += delegate { UpdateObjectDetectionGoodJudgementMoveButtons(); };
            return grid;
        }

        private ContextMenuStrip BuildObjectDetectionGoodJudgementContextMenu(
            ObjectDetectionParameterSettings parameter)
        {
            objectDetectionGoodJudgementContextMenu = new ContextMenuStrip();
            var editItem = new ToolStripMenuItem("修改");
            var moveUpItem = new ToolStripMenuItem("上移");
            var moveDownItem = new ToolStripMenuItem("下移");
            var deleteItem = new ToolStripMenuItem("刪除");
            editItem.Click += delegate
            {
                LoadSelectedObjectDetectionGoodJudgementRule(parameter);
            };
            moveUpItem.Click += delegate
            {
                MoveObjectDetectionGoodJudgementRule(parameter, -1);
            };
            moveDownItem.Click += delegate
            {
                MoveObjectDetectionGoodJudgementRule(parameter, 1);
            };
            deleteItem.Click += delegate
            {
                DeleteSelectedObjectDetectionGoodJudgementRule(parameter);
            };
            objectDetectionGoodJudgementContextMenu.Items.Add(editItem);
            objectDetectionGoodJudgementContextMenu.Items.Add(moveUpItem);
            objectDetectionGoodJudgementContextMenu.Items.Add(moveDownItem);
            objectDetectionGoodJudgementContextMenu.Items.Add(deleteItem);
            return objectDetectionGoodJudgementContextMenu;
        }

        private void ObjectDetectionGoodJudgementRulesGrid_CellMouseDown(
            object sender,
            DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right ||
                objectDetectionGoodJudgementRulesGrid == null ||
                e.RowIndex < 0)
            {
                return;
            }

            objectDetectionGoodJudgementRulesGrid.ClearSelection();
            objectDetectionGoodJudgementRulesGrid.Rows[e.RowIndex].Selected = true;
            objectDetectionGoodJudgementRulesGrid.CurrentCell =
                objectDetectionGoodJudgementRulesGrid.Rows[e.RowIndex].Cells[0];
            UpdateObjectDetectionGoodJudgementMoveButtons();
        }

        private void RefreshObjectDetectionGoodJudgementRulesGrid(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionGoodJudgementRulesGrid == null)
            {
                return;
            }

            objectDetectionGoodJudgementRulesGrid.Rows.Clear();
            if (parameter == null || parameter.GoodJudgementRules == null)
            {
                return;
            }

            foreach (ObjectDetectionGoodJudgementRuleSettings rule in parameter.GoodJudgementRules)
            {
                if (rule == null)
                {
                    continue;
                }

                objectDetectionGoodJudgementRulesGrid.Rows.Add(
                    rule.Number,
                    rule.Name ?? string.Empty,
                    rule.Enabled,
                    rule.CalculationExpression ?? string.Empty,
                    rule.SpecificationExpression ?? string.Empty,
                    rule.AlternativeCalculationExpression ?? string.Empty,
                    rule.AlternativeSpecificationExpression ?? string.Empty);
            }
        }

        private void ApplyObjectDetectionGoodJudgementRule(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null)
            {
                return;
            }

            string name = objectDetectionGoodJudgementNameTextBox == null
                ? string.Empty
                : objectDetectionGoodJudgementNameTextBox.Text.Trim();
            string calculation = objectDetectionGoodJudgementCalculationTextBox == null
                ? string.Empty
                : objectDetectionGoodJudgementCalculationTextBox.Text.Trim();
            string specification = objectDetectionGoodJudgementSpecificationTextBox == null
                ? string.Empty
                : objectDetectionGoodJudgementSpecificationTextBox.Text.Trim();
            string alternativeCalculation = objectDetectionGoodJudgementAlternativeCalculationTextBox == null
                ? string.Empty
                : objectDetectionGoodJudgementAlternativeCalculationTextBox.Text.Trim();
            string alternativeSpecification = objectDetectionGoodJudgementAlternativeSpecificationTextBox == null
                ? string.Empty
                : objectDetectionGoodJudgementAlternativeSpecificationTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "判定條件" + GetNextObjectDetectionGoodJudgementRuleNumber(parameter).ToString(CultureInfo.InvariantCulture);
            }

            string validationMessage;
            if (!ValidateObjectDetectionGoodJudgementRule(
                    parameter,
                    calculation,
                    specification,
                    alternativeCalculation,
                    alternativeSpecification,
                    out validationMessage))
            {
                if (objectDetectionGoodJudgementStatusLabel != null)
                {
                    objectDetectionGoodJudgementStatusLabel.ForeColor = Color.Firebrick;
                    objectDetectionGoodJudgementStatusLabel.Text = validationMessage;
                }

                return;
            }

            if (parameter.GoodJudgementRules == null)
            {
                parameter.GoodJudgementRules = new List<ObjectDetectionGoodJudgementRuleSettings>();
            }

            ObjectDetectionGoodJudgementRuleSettings rule = null;
            if (!objectDetectionGoodJudgementCreateNewRule &&
                !string.IsNullOrWhiteSpace(objectDetectionGoodJudgementEditingRuleId))
            {
                rule = parameter.GoodJudgementRules.FirstOrDefault(item =>
                    item != null && string.Equals(
                        item.Id,
                        objectDetectionGoodJudgementEditingRuleId,
                        StringComparison.Ordinal));
            }

            if (rule == null)
            {
                rule = new ObjectDetectionGoodJudgementRuleSettings
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Number = GetNextObjectDetectionGoodJudgementRuleNumber(parameter)
                };
                parameter.GoodJudgementRules.Add(rule);
            }

            rule.Name = name;
            rule.Enabled = objectDetectionGoodJudgementEnabledCheckBox == null ||
                objectDetectionGoodJudgementEnabledCheckBox.Checked;
            rule.CalculationExpression = calculation;
            rule.SpecificationExpression = specification;
            rule.AlternativeCalculationExpression = alternativeCalculation;
            rule.AlternativeSpecificationExpression = alternativeSpecification;

            objectDetectionGoodJudgementEditingRuleId = rule.Id;
            objectDetectionGoodJudgementCreateNewRule = false;
            RefreshObjectDetectionGoodJudgementRulesGrid(parameter);
            UpdateObjectDetectionGoodJudgementEditorState(false);
            UpdateObjectDetectionGoodJudgementMoveButtons();
            SaveSystemParameters();
            if (objectDetectionGoodJudgementStatusLabel != null)
            {
                objectDetectionGoodJudgementStatusLabel.ForeColor = Color.ForestGreen;
                objectDetectionGoodJudgementStatusLabel.Text =
                    "已套用「" + rule.Name + "」，請按保存判定條件確認寫入參數檔";
            }
            statusLabel.Text = parameter.DisplayName + " 已套用尺寸良品判斷條件" + rule.Name;
        }

        private void ResetObjectDetectionGoodJudgementEditor(
            ObjectDetectionParameterSettings parameter)
        {
            objectDetectionGoodJudgementEditingRuleId = string.Empty;
            objectDetectionGoodJudgementCreateNewRule = true;
            if (objectDetectionGoodJudgementNameTextBox != null)
            {
                objectDetectionGoodJudgementNameTextBox.Clear();
            }
            if (objectDetectionGoodJudgementCalculationTextBox != null)
            {
                objectDetectionGoodJudgementCalculationTextBox.Clear();
            }
            if (objectDetectionGoodJudgementSpecificationTextBox != null)
            {
                objectDetectionGoodJudgementSpecificationTextBox.Clear();
            }
            if (objectDetectionGoodJudgementAlternativeCalculationTextBox != null)
            {
                objectDetectionGoodJudgementAlternativeCalculationTextBox.Clear();
            }
            if (objectDetectionGoodJudgementAlternativeSpecificationTextBox != null)
            {
                objectDetectionGoodJudgementAlternativeSpecificationTextBox.Clear();
            }
            if (objectDetectionGoodJudgementEnabledCheckBox != null)
            {
                objectDetectionGoodJudgementEnabledCheckBox.Checked = true;
            }
            UpdateObjectDetectionGoodJudgementEditorState(false);
            UpdateObjectDetectionGoodJudgementMoveButtons();
            if (objectDetectionGoodJudgementStatusLabel != null)
            {
                objectDetectionGoodJudgementStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                objectDetectionGoodJudgementStatusLabel.Text = "可新增新的尺寸良品判斷條件";
            }
        }

        private void LoadSelectedObjectDetectionGoodJudgementRule(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                parameter.GoodJudgementRules == null ||
                objectDetectionGoodJudgementRulesGrid == null ||
                objectDetectionGoodJudgementRulesGrid.SelectedRows.Count == 0)
            {
                return;
            }

            int rowIndex = objectDetectionGoodJudgementRulesGrid.SelectedRows[0].Index;
            if (rowIndex < 0 || rowIndex >= parameter.GoodJudgementRules.Count)
            {
                return;
            }

            ObjectDetectionGoodJudgementRuleSettings rule = parameter.GoodJudgementRules[rowIndex];
            if (rule == null)
            {
                return;
            }

            objectDetectionGoodJudgementEditingRuleId = rule.Id;
            objectDetectionGoodJudgementCreateNewRule = false;
            if (objectDetectionGoodJudgementNameTextBox != null)
            {
                objectDetectionGoodJudgementNameTextBox.Text = rule.Name ?? string.Empty;
            }
            if (objectDetectionGoodJudgementCalculationTextBox != null)
            {
                objectDetectionGoodJudgementCalculationTextBox.Text = rule.CalculationExpression ?? string.Empty;
            }
            if (objectDetectionGoodJudgementSpecificationTextBox != null)
            {
                objectDetectionGoodJudgementSpecificationTextBox.Text = rule.SpecificationExpression ?? string.Empty;
            }
            if (objectDetectionGoodJudgementAlternativeCalculationTextBox != null)
            {
                objectDetectionGoodJudgementAlternativeCalculationTextBox.Text =
                    rule.AlternativeCalculationExpression ?? string.Empty;
            }
            if (objectDetectionGoodJudgementAlternativeSpecificationTextBox != null)
            {
                objectDetectionGoodJudgementAlternativeSpecificationTextBox.Text =
                    rule.AlternativeSpecificationExpression ?? string.Empty;
            }
            if (objectDetectionGoodJudgementEnabledCheckBox != null)
            {
                objectDetectionGoodJudgementEnabledCheckBox.Checked = rule.Enabled;
            }
            UpdateObjectDetectionGoodJudgementEditorState(true);
            if (objectDetectionGoodJudgementStatusLabel != null)
            {
                objectDetectionGoodJudgementStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                objectDetectionGoodJudgementStatusLabel.Text = "正在修改「" + rule.Name + "」";
            }
        }

        private void DeleteSelectedObjectDetectionGoodJudgementRule(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                parameter.GoodJudgementRules == null ||
                objectDetectionGoodJudgementRulesGrid == null ||
                objectDetectionGoodJudgementRulesGrid.SelectedRows.Count == 0)
            {
                return;
            }

            int rowIndex = objectDetectionGoodJudgementRulesGrid.SelectedRows[0].Index;
            if (rowIndex < 0 || rowIndex >= parameter.GoodJudgementRules.Count)
            {
                return;
            }

            ObjectDetectionGoodJudgementRuleSettings rule = parameter.GoodJudgementRules[rowIndex];
            if (MessageBox.Show(
                    this,
                    "確定要刪除尺寸良品判斷條件「" + (rule == null ? string.Empty : rule.Name) + "」嗎？",
                    "刪除尺寸良品判斷條件",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            parameter.GoodJudgementRules.RemoveAt(rowIndex);
            ResetObjectDetectionGoodJudgementEditor(parameter);
            RefreshObjectDetectionGoodJudgementRulesGrid(parameter);
            SaveSystemParameters();
            statusLabel.Text = parameter.DisplayName + " 已刪除尺寸良品判斷條件";
        }

        private void MoveObjectDetectionGoodJudgementRule(
            ObjectDetectionParameterSettings parameter,
            int direction)
        {
            if (parameter == null ||
                parameter.GoodJudgementRules == null ||
                objectDetectionGoodJudgementRulesGrid == null ||
                objectDetectionGoodJudgementRulesGrid.SelectedRows.Count == 0)
            {
                return;
            }

            int currentIndex = objectDetectionGoodJudgementRulesGrid.SelectedRows[0].Index;
            int targetIndex = currentIndex + direction;
            if (currentIndex < 0 ||
                currentIndex >= parameter.GoodJudgementRules.Count ||
                targetIndex < 0 ||
                targetIndex >= parameter.GoodJudgementRules.Count)
            {
                return;
            }

            ObjectDetectionGoodJudgementRuleSettings moved = parameter.GoodJudgementRules[currentIndex];
            parameter.GoodJudgementRules[currentIndex] = parameter.GoodJudgementRules[targetIndex];
            parameter.GoodJudgementRules[targetIndex] = moved;
            RefreshObjectDetectionGoodJudgementRulesGrid(parameter);
            objectDetectionGoodJudgementRulesGrid.ClearSelection();
            objectDetectionGoodJudgementRulesGrid.Rows[targetIndex].Selected = true;
            objectDetectionGoodJudgementRulesGrid.CurrentCell =
                objectDetectionGoodJudgementRulesGrid.Rows[targetIndex].Cells[0];
            UpdateObjectDetectionGoodJudgementMoveButtons();
            SaveSystemParameters();
        }

        private void UpdateObjectDetectionGoodJudgementMoveButtons()
        {
            bool hasSelection = objectDetectionGoodJudgementRulesGrid != null &&
                objectDetectionGoodJudgementRulesGrid.SelectedRows.Count > 0;
            int selectedIndex = hasSelection
                ? objectDetectionGoodJudgementRulesGrid.SelectedRows[0].Index
                : -1;
            if (objectDetectionGoodJudgementMoveUpButton != null)
            {
                objectDetectionGoodJudgementMoveUpButton.Enabled = hasSelection && selectedIndex > 0;
            }
            if (objectDetectionGoodJudgementMoveDownButton != null)
            {
                objectDetectionGoodJudgementMoveDownButton.Enabled = hasSelection &&
                    selectedIndex >= 0 &&
                    parameterHasGoodJudgementRuleAfter(selectedIndex);
            }
        }

        private bool parameterHasGoodJudgementRuleAfter(int selectedIndex)
        {
            return objectDetectionGoodJudgementRulesGrid != null &&
                selectedIndex >= 0 &&
                selectedIndex < objectDetectionGoodJudgementRulesGrid.Rows.Count - 1;
        }

        private void UpdateObjectDetectionGoodJudgementEditorState(bool editing)
        {
            if (objectDetectionGoodJudgementApplyButton != null)
            {
                objectDetectionGoodJudgementApplyButton.Text = editing ? "更新" : "新增";
            }
        }

        private static bool ValidateObjectDetectionGoodJudgementRule(
            ObjectDetectionParameterSettings parameter,
            string calculation,
            string specification,
            string alternativeCalculation,
            string alternativeSpecification,
            out string message)
        {
            message = string.Empty;
            bool hasA = !string.IsNullOrWhiteSpace(calculation) ||
                !string.IsNullOrWhiteSpace(specification);
            bool hasB = !string.IsNullOrWhiteSpace(alternativeCalculation) ||
                !string.IsNullOrWhiteSpace(alternativeSpecification);
            if (!hasA && !hasB)
            {
                message = "請至少設定 A 規或 B 規的計算式與規格";
                return false;
            }

            if (hasA && (string.IsNullOrWhiteSpace(calculation) ||
                         string.IsNullOrWhiteSpace(specification)))
            {
                message = "A 規計算式與 A 規規格必須一起設定";
                return false;
            }

            if (hasB && (string.IsNullOrWhiteSpace(alternativeCalculation) ||
                         string.IsNullOrWhiteSpace(alternativeSpecification)))
            {
                message = "B 規計算式與 B 規規格必須一起設定";
                return false;
            }

            string expressionValidationMessage;
            if (!string.IsNullOrWhiteSpace(calculation) &&
                !ValidateObjectDetectionGoodJudgementExpression(
                    calculation,
                    parameter,
                    out expressionValidationMessage))
            {
                message = "A 規計算式格式不正確：" + expressionValidationMessage;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(alternativeCalculation) &&
                !ValidateObjectDetectionGoodJudgementExpression(
                    alternativeCalculation,
                    parameter,
                    out expressionValidationMessage))
            {
                message = "B 規計算式格式不正確：" + expressionValidationMessage;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(specification) &&
                !ValidateObjectDetectionGoodJudgementSpecification(specification))
            {
                message = "A 規規格格式不正確，例如 x<5 或 5<x<10";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(alternativeSpecification) &&
                !ValidateObjectDetectionGoodJudgementSpecification(alternativeSpecification))
            {
                message = "B 規規格格式不正確，例如 x<5 或 5<x<10";
                return false;
            }

            return true;
        }

        private static bool ValidateObjectDetectionGoodJudgementExpression(
            string expression,
            ObjectDetectionParameterSettings parameter,
            out string message)
        {
            message = "請使用量測編號、四則運算、舊版 min/max 語法，或平行量測的 MIN(n)、AVG(n)、MAX(n)";
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            string normalized = System.Text.RegularExpressions.Regex.Replace(
                expression,
                @"\s+",
                string.Empty);
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    normalized,
                    @"^[0-9()+*/.%\-,a-z]+$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                !AreObjectDetectionGoodJudgementFunctionNamesValid(normalized))
            {
                return false;
            }

            var statisticFunctionPattern = new System.Text.RegularExpressions.Regex(
                @"(?<![a-z0-9_])(?<function>min|avg|max)\((?<number>\d+)\)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var statisticFunctionMatches = statisticFunctionPattern.Matches(normalized);
            foreach (System.Text.RegularExpressions.Match match in statisticFunctionMatches)
            {
                int measurementNumber;
                if (!int.TryParse(
                    match.Groups["number"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out measurementNumber) || measurementNumber <= 0)
                {
                    message = "MIN/AVG/MAX 的括號內請填正確的量測編號，例如 AVG(1)";
                    return false;
                }

                ObjectDetectionMeasurementRecordSettings record =
                    parameter == null || parameter.MeasurementRecords == null
                        ? null
                        : parameter.MeasurementRecords.FirstOrDefault(item =>
                            item != null && item.Number == measurementNumber);
                if (record == null)
                {
                    message = "找不到量測編號 " +
                        measurementNumber.ToString(CultureInfo.InvariantCulture) +
                        "；請先建立並套用對應的量測紀錄";
                    return false;
                }

                if (!string.Equals(record.Mode, "Parallel", StringComparison.OrdinalIgnoreCase))
                {
                    message = "MIN/AVG/MAX 只適用於平行量測；量測編號 " +
                        measurementNumber.ToString(CultureInfo.InvariantCulture) +
                        " 是單線量測，請改用 (" +
                        measurementNumber.ToString(CultureInfo.InvariantCulture) + ")";
                    return false;
                }
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                @"(?<![a-z0-9_])(?:min|avg|max)\((?!\(|\d+\))",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                message = "統計函式格式為 MIN(n)、AVG(n) 或 MAX(n)，n 必須是量測編號；舊式 min/max 請使用 min((1)(2)) 格式";
                return false;
            }

            int depth = 0;
            foreach (char character in normalized)
            {
                if (character == '(')
                {
                    depth++;
                }
                else if (character == ')')
                {
                    depth--;
                    if (depth < 0)
                    {
                        return false;
                    }
                }
            }

            if (depth != 0 || normalized.IndexOf("()", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool AreObjectDetectionGoodJudgementFunctionNamesValid(string expression)
        {
            System.Text.RegularExpressions.MatchCollection functionMatches =
                System.Text.RegularExpressions.Regex.Matches(
                    expression,
                    @"[a-z]+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in functionMatches)
            {
                if (!string.Equals(match.Value, "min", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(match.Value, "avg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(match.Value, "max", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                int nextIndex = match.Index + match.Length;
                if (nextIndex >= expression.Length || expression[nextIndex] != '(' ||
                    (match.Index > 0 &&
                     (char.IsLetterOrDigit(expression[match.Index - 1]) ||
                      expression[match.Index - 1] == '_')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateObjectDetectionGoodJudgementSpecification(string specification)
        {
            string normalized = (specification ?? string.Empty).Replace(" ", string.Empty);
            return System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                @"^(?:x(?:<=|>=|<|>)-?\d+(?:\.\d+)?|-?\d+(?:\.\d+)?(?:<=|>=|<|>)x|-?\d+(?:\.\d+)?(?:<=|<)x(?:<=|<)-?\d+(?:\.\d+)?|-?\d+(?:>=|>)x(?:>=|>)-?\d+(?:\.\d+)?)$");
        }

    }
}
