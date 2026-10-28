using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Services
{
    [DataContract]
    public sealed class InspectionHistoryDetailRecord
    {
        [DataMember(Order = 1)] public string Title { get; set; }
        [DataMember(Order = 2)] public string Measured { get; set; }
        [DataMember(Order = 3)] public string Range { get; set; }
        [DataMember(Order = 4)] public string Condition { get; set; }
        [DataMember(Order = 5)] public string Result { get; set; }
        [DataMember(Order = 6)] public string Description { get; set; }
    }

    [DataContract]
    public sealed class InspectionHistoryObjectRecord
    {
        [DataMember(Order = 1)] public int ObjectNumber { get; set; }
        [DataMember(Order = 2)] public int GridRow { get; set; }
        [DataMember(Order = 3)] public int GridColumn { get; set; }
        [DataMember(Order = 4)] public int BoundsX { get; set; }
        [DataMember(Order = 5)] public int BoundsY { get; set; }
        [DataMember(Order = 6)] public int BoundsWidth { get; set; }
        [DataMember(Order = 7)] public int BoundsHeight { get; set; }
        [DataMember(Order = 8)] public string SizeGrade { get; set; }
        [DataMember(Order = 9)] public string DefectGrade { get; set; }
        [DataMember(Order = 10)] public string CombinedGrade { get; set; }
        [DataMember(Order = 11)] public List<InspectionHistoryDetailRecord> SizeDetails { get; set; }
        [DataMember(Order = 12)] public List<InspectionHistoryDetailRecord> DefectDetails { get; set; }
    }

    [DataContract]
    public sealed class InspectionHistoryBatchRecord
    {
        public InspectionHistoryBatchRecord()
        {
            Objects = new List<InspectionHistoryObjectRecord>();
        }

        [DataMember(Order = 1)] public string BatchId { get; set; }
        [DataMember(Order = 2)] public DateTime InspectedAt { get; set; }
        [DataMember(Order = 3)] public long CameraSequence { get; set; }
        [DataMember(Order = 4)] public string RecipeId { get; set; }
        [DataMember(Order = 5)] public string RecipeName { get; set; }
        [DataMember(Order = 6)] public string RecipeSnapshotPath { get; set; }
        [DataMember(Order = 7)] public string ImagePath { get; set; }
        [DataMember(Order = 8)] public bool JudgementComplete { get; set; }
        [DataMember(Order = 9)] public string UndeterminedReason { get; set; }
        [DataMember(Order = 10)] public int ImageWidth { get; set; }
        [DataMember(Order = 11)] public int ImageHeight { get; set; }
        [DataMember(Order = 12)] public int ObjectCount { get; set; }
        [DataMember(Order = 13)] public int SizeACount { get; set; }
        [DataMember(Order = 14)] public int SizeBCount { get; set; }
        [DataMember(Order = 15)] public int SizeNgCount { get; set; }
        [DataMember(Order = 16)] public int SizePendingCount { get; set; }
        [DataMember(Order = 17)] public int DefectACount { get; set; }
        [DataMember(Order = 18)] public int DefectNgCount { get; set; }
        [DataMember(Order = 19)] public int DefectPendingCount { get; set; }
        [DataMember(Order = 20)] public int CombinedACount { get; set; }
        [DataMember(Order = 21)] public int CombinedBCount { get; set; }
        [DataMember(Order = 22)] public int CombinedNgCount { get; set; }
        [DataMember(Order = 23)] public int CombinedPendingCount { get; set; }
        [DataMember(Order = 24)] public List<InspectionHistoryObjectRecord> Objects { get; set; }
        [DataMember(Order = 25)] public string ParameterId { get; set; }
    }

    /// <summary>
    /// Daily SQLite inspection history. It uses the Windows SQLite runtime so the
    /// application does not need to download or ship another managed provider.
    /// </summary>
    public sealed class InspectionHistoryStore
    {
        private const string SqliteLibrary = "winsqlite3.dll";
        private readonly string applicationRoot;
        private readonly string historyDirectory;
        private readonly string snapshotDirectory;

        public InspectionHistoryStore(string applicationRootDirectory)
        {
            applicationRoot = Path.GetFullPath(applicationRootDirectory ?? AppDomain.CurrentDomain.BaseDirectory);
            historyDirectory = Path.Combine(applicationRoot, "history");
            snapshotDirectory = Path.Combine(historyDirectory, "recipe-snapshots");
        }

        public string SaveRecipeSnapshot(SystemParameterSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            Directory.CreateDirectory(snapshotDirectory);
            string temporaryPath = Path.Combine(Path.GetTempPath(), "inspection-recipe-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                new SystemParameterIniService(temporaryPath).Save(settings);
                SystemParameterSettings normalized = new SystemParameterIniService(temporaryPath).Load();
                normalized.LastImagePath = string.Empty;
                normalized.ActiveDetectionRecipeId = string.Empty;
                new SystemParameterIniService(temporaryPath).Save(normalized);
                byte[] bytes = File.ReadAllBytes(temporaryPath);
                string hash;
                using (SHA256 sha = SHA256.Create())
                {
                    hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                }
                string target = Path.Combine(snapshotDirectory, hash + ".ini");
                if (!File.Exists(target)) File.WriteAllBytes(target, bytes);
                return MakeRelativePath(applicationRoot, target);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        public string ResolveStoredPath(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) return string.Empty;
            return Path.IsPathRooted(relativeOrAbsolutePath)
                ? Path.GetFullPath(relativeOrAbsolutePath)
                : Path.GetFullPath(Path.Combine(applicationRoot, relativeOrAbsolutePath));
        }

        public string GetStoredPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return string.Empty;
            return MakeRelativePath(applicationRoot, Path.GetFullPath(absolutePath));
        }

        public void SaveBatch(InspectionHistoryBatchRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            if (string.IsNullOrWhiteSpace(record.BatchId)) record.BatchId = Guid.NewGuid().ToString("N");
            if (record.InspectedAt == DateTime.MinValue) record.InspectedAt = DateTime.Now;
            if (record.Objects == null) record.Objects = new List<InspectionHistoryObjectRecord>();
            record.ObjectCount = record.Objects.Count;
            string databasePath = GetDailyDatabasePath(record.InspectedAt);
            Directory.CreateDirectory(historyDirectory);
            using (var database = new NativeSqliteDatabase(databasePath))
            {
                EnsureSchema(database);
                database.Execute("BEGIN IMMEDIATE TRANSACTION");
                try
                {
                    database.Execute(
                        "INSERT OR REPLACE INTO batches (batch_id, inspected_at, camera_sequence, recipe_id, recipe_name, recipe_snapshot_path, image_path, judgement_complete, undetermined_reason, image_width, image_height, object_count, size_a, size_b, size_ng, size_pending, defect_a, defect_ng, defect_pending, combined_a, combined_b, combined_ng, combined_pending, parameter_id) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        record.BatchId,
                        record.InspectedAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        record.CameraSequence,
                        record.RecipeId,
                        record.RecipeName,
                        record.RecipeSnapshotPath,
                        record.ImagePath,
                        record.JudgementComplete ? 1 : 0,
                        record.UndeterminedReason,
                        record.ImageWidth,
                        record.ImageHeight,
                        record.ObjectCount,
                        record.SizeACount,
                        record.SizeBCount,
                        record.SizeNgCount,
                        record.SizePendingCount,
                        record.DefectACount,
                        record.DefectNgCount,
                        record.DefectPendingCount,
                        record.CombinedACount,
                        record.CombinedBCount,
                        record.CombinedNgCount,
                        record.CombinedPendingCount,
                        record.ParameterId);
                    database.Execute("DELETE FROM objects WHERE batch_id=?", record.BatchId);
                    foreach (InspectionHistoryObjectRecord item in record.Objects)
                    {
                        database.Execute(
                            "INSERT INTO objects (batch_id, object_number, grid_row, grid_column, bounds_x, bounds_y, bounds_width, bounds_height, size_grade, defect_grade, combined_grade, size_details_json, defect_details_json) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                            record.BatchId,
                            item.ObjectNumber,
                            item.GridRow,
                            item.GridColumn,
                            item.BoundsX,
                            item.BoundsY,
                            item.BoundsWidth,
                            item.BoundsHeight,
                            item.SizeGrade,
                            item.DefectGrade,
                            item.CombinedGrade,
                            Serialize(item.SizeDetails ?? new List<InspectionHistoryDetailRecord>()),
                            Serialize(item.DefectDetails ?? new List<InspectionHistoryDetailRecord>()));
                    }
                    database.Execute("COMMIT");
                }
                catch
                {
                    try { database.Execute("ROLLBACK"); } catch { }
                    throw;
                }
            }
        }

        public IList<InspectionHistoryBatchRecord> LoadBatches(DateTime date)
        {
            string path = GetDailyDatabasePath(date);
            if (!File.Exists(path)) return new List<InspectionHistoryBatchRecord>();
            using (var database = new NativeSqliteDatabase(path))
            {
                EnsureSchema(database);
                List<object[]> rows = database.Query(
                    "SELECT batch_id, inspected_at, camera_sequence, recipe_id, recipe_name, recipe_snapshot_path, image_path, judgement_complete, undetermined_reason, image_width, image_height, object_count, size_a, size_b, size_ng, size_pending, defect_a, defect_ng, defect_pending, combined_a, combined_b, combined_ng, combined_pending, parameter_id FROM batches ORDER BY inspected_at DESC");
                return rows.Select(MapBatch).ToList();
            }
        }

        public InspectionHistoryBatchRecord LoadBatch(DateTime date, string batchId)
        {
            if (string.IsNullOrWhiteSpace(batchId)) return null;
            string path = GetDailyDatabasePath(date);
            if (!File.Exists(path)) return null;
            using (var database = new NativeSqliteDatabase(path))
            {
                EnsureSchema(database);
                List<object[]> rows = database.Query(
                    "SELECT batch_id, inspected_at, camera_sequence, recipe_id, recipe_name, recipe_snapshot_path, image_path, judgement_complete, undetermined_reason, image_width, image_height, object_count, size_a, size_b, size_ng, size_pending, defect_a, defect_ng, defect_pending, combined_a, combined_b, combined_ng, combined_pending, parameter_id FROM batches WHERE batch_id=? LIMIT 1",
                    batchId);
                if (rows.Count == 0) return null;
                InspectionHistoryBatchRecord record = MapBatch(rows[0]);
                foreach (object[] row in database.Query(
                    "SELECT object_number, grid_row, grid_column, bounds_x, bounds_y, bounds_width, bounds_height, size_grade, defect_grade, combined_grade, size_details_json, defect_details_json FROM objects WHERE batch_id=? ORDER BY object_number",
                    batchId))
                {
                    record.Objects.Add(new InspectionHistoryObjectRecord
                    {
                        ObjectNumber = Int(row[0]), GridRow = Int(row[1]), GridColumn = Int(row[2]),
                        BoundsX = Int(row[3]), BoundsY = Int(row[4]), BoundsWidth = Int(row[5]), BoundsHeight = Int(row[6]),
                        SizeGrade = Text(row[7]), DefectGrade = Text(row[8]), CombinedGrade = Text(row[9]),
                        SizeDetails = DeserializeDetails(Text(row[10])), DefectDetails = DeserializeDetails(Text(row[11]))
                    });
                }
                return record;
            }
        }

        private string GetDailyDatabasePath(DateTime date)
        {
            return Path.Combine(historyDirectory, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".db");
        }

        private static void EnsureSchema(NativeSqliteDatabase database)
        {
            database.Execute("PRAGMA journal_mode=WAL");
            database.Execute("PRAGMA synchronous=NORMAL");
            database.Execute("PRAGMA foreign_keys=ON");
            database.Execute("CREATE TABLE IF NOT EXISTS batches (batch_id TEXT PRIMARY KEY, inspected_at TEXT NOT NULL, camera_sequence INTEGER NOT NULL, recipe_id TEXT, recipe_name TEXT, recipe_snapshot_path TEXT, image_path TEXT, judgement_complete INTEGER NOT NULL, undetermined_reason TEXT, image_width INTEGER NOT NULL, image_height INTEGER NOT NULL, object_count INTEGER NOT NULL, size_a INTEGER NOT NULL, size_b INTEGER NOT NULL, size_ng INTEGER NOT NULL, size_pending INTEGER NOT NULL, defect_a INTEGER NOT NULL, defect_ng INTEGER NOT NULL, defect_pending INTEGER NOT NULL, combined_a INTEGER NOT NULL, combined_b INTEGER NOT NULL, combined_ng INTEGER NOT NULL, combined_pending INTEGER NOT NULL, parameter_id TEXT)");
            bool hasParameterId = database.Query("PRAGMA table_info(batches)")
                .Any(row => row.Length > 1 && string.Equals(
                    Text(row[1]), "parameter_id", StringComparison.OrdinalIgnoreCase));
            if (!hasParameterId)
            {
                database.Execute("ALTER TABLE batches ADD COLUMN parameter_id TEXT");
            }
            database.Execute("CREATE TABLE IF NOT EXISTS objects (id INTEGER PRIMARY KEY AUTOINCREMENT, batch_id TEXT NOT NULL, object_number INTEGER NOT NULL, grid_row INTEGER NOT NULL, grid_column INTEGER NOT NULL, bounds_x INTEGER NOT NULL, bounds_y INTEGER NOT NULL, bounds_width INTEGER NOT NULL, bounds_height INTEGER NOT NULL, size_grade TEXT NOT NULL, defect_grade TEXT NOT NULL, combined_grade TEXT NOT NULL, size_details_json TEXT NOT NULL, defect_details_json TEXT NOT NULL, FOREIGN KEY(batch_id) REFERENCES batches(batch_id) ON DELETE CASCADE)");
            database.Execute("CREATE INDEX IF NOT EXISTS idx_batches_inspected_at ON batches(inspected_at)");
            database.Execute("CREATE INDEX IF NOT EXISTS idx_objects_batch ON objects(batch_id, object_number)");
        }

        private static InspectionHistoryBatchRecord MapBatch(object[] row)
        {
            DateTime inspectedAt;
            DateTime.TryParse(Text(row[1]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out inspectedAt);
            return new InspectionHistoryBatchRecord
            {
                BatchId = Text(row[0]), InspectedAt = inspectedAt, CameraSequence = Long(row[2]),
                RecipeId = Text(row[3]), RecipeName = Text(row[4]), RecipeSnapshotPath = Text(row[5]), ImagePath = Text(row[6]),
                JudgementComplete = Int(row[7]) != 0, UndeterminedReason = Text(row[8]),
                ImageWidth = Int(row[9]), ImageHeight = Int(row[10]), ObjectCount = Int(row[11]),
                SizeACount = Int(row[12]), SizeBCount = Int(row[13]), SizeNgCount = Int(row[14]), SizePendingCount = Int(row[15]),
                DefectACount = Int(row[16]), DefectNgCount = Int(row[17]), DefectPendingCount = Int(row[18]),
                CombinedACount = Int(row[19]), CombinedBCount = Int(row[20]), CombinedNgCount = Int(row[21]), CombinedPendingCount = Int(row[22]),
                ParameterId = row.Length > 23 ? Text(row[23]) : string.Empty
            };
        }

        private static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static List<InspectionHistoryDetailRecord> DeserializeDetails(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<InspectionHistoryDetailRecord>();
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<InspectionHistoryDetailRecord>));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as List<InspectionHistoryDetailRecord> ??
                        new List<InspectionHistoryDetailRecord>();
                }
            }
            catch { return new List<InspectionHistoryDetailRecord>(); }
        }

        private static string Text(object value) { return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture); }
        private static int Int(object value) { return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        private static long Long(object value) { return value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture); }

        private static string MakeRelativePath(string fromDirectory, string toPath)
        {
            Uri from = new Uri(AppendDirectorySeparator(Path.GetFullPath(fromDirectory)));
            Uri to = new Uri(Path.GetFullPath(toPath));
            return Uri.UnescapeDataString(from.MakeRelativeUri(to).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path : path + Path.DirectorySeparatorChar;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private sealed class NativeSqliteDatabase : IDisposable
        {
            private IntPtr handle;

            public NativeSqliteDatabase(string path)
            {
                IntPtr filename = Utf8Pointer(path);
                try
                {
                    int result = sqlite3_open(filename, out handle);
                    if (result != 0) throw CreateException(result);
                    sqlite3_busy_timeout(handle, 5000);
                }
                finally { MarshalFree(filename); }
            }

            public void Execute(string sql, params object[] values)
            {
                IntPtr statement = Prepare(sql, values);
                try
                {
                    int result = sqlite3_step(statement);
                    if (result != 101 && result != 100) throw CreateException(result);
                    while (result == 100) result = sqlite3_step(statement);
                    if (result != 101) throw CreateException(result);
                }
                finally { sqlite3_finalize(statement); }
            }

            public List<object[]> Query(string sql, params object[] values)
            {
                var rows = new List<object[]>();
                IntPtr statement = Prepare(sql, values);
                try
                {
                    int result;
                    while ((result = sqlite3_step(statement)) == 100)
                    {
                        int count = sqlite3_column_count(statement);
                        var row = new object[count];
                        for (int index = 0; index < count; index++)
                        {
                            switch (sqlite3_column_type(statement, index))
                            {
                                case 1: row[index] = sqlite3_column_int64(statement, index); break;
                                case 2: row[index] = sqlite3_column_double(statement, index); break;
                                case 3: row[index] = ColumnText(statement, index); break;
                                default: row[index] = null; break;
                            }
                        }
                        rows.Add(row);
                    }
                    if (result != 101) throw CreateException(result);
                }
                finally { sqlite3_finalize(statement); }
                return rows;
            }

            private IntPtr Prepare(string sql, object[] values)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sql + "\0");
                IntPtr sqlPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, sqlPointer, bytes.Length);
                IntPtr statement;
                try
                {
                    int result = sqlite3_prepare_v2(handle, sqlPointer, bytes.Length - 1, out statement, IntPtr.Zero);
                    if (result != 0) throw CreateException(result);
                }
                finally { Marshal.FreeHGlobal(sqlPointer); }

                try
                {
                    for (int index = 0; values != null && index < values.Length; index++) Bind(statement, index + 1, values[index]);
                    return statement;
                }
                catch
                {
                    sqlite3_finalize(statement);
                    throw;
                }
            }

            private void Bind(IntPtr statement, int index, object value)
            {
                int result;
                if (value == null)
                {
                    result = sqlite3_bind_null(statement, index);
                }
                else if (value is bool)
                {
                    result = sqlite3_bind_int(statement, index, (bool)value ? 1 : 0);
                }
                else if (value is int || value is short || value is byte)
                {
                    result = sqlite3_bind_int(statement, index, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                }
                else if (value is long)
                {
                    result = sqlite3_bind_int64(statement, index, (long)value);
                }
                else if (value is double || value is float || value is decimal)
                {
                    result = sqlite3_bind_double(statement, index, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                    IntPtr buffer = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
                    try
                    {
                        if (bytes.Length > 0) Marshal.Copy(bytes, 0, buffer, bytes.Length);
                        result = sqlite3_bind_text(statement, index, buffer, bytes.Length, new IntPtr(-1));
                    }
                    finally { Marshal.FreeHGlobal(buffer); }
                }
                if (result != 0) throw CreateException(result);
            }

            private string ColumnText(IntPtr statement, int index)
            {
                IntPtr pointer = sqlite3_column_text(statement, index);
                int length = sqlite3_column_bytes(statement, index);
                if (pointer == IntPtr.Zero || length <= 0) return string.Empty;
                byte[] bytes = new byte[length];
                Marshal.Copy(pointer, bytes, 0, length);
                return Encoding.UTF8.GetString(bytes);
            }

            private Exception CreateException(int result)
            {
                string message = handle == IntPtr.Zero ? "SQLite error " + result : PtrToUtf8(sqlite3_errmsg(handle));
                return new InvalidDataException("歷史資料庫操作失敗：" + message + " (" + result + ")");
            }

            public void Dispose()
            {
                if (handle != IntPtr.Zero)
                {
                    sqlite3_close_v2(handle);
                    handle = IntPtr.Zero;
                }
            }

            private static IntPtr Utf8Pointer(string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes((value ?? string.Empty) + "\0");
                IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                return pointer;
            }

            private static void MarshalFree(IntPtr pointer) { if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer); }

            private static string PtrToUtf8(IntPtr pointer)
            {
                if (pointer == IntPtr.Zero) return string.Empty;
                var bytes = new List<byte>();
                for (int offset = 0; ; offset++)
                {
                    byte value = Marshal.ReadByte(pointer, offset);
                    if (value == 0) break;
                    bytes.Add(value);
                }
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_open(IntPtr filename, out IntPtr database);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_close_v2(IntPtr database);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_prepare_v2(IntPtr database, IntPtr sql, int bytes, out IntPtr statement, IntPtr tail);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_step(IntPtr statement);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_finalize(IntPtr statement);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_bind_null(IntPtr statement, int index);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_bind_int(IntPtr statement, int index, int value);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_bind_double(IntPtr statement, int index, double value);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_bind_text(IntPtr statement, int index, IntPtr value, int bytes, IntPtr destructor);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_column_count(IntPtr statement);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_column_type(IntPtr statement, int index);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern long sqlite3_column_int64(IntPtr statement, int index);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern double sqlite3_column_double(IntPtr statement, int index);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern int sqlite3_column_bytes(IntPtr statement, int index);
            [System.Runtime.InteropServices.DllImport(SqliteLibrary, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private static extern IntPtr sqlite3_errmsg(IntPtr database);
        }
    }
}
