using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private bool objectDefinitionMenuExpanded;

        private static string CreateObjectDefinitionText(int definitionNumber)
        {
            return "    物件組定義" + definitionNumber.ToString(CultureInfo.InvariantCulture);
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
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                string displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? "物件組定義" + (index + 1).ToString(CultureInfo.InvariantCulture)
                    : definition.DisplayName.Trim();
                if (string.Equals(displayName, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private void ToggleObjectDefinitionMenu()
        {
            objectDefinitionMenuExpanded = !objectDefinitionMenuExpanded;
            RebuildVisibleObjectDefinitions();
            statusLabel.Text = objectDefinitionMenuExpanded
                ? "已展開物件定義"
                : "已收合物件定義";
        }

        private void AddObjectDefinition()
        {
            systemParameters.ObjectDefinitions.Add(new ObjectDefinitionSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "物件組定義" +
                    (systemParameters.ObjectDefinitions.Count + 1).ToString(CultureInfo.InvariantCulture)
            });
            objectDefinitionMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectDefinitions();
            functionListBox.SelectedItem = CreateObjectDefinitionText(
                systemParameters.ObjectDefinitions.Count);
            statusLabel.Text = "已新增物件組定義，尚未設定物件內容";
        }

        private void RebuildVisibleObjectDefinitions()
        {
            int menuIndex = functionListBox.Items.IndexOf(ObjectDefinitionMenuText);
            if (menuIndex < 0)
            {
                return;
            }

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
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                string displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? "物件組定義" + (index + 1).ToString(CultureInfo.InvariantCulture)
                    : definition.DisplayName.Trim();
                functionListBox.Items.Insert(insertIndex++, "    " + displayName);
            }
        }

        private void ShowObjectDefinitionMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增物件組定義", null, delegate { AddObjectDefinition(); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDefinitionPlaceholder(int definitionIndex)
        {
            if (definitionIndex < 0 || definitionIndex >= systemParameters.ObjectDefinitions.Count)
            {
                return;
            }

            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.Text =
                "目前選擇物件組定義，之後可在這裡設定要納入的區塊與物件判定規則。";
        }
    }
}
