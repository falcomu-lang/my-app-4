using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    internal sealed class DetectionRecipeMetadataDialog : Form
    {
        private readonly TextBox nameTextBox;
        private readonly TextBox descriptionTextBox;

        public DetectionRecipeMetadataDialog(string title, string displayName, string description)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(520, 360);
            Padding = new Padding(16);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text = "參數名稱",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            nameTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 120,
                Text = displayName ?? string.Empty
            };
            layout.Controls.Add(nameTextBox, 0, 1);
            layout.Controls.Add(new Label
            {
                Text = "參數說明",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);
            descriptionTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                MaxLength = 4000,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                Text = description ?? string.Empty
            };
            layout.Controls.Add(descriptionTextBox, 0, 3);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            var saveButton = new Button { Text = "保存", Width = 88, DialogResult = DialogResult.None };
            var cancelButton = new Button { Text = "取消", Width = 88, DialogResult = DialogResult.Cancel };
            saveButton.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    MessageBox.Show(this, "請輸入參數名稱。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    nameTextBox.Focus();
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(saveButton);
            layout.Controls.Add(buttons, 0, 4);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        public string DisplayName
        {
            get { return nameTextBox.Text.Trim(); }
        }

        public string Description
        {
            get { return descriptionTextBox.Text.Trim(); }
        }
    }

    internal sealed class DetectionRecipeSelectionDialog : Form
    {
        private readonly DetectionRecipeCatalogService catalogService;
        private readonly DataGridView recipeGrid;
        private readonly TextBox descriptionTextBox;
        private readonly Button editButton;
        private readonly Button useButton;

        public DetectionRecipeSelectionDialog(
            DetectionRecipeCatalogService catalogService,
            IList<DetectionRecipeCatalogEntry> entries)
        {
            this.catalogService = catalogService ?? throw new ArgumentNullException("catalogService");
            Text = "檢測參數選擇";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MinimumSize = new Size(680, 420);
            ClientSize = new Size(820, 540);
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                Text = "選取要載入的檢測參數",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            }, 0, 0);

            recipeGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                EditMode = DataGridViewEditMode.EditProgrammatically
            };
            recipeGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "參數名稱",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 55,
                MinimumWidth = 160
            });
            recipeGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Added",
                HeaderText = "加入時間",
                Width = 175
            });
            recipeGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "檔案狀態",
                Width = 100
            });
            recipeGrid.SelectionChanged += RecipeGrid_SelectionChanged;
            recipeGrid.CellDoubleClick += delegate { UseSelectedRecipe(); };
            layout.Controls.Add(recipeGrid, 0, 1);

            var footer = new Panel { Dock = DockStyle.Fill };
            layout.Controls.Add(footer, 0, 2);
            footer.Controls.Add(new Label
            {
                Text = "參數說明",
                Left = 0,
                Top = 4,
                Width = 100,
                Height = 22
            });
            descriptionTextBox = new TextBox
            {
                Left = 0,
                Top = 26,
                Width = ClientSize.Width - 28,
                Height = 50,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            footer.Controls.Add(descriptionTextBox);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0)
            };
            useButton = new Button { Text = "套用此參數", Width = 112, Enabled = false };
            var cancelButton = new Button { Text = "取消", Width = 88, DialogResult = DialogResult.Cancel };
            editButton = new Button { Text = "編輯名稱與說明", Width = 132, Enabled = false };
            useButton.Click += delegate { UseSelectedRecipe(); };
            editButton.Click += delegate { EditSelectedRecipe(); };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(useButton);
            buttons.Controls.Add(editButton);
            footer.Controls.Add(buttons);
            AcceptButton = useButton;
            CancelButton = cancelButton;

            Populate(entries);
        }

        public string SelectedRecipeId { get; private set; }

        private void Populate(IList<DetectionRecipeCatalogEntry> entries)
        {
            recipeGrid.Rows.Clear();
            foreach (DetectionRecipeCatalogEntry entry in entries ?? new List<DetectionRecipeCatalogEntry>())
            {
                bool exists = catalogService.ParameterFileExists(entry);
                DateTime added;
                string addedText = DateTime.TryParse(
                    entry.AddedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out added)
                    ? added.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
                    : string.Empty;
                int index = recipeGrid.Rows.Add(
                    entry.DisplayName,
                    addedText,
                    exists ? "可用" : "檔案缺失");
                recipeGrid.Rows[index].Tag = entry;
                if (!exists)
                {
                    recipeGrid.Rows[index].DefaultCellStyle.ForeColor = Color.Firebrick;
                }
            }

            if (recipeGrid.Rows.Count > 0)
            {
                recipeGrid.Rows[0].Selected = true;
                recipeGrid.CurrentCell = recipeGrid.Rows[0].Cells[0];
            }
            UpdateSelectionDetails();
        }

        private void RecipeGrid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectionDetails();
        }

        private DetectionRecipeCatalogEntry GetSelectedEntry()
        {
            return recipeGrid.SelectedRows.Count == 0
                ? null
                : recipeGrid.SelectedRows[0].Tag as DetectionRecipeCatalogEntry;
        }

        private void UpdateSelectionDetails()
        {
            DetectionRecipeCatalogEntry entry = GetSelectedEntry();
            descriptionTextBox.Text = entry == null ? string.Empty : entry.Description ?? string.Empty;
            bool selected = entry != null;
            editButton.Enabled = selected;
            useButton.Enabled = selected && catalogService.ParameterFileExists(entry);
        }

        private void EditSelectedRecipe()
        {
            DetectionRecipeCatalogEntry entry = GetSelectedEntry();
            if (entry == null) return;

            using (var editor = new DetectionRecipeMetadataDialog(
                "編輯參數名稱與說明",
                entry.DisplayName,
                entry.Description))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    catalogService.UpdateMetadata(entry.Id, editor.DisplayName, editor.Description);
                    Populate(catalogService.LoadParameterList());
                    SelectRecipeRow(entry.Id);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "無法保存說明", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SelectRecipeRow(string id)
        {
            foreach (DataGridViewRow row in recipeGrid.Rows)
            {
                DetectionRecipeCatalogEntry entry = row.Tag as DetectionRecipeCatalogEntry;
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    row.Selected = true;
                    recipeGrid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }

        private void UseSelectedRecipe()
        {
            DetectionRecipeCatalogEntry entry = GetSelectedEntry();
            if (entry == null || !catalogService.ParameterFileExists(entry))
            {
                MessageBox.Show(this, "所選參數檔不存在，請重新加入參數。", "參數檔缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdateSelectionDetails();
                return;
            }

            SelectedRecipeId = entry.Id;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
