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
        private bool objectJudgementMenuExpanded;
        private bool isRebuildingObjectJudgementMenu;
        private readonly HashSet<string> expandedObjectJudgementIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> expandedObjectJudgementGroupIds =
            new HashSet<string>(StringComparer.Ordinal);
        private Panel objectJudgementParameterPanel;
        private ImageDisplayControl leftBlockProcessingDisplayControl;
        private ImageDisplayControl rightBlockProcessingDisplayControl;

        private void BlockProcessingDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            // During panning, draw only the base image. The block-result overlay
            // is intentionally restored by the normal repaint after MouseUp so
            // repeated viewport movement does not keep compositing red pixels.
            ImageDisplayControl display = sender as ImageDisplayControl;
            if (display != null && display.IsPanning)
            {
                return;
            }

            string selectedFunction = functionListBox.SelectedItem as string;
            if (GetSelectedObjectJudgementIndex() < 0 &&
                string.IsNullOrEmpty(GetObjectJudgementGroupId(selectedFunction)) &&
                string.IsNullOrEmpty(activeObjectJudgementId) &&
                string.IsNullOrEmpty(activeObjectJudgementGroupId))
            {
                return;
            }

            PaintLargeObjectJudgementOverlay(sender, e);
        }

        private int GetSelectedObjectJudgementIndex()
        {
            string selectedFunction = functionListBox.SelectedItem as string;
            int objectIndex;
            int processingIndex;
            if (TryGetObjectJudgementProcessingLocation(
                    selectedFunction,
                    out objectIndex,
                    out processingIndex))
            {
                return objectIndex;
            }

            return GetObjectJudgementIndex(selectedFunction);
        }

        private void InvalidateBlockProcessingDisplays()
        {
            if (SkipImageDisplayUpdate())
            {
                return;
            }

            if (leftBlockProcessingDisplayControl != null)
            {
                leftBlockProcessingDisplayControl.InvalidateImageView();
            }

            if (rightBlockProcessingDisplayControl != null)
            {
                rightBlockProcessingDisplayControl.InvalidateImageView();
            }
        }

        private void AddBlockProcessingImageTabs()
        {
            if (leftBlockProcessingTabPage != null && rightBlockProcessingTabPage != null)
            {
                return;
            }

            leftBlockProcessingTabPage = CreateImageTabPage(
                "leftBlockProcessingTabPage",
                "區塊處理",
                out leftBlockProcessingDisplayHostPanel);
            rightBlockProcessingTabPage = CreateImageTabPage(
                "rightBlockProcessingTabPage",
                "區塊處理",
                out rightBlockProcessingDisplayHostPanel);
            InsertBlockProcessingTab(leftImageTabControl, leftObjectsTabPage, leftBlockProcessingTabPage);
            InsertBlockProcessingTab(rightImageTabControl, rightObjectsTabPage, rightBlockProcessingTabPage);
        }

        private static void InsertBlockProcessingTab(TabControl tabControl, TabPage objectResultTabPage, TabPage blockProcessingTabPage)
        {
            if (tabControl == null || blockProcessingTabPage == null || tabControl.TabPages.Contains(blockProcessingTabPage))
            {
                return;
            }

            int objectResultIndex = objectResultTabPage == null
                ? -1
                : tabControl.TabPages.IndexOf(objectResultTabPage);
            if (objectResultIndex < 0)
            {
                tabControl.TabPages.Add(blockProcessingTabPage);
            }
            else
            {
                tabControl.TabPages.Insert(objectResultIndex, blockProcessingTabPage);
            }
        }

        private static string CreateObjectJudgementText(int objectNumber)
        {
            return "    區塊" + objectNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CreateObjectJudgementText(int objectNumber, int depth)
        {
            return new string(' ', 4 + (depth * 2)) + "區塊" +
                objectNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CreateObjectJudgementProcessingText(int processingNumber)
        {
            return "        處理" + processingNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CreateObjectJudgementProcessingText(int processingNumber, int depth)
        {
            return new string(' ', 6 + (depth * 2)) + "處理" +
                processingNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CreateObjectJudgementGroupText(
            ObjectJudgementGroupSettings group,
            int depth)
        {
            string displayName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? "物件群組"
                : group.DisplayName.Trim();
            return new string(' ', 4 + (depth * 2)) + "物件群組(" + displayName + ")";
        }

        private ObjectJudgementGroupSettings FindObjectJudgementGroup(string groupId)
        {
            return systemParameters.ObjectJudgementGroups.Find(
                group => string.Equals(group.Id, groupId, StringComparison.Ordinal));
        }

        private string GetObjectJudgementGroupId(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return null;
            }

            string trimmedText = menuText.Trim();
            foreach (ObjectJudgementGroupSettings group in systemParameters.ObjectJudgementGroups)
            {
                if (string.Equals(
                    trimmedText,
                    CreateObjectJudgementGroupText(group, 0).Trim(),
                    StringComparison.Ordinal) ||
                    string.Equals(
                        trimmedText,
                        CreateObjectJudgementGroupText(group, 1).Trim(),
                        StringComparison.Ordinal))
                {
                    return group.Id;
                }
            }

            return null;
        }

        private int GetObjectJudgementIndex(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return -1;
            }

            string name = menuText.Trim();
            for (int index = 0; index < systemParameters.ObjectJudgements.Count; index++)
            {
            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[index];
            string displayName = string.IsNullOrWhiteSpace(objectJudgement.DisplayName)
                ? "區塊" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : objectJudgement.DisplayName.Trim();
                if (string.Equals(displayName, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private void SelectObjectJudgementItem(int objectIndex)
        {
            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                if (GetObjectJudgementIndex(functionListBox.Items[itemIndex] as string) == objectIndex)
                {
                    functionListBox.SelectedIndex = itemIndex;
                    return;
                }
            }
        }

        private void ToggleObjectJudgementMenu()
        {
            objectJudgementMenuExpanded = !objectJudgementMenuExpanded;
            RebuildVisibleObjectJudgements();
            statusLabel.Text = objectJudgementMenuExpanded ? "已展開整合成區塊" : "已收合整合成區塊";
        }

        private void ToggleObjectJudgement(int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            string objectId = systemParameters.ObjectJudgements[objectIndex].Id;
            if (!expandedObjectJudgementIds.Remove(objectId))
            {
                expandedObjectJudgementIds.Add(objectId);
            }

            RebuildVisibleObjectJudgements();
            SelectObjectJudgementItem(objectIndex);
        }

        private void ToggleObjectJudgementGroup(string groupId)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            if (!expandedObjectJudgementGroupIds.Remove(group.Id))
            {
                expandedObjectJudgementGroupIds.Add(group.Id);
            }

            RebuildVisibleObjectJudgements();
            SelectObjectJudgementGroup(group);
        }

        private void AddObjectJudgement()
        {
            systemParameters.ObjectJudgements.Add(new ObjectJudgementSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "區塊" + (systemParameters.ObjectJudgements.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                RelationType = string.Empty,
                RelationId = string.Empty
            });
            objectJudgementMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            functionListBox.SelectedItem = CreateObjectJudgementText(systemParameters.ObjectJudgements.Count);
            statusLabel.Text = "已新增區塊，請選擇影像關聯";
        }

        private void RebuildVisibleObjectJudgements()
        {
            bool wasRebuilding = isRebuildingObjectJudgementMenu;
            isRebuildingObjectJudgementMenu = true;
            try
            {
                int objectMenuIndex = functionListBox.Items.IndexOf(ObjectJudgementMenuText);
                if (objectMenuIndex < 0)
                {
                    return;
                }

                int removeIndex = objectMenuIndex + 1;
                while (removeIndex < functionListBox.Items.Count)
                {
                    string text = functionListBox.Items[removeIndex] as string;
                    if (string.IsNullOrEmpty(text) || !text.StartsWith("    ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    functionListBox.Items.RemoveAt(removeIndex);
                }

                if (!objectJudgementMenuExpanded)
                {
                    return;
                }

                int insertIndex = objectMenuIndex + 1;
                for (int index = 0; index < systemParameters.ObjectJudgements.Count; index++)
                {
                    ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[index];
                    if (string.IsNullOrWhiteSpace(objectJudgement.GroupId))
                    {
                        InsertVisibleObjectJudgement(objectJudgement, index, 0, ref insertIndex);
                    }
                }

                foreach (ObjectJudgementGroupSettings group in systemParameters.ObjectJudgementGroups)
                {
                    if (string.IsNullOrWhiteSpace(group.ParentGroupId))
                    {
                        InsertVisibleObjectJudgementGroup(group, 0, ref insertIndex);
                    }
                }
            }
            finally
            {
                isRebuildingObjectJudgementMenu = wasRebuilding;
            }
        }

        private void InsertVisibleObjectJudgement(
            ObjectJudgementSettings objectJudgement,
            int objectIndex,
            int depth,
            ref int insertIndex)
        {
            functionListBox.Items.Insert(
                insertIndex++,
                new string(' ', 4 + (depth * 2)) +
                GetObjectJudgementDisplayName(objectJudgement, objectIndex));
            if (!expandedObjectJudgementIds.Contains(objectJudgement.Id) ||
                objectJudgement.ProcessingSteps.Count == 0)
            {
                return;
            }

            for (int processingIndex = 0; processingIndex < objectJudgement.ProcessingSteps.Count; processingIndex++)
            {
                ObjectJudgementProcessingSettings processing = objectJudgement.ProcessingSteps[processingIndex];
                functionListBox.Items.Insert(
                    insertIndex++,
                    new string(' ', 6 + ((depth + 1) * 2)) +
                    GetObjectJudgementProcessingDisplayName(processing, processingIndex));
            }
        }

        private void InsertVisibleObjectJudgementGroup(
            ObjectJudgementGroupSettings group,
            int depth,
            ref int insertIndex)
        {
            functionListBox.Items.Insert(insertIndex++, CreateObjectJudgementGroupText(group, depth));
            if (!expandedObjectJudgementGroupIds.Contains(group.Id))
            {
                return;
            }

            for (int index = 0; index < systemParameters.ObjectJudgements.Count; index++)
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[index];
                if (string.Equals(objectJudgement.GroupId, group.Id, StringComparison.Ordinal))
                {
                    InsertVisibleObjectJudgement(objectJudgement, index, depth + 1, ref insertIndex);
                }
            }

            foreach (ObjectJudgementGroupSettings childGroup in systemParameters.ObjectJudgementGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    InsertVisibleObjectJudgementGroup(childGroup, depth + 1, ref insertIndex);
                }
            }
        }

        private void ShowObjectJudgementMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增區塊", null, delegate { AddObjectJudgement(); });
            menu.Show(functionListBox, location);
        }

        private List<int> GetSelectedObjectJudgementIndexes()
        {
            var indexes = new List<int>();
            foreach (int selectedIndex in functionListBox.SelectedIndices)
            {
                int objectIndex = GetObjectJudgementIndex(functionListBox.Items[selectedIndex] as string);
                if (objectIndex >= 0 && !indexes.Contains(objectIndex))
                {
                    indexes.Add(objectIndex);
                }
            }

            indexes.Sort();
            return indexes;
        }

        private List<string> GetSelectedObjectJudgementGroupIds()
        {
            var groupIds = new List<string>();
            foreach (int selectedIndex in functionListBox.SelectedIndices)
            {
                string groupId = GetObjectJudgementGroupId(functionListBox.Items[selectedIndex] as string);
                if (!string.IsNullOrEmpty(groupId) && !groupIds.Contains(groupId))
                {
                    groupIds.Add(groupId);
                }
            }

            return groupIds;
        }

        private void ShowObjectJudgementMultiSelectContextMenu(
            List<int> objectIndexes,
            Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("分組", null, delegate { CreateObjectJudgementGroup(objectIndexes); });
            menu.Show(functionListBox, location);
        }

        private void CreateObjectJudgementGroup(List<int> objectIndexes)
        {
            if (objectIndexes == null || objectIndexes.Count < 2)
            {
                statusLabel.Text = "至少選擇兩個區塊才能分組";
                return;
            }

            List<int> rootObjectIndexes = objectIndexes
                .Where(index => index >= 0 && index < systemParameters.ObjectJudgements.Count)
                .Where(index => string.IsNullOrWhiteSpace(systemParameters.ObjectJudgements[index].GroupId))
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (rootObjectIndexes.Count < 2)
            {
                statusLabel.Text = "只能將尚未分組的區塊建立群組";
                return;
            }

            var newGroup = new ObjectJudgementGroupSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "群組" +
                    (systemParameters.ObjectJudgementGroups.Count + 1).ToString(CultureInfo.InvariantCulture),
                ParentGroupId = string.Empty
            };
            systemParameters.ObjectJudgementGroups.Add(newGroup);
            foreach (int objectIndex in rootObjectIndexes)
            {
                systemParameters.ObjectJudgements[objectIndex].GroupId = newGroup.Id;
            }

            objectJudgementMenuExpanded = true;
            expandedObjectJudgementGroupIds.Add(newGroup.Id);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            functionListBox.SelectedItem = CreateObjectJudgementGroupText(newGroup, 0);
            statusLabel.Text = "已建立物件群組";
        }

        private void ShowObjectJudgementGroupContextMenu(string groupId, Point location)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessObjectJudgementGroup(group.Id); });
            menu.Items.Add("上移", null, delegate { MoveObjectJudgementGroup(group.Id, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectJudgementGroup(group.Id, 1); });
            menu.Items.Add("命名", null, delegate { RenameObjectJudgementGroup(group.Id); });
            menu.Items.Add("新增區塊", null, delegate { AddObjectJudgementToGroup(group.Id); });
            menu.Items.Add("解除群組", null, delegate { UngroupObjectJudgements(group.Id); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectJudgementGroup(group.Id); });
            menu.Show(functionListBox, location);
        }

        private void AddObjectJudgementToGroup(string groupId)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            int objectNumber = systemParameters.ObjectJudgements.Count + 1;
            var objectJudgement = new ObjectJudgementSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "區塊" + objectNumber.ToString(CultureInfo.InvariantCulture),
                RelationType = string.Empty,
                RelationId = string.Empty,
                GroupId = group.Id
            };
            systemParameters.ObjectJudgements.Add(objectJudgement);
            objectJudgementMenuExpanded = true;
            expandedObjectJudgementGroupIds.Add(group.Id);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            SelectObjectJudgementItem(systemParameters.ObjectJudgements.Count - 1);
            statusLabel.Text = "已新增" + objectJudgement.DisplayName + "至" + group.DisplayName;
        }

        private void ShowObjectJudgementGroupMultiSelectContextMenu(
            List<string> groupIds,
            Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("分組", null, delegate { CreateObjectJudgementGroupFromGroups(groupIds); });
            menu.Show(functionListBox, location);
        }

        private void CreateObjectJudgementGroupFromGroups(List<string> groupIds)
        {
            if (groupIds == null || groupIds.Count < 2)
            {
                statusLabel.Text = "至少選擇兩個群組才能分組";
                return;
            }

            List<ObjectJudgementGroupSettings> selectedGroups = groupIds
                .Select(FindObjectJudgementGroup)
                .Where(group => group != null)
                .Distinct()
                .ToList();
            if (selectedGroups.Count < 2)
            {
                statusLabel.Text = "至少選擇兩個有效群組才能分組";
                return;
            }

            var newGroup = new ObjectJudgementGroupSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "群組" +
                    (systemParameters.ObjectJudgementGroups.Count + 1).ToString(CultureInfo.InvariantCulture),
                ParentGroupId = string.Empty
            };
            systemParameters.ObjectJudgementGroups.Add(newGroup);
            foreach (ObjectJudgementGroupSettings selectedGroup in selectedGroups)
            {
                selectedGroup.ParentGroupId = newGroup.Id;
            }

            objectJudgementMenuExpanded = true;
            expandedObjectJudgementGroupIds.Add(newGroup.Id);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            SelectObjectJudgementGroup(newGroup);
            statusLabel.Text = "已建立物件群組";
        }

        private void SelectObjectJudgementGroup(ObjectJudgementGroupSettings group)
        {
            if (group == null)
            {
                return;
            }

            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                if (string.Equals(
                    GetObjectJudgementGroupId(functionListBox.Items[itemIndex] as string),
                    group.Id,
                    StringComparison.Ordinal))
                {
                    functionListBox.SelectedIndex = itemIndex;
                    return;
                }
            }
        }

        private void MoveObjectJudgementGroup(string groupId, int direction)
        {
            int index = systemParameters.ObjectJudgementGroups.FindIndex(
                group => string.Equals(group.Id, groupId, StringComparison.Ordinal));
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 ||
                targetIndex >= systemParameters.ObjectJudgementGroups.Count)
            {
                return;
            }

            ObjectJudgementGroupSettings movedGroup = systemParameters.ObjectJudgementGroups[index];
            systemParameters.ObjectJudgementGroups.RemoveAt(index);
            systemParameters.ObjectJudgementGroups.Insert(targetIndex, movedGroup);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            SelectObjectJudgementGroup(movedGroup);
        }

        private void RenameObjectJudgementGroup(string groupId)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            string currentName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? "物件群組"
                : group.DisplayName.Trim();
            string name = PromptForText("物件群組名稱", "請輸入物件群組名稱：", currentName);
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            group.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            SelectObjectJudgementGroup(group);
        }

        private void UngroupObjectJudgements(string groupId)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            string parentGroupId = group.ParentGroupId ?? string.Empty;
            foreach (ObjectJudgementSettings objectJudgement in systemParameters.ObjectJudgements)
            {
                if (string.Equals(objectJudgement.GroupId, group.Id, StringComparison.Ordinal))
                {
                    objectJudgement.GroupId = parentGroupId;
                }
            }

            foreach (ObjectJudgementGroupSettings childGroup in systemParameters.ObjectJudgementGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    childGroup.ParentGroupId = parentGroupId;
                }
            }

            systemParameters.ObjectJudgementGroups.Remove(group);
            expandedObjectJudgementGroupIds.Remove(group.Id);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            statusLabel.Text = "已解除物件群組";
        }

        private void DeleteObjectJudgementGroup(string groupId)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            if (MessageBox.Show(
                    this,
                    "刪除群組後會保留其中的區塊，是否繼續？",
                    "刪除物件群組",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            UngroupObjectJudgements(group.Id);
        }

        private bool TryGetObjectJudgementProcessingLocation(
            string menuText,
            out int objectIndex,
            out int processingIndex)
        {
            return TryGetObjectJudgementProcessingLocation(
                functionListBox == null ? -1 : functionListBox.SelectedIndex,
                menuText,
                out objectIndex,
                out processingIndex);
        }

        private bool TryGetObjectJudgementProcessingLocation(
            int visibleItemIndex,
            string menuText,
            out int objectIndex,
            out int processingIndex)
        {
            objectIndex = -1;
            processingIndex = -1;
            if (functionListBox == null ||
                visibleItemIndex < 0 ||
                visibleItemIndex >= functionListBox.Items.Count ||
                string.IsNullOrWhiteSpace(menuText) ||
                !menuText.StartsWith("        ", StringComparison.Ordinal))
            {
                return false;
            }

            string name = menuText.Trim();
            int processingIndent = CountLeadingSpaces(menuText);
            for (int itemIndex = visibleItemIndex - 1; itemIndex >= 0; itemIndex--)
            {
                string candidateText = functionListBox.Items[itemIndex] as string;
                if (string.IsNullOrWhiteSpace(candidateText) ||
                    CountLeadingSpaces(candidateText) >= processingIndent)
                {
                    continue;
                }

                int candidateObjectIndex = GetObjectJudgementIndex(candidateText);
                if (candidateObjectIndex < 0)
                {
                    continue;
                }

                ObjectJudgementSettings objectJudgement =
                    systemParameters.ObjectJudgements[candidateObjectIndex];
                for (int currentProcessingIndex = 0;
                    currentProcessingIndex < objectJudgement.ProcessingSteps.Count;
                    currentProcessingIndex++)
                {
                    if (string.Equals(
                        GetObjectJudgementProcessingDisplayName(
                            objectJudgement.ProcessingSteps[currentProcessingIndex],
                            currentProcessingIndex),
                        name,
                        StringComparison.Ordinal))
                    {
                        objectIndex = candidateObjectIndex;
                        processingIndex = currentProcessingIndex;
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private static int CountLeadingSpaces(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            while (count < value.Length && value[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private void ShowObjectJudgementItemContextMenu(int objectIndex, Point location)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessObjectJudgement(objectIndex); });
            menu.Items.Add("上移", null, delegate { MoveObjectJudgement(objectIndex, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectJudgement(objectIndex, 1); });
            menu.Items.Add("命名", null, delegate { RenameObjectJudgement(objectIndex); });
            menu.Items.Add("新增處理", null, delegate { AddObjectJudgementProcessing(objectIndex); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectJudgement(objectIndex); });
            menu.Show(functionListBox, location);
        }

        private void ProcessObjectJudgement(int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            // A block command always runs the complete child chain. Keep this
            // separate from the child command, which intentionally runs only
            // through the clicked child index.
            StartObjectJudgementProcessing(objectIndex);
        }

        private void FocusObjectJudgementProcessing(int objectIndex, int processingIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            if (processingIndex < 0 || processingIndex >= objectJudgement.ProcessingSteps.Count)
            {
                return;
            }

            objectJudgementMenuExpanded = true;
            expandedObjectJudgementIds.Add(objectJudgement.Id);
            RebuildVisibleObjectJudgements();

            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                int selectedObjectIndex;
                int selectedProcessingIndex;
                if (TryGetObjectJudgementProcessingLocation(
                        functionListBox.Items[itemIndex] as string,
                        out selectedObjectIndex,
                        out selectedProcessingIndex) &&
                    selectedObjectIndex == objectIndex &&
                    selectedProcessingIndex == processingIndex)
                {
                    functionListBox.SelectedIndex = itemIndex;
                    return;
                }
            }

            // A block inside a collapsed object group may not be visible in
            // the list. The parameter panel can still be opened directly.
            ShowObjectJudgementProcessingParameterPanel(objectIndex, processingIndex);
        }

        private void ProcessObjectJudgementGroup(string groupId)
        {
            if (FindObjectJudgementGroup(groupId) == null)
            {
                return;
            }

            StartObjectJudgementGroupProcessing(groupId);
        }

        private void RequestObjectJudgementRelatedImageDisplays(ObjectJudgementSettings objectJudgement)
        {
            if (objectJudgement == null || string.IsNullOrWhiteSpace(objectJudgement.RelationId))
            {
                return;
            }

            string displaySignature = CreateObjectJudgementRelatedDisplaySignature(objectJudgement);
            if (string.Equals(requestedObjectJudgementDisplaySignature, displaySignature, StringComparison.Ordinal) &&
                IsObjectJudgementRelatedDisplayStateActive(objectJudgement))
            {
                return;
            }

            if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
            {
                // A block linked to a relation group shows the combined relation
                // result in the processed-image tab before block processing runs.
                ProcessImageRelationGroup(objectJudgement.RelationId);
                requestedObjectJudgementDisplaySignature = displaySignature;
                return;
            }

            if (!string.Equals(objectJudgement.RelationType, "Relation", StringComparison.Ordinal))
            {
                return;
            }

            int relationIndex = systemParameters.ImageRelations.FindIndex(
                relation => string.Equals(relation.Id, objectJudgement.RelationId, StringComparison.Ordinal));
            if (relationIndex >= 0)
            {
                // This also prepares the linked preprocessing source when the
                // relation does not use the original image.
                ProcessImageRelation(relationIndex);
                requestedObjectJudgementDisplaySignature = displaySignature;
            }
        }

        private string CreateObjectJudgementRelatedDisplaySignature(ObjectJudgementSettings objectJudgement)
        {
            var parts = new List<string>
            {
                systemParameters.LastImagePath ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                objectJudgement.RelationType ?? string.Empty,
                objectJudgement.RelationId ?? string.Empty
            };

            List<ImageRelationSettings> relations;
            if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
            {
                relations = GetImageRelationGroupRelations(objectJudgement.RelationId);
            }
            else
            {
                ImageRelationSettings relation = systemParameters.ImageRelations.FirstOrDefault(
                    item => string.Equals(item.Id, objectJudgement.RelationId, StringComparison.Ordinal));
                relations = relation == null
                    ? new List<ImageRelationSettings>()
                    : new List<ImageRelationSettings> { relation };
            }

            foreach (ImageRelationSettings relation in relations)
            {
                parts.Add(relation.Id ?? string.Empty);
                parts.Add(relation.SourceType ?? string.Empty);
                parts.Add(relation.SourceId ?? string.Empty);
                parts.Add(relation.ProcessingType ?? string.Empty);
                parts.Add(relation.ProcessingId ?? string.Empty);
                foreach (ImageProcessingStepSettings step in GetImageProcessingStepsForRelation(relation))
                {
                    parts.Add(step.Id ?? string.Empty);
                    parts.Add(step.Method ?? string.Empty);
                    parts.Add(step.Parameters ?? string.Empty);
                }
            }

            foreach (ImageProcessingStepSettings step in systemParameters.ImagePreprocessingSteps)
            {
                parts.Add("preprocess");
                parts.Add(step.Id ?? string.Empty);
                parts.Add(step.Method ?? string.Empty);
                parts.Add(step.Parameters ?? string.Empty);
            }

            return string.Join("|", parts.ToArray());
        }

        private bool IsObjectJudgementRelatedDisplayStateActive(ObjectJudgementSettings objectJudgement)
        {
            if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
            {
                return string.Equals(activeImageRelationGroupId, objectJudgement.RelationId, StringComparison.Ordinal) &&
                    imageProcessingExecutionRequested;
            }

            ImageRelationSettings relation = systemParameters.ImageRelations.FirstOrDefault(
                item => string.Equals(item.Id, objectJudgement.RelationId, StringComparison.Ordinal));
            return relation != null &&
                string.IsNullOrWhiteSpace(activeImageRelationGroupId) &&
                string.Equals(activeImageRelationSourceType, relation.SourceType, StringComparison.Ordinal) &&
                string.Equals(activeImageRelationSourceId, relation.SourceId, StringComparison.Ordinal) &&
                imageProcessingExecutionRequested;
        }

        private void AddObjectJudgementProcessing(int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            objectJudgement.ProcessingSteps.Add(new ObjectJudgementProcessingSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "處理" + (objectJudgement.ProcessingSteps.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Method = string.Empty,
                Parameters = string.Empty
            });
            SaveSystemParameters();
            objectJudgementMenuExpanded = true;
            expandedObjectJudgementIds.Add(objectJudgement.Id);
            RebuildVisibleObjectJudgements();
            functionListBox.SelectedItem = "        " + GetObjectJudgementProcessingDisplayName(
                objectJudgement.ProcessingSteps[objectJudgement.ProcessingSteps.Count - 1],
                objectJudgement.ProcessingSteps.Count - 1);
            statusLabel.Text = "已新增區塊處理，請選擇處理方式";
        }

        private void ShowObjectJudgementProcessingItemContextMenu(int objectIndex, int processingIndex, Point location)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            if (processingIndex < 0 || processingIndex >= objectJudgement.ProcessingSteps.Count)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessObjectJudgementProcessing(objectIndex, processingIndex); });
            menu.Items.Add("上移", null, delegate { MoveObjectJudgementProcessing(objectIndex, processingIndex, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectJudgementProcessing(objectIndex, processingIndex, 1); });
            menu.Items.Add("命名", null, delegate { RenameObjectJudgementProcessing(objectIndex, processingIndex); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectJudgementProcessing(objectIndex, processingIndex); });
            menu.Show(functionListBox, location);
        }

        private void ProcessObjectJudgementProcessing(int objectIndex, int processingIndex)
        {
            ObjectJudgementProcessingSettings processing = GetObjectJudgementProcessing(objectIndex, processingIndex);
            if (processing == null)
            {
                return;
            }

            StartObjectJudgementProcessing(objectIndex, processingIndex);
        }

        private void MoveObjectJudgementProcessing(int objectIndex, int processingIndex, int direction)
        {
            ObjectJudgementSettings objectJudgement;
            ObjectJudgementProcessingSettings processing;
            if (!TryGetObjectJudgementProcessing(objectIndex, processingIndex, out objectJudgement, out processing))
            {
                return;
            }

            int targetIndex = processingIndex + direction;
            if (targetIndex < 0 || targetIndex >= objectJudgement.ProcessingSteps.Count)
            {
                return;
            }

            objectJudgement.ProcessingSteps.RemoveAt(processingIndex);
            objectJudgement.ProcessingSteps.Insert(targetIndex, processing);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            functionListBox.SelectedItem = "        " + GetObjectJudgementProcessingDisplayName(processing, targetIndex);
        }

        private void RenameObjectJudgementProcessing(int objectIndex, int processingIndex)
        {
            ObjectJudgementProcessingSettings processing = GetObjectJudgementProcessing(objectIndex, processingIndex);
            if (processing == null)
            {
                return;
            }

            string currentName = GetObjectJudgementProcessingDisplayName(processing, processingIndex);
            string name = PromptForText("處理名稱", "請輸入處理名稱：", currentName);
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            processing.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            functionListBox.SelectedItem = "        " + processing.DisplayName;
        }

        private void DeleteObjectJudgementProcessing(int objectIndex, int processingIndex)
        {
            ObjectJudgementSettings objectJudgement;
            ObjectJudgementProcessingSettings processing;
            if (!TryGetObjectJudgementProcessing(objectIndex, processingIndex, out objectJudgement, out processing))
            {
                return;
            }

            string displayName = GetObjectJudgementProcessingDisplayName(processing, processingIndex);
            if (MessageBox.Show(
                    this,
                    "是否要刪除「" + displayName + "」？",
                    "刪除區塊處理",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            objectJudgement.ProcessingSteps.RemoveAt(processingIndex);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            statusLabel.Text = "已刪除" + displayName;
        }

        private ObjectJudgementProcessingSettings GetObjectJudgementProcessing(int objectIndex, int processingIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return null;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            return processingIndex >= 0 && processingIndex < objectJudgement.ProcessingSteps.Count
                ? objectJudgement.ProcessingSteps[processingIndex]
                : null;
        }

        private bool TryGetObjectJudgementProcessing(
            int objectIndex,
            int processingIndex,
            out ObjectJudgementSettings objectJudgement,
            out ObjectJudgementProcessingSettings processing)
        {
            objectJudgement = null;
            processing = null;
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return false;
            }

            objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            if (processingIndex < 0 || processingIndex >= objectJudgement.ProcessingSteps.Count)
            {
                objectJudgement = null;
                return false;
            }

            processing = objectJudgement.ProcessingSteps[processingIndex];
            return true;
        }

        private static string GetObjectJudgementProcessingDisplayName(
            ObjectJudgementProcessingSettings processing,
            int processingIndex)
        {
            return processing == null || string.IsNullOrWhiteSpace(processing.DisplayName)
                ? "處理" + (processingIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : processing.DisplayName.Trim();
        }

        private void ActivateObjectJudgementRelation(ObjectJudgementSettings objectJudgement)
        {
            SetActiveObjectJudgement(objectJudgement);
            activeImageRelationGroupId = null;
            selectedImageRelationGroupId = null;
            activeImageRelationSourceType = "Original";
            activeImageRelationSourceId = null;
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;

            if (objectJudgement == null)
            {
                return;
            }

            if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
            {
                activeImageRelationGroupId = objectJudgement.RelationId;
                InvalidateBlockProcessingDisplays();
                return;
            }

            if (!string.Equals(objectJudgement.RelationType, "Relation", StringComparison.Ordinal))
            {
                return;
            }

            ImageRelationSettings relation = systemParameters.ImageRelations.Find(
                item => string.Equals(item.Id, objectJudgement.RelationId, StringComparison.Ordinal));
            if (relation == null)
            {
                return;
            }

            activeImageRelationSourceType = relation.SourceType;
            activeImageRelationSourceId = relation.SourceId;
            if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal))
            {
                // Processing groups and image-relation groups use different
                // namespaces. Keep the processing group in its own selector;
                // activeImageRelationGroupId is reserved for relation groups.
                selectedImageProcessingGroupId = relation.ProcessingId;
                InvalidateBlockProcessingDisplays();
                return;
            }

            selectedImageProcessingStepIndex = systemParameters.ImageProcessingSteps.FindIndex(
                item => string.Equals(item.Id, relation.ProcessingId, StringComparison.Ordinal));
            InvalidateBlockProcessingDisplays();
        }

        private void ShowObjectJudgementProcessingParameterPanel(int objectIndex, int processingIndex)
        {
            ObjectJudgementProcessingSettings processing = GetObjectJudgementProcessing(objectIndex, processingIndex);
            if (processing == null)
            {
                return;
            }

            parameterPlaceholderLabel.Visible = false;
            HideImageRelationParameterPanel();
            HideImageProcessingParameterPanel();
            HideObjectJudgementParameterPanel();
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
            }
            if (imagePreprocessingFlowTreeView != null)
            {
                imagePreprocessingFlowTreeView.Visible = false;
            }

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            objectJudgementParameterPanel = panel;
            BuildObjectJudgementProcessingParameterPanel(panel, processing, objectIndex, processingIndex);
            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            rightPanelTitleLabel.Text = GetObjectJudgementProcessingDisplayName(processing, processingIndex) + " 參數";
        }

        private void MoveObjectJudgement(int objectIndex, int direction)
        {
            int targetIndex = objectIndex + direction;
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count ||
                targetIndex < 0 || targetIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            systemParameters.ObjectJudgements.RemoveAt(objectIndex);
            systemParameters.ObjectJudgements.Insert(targetIndex, objectJudgement);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            SelectObjectJudgementItem(targetIndex);
        }

        private void RenameObjectJudgement(int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            string currentName = GetObjectJudgementDisplayName(objectJudgement, objectIndex);
            string name = PromptForText("區塊名稱", "請輸入區塊名稱：", currentName);
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            objectJudgement.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            SelectObjectJudgementItem(objectIndex);
        }

        private void DeleteObjectJudgement(int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            string displayName = GetObjectJudgementDisplayName(objectJudgement, objectIndex);
            if (MessageBox.Show(
                    this,
                    "是否要刪除「" + displayName + "」？",
                    "刪除整合成區塊",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            systemParameters.ObjectJudgements.RemoveAt(objectIndex);
            SaveSystemParameters();
            RebuildVisibleObjectJudgements();
            statusLabel.Text = "已刪除" + displayName;
        }

        private static string GetObjectJudgementDisplayName(ObjectJudgementSettings objectJudgement, int objectIndex)
        {
            return string.IsNullOrWhiteSpace(objectJudgement.DisplayName)
                ? "區塊" + (objectIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : objectJudgement.DisplayName.Trim();
        }

        private void ShowObjectJudgementParameterPanel(int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            parameterPlaceholderLabel.Visible = false;
            HideImageRelationParameterPanel();
            HideImageProcessingParameterPanel();
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
            }
            if (imagePreprocessingFlowTreeView != null)
            {
                imagePreprocessingFlowTreeView.Visible = false;
            }
            if (objectJudgementParameterPanel != null)
            {
                parameterPanel.Controls.Remove(objectJudgementParameterPanel);
            }

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            objectJudgementParameterPanel = panel;
            panel.Controls.Add(new Label { Text = "影像關聯", Left = 8, Top = 12, Width = 250 });
            var relationCombo = new ComboBox
            {
                Left = 8,
                Top = 34,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                string displayName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名關聯" : relation.DisplayName;
                relationCombo.Items.Add(new RelationChoice
                {
                    DisplayText = displayName,
                    Type = "Relation",
                    Id = relation.Id
                });
            }

            foreach (ImageRelationGroupSettings group in systemParameters.ImageRelationGroups)
            {
                string displayName = string.IsNullOrWhiteSpace(group.DisplayName) ? "未命名群組" : group.DisplayName;
                relationCombo.Items.Add(new RelationChoice
                {
                    DisplayText = "關聯群組(" + displayName + ")（合併全部二值 MASK）",
                    Type = "Group",
                    Id = group.Id
                });
            }

            relationCombo.SelectedIndex = FindObjectRelationChoiceIndex(
                relationCombo,
                objectJudgement.RelationType,
                objectJudgement.RelationId);
            panel.Controls.Add(relationCombo);

            var apply = new Button
            {
                Text = "套用",
                Left = 8,
                Top = 78,
                Width = parameterPanel.Width - 18
            };
            apply.Click += delegate
            {
                RelationChoice choice = relationCombo.SelectedItem as RelationChoice;
                if (choice == null)
                {
                    objectJudgement.RelationType = string.Empty;
                    objectJudgement.RelationId = string.Empty;
                }
                else
                {
                    objectJudgement.RelationType = choice.Type;
                    objectJudgement.RelationId = choice.Id;
                }

                SaveSystemParameters();
                // Changing a block relation invalidates both the block result and
                // any group result that may contain this block. Re-activate the
                // selected source so the next run cannot reuse the old relation.
                InvalidateObjectJudgementProcessingResults();
                ActivateObjectJudgementRelation(objectJudgement);
                statusLabel.Text = "已套用" + objectJudgement.DisplayName + " 的影像關聯";
            };
            panel.Controls.Add(apply);
            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            rightPanelTitleLabel.Text = objectJudgement.DisplayName + " 參數";
        }

        private void HideObjectJudgementParameterPanel()
        {
            if (objectJudgementParameterPanel != null)
            {
                objectJudgementParameterPanel.Visible = false;
            }
        }

        private void NormalizeObjectJudgementDefaultNames()
        {
            bool changed = false;
            for (int index = 0; index < systemParameters.ObjectJudgements.Count; index++)
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[index];
                string oldName = "物件" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (string.Equals(objectJudgement.DisplayName, oldName, StringComparison.Ordinal))
                {
                    objectJudgement.DisplayName = "區塊" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSystemParameters();
            }
        }

        private static int FindObjectRelationChoiceIndex(ComboBox combo, string type, string id)
        {
            for (int index = 0; index < combo.Items.Count; index++)
            {
                RelationChoice choice = combo.Items[index] as RelationChoice;
                if (choice != null &&
                    string.Equals(choice.Type, type ?? string.Empty, StringComparison.Ordinal) &&
                    string.Equals(choice.Id, id ?? string.Empty, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return combo.Items.Count > 0 ? 0 : -1;
        }
    }
}
