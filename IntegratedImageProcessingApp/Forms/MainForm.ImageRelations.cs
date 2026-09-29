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
        private void ShowImageRelationMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增關聯", null, delegate { AddImageRelation(); });
            menu.Show(functionListBox, location);
        }

        private void ShowImageRelationParameterPanel(int relationIndex)
        {
            if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count) return;
            parameterPlaceholderLabel.Visible = false;
            HideImageProcessingParameterPanel();
            if (imageProcessingFlowTreeView != null) imageProcessingFlowTreeView.Visible = false;
            if (imagePreprocessingFlowTreeView != null) imagePreprocessingFlowTreeView.Visible = false;
            if (imageRelationParameterPanel != null) parameterPanel.Controls.Remove(imageRelationParameterPanel);
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            imageRelationParameterPanel = panel;
            panel.Controls.Add(new Label { Text = "來源影像", Left = 8, Top = 12, Width = 250 });
            var source = new ComboBox { Left = 8, Top = 34, Width = parameterPanel.Width - 18, DropDownStyle = ComboBoxStyle.DropDownList };
            source.Items.Add(new RelationChoice { DisplayText = "原始影像", Type = "Original", Id = string.Empty });
            for (int index = 0; index < systemParameters.ImagePreprocessingSteps.Count; index++)
            {
                ImageProcessingStepSettings step = systemParameters.ImagePreprocessingSteps[index];
                string displayName = string.IsNullOrWhiteSpace(step.DisplayName) ? "前處理" + (index + 1) : step.DisplayName;
                source.Items.Add(new RelationChoice { DisplayText = displayName + " (" + step.Method + ")", Type = "Step", Id = step.Id });
            }
            foreach (ImageProcessingGroupSettings group in systemParameters.ImagePreprocessingGroups)
                source.Items.Add(new RelationChoice { DisplayText = group.DisplayName, Type = "Group", Id = group.Id });
            source.SelectedIndex = FindRelationChoiceIndex(source, relation.SourceType, relation.SourceId);
            panel.Controls.Add(source);
            panel.Controls.Add(new Label { Text = "影像處理", Left = 8, Top = 72, Width = 250 });
            var target = new ComboBox { Left = 8, Top = 94, Width = parameterPanel.Width - 18, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int index = 0; index < systemParameters.ImageProcessingSteps.Count; index++)
            {
                ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[index];
                string displayName = string.IsNullOrWhiteSpace(step.DisplayName) ? "處理" + (index + 1) : step.DisplayName;
                target.Items.Add(new RelationChoice { DisplayText = displayName + " (" + step.Method + ")", Type = "Step", Id = step.Id });
            }
            foreach (ImageProcessingGroupSettings group in systemParameters.ImageProcessingGroups)
                target.Items.Add(new RelationChoice { DisplayText = group.DisplayName, Type = "Group", Id = group.Id });
            target.SelectedIndex = FindRelationChoiceIndex(target, relation.ProcessingType, relation.ProcessingId);
            panel.Controls.Add(target);
            var apply = new Button { Text = "套用", Left = 8, Top = 135, Width = parameterPanel.Width - 18 };
            apply.Click += delegate
            {
                SaveRelationChoice(relation, source.SelectedItem, true);
                SaveRelationChoice(relation, target.SelectedItem, false);
                SaveSystemParameters();
                statusLabel.Text = "已套用影像關聯";
            };
            panel.Controls.Add(apply);
            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
        }

        private void HideImageRelationParameterPanel()
        {
            if (imageRelationParameterPanel != null)
            {
                imageRelationParameterPanel.Visible = false;
            }
        }

        private static int FindRelationChoiceIndex(ComboBox combo, string type, string id)
        {
            for (int index = 0; index < combo.Items.Count; index++)
            {
                RelationChoice choice = combo.Items[index] as RelationChoice;
                if (choice != null && choice.Type == (type ?? string.Empty) && choice.Id == (id ?? string.Empty)) return index;
            }
            return combo.Items.Count > 0 ? 0 : -1;
        }

        private static void SaveRelationChoice(ImageRelationSettings relation, object choice, bool source)
        {
            RelationChoice selected = choice as RelationChoice;
            if (selected == null) return;
            if (source) { relation.SourceType = selected.Type; relation.SourceId = selected.Id; }
            else { relation.ProcessingType = selected.Type; relation.ProcessingId = selected.Id; }
        }

        private void AddImageRelation()
        {
            systemParameters.ImageRelations.Add(new ImageRelationSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "關聯" + (systemParameters.ImageRelations.Count + 1).ToString(CultureInfo.InvariantCulture),
                SourceType = "Original",
                SourceId = string.Empty,
                ProcessingType = "Step",
                ProcessingId = string.Empty,
                GroupId = string.Empty
            });
            SaveSystemParameters();
            imageRelationMenuExpanded = true;
            RebuildVisibleImageRelations();
            statusLabel.Text = "已新增影像關聯，請選擇來源與處理項目";
        }

        private void ToggleImageRelationMenu()
        {
            imageRelationMenuExpanded = !imageRelationMenuExpanded;
            RebuildVisibleImageRelations();
            statusLabel.Text = imageRelationMenuExpanded ? "已展開影像關聯" : "已收合影像關聯";
        }

        private void RebuildVisibleImageRelations()
        {
            int relationMenuIndex = functionListBox.Items.IndexOf(ImageRelationMenuText);
            if (relationMenuIndex < 0)
            {
                return;
            }

            int removeIndex = relationMenuIndex + 1;
            while (removeIndex < functionListBox.Items.Count)
            {
                string text = functionListBox.Items[removeIndex] as string;
                if (string.IsNullOrEmpty(text) || !text.StartsWith("    ", StringComparison.Ordinal))
                {
                    break;
                }

                functionListBox.Items.RemoveAt(removeIndex);
            }

            if (!imageRelationMenuExpanded)
            {
                return;
            }

            int insertIndex = relationMenuIndex + 1;
            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                if (!string.IsNullOrWhiteSpace(relation.GroupId))
                {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名" : relation.DisplayName;
                functionListBox.Items.Insert(insertIndex++, "    " + displayName);
            }

            foreach (ImageRelationGroupSettings group in systemParameters.ImageRelationGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.ParentGroupId))
                {
                    continue;
                }

                InsertVisibleImageRelationGroup(group, ref insertIndex, 0);
            }
        }

        private void InsertVisibleImageRelationGroup(ImageRelationGroupSettings group, ref int insertIndex, int depth)
        {
            functionListBox.Items.Insert(insertIndex++, CreateImageRelationGroupText(group, depth));
            if (!expandedImageRelationGroupIds.Contains(group.Id))
            {
                return;
            }

            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                if (string.Equals(relation.GroupId, group.Id, StringComparison.Ordinal))
                {
                    string displayName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名" : relation.DisplayName;
                    functionListBox.Items.Insert(insertIndex++, new string(' ', 6 + (depth * 2)) + displayName);
                }
            }

            foreach (ImageRelationGroupSettings child in systemParameters.ImageRelationGroups)
            {
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal))
                {
                    InsertVisibleImageRelationGroup(child, ref insertIndex, depth + 1);
                }
            }
        }

        private int GetImageRelationIndex(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("    ", StringComparison.Ordinal)) return -1;
            string name = text.Trim();
            for (int index = 0; index < systemParameters.ImageRelations.Count; index++)
            {
                if (string.Equals(systemParameters.ImageRelations[index].DisplayName, name, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private void ShowImageRelationItemContextMenu(int relationIndex, Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessImageRelation(relationIndex); });
            menu.Items.Add("上移", null, delegate { MoveImageRelation(relationIndex, -1); });
            menu.Items.Add("下移", null, delegate { MoveImageRelation(relationIndex, 1); });
            menu.Items.Add("命名", null, delegate { RenameImageRelation(relationIndex); });
            menu.Items.Add("刪除", null, delegate
            {
                if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count)
                {
                    return;
                }

                ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
                if (!ConfirmDeleteImageRelation(relation))
                {
                    return;
                }

                systemParameters.ImageRelations.RemoveAt(relationIndex);
                SaveSystemParameters();
                RebuildVisibleImageRelations();
            });
            menu.Show(functionListBox, location);
        }

        private bool ConfirmDeleteImageRelation(ImageRelationSettings relation)
        {
            if (relation == null)
            {
                return false;
            }

            var affectedObjectNames = new List<string>();
            var directlyUsingObjects = new List<ObjectJudgementSettings>();
            foreach (ObjectJudgementSettings objectJudgement in systemParameters.ObjectJudgements)
            {
                string objectName = string.IsNullOrWhiteSpace(objectJudgement.DisplayName)
                    ? "未命名區塊"
                    : objectJudgement.DisplayName;
                bool isDirectReference =
                    string.Equals(objectJudgement.RelationType, "Relation", StringComparison.Ordinal) &&
                    string.Equals(objectJudgement.RelationId, relation.Id, StringComparison.Ordinal);

                bool isGroupReference = false;
                if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
                {
                    ImageRelationGroupSettings group = FindImageRelationGroup(objectJudgement.RelationId);
                    if (group != null)
                    {
                        foreach (ImageRelationSettings groupedRelation in GetImageRelationGroupRelations(group.Id))
                        {
                            if (string.Equals(groupedRelation.Id, relation.Id, StringComparison.Ordinal))
                            {
                                isGroupReference = true;
                                break;
                            }
                        }
                    }
                }

                if (isDirectReference || isGroupReference)
                {
                    affectedObjectNames.Add(objectName);
                    if (isDirectReference)
                    {
                        directlyUsingObjects.Add(objectJudgement);
                    }
                }
            }

            string relationName = string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名關聯" : relation.DisplayName;
            string message;
            string title;
            if (affectedObjectNames.Count == 0)
            {
                message = "是否要刪除關聯「" + relationName + "」？";
                title = "刪除影像關聯";
            }
            else
            {
                message = "關聯「" + relationName + "」已被以下整合成區塊使用：\r\n" +
                    string.Join("、", affectedObjectNames.ToArray()) +
                    "\r\n\r\n刪除後，直接使用此關聯的區塊設定會被清除；使用關聯群組的區塊結果也會受到影響。\r\n是否仍要刪除？";
                title = "刪除前關聯警告";
            }

            if (MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return false;
            }

            foreach (ObjectJudgementSettings objectJudgement in directlyUsingObjects)
            {
                objectJudgement.RelationType = string.Empty;
                objectJudgement.RelationId = string.Empty;
            }

            return true;
        }

        private void MoveImageRelation(int relationIndex, int direction)
        {
            int target = relationIndex + direction;
            if (relationIndex < 0 || target < 0 || target >= systemParameters.ImageRelations.Count) return;
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            systemParameters.ImageRelations.RemoveAt(relationIndex);
            systemParameters.ImageRelations.Insert(target, relation);
            SaveSystemParameters();
            RebuildVisibleImageRelations();
        }

        private void ProcessImageRelation(int relationIndex)
        {
            if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count) return;
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            if (string.IsNullOrWhiteSpace(relation.SourceType))
            {
                statusLabel.Text = "影像關聯尚未選擇來源影像";
                return;
            }
            activeImageRelationSourceType = relation.SourceType;
            activeImageRelationSourceId = relation.SourceId;
            activeImageRelationGroupId = null;
            selectedImageRelationGroupId = null;
            preprocessingExecutionRequestedByImageRelation =
                !string.Equals(relation.SourceType, "Original", StringComparison.Ordinal);
            if (!string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
            {
                // A relation is an explicit processing request, so it may start
                // preparation of its selected preprocessing source even though
                // normal startup intentionally does not run preprocessing.
                preprocessingExecutionRequested = preprocessingExecutionRequestedByImageRelation;
            }
            else
            {
                preprocessingExecutionRequested = false;
                RestorePreprocessedDisplaysToOriginalSource();
            }
            if (string.IsNullOrWhiteSpace(relation.ProcessingId))
            {
                statusLabel.Text = "影像關聯尚未選擇影像處理項目";
                return;
            }

            if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal))
            {
                if (!systemParameters.ImageProcessingGroups.Any(group => string.Equals(group.Id, relation.ProcessingId, StringComparison.Ordinal)))
                {
                    statusLabel.Text = "影像關聯的處理群組不存在";
                    return;
                }
                ProcessImageProcessingGroup(relation.ProcessingId);
                return;
            }

            int stepIndex = systemParameters.ImageProcessingSteps.FindIndex(step => string.Equals(step.Id, relation.ProcessingId, StringComparison.Ordinal));
            if (stepIndex < 0)
            {
                statusLabel.Text = "影像關聯的處理項目不存在";
                return;
            }
            ProcessImageProcessingStep(CreateImageProcessingStepText(stepIndex + 1));
        }

        private void RenameImageRelation(int relationIndex)
        {
            if (relationIndex < 0 || relationIndex >= systemParameters.ImageRelations.Count) return;
            ImageRelationSettings relation = systemParameters.ImageRelations[relationIndex];
            string name = PromptForText("關聯名稱", "請輸入關聯名稱：", relation.DisplayName);
            if (name == null) return;
            relation.DisplayName = string.IsNullOrWhiteSpace(name) ? relation.DisplayName : name.Trim();
            SaveSystemParameters();
            RebuildVisibleImageRelations();
        }

        private string PromptForText(string title, string message, string value)
        {
            using (var dialog = new Form { Text = title, Width = 360, Height = 135, StartPosition = FormStartPosition.CenterParent })
            using (var input = new TextBox { Left = 12, Top = 30, Width = 320, Text = value ?? string.Empty })
            using (var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, Left = 176, Top = 65, Width = 75 })
            using (var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 257, Top = 65, Width = 75 })
            {
                dialog.Controls.Add(new Label { Text = message, Left = 12, Top = 8, Width = 320 });
                dialog.Controls.Add(input); dialog.Controls.Add(ok); dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
            }
        }

        private bool ConfirmDeleteWithImageRelations(
            string itemDescription,
            HashSet<string> preprocessingStepIds,
            HashSet<string> preprocessingGroupIds,
            HashSet<string> processingStepIds,
            HashSet<string> processingGroupIds)
        {
            var affectedRelations = new List<ImageRelationSettings>();
            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                bool usesDeletedPreprocessingSource =
                    string.Equals(relation.SourceType, "Step", StringComparison.Ordinal) &&
                    preprocessingStepIds.Contains(relation.SourceId) ||
                    string.Equals(relation.SourceType, "Group", StringComparison.Ordinal) &&
                    preprocessingGroupIds.Contains(relation.SourceId);
                bool usesDeletedProcessingTarget =
                    string.Equals(relation.ProcessingType, "Step", StringComparison.Ordinal) &&
                    processingStepIds.Contains(relation.ProcessingId) ||
                    string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal) &&
                    processingGroupIds.Contains(relation.ProcessingId);

                if (usesDeletedPreprocessingSource || usesDeletedProcessingTarget)
                {
                    affectedRelations.Add(relation);
                }
            }

            var relationNames = new List<string>();
            foreach (ImageRelationSettings relation in affectedRelations)
            {
                relationNames.Add("關聯「" + (string.IsNullOrWhiteSpace(relation.DisplayName) ? "未命名關聯" : relation.DisplayName) + "」");
            }

            string message;
/*
            if (affectedRelations.Count == 0)
            {
                message = "是否要刪除" + itemDescription + "？";
            }
            else
            {
                message = "" + itemDescription + " 已建立影像關聯：\r\n" +
                    string.Join("、", relationNames.ToArray()) +
\n"\r\n刪除後，受影響的關聯也會一併刪除。\r\n是否要繼續？";
            }

*/
            message = affectedRelations.Count == 0
                ? "是否要刪除" + itemDescription + "？"
                : itemDescription + " 已建立影像關聯：\r\n" +
                    string.Join("、", relationNames.ToArray()) +
                    "\r\n\r\n刪除後，受影響的關聯也會一併刪除。\r\n是否要繼續？";

            if (MessageBox.Show(
                    this,
                    message,
                    affectedRelations.Count == 0 ? "刪除確認" : "刪除前關聯確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return false;
            }

            foreach (ImageRelationSettings relation in affectedRelations)
            {
                systemParameters.ImageRelations.Remove(relation);
            }

            if (affectedRelations.Count > 0)
            {
                selectedImageRelationIndex = -1;
                activeImageRelationSourceType = "Original";
                activeImageRelationSourceId = null;
                RebuildVisibleImageRelations();
            }

            return true;
        }

    }
}
