using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace IntegratedImageProcessingApp.Services
{
    /// <summary>
    /// Durable sender outbox and receiver de-duplication archive for inspection results.
    /// These files are separate from local inspection history and contain result metadata only.
    /// </summary>
    public sealed class InspectionTcpResultStore
    {
        private readonly object sync = new object();
        private readonly string outboxDirectory;
        private readonly string inboxDirectory;

        public InspectionTcpResultStore(string applicationRoot)
        {
            string root = Path.GetFullPath(applicationRoot ?? AppDomain.CurrentDomain.BaseDirectory);
            outboxDirectory = Path.Combine(root, "tcp-results", "outbox");
            inboxDirectory = Path.Combine(root, "tcp-results", "inbox");
        }

        public void Enqueue(InspectionTcpMessage message)
        {
            if (message == null) throw new ArgumentNullException("message");
            if (string.IsNullOrWhiteSpace(message.ResultId))
            {
                throw new InvalidDataException("檢測結果缺少 Result ID，無法加入 TCP 待送佇列。");
            }

            lock (sync)
            {
                Directory.CreateDirectory(outboxDirectory);
                string path = Path.Combine(outboxDirectory, HashId(message.ResultId) + ".json");
                if (File.Exists(path)) return;
                WriteMessage(path, message);
            }
        }

        public IList<InspectionTcpMessage> LoadPending()
        {
            lock (sync)
            {
                if (!Directory.Exists(outboxDirectory)) return new List<InspectionTcpMessage>();
                var messages = new List<InspectionTcpMessage>();
                foreach (string path in Directory.GetFiles(outboxDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        InspectionTcpMessage message = ReadMessage(path);
                        if (message != null && !string.IsNullOrWhiteSpace(message.ResultId))
                        {
                            messages.Add(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "Unable to read pending inspection result " + path + ": " + exception.Message);
                    }
                }
                return messages.OrderBy(GetResultTime).ToList();
            }
        }

        public void Acknowledge(string resultId)
        {
            if (string.IsNullOrWhiteSpace(resultId)) return;
            lock (sync)
            {
                string path = Path.Combine(outboxDirectory, HashId(resultId) + ".json");
                if (File.Exists(path)) File.Delete(path);
            }
        }

        /// <summary>
        /// Persists the received result before the caller sends its ACK. Repeated IDs
        /// map to the same file, so retransmission is idempotent.
        /// </summary>
        public bool PersistReceived(InspectionTcpMessage message)
        {
            if (message == null) throw new ArgumentNullException("message");
            if (string.IsNullOrWhiteSpace(message.ResultId))
            {
                throw new InvalidDataException("收到的檢測結果缺少 Result ID，不能確認接收。");
            }

            DateTime resultTime = GetResultTime(message);
            string dateDirectory = Path.Combine(
                inboxDirectory, resultTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            lock (sync)
            {
                Directory.CreateDirectory(dateDirectory);
                string path = Path.Combine(dateDirectory, HashId(message.ResultId) + ".json");
                if (File.Exists(path)) return false;
                WriteMessage(path, message);
                return true;
            }
        }

        public IList<InspectionTcpMessage> LoadLatestReceivedByStation()
        {
            lock (sync)
            {
                if (!Directory.Exists(inboxDirectory)) return new List<InspectionTcpMessage>();
                var messages = new List<InspectionTcpMessage>();
                foreach (string path in Directory.GetFiles(inboxDirectory, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        InspectionTcpMessage message = ReadMessage(path);
                        if (message != null && message.StationId > 0)
                        {
                            messages.Add(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "Unable to read received inspection result " + path + ": " + exception.Message);
                    }
                }
                return messages
                    .GroupBy(message => message.StationId)
                    .Select(group => group.OrderByDescending(GetResultTime).First())
                    .ToList();
            }
        }

        private static void WriteMessage(string path, InspectionTcpMessage message)
        {
            string json = Serialize(message);
            AtomicFileWriter.Write(path, writer => writer.Write(json));
        }

        private static InspectionTcpMessage ReadMessage(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var serializer = new DataContractJsonSerializer(typeof(InspectionTcpMessage));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as InspectionTcpMessage;
            }
        }

        private static string Serialize(InspectionTcpMessage message)
        {
            var serializer = new DataContractJsonSerializer(typeof(InspectionTcpMessage));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, message);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static string HashId(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static DateTime GetResultTime(InspectionTcpMessage message)
        {
            DateTime resultTime;
            if (message != null && !string.IsNullOrWhiteSpace(message.ResultAtUtc) &&
                DateTime.TryParse(message.ResultAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out resultTime))
            {
                return resultTime.ToUniversalTime();
            }
            if (message != null && !string.IsNullOrWhiteSpace(message.SentAtUtc) &&
                DateTime.TryParse(message.SentAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out resultTime))
            {
                return resultTime.ToUniversalTime();
            }
            return DateTime.UtcNow;
        }
    }
}
