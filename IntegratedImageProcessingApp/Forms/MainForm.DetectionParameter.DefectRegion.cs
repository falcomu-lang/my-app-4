using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ObjectDetectionRoiGroupChoice
        {
            public ObjectDetectionRoiGroupChoice(string id, string displayName)
            {
                Id = id ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
            }

            public string Id { get; private set; }

            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(DisplayName)
                    ? "未命名 ROI 組"
                    : DisplayName;
            }
        }

        private sealed class ObjectDetectionRoiGroupMemberChoice
        {
            public ObjectDetectionRoiGroupMemberChoice(int number, string text)
            {
                Number = number;
                Text = text;
            }

            public int Number { get; private set; }

            public string Text { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        [Flags]
        private enum ObjectDetectionDefectRegionEdges
        {
            None = 0,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8
        }

        private Button objectDetectionDefectRegionDrawButton;
        private Button objectDetectionDefectRegionApplyButton;
        private Button objectDetectionDefectRegionCancelButton;
        private Button objectDetectionDefectRegionClearButton;
        private Button objectDetectionDefectRunButton;
        private CheckBox objectDetectionDefectParallelExecutionCheckBox;
        private Label objectDetectionDefectRegionStatusLabel;
        private bool objectDetectionDefectRegionDrawMode;
        private bool objectDetectionDefectRegionIsDrawing;
        private bool objectDetectionDefectRegionIsResizing;
        private bool objectDetectionDefectRegionDraftConfigured;
        private ObjectDetectionDefectRegionEdges objectDetectionDefectRegionResizeEdges;
        private Point objectDetectionDefectRegionDrawStart;
        private Point objectDetectionDefectRegionDrawCurrent;
        private RectangleF objectDetectionDefectRegionDraft;
        private string objectDetectionDefectRegionDraftParameterId;
        private int objectDetectionDefectRegionDraftObjectNumber;
        private ComboBox objectDetectionDefectRoiGroupManagerComboBox;
        private CheckedListBox objectDetectionDefectRoiGroupMembersListBox;
        private Label objectDetectionDefectRoiGroupStatusLabel;
        private Button objectDetectionDefectRoiGroupRenameButton;
        private Button objectDetectionDefectRoiGroupDeleteButton;
        private Button objectDetectionDefectRoiGroupApplyButton;
        private Button objectDetectionDefectRoiGroupCancelButton;
        private Control objectDetectionDefectRoiGroupPanel;
        private bool isLoadingObjectDetectionDefectRoiGroupMembers;
        private bool isRefreshingObjectDetectionRoiGroupSelectors;

        private Control BuildObjectDetectionDefectRoiGroupPanel(
            ObjectDetectionParameterSettings parameter)
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 188,
                Text = "檢測範圍組",
                Padding = new Padding(8, 18, 8, 6)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            objectDetectionDefectRoiGroupManagerComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 2)
            };
            objectDetectionDefectRoiGroupManagerComboBox.SelectedIndexChanged += delegate
            {
                LoadObjectDetectionDefectRoiGroupMembers(parameter);
            };
            var groupActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var refreshButton = new Button
            {
                Text = "更新序號",
                Width = 78,
                Height = 26,
                Margin = new Padding(0, 1, 4, 0)
            };
            refreshButton.Click += delegate
            {
                LoadObjectDetectionDefectRoiGroupMembers(parameter);
            };
            var createButton = new Button
            {
                Text = "新增組",
                Width = 72,
                Height = 26,
                Margin = new Padding(0, 1, 4, 0)
            };
            objectDetectionDefectRoiGroupRenameButton = new Button
            {
                Text = "改名",
                Width = 62,
                Height = 26,
                Margin = new Padding(0, 1, 4, 0)
            };
            objectDetectionDefectRoiGroupDeleteButton = new Button
            {
                Text = "刪除組",
                Width = 72,
                Height = 26,
                Margin = new Padding(0, 1, 4, 0)
            };
            createButton.Click += delegate { CreateObjectDetectionDefectRoiGroup(parameter); };
            objectDetectionDefectRoiGroupRenameButton.Click +=
                delegate { RenameObjectDetectionDefectRoiGroup(parameter); };
            objectDetectionDefectRoiGroupDeleteButton.Click +=
                delegate { DeleteObjectDetectionDefectRoiGroup(parameter); };
            groupActions.Controls.Add(refreshButton);
            groupActions.Controls.Add(createButton);
            groupActions.Controls.Add(objectDetectionDefectRoiGroupRenameButton);
            groupActions.Controls.Add(objectDetectionDefectRoiGroupDeleteButton);

            objectDetectionDefectRoiGroupMembersListBox = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty
            };
            objectDetectionDefectRoiGroupMembersListBox.ItemCheck += delegate
            {
                if (!isLoadingObjectDetectionDefectRoiGroupMembers &&
                    objectDetectionDefectRoiGroupStatusLabel != null)
                {
                    objectDetectionDefectRoiGroupStatusLabel.Text =
                        "成員選擇尚未保存；按套用保存，或按取消還原。";
                }
            };
            objectDetectionDefectRoiGroupStatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                Margin = new Padding(0, 2, 4, 0)
            };
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            objectDetectionDefectRoiGroupApplyButton = new Button
            {
                Text = "套用",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 2, 2, 0)
            };
            objectDetectionDefectRoiGroupCancelButton = new Button
            {
                Text = "取消",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 2, 0, 0)
            };
            objectDetectionDefectRoiGroupApplyButton.Click +=
                delegate { ApplyObjectDetectionDefectRoiGroupMembers(parameter); };
            objectDetectionDefectRoiGroupCancelButton.Click +=
                delegate { LoadObjectDetectionDefectRoiGroupMembers(parameter); };
            footer.Controls.Add(objectDetectionDefectRoiGroupStatusLabel, 0, 0);
            footer.Controls.Add(objectDetectionDefectRoiGroupApplyButton, 1, 0);
            footer.Controls.Add(objectDetectionDefectRoiGroupCancelButton, 2, 0);

            layout.Controls.Add(objectDetectionDefectRoiGroupManagerComboBox, 0, 0);
            layout.Controls.Add(groupActions, 0, 1);
            layout.Controls.Add(objectDetectionDefectRoiGroupMembersListBox, 0, 2);
            layout.Controls.Add(footer, 0, 3);
            group.Controls.Add(layout);

            RefreshObjectDetectionDefectRoiGroupManager(parameter, null);
            objectDetectionDefectRoiGroupPanel = group;
            return group;
        }

        private void RefreshObjectDetectionDefectRoiGroupManager(
            ObjectDetectionParameterSettings parameter,
            string selectedGroupId)
        {
            if (objectDetectionDefectRoiGroupManagerComboBox == null ||
                objectDetectionDefectRoiGroupManagerComboBox.IsDisposed)
            {
                return;
            }

            isLoadingObjectDetectionDefectRoiGroupMembers = true;
            try
            {
                ComboBox comboBox = objectDetectionDefectRoiGroupManagerComboBox;
                comboBox.BeginUpdate();
                comboBox.Items.Clear();
                if (parameter != null && parameter.DefectInspectionRoiGroups != null)
                {
                    foreach (ObjectDetectionRoiGroupSettings roiGroup in
                        parameter.DefectInspectionRoiGroups)
                    {
                        if (roiGroup != null)
                        {
                            comboBox.Items.Add(new ObjectDetectionRoiGroupChoice(
                                roiGroup.Id,
                                roiGroup.DisplayName));
                        }
                    }
                }
                ObjectDetectionRoiGroupChoice selected = comboBox.Items
                    .Cast<ObjectDetectionRoiGroupChoice>()
                    .FirstOrDefault(item => string.Equals(
                        item.Id,
                        selectedGroupId,
                        StringComparison.Ordinal));
                if (selected == null && comboBox.Items.Count > 0)
                {
                    selected = comboBox.Items[0] as ObjectDetectionRoiGroupChoice;
                }
                comboBox.SelectedItem = selected;
                comboBox.EndUpdate();
            }
            finally
            {
                isLoadingObjectDetectionDefectRoiGroupMembers = false;
            }
            LoadObjectDetectionDefectRoiGroupMembers(parameter);
        }

        private void LoadObjectDetectionDefectRoiGroupMembers(
            ObjectDetectionParameterSettings parameter)
        {
            if (isLoadingObjectDetectionDefectRoiGroupMembers ||
                objectDetectionDefectRoiGroupMembersListBox == null ||
                objectDetectionDefectRoiGroupMembersListBox.IsDisposed)
            {
                return;
            }

            ObjectDetectionRoiGroupChoice choice =
                objectDetectionDefectRoiGroupManagerComboBox == null
                    ? null
                    : objectDetectionDefectRoiGroupManagerComboBox.SelectedItem
                        as ObjectDetectionRoiGroupChoice;
            ObjectDetectionRoiGroupSettings roiGroup =
                FindObjectDetectionRoiGroup(parameter, choice == null ? null : choice.Id);
            isLoadingObjectDetectionDefectRoiGroupMembers = true;
            try
            {
                CheckedListBox list = objectDetectionDefectRoiGroupMembersListBox;
                list.Items.Clear();
                HashSet<int> savedNumbers = roiGroup == null || roiGroup.RoiObjectNumbers == null
                    ? new HashSet<int>()
                    : new HashSet<int>(roiGroup.RoiObjectNumbers.Where(number => number > 0));
                List<ObjectDefinitionDetectedObject> objects =
                    GetCompletedObjectDetectionRoiGroupObjects(roiGroup);
                var availableNumbers = new HashSet<int>();
                foreach (ObjectDefinitionDetectedObject item in objects)
                {
                    availableNumbers.Add(item.Number);
                    int index = list.Items.Add(new ObjectDetectionRoiGroupMemberChoice(
                        item.Number,
                        "物件 " + item.Number.ToString(CultureInfo.CurrentCulture)));
                    list.SetItemChecked(index, savedNumbers.Contains(item.Number));
                }
                foreach (int missingNumber in savedNumbers.Where(number =>
                    !availableNumbers.Contains(number)).OrderBy(number => number))
                {
                    int index = list.Items.Add(new ObjectDetectionRoiGroupMemberChoice(
                        missingNumber,
                        "物件 " + missingNumber.ToString(CultureInfo.CurrentCulture) +
                        "（目前未找到）"));
                    list.SetItemChecked(index, true);
                }

                bool canEdit = roiGroup != null;
                list.Enabled = canEdit;
                if (objectDetectionDefectRoiGroupRenameButton != null)
                {
                    objectDetectionDefectRoiGroupRenameButton.Enabled = canEdit;
                }
                if (objectDetectionDefectRoiGroupDeleteButton != null)
                {
                    objectDetectionDefectRoiGroupDeleteButton.Enabled = canEdit;
                }
                if (objectDetectionDefectRoiGroupApplyButton != null)
                {
                    objectDetectionDefectRoiGroupApplyButton.Enabled = canEdit;
                }
                if (objectDetectionDefectRoiGroupCancelButton != null)
                {
                    objectDetectionDefectRoiGroupCancelButton.Enabled = canEdit;
                }
                if (objectDetectionDefectRoiGroupStatusLabel != null)
                {
                    objectDetectionDefectRoiGroupStatusLabel.Text = roiGroup == null
                        ? "先新增 ROI 組，再勾選物件序號。"
                        : objects.Count == 0
                            ? "尚無已完成的物件定義結果；既有成員仍保留。"
                            : "已勾選 " + savedNumbers.Count.ToString(CultureInfo.CurrentCulture) +
                                " 個物件；變更後按套用保存。";
                }
            }
            finally
            {
                isLoadingObjectDetectionDefectRoiGroupMembers = false;
            }
        }

        private List<ObjectDefinitionDetectedObject> GetCompletedObjectDetectionRoiGroupObjects(
            ObjectDetectionRoiGroupSettings roiGroup)
        {
            if (roiGroup == null || string.IsNullOrWhiteSpace(roiGroup.ObjectDefinitionId))
            {
                return new List<ObjectDefinitionDetectedObject>();
            }
            ObjectDefinitionSettings definition = FindObjectDefinition(roiGroup.ObjectDefinitionId);
            if (definition == null)
            {
                return new List<ObjectDefinitionDetectedObject>();
            }
            string signature = CreateObjectDefinitionProcessingSignature(definition);
            return HasCompletedObjectDefinitionResult(definition, signature)
                ? SnapshotCompletedObjectDefinitionObjects(definition, signature)
                : new List<ObjectDefinitionDetectedObject>();
        }

        private void CreateObjectDetectionDefectRoiGroup(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                MessageBox.Show(
                    this,
                    "請先在主參數設定選擇物件定義，ROI 組才能知道成員物件的來源。",
                    "新增檢測範圍組",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            string name = PromptForObjectDetectionRoiGroupName("新增檢測範圍組", string.Empty);
            if (name == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "請輸入 ROI 組名稱。", "名稱不可空白",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (parameter.DefectInspectionRoiGroups == null)
            {
                parameter.DefectInspectionRoiGroups = new List<ObjectDetectionRoiGroupSettings>();
            }
            if (parameter.DefectInspectionRoiGroups.Any(group => group != null &&
                string.Equals(group.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "此參數已存在相同名稱的 ROI 組。", "名稱重複",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var newGroup = new ObjectDetectionRoiGroupSettings
            {
                DisplayName = name,
                ObjectDefinitionId = parameter.ObjectDefinitionId
            };
            parameter.DefectInspectionRoiGroups.Add(newGroup);
            SaveSystemParameters();
            RefreshObjectDetectionDefectRoiGroupManager(parameter, newGroup.Id);
            RefreshObjectDetectionRoiGroupSelectors(parameter);
        }

        private void RenameObjectDetectionDefectRoiGroup(
            ObjectDetectionParameterSettings parameter)
        {
            ObjectDetectionRoiGroupSettings roiGroup = GetSelectedObjectDetectionRoiGroup(parameter);
            if (roiGroup == null)
            {
                return;
            }
            string name = PromptForObjectDetectionRoiGroupName(
                "修改檢測範圍組名稱",
                roiGroup.DisplayName);
            if (name == null || string.Equals(name, roiGroup.DisplayName, StringComparison.Ordinal))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "請輸入 ROI 組名稱。", "名稱不可空白",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (parameter.DefectInspectionRoiGroups.Any(group => group != null &&
                !string.Equals(group.Id, roiGroup.Id, StringComparison.Ordinal) &&
                string.Equals(group.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "此參數已存在相同名稱的 ROI 組。", "名稱重複",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            roiGroup.DisplayName = name;
            SaveSystemParameters();
            RefreshObjectDetectionDefectRoiGroupManager(parameter, roiGroup.Id);
            RefreshObjectDetectionRoiGroupSelectors(parameter);
        }

        private void DeleteObjectDetectionDefectRoiGroup(
            ObjectDetectionParameterSettings parameter)
        {
            ObjectDetectionRoiGroupSettings roiGroup = GetSelectedObjectDetectionRoiGroup(parameter);
            if (roiGroup == null)
            {
                return;
            }
            EnsureObjectDetectionDefectCores(parameter);
            ObjectDetectionFrequencySettings frequency2 =
                parameter.DefectFrequency2Settings ?? new ObjectDetectionFrequencySettings();
            bool referenced = parameter.DefectDetectionCores.Any(core => core != null &&
                    string.Equals(core.RoiGroupId, roiGroup.Id, StringComparison.Ordinal)) ||
                string.Equals(parameter.DefectFrequencyRoiGroupId, roiGroup.Id, StringComparison.Ordinal) ||
                string.Equals(frequency2.RoiGroupId, roiGroup.Id, StringComparison.Ordinal) ||
                (objectDetectionDefectCoreDrafts != null && objectDetectionDefectCoreDrafts.Any(core =>
                    core != null && string.Equals(core.RoiGroupId, roiGroup.Id, StringComparison.Ordinal)));
            if (referenced)
            {
                MessageBox.Show(
                    this,
                    "此 ROI 組仍被缺陷條件或頻域條件使用。請先將條件改為其他組，再刪除此組。",
                    "無法刪除檢測範圍組",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(
                    this,
                    "確定刪除 ROI 組「" + roiGroup.DisplayName + "」嗎？",
                    "刪除檢測範圍組",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            parameter.DefectInspectionRoiGroups.Remove(roiGroup);
            SaveSystemParameters();
            RefreshObjectDetectionDefectRoiGroupManager(parameter, null);
            RefreshObjectDetectionRoiGroupSelectors(parameter);
        }

        private void ApplyObjectDetectionDefectRoiGroupMembers(
            ObjectDetectionParameterSettings parameter)
        {
            ObjectDetectionRoiGroupSettings roiGroup = GetSelectedObjectDetectionRoiGroup(parameter);
            if (roiGroup == null || objectDetectionDefectRoiGroupMembersListBox == null)
            {
                return;
            }
            roiGroup.RoiObjectNumbers = objectDetectionDefectRoiGroupMembersListBox.CheckedItems
                .Cast<ObjectDetectionRoiGroupMemberChoice>()
                .Select(item => item.Number)
                .Where(number => number > 0)
                .Distinct()
                .OrderBy(number => number)
                .ToList();
            SaveSystemParameters();
            InvalidateObjectDetectionDefectRoiGroupResults(parameter, roiGroup.Id);
            RefreshObjectDetectionRoiGroupSelectors(parameter);
            LoadObjectDetectionDefectRoiGroupMembers(parameter);
            if (objectDetectionDefectRoiGroupStatusLabel != null)
            {
                objectDetectionDefectRoiGroupStatusLabel.Text = roiGroup.RoiObjectNumbers.Count == 0
                    ? "已保存空組；指定至少一個物件後才能執行。"
                    : "已保存 " + roiGroup.RoiObjectNumbers.Count.ToString(CultureInfo.CurrentCulture) +
                        " 個物件。";
            }
        }

        private void InvalidateObjectDetectionDefectRoiGroupResults(
            ObjectDetectionParameterSettings parameter,
            string roiGroupId)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(roiGroupId))
            {
                return;
            }
            EnsureObjectDetectionDefectCores(parameter);
            foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores)
            {
                if (core != null && string.Equals(core.RoiGroupId, roiGroupId, StringComparison.Ordinal))
                {
                    RemoveObjectDetectionDefectCoreResult(parameter.Id, core.CoreKey);
                }
            }
            if (string.Equals(parameter.DefectFrequencyRoiGroupId, roiGroupId, StringComparison.Ordinal))
            {
                RemoveObjectDetectionFrequencyResult(parameter.Id, 0);
            }
            if (parameter.DefectFrequency2Settings != null &&
                string.Equals(parameter.DefectFrequency2Settings.RoiGroupId, roiGroupId, StringComparison.Ordinal))
            {
                RemoveObjectDetectionFrequencyResult(parameter.Id, 1);
            }
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            InvalidateObjectDetectionDefectIntegrationDisplay();
            for (int index = 0; index < ObjectDetectionDefectDisplayNames.Length - 1; index++)
            {
                ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(index);
                if (display != null)
                {
                    display.InvalidateImageView();
                }
            }
        }

        private ObjectDetectionRoiGroupSettings GetSelectedObjectDetectionRoiGroup(
            ObjectDetectionParameterSettings parameter)
        {
            ObjectDetectionRoiGroupChoice choice =
                objectDetectionDefectRoiGroupManagerComboBox == null
                    ? null
                    : objectDetectionDefectRoiGroupManagerComboBox.SelectedItem
                        as ObjectDetectionRoiGroupChoice;
            return FindObjectDetectionRoiGroup(parameter, choice == null ? null : choice.Id);
        }

        private static ObjectDetectionRoiGroupSettings FindObjectDetectionRoiGroup(
            ObjectDetectionParameterSettings parameter,
            string roiGroupId)
        {
            return parameter == null || string.IsNullOrWhiteSpace(roiGroupId) ||
                parameter.DefectInspectionRoiGroups == null
                ? null
                : parameter.DefectInspectionRoiGroups.FirstOrDefault(group => group != null &&
                    string.Equals(group.Id, roiGroupId, StringComparison.Ordinal));
        }

        private string PromptForObjectDetectionRoiGroupName(string title, string initialValue)
        {
            using (var dialog = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(330, 112)
            })
            {
                var label = new Label
                {
                    AutoSize = false,
                    Text = "檢測範圍組名稱",
                    Location = new Point(12, 10),
                    Size = new Size(300, 20)
                };
                var textBox = new TextBox
                {
                    Location = new Point(12, 34),
                    Width = 306,
                    MaxLength = 64,
                    Text = initialValue ?? string.Empty
                };
                var ok = new Button
                {
                    Text = "確定",
                    DialogResult = DialogResult.OK,
                    Location = new Point(162, 72),
                    Size = new Size(72, 28)
                };
                var cancel = new Button
                {
                    Text = "取消",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(244, 72),
                    Size = new Size(72, 28)
                };
                dialog.Controls.Add(label);
                dialog.Controls.Add(textBox);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                textBox.SelectAll();
                return dialog.ShowDialog(this) == DialogResult.OK
                    ? textBox.Text.Trim()
                    : null;
            }
        }

        private void PopulateObjectDetectionRoiGroupSelector(
            ObjectDetectionParameterSettings parameter,
            ComboBox selector,
            string selectedGroupId)
        {
            if (selector == null || selector.IsDisposed)
            {
                return;
            }
            bool wasRefreshing = isRefreshingObjectDetectionRoiGroupSelectors;
            isRefreshingObjectDetectionRoiGroupSelectors = true;
            try
            {
                selector.BeginUpdate();
                selector.Items.Clear();
                selector.Items.Add(new ObjectDetectionRoiGroupChoice(string.Empty, "未指定 ROI 組"));
                if (parameter != null && parameter.DefectInspectionRoiGroups != null)
                {
                    foreach (ObjectDetectionRoiGroupSettings roiGroup in
                        parameter.DefectInspectionRoiGroups.Where(group => group != null &&
                            string.Equals(group.ObjectDefinitionId, parameter.ObjectDefinitionId,
                                StringComparison.Ordinal)))
                    {
                        selector.Items.Add(new ObjectDetectionRoiGroupChoice(
                            roiGroup.Id,
                            roiGroup.DisplayName));
                    }
                }
                ObjectDetectionRoiGroupChoice selected = selector.Items
                    .Cast<ObjectDetectionRoiGroupChoice>()
                    .FirstOrDefault(choice => string.Equals(
                        choice.Id,
                        selectedGroupId ?? string.Empty,
                        StringComparison.Ordinal));
                selector.SelectedItem = selected ?? selector.Items[0];
                selector.EndUpdate();
            }
            finally
            {
                isRefreshingObjectDetectionRoiGroupSelectors = wasRefreshing;
            }
        }

        private void RefreshObjectDetectionRoiGroupSelectors(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null)
            {
                return;
            }
            EnsureObjectDetectionDefectCores(parameter);
            EnsureObjectDetectionDefectCoreDrafts(parameter);
            if (objectDetectionDefectCoreRoiGroupSelectors != null)
            {
                for (int index = 0; index < objectDetectionDefectCoreRoiGroupSelectors.Length; index++)
                {
                    if (index < objectDetectionDefectCoreDrafts.Count)
                    {
                        PopulateObjectDetectionRoiGroupSelector(
                            parameter,
                            objectDetectionDefectCoreRoiGroupSelectors[index],
                            objectDetectionDefectCoreDrafts[index].RoiGroupId);
                    }
                }
            }
            foreach (ObjectDetectionFrequencyUiState state in objectDetectionFrequencyUiStates)
            {
                if (state == null || state.RoiGroupSelector == null || state.RoiGroupSelector.IsDisposed ||
                    !string.Equals(state.DraftParameterId, parameter.Id, StringComparison.Ordinal))
                {
                    continue;
                }
                ObjectDetectionRoiGroupChoice currentChoice =
                    state.RoiGroupSelector.SelectedItem as ObjectDetectionRoiGroupChoice;
                string groupId = currentChoice == null ? string.Empty : currentChoice.Id;
                PopulateObjectDetectionRoiGroupSelector(
                    parameter,
                    state.RoiGroupSelector,
                    groupId);
            }
        }

        private bool TryValidateObjectDetectionRoiGroupAssignments(
            ObjectDetectionParameterSettings parameter,
            string singleCoreKey,
            bool includeEnabledCores,
            bool includeFrequency1,
            bool includeFrequency2,
            out string error)
        {
            error = null;
            if (parameter == null)
            {
                error = "找不到所選檢測參數。";
                return false;
            }
            EnsureObjectDetectionDefectCores(parameter);
            var missing = new List<string>();
            IEnumerable<ObjectDetectionDefectCoreSettings> selectedCores =
                parameter.DefectDetectionCores.Take(ObjectDetectionDefectCoreKeys.Length);
            if (!string.IsNullOrWhiteSpace(singleCoreKey))
            {
                selectedCores = selectedCores.Where(core => string.Equals(
                    core.CoreKey,
                    singleCoreKey,
                    StringComparison.Ordinal));
            }
            else if (includeEnabledCores)
            {
                selectedCores = selectedCores.Where(core => core.Enabled);
            }
            else
            {
                selectedCores = Enumerable.Empty<ObjectDetectionDefectCoreSettings>();
            }
            List<ObjectDetectionDefectCoreSettings> coreList = selectedCores.ToList();
            foreach (ObjectDetectionDefectCoreSettings core in coreList)
            {
                int index = GetObjectDetectionDefectCoreIndex(core.CoreKey);
                if (!IsValidObjectDetectionRoiGroupAssignment(parameter, core.RoiGroupId))
                {
                    missing.Add(GetObjectDetectionDefectCoreLabel(index));
                }
            }
            if (includeFrequency1 && parameter.DefectFrequencyEnabled &&
                !IsValidObjectDetectionRoiGroupAssignment(parameter, parameter.DefectFrequencyRoiGroupId))
            {
                missing.Add("頻域異常一");
            }
            ObjectDetectionFrequencySettings frequency2 =
                parameter.DefectFrequency2Settings ?? new ObjectDetectionFrequencySettings();
            if (includeFrequency2 && frequency2.Enabled &&
                !IsValidObjectDetectionRoiGroupAssignment(parameter, frequency2.RoiGroupId))
            {
                missing.Add("頻域異常二");
            }
            if (missing.Count == 0)
            {
                ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                    ? null
                    : FindObjectDefinition(parameter.ObjectDefinitionId);
                if (definition != null)
                {
                    string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
                    if (HasCompletedObjectDefinitionResult(definition, definitionSignature))
                    {
                        Dictionary<string, HashSet<int>> resolvedMembers;
                        string memberError;
                        if (!TryValidateObjectDetectionDefectRoiGroupMembers(
                            parameter,
                            coreList,
                            includeFrequency1,
                            includeFrequency2,
                            SnapshotCompletedObjectDefinitionObjects(definition, definitionSignature),
                            out resolvedMembers,
                            out memberError))
                        {
                            error = memberError;
                            return false;
                        }
                    }
                }
                return true;
            }
            error = "以下啟用的檢測條件尚未指定有效 ROI 組，因此沒有開始運算：" +
                string.Join("、", missing) + "。請在各條件的「使用 ROI 組」選擇組別。";
            return false;
        }

        private static bool IsValidObjectDetectionRoiGroupAssignment(
            ObjectDetectionParameterSettings parameter,
            string roiGroupId)
        {
            ObjectDetectionRoiGroupSettings roiGroup =
                FindObjectDetectionRoiGroup(parameter, roiGroupId);
            return roiGroup != null &&
                string.Equals(roiGroup.ObjectDefinitionId, parameter.ObjectDefinitionId,
                    StringComparison.Ordinal) &&
                roiGroup.RoiObjectNumbers != null &&
                roiGroup.RoiObjectNumbers.Any(number => number > 0);
        }

        private bool TryResolveObjectDetectionRoiGroupMembers(
            ObjectDetectionParameterSettings parameter,
            string roiGroupId,
            IList<ObjectDefinitionDetectedObject> objects,
            out HashSet<int> memberNumbers,
            out string error)
        {
            memberNumbers = new HashSet<int>();
            error = null;
            ObjectDetectionRoiGroupSettings roiGroup = FindObjectDetectionRoiGroup(
                parameter,
                roiGroupId);
            if (!IsValidObjectDetectionRoiGroupAssignment(parameter, roiGroupId))
            {
                error = "缺少有效的 ROI 組指定，沒有開始運算。";
                return false;
            }
            if (!string.Equals(roiGroup.ObjectDefinitionId, parameter.ObjectDefinitionId,
                StringComparison.Ordinal))
            {
                error = "ROI 組「" + roiGroup.DisplayName + "」所屬的物件定義與目前參數不一致。";
                return false;
            }
            var availableNumbers = new HashSet<int>((objects ??
                new List<ObjectDefinitionDetectedObject>()).Where(item => item != null)
                .Select(item => item.Number));
            memberNumbers = new HashSet<int>(roiGroup.RoiObjectNumbers ?? new List<int>());
            int[] unknownNumbers = memberNumbers.Where(number => !availableNumbers.Contains(number)).ToArray();
            if (unknownNumbers.Length > 0)
            {
                error = "ROI 組「" + roiGroup.DisplayName + "」包含目前找不到的物件序號：" +
                    string.Join("、", unknownNumbers.OrderBy(number => number)) +
                    "。請重新整理該組成員後再執行。";
                return false;
            }
            if (memberNumbers.Count == 0)
            {
                error = "ROI 組「" + roiGroup.DisplayName + "」沒有指定物件，沒有開始運算。";
                return false;
            }
            return true;
        }

        private bool TryValidateObjectDetectionDefectRoiGroupMembers(
            ObjectDetectionParameterSettings parameter,
            IEnumerable<ObjectDetectionDefectCoreSettings> cores,
            bool includeFrequency1,
            bool includeFrequency2,
            IList<ObjectDefinitionDetectedObject> objects,
            out Dictionary<string, HashSet<int>> membersByGroupId,
            out string error)
        {
            membersByGroupId = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            error = null;
            var groups = new List<ObjectDetectionRoiGroupSettings>();
            foreach (ObjectDetectionDefectCoreSettings core in cores ??
                Enumerable.Empty<ObjectDetectionDefectCoreSettings>())
            {
                AddObjectDetectionRoiGroupToList(
                    groups,
                    FindObjectDetectionRoiGroup(parameter, core.RoiGroupId));
            }
            ObjectDetectionFrequencySettings frequency2 =
                parameter.DefectFrequency2Settings ?? new ObjectDetectionFrequencySettings();
            if (includeFrequency1 && parameter.DefectFrequencyEnabled)
            {
                AddObjectDetectionRoiGroupToList(
                    groups,
                    FindObjectDetectionRoiGroup(parameter, parameter.DefectFrequencyRoiGroupId));
            }
            if (includeFrequency2 && frequency2.Enabled)
            {
                AddObjectDetectionRoiGroupToList(
                    groups,
                    FindObjectDetectionRoiGroup(parameter, frequency2.RoiGroupId));
            }

            var availableNumbers = new HashSet<int>((objects ??
                new List<ObjectDefinitionDetectedObject>()).Where(item => item != null)
                .Select(item => item.Number));
            foreach (ObjectDetectionRoiGroupSettings roiGroup in groups)
            {
                if (!string.Equals(roiGroup.ObjectDefinitionId, parameter.ObjectDefinitionId,
                    StringComparison.Ordinal))
                {
                    error = "ROI 組「" + roiGroup.DisplayName + "」所屬的物件定義與目前參數不一致。";
                    return false;
                }
                var groupMembers = new HashSet<int>(roiGroup.RoiObjectNumbers ?? new List<int>());
                int[] unknownNumbers = groupMembers.Where(number => !availableNumbers.Contains(number)).ToArray();
                if (unknownNumbers.Length > 0)
                {
                    error = "ROI 組「" + roiGroup.DisplayName + "」包含目前找不到的物件序號：" +
                        string.Join("、", unknownNumbers.OrderBy(number => number)) +
                        "。請重新整理該組成員後再執行。";
                    return false;
                }
                if (groupMembers.Count == 0)
                {
                    error = "ROI 組「" + roiGroup.DisplayName + "」沒有指定物件，沒有開始運算。";
                    return false;
                }
                membersByGroupId[roiGroup.Id] = groupMembers;
            }
            return true;
        }

        private static void AddObjectDetectionRoiGroupToList(
            IList<ObjectDetectionRoiGroupSettings> groups,
            ObjectDetectionRoiGroupSettings roiGroup)
        {
            if (groups != null && roiGroup != null && !groups.Contains(roiGroup))
            {
                groups.Add(roiGroup);
            }
        }

        private static string CreateObjectDetectionRoiGroupSignature(
            ObjectDetectionParameterSettings parameter,
            string roiGroupId)
        {
            ObjectDetectionRoiGroupSettings roiGroup =
                FindObjectDetectionRoiGroup(parameter, roiGroupId);
            return roiGroup == null
                ? string.Empty
                : string.Join(":", new[]
                {
                    roiGroup.Id ?? string.Empty,
                    roiGroup.ObjectDefinitionId ?? string.Empty,
                    string.Join(",", (roiGroup.RoiObjectNumbers ?? new List<int>())
                        .Where(number => number > 0)
                        .Distinct()
                        .OrderBy(number => number)
                        .Select(number => number.ToString(CultureInfo.InvariantCulture)))
                });
        }

        private Control BuildObjectDetectionDefectInspectionRegionPanel(
            ObjectDetectionParameterSettings parameter)
        {
            if (!string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                ResetObjectDetectionDefectRegionDraft();
                objectDetectionDefectRegionDraftParameterId = parameter.Id;
            }

            var group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 158,
                Text = "檢測範圍",
                Padding = new Padding(8, 18, 8, 6)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var instruction = new Label
            {
                Dock = DockStyle.Fill,
                Text = "按住 Ctrl 拖曳框選；抓住框線或角落可微調範圍。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            objectDetectionDefectRegionDrawButton = new Button
            {
                AutoSize = false,
                Width = 104,
                Height = 27,
                Text = "框選範圍",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionApplyButton = new Button
            {
                AutoSize = false,
                Width = 62,
                Height = 27,
                Text = "套用",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionCancelButton = new Button
            {
                AutoSize = false,
                Width = 62,
                Height = 27,
                Text = "取消",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionClearButton = new Button
            {
                AutoSize = false,
                Width = 62,
                Height = 27,
                Text = "清除",
                UseVisualStyleBackColor = true
            };
            objectDetectionDefectRegionDrawButton.Click +=
                ObjectDetectionDefectRegionDrawButton_Click;
            objectDetectionDefectRegionApplyButton.Click +=
                ObjectDetectionDefectRegionApplyButton_Click;
            objectDetectionDefectRegionCancelButton.Click +=
                ObjectDetectionDefectRegionCancelButton_Click;
            objectDetectionDefectRegionClearButton.Click +=
                ObjectDetectionDefectRegionClearButton_Click;
            buttons.Controls.Add(objectDetectionDefectRegionDrawButton);
            buttons.Controls.Add(objectDetectionDefectRegionApplyButton);
            buttons.Controls.Add(objectDetectionDefectRegionCancelButton);
            buttons.Controls.Add(objectDetectionDefectRegionClearButton);

            objectDetectionDefectRegionStatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            layout.Controls.Add(instruction, 0, 0);
            layout.Controls.Add(buttons, 0, 1);
            layout.Controls.Add(objectDetectionDefectRegionStatusLabel, 0, 2);
            group.Controls.Add(layout);

            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            return group;
        }

        private Control BuildObjectDetectionDefectExecutionPanel(
            ObjectDetectionParameterSettings parameter)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(8, 4, 8, 2)
            };
            var executionOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                FlowDirection = FlowDirection.LeftToRight
            };
            objectDetectionDefectParallelExecutionCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "平行運算（物件 x 啟用項目）",
                Checked = parameter.DefectParallelExecutionEnabled,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 8, 0)
            };
            objectDetectionDefectParallelExecutionCheckBox.CheckedChanged += delegate
            {
                ObjectDetectionParameterSettings current = FindObjectDetectionParameter(parameter.Id);
                if (current == null)
                {
                    return;
                }
                current.DefectParallelExecutionEnabled =
                    objectDetectionDefectParallelExecutionCheckBox.Checked;
                SaveSystemParameters();
            };
            objectDetectionDefectRunButton = new Button
            {
                AutoSize = false,
                Width = 128,
                Height = 27,
                Text = "執行缺陷檢測",
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            objectDetectionDefectRunButton.Click += async delegate
            {
                ObjectDetectionParameterSettings current =
                    FindObjectDetectionParameter(parameter.Id);
                if (current != null)
                {
                    await RunObjectDetectionDefectProcessingAsync(current.Id, null);
                }
            };
            executionOptions.Controls.Add(objectDetectionDefectParallelExecutionCheckBox);
            executionOptions.Controls.Add(objectDetectionDefectRunButton);
            panel.Controls.Add(executionOptions);
            return panel;
        }

        private void ObjectDetectionDefectRegionDrawButton_Click(object sender, EventArgs e)
        {
            if (objectDetectionDefectRegionDrawMode)
            {
                objectDetectionDefectRegionDrawMode = false;
                objectDetectionDefectRegionIsDrawing = false;
                objectDetectionDefectRegionIsResizing = false;
                objectDetectionDefectRegionDrawStart = Point.Empty;
                if (objectDetectionDefectDisplayControl != null)
                {
                    objectDetectionDefectDisplayControl.ReleaseViewerPointer();
                }
                objectDetectionDefectRegionDrawButton.Text = "框選範圍";
                SetObjectDetectionDefectRegionStatus("已停止編輯；尚未套用的範圍仍可套用或取消。");
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            ObjectDetectionParameterSettings parameter = GetObjectDetectionParameterForDisplay();
            ObjectDefinitionDetectedObject selectedObject;
            if (parameter == null ||
                !TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                SetObjectDetectionDefectRegionStatus("請先選擇已找到的物件序號。");
                return;
            }

            if (IsImageDisplayUpdateSuppressed)
            {
                SetObjectDetectionDefectRegionStatus("目前勾選了不顯示畫面，請先取消勾選再框選。");
                return;
            }

            if (leftImageTabControl == null || objectDetectionDefectDisplayTabPage == null)
            {
                SetObjectDetectionDefectRegionStatus("缺陷顯示畫面尚未準備完成。");
                return;
            }

            leftImageTabControl.SelectedTab = objectDetectionDefectDisplayTabPage;
            RefreshObjectDetectionDefectDisplay();
            if (objectDetectionDefectDisplayControl == null ||
                !objectDetectionDefectDisplayControl.HasImage ||
                !string.Equals(
                    objectDetectionDefectDisplayParameterId,
                    parameter.Id,
                    StringComparison.Ordinal) ||
                objectDetectionDefectDisplayImageGeneration != imageSourceGeneration)
            {
                SetObjectDetectionDefectRegionStatus(
                    "平場校正後的缺陷顯示影像尚未就緒；請確認校正結果後再試。 ");
                return;
            }

            FocusObjectDetectionImage(selectedObject.Bounds);
            objectDetectionDefectRegionDrawMode = true;
            objectDetectionDefectRegionIsDrawing = false;
            objectDetectionDefectRegionIsResizing = false;
            objectDetectionDefectRegionDraftParameterId = parameter.Id;
            objectDetectionDefectRegionDraftObjectNumber = selectedObject.Number;
            objectDetectionDefectRegionDrawButton.Text = "停止框選";
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            SetObjectDetectionDefectRegionStatus(
                "物件" + selectedObject.Number.ToString(CultureInfo.InvariantCulture) +
                "：Ctrl 拖曳可框選；抓住現有框線可微調。放開後按「套用」保存。");
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectRegionApplyButton_Click(object sender, EventArgs e)
        {
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null || !objectDetectionDefectRegionDraftConfigured ||
                !string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                SetObjectDetectionDefectRegionStatus("請先完成框選，再套用檢測範圍。");
                return;
            }

            if (string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                SetObjectDetectionDefectRegionStatus("目前檢測參數尚未指定物件定義來源。");
                return;
            }

            parameter.DefectInspectionRegionConfigured = true;
            parameter.DefectInspectionRegionObjectDefinitionId = parameter.ObjectDefinitionId;
            parameter.DefectInspectionRegionReferenceObjectNumber =
                objectDetectionDefectRegionDraftObjectNumber;
            parameter.DefectInspectionRegionLeft = objectDetectionDefectRegionDraft.Left;
            parameter.DefectInspectionRegionTop = objectDetectionDefectRegionDraft.Top;
            parameter.DefectInspectionRegionRight = objectDetectionDefectRegionDraft.Right;
            parameter.DefectInspectionRegionBottom = objectDetectionDefectRegionDraft.Bottom;
            if (string.IsNullOrWhiteSpace(parameter.DefectInspectionRegionId))
            {
                parameter.DefectInspectionRegionId = Guid.NewGuid().ToString("N");
            }

            SaveSystemParameters();
            objectDetectionDefectRegionDrawMode = false;
            objectDetectionDefectRegionIsDrawing = false;
            objectDetectionDefectRegionIsResizing = false;
            objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
            objectDetectionDefectRegionDraftConfigured = false;
            objectDetectionDefectRegionDrawStart = Point.Empty;
            if (objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.ReleaseViewerPointer();
            }
            objectDetectionDefectRegionDrawButton.Text = "框選範圍";
            SetObjectDetectionDefectRegionStatus(
                "已套用：範例物件" +
                parameter.DefectInspectionRegionReferenceObjectNumber.ToString(CultureInfo.InvariantCulture) +
                "；此 ROI 相對範圍會套用至其他序號。");
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            InvalidateObjectDetectionDefectRegionDisplay();
            if (statusLabel != null)
            {
                statusLabel.Text = parameter.DisplayName + "：檢測範圍已套用並保存";
            }
        }

        private void ObjectDetectionDefectRegionCancelButton_Click(object sender, EventArgs e)
        {
            ResetObjectDetectionDefectRegionDraft();
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            SetObjectDetectionDefectRegionStatus("已取消尚未套用的檢測範圍。");
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectRegionClearButton_Click(object sender, EventArgs e)
        {
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null)
            {
                return;
            }

            if (parameter.DefectInspectionRegionConfigured &&
                MessageBox.Show(
                    this,
                    "確定清除已保存的檢測範圍嗎？",
                    "清除檢測範圍",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            ResetObjectDetectionDefectRegionDraft();
            parameter.DefectInspectionRegionId = Guid.NewGuid().ToString("N");
            parameter.DefectInspectionRegionObjectDefinitionId = string.Empty;
            parameter.DefectInspectionRegionReferenceObjectNumber = 0;
            parameter.DefectInspectionRegionConfigured = false;
            parameter.DefectInspectionRegionLeft = 0;
            parameter.DefectInspectionRegionTop = 0;
            parameter.DefectInspectionRegionRight = 1;
            parameter.DefectInspectionRegionBottom = 1;
            SaveSystemParameters();
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            SetObjectDetectionDefectRegionStatus("已清除檢測範圍。");
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectDisplayControl_ImageMouseDown(
            object sender,
            ImageMouseEventArgs e)
        {
            if (!objectDetectionDefectRegionDrawMode || e == null ||
                e.Button != MouseButtons.Left ||
                (e.Modifiers & Keys.Control) == 0 ||
                !e.IsInsideImage)
            {
                return;
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                SetObjectDetectionDefectRegionStatus("目前序號沒有可用的物件 ROI。");
                return;
            }

            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            PointF local = frame.ToNormalizedLocal(
                e.ImageLocation.X,
                e.ImageLocation.Y);
            e.Handled = true;
            if (!IsObjectDetectionDefectRegionPointInsideRoi(local))
            {
                SetObjectDetectionDefectRegionStatus("請從綠色 ROI 框內開始拖曳。");
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            RectangleF currentRegion;
            if (TryGetObjectDetectionDefectRegionForEditing(
                    parameter,
                    selectedObject,
                    out currentRegion))
            {
                ObjectDetectionDefectRegionEdges resizeEdges =
                    FindObjectDetectionDefectRegionResizeEdges(
                        local,
                        currentRegion,
                        frame,
                        objectDetectionDefectDisplayControl.ViewState.Zoom);
                if (resizeEdges != ObjectDetectionDefectRegionEdges.None)
                {
                    objectDetectionDefectRegionDraft = currentRegion;
                    objectDetectionDefectRegionDraftConfigured = true;
                    objectDetectionDefectRegionDraftParameterId = activeObjectDetectionParameterId;
                    objectDetectionDefectRegionDraftObjectNumber = selectedObject.Number;
                    objectDetectionDefectRegionResizeEdges = resizeEdges;
                    objectDetectionDefectRegionIsResizing = true;
                    objectDetectionDefectDisplayControl.CaptureViewerPointer();
                    objectDetectionDefectDisplayControl.SetViewerCursor(
                        GetObjectDetectionDefectRegionResizeCursor(resizeEdges));
                    SetObjectDetectionDefectRegionStatus(
                        "調整中：拖曳框線或角落，放開後按「套用」保存。");
                    InvalidateObjectDetectionDefectRegionDisplay();
                    return;
                }
            }

            objectDetectionDefectRegionDraftConfigured = false;
            objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
            objectDetectionDefectRegionIsDrawing = true;
            objectDetectionDefectRegionDrawStart = e.ImageLocation;
            objectDetectionDefectRegionDrawCurrent = e.ImageLocation;
            objectDetectionDefectDisplayControl.CaptureViewerPointer();
            objectDetectionDefectDisplayControl.SetViewerCursor(Cursors.Cross);
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectDisplayControl_ImageMouseMove(
            object sender,
            ImageMouseEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (objectDetectionDefectRegionIsResizing)
            {
                e.Handled = true;
                ObjectDefinitionDetectedObject selectedObject;
                if (TryGetSelectedObjectDetectionObject(out selectedObject))
                {
                    ObjectDetectionMeasurementFrame frame =
                        CreateObjectDetectionMeasurementFrame(selectedObject);
                    PointF local = frame.ToNormalizedLocal(
                        e.ImageLocation.X,
                        e.ImageLocation.Y);
                    UpdateObjectDetectionDefectRegionResize(local, frame);
                    e.Handled = true;
                    InvalidateObjectDetectionDefectRegionDisplay();
                }

                return;
            }

            if (objectDetectionDefectRegionIsDrawing)
            {
                objectDetectionDefectRegionDrawCurrent = e.ImageLocation;
                e.Handled = true;
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            UpdateObjectDetectionDefectRegionHoverCursor(e);
        }

        private void ObjectDetectionDefectDisplayControl_ImageMouseUp(
            object sender,
            ImageMouseEventArgs e)
        {
            if ((!objectDetectionDefectRegionIsDrawing &&
                !objectDetectionDefectRegionIsResizing) || e == null ||
                e.Button != MouseButtons.Left)
            {
                return;
            }

            e.Handled = true;
            objectDetectionDefectDisplayControl.ReleaseViewerPointer();
            if (objectDetectionDefectRegionIsResizing)
            {
                ObjectDefinitionDetectedObject resizedObject;
                if (TryGetSelectedObjectDetectionObject(out resizedObject))
                {
                    ObjectDetectionMeasurementFrame resizedFrame =
                        CreateObjectDetectionMeasurementFrame(resizedObject);
                    PointF local = resizedFrame.ToNormalizedLocal(
                        e.ImageLocation.X,
                        e.ImageLocation.Y);
                    UpdateObjectDetectionDefectRegionResize(local, resizedFrame);
                }

                objectDetectionDefectRegionIsResizing = false;
                objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
                SetObjectDetectionDefectRegionDraftStatus("範圍已調整");
                UpdateObjectDetectionDefectInspectionRegionControls(
                    FindObjectDetectionParameter(activeObjectDetectionParameterId));
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            objectDetectionDefectRegionDrawCurrent = e.ImageLocation;
            objectDetectionDefectRegionIsDrawing = false;
            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                objectDetectionDefectRegionIsResizing = false;
                objectDetectionDefectDisplayControl.ReleaseViewerPointer();
                SetObjectDetectionDefectRegionStatus("物件 ROI 已失效，請重新選擇序號。");
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            PointF start = frame.ToNormalizedLocal(
                objectDetectionDefectRegionDrawStart.X,
                objectDetectionDefectRegionDrawStart.Y);
            PointF end = frame.ToNormalizedLocal(
                objectDetectionDefectRegionDrawCurrent.X,
                objectDetectionDefectRegionDrawCurrent.Y);
            float left = ClampUnit(Math.Min(start.X, end.X));
            float top = ClampUnit(Math.Min(start.Y, end.Y));
            float right = ClampUnit(Math.Max(start.X, end.X));
            float bottom = ClampUnit(Math.Max(start.Y, end.Y));
            if ((right - left) * frame.Width < 1f ||
                (bottom - top) * frame.Height < 1f)
            {
                SetObjectDetectionDefectRegionStatus("框選範圍太小，請重新拖曳。");
                InvalidateObjectDetectionDefectRegionDisplay();
                return;
            }

            objectDetectionDefectRegionDraft = RectangleF.FromLTRB(
                left,
                top,
                right,
                bottom);
            objectDetectionDefectRegionDraftConfigured = true;
            objectDetectionDefectRegionDraftParameterId = activeObjectDetectionParameterId;
            objectDetectionDefectRegionDraftObjectNumber = selectedObject.Number;
            objectDetectionDefectRegionDrawButton.Text = "停止框選";
            SetObjectDetectionDefectRegionDraftStatus("框選完成");
            UpdateObjectDetectionDefectInspectionRegionControls(
                FindObjectDetectionParameter(activeObjectDetectionParameterId));
            InvalidateObjectDetectionDefectRegionDisplay();
        }

        private void ObjectDetectionDefectDisplayControl_ImageOverlayPaint(
            object sender,
            ImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            int displayIndex = GetObjectDetectionDefectDisplayIndex(display);
            if (e == null || !isObjectDetectionParameterImageLayout ||
                leftImageTabControl == null ||
                displayIndex < 0 ||
                leftImageTabControl.SelectedTab != GetObjectDetectionDefectDisplayTabPage(displayIndex))
            {
                return;
            }

            bool deferCoreDetailOverlay =
                (displayIndex < ObjectDetectionDefectCoreKeys.Length ||
                    displayIndex == ObjectDetectionDefectFrequencyDisplayIndex ||
                    displayIndex == ObjectDetectionDefectFrequency2DisplayIndex) &&
                display.IsPanOverlaySettling;
            if (!display.IsViewInteractionInProgress && !deferCoreDetailOverlay)
            {
                if (displayIndex == ObjectDetectionDefectIntegratedDisplayIndex)
                {
                    DrawObjectDetectionDefectCompositeCoreFrames(
                        e.Graphics,
                        e.Zoom,
                        e.Offset,
                        e.VisibleSourceRect);
                }
                else if (displayIndex == ObjectDetectionDefectFrequencyDisplayIndex ||
                    displayIndex == ObjectDetectionDefectFrequency2DisplayIndex)
                {
                    DrawObjectDetectionFrequencyAnomalies(
                        e.Graphics,
                        e.Zoom,
                        e.Offset,
                        e.VisibleSourceRect,
                        displayIndex == ObjectDetectionDefectFrequency2DisplayIndex ? 1 : 0);
                }
                else
                {
                    DrawObjectDetectionDefectCoreResult(
                        e.Graphics,
                        e.Zoom,
                        e.Offset,
                        e.VisibleSourceRect,
                        GetObjectDetectionDefectDisplayCoreKey(displayIndex));
                }
            }

            ObjectDefinitionDetectedObject selectedObject;
            if (!TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                return;
            }

            using (var roiPen = new Pen(Color.LimeGreen, 2f))
            {
                DrawObjectDetectionObjectOutline(
                    e.Graphics,
                    selectedObject,
                    roiPen,
                    e.Zoom,
                    e.Offset);
            }

            ObjectDetectionParameterSettings parameter =
                GetObjectDetectionParameterForDisplay();
            RectangleF normalizedRegion;
            bool draft = false;
            if (objectDetectionDefectRegionIsDrawing)
            {
                ObjectDetectionMeasurementFrame frame =
                    CreateObjectDetectionMeasurementFrame(selectedObject);
                PointF start = frame.ToNormalizedLocal(
                    objectDetectionDefectRegionDrawStart.X,
                    objectDetectionDefectRegionDrawStart.Y);
                PointF current = frame.ToNormalizedLocal(
                    objectDetectionDefectRegionDrawCurrent.X,
                    objectDetectionDefectRegionDrawCurrent.Y);
                normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit(Math.Min(start.X, current.X)),
                    ClampUnit(Math.Min(start.Y, current.Y)),
                    ClampUnit(Math.Max(start.X, current.X)),
                    ClampUnit(Math.Max(start.Y, current.Y)));
                draft = true;
            }
            else if (objectDetectionDefectRegionDraftConfigured &&
                string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    activeObjectDetectionParameterId,
                    StringComparison.Ordinal))
            {
                normalizedRegion = objectDetectionDefectRegionDraft;
                draft = true;
            }
            else if (parameter != null && parameter.DefectInspectionRegionConfigured &&
                string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal))
            {
                normalizedRegion = RectangleF.FromLTRB(
                    ClampUnit((float)parameter.DefectInspectionRegionLeft),
                    ClampUnit((float)parameter.DefectInspectionRegionTop),
                    ClampUnit((float)parameter.DefectInspectionRegionRight),
                    ClampUnit((float)parameter.DefectInspectionRegionBottom));
            }
            else
            {
                return;
            }

            if (normalizedRegion.Width <= 0 || normalizedRegion.Height <= 0)
            {
                return;
            }

            PointF[] imageCorners = CreateObjectDetectionDefectRegionImageCorners(
                selectedObject,
                normalizedRegion);
            PointF[] screenCorners = new PointF[imageCorners.Length];
            for (int index = 0; index < imageCorners.Length; index++)
            {
                screenCorners[index] = new PointF(
                    e.Offset.X + (imageCorners[index].X * e.Zoom),
                    e.Offset.Y + (imageCorners[index].Y * e.Zoom));
            }

            Color fillColor = draft
                ? Color.FromArgb(48, 255, 166, 0)
                : Color.FromArgb(46, 0, 150, 220);
            Color lineColor = draft ? Color.DarkOrange : Color.DeepSkyBlue;
            using (var fill = new SolidBrush(fillColor))
            using (var outline = new Pen(lineColor, 2f))
            {
                e.Graphics.FillPolygon(fill, screenCorners);
                e.Graphics.DrawPolygon(outline, screenCorners);
            }
        }

        private static PointF[] CreateObjectDetectionDefectRegionImageCorners(
            ObjectDefinitionDetectedObject selectedObject,
            RectangleF normalizedRegion)
        {
            ObjectDetectionMeasurementFrame frame =
                CreateObjectDetectionMeasurementFrame(selectedObject);
            return new[]
            {
                frame.ToImage((double)normalizedRegion.Left, (double)normalizedRegion.Top),
                frame.ToImage((double)normalizedRegion.Right, (double)normalizedRegion.Top),
                frame.ToImage((double)normalizedRegion.Right, (double)normalizedRegion.Bottom),
                frame.ToImage((double)normalizedRegion.Left, (double)normalizedRegion.Bottom)
            };
        }

        private bool TryGetObjectDetectionDefectRegionForEditing(
            ObjectDetectionParameterSettings parameter,
            ObjectDefinitionDetectedObject selectedObject,
            out RectangleF region)
        {
            region = RectangleF.Empty;
            if (parameter == null || selectedObject == null)
            {
                return false;
            }

            if (objectDetectionDefectRegionDraftConfigured &&
                string.Equals(
                    objectDetectionDefectRegionDraftParameterId,
                    parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionDefectRegionDraftObjectNumber == selectedObject.Number)
            {
                region = objectDetectionDefectRegionDraft;
                return region.Width > 0 && region.Height > 0;
            }

            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            region = RectangleF.FromLTRB(
                ClampUnit((float)parameter.DefectInspectionRegionLeft),
                ClampUnit((float)parameter.DefectInspectionRegionTop),
                ClampUnit((float)parameter.DefectInspectionRegionRight),
                ClampUnit((float)parameter.DefectInspectionRegionBottom));
            return region.Width > 0 && region.Height > 0;
        }

        private static ObjectDetectionDefectRegionEdges FindObjectDetectionDefectRegionResizeEdges(
            PointF normalizedPoint,
            RectangleF region,
            ObjectDetectionMeasurementFrame frame,
            float zoom)
        {
            const float hitTolerancePixels = 8f;
            if (zoom <= 0)
            {
                return ObjectDetectionDefectRegionEdges.None;
            }

            float toleranceX = hitTolerancePixels / Math.Max(1f, frame.Width * zoom);
            float toleranceY = hitTolerancePixels / Math.Max(1f, frame.Height * zoom);
            bool withinVerticalEdge = normalizedPoint.Y >= region.Top - toleranceY &&
                normalizedPoint.Y <= region.Bottom + toleranceY;
            bool withinHorizontalEdge = normalizedPoint.X >= region.Left - toleranceX &&
                normalizedPoint.X <= region.Right + toleranceX;
            bool nearLeft = withinVerticalEdge &&
                Math.Abs(normalizedPoint.X - region.Left) <= toleranceX;
            bool nearRight = withinVerticalEdge &&
                Math.Abs(normalizedPoint.X - region.Right) <= toleranceX;
            bool nearTop = withinHorizontalEdge &&
                Math.Abs(normalizedPoint.Y - region.Top) <= toleranceY;
            bool nearBottom = withinHorizontalEdge &&
                Math.Abs(normalizedPoint.Y - region.Bottom) <= toleranceY;

            if (nearLeft && nearRight)
            {
                if (Math.Abs(normalizedPoint.X - region.Left) <=
                    Math.Abs(normalizedPoint.X - region.Right))
                {
                    nearRight = false;
                }
                else
                {
                    nearLeft = false;
                }
            }

            if (nearTop && nearBottom)
            {
                if (Math.Abs(normalizedPoint.Y - region.Top) <=
                    Math.Abs(normalizedPoint.Y - region.Bottom))
                {
                    nearBottom = false;
                }
                else
                {
                    nearTop = false;
                }
            }

            ObjectDetectionDefectRegionEdges edges = ObjectDetectionDefectRegionEdges.None;
            if (nearLeft)
            {
                edges |= ObjectDetectionDefectRegionEdges.Left;
            }

            if (nearTop)
            {
                edges |= ObjectDetectionDefectRegionEdges.Top;
            }

            if (nearRight)
            {
                edges |= ObjectDetectionDefectRegionEdges.Right;
            }

            if (nearBottom)
            {
                edges |= ObjectDetectionDefectRegionEdges.Bottom;
            }

            return edges;
        }

        private void UpdateObjectDetectionDefectRegionResize(
            PointF normalizedPoint,
            ObjectDetectionMeasurementFrame frame)
        {
            RectangleF region = objectDetectionDefectRegionDraft;
            float minimumWidth = 1f / Math.Max(1f, frame.Width);
            float minimumHeight = 1f / Math.Max(1f, frame.Height);
            float x = ClampUnit(normalizedPoint.X);
            float y = ClampUnit(normalizedPoint.Y);

            if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Left) != 0)
            {
                float fixedRight = region.Right;
                region.X = Math.Min(x, fixedRight - minimumWidth);
                region.Width = fixedRight - region.Left;
            }
            else if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Right) != 0)
            {
                region.Width = Math.Max(x, region.Left + minimumWidth) - region.Left;
            }

            if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Top) != 0)
            {
                float fixedBottom = region.Bottom;
                region.Y = Math.Min(y, fixedBottom - minimumHeight);
                region.Height = fixedBottom - region.Top;
            }
            else if ((objectDetectionDefectRegionResizeEdges & ObjectDetectionDefectRegionEdges.Bottom) != 0)
            {
                region.Height = Math.Max(y, region.Top + minimumHeight) - region.Top;
            }

            objectDetectionDefectRegionDraft = region;
            objectDetectionDefectRegionDraftConfigured = true;
        }

        private void UpdateObjectDetectionDefectRegionHoverCursor(ImageMouseEventArgs e)
        {
            if (objectDetectionDefectDisplayControl == null)
            {
                return;
            }

            ObjectDetectionDefectRegionEdges edges = ObjectDetectionDefectRegionEdges.None;
            ObjectDefinitionDetectedObject selectedObject;
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (objectDetectionDefectRegionDrawMode && e.IsInsideImage &&
                (e.Modifiers & Keys.Control) != 0 &&
                TryGetSelectedObjectDetectionObject(out selectedObject))
            {
                RectangleF region;
                if (TryGetObjectDetectionDefectRegionForEditing(
                        parameter,
                        selectedObject,
                        out region))
                {
                    ObjectDetectionMeasurementFrame frame =
                        CreateObjectDetectionMeasurementFrame(selectedObject);
                    PointF local = frame.ToNormalizedLocal(
                        e.ImageLocation.X,
                        e.ImageLocation.Y);
                    edges = FindObjectDetectionDefectRegionResizeEdges(
                        local,
                        region,
                        frame,
                        objectDetectionDefectDisplayControl.ViewState.Zoom);
                }
            }

            objectDetectionDefectDisplayControl.SetViewerCursor(
                edges == ObjectDetectionDefectRegionEdges.None
                    ? Cursors.Default
                    : GetObjectDetectionDefectRegionResizeCursor(edges));
        }

        private void ObjectDetectionDefectDisplayControl_ImagePointerMoved(
            object sender,
            ImagePointerMovedEventArgs e)
        {
            if (e != null && !e.IsInsideViewer &&
                !objectDetectionDefectRegionIsDrawing &&
                !objectDetectionDefectRegionIsResizing &&
                objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.SetViewerCursor(Cursors.Default);
            }
        }

        private static Cursor GetObjectDetectionDefectRegionResizeCursor(
            ObjectDetectionDefectRegionEdges edges)
        {
            bool horizontal =
                (edges & (ObjectDetectionDefectRegionEdges.Left | ObjectDetectionDefectRegionEdges.Right)) != 0;
            bool vertical =
                (edges & (ObjectDetectionDefectRegionEdges.Top | ObjectDetectionDefectRegionEdges.Bottom)) != 0;
            if (horizontal && vertical)
            {
                bool sameDiagonal =
                    ((edges & ObjectDetectionDefectRegionEdges.Left) != 0) ==
                    ((edges & ObjectDetectionDefectRegionEdges.Top) != 0);
                return sameDiagonal ? Cursors.SizeNWSE : Cursors.SizeNESW;
            }

            return horizontal ? Cursors.SizeWE : Cursors.SizeNS;
        }

        private void SetObjectDetectionDefectRegionDraftStatus(string prefix)
        {
            RectangleF region = objectDetectionDefectRegionDraft;
            SetObjectDetectionDefectRegionStatus(
                prefix + "：X " + (region.Left * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%，Y " + (region.Top * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%，寬 " + (region.Width * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%，高 " + (region.Height * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                "%；按「套用」保存。");
        }

        private static bool IsObjectDetectionDefectRegionPointInsideRoi(PointF normalizedPoint)
        {
            const float tolerance = 0.0001f;
            return normalizedPoint.X >= -tolerance && normalizedPoint.X <= 1f + tolerance &&
                normalizedPoint.Y >= -tolerance && normalizedPoint.Y <= 1f + tolerance;
        }

        private static float ClampUnit(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private void UpdateObjectDetectionDefectInspectionRegionControls(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionDefectRegionStatusLabel == null ||
                objectDetectionDefectRegionStatusLabel.IsDisposed)
            {
                return;
            }

            bool hasSelectedObject = TryGetSelectedObjectDetectionObject(
                out ObjectDefinitionDetectedObject selectedObject);
            bool sourceMatches = parameter != null &&
                string.Equals(
                    parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId,
                    StringComparison.Ordinal);
            if (objectDetectionDefectRegionDrawButton != null)
            {
                objectDetectionDefectRegionDrawButton.Enabled = parameter != null && hasSelectedObject;
                objectDetectionDefectRegionDrawButton.Text = objectDetectionDefectRegionDrawMode
                    ? "停止框選"
                    : "框選範圍";
            }

            if (objectDetectionDefectRegionApplyButton != null)
            {
                objectDetectionDefectRegionApplyButton.Enabled =
                    parameter != null && objectDetectionDefectRegionDraftConfigured;
            }

            if (objectDetectionDefectRegionCancelButton != null)
            {
                objectDetectionDefectRegionCancelButton.Enabled =
                    objectDetectionDefectRegionDrawMode ||
                    objectDetectionDefectRegionIsDrawing ||
                    objectDetectionDefectRegionDraftConfigured;
            }

            if (objectDetectionDefectRegionClearButton != null)
            {
                objectDetectionDefectRegionClearButton.Enabled = parameter != null &&
                    (parameter.DefectInspectionRegionConfigured ||
                    objectDetectionDefectRegionDraftConfigured);
            }

            if (objectDetectionDefectParallelExecutionCheckBox != null)
            {
                objectDetectionDefectParallelExecutionCheckBox.Enabled =
                    parameter != null && !objectDetectionDefectProcessingRequested;
            }

            if (objectDetectionDefectRunButton != null)
            {
                objectDetectionDefectRunButton.Enabled = parameter != null &&
                    parameter.DefectInspectionRegionConfigured && sourceMatches &&
                    !objectDetectionDefectProcessingRequested;
            }

            if (objectDetectionDefectRegionDraftConfigured)
            {
                RectangleF region = objectDetectionDefectRegionDraft;
                objectDetectionDefectRegionStatusLabel.Text =
                    "草稿範例物件 " + objectDetectionDefectRegionDraftObjectNumber.ToString(CultureInfo.InvariantCulture) +
                    "；X " + (region.Left * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，Y " + (region.Top * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，寬 " + (region.Width * 100f).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，高 " + (region.Height * 100f).ToString("0.0", CultureInfo.CurrentCulture) + "%。";
            }
            else if (parameter != null && parameter.DefectInspectionRegionConfigured && !sourceMatches)
            {
                objectDetectionDefectRegionStatusLabel.Text =
                    "物件定義來源已變更，請重新框選並套用範圍。";
            }
            else if (parameter != null && parameter.DefectInspectionRegionConfigured)
            {
                objectDetectionDefectRegionStatusLabel.Text =
                    "已套用至所有序號；範例物件 " +
                    parameter.DefectInspectionRegionReferenceObjectNumber.ToString(CultureInfo.InvariantCulture) +
                    "，X " + (parameter.DefectInspectionRegionLeft * 100.0).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，Y " + (parameter.DefectInspectionRegionTop * 100.0).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，寬 " + ((parameter.DefectInspectionRegionRight - parameter.DefectInspectionRegionLeft) * 100.0).ToString("0.0", CultureInfo.CurrentCulture) +
                    "%，高 " + ((parameter.DefectInspectionRegionBottom - parameter.DefectInspectionRegionTop) * 100.0).ToString("0.0", CultureInfo.CurrentCulture) + "%。";
            }
            else if (!hasSelectedObject)
            {
                objectDetectionDefectRegionStatusLabel.Text = "請先選擇已找到的物件序號。";
            }
            else
            {
                objectDetectionDefectRegionStatusLabel.Text = "尚未設定檢測範圍。";
            }
        }

        private void CancelObjectDetectionDefectInspectionRegionDraftForObjectChange(
            int newObjectNumber)
        {
            if ((objectDetectionDefectRegionDrawMode || objectDetectionDefectRegionIsDrawing ||
                objectDetectionDefectRegionDraftConfigured) &&
                objectDetectionDefectRegionDraftObjectNumber > 0 &&
                objectDetectionDefectRegionDraftObjectNumber != newObjectNumber)
            {
                ResetObjectDetectionDefectRegionDraft();
                SetObjectDetectionDefectRegionStatus("序號已變更，未套用的框選草稿已取消。");
                InvalidateObjectDetectionDefectRegionDisplay();
            }
        }

        private void ResetObjectDetectionDefectRegionDraft()
        {
            if (objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.ReleaseViewerPointer();
            }

            objectDetectionDefectRegionDrawMode = false;
            objectDetectionDefectRegionIsDrawing = false;
            objectDetectionDefectRegionIsResizing = false;
            objectDetectionDefectRegionDraftConfigured = false;
            objectDetectionDefectRegionResizeEdges = ObjectDetectionDefectRegionEdges.None;
            objectDetectionDefectRegionDrawStart = Point.Empty;
            objectDetectionDefectRegionDrawCurrent = Point.Empty;
            objectDetectionDefectRegionDraft = RectangleF.Empty;
            objectDetectionDefectRegionDraftObjectNumber = 0;
            if (objectDetectionDefectRegionDrawButton != null &&
                !objectDetectionDefectRegionDrawButton.IsDisposed)
            {
                objectDetectionDefectRegionDrawButton.Text = "框選範圍";
            }
        }

        private void SetObjectDetectionDefectRegionStatus(string text)
        {
            if (objectDetectionDefectRegionStatusLabel != null &&
                !objectDetectionDefectRegionStatusLabel.IsDisposed)
            {
                objectDetectionDefectRegionStatusLabel.Text = text ?? string.Empty;
            }
        }

        private void InvalidateObjectDetectionDefectRegionDisplay()
        {
            if (objectDetectionDefectDisplayControl != null &&
                !objectDetectionDefectDisplayControl.IsDisposed)
            {
                objectDetectionDefectDisplayControl.InvalidateImageView();
            }
        }
    }
}
