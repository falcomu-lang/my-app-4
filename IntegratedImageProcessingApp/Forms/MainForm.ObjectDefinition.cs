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
        private bool objectDefinitionMenuExpanded;
        private bool isRebuildingObjectDefinitionMenu;
        private readonly HashSet<string> expandedObjectDefinitionIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> visibleObjectDefinitionIds =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> visibleObjectDefinitionProcessingIds =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> visibleObjectDefinitionProcessingOwnerIds =
            new Dictionary<int, string>();
        private Panel objectDefinitionParameterPanel;

        private static string CreateObjectDefinitionText(int definitionNumber)
        {
            return "    物件組" + definitionNumber.ToString(CultureInfo.InvariantCulture);
        }

        private static string CreateObjectDefinitionProcessingText(int processingNumber)
        {
            return "        處理" + processingNumber.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetObjectDefinitionDisplayName(
            ObjectDefinitionSettings definition,
            int definitionIndex)
        {
            return string.IsNullOrWhiteSpace(definition.DisplayName)
                ? "物件組" + (definitionIndex + 1).ToString(CultureInfo.InvariantCulture)
                : definition.DisplayName.Trim();
        }

        private static string GetObjectDefinitionProcessingDisplayName(
            ObjectDefinitionProcessingSettings processing,
            int processingIndex)
        {
            return string.IsNullOrWhiteSpace(processing.DisplayName)
                ? "處理" + (processingIndex + 1).ToString(CultureInfo.InvariantCulture)
                : processing.DisplayName.Trim();
        }

        private ObjectDefinitionSettings FindObjectDefinition(string definitionId)
        {
            return systemParameters.ObjectDefinitions.Find(
                definition => string.Equals(definition.Id, definitionId, StringComparison.Ordinal));
        }

        private string GetObjectDefinitionId(int visibleIndex, string menuText)
        {
            string id;
            if (visibleObjectDefinitionIds.TryGetValue(visibleIndex, out id))
            {
                return id;
            }

            string name = menuText == null ? string.Empty : menuText.Trim();
            for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
            {
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                if (string.Equals(
                        GetObjectDefinitionDisplayName(definition, index),
                        name,
                        StringComparison.Ordinal))
                {
                    return definition.Id;
                }
            }

            return null;
        }

        private string GetObjectDefinitionProcessingId(int visibleIndex)
        {
            string id;
            return visibleObjectDefinitionProcessingIds.TryGetValue(visibleIndex, out id) ? id : null;
        }

        private int GetObjectDefinitionIndex(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return -1;
            }

            string name = menuText.Trim();
            for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
            {
                if (string.Equals(
                    GetObjectDefinitionDisplayName(systemParameters.ObjectDefinitions[index], index),
                    name,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private int GetObjectDefinitionIndex(int visibleIndex, string menuText)
        {
            string id = GetObjectDefinitionId(visibleIndex, menuText);
            return string.IsNullOrEmpty(id)
                ? -1
                : systemParameters.ObjectDefinitions.FindIndex(
                    definition => string.Equals(definition.Id, id, StringComparison.Ordinal));
        }

        private bool TryGetObjectDefinitionProcessingLocation(
            int visibleIndex,
            out string definitionId,
            out int processingIndex)
        {
            definitionId = null;
            processingIndex = -1;
            string processingId = GetObjectDefinitionProcessingId(visibleIndex);
            if (string.IsNullOrEmpty(processingId) ||
                !visibleObjectDefinitionProcessingOwnerIds.TryGetValue(visibleIndex, out definitionId))
            {
                return false;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                definitionId = null;
                return false;
            }

            processingIndex = definition.ProcessingSteps.FindIndex(
                processing => string.Equals(processing.Id, processingId, StringComparison.Ordinal));
            if (processingIndex < 0)
            {
                definitionId = null;
            }

            return processingIndex >= 0;
        }

        private void ToggleObjectDefinitionMenu()
        {
            objectDefinitionMenuExpanded = !objectDefinitionMenuExpanded;
            RebuildVisibleObjectDefinitions();
            statusLabel.Text = objectDefinitionMenuExpanded
                ? "已展開物件定義"
                : "已收合物件定義";
        }

        private void ToggleObjectDefinition(string definitionId)
        {
            if (FindObjectDefinition(definitionId) == null)
            {
                return;
            }

            if (!expandedObjectDefinitionIds.Remove(definitionId))
            {
                expandedObjectDefinitionIds.Add(definitionId);
            }

            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionItem(definitionId);
        }

        private void AddObjectDefinition()
        {
            var definition = new ObjectDefinitionSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "物件組" +
                    (systemParameters.ObjectDefinitions.Count + 1).ToString(CultureInfo.InvariantCulture)
            };
            systemParameters.ObjectDefinitions.Add(definition);
            objectDefinitionMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionItem(definition.Id);
            statusLabel.Text = "已新增" + definition.DisplayName + "，請選擇來源區塊";
        }

        private void RebuildVisibleObjectDefinitions()
        {
            bool wasRebuilding = isRebuildingObjectDefinitionMenu;
            isRebuildingObjectDefinitionMenu = true;
            try
            {
                int menuIndex = functionListBox.Items.IndexOf(ObjectDefinitionMenuText);
                if (menuIndex < 0)
                {
                    return;
                }

                visibleObjectDefinitionIds.Clear();
                visibleObjectDefinitionProcessingIds.Clear();
                visibleObjectDefinitionProcessingOwnerIds.Clear();
                int removeIndex = menuIndex + 1;
                while (removeIndex < functionListBox.Items.Count)
                {
                    string text = functionListBox.Items[removeIndex] as string;
                    if (string.IsNullOrEmpty(text) || !text.StartsWith("    ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    functionListBox.Items.RemoveAt(removeIndex);
                }

                if (!objectDefinitionMenuExpanded)
                {
                    return;
                }

                int insertIndex = menuIndex + 1;
                for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
                {
                    InsertVisibleObjectDefinition(
                        systemParameters.ObjectDefinitions[index],
                        index,
                        ref insertIndex);
                }
            }
            finally
            {
                isRebuildingObjectDefinitionMenu = wasRebuilding;
            }
        }

        private void InsertVisibleObjectDefinition(
            ObjectDefinitionSettings definition,
            int definitionIndex,
            ref int insertIndex)
        {
            int definitionVisibleIndex = insertIndex;
            functionListBox.Items.Insert(
                insertIndex++,
                "    " + GetObjectDefinitionDisplayName(definition, definitionIndex));
            visibleObjectDefinitionIds[definitionVisibleIndex] = definition.Id;

            if (!expandedObjectDefinitionIds.Contains(definition.Id))
            {
                return;
            }

            for (int processingIndex = 0;
                processingIndex < definition.ProcessingSteps.Count;
                processingIndex++)
            {
                ObjectDefinitionProcessingSettings processing = definition.ProcessingSteps[processingIndex];
                int processingVisibleIndex = insertIndex;
                functionListBox.Items.Insert(
                    insertIndex++,
                    "        " + GetObjectDefinitionProcessingDisplayName(processing, processingIndex));
                visibleObjectDefinitionProcessingIds[processingVisibleIndex] = processing.Id;
                visibleObjectDefinitionProcessingOwnerIds[processingVisibleIndex] = definition.Id;
            }
        }

        private void SelectObjectDefinitionItem(string definitionId)
        {
            foreach (KeyValuePair<int, string> item in visibleObjectDefinitionIds)
            {
                if (string.Equals(item.Value, definitionId, StringComparison.Ordinal))
                {
                    functionListBox.SelectedIndex = item.Key;
                    return;
                }
            }
        }

        private void SelectObjectDefinitionProcessingItem(string processingId)
        {
            foreach (KeyValuePair<int, string> item in visibleObjectDefinitionProcessingIds)
            {
                if (string.Equals(item.Value, processingId, StringComparison.Ordinal))
                {
                    functionListBox.SelectedIndex = item.Key;
                    return;
                }
            }
        }

        private void ShowObjectDefinitionMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增物件組", null, delegate { AddObjectDefinition(); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDefinitionItemContextMenu(string definitionId, Point location)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessObjectDefinition(definition.Id); });
            menu.Items.Add("上移", null, delegate { MoveObjectDefinition(definition.Id, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectDefinition(definition.Id, 1); });
            menu.Items.Add("命名", null, delegate { RenameObjectDefinition(definition.Id); });
            menu.Items.Add("新增處理", null, delegate { AddObjectDefinitionProcessing(definition.Id); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectDefinition(definition.Id); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDefinitionProcessingContextMenu(
            string definitionId,
            string processingId,
            Point location)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            ObjectDefinitionProcessingSettings processing = definition.ProcessingSteps.FirstOrDefault(
                item => string.Equals(item.Id, processingId, StringComparison.Ordinal));
            if (processing == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate
            {
                ProcessObjectDefinitionProcessing(definition.Id, processing.Id);
            });
            menu.Items.Add("上移", null, delegate
            {
                MoveObjectDefinitionProcessing(definition.Id, processing.Id, -1);
            });
            menu.Items.Add("下移", null, delegate
            {
                MoveObjectDefinitionProcessing(definition.Id, processing.Id, 1);
            });
            menu.Items.Add("命名", null, delegate
            {
                RenameObjectDefinitionProcessing(definition.Id, processing.Id);
            });
            menu.Items.Add("刪除", null, delegate
            {
                DeleteObjectDefinitionProcessing(definition.Id, processing.Id);
            });
            menu.Show(functionListBox, location);
        }

        private void ProcessObjectDefinition(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            string sourceId = definition.ObjectJudgementIds.FirstOrDefault(
                id => systemParameters.ObjectJudgements.Any(
                    objectJudgement => string.Equals(objectJudgement.Id, id, StringComparison.Ordinal)));
            if (string.IsNullOrEmpty(sourceId))
            {
                statusLabel.Text = definition.DisplayName + " 尚未設定來源區塊";
                return;
            }

            statusLabel.Text = definition.DisplayName + " 已開始處理，來源為" + GetObjectJudgementDisplayNameById(sourceId);
        }

        private void ProcessObjectDefinitionProcessing(string definitionId, string processingId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            ObjectDefinitionProcessingSettings processing = definition == null
                ? null
                : definition.ProcessingSteps.FirstOrDefault(
                    item => string.Equals(item.Id, processingId, StringComparison.Ordinal));
            if (processing == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(processing.Method))
            {
                statusLabel.Text = definition.DisplayName + " 的" + processing.DisplayName + " 尚未設定處理方式";
                return;
            }

            statusLabel.Text = definition.DisplayName + " 的" + processing.DisplayName + " 已開始處理";
        }

        private string GetObjectJudgementDisplayNameById(string objectId)
        {
            int index = systemParameters.ObjectJudgements.FindIndex(
                objectJudgement => string.Equals(objectJudgement.Id, objectId, StringComparison.Ordinal));
            return index < 0
                ? "未知區塊"
                : GetObjectJudgementDisplayName(systemParameters.ObjectJudgements[index], index);
        }

        private void MoveObjectDefinition(string definitionId, int direction)
        {
            int index = systemParameters.ObjectDefinitions.FindIndex(
                definition => string.Equals(definition.Id, definitionId, StringComparison.Ordinal));
            int target = index + direction;
            if (index < 0 || target < 0 || target >= systemParameters.ObjectDefinitions.Count)
            {
                return;
            }

            ObjectDefinitionSettings objectDefinition = systemParameters.ObjectDefinitions[index];
            systemParameters.ObjectDefinitions.RemoveAt(index);
            systemParameters.ObjectDefinitions.Insert(target, objectDefinition);
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionItem(definitionId);
        }

        private void RenameObjectDefinition(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            string name = PromptForText("命名物件組", "物件組名稱", definition.DisplayName);
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            definition.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionItem(definitionId);
            statusLabel.Text = "已命名物件組" + definition.DisplayName;
        }

        private void DeleteObjectDefinition(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "確定要刪除「" + definition.DisplayName + "」嗎？",
                    "刪除物件組",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            systemParameters.ObjectDefinitions.Remove(definition);
            expandedObjectDefinitionIds.Remove(definitionId);
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            statusLabel.Text = "已刪除物件組";
        }

        private void AddObjectDefinitionProcessing(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            var processing = new ObjectDefinitionProcessingSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "處理" +
                    (definition.ProcessingSteps.Count + 1).ToString(CultureInfo.InvariantCulture),
                Method = string.Empty,
                Parameters = string.Empty
            };
            definition.ProcessingSteps.Add(processing);
            expandedObjectDefinitionIds.Add(definition.Id);
            objectDefinitionMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionProcessingItem(processing.Id);
            statusLabel.Text = "已新增" + processing.DisplayName + "至" + definition.DisplayName;
        }

        private void MoveObjectDefinitionProcessing(string definitionId, string processingId, int direction)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            int index = definition.ProcessingSteps.FindIndex(
                processing => string.Equals(processing.Id, processingId, StringComparison.Ordinal));
            int target = index + direction;
            if (index < 0 || target < 0 || target >= definition.ProcessingSteps.Count)
            {
                return;
            }

            ObjectDefinitionProcessingSettings processingItem = definition.ProcessingSteps[index];
            definition.ProcessingSteps.RemoveAt(index);
            definition.ProcessingSteps.Insert(target, processingItem);
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionProcessingItem(processingId);
        }

        private void RenameObjectDefinitionProcessing(string definitionId, string processingId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            ObjectDefinitionProcessingSettings processing = definition == null
                ? null
                : definition.ProcessingSteps.FirstOrDefault(
                    item => string.Equals(item.Id, processingId, StringComparison.Ordinal));
            if (processing == null)
            {
                return;
            }

            string name = PromptForText("命名物件組處理", "處理名稱", processing.DisplayName);
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            processing.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionProcessingItem(processingId);
            statusLabel.Text = "已命名" + processing.DisplayName;
        }

        private void DeleteObjectDefinitionProcessing(string definitionId, string processingId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            ObjectDefinitionProcessingSettings processing = definition.ProcessingSteps.FirstOrDefault(
                item => string.Equals(item.Id, processingId, StringComparison.Ordinal));
            if (processing == null)
            {
                return;
            }

            definition.ProcessingSteps.Remove(processing);
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            SelectObjectDefinitionItem(definitionId);
            statusLabel.Text = "已刪除" + processing.DisplayName;
        }

        private void ShowObjectDefinitionParameterPanel(string definitionId)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null)
            {
                return;
            }

            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            if (objectDefinitionParameterPanel != null)
            {
                parameterPanel.Controls.Remove(objectDefinitionParameterPanel);
            }

            parameterPlaceholderLabel.Visible = false;
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            objectDefinitionParameterPanel = panel;
            panel.Controls.Add(new Label
            {
                Text = "來源區塊",
                Left = 8,
                Top = 12,
                Width = 250
            });

            var source = new ComboBox
            {
                Left = 8,
                Top = 34,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            source.Items.Add(new ObjectDefinitionSourceChoice
            {
                DisplayText = "未指定來源區塊",
                Id = string.Empty
            });
            for (int index = 0; index < systemParameters.ObjectJudgements.Count; index++)
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[index];
                source.Items.Add(new ObjectDefinitionSourceChoice
                {
                    DisplayText = GetObjectJudgementDisplayName(objectJudgement, index),
                    Id = objectJudgement.Id
                });
            }

            string currentSourceId = definition.ObjectJudgementIds.FirstOrDefault();
            source.SelectedIndex = 0;
            for (int index = 0; index < source.Items.Count; index++)
            {
                ObjectDefinitionSourceChoice choice = source.Items[index] as ObjectDefinitionSourceChoice;
                if (choice != null && string.Equals(choice.Id, currentSourceId, StringComparison.Ordinal))
                {
                    source.SelectedIndex = index;
                    break;
                }
            }
            panel.Controls.Add(source);

            var apply = new Button
            {
                Text = "確認",
                Left = 8,
                Top = 76,
                Width = parameterPanel.Width - 18
            };
            apply.Click += delegate
            {
                ObjectDefinitionSourceChoice choice = source.SelectedItem as ObjectDefinitionSourceChoice;
                definition.ObjectJudgementIds.Clear();
                if (choice != null && !string.IsNullOrEmpty(choice.Id))
                {
                    definition.ObjectJudgementIds.Add(choice.Id);
                }

                SaveSystemParameters();
                statusLabel.Text = "已確認" + definition.DisplayName + "的來源區塊";
            };
            panel.Controls.Add(apply);
            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            rightPanelTitleLabel.Text = definition.DisplayName + " 參數";
        }

        private void ShowObjectDefinitionProcessingParameterPanel(
            string definitionId,
            int processingIndex)
        {
            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            if (definition == null ||
                processingIndex < 0 ||
                processingIndex >= definition.ProcessingSteps.Count)
            {
                return;
            }

            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.Text =
                definition.DisplayName + " 的" +
                GetObjectDefinitionProcessingDisplayName(
                    definition.ProcessingSteps[processingIndex],
                    processingIndex) +
                " 已建立，處理方式待定義。";
        }

        private void HideObjectDefinitionParameterPanel()
        {
            if (objectDefinitionParameterPanel != null)
            {
                objectDefinitionParameterPanel.Visible = false;
            }
        }

        private void NormalizeObjectDefinitionDefaultNames()
        {
            bool changed = false;
            for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
            {
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                string oldDefaultName = "物件組定義" +
                    (index + 1).ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(definition.DisplayName) ||
                    string.Equals(definition.DisplayName, oldDefaultName, StringComparison.Ordinal))
                {
                    definition.DisplayName = "物件組" +
                        (index + 1).ToString(CultureInfo.InvariantCulture);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSystemParameters();
            }
        }

        private sealed class ObjectDefinitionSourceChoice
        {
            public string DisplayText { get; set; }
            public string Id { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }
    }
}
