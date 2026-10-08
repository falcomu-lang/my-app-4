using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Interlocked = System.Threading.Interlocked;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private bool isImportingDetectionParameterSettings;
        private bool suppressObjectDetectionParameterSourceAutoProcessing;
        private bool objectDetectionParameterMenuExpanded;
        private bool isRebuildingObjectDetectionParameterMenu;
        private readonly Dictionary<int, string> visibleObjectDetectionParameterIds =
            new Dictionary<int, string>();

        private static string GetObjectDetectionParameterDisplayName(
            ObjectDetectionParameterSettings parameter,
            int parameterIndex)
        {
            return string.IsNullOrWhiteSpace(parameter.DisplayName)
                ? "檢測參數" + (parameterIndex + 1).ToString(CultureInfo.InvariantCulture)
                : parameter.DisplayName.Trim();
        }

        private ObjectDetectionParameterSettings FindObjectDetectionParameter(string parameterId)
        {
            return systemParameters.ObjectDetectionParameters.FirstOrDefault(
                parameter => string.Equals(parameter.Id, parameterId, StringComparison.Ordinal));
        }

        private bool TryGetObjectDetectionParameterLocation(
            int visibleIndex,
            string menuText,
            out string parameterId)
        {
            parameterId = null;
            if (!visibleObjectDetectionParameterIds.TryGetValue(visibleIndex, out parameterId))
            {
                string name = menuText == null ? string.Empty : menuText.Trim();
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    if (string.Equals(
                        GetObjectDetectionParameterDisplayName(systemParameters.ObjectDetectionParameters[index], index),
                        name,
                        StringComparison.Ordinal))
                    {
                        parameterId = systemParameters.ObjectDetectionParameters[index].Id;
                        break;
                    }
                }
            }

            return !string.IsNullOrEmpty(parameterId) && FindObjectDetectionParameter(parameterId) != null;
        }

        private void ToggleObjectDetectionParameterMenu()
        {
            objectDetectionParameterMenuExpanded = !objectDetectionParameterMenuExpanded;
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = objectDetectionParameterMenuExpanded
                ? "已展開檢測參數設定"
                : "已收合檢測參數設定";
        }

        private void AddObjectDetectionParameter()
        {
            var parameter = new ObjectDetectionParameterSettings
            {
                DisplayName = "檢測參數" +
                    (systemParameters.ObjectDetectionParameters.Count + 1).ToString(CultureInfo.InvariantCulture),
                Parameters = string.Empty
            };
            systemParameters.ObjectDetectionParameters.Add(parameter);
            objectDetectionParameterMenuExpanded = true;
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameter.Id);
            statusLabel.Text = "已新增" + parameter.DisplayName;
        }

        private void RebuildVisibleObjectDetectionParameters()
        {
            bool wasRebuilding = isRebuildingObjectDetectionParameterMenu;
            isRebuildingObjectDetectionParameterMenu = true;
            try
            {
                int menuIndex = functionListBox.Items.IndexOf(ObjectDetectionParameterMenuText);
                if (menuIndex < 0)
                {
                    return;
                }

                visibleObjectDetectionParameterIds.Clear();
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

                if (!objectDetectionParameterMenuExpanded)
                {
                    return;
                }

                int insertIndex = menuIndex + 1;
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
                    visibleObjectDetectionParameterIds[insertIndex] = parameter.Id;
                    functionListBox.Items.Insert(
                        insertIndex++,
                        "    " + GetObjectDetectionParameterDisplayName(parameter, index));
                }
            }
            finally
            {
                isRebuildingObjectDetectionParameterMenu = wasRebuilding;
            }
        }

        private void SelectObjectDetectionParameter(string parameterId)
        {
            foreach (KeyValuePair<int, string> item in visibleObjectDetectionParameterIds)
            {
                if (string.Equals(item.Value, parameterId, StringComparison.Ordinal))
                {
                    functionListBox.SelectedIndex = item.Key;
                    return;
                }
            }
        }

        private void ShowObjectDetectionParameterMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("新增檢測參數", null, delegate { AddObjectDetectionParameter(); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDetectionParameterItemContextMenu(string parameterId, Point location)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add(
                "匯出檢測參數及關聯設定...",
                null,
                delegate { ExportObjectDetectionParameterSettings(parameterId); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("命名", null, delegate { RenameObjectDetectionParameter(parameterId); });
            menu.Items.Add("上移", null, delegate { MoveObjectDetectionParameter(parameterId, -1); });
            menu.Items.Add("下移", null, delegate { MoveObjectDetectionParameter(parameterId, 1); });
            menu.Items.Add("刪除", null, delegate { DeleteObjectDetectionParameter(parameterId); });
            menu.Show(functionListBox, location);
        }

        private void ExportObjectDetectionParameterSettings(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(parameter.DisplayName)
                ? "檢測參數"
                : parameter.DisplayName.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                displayName = displayName.Replace(invalidCharacter, '_');
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "匯出檢測參數及關聯設定";
                dialog.Filter = "檢測參數設定檔 (*.ini)|*.ini|所有檔案 (*.*)|*.*";
                dialog.DefaultExt = "ini";
                dialog.AddExtension = true;
                dialog.FileName = displayName + ".ini";
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    SystemParameterSettings exportSettings =
                        CreateObjectDetectionParameterExportSettings(parameter);
                    new SystemParameterIniService(dialog.FileName).Save(exportSettings);
                    statusLabel.Text = "已匯出「" + displayName + "」及其關聯設定";
                    MessageBox.Show(
                        this,
                        "檢測參數與相關流程設定已保存至：\r\n" + dialog.FileName,
                        "匯出完成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法保存檢測參數設定：\r\n" + exception.Message,
                        "匯出失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ImportDetectionParameterSettings()
        {
            if (isLoadingImage || parameterApplyInProgress || isPreparingPreprocessedImage ||
                imageProcessingExecutionRequested || objectJudgementProcessingRequested ||
                objectDefinitionProcessingRequested || objectDetectionDefectProcessingRequested)
            {
                MessageBox.Show(
                    this,
                    "目前仍有影像流程正在執行，請完成後再讀取檢測參數。",
                    "無法讀取",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "讀取檢測參數及關聯設定";
                dialog.Filter = "檢測參數設定檔 (*.ini)|*.ini|所有檔案 (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                SystemParameterSettings importedSettings;
                try
                {
                    importedSettings = new SystemParameterIniService(dialog.FileName).Load();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法讀取檢測參數設定：\r\n" + exception.Message,
                        "讀取失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (importedSettings == null || importedSettings.ObjectDetectionParameters.Count != 1)
                {
                    MessageBox.Show(
                        this,
                        "這個檔案必須包含且只包含一個檢測參數。\r\n請選擇由檢測參數匯出的設定檔。",
                        "檔案內容不符",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                ObjectDetectionParameterSettings importedParameter =
                    importedSettings.ObjectDetectionParameters[0];
                string parameterName = string.IsNullOrWhiteSpace(importedParameter.DisplayName)
                    ? "檢測參數"
                    : importedParameter.DisplayName.Trim();
                DialogResult confirm = MessageBox.Show(
                    this,
                    "將以「" + parameterName + "」及檔案內的相關流程設定，完整取代目前所有設定。\r\n" +
                    "匯入後只會保留這一個檢測參數；目前開啟的圖片與檢視位置會保留，且不會自動重新運算。\r\n\r\n" +
                    "確定要覆蓋嗎？",
                    "覆蓋目前設定",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }

                // The exported profile intentionally omits a machine-specific image path.
                importedSettings.LastImagePath = systemParameters.LastImagePath;
                string settingsPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SystemParameters.ini");
                string temporarySettingsPath = settingsPath + ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    new SystemParameterIniService(temporarySettingsPath).Save(importedSettings);
                    if (File.Exists(settingsPath))
                    {
                        File.Replace(temporarySettingsPath, settingsPath, null);
                    }
                    else
                    {
                        File.Move(temporarySettingsPath, settingsPath);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法套用檢測參數設定，原本設定未變更：\r\n" + exception.Message,
                        "覆蓋失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    if (File.Exists(temporarySettingsPath))
                    {
                        File.Delete(temporarySettingsPath);
                    }
                }

                ApplyImportedDetectionParameterSettings(importedSettings, importedParameter.Id);
                statusLabel.Text = "已覆蓋目前設定並載入「" + parameterName + "」；圖片未變更，尚未重新運算";
                MessageBox.Show(
                    this,
                    "已載入「" + parameterName + "」，目前流程設定已由檔案內容完整取代。\r\n" +
                    "目前圖片與檢視位置已保留；處理結果需由你手動執行更新。",
                    "讀取完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ApplyImportedDetectionParameterSettings(
            SystemParameterSettings importedSettings,
            string importedParameterId)
        {
            isImportingDetectionParameterSettings = true;
            try
            {
                functionListBox.SelectedIndex = -1;
                CloseImageProcessingStepContextMenu();
                if (roiMenuExpanded) ToggleRoiMenu();
                if (imagePreprocessingMenuExpanded) ToggleImagePreprocessingMenu();
                if (imageProcessingMenuExpanded) ToggleImageProcessingMenu();
                if (imageRelationMenuExpanded) ToggleImageRelationMenu();
                if (objectJudgementMenuExpanded) ToggleObjectJudgementMenu();
                if (objectDefinitionMenuExpanded) ToggleObjectDefinitionMenu();
                if (objectDetectionParameterMenuExpanded) ToggleObjectDetectionParameterMenu();
                RemoveRoiSubMenuItems();
                RemoveImagePreprocessingSubMenuItems();
                RemoveImageProcessingSubMenuItems();

                expandedRoiText = null;
                expandedImageProcessingGroupIds.Clear();
                expandedImagePreprocessingGroupIds.Clear();
                expandedImageRelationGroupIds.Clear();
                expandedObjectJudgementIds.Clear();
                expandedObjectJudgementGroupIds.Clear();
                expandedObjectDefinitionIds.Clear();
                RemoveImageProcessingStepCommandMenuItems();

                if (imageProcessingDebounceTimer != null)
                {
                    imageProcessingDebounceTimer.Stop();
                }

                imageSourceGeneration++;
                imageProcessingExecutionRequested = false;
                explicitProcessedImageUpdateRequested = false;
                imageProcessingDisplayPending = false;
                includeImageProcessingTimeOnNextDisplay = false;
                imageProcessingStepElapsedMilliseconds.Clear();
                pendingImageProcessingParameters = null;
                MarkProcessedImageDirty();

                preprocessingExecutionRequested = false;
                preprocessingExecutionRequestedByImageRelation = false;
                preprocessedImageDirty = true;
                preprocessedImageGeneration++;
                imagePreprocessingStepElapsedMilliseconds.Clear();
                pendingImagePreprocessingParameters = null;
                lock (largePreprocessedImageLock)
                {
                    if (largePreprocessedImageSource != null)
                    {
                        largePreprocessedImageSource.ReleaseReference();
                        largePreprocessedImageSource = null;
                    }
                }
                if (latestPreprocessedImage != null)
                {
                    latestPreprocessedImage.Dispose();
                    latestPreprocessedImage = null;
                }

                InvalidateObjectJudgementProcessingResults();
                InvalidateObjectDetectionMeasurementMaskCache();
                ClearObjectDetectionMeasurementClipCache();
                if (objectDetectionMeasurementHighlightTimer != null)
                {
                    objectDetectionMeasurementHighlightTimer.Stop();
                }
                if (objectDetectionMeasurementResultHighlightTimer != null)
                {
                    objectDetectionMeasurementResultHighlightTimer.Stop();
                }
                objectDetectionMeasurementHighlightVisible = false;
                objectDetectionMeasurementResultHighlightsVisible = false;
                objectDetectionMeasurementResultParameterId = null;
                objectDetectionMeasurementResultObjectNumber = -1;
                objectDetectionMeasurementMinimumResultLine = default(ObjectDetectionImageLine);
                objectDetectionMeasurementMaximumResultLine = default(ObjectDetectionImageLine);
                objectDetectionMeasurementIsDrawing = false;

                systemParameters = importedSettings;
                selectedRoiIndex = -1;
                selectedImageProcessingStepIndex = -1;
                selectedImageProcessingGroupId = null;
                displayedImageProcessingStepIndex = -1;
                displayedImageProcessingGroupId = null;
                displayedImageRelationGroupId = null;
                displayedImageRelationSourceType = "Original";
                displayedImageRelationSourceId = null;
                selectedImageRelationIndex = -1;
                selectedImageRelationGroupId = null;
                activeImageRelationSourceType = "Original";
                activeImageRelationSourceId = null;
                activeImageRelationGroupId = null;
                activeObjectJudgementId = null;
                activeObjectJudgementGroupId = null;
                activeObjectDetectionParameterId = null;
                selectedObjectDetectionNumber = -1;
                objectDetectionMeasurementAppliedRecordId = null;

                roiMenuExpanded = false;
                imagePreprocessingMenuExpanded = false;
                imageProcessingMenuExpanded = false;
                imageRelationMenuExpanded = false;
                objectJudgementMenuExpanded = false;
                objectDefinitionMenuExpanded = false;
                objectDetectionParameterMenuExpanded = true;

                RebuildVisibleRoiItems();
                RebuildVisibleImageProcessingSteps();
                RebuildVisibleImagePreprocessingSteps();
                RebuildVisibleImageRelations();
                RebuildVisibleObjectJudgements();
                RebuildVisibleObjectDefinitions();
                RebuildVisibleObjectDetectionParameters();
                SelectObjectDetectionParameter(importedParameterId);
            }
            finally
            {
                isImportingDetectionParameterSettings = false;
            }

            suppressObjectDetectionParameterSourceAutoProcessing = true;
            try
            {
                ShowObjectDetectionParameterPanel(importedParameterId);
            }
            finally
            {
                suppressObjectDetectionParameterSourceAutoProcessing = false;
            }
        }

        private SystemParameterSettings CreateObjectDetectionParameterExportSettings(
            ObjectDetectionParameterSettings parameter)
        {
            var exportSettings = new SystemParameterSettings
            {
                LastImagePath = string.Empty,
                RoiEnabled = systemParameters.RoiEnabled,
                Roi = systemParameters.Roi
            };

            exportSettings.RoiRegions.AddRange(systemParameters.RoiRegions);
            exportSettings.ImagePreprocessingSteps.AddRange(systemParameters.ImagePreprocessingSteps);
            exportSettings.ImagePreprocessingGroups.AddRange(systemParameters.ImagePreprocessingGroups);
            exportSettings.ImageProcessingSteps.AddRange(systemParameters.ImageProcessingSteps);
            exportSettings.ImageProcessingGroups.AddRange(systemParameters.ImageProcessingGroups);
            exportSettings.ImageRelations.AddRange(systemParameters.ImageRelations);
            exportSettings.ImageRelationGroups.AddRange(systemParameters.ImageRelationGroups);
            exportSettings.ObjectJudgements.AddRange(systemParameters.ObjectJudgements);
            exportSettings.ObjectJudgementGroups.AddRange(systemParameters.ObjectJudgementGroups);
            exportSettings.ObjectDefinitions.AddRange(systemParameters.ObjectDefinitions);
            exportSettings.ObjectDetectionParameters.Add(parameter);
            return exportSettings;
        }

        private void RenameObjectDetectionParameter(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            string name = PromptForText("命名檢測參數", "檢測參數名稱", parameter.DisplayName);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            parameter.DisplayName = name.Trim();
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameterId);
            statusLabel.Text = "已命名檢測參數" + parameter.DisplayName;
        }

        private void MoveObjectDetectionParameter(string parameterId, int direction)
        {
            int index = systemParameters.ObjectDetectionParameters.FindIndex(
                item => string.Equals(item.Id, parameterId, StringComparison.Ordinal));
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= systemParameters.ObjectDetectionParameters.Count)
            {
                return;
            }

            ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
            systemParameters.ObjectDetectionParameters.RemoveAt(index);
            systemParameters.ObjectDetectionParameters.Insert(targetIndex, parameter);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(parameterId);
        }

        private void DeleteObjectDetectionParameter(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "確定要刪除「" + parameter.DisplayName + "」嗎？",
                    "刪除檢測參數",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            systemParameters.ObjectDetectionParameters.Remove(parameter);
            RemoveObjectDetectionDefectCoreResults(parameter.Id);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = "已刪除檢測參數";
        }


    }
}
