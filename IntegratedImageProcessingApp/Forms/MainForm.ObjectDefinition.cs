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

            // Object definitions are configured as one complete numbering flow
            // in the right parameter panel. Legacy child-processing records are
            // kept in the model for INI compatibility, but are no longer shown.
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

            if (string.IsNullOrWhiteSpace(definition.SourceId) &&
                definition.ObjectJudgementIds.Count > 0)
            {
                // Preserve compatibility with older settings that only stored the first source ID.
                definition.SourceType = "ObjectJudgement";
                definition.SourceId = definition.ObjectJudgementIds.FirstOrDefault(
                    id => systemParameters.ObjectJudgements.Any(
                        objectJudgement => string.Equals(objectJudgement.Id, id, StringComparison.Ordinal)));
            }

            bool hasSource = !string.IsNullOrWhiteSpace(definition.SourceId);
            if (hasSource && string.Equals(definition.SourceType, "Group", StringComparison.Ordinal))
            {
                hasSource = FindObjectJudgementGroup(definition.SourceId) != null;
            }
            else if (hasSource)
            {
                hasSource = systemParameters.ObjectJudgements.Any(
                    objectJudgement => string.Equals(objectJudgement.Id, definition.SourceId, StringComparison.Ordinal));
            }

            if (!hasSource)
            {
                statusLabel.Text = definition.DisplayName + " 尚未設定來源區塊";
                return;
            }

            StartObjectDefinitionProcessing(definition.Id);
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
                Id = string.Empty,
                SourceType = string.Empty
            });
            for (int index = 0; index < systemParameters.ObjectJudgements.Count; index++)
            {
                ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[index];
                source.Items.Add(new ObjectDefinitionSourceChoice
                {
                    DisplayText = GetObjectJudgementDisplayName(objectJudgement, index),
                    Id = objectJudgement.Id,
                    SourceType = "ObjectJudgement"
                });
            }

            for (int index = 0; index < systemParameters.ObjectJudgementGroups.Count; index++)
            {
                ObjectJudgementGroupSettings group = systemParameters.ObjectJudgementGroups[index];
                source.Items.Add(new ObjectDefinitionSourceChoice
                {
                    DisplayText = "群組：" + CreateObjectJudgementGroupText(group, 0).Trim(),
                    Id = group.Id,
                    SourceType = "Group"
                });
            }

            string currentSourceType = string.IsNullOrWhiteSpace(definition.SourceType)
                ? "ObjectJudgement"
                : definition.SourceType;
            string currentSourceId = definition.SourceId;
            if (string.IsNullOrWhiteSpace(currentSourceId))
            {
                currentSourceId = definition.ObjectJudgementIds.FirstOrDefault();
            }

            source.SelectedIndex = 0;
            for (int index = 0; index < source.Items.Count; index++)
            {
                ObjectDefinitionSourceChoice choice = source.Items[index] as ObjectDefinitionSourceChoice;
                if (choice != null &&
                    string.Equals(choice.Id, currentSourceId, StringComparison.Ordinal) &&
                    string.Equals(choice.SourceType, currentSourceType, StringComparison.Ordinal))
                {
                    source.SelectedIndex = index;
                    break;
                }
            }
            panel.Controls.Add(source);

            panel.Controls.Add(new Label
            {
                Text = "連通方式",
                Left = 8,
                Top = 78,
                Width = 250
            });

            var connectivity = new ComboBox
            {
                Left = 8,
                Top = 100,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            connectivity.Items.Add(new ObjectDefinitionOption("4-連通", 4));
            connectivity.Items.Add(new ObjectDefinitionOption("8-連通", 8));
            connectivity.SelectedIndex = definition.Connectivity == 4 ? 0 : 1;
            panel.Controls.Add(connectivity);

            panel.Controls.Add(new Label
            {
                Text = "最小面積（0 表示不限）",
                Left = 8,
                Top = 134,
                Width = 250
            });
            var minArea = CreateObjectDefinitionNumberBox(definition.MinArea, 8, 156, parameterPanel.Width - 18);
            panel.Controls.Add(minArea);

            panel.Controls.Add(new Label
            {
                Text = "最大面積（0 表示不限）",
                Left = 8,
                Top = 190,
                Width = 250
            });
            var maxArea = CreateObjectDefinitionNumberBox(definition.MaxArea, 8, 212, parameterPanel.Width - 18);
            panel.Controls.Add(maxArea);

            panel.Controls.Add(new Label
            {
                Text = "編號順序",
                Left = 8,
                Top = 246,
                Width = 250
            });
            var numberingOrder = new ComboBox
            {
                Left = 8,
                Top = 268,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            numberingOrder.Items.Add(new ObjectDefinitionOption("由上到下、由左到右", "TopToBottomLeftToRight"));
            numberingOrder.Items.Add(new ObjectDefinitionOption("由左到右、由上到下", "LeftToRightTopToBottom"));
            numberingOrder.Items.Add(new ObjectDefinitionOption("面積由大到小", "AreaDescending"));
            numberingOrder.Items.Add(new ObjectDefinitionOption("面積由小到大", "AreaAscending"));
            SelectObjectDefinitionOption(numberingOrder, definition.NumberingOrder, "TopToBottomLeftToRight");
            panel.Controls.Add(numberingOrder);

            panel.Controls.Add(new Label
            {
                Text = "物件連結方式",
                Left = 8,
                Top = 302,
                Width = 250
            });
            var mergeMethod = new ComboBox
            {
                Left = 8,
                Top = 324,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            mergeMethod.Items.Add(new ObjectDefinitionOption("不合併", "None"));
            mergeMethod.Items.Add(new ObjectDefinitionOption("依距離合併（Merge by Distance）", "Distance"));
            SelectObjectDefinitionOption(mergeMethod, definition.MergeMethod, "None");
            panel.Controls.Add(mergeMethod);

            panel.Controls.Add(new Label
            {
                Text = "最大合併距離（pixels）",
                Left = 8,
                Top = 358,
                Width = 250
            });
            var mergeDistance = CreateObjectDefinitionNumberBox(
                definition.MaxMergeDistance,
                8,
                380,
                parameterPanel.Width - 18);
            mergeDistance.Maximum = 10000;
            mergeDistance.Enabled = string.Equals(definition.MergeMethod, "Distance", StringComparison.Ordinal);
            panel.Controls.Add(mergeDistance);
            mergeMethod.SelectedIndexChanged += delegate
            {
                ObjectDefinitionOption option = mergeMethod.SelectedItem as ObjectDefinitionOption;
                mergeDistance.Enabled = option != null &&
                    string.Equals(Convert.ToString(option.Value, CultureInfo.InvariantCulture), "Distance", StringComparison.Ordinal);
            };

            panel.Controls.Add(new Label
            {
                Text = "黃框線寬度（pixels）",
                Left = 8,
                Top = 414,
                Width = 250
            });
            var resultBoxLineWidth = CreateObjectDefinitionNumberBox(
                definition.ResultBoxLineWidth,
                8,
                436,
                parameterPanel.Width - 18);
            resultBoxLineWidth.Maximum = 20;
            resultBoxLineWidth.Value = Math.Max(1, Math.Min(resultBoxLineWidth.Maximum, definition.ResultBoxLineWidth));
            panel.Controls.Add(resultBoxLineWidth);

            panel.Controls.Add(new Label
            {
                Text = "編號文字大小（points）",
                Left = 8,
                Top = 470,
                Width = 250
            });
            var resultNumberFontSize = CreateObjectDefinitionNumberBox(
                definition.ResultNumberFontSize,
                8,
                492,
                parameterPanel.Width - 18);
            resultNumberFontSize.Maximum = 72;
            resultNumberFontSize.Value = Math.Max(6, Math.Min(resultNumberFontSize.Maximum, definition.ResultNumberFontSize));
            panel.Controls.Add(resultNumberFontSize);

            var measurementNote = new Label
            {
                Text = "量測來源固定使用原始二值影像；連結設定只影響物件編號，不修改量測邊界。",
                Left = 8,
                Top = 534,
                Width = parameterPanel.Width - 18,
                Height = 42,
                AutoEllipsis = false
            };
            panel.Controls.Add(measurementNote);

            var apply = new Button
            {
                Text = "套用",
                Left = 8,
                Top = 586,
                Width = parameterPanel.Width - 18
            };
            apply.Click += delegate
            {
                ObjectDefinitionSourceChoice choice = source.SelectedItem as ObjectDefinitionSourceChoice;
                ObjectDefinitionOption connectivityOption = connectivity.SelectedItem as ObjectDefinitionOption;
                ObjectDefinitionOption orderOption = numberingOrder.SelectedItem as ObjectDefinitionOption;
                ObjectDefinitionOption mergeOption = mergeMethod.SelectedItem as ObjectDefinitionOption;

                definition.SourceType = choice == null ? string.Empty : choice.SourceType;
                definition.SourceId = choice == null ? string.Empty : choice.Id;
                definition.ObjectJudgementIds.Clear();
                if (choice != null && string.Equals(choice.SourceType, "ObjectJudgement", StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(choice.Id))
                {
                    definition.ObjectJudgementIds.Add(choice.Id);
                }

                definition.Connectivity = connectivityOption == null ? 8 : (int)connectivityOption.Value;
                definition.MinArea = (double)minArea.Value;
                definition.MaxArea = (double)maxArea.Value;
                definition.NumberingOrder = orderOption == null
                    ? "TopToBottomLeftToRight"
                    : Convert.ToString(orderOption.Value, CultureInfo.InvariantCulture);
                definition.MergeMethod = mergeOption == null ? "None" : Convert.ToString(mergeOption.Value, CultureInfo.InvariantCulture);
                definition.MaxMergeDistance = definition.MergeMethod == "Distance"
                    ? (int)mergeDistance.Value
                    : 0;
                definition.ResultBoxLineWidth = Math.Max(1, Math.Min(20, (int)resultBoxLineWidth.Value));
                definition.ResultNumberFontSize = Math.Max(6, Math.Min(72, (int)resultNumberFontSize.Value));

                if (definition.MaxArea > 0 && definition.MaxArea < definition.MinArea)
                {
                    definition.MaxArea = definition.MinArea;
                }

                SaveSystemParameters();
                InvalidateObjectDefinitionDisplayOnly(definition.Id);
                statusLabel.Text = "已套用" + definition.DisplayName + "設定，等待物件編號處理";
            };
            panel.Controls.Add(apply);

            var cancel = new Button
            {
                Text = "取消",
                Left = 8,
                Top = 622,
                Width = parameterPanel.Width - 18
            };
            cancel.Click += delegate
            {
                ShowObjectDefinitionParameterPanel(definition.Id);
                statusLabel.Text = "已取消" + definition.DisplayName + "的修改";
            };
            panel.Controls.Add(cancel);

            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            rightPanelTitleLabel.Text = definition.DisplayName + " 參數";
        }

        private static NumericUpDown CreateObjectDefinitionNumberBox(
            double value,
            int left,
            int top,
            int width)
        {
            var numberBox = new NumericUpDown
            {
                Left = left,
                Top = top,
                Width = width,
                Minimum = 0,
                Maximum = 2000000000,
                DecimalPlaces = 0,
                Increment = 1,
                ThousandsSeparator = true
            };
            numberBox.Value = Math.Max(
                numberBox.Minimum,
                Math.Min(numberBox.Maximum, (decimal)Math.Max(0, value)));
            return numberBox;
        }

        private static void SelectObjectDefinitionOption(
            ComboBox comboBox,
            string selectedValue,
            string defaultValue)
        {
            string value = string.IsNullOrWhiteSpace(selectedValue) ? defaultValue : selectedValue;
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                ObjectDefinitionOption option = comboBox.Items[index] as ObjectDefinitionOption;
                if (option != null && string.Equals(Convert.ToString(option.Value, CultureInfo.InvariantCulture), value, StringComparison.Ordinal))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
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
            public string SourceType { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class ObjectDefinitionOption
        {
            public ObjectDefinitionOption(string displayText, object value)
            {
                DisplayText = displayText;
                Value = value;
            }

            public string DisplayText { get; private set; }

            public object Value { get; private set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }
    }
}
