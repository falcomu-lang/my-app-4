using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
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
        private readonly Dictionary<int, string> visibleObjectDetectionParameterGroupNames =
            new Dictionary<int, string>();
        private readonly HashSet<string> collapsedObjectDetectionParameterGroups =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            if (visibleObjectDetectionParameterGroupNames.ContainsKey(visibleIndex))
            {
                return false;
            }

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
                visibleObjectDetectionParameterGroupNames.Clear();
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
                Dictionary<string, List<ObjectDetectionParameterSettings>> groups =
                    systemParameters.ObjectDetectionParameters
                        .Where(parameter => !string.IsNullOrWhiteSpace(parameter.GroupName))
                        .GroupBy(parameter => parameter.GroupName.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Where(group => group.Count() >= 2)
                        .ToDictionary(
                            group => group.Key,
                            group => group.ToList(),
                            StringComparer.OrdinalIgnoreCase);
                var renderedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < systemParameters.ObjectDetectionParameters.Count; index++)
                {
                    ObjectDetectionParameterSettings parameter = systemParameters.ObjectDetectionParameters[index];
                    string groupName = string.IsNullOrWhiteSpace(parameter.GroupName)
                        ? string.Empty
                        : parameter.GroupName.Trim();
                    List<ObjectDetectionParameterSettings> groupParameters;
                    if (groups.TryGetValue(groupName, out groupParameters))
                    {
                        if (!renderedGroups.Add(groupName))
                        {
                            continue;
                        }

                        bool isCollapsed = collapsedObjectDetectionParameterGroups.Contains(groupName);
                        visibleObjectDetectionParameterGroupNames[insertIndex] = groupName;
                        functionListBox.Items.Insert(
                            insertIndex++,
                            "    " + (isCollapsed ? "[+] " : "[-] ") + groupName);
                        if (isCollapsed)
                        {
                            continue;
                        }

                        for (int memberIndex = 0;
                            memberIndex < systemParameters.ObjectDetectionParameters.Count;
                            memberIndex++)
                        {
                            ObjectDetectionParameterSettings member =
                                systemParameters.ObjectDetectionParameters[memberIndex];
                            if (!string.Equals(
                                    member.GroupName == null ? string.Empty : member.GroupName.Trim(),
                                    groupName,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            visibleObjectDetectionParameterIds[insertIndex] = member.Id;
                            functionListBox.Items.Insert(
                                insertIndex++,
                                "        " + GetObjectDetectionParameterDisplayName(member, memberIndex));
                        }
                    }
                    else
                    {
                        visibleObjectDetectionParameterIds[insertIndex] = parameter.Id;
                        functionListBox.Items.Insert(
                            insertIndex++,
                            "    " + GetObjectDetectionParameterDisplayName(parameter, index));
                    }
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

        private List<string> GetSelectedObjectDetectionParameterIds()
        {
            var parameterIds = new List<string>();
            foreach (int selectedIndex in functionListBox.SelectedIndices)
            {
                string parameterId;
                if (visibleObjectDetectionParameterIds.TryGetValue(selectedIndex, out parameterId) &&
                    !string.IsNullOrEmpty(parameterId) &&
                    !parameterIds.Contains(parameterId, StringComparer.Ordinal))
                {
                    parameterIds.Add(parameterId);
                }
            }

            return parameterIds;
        }

        private void ToggleObjectDetectionParameterGroup(string groupName)
        {
            if (collapsedObjectDetectionParameterGroups.Add(groupName))
            {
                statusLabel.Text = "已收合檢測參數群組「" + groupName + "」";
            }
            else
            {
                collapsedObjectDetectionParameterGroups.Remove(groupName);
                statusLabel.Text = "已展開檢測參數群組「" + groupName + "」";
            }

            RebuildVisibleObjectDetectionParameters();
            foreach (KeyValuePair<int, string> item in visibleObjectDetectionParameterGroupNames)
            {
                if (string.Equals(item.Value, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    functionListBox.SelectedIndex = item.Key;
                    break;
                }
            }
        }

        private void ShowObjectDetectionParameterGroupContextMenu(string groupName, Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add(
                "保存群組及其參數...",
                null,
                delegate { ExportObjectDetectionParameterGroup(groupName); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(
                collapsedObjectDetectionParameterGroups.Contains(groupName) ? "展開群組" : "收合群組",
                null,
                delegate { ToggleObjectDetectionParameterGroup(groupName); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("取消分組", null, delegate { DissolveObjectDetectionParameterGroup(groupName); });
            menu.Show(functionListBox, location);
        }

        private void ShowObjectDetectionParameterMultiSelectContextMenu(
            List<string> parameterIds,
            Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("分組", null, delegate { GroupObjectDetectionParameters(parameterIds); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem ungroupItem = menu.Items.Add(
                "取消所選參數分組",
                null,
                delegate { UngroupObjectDetectionParameters(parameterIds); });
            ungroupItem.Enabled = parameterIds
                .Select(FindObjectDetectionParameter)
                .Any(parameter => parameter != null && !string.IsNullOrWhiteSpace(parameter.GroupName));
            menu.Show(functionListBox, location);
        }

        private void GroupObjectDetectionParameters(List<string> parameterIds)
        {
            List<ObjectDetectionParameterSettings> selectedParameters = parameterIds
                .Select(FindObjectDetectionParameter)
                .Where(parameter => parameter != null)
                .ToList();
            if (selectedParameters.Count < 2)
            {
                statusLabel.Text = "至少選擇兩個檢測參數才能分組";
                return;
            }

            var existingGroupNames = new HashSet<string>(
                systemParameters.ObjectDetectionParameters
                    .Where(parameter => !string.IsNullOrWhiteSpace(parameter.GroupName))
                    .Select(parameter => parameter.GroupName.Trim()),
                StringComparer.OrdinalIgnoreCase);
            int nextGroupNumber = 1;
            while (existingGroupNames.Contains(
                "群組" + nextGroupNumber.ToString(CultureInfo.InvariantCulture)))
            {
                nextGroupNumber++;
            }
            string defaultGroupName = "群組" + nextGroupNumber.ToString(CultureInfo.InvariantCulture);
            string requestedName = PromptForText(
                "分組檢測參數",
                "群組名稱（輸入既有名稱會加入該群組）",
                defaultGroupName);
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                return;
            }

            requestedName = requestedName.Trim();
            string groupName = systemParameters.ObjectDetectionParameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.GroupName))
                .Select(parameter => parameter.GroupName.Trim())
                .FirstOrDefault(name => string.Equals(
                    name,
                    requestedName,
                    StringComparison.OrdinalIgnoreCase)) ?? requestedName;
            List<string> previousGroupNames = selectedParameters
                .Select(parameter => parameter.GroupName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (ObjectDetectionParameterSettings parameter in selectedParameters)
            {
                parameter.GroupName = groupName;
            }

            NormalizeObjectDetectionParameterGroups(previousGroupNames);
            collapsedObjectDetectionParameterGroups.Remove(groupName);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            SelectObjectDetectionParameter(selectedParameters[0].Id);
            statusLabel.Text = "已將 " + selectedParameters.Count + " 個檢測參數分組至「" + groupName + "」";
        }

        private void UngroupObjectDetectionParameters(IEnumerable<string> parameterIds)
        {
            List<ObjectDetectionParameterSettings> selectedParameters = parameterIds
                .Select(FindObjectDetectionParameter)
                .Where(parameter => parameter != null)
                .ToList();
            List<string> groupNames = selectedParameters
                .Select(parameter => parameter.GroupName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (groupNames.Count == 0)
            {
                return;
            }

            foreach (ObjectDetectionParameterSettings parameter in selectedParameters)
            {
                parameter.GroupName = string.Empty;
            }

            NormalizeObjectDetectionParameterGroups(groupNames);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            if (selectedParameters.Count > 0)
            {
                SelectObjectDetectionParameter(selectedParameters[0].Id);
            }
            statusLabel.Text = "已取消所選檢測參數的分組";
        }

        private void DissolveObjectDetectionParameterGroup(string groupName)
        {
            List<string> parameterIds = systemParameters.ObjectDetectionParameters
                .Where(parameter => string.Equals(
                    parameter.GroupName == null ? string.Empty : parameter.GroupName.Trim(),
                    groupName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(parameter => parameter.Id)
                .ToList();
            UngroupObjectDetectionParameters(parameterIds);
        }

        private void NormalizeObjectDetectionParameterGroups(IEnumerable<string> groupNames)
        {
            foreach (string groupName in groupNames)
            {
                List<ObjectDetectionParameterSettings> members = systemParameters.ObjectDetectionParameters
                    .Where(parameter => string.Equals(
                        parameter.GroupName == null ? string.Empty : parameter.GroupName.Trim(),
                        groupName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (members.Count == 1)
                {
                    members[0].GroupName = string.Empty;
                    collapsedObjectDetectionParameterGroups.Remove(groupName);
                }
                else if (members.Count == 0)
                {
                    collapsedObjectDetectionParameterGroups.Remove(groupName);
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
            if (!string.IsNullOrWhiteSpace(parameter.GroupName))
            {
                menu.Items.Add(
                    "取消分組",
                    null,
                    delegate { UngroupObjectDetectionParameters(new[] { parameterId }); });
            }
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
                    string originalGroupName = parameter.GroupName;
                    try
                    {
                        parameter.GroupName = string.Empty;
                        new SystemParameterIniService(dialog.FileName).Save(exportSettings);
                    }
                    finally
                    {
                        parameter.GroupName = originalGroupName;
                    }
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

        private void ExportObjectDetectionParameterGroup(string groupName)
        {
            List<ObjectDetectionParameterSettings> groupParameters =
                systemParameters.ObjectDetectionParameters
                    .Where(parameter => string.Equals(
                        parameter.GroupName == null ? string.Empty : parameter.GroupName.Trim(),
                        groupName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            if (groupParameters.Count < 2)
            {
                MessageBox.Show(
                    this,
                    "群組至少要有兩個檢測參數才能保存。",
                    "無法保存群組",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string fileName = groupName;
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidCharacter, '_');
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "保存檢測參數群組及其成員";
                dialog.Filter = "檢測參數設定檔 (*.ini)|*.ini|所有檔案 (*.*)|*.*";
                dialog.DefaultExt = "ini";
                dialog.AddExtension = true;
                dialog.FileName = fileName + ".ini";
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    SystemParameterSettings exportSettings =
                        CreateObjectDetectionParameterExportSettings(groupParameters[0]);
                    exportSettings.ObjectDetectionParameters.Clear();
                    exportSettings.ObjectDetectionParameters.AddRange(groupParameters);
                    new SystemParameterIniService(dialog.FileName).Save(exportSettings);
                    statusLabel.Text = "已保存群組「" + groupName + "」及 " +
                        groupParameters.Count + " 個檢測參數";
                    MessageBox.Show(
                        this,
                        "群組「" + groupName + "」及其 " + groupParameters.Count +
                        " 個成員已保存至：\r\n" + dialog.FileName,
                        "群組保存完成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法保存檢測參數群組：\r\n" + exception.Message,
                        "群組保存失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyImportedDetectionParameterSettings(
            SystemParameterSettings importedSettings,
            string importedParameterId)
        {
            CancelDetectionRecipeBoundOperations();
            ClearObjectDetectionResultReviewResults();
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
                ClearObjectDetectionDefectCoreResults();
                foreach (string resultKey in objectDetectionFrequencyResults.Keys.ToList())
                {
                    const string frequency2Suffix = "|Frequency2";
                    bool isFrequency2 = resultKey.EndsWith(
                        frequency2Suffix,
                        StringComparison.Ordinal);
                    string parameterId = isFrequency2
                        ? resultKey.Substring(0, resultKey.Length - frequency2Suffix.Length)
                        : resultKey;
                    RemoveObjectDetectionFrequencyResult(parameterId, isFrequency2 ? 1 : 0);
                }
                Interlocked.Increment(ref objectDetectionFlatFieldEvaluationGeneration);
                ClearObjectDetectionFlatFieldCorrectionPreview();
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
                collapsedObjectDetectionParameterGroups.Clear();
                if (objectDetectionParameterTabControl != null &&
                    objectDetectionParameterTabControl.TabPages.Count > 0)
                {
                    objectDetectionParameterTabControl.SelectedIndex = 0;
                }
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
                objectDetectionParameterMenuExpanded = !string.IsNullOrWhiteSpace(importedParameterId);

                RebuildVisibleRoiItems();
                RebuildVisibleImageProcessingSteps();
                RebuildVisibleImagePreprocessingSteps();
                RebuildVisibleImageRelations();
                RebuildVisibleObjectJudgements();
                RebuildVisibleObjectDefinitions();
                RebuildVisibleObjectDetectionParameters();
                if (!string.IsNullOrWhiteSpace(importedParameterId))
                {
                    SelectObjectDetectionParameter(importedParameterId);
                }
            }
            finally
            {
                isImportingDetectionParameterSettings = false;
            }

            if (string.IsNullOrWhiteSpace(importedParameterId))
            {
                ResetRightFunctionPanel();
                rightPanelTitleLabel.Text = "新建參數";
                parameterPlaceholderLabel.Text = "空白檢測參數已建立；請從 ROI 與找尋物件流程開始設定。";
                parameterPlaceholderLabel.Visible = true;
                parameterPlaceholderLabel.BringToFront();
            }
            else
            {
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
            InvalidateConnectionControllerRecipeMetadata();
            QueueStationStatusPublish(true);
        }

        private void CancelDetectionRecipeBoundOperations()
        {
            Interlocked.Increment(ref detectionRecipeGeneration);
            var replacement = new CancellationTokenSource();
            CancellationTokenSource previousCancellation =
                Interlocked.Exchange(ref detectionRecipeCancellationTokenSource, replacement);
            if (previousCancellation != null)
            {
                try
                {
                    previousCancellation.Cancel();
                }
                finally
                {
                    previousCancellation.Dispose();
                }
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

            string groupName = parameter.GroupName;
            systemParameters.ObjectDetectionParameters.Remove(parameter);
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                NormalizeObjectDetectionParameterGroups(new[] { groupName });
            }
            RemoveObjectDetectionDefectCoreResults(parameter.Id);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = "已刪除檢測參數";
        }


    }
}
