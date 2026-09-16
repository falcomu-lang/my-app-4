using System;
using System.Drawing;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private bool objectJudgementMenuExpanded;
        private Panel objectJudgementParameterPanel;
        private ImageDisplayControl leftBlockProcessingDisplayControl;
        private ImageDisplayControl rightBlockProcessingDisplayControl;

        private void BlockProcessingDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            if (GetSelectedObjectJudgementIndex() < 0)
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

        private static string CreateObjectJudgementProcessingText(int processingNumber)
        {
            return "        處理" + processingNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

        private void ToggleObjectJudgementMenu()
        {
            objectJudgementMenuExpanded = !objectJudgementMenuExpanded;
            RebuildVisibleObjectJudgements();
            statusLabel.Text = objectJudgementMenuExpanded ? "已展開整合成區塊" : "已收合整合成區塊";
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
                functionListBox.Items.Insert(insertIndex++, "    " + GetObjectJudgementDisplayName(objectJudgement, index));
                for (int processingIndex = 0; processingIndex < objectJudgement.ProcessingSteps.Count; processingIndex++)
                {
                    ObjectJudgementProcessingSettings processing = objectJudgement.ProcessingSteps[processingIndex];
                    functionListBox.Items.Insert(
                        insertIndex++,
                        "        " + GetObjectJudgementProcessingDisplayName(processing, processingIndex));
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

        private bool TryGetObjectJudgementProcessingLocation(
            string menuText,
            out int objectIndex,
            out int processingIndex)
        {
            objectIndex = -1;
            processingIndex = -1;
            if (string.IsNullOrWhiteSpace(menuText) || !menuText.StartsWith("        ", StringComparison.Ordinal))
            {
                return false;
            }

            string name = menuText.Trim();
            for (int currentObjectIndex = 0; currentObjectIndex < systemParameters.ObjectJudgements.Count; currentObjectIndex++)
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[currentObjectIndex];
                for (int currentProcessingIndex = 0; currentProcessingIndex < objectJudgement.ProcessingSteps.Count; currentProcessingIndex++)
                {
                    if (string.Equals(
                        GetObjectJudgementProcessingDisplayName(
                            objectJudgement.ProcessingSteps[currentProcessingIndex],
                            currentProcessingIndex),
                        name,
                        StringComparison.Ordinal))
                    {
                        objectIndex = currentObjectIndex;
                        processingIndex = currentProcessingIndex;
                        return true;
                    }
                }
            }

            return false;
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

            StartObjectJudgementProcessing(objectIndex);
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

            StartObjectJudgementProcessing(objectIndex);
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
                activeImageRelationGroupId = relation.ProcessingId;
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

            ActivateObjectJudgementRelation(systemParameters.ObjectJudgements[objectIndex]);
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
            functionListBox.SelectedItem = "    " + GetObjectJudgementDisplayName(objectJudgement, targetIndex);
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
            functionListBox.SelectedItem = "    " + objectJudgement.DisplayName;
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
            ActivateObjectJudgementRelation(objectJudgement);
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
