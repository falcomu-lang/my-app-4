using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private void ShowImageProcessingMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("新增影像處理", null, delegate { AddImageProcessingStep(); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowRoiMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("新增 ROI", null, delegate { BeginAddRoiSelection(); });
            menu.Items.Add("顯示全部", null, delegate { ShowAllRoiOverlays(); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowRoiItemContextMenu(string roiText, Point location)
        {
            int roiIndex = GetRoiIndex(roiText);
            if (roiIndex < 0 || roiIndex >= systemParameters.RoiRegions.Count)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            string roiId = systemParameters.RoiRegions[roiIndex].Id;
            menu.Items.Add("編輯 ROI", null, delegate { BeginEditRoi(roiId); });
            menu.Items.Add("刪除", null, delegate
            {
                expandedRoiText = roiText;
                DeleteSelectedRoi();
            });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private List<string> GetSelectedImageProcessingGroupIds()
        {
            var groupIds = new List<string>();
            foreach (object selectedItem in functionListBox.SelectedItems)
            {
                string groupId = GetImageProcessingGroupId(selectedItem as string);
                if (!string.IsNullOrEmpty(groupId) && !groupIds.Contains(groupId))
                {
                    groupIds.Add(groupId);
                }
            }

            return groupIds;
        }

        private void ShowImageProcessingGroupContextMenu(List<int> stepIndexes, List<string> groupIds, Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("分組", null, delegate { CreateImageProcessingGroup(stepIndexes, groupIds); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowImageProcessingStepContextMenu(string stepText, Point location)
        {
            CloseImageProcessingStepContextMenu();

            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("處理", null, delegate
            {
                ProcessImageProcessingStep(stepText);
            });
            menu.Items.Add("上移", null, delegate
            {
                expandedImageProcessingStepText = stepText;
                MoveImageProcessingStep(-1);
            });
            menu.Items.Add("下移", null, delegate
            {
                expandedImageProcessingStepText = stepText;
                MoveImageProcessingStep(1);
            });
            menu.Items.Add("命名", null, delegate { RenameImageProcessingStep(stepText); });
            menu.Items.Add("刪除", null, delegate
            {
                expandedImageProcessingStepText = stepText;
                DeleteImageProcessingStep();
            });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void ShowImageProcessingGroupItemContextMenu(string groupId, Point location)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("處理", null, delegate { ProcessImageProcessingGroup(group.Id); });
            menu.Items.Add("上移", null, delegate { MoveImageProcessingGroup(group.Id, -1); });
            menu.Items.Add("下移", null, delegate { MoveImageProcessingGroup(group.Id, 1); });
            menu.Items.Add("命名", null, delegate { RenameImageProcessingGroup(group.Id); });
            menu.Items.Add("新增處理", null, delegate { AddImageProcessingStepToGroup(group.Id); });
            menu.Items.Add("解除群組", null, delegate { UngroupImageProcessingGroup(group.Id); });
            menu.Items.Add("刪除", null, delegate { DeleteImageProcessingGroup(group.Id); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu))
                {
                    imageProcessingStepContextMenu = null;
                }
            };
            menu.Show(functionListBox, location);
        }

        private void CloseImageProcessingStepContextMenu()
        {
            ContextMenuStrip menu = imageProcessingStepContextMenu;
            imageProcessingStepContextMenu = null;
            if (menu != null && !menu.IsDisposed)
            {
                menu.Close();
                if (!menu.IsDisposed)
                {
                    menu.Dispose();
                }
            }
        }

        private void CreateImageProcessingGroup(List<int> stepIndexes, List<string> groupIds)
        {
            int stepCount = stepIndexes == null ? 0 : stepIndexes.Count;
            int groupCount = groupIds == null ? 0 : groupIds.Count;
            if (stepCount + groupCount < 2)
            {
                return;
            }

            string displayName;
            if (!TryGetImageProcessingStepName(string.Empty, out displayName))
            {
                return;
            }

            var group = new ImageProcessingGroupSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "未命名群組" : displayName
            };
            systemParameters.ImageProcessingGroups.Add(group);
            foreach (int stepIndex in stepIndexes)
            {
                if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
                {
                    systemParameters.ImageProcessingSteps[stepIndex].GroupId = group.Id;
                }
            }

            foreach (string groupId in groupIds)
            {
                ImageProcessingGroupSettings childGroup = FindImageProcessingGroup(groupId);
                if (childGroup != null)
                {
                    childGroup.ParentGroupId = group.Id;
                }
            }

            RemoveEmptyImageProcessingGroups();
            SaveSystemParameters();
            MarkProcessedImageDirty();
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = CreateImageProcessingGroupText(group);
            statusLabel.Text = "已建立群組：" + group.DisplayName;
        }

        private void RenameImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            string name;
            if (!TryGetImageProcessingStepName(group.DisplayName, out name) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            group.DisplayName = name;
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = CreateImageProcessingGroupText(group);
            statusLabel.Text = "已命名群組：" + group.DisplayName;
        }

        private void MoveImageProcessingGroup(string groupId, int direction)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            var siblings = new List<ImageProcessingGroupSettings>();
            foreach (ImageProcessingGroupSettings candidate in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(candidate.ParentGroupId, group.ParentGroupId, StringComparison.Ordinal))
                {
                    siblings.Add(candidate);
                }
            }

            int siblingIndex = siblings.IndexOf(group);
            int targetSiblingIndex = siblingIndex + direction;
            if (siblingIndex < 0 || targetSiblingIndex < 0 || targetSiblingIndex >= siblings.Count)
            {
                return;
            }

            ImageProcessingGroupSettings target = siblings[targetSiblingIndex];
            int groupIndex = systemParameters.ImageProcessingGroups.IndexOf(group);
            systemParameters.ImageProcessingGroups.RemoveAt(groupIndex);
            int targetIndex = systemParameters.ImageProcessingGroups.IndexOf(target);
            systemParameters.ImageProcessingGroups.Insert(
                direction > 0 ? targetIndex + 1 : targetIndex,
                group);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = CreateImageProcessingGroupText(group);
            statusLabel.Text = "已移動群組：" + group.DisplayName;
        }

        private void UngroupImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "是否要解除群組「" + group.DisplayName + "」？群組內的處理項目與子群組會保留。",
                    "解除群組",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            foreach (ImageProcessingStepSettings step in systemParameters.ImageProcessingSteps)
            {
                if (string.Equals(step.GroupId, group.Id, StringComparison.Ordinal))
                {
                    step.GroupId = null;
                }
            }

            foreach (ImageProcessingGroupSettings child in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    child.ParentGroupId = group.ParentGroupId;
                }
            }

            expandedImageProcessingGroupIds.Remove(group.Id);
            systemParameters.ImageProcessingGroups.Remove(group);
            SaveSystemParameters();
            MarkProcessedImageDirty();
            RebuildVisibleImageProcessingSteps();
            statusLabel.Text = "已解除群組：" + group.DisplayName;
        }

        private void DeleteImageProcessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            var groupIdsToDelete = new HashSet<string>(StringComparer.Ordinal);
            CollectImageProcessingGroupAndDescendantIds(group.Id, groupIdsToDelete);
            int stepCount = systemParameters.ImageProcessingSteps.Count(step => groupIdsToDelete.Contains(step.GroupId));
            var deletedStepIds = new HashSet<string>(
                systemParameters.ImageProcessingSteps
                    .Where(step => groupIdsToDelete.Contains(step.GroupId))
                    .Select(step => step.Id),
                StringComparer.Ordinal);
            if (!ConfirmDeleteWithImageRelations(
                    "影像處理群組「" + group.DisplayName + "」及其底下的 " + stepCount + " 個處理",
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal),
                    deletedStepIds,
                    groupIdsToDelete))
            {
                return;
            }

            for (int index = systemParameters.ImageProcessingSteps.Count - 1; index >= 0; index--)
            {
                if (groupIdsToDelete.Contains(systemParameters.ImageProcessingSteps[index].GroupId))
                {
                    systemParameters.ImageProcessingSteps.RemoveAt(index);
                }
            }

            for (int index = systemParameters.ImageProcessingGroups.Count - 1; index >= 0; index--)
            {
                if (groupIdsToDelete.Contains(systemParameters.ImageProcessingGroups[index].Id))
                {
                    expandedImageProcessingGroupIds.Remove(systemParameters.ImageProcessingGroups[index].Id);
                    systemParameters.ImageProcessingGroups.RemoveAt(index);
                }
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            SaveSystemParameters();
            MarkProcessedImageDirty();
            ClearProcessedPreviewImages();
            RebuildVisibleImageProcessingSteps();
            statusLabel.Text = "已刪除群組：" + group.DisplayName;
        }

        private void CollectImageProcessingGroupAndDescendantIds(string groupId, HashSet<string> groupIds)
        {
            if (string.IsNullOrEmpty(groupId) || !groupIds.Add(groupId))
            {
                return;
            }

            foreach (ImageProcessingGroupSettings child in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(child.ParentGroupId, groupId, StringComparison.Ordinal))
                {
                    CollectImageProcessingGroupAndDescendantIds(child.Id, groupIds);
                }
            }
        }

        private void RemoveEmptyImageProcessingGroups()
        {
            bool removed;
            do
            {
                removed = false;
                for (int index = systemParameters.ImageProcessingGroups.Count - 1; index >= 0; index--)
                {
                    ImageProcessingGroupSettings group = systemParameters.ImageProcessingGroups[index];
                    bool containsStep = systemParameters.ImageProcessingSteps.Any(step =>
                        string.Equals(step.GroupId, group.Id, StringComparison.Ordinal));
                    bool containsGroup = systemParameters.ImageProcessingGroups.Any(child =>
                        !ReferenceEquals(child, group) &&
                        string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal));
                    if (containsStep || containsGroup)
                    {
                        continue;
                    }

                    expandedImageProcessingGroupIds.Remove(group.Id);
                    systemParameters.ImageProcessingGroups.RemoveAt(index);
                    removed = true;
                }
            }
            while (removed);
        }

        private bool IsImageProcessingStepCommandForClickedStep(int commandIndex)
        {
            for (int index = commandIndex - 1; index >= 0; index--)
            {
                string menuItem = functionListBox.Items[index] as string;
                if (IsImageProcessingStepCommandMenuItem(menuItem))
                {
                    continue;
                }

                return IsImageProcessingStepMenuItem(menuItem);
            }

            return false;
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
                    roiMenuExpanded = true;
                    RebuildVisibleRoiItems();
                }
            }
        }

        private void RemoveRoiSubMenuItems()
        {
            RemoveRoiSubMenuItemsFromListBox();
            roiMenuExpanded = false;
        }

        private void RemoveRoiSubMenuItemsFromListBox()
        {
            for (int index = functionListBox.Items.Count - 1; index >= 0; index--)
            {
                string itemText = functionListBox.Items[index] as string;
                if (IsRoiMenuItem(itemText))
                {
                    functionListBox.Items.RemoveAt(index);
                }
            }

            expandedRoiText = null;
        }

        private void RebuildVisibleRoiItems()
        {
            if (!roiMenuExpanded)
            {
                return;
            }

            RemoveRoiSubMenuItemsFromListBox();

            int insertIndex = functionListBox.Items.IndexOf(RoiMenuText) + 1;
            for (int index = 0; index < systemParameters.RoiRegions.Count; index++)
            {
                functionListBox.Items.Insert(insertIndex, CreateRoiText(index + 1));
                insertIndex++;
            }
        }

        private void ShowAllRoiOverlays()
        {
            var rois = new List<Rectangle>();
            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                rois.Add(roiRegion.Bounds);
            }

            leftOriginalDisplayControl.SetRoiOverlays(rois);
            rightOriginalDisplayControl.SetRoiOverlays(rois);
            leftProcessedDisplayControl.SetRoiOverlays(rois);
            rightProcessedDisplayControl.SetRoiOverlays(rois);
            statusLabel.Text = "目前顯示全部 ROI";
        }

        private void DeleteSelectedRoi()
        {
            int roiIndex = GetRoiIndex(expandedRoiText);
            if (roiIndex < 0 || roiIndex >= systemParameters.RoiRegions.Count)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "是否要刪除該項 ROI？",
                "刪除 ROI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                statusLabel.Text = "已取消刪除 ROI";
                return;
            }

            systemParameters.RoiRegions.RemoveAt(roiIndex);
            selectedRoiIndex = Math.Min(roiIndex, systemParameters.RoiRegions.Count - 1);
            SyncLegacyRoiFromSelectedRoi();
            SaveSystemParameters();
            RebuildVisibleRoiItems();
            ApplySelectedRoiOverlay();
            ClearProcessedPreviewImages();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已刪除 ROI " + (roiIndex + 1);
        }

        private static string CreateRoiText(int roiNumber)
        {
            return "    ROI " + roiNumber;
        }

        private static bool IsRoiMenuItem(string menuText)
        {
            if (string.IsNullOrEmpty(menuText))
            {
                return false;
            }

            string trimmedText = menuText.Trim();
            if (!trimmedText.StartsWith("ROI ", StringComparison.Ordinal))
            {
                return false;
            }

            int roiNumber;
            return int.TryParse(trimmedText.Substring("ROI ".Length), out roiNumber);
        }

        private int GetRoiIndex(string roiText)
        {
            if (!IsRoiMenuItem(roiText))
            {
                return -1;
            }

            int roiNumber;
            return int.TryParse(roiText.Trim().Substring("ROI ".Length), out roiNumber)
                ? roiNumber - 1
                : -1;
        }

        private void ToggleImageProcessingMenu()
        {
            if (imageProcessingMenuExpanded)
            {
                RemoveImageProcessingSubMenuItems();
            }
            else
            {
                int imageProcessingIndex = functionListBox.Items.IndexOf(ImageProcessingMenuText);
                if (imageProcessingIndex >= 0)
                {
                    imageProcessingMenuExpanded = true;
                    RebuildVisibleImageProcessingSteps();
                }
            }
        }

        private void AddImageProcessingStep()
        {
            RemoveImageProcessingStepCommandMenuItems();

            systemParameters.ImageProcessingSteps.Add(new ImageProcessingStepSettings
            {
                Id = Guid.NewGuid().ToString("N")
            });
            SaveSystemParameters();
            string stepText = CreateImageProcessingStepText(systemParameters.ImageProcessingSteps.Count);
            imageProcessingMenuExpanded = true;
            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = stepText;
            statusLabel.Text = "已新增" + stepText.Trim();
        }

        private void AddImageProcessingStepToGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();

            var step = new ImageProcessingStepSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = group.Id
            };
            systemParameters.ImageProcessingSteps.Add(step);
            int stepIndex = systemParameters.ImageProcessingSteps.Count - 1;

            imageProcessingMenuExpanded = true;
            expandedImageProcessingGroupIds.Add(group.Id);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();

            string selectedStepText = null;
            foreach (object item in functionListBox.Items)
            {
                string itemText = item as string;
                if (IsImageProcessingStepMenuItem(itemText) &&
                    GetImageProcessingStepIndex(itemText) == stepIndex)
                {
                    selectedStepText = itemText;
                    break;
                }
            }

            if (selectedStepText != null)
            {
                functionListBox.SelectedItem = selectedStepText;
            }

            statusLabel.Text = "已新增群組處理，請選擇處理方式";
        }

        private void ToggleImageProcessingStepMenu(string stepText)
        {
            if (expandedImageProcessingStepText == stepText)
            {
                RemoveImageProcessingStepCommandMenuItems();
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();

            int stepIndex = functionListBox.Items.IndexOf(stepText);
            if (stepIndex >= 0)
            {
                functionListBox.Items.Insert(stepIndex + 1, DeleteImageProcessingStepMenuText);
                functionListBox.Items.Insert(stepIndex + 2, MoveUpImageProcessingStepMenuText);
                functionListBox.Items.Insert(stepIndex + 3, MoveDownImageProcessingStepMenuText);
                expandedImageProcessingStepText = stepText;
            }
        }

        private void RemoveImageProcessingSubMenuItems()
        {
            RemoveImageProcessingSubMenuItemsFromListBox();
            imageProcessingMenuExpanded = false;
        }

        private void RemoveImageProcessingSubMenuItemsFromListBox()
        {
            for (int index = functionListBox.Items.Count - 1; index >= 0; index--)
            {
                string itemText = functionListBox.Items[index] as string;
                if (IsImageProcessingStepMenuItem(itemText) ||
                    IsImageProcessingGroupMenuItem(itemText))
                {
                    functionListBox.Items.RemoveAt(index);
                }
            }

            visibleImageProcessingStepIds.Clear();
            visibleImageProcessingGroupIds.Clear();
        }

        private void RemoveImageProcessingStepCommandMenuItems()
        {
            functionListBox.Items.Remove(DeleteImageProcessingStepMenuText);
            functionListBox.Items.Remove(MoveUpImageProcessingStepMenuText);
            functionListBox.Items.Remove(MoveDownImageProcessingStepMenuText);
            expandedImageProcessingStepText = null;
        }

        private void HandleImageProcessingStepCommand(string selectedFunction)
        {
            if (string.IsNullOrEmpty(expandedImageProcessingStepText))
            {
                return;
            }

            if (selectedFunction == DeleteImageProcessingStepMenuText)
            {
                DeleteImageProcessingStep();
            }
            else if (selectedFunction == MoveUpImageProcessingStepMenuText)
            {
                MoveImageProcessingStep(-1);
            }
            else if (selectedFunction == MoveDownImageProcessingStepMenuText)
            {
                MoveImageProcessingStep(1);
            }
        }

        private void DeleteImageProcessingStep()
        {
            string stepText = expandedImageProcessingStepText;
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            var deletedStepIds = new HashSet<string>(StringComparer.Ordinal) { step.Id };
            if (!ConfirmDeleteWithImageRelations(
                    "處理「" + (string.IsNullOrWhiteSpace(step.DisplayName) ? step.Method : step.DisplayName) + "」",
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal),
                    deletedStepIds,
                    new HashSet<string>(StringComparer.Ordinal)))
            {
                statusLabel.Text = "已取消刪除";
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();
            functionListBox.Items.Remove(stepText);
            if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                systemParameters.ImageProcessingSteps.RemoveAt(stepIndex);
                RemoveEmptyImageProcessingGroups();
                SaveSystemParameters();
            }

            selectedImageProcessingStepIndex = Math.Min(selectedImageProcessingStepIndex, systemParameters.ImageProcessingSteps.Count - 1);
            MarkProcessedImageDirty();
            if (!HasPreviewableImageProcessingStep())
            {
                ClearProcessedPreviewImages();
            }
            else
            {
                ScheduleProcessedImageUpdateIfVisible();
            }

            RebuildVisibleImageProcessingSteps();
            statusLabel.Text = "已刪除" + stepText.Trim();
        }

        private void MoveImageProcessingStep(int direction)
        {
            string stepText = expandedImageProcessingStepText;
            RemoveImageProcessingStepCommandMenuItems();

            int stepIndex = GetImageProcessingStepIndex(stepText);
            int targetIndex = stepIndex + direction;
            if (stepIndex < 0 || targetIndex < 0 || targetIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                statusLabel.Text = stepText.Trim() + (direction < 0 ? " 目前已在最上方" : " 目前已在最下方");
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            systemParameters.ImageProcessingSteps.RemoveAt(stepIndex);
            systemParameters.ImageProcessingSteps.Insert(targetIndex, step);
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            string movedStepText = CreateImageProcessingStepText(targetIndex + 1);
            functionListBox.SelectedItem = movedStepText;
            statusLabel.Text = "已移動" + movedStepText.Trim();
        }

        private void RebuildVisibleImageProcessingSteps()
        {
            if (!imageProcessingMenuExpanded)
            {
                return;
            }

            RemoveImageProcessingStepCommandMenuItems();
            RemoveImageProcessingSubMenuItemsFromListBox();

            int insertIndex = functionListBox.Items.IndexOf(ImageProcessingMenuText) + 1;
            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[index];
                if (string.IsNullOrWhiteSpace(step.GroupId))
                {
                    functionListBox.Items.Insert(
                        insertIndex,
                        RegisterVisibleImageProcessingStep(step, index + 1, 0));
                    insertIndex++;
                }
            }

            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.IsNullOrWhiteSpace(group.ParentGroupId))
                {
                    InsertImageProcessingGroup(group, 0, ref insertIndex);
                }
            }
        }

        private void InsertImageProcessingGroup(ImageProcessingGroupSettings group, int depth, ref int insertIndex)
        {
            functionListBox.Items.Insert(
                insertIndex,
                RegisterVisibleImageProcessingGroup(group, depth));
            insertIndex++;
            if (!expandedImageProcessingGroupIds.Contains(group.Id))
            {
                return;
            }

            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                if (string.Equals(systemParameters.ImageProcessingSteps[index].GroupId, group.Id, StringComparison.Ordinal))
                {
                    functionListBox.Items.Insert(
                        insertIndex,
                        RegisterVisibleImageProcessingStep(
                            systemParameters.ImageProcessingSteps[index],
                            index + 1,
                            depth + 1));
                    insertIndex++;
                }
            }

            foreach (ImageProcessingGroupSettings childGroup in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    InsertImageProcessingGroup(childGroup, depth + 1, ref insertIndex);
                }
            }
        }

        private ImageProcessingGroupSettings FindImageProcessingGroup(string groupId)
        {
            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(group.Id, groupId, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }

        private void ToggleImageProcessingGroup(string groupText)
        {
            string groupId = GetImageProcessingGroupId(groupText);
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            if (!expandedImageProcessingGroupIds.Remove(groupId))
            {
                expandedImageProcessingGroupIds.Add(groupId);
            }

            RebuildVisibleImageProcessingSteps();
            functionListBox.SelectedItem = groupText;
        }

        private static string CreateImageProcessingGroupText(ImageProcessingGroupSettings group, int depth)
        {
            string displayName = string.IsNullOrWhiteSpace(group.DisplayName) ? "未命名群組" : group.DisplayName;
            return new string(' ', 4 + (depth * 2)) + "群組(" + displayName + ")";
        }

        private static string CreateImageProcessingGroupText(ImageProcessingGroupSettings group)
        {
            return CreateImageProcessingGroupText(group, 0);
        }

        private string GetImageProcessingGroupId(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return null;
            }

            string groupId;
            if (visibleImageProcessingGroupIds.TryGetValue(menuText, out groupId) &&
                FindImageProcessingGroup(groupId) != null)
            {
                return groupId;
            }

            string trimmedText = menuText.Trim();
            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
            {
                if (string.Equals(trimmedText, CreateImageProcessingGroupText(group).Trim(), StringComparison.Ordinal))
                {
                    return group.Id;
                }
            }

            return null;
        }

        private static bool IsImageProcessingGroupMenuItem(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return false;
            }

            string trimmedText = menuText.Trim();
            return trimmedText.StartsWith("群組(", StringComparison.Ordinal) &&
                trimmedText.EndsWith(")", StringComparison.Ordinal);
        }

        private int GetImageProcessingStepIndex(string stepText)
        {
            string stepId;
            if (!string.IsNullOrWhiteSpace(stepText) &&
                visibleImageProcessingStepIds.TryGetValue(stepText, out stepId))
            {
                int mappedIndex = systemParameters.ImageProcessingSteps.FindIndex(
                    step => string.Equals(step.Id, stepId, StringComparison.Ordinal));
                if (mappedIndex >= 0)
                {
                    return mappedIndex;
                }
            }

            if (!IsImageProcessingStepMenuItem(stepText))
            {
                return -1;
            }

            string trimmedText = stepText.Trim();
            int prefixLength = "處理".Length;
            int suffixIndex = trimmedText.IndexOf('(');
            if (suffixIndex <= prefixLength)
            {
                return -1;
            }

            int stepNumber;
            return int.TryParse(trimmedText.Substring(prefixLength, suffixIndex - prefixLength), out stepNumber)
                ? stepNumber - 1
                : -1;
        }

        private string RegisterVisibleImageProcessingGroup(
            ImageProcessingGroupSettings group,
            int depth)
        {
            string text = CreateImageProcessingGroupText(group, depth);
            if (group != null && !string.IsNullOrWhiteSpace(group.Id))
            {
                visibleImageProcessingGroupIds[text] = group.Id;
            }

            return text;
        }

        private string CreateImageProcessingStepText(int stepNumber)
        {
            return CreateImageProcessingStepText(stepNumber, false);
        }

        private string CreateImageProcessingStepText(int stepNumber, bool grouped)
        {
            return CreateImageProcessingStepText(stepNumber, grouped ? 1 : 0);
        }

        private string CreateImageProcessingStepText(int stepNumber, int depth)
        {
            string displayName = string.Empty;
            int stepIndex = stepNumber - 1;
            if (stepIndex >= 0 && stepIndex < systemParameters.ImageProcessingSteps.Count)
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
                displayName = string.IsNullOrWhiteSpace(step.DisplayName) ? step.Method : step.DisplayName;
            }

            string stepName = string.IsNullOrWhiteSpace(displayName) ? "未決定" : displayName;
            return new string(' ', 4 + (Math.Max(0, depth) * 2)) + "處理" + stepNumber + "(" + stepName + ")";
        }

        private void RenameImageProcessingStep(string stepText)
        {
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
            string name;
            if (!TryGetImageProcessingStepName(step.DisplayName, out name))
            {
                return;
            }

            step.DisplayName = name;
            SaveSystemParameters();
            RebuildVisibleImageProcessingSteps();
            string renamedStepText = CreateImageProcessingStepText(stepIndex + 1);
            functionListBox.SelectedItem = renamedStepText;
            statusLabel.Text = "已命名" + renamedStepText.Trim();
        }

        private bool TryGetImageProcessingStepName(string currentName, out string name)
        {
            name = currentName ?? string.Empty;
            using (var dialog = new Form())
            using (var nameBox = new TextBox())
            using (var confirmButton = new Button())
            using (var cancelButton = new Button())
            using (var prompt = new Label())
            {
                dialog.Text = "命名影像處理";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 116);

                prompt.Text = "處理名稱";
                prompt.Location = new Point(14, 16);
                prompt.AutoSize = true;
                nameBox.Location = new Point(14, 38);
                nameBox.Size = new Size(332, 23);
                nameBox.Text = name;
                nameBox.SelectAll();
                confirmButton.Text = "確定";
                confirmButton.DialogResult = DialogResult.OK;
                confirmButton.Location = new Point(190, 76);
                cancelButton.Text = "取消";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(271, 76);
                dialog.AcceptButton = confirmButton;
                dialog.CancelButton = cancelButton;
                dialog.Controls.Add(prompt);
                dialog.Controls.Add(nameBox);
                dialog.Controls.Add(confirmButton);
                dialog.Controls.Add(cancelButton);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                name = nameBox.Text.Trim();
                return true;
            }
        }

    }
}
