using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private string detectionRecipePendingDefinitionId;
        private TaskCompletionSource<string> detectionRecipeDefinitionCompletion;
        private bool isDetectionRecipeAutoRunActive;

        private void EnsureDetectionRecipeGenerationIsCurrent(
            int generation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Interlocked.CompareExchange(ref detectionRecipeGeneration, 0, 0))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private void AddDetectionParameterRecipe()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "讀取檢測參數並加入 Recipe";
                dialog.Filter = "檢測參數設定檔 (*.ini)|*.ini|所有檔案 (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    SystemParameterSettings imported = new SystemParameterIniService(dialog.FileName).Load();
                    if (imported == null || imported.ObjectDetectionParameters == null ||
                        imported.ObjectDetectionParameters.Count != 1)
                    {
                        MessageBox.Show(
                            this,
                            "參數檔必須包含且只包含一組檢測參數。請選擇由檢測參數匯出的 INI 檔。",
                            "檔案內容不符",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    ObjectDetectionParameterSettings parameter = imported.ObjectDetectionParameters[0];
                    string suggestedName = string.IsNullOrWhiteSpace(parameter.DisplayName)
                        ? Path.GetFileNameWithoutExtension(dialog.FileName)
                        : parameter.DisplayName.Trim();
                    using (var metadata = new DetectionRecipeMetadataDialog(
                        "加入檢測參數 Recipe",
                        suggestedName,
                        string.Empty))
                    {
                        if (metadata.ShowDialog(this) != DialogResult.OK) return;

                        var catalog = new DetectionRecipeCatalogService(AppDomain.CurrentDomain.BaseDirectory);
                        DetectionRecipeCatalogEntry entry = catalog.AddRecipe(
                            dialog.FileName,
                            metadata.DisplayName,
                            metadata.Description);
                        statusLabel.Text = "已加入 Recipe：「" + entry.DisplayName + "」";
                        MessageBox.Show(
                            this,
                            "參數已加入軟體根目錄的 recipe 資料夾。\r\n" +
                            "加入後不會自動啟用，請使用「檢測參數選擇」載入。",
                            "加入完成",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法加入檢測參數 Recipe：\r\n" + exception.Message +
                        "\r\n\r\n請確認軟體根目錄允許寫入。",
                        "加入失敗",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private async void SelectDetectionParameterRecipe()
        {
            DetectionRecipeCatalogService catalog;
            System.Collections.Generic.IList<DetectionRecipeCatalogEntry> entries;
            try
            {
                catalog = new DetectionRecipeCatalogService(AppDomain.CurrentDomain.BaseDirectory);
                entries = catalog.LoadParameterList();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "無法讀取 recipe 參數清單：\r\n" + exception.Message,
                    "參數清單錯誤",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (entries.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "recipe 參數庫目前是空的。請先使用「檢測參數讀取加入」。",
                    "尚無參數",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            IList<string> inUseRecipeIds;
            try
            {
                inUseRecipeIds = FindInUseDetectionRecipeIds(catalog, entries);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "無法確認哪些參數正在使用，因此本次暫停刪除功能。\r\n" + exception.Message,
                    "使用狀態無法確認",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                inUseRecipeIds = entries.Select(entry => entry.Id).ToList();
            }
            using (var dialog = new DetectionRecipeSelectionDialog(catalog, entries, inUseRecipeIds))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    entries = catalog.LoadParameterList();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "無法重新讀取 recipe 參數清單：\r\n" + exception.Message,
                        "參數清單錯誤",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                DetectionRecipeCatalogEntry entry = entries.FirstOrDefault(
                    item => string.Equals(item.Id, dialog.SelectedRecipeId, StringComparison.Ordinal));
                if (entry == null || !catalog.ParameterFileExists(entry))
                {
                    MessageBox.Show(
                        this,
                        "找不到所選 Recipe 的參數檔，請重新加入該參數。",
                        "參數檔缺失",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                await LoadDetectionParameterRecipeAsync(catalog, entry);
            }
        }

        private async Task LoadDetectionParameterRecipeAsync(
            DetectionRecipeCatalogService catalog,
            DetectionRecipeCatalogEntry entry)
        {
            SystemParameterSettings imported;
            string recipeFilePath;
            try
            {
                recipeFilePath = catalog.GetParameterFilePath(entry);
                imported = new SystemParameterIniService(recipeFilePath).Load();
                if (imported == null || imported.ObjectDetectionParameters == null ||
                    imported.ObjectDetectionParameters.Count != 1)
                {
                    throw new InvalidDataException("Recipe 檔案必須包含且只包含一組檢測參數。");
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "無法讀取所選 Recipe：\r\n" + exception.Message,
                    "讀取失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "將載入「" + entry.DisplayName + "」的完整檢測參數及關聯流程，取代目前的流程設定。\r\n" +
                "若舊參數仍在運算，會取消舊運算並清除舊結果。套用後會保留目前圖片，\r\n" +
                "重新處理參數所需的影像／物件流程，並執行所有啟用的缺陷條件（含頻域與紋理）。\r\n" +
                "Recipe 原檔與說明不會被修改；大型影像可能需要較長時間。\r\n\r\n" +
                "確定要載入嗎？",
                "載入檢測參數",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            imported.LastImagePath = systemParameters.LastImagePath;
            imported.ActiveDetectionRecipeId = entry.Id;
            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemParameters.ini");
            string temporarySettingsPath = settingsPath + ".recipe-" + Guid.NewGuid().ToString("N") + ".tmp";
            bool settingsSaved = false;
            try
            {
                new SystemParameterIniService(temporarySettingsPath).Save(imported);
                if (File.Exists(settingsPath)) File.Replace(temporarySettingsPath, settingsPath, null);
                else File.Move(temporarySettingsPath, settingsPath);
                settingsSaved = true;

                ApplyImportedDetectionParameterSettings(
                    imported,
                    imported.ObjectDetectionParameters[0].Id);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    (settingsSaved
                        ? "Recipe 已寫入目前設定檔，但畫面載入未完成；建議重新啟動軟體。\r\n"
                        : "無法保存 Recipe，原本設定未變更：\r\n") + exception.Message,
                    "載入失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            finally
            {
                if (File.Exists(temporarySettingsPath)) File.Delete(temporarySettingsPath);
            }

            await ReprocessAppliedDetectionRecipeAsync(
                entry.DisplayName,
                imported.ObjectDetectionParameters[0].Id);
        }

        private async Task ReprocessAppliedDetectionRecipeAsync(
            string recipeName,
            string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                ReportAppliedRecipeProcessingFailure(recipeName, "找不到已套用的檢測參數。");
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null || !HasConfiguredObjectDefinitionSource(definition))
            {
                ReportAppliedRecipeProcessingFailure(recipeName, "參數未指定有效的物件定義來源。");
                return;
            }
            if (systemParameters.RoiRegions == null ||
                !systemParameters.RoiRegions.Any(region => region.Bounds.Width > 0 && region.Bounds.Height > 0))
            {
                ReportAppliedRecipeProcessingFailure(recipeName, "參數沒有有效的 ROI，無法重新處理影像。");
                return;
            }
            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                ReportAppliedRecipeProcessingFailure(recipeName, "參數沒有對應目前物件定義的有效缺陷檢測範圍。");
                return;
            }
            bool hasEnabledCore = parameter.DefectDetectionCores != null &&
                parameter.DefectDetectionCores.Take(4).Any(core => core.Enabled);
            if (!hasEnabledCore && !parameter.DefectFrequencyEnabled && !parameter.DefectLineTextureEnabled)
            {
                ReportAppliedRecipeProcessingFailure(recipeName, "目前沒有啟用任何缺陷條件。");
                return;
            }

            LargeImageSource sharedSource = GetObjectDefinitionSharedImageSource();
            bool hasImage = sharedSource != null ||
                (rightOriginalDisplayControl != null && rightOriginalDisplayControl.HasImage) ||
                (leftOriginalDisplayControl != null && leftOriginalDisplayControl.HasImage);
            if (sharedSource != null)
            {
                sharedSource.ReleaseReference();
            }
            if (!hasImage)
            {
                ReportAppliedRecipeProcessingFailure(recipeName, "目前沒有載入圖片，請先載入圖片後再執行完整流程。");
                return;
            }

            int recipeGeneration = Interlocked.CompareExchange(ref detectionRecipeGeneration, 0, 0);
            CancellationToken cancellationToken = detectionRecipeCancellationTokenSource.Token;
            string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
            var definitionCompletion = new TaskCompletionSource<string>();

            try
            {
                await WaitForPreviousDetectionRecipeRunsAsync(cancellationToken, recipeGeneration);
                statusLabel.Text = "已套用 Recipe：「" + recipeName + "」；正在重新處理影像與物件定義...";
                if (!HasCompletedObjectDefinitionResult(definition, definitionSignature))
                {
                    detectionRecipePendingDefinitionId = definition.Id;
                    detectionRecipeDefinitionCompletion = definitionCompletion;
                    isDetectionRecipeAutoRunActive = true;
                    StartObjectDefinitionProcessing(definition.Id);
                    await WaitForAppliedRecipeDefinitionAsync(
                        definition,
                        definitionSignature,
                        definitionCompletion,
                        cancellationToken,
                        recipeGeneration);
                }
                EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);

                if (!string.IsNullOrWhiteSpace(parameter.FlatFieldMaskPrimaryId))
                {
                    if (objectDetectionFlatFieldResultLabel == null ||
                        objectDetectionFlatFieldResultLabel.IsDisposed)
                    {
                        throw new InvalidOperationException("平場校正設定尚未就緒，無法套用參數保存的 MASK。");
                    }

                    statusLabel.Text = "物件處理完成；正在套用平場校正 MASK 與校正值...";
                    await ApplyObjectDetectionFlatFieldMaskAsync(
                        parameter,
                        objectDetectionFlatFieldResultLabel,
                        null,
                        false,
                        true);
                    EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
                }

                statusLabel.Text = "前置影像處理完成；正在執行所有啟用的缺陷條件...";
                await RunObjectDetectionDefectProcessingAsync(parameter.Id, null);
                EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
                EnsureAppliedRecipeDefectResultsCompleted(parameter);

                if (leftImageTabControl != null &&
                    objectDetectionDefectDisplayTabPages != null &&
                    objectDetectionDefectDisplayTabPages.Length > ObjectDetectionDefectIntegratedDisplayIndex)
                {
                    leftImageTabControl.SelectedTab =
                        objectDetectionDefectDisplayTabPages[ObjectDetectionDefectIntegratedDisplayIndex];
                }
                statusLabel.Text = "Recipe「" + recipeName + "」已套用，影像處理與全部啟用的缺陷檢測均已完成。";
            }
            catch (OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ReportAppliedRecipeProcessingFailure(recipeName, "處理流程已中止。");
                }
            }
            catch (Exception exception)
            {
                ReportAppliedRecipeProcessingFailure(recipeName, exception.Message);
            }
            finally
            {
                if (ReferenceEquals(detectionRecipeDefinitionCompletion, definitionCompletion))
                {
                    isDetectionRecipeAutoRunActive = false;
                    detectionRecipeDefinitionCompletion = null;
                    detectionRecipePendingDefinitionId = null;
                }
            }
        }

        private async Task WaitForPreviousDetectionRecipeRunsAsync(
            CancellationToken cancellationToken,
            int recipeGeneration)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (isDetectionRecipeAutoRunActive ||
                objectDetectionDefectProcessingRequested ||
                objectDetectionFrequencyAnalysisRunning ||
                objectDetectionLineTextureAnalysisRunning)
            {
                EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
                if (elapsed.Elapsed > TimeSpan.FromMinutes(30))
                {
                    throw new TimeoutException("舊的檢測工作未能在 30 分鐘內停止，新的 Recipe 尚未開始運算。");
                }
                await Task.Delay(100, cancellationToken);
            }
        }

        private async Task WaitForAppliedRecipeDefinitionAsync(
            ObjectDefinitionSettings definition,
            string signature,
            TaskCompletionSource<string> completion,
            CancellationToken cancellationToken,
            int recipeGeneration)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!HasCompletedObjectDefinitionResult(definition, signature))
            {
                EnsureDetectionRecipeGenerationIsCurrent(recipeGeneration, cancellationToken);
                if (elapsed.Elapsed > TimeSpan.FromMinutes(30))
                {
                    throw new TimeoutException("物件定義處理超過 30 分鐘，請檢查影像大小與來源設定。");
                }
                if (completion.Task.IsCompleted)
                {
                    string error = await completion.Task;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                        ? "物件定義未能完成，請檢查左下角 MEMO 的處理訊息。"
                        : error);
                }
                if (elapsed.Elapsed > TimeSpan.FromSeconds(2) &&
                    !string.Equals(pendingObjectDefinitionProcessingSignature, signature, StringComparison.Ordinal) &&
                    !string.Equals(activeObjectDefinitionProcessingSignature, signature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("物件定義流程未能啟動，請確認影像來源與 ROI 設定。");
                }
                await Task.WhenAny(completion.Task, Task.Delay(100, cancellationToken));
            }
        }

        private void EnsureAppliedRecipeDefectResultsCompleted(ObjectDetectionParameterSettings parameter)
        {
            bool hasEnabledCore = parameter.DefectDetectionCores != null &&
                parameter.DefectDetectionCores.Take(4).Any(core => core.Enabled);
            if (hasEnabledCore)
            {
                Dictionary<string, ObjectDetectionDefectCoreResult> results;
                if (!objectDetectionDefectCoreResults.TryGetValue(parameter.Id, out results))
                {
                    throw new InvalidOperationException("缺陷核心沒有產生結果，請檢查缺陷檢測範圍與平場校正設定。");
                }
                foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores.Take(4).Where(item => item.Enabled))
                {
                    if (!results.ContainsKey(core.CoreKey))
                    {
                        int coreIndex = Array.IndexOf(ObjectDetectionDefectCoreKeys, core.CoreKey);
                        string coreName = coreIndex >= 0
                            ? GetObjectDetectionDefectCoreLabel(coreIndex)
                            : "缺陷條件";
                        throw new InvalidOperationException(coreName + "未完成運算。");
                    }
                }
            }

            ObjectDetectionFrequencyResult frequencyResult;
            if (parameter.DefectFrequencyEnabled &&
                !TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult))
            {
                throw new InvalidOperationException("頻域異常分析未完成。");
            }
            ObjectDetectionLineTextureResult textureResult;
            if (parameter.DefectLineTextureEnabled &&
                !TryGetCurrentObjectDetectionLineTextureResult(parameter, out textureResult))
            {
                throw new InvalidOperationException("紋理異常分析未完成。");
            }
        }

        private void ReportAppliedRecipeProcessingFailure(string recipeName, string reason)
        {
            string message = "Recipe「" + recipeName + "」已套用，但完整流程未完成：" + reason;
            if (statusLabel != null && !statusLabel.IsDisposed)
            {
                statusLabel.Text = message;
            }
            SetObjectDetectionDefectRegionStatus(message);
            if (!IsDisposed)
            {
                MessageBox.Show(this, message, "自動檢測未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private IList<string> FindInUseDetectionRecipeIds(
            DetectionRecipeCatalogService catalog,
            IList<DetectionRecipeCatalogEntry> entries)
        {
            string activeRecipeId = systemParameters.ActiveDetectionRecipeId;
            if (!string.IsNullOrWhiteSpace(activeRecipeId) &&
                entries.Any(entry => string.Equals(entry.Id, activeRecipeId, StringComparison.Ordinal)))
            {
                return new[] { activeRecipeId };
            }

            IList<DetectionRecipeCatalogEntry> matchingEntries =
                catalog.FindRecipesMatchingSettings(systemParameters);
            if (matchingEntries.Count == 1)
            {
                activeRecipeId = matchingEntries[0].Id;
                systemParameters.ActiveDetectionRecipeId = activeRecipeId;
                try
                {
                    SaveSystemParameters();
                }
                catch (Exception exception)
                {
                    statusLabel.Text = "已辨認目前使用的 Recipe，但無法保存使用狀態：" + exception.Message;
                }
            }

            return matchingEntries.Select(entry => entry.Id).ToList();
        }

    }
}
