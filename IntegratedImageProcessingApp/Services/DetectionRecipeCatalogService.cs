using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Services
{
    [DataContract]
    public sealed class DetectionRecipeCatalogEntry
    {
        [DataMember(Name = "id", Order = 1)]
        public string Id { get; set; }

        [DataMember(Name = "name", Order = 2)]
        public string DisplayName { get; set; }

        [DataMember(Name = "description", Order = 3)]
        public string Description { get; set; }

        [DataMember(Name = "addedUtc", Order = 4)]
        public string AddedUtc { get; set; }

        [DataMember(Name = "updatedUtc", Order = 5)]
        public string UpdatedUtc { get; set; }
    }

    [DataContract]
    internal sealed class DetectionRecipeCatalogDocument
    {
        [DataMember(Name = "version", Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "parameterList", Order = 2)]
        public List<DetectionRecipeCatalogEntry> ParameterList { get; set; }
    }

    public sealed class DetectionRecipeCatalogService
    {
        private const int CatalogVersion = 1;
        private readonly string recipeDirectory;
        private readonly string catalogPath;
        private readonly string catalogBackupPath;

        public DetectionRecipeCatalogService(string applicationRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(applicationRootDirectory))
            {
                throw new ArgumentException("應用程式根目錄不可為空。", "applicationRootDirectory");
            }

            recipeDirectory = Path.Combine(applicationRootDirectory, "recipe");
            catalogPath = Path.Combine(recipeDirectory, "parameterList.json");
            catalogBackupPath = catalogPath + ".bak";
        }

        public string RecipeDirectory
        {
            get { return recipeDirectory; }
        }

        public IList<DetectionRecipeCatalogEntry> LoadParameterList()
        {
            Directory.CreateDirectory(recipeDirectory);
            if (!File.Exists(catalogPath) && File.Exists(catalogBackupPath))
            {
                File.Move(catalogBackupPath, catalogPath);
            }
            if (!File.Exists(catalogPath))
            {
                return new List<DetectionRecipeCatalogEntry>();
            }

            DetectionRecipeCatalogDocument document;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(DetectionRecipeCatalogDocument));
                using (FileStream stream = File.OpenRead(catalogPath))
                {
                    document = serializer.ReadObject(stream) as DetectionRecipeCatalogDocument;
                }
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("參數清單 parameterList.json 無法讀取。", exception);
            }

            if (document == null || document.Version != CatalogVersion || document.ParameterList == null)
            {
                throw new InvalidDataException("參數清單格式不支援或內容不完整。請保留 recipe 資料夾後再處理。");
            }

            if (document.ParameterList.Any(entry => !IsValidEntry(entry)) ||
                document.ParameterList.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() !=
                document.ParameterList.Count)
            {
                throw new InvalidDataException("參數清單包含無效或重複的 Recipe 識別碼，未修改原清單。");
            }

            return document.ParameterList.ToList();
        }

        public DetectionRecipeCatalogEntry AddRecipe(
            string sourceFilePath,
            string displayName,
            string description)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("找不到選取的檢測參數檔。", sourceFilePath);
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("請輸入參數名稱。", "displayName");
            }

            ValidateParameterFile(sourceFilePath);
            IList<DetectionRecipeCatalogEntry> entries = LoadParameterList();
            if (entries.Any(item => string.Equals(
                item.DisplayName,
                displayName.Trim(),
                StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("參數名稱已存在，請使用不同名稱。");
            }

            string id = Guid.NewGuid().ToString("N");
            string destinationPath = GetParameterFilePath(id);
            string temporaryPath = destinationPath + ".tmp";
            var entry = new DetectionRecipeCatalogEntry
            {
                Id = id,
                DisplayName = displayName.Trim(),
                Description = description == null ? string.Empty : description.Trim(),
                AddedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                UpdatedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            };
            bool recipeFileCreated = false;

            try
            {
                File.Copy(sourceFilePath, temporaryPath, false);
                ValidateParameterFile(temporaryPath);
                File.Move(temporaryPath, destinationPath);
                recipeFileCreated = true;
                entries.Add(entry);
                SaveParameterList(entries);
            }
            catch
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                if (recipeFileCreated && File.Exists(destinationPath)) File.Delete(destinationPath);
                throw;
            }

            return entry;
        }

        public void UpdateMetadata(string id, string displayName, string description)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("請輸入參數名稱。", "displayName");
            }

            IList<DetectionRecipeCatalogEntry> entries = LoadParameterList();
            DetectionRecipeCatalogEntry entry = entries.FirstOrDefault(
                item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (entry == null)
            {
                throw new InvalidOperationException("找不到要修改的參數資料。");
            }
            if (entries.Any(item => !string.Equals(item.Id, id, StringComparison.Ordinal) &&
                string.Equals(item.DisplayName, displayName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("參數名稱已存在，請使用不同名稱。");
            }

            entry.DisplayName = displayName.Trim();
            entry.Description = description == null ? string.Empty : description.Trim();
            entry.UpdatedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            SaveParameterList(entries);
        }

        public string GetParameterFilePath(DetectionRecipeCatalogEntry entry)
        {
            if (entry == null || !IsValidId(entry.Id))
            {
                throw new InvalidDataException("參數清單中的識別碼無效。");
            }

            return GetParameterFilePath(entry.Id);
        }

        public bool ParameterFileExists(DetectionRecipeCatalogEntry entry)
        {
            return entry != null && IsValidId(entry.Id) && File.Exists(GetParameterFilePath(entry.Id));
        }

        private string GetParameterFilePath(string id)
        {
            return Path.Combine(recipeDirectory, id + ".ini");
        }

        private void SaveParameterList(IList<DetectionRecipeCatalogEntry> entries)
        {
            Directory.CreateDirectory(recipeDirectory);
            string temporaryPath = catalogPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var document = new DetectionRecipeCatalogDocument
                {
                    Version = CatalogVersion,
                    ParameterList = entries.ToList()
                };
                var serializer = new DataContractJsonSerializer(typeof(DetectionRecipeCatalogDocument));
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    serializer.WriteObject(stream, document);
                    stream.Flush();
                }

                if (!File.Exists(catalogPath))
                {
                    File.Move(temporaryPath, catalogPath);
                    return;
                }

                if (File.Exists(catalogBackupPath)) File.Delete(catalogBackupPath);
                File.Move(catalogPath, catalogBackupPath);
                try
                {
                    File.Move(temporaryPath, catalogPath);
                }
                catch
                {
                    if (!File.Exists(catalogPath) && File.Exists(catalogBackupPath))
                    {
                        File.Move(catalogBackupPath, catalogPath);
                    }
                    throw;
                }

                try { File.Delete(catalogBackupPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static void ValidateParameterFile(string filePath)
        {
            SystemParameterSettings settings = new SystemParameterIniService(filePath).Load();
            if (settings == null || settings.ObjectDetectionParameters == null ||
                settings.ObjectDetectionParameters.Count != 1)
            {
                throw new InvalidDataException(
                    "參數檔必須包含且只包含一組檢測參數，請選擇由檢測參數匯出的 INI 檔。");
            }
        }

        private static bool IsValidEntry(DetectionRecipeCatalogEntry entry)
        {
            return entry != null && IsValidId(entry.Id) && !string.IsNullOrWhiteSpace(entry.DisplayName);
        }

        private static bool IsValidId(string id)
        {
            Guid parsed;
            return !string.IsNullOrWhiteSpace(id) && Guid.TryParseExact(id, "N", out parsed);
        }
    }
}
