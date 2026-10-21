using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace IntegratedImageProcessingApp.Services
{
    [XmlRoot("ConnectionControllerSettings")]
    public sealed class ConnectionControllerSettings
    {
        public ConnectionControllerSettings()
        {
            ConnectWhenOpened = false;
            AutoReconnect = true;
            ConnectTimeoutSeconds = 5;
            ReconnectIntervalSeconds = 3;
            HeartbeatTimeoutSeconds = 10;
            Stations = new List<ConnectionControllerStation>();
        }

        public bool ConnectWhenOpened { get; set; }

        public bool AutoReconnect { get; set; }

        public int ConnectTimeoutSeconds { get; set; }

        public int ReconnectIntervalSeconds { get; set; }

        public int HeartbeatTimeoutSeconds { get; set; }

        [XmlArrayItem("Station")]
        public List<ConnectionControllerStation> Stations { get; set; }
    }

    public sealed class ConnectionControllerStation
    {
        public ConnectionControllerStation()
        {
            Enabled = false;
            SoftwareId = 1;
            Name = string.Empty;
            IpAddress = string.Empty;
            Port = 5000;
        }

        public bool Enabled { get; set; }

        public int SoftwareId { get; set; }

        public string Name { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }
    }

    public sealed class ConnectionControllerSettingsService
    {
        private readonly string filePath;

        public ConnectionControllerSettingsService(string filePath)
        {
            this.filePath = filePath;
        }

        public ConnectionControllerSettings Load()
        {
            if (!File.Exists(filePath))
            {
                return new ConnectionControllerSettings();
            }

            var serializer = new XmlSerializer(typeof(ConnectionControllerSettings));
            using (var stream = File.OpenRead(filePath))
            {
                var settings = serializer.Deserialize(stream) as ConnectionControllerSettings;
                if (settings == null)
                {
                    return new ConnectionControllerSettings();
                }

                if (settings.Stations == null)
                {
                    settings.Stations = new List<ConnectionControllerStation>();
                }
                return settings;
            }
        }

        public void Save(ConnectionControllerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new XmlSerializer(typeof(ConnectionControllerSettings));
            using (var stream = File.Create(filePath))
            {
                serializer.Serialize(stream, settings);
            }
        }
    }
}
