using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IntegratedImageProcessingApp.Services
{
    public static class InspectionTcpCommandSecurity
    {
        public static string CreateSignature(
            string secret,
            string commandId,
            string command,
            int senderStationId,
            int targetStationId,
            string sentAtUtc)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new ArgumentException("A command authentication secret is required.", "secret");
            }

            string canonical = CreateCanonicalCommand(
                commandId, command, senderStationId, targetStationId, sentAtUtc);
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
            }
        }

        public static bool VerifySignature(
            string secret,
            string commandId,
            string command,
            int senderStationId,
            int targetStationId,
            string sentAtUtc,
            string signature)
        {
            if (string.IsNullOrWhiteSpace(signature)) return false;
            byte[] supplied;
            try
            {
                supplied = Convert.FromBase64String(signature);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(CreateSignature(
                    secret, commandId, command, senderStationId, targetStationId, sentAtUtc));
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (supplied.Length != expected.Length) return false;
            int difference = 0;
            for (int index = 0; index < supplied.Length; index++)
            {
                difference |= supplied[index] ^ expected[index];
            }
            return difference == 0;
        }

        private static string CreateCanonicalCommand(
            string commandId,
            string command,
            int senderStationId,
            int targetStationId,
            string sentAtUtc)
        {
            return string.Join("\n", new[]
            {
                commandId ?? string.Empty,
                (command ?? string.Empty).Trim().ToLowerInvariant(),
                senderStationId.ToString(CultureInfo.InvariantCulture),
                targetStationId.ToString(CultureInfo.InvariantCulture),
                sentAtUtc ?? string.Empty
            });
        }
    }

    /// <summary>
    /// Durable replay protection for signed remote commands. A command ID is reserved
    /// with CreateNew semantics before the command is executed.
    /// </summary>
    public sealed class InspectionTcpCommandReplayGuard
    {
        private readonly object sync = new object();
        private readonly string rootDirectory;

        public InspectionTcpCommandReplayGuard(string applicationRoot)
        {
            rootDirectory = Path.Combine(
                Path.GetFullPath(applicationRoot ?? AppDomain.CurrentDomain.BaseDirectory),
                "tcp-results", "accepted-commands");
        }

        public bool TryRegister(string commandId, DateTime acceptedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 128)
            {
                return false;
            }

            string idFileName = HashId(commandId) + ".id";
            string today = acceptedAtUtc.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string yesterday = acceptedAtUtc.ToUniversalTime().AddDays(-1)
                .ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string tomorrow = acceptedAtUtc.ToUniversalTime().AddDays(1)
                .ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            lock (sync)
            {
                foreach (string day in new[] { today, yesterday, tomorrow })
                {
                    if (File.Exists(Path.Combine(rootDirectory, day, idFileName)))
                    {
                        return false;
                    }
                }

                string directory = Path.Combine(rootDirectory, today);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, idFileName);
                try
                {
                    using (var stream = new FileStream(
                        path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(commandId);
                        writer.Flush();
                        stream.Flush(true);
                    }
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }

        private static string HashId(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
