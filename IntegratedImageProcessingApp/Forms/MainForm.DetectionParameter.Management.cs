using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private bool isActivatingDetectionParameterProfile;
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
                    string savedProfileData = parameter.ProfileSettingsData;
                    try
                    {
                        parameter.ProfileSettingsData = string.Empty;
                        new SystemParameterIniService(dialog.FileName).Save(exportSettings);
                    }
                    finally
                    {
                        parameter.ProfileSettingsData = savedProfileData;
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

        private void ImportDetectionParameterSettings()
        {
            if (isLoadingImage || parameterApplyInProgress || isPreparingPreprocessedImage ||
                imageProcessingExecutionRequested || objectJudgementProcessingRequested ||
                objectDefinitionProcessingRequested || objectDetectionDefectProcessingRequested ||
                isObjectDetectionResultReviewRunning)
            {
                MessageBox.Show(
                    this,
                    "目前仍有影像流程正在執行，請完成後再插入檢測參數。",
                    "無法插入",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "插入兩組檢測參數及關聯設定";
                dialog.Filter = "檢測參數設定檔 (*.ini)|*.ini|所有檔案 (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (dialog.FileNames.Length != 2)
                {
                    MessageBox.Show(
                        this,
                        "請一次選取兩個檢測參數設定檔。",
                        "選取數量不符",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                SystemParameterSettings mergedSettings;
                var importedNames = new List<string>();
                try
                {
                    mergedSettings = CloneSystemParameterSettings(systemParameters);
                    string activeId = activeObjectDetectionParameterId ??
                        systemParameters.ActiveObjectDetectionParameterId;
                    ObjectDetectionParameterSettings activeParameter =
                        mergedSettings.ObjectDetectionParameters.FirstOrDefault(
                            item => string.Equals(item.Id, activeId, StringComparison.Ordinal));
                    if (activeParameter != null && !string.IsNullOrWhiteSpace(activeParameter.ProfileSettingsData))
                    {
                        activeParameter.ProfileSettingsData = CreateDetectionParameterProfileData(mergedSettings);
                    }
                    else
                    {
                        mergedSettings.DetectionParameterBaselineData =
                            CreateDetectionParameterProfileData(mergedSettings);
                    }

                    mergedSettings.ActiveObjectDetectionParameterId = activeId ?? string.Empty;
                    var occupiedIds = CollectSettingsIds(mergedSettings);
                    foreach (string fileName in dialog.FileNames)
                    {
                        SystemParameterSettings importedSettings =
                            new SystemParameterIniService(fileName).Load();
                        if (importedSettings == null || importedSettings.ObjectDetectionParameters.Count != 1)
                        {
                            throw new InvalidDataException(
                                "檔案「" + Path.GetFileName(fileName) +
                                "」必須包含且只包含一個檢測參數，請選擇由檢測參數匯出的設定檔。");
                        }

                        importedSettings.ActiveObjectDetectionParameterId = string.Empty;
                        importedSettings.DetectionParameterBaselineData = string.Empty;
                        RemapDetectionParameterPackageIds(importedSettings, occupiedIds);
                        ObjectDetectionParameterSettings importedParameter =
                            importedSettings.ObjectDetectionParameters[0];
                        importedParameter.ProfileSettingsData = CreateDetectionParameterProfileData(importedSettings);
                        importedParameter.DisplayName = GetUniqueDetectionParameterName(
                            importedParameter.DisplayName,
                            mergedSettings.ObjectDetectionParameters.Select(
                                (item, index) => GetObjectDetectionParameterDisplayName(item, index))
                                .Concat(importedNames));
                        mergedSettings.ObjectDetectionParameters.Add(importedParameter);
                        importedNames.Add(importedParameter.DisplayName);
                    }

                    SaveSystemParameterSettingsAtomically(mergedSettings);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法插入檢測參數設定；原本設定未變更：\r\n" + exception.Message,
                        "插入失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                systemParameters = mergedSettings;
                objectDetectionParameterMenuExpanded = true;
                RebuildVisibleObjectDetectionParameters();
                SelectObjectDetectionParameter(activeObjectDetectionParameterId);
                statusLabel.Text = "已插入兩組檢測參數；請選擇要使用的參數";
                MessageBox.Show(
                    this,
                    "已插入兩組獨立檢測參數：\r\n「" + importedNames[0] + "」\r\n「" + importedNames[1] + "」\r\n\r\n" +
                    "兩組的 ROI 與影像流程會各自保存。請在左側檢測參數清單選擇一組，再執行檢測。",
                    "插入完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private static SystemParameterSettings CloneSystemParameterSettings(SystemParameterSettings settings)
        {
            string temporaryPath = Path.Combine(
                Path.GetTempPath(),
                "iip-settings-clone-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                new SystemParameterIniService(temporaryPath).Save(settings);
                return new SystemParameterIniService(temporaryPath).Load();
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string CreateDetectionParameterProfileData(SystemParameterSettings settings)
        {
            var profile = new SystemParameterSettings
            {
                LastImagePath = string.Empty,
                RoiEnabled = settings.RoiEnabled,
                Roi = settings.Roi
            };
            profile.RoiRegions.AddRange(settings.RoiRegions);
            profile.ImagePreprocessingSteps.AddRange(settings.ImagePreprocessingSteps);
            profile.ImagePreprocessingGroups.AddRange(settings.ImagePreprocessingGroups);
            profile.ImageProcessingSteps.AddRange(settings.ImageProcessingSteps);
            profile.ImageProcessingGroups.AddRange(settings.ImageProcessingGroups);
            profile.ImageRelations.AddRange(settings.ImageRelations);
            profile.ImageRelationGroups.AddRange(settings.ImageRelationGroups);
            profile.ObjectJudgements.AddRange(settings.ObjectJudgements);
            profile.ObjectJudgementGroups.AddRange(settings.ObjectJudgementGroups);
            profile.ObjectDefinitions.AddRange(settings.ObjectDefinitions);

            string temporaryPath = Path.Combine(
                Path.GetTempPath(),
                "iip-profile-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                new SystemParameterIniService(temporaryPath).Save(profile);
                return Convert.ToBase64String(File.ReadAllBytes(temporaryPath));
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static SystemParameterSettings ReadDetectionParameterProfileData(string data)
        {
            string temporaryPath = Path.Combine(
                Path.GetTempPath(),
                "iip-profile-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                File.WriteAllBytes(temporaryPath, Convert.FromBase64String(data));
                return new SystemParameterIniService(temporaryPath).Load();
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static IEnumerable<object> EnumerateSettingsObjects(object value)
        {
            if (value == null || value is string || value.GetType().IsValueType)
            {
                yield break;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                foreach (object item in enumerable)
                {
                    foreach (object nested in EnumerateSettingsObjects(item)) yield return nested;
                }
                yield break;
            }

            yield return value;
            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0 ||
                    property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
                {
                    continue;
                }

                object nestedValue = property.GetValue(value, null);
                foreach (object nested in EnumerateSettingsObjects(nestedValue)) yield return nested;
            }
        }

        private static HashSet<string> CollectSettingsIds(SystemParameterSettings settings)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (object item in EnumerateSettingsObjects(settings))
            {
                PropertyInfo idProperty = item.GetType().GetProperty("Id");
                string id = idProperty == null ? null : idProperty.GetValue(item, null) as string;
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
            return ids;
        }

        private static void RemapDetectionParameterPackageIds(
            SystemParameterSettings settings,
            HashSet<string> occupiedIds)
        {
            var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (object item in EnumerateSettingsObjects(settings))
            {
                PropertyInfo idProperty = item.GetType().GetProperty("Id");
                string oldId = idProperty == null ? null : idProperty.GetValue(item, null) as string;
                if (string.IsNullOrWhiteSpace(oldId)) continue;

                string newId;
                if (!idMap.TryGetValue(oldId, out newId))
                {
                    do { newId = Guid.NewGuid().ToString("N"); }
                    while (!occupiedIds.Add(newId));
                    idMap.Add(oldId, newId);
                }
                idProperty.SetValue(item, newId, null);
            }

            foreach (object item in EnumerateSettingsObjects(settings))
            {
                foreach (PropertyInfo property in item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || property.Name == "Id" || property.GetIndexParameters().Length > 0) continue;
                    if (property.PropertyType == typeof(string) && property.CanWrite &&
                        property.Name.EndsWith("Id", StringComparison.Ordinal))
                    {
                        string oldReference = property.GetValue(item, null) as string;
                        string newReference;
                        if (!string.IsNullOrEmpty(oldReference) && idMap.TryGetValue(oldReference, out newReference))
                        {
                            property.SetValue(item, newReference, null);
                        }
                    }
                    else if (property.PropertyType == typeof(string) && property.CanWrite &&
                        property.Name.EndsWith("Namespace", StringComparison.Ordinal))
                    {
                        string oldNamespace = property.GetValue(item, null) as string;
                        if (!string.IsNullOrEmpty(oldNamespace))
                        {
                            string[] parts = oldNamespace.Split('|');
                            for (int index = 0; index < parts.Length; index++)
                            {
                                string newReference;
                                if (idMap.TryGetValue(parts[index], out newReference))
                                {
                                    parts[index] = newReference;
                                }
                            }
                            property.SetValue(item, string.Join("|", parts), null);
                        }
                    }
                    else if (property.Name.EndsWith("Ids", StringComparison.Ordinal))
                    {
                        IList references = property.GetValue(item, null) as IList;
                        if (references == null) continue;
                        for (int index = 0; index < references.Count; index++)
                        {
                            string oldReference = references[index] as string;
                            string newReference;
                            if (!string.IsNullOrEmpty(oldReference) && idMap.TryGetValue(oldReference, out newReference))
                            {
                                references[index] = newReference;
                            }
                        }
                    }
                }
            }

            settings.ActiveObjectDetectionParameterId = string.Empty;
            settings.DetectionParameterBaselineData = string.Empty;
            foreach (ObjectDetectionParameterSettings parameter in settings.ObjectDetectionParameters)
            {
                parameter.ProfileSettingsData = string.Empty;
                if (!string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData))
                {
                    parameter.FlatFieldSavedSettingsSignature =
                        CreateObjectDetectionFlatFieldSettingsSignature(parameter);
                }
            }
        }

        private static string GetUniqueDetectionParameterName(string candidate, IEnumerable<string> existingNames)
        {
            string baseName = string.IsNullOrWhiteSpace(candidate) ? "檢測參數" : candidate.Trim();
            var names = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(baseName)) return baseName;

            int suffix = 2;
            string name;
            do { name = baseName + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")"; suffix++; }
            while (names.Contains(name));
            return name;
        }

        private void SaveSystemParameterSettingsAtomically(SystemParameterSettings settings)
        {
            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemParameters.ini");
            string temporaryPath = settingsPath + ".insert-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                new SystemParameterIniService(temporaryPath).Save(settings);
                if (File.Exists(settingsPath)) File.Replace(temporaryPath, settingsPath, null);
                else File.Move(temporaryPath, settingsPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private bool TryActivateDetectionParameterProfile(string parameterId, bool showParameterPanel)
        {
            if (isActivatingDetectionParameterProfile)
            {
                return false;
            }

            ObjectDetectionParameterSettings target = string.IsNullOrEmpty(parameterId)
                ? null
                : FindObjectDetectionParameter(parameterId);
            string currentId = activeObjectDetectionParameterId ??
                systemParameters.ActiveObjectDetectionParameterId;
            if (string.Equals(currentId, parameterId, StringComparison.Ordinal))
            {
                return false;
            }

            if (isLoadingImage || parameterApplyInProgress || isPreparingPreprocessedImage ||
                imageProcessingExecutionRequested || objectJudgementProcessingRequested ||
                objectDefinitionProcessingRequested || objectDetectionDefectProcessingRequested ||
                isObjectDetectionResultReviewRunning)
            {
                statusLabel.Text = "目前有流程正在執行，完成後才能切換檢測參數";
                return true;
            }

            ObjectDetectionParameterSettings current = FindObjectDetectionParameter(currentId);
            bool currentHasProfile = current != null && !string.IsNullOrWhiteSpace(current.ProfileSettingsData);
            bool targetHasProfile = target != null && !string.IsNullOrWhiteSpace(target.ProfileSettingsData);
            if (!currentHasProfile && !targetHasProfile)
            {
                activeObjectDetectionParameterId = parameterId;
                systemParameters.ActiveObjectDetectionParameterId = parameterId ?? string.Empty;
                SaveSystemParameters();
                return false;
            }

            try
            {
                if (currentHasProfile)
                {
                    current.ProfileSettingsData = CreateDetectionParameterProfileData(systemParameters);
                }
                else
                {
                    systemParameters.DetectionParameterBaselineData =
                        CreateDetectionParameterProfileData(systemParameters);
                }

                string targetData = targetHasProfile
                    ? target.ProfileSettingsData
                    : systemParameters.DetectionParameterBaselineData;
                if (string.IsNullOrWhiteSpace(targetData))
                {
                    throw new InvalidDataException("找不到這組參數所保存的流程設定。");
                }

                SystemParameterSettings profile = ReadDetectionParameterProfileData(targetData);
                SystemParameterSettings updated = CloneSystemParameterSettings(systemParameters);
                CopyDetectionParameterProfileConfiguration(updated, profile);
                updated.ActiveObjectDetectionParameterId = parameterId ?? string.Empty;
                updated.LastImagePath = systemParameters.LastImagePath;
                SaveSystemParameterSettingsAtomically(updated);

                isActivatingDetectionParameterProfile = true;
                try
                {
                    ApplyImportedDetectionParameterSettings(updated, parameterId, showParameterPanel);
                }
                finally
                {
                    isActivatingDetectionParameterProfile = false;
                }
                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "無法切換檢測參數流程；目前設定未切換：\r\n" + exception.Message,
                    "切換失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return true;
            }
        }

        private static void CopyDetectionParameterProfileConfiguration(
            SystemParameterSettings target,
            SystemParameterSettings source)
        {
            target.RoiEnabled = source.RoiEnabled;
            target.Roi = source.Roi;
            target.RoiRegions.Clear();
            target.RoiRegions.AddRange(source.RoiRegions);
            target.ImagePreprocessingSteps.Clear();
            target.ImagePreprocessingSteps.AddRange(source.ImagePreprocessingSteps);
            target.ImagePreprocessingGroups.Clear();
            target.ImagePreprocessingGroups.AddRange(source.ImagePreprocessingGroups);
            target.ImageProcessingSteps.Clear();
            target.ImageProcessingSteps.AddRange(source.ImageProcessingSteps);
            target.ImageProcessingGroups.Clear();
            target.ImageProcessingGroups.AddRange(source.ImageProcessingGroups);
            target.ImageRelations.Clear();
            target.ImageRelations.AddRange(source.ImageRelations);
            target.ImageRelationGroups.Clear();
            target.ImageRelationGroups.AddRange(source.ImageRelationGroups);
            target.ObjectJudgements.Clear();
            target.ObjectJudgements.AddRange(source.ObjectJudgements);
            target.ObjectJudgementGroups.Clear();
            target.ObjectJudgementGroups.AddRange(source.ObjectJudgementGroups);
            target.ObjectDefinitions.Clear();
            target.ObjectDefinitions.AddRange(source.ObjectDefinitions);
        }

        private void ApplyImportedDetectionParameterSettings(
            SystemParameterSettings importedSettings,
            string importedParameterId,
            bool showParameterPanel = true)
        {
            isImportingDetectionParameterSettings = true;
            try
            {
                if (showParameterPanel)
                {
                    functionListBox.SelectedIndex = -1;
                }
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
                systemParameters.ActiveObjectDetectionParameterId = importedParameterId ?? string.Empty;
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
                activeObjectDetectionParameterId = importedParameterId;
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
                if (showParameterPanel)
                {
                    SelectObjectDetectionParameter(importedParameterId);
                }
                else if (isObjectDetectionResultReviewMode)
                {
                    int resultReviewIndex = functionListBox.Items.IndexOf(ObjectDetectionResultReviewMenuText);
                    if (resultReviewIndex >= 0) functionListBox.SelectedIndex = resultReviewIndex;
                }
            }
            finally
            {
                isImportingDetectionParameterSettings = false;
            }

            if (showParameterPanel)
            {
                ShowObjectDetectionParameterPanel(importedParameterId);
            }
            else if (isObjectDetectionResultReviewMode)
            {
                PopulateObjectDetectionResultReviewParameters();
            }
        }

        private SystemParameterSettings CreateObjectDetectionParameterExportSettings(
            ObjectDetectionParameterSettings parameter)
        {
            SystemParameterSettings profileSettings =
                string.IsNullOrWhiteSpace(parameter.ProfileSettingsData)
                    ? systemParameters
                    : ReadDetectionParameterProfileData(parameter.ProfileSettingsData);
            var exportSettings = new SystemParameterSettings
            {
                LastImagePath = string.Empty,
                RoiEnabled = profileSettings.RoiEnabled,
                Roi = profileSettings.Roi
            };

            exportSettings.RoiRegions.AddRange(profileSettings.RoiRegions);
            exportSettings.ImagePreprocessingSteps.AddRange(profileSettings.ImagePreprocessingSteps);
            exportSettings.ImagePreprocessingGroups.AddRange(profileSettings.ImagePreprocessingGroups);
            exportSettings.ImageProcessingSteps.AddRange(profileSettings.ImageProcessingSteps);
            exportSettings.ImageProcessingGroups.AddRange(profileSettings.ImageProcessingGroups);
            exportSettings.ImageRelations.AddRange(profileSettings.ImageRelations);
            exportSettings.ImageRelationGroups.AddRange(profileSettings.ImageRelationGroups);
            exportSettings.ObjectJudgements.AddRange(profileSettings.ObjectJudgements);
            exportSettings.ObjectJudgementGroups.AddRange(profileSettings.ObjectJudgementGroups);
            exportSettings.ObjectDefinitions.AddRange(profileSettings.ObjectDefinitions);
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

            if (string.Equals(activeObjectDetectionParameterId, parameterId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(parameter.ProfileSettingsData))
            {
                if (!TryActivateDetectionParameterProfile(null, true))
                {
                    return;
                }
                parameter = FindObjectDetectionParameter(parameterId);
                if (parameter == null)
                {
                    return;
                }
            }

            systemParameters.ObjectDetectionParameters.Remove(parameter);
            if (!systemParameters.ObjectDetectionParameters.Any(
                item => !string.IsNullOrWhiteSpace(item.ProfileSettingsData)))
            {
                systemParameters.DetectionParameterBaselineData = string.Empty;
            }
            if (string.Equals(systemParameters.ActiveObjectDetectionParameterId, parameterId, StringComparison.Ordinal))
            {
                systemParameters.ActiveObjectDetectionParameterId = string.Empty;
                activeObjectDetectionParameterId = null;
            }
            RemoveObjectDetectionDefectCoreResults(parameter.Id);
            SaveSystemParameters();
            RebuildVisibleObjectDetectionParameters();
            statusLabel.Text = "已刪除檢測參數";
        }


    }
}
