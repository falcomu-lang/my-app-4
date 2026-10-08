using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
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

        private void SelectDetectionParameterRecipe()
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

            using (var dialog = new DetectionRecipeSelectionDialog(catalog, entries))
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

                LoadDetectionParameterRecipe(catalog, entry);
            }
        }

        private void LoadDetectionParameterRecipe(
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
                "若舊參數仍在運算，會取消舊運算並清除舊結果。Recipe 原檔與說明不會被修改；\r\n" +
                "目前圖片會保留，且不會自動重新運算。\r\n\r\n" +
                "確定要載入嗎？",
                "載入檢測參數",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            imported.LastImagePath = systemParameters.LastImagePath;
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
                statusLabel.Text = "已套用 Recipe：「" + entry.DisplayName + "」；舊結果已清除，圖片保留，尚未重新運算";
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
            }
            finally
            {
                if (File.Exists(temporarySettingsPath)) File.Delete(temporarySettingsPath);
            }
        }

    }
}
