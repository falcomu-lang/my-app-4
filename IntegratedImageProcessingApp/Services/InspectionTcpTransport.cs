using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntegratedImageProcessingApp.Services
{
    [DataContract]
    public sealed class InspectionTcpMessage
    {
        public InspectionTcpMessage()
        {
            MessageId = Guid.NewGuid().ToString("N");
            SentAtUtc = DateTime.UtcNow.ToString("o");
            Objects = new List<InspectionTcpObjectResult>();
        }

        [DataMember(Order = 1)] public string MessageType { get; set; }
        [DataMember(Order = 2)] public string MessageId { get; set; }
        [DataMember(Order = 3)] public string CommandId { get; set; }
        [DataMember(Order = 4)] public string Command { get; set; }
        [DataMember(Order = 5)] public int StationId { get; set; }
        [DataMember(Order = 6)] public string StationName { get; set; }
        [DataMember(Order = 7)] public string SentAtUtc { get; set; }
        [DataMember(Order = 8)] public string Status { get; set; }
        [DataMember(Order = 9)] public bool CameraConnected { get; set; }
        [DataMember(Order = 10)] public bool IsCapturing { get; set; }
        [DataMember(Order = 11)] public bool JudgementComplete { get; set; }
        [DataMember(Order = 12)] public string RecipeId { get; set; }
        [DataMember(Order = 13)] public string RecipeName { get; set; }
        [DataMember(Order = 14)] public int ObjectCount { get; set; }
        [DataMember(Order = 15)] public int ACount { get; set; }
        [DataMember(Order = 16)] public int BCount { get; set; }
        [DataMember(Order = 17)] public int NgCount { get; set; }
        [DataMember(Order = 18)] public int PendingCount { get; set; }
        [DataMember(Order = 19)] public List<InspectionTcpObjectResult> Objects { get; set; }
        [DataMember(Order = 20)] public bool Accepted { get; set; }
        [DataMember(Order = 21)] public string ResultAtUtc { get; set; }
    }

    [DataContract]
    public sealed class InspectionTcpObjectResult
    {
        [DataMember(Order = 1)] public int Number { get; set; }
        [DataMember(Order = 2)] public int Row { get; set; }
        [DataMember(Order = 3)] public int Column { get; set; }
        [DataMember(Order = 4)] public string SizeGrade { get; set; }
        [DataMember(Order = 5)] public string DefectGrade { get; set; }
        [DataMember(Order = 6)] public string CombinedGrade { get; set; }
    }

    public sealed class InspectionTcpMessageEventArgs : EventArgs
    {
        public InspectionTcpMessageEventArgs(
            string peerKey,
            int? configuredStationId,
            InspectionTcpMessage message,
            bool isOutgoing)
        {
            PeerKey = peerKey;
            ConfiguredStationId = configuredStationId;
            Message = message;
            IsOutgoing = isOutgoing;
        }

        public string PeerKey { get; private set; }
        public int? ConfiguredStationId { get; private set; }
        public InspectionTcpMessage Message { get; private set; }
        public bool IsOutgoing { get; private set; }
    }

    public sealed class InspectionTcpPeerEventArgs : EventArgs
    {
        public InspectionTcpPeerEventArgs(string peerKey, int? stationId, bool connected)
        {
            PeerKey = peerKey;
            StationId = stationId;
            Connected = connected;
        }

        public string PeerKey { get; private set; }
        public int? StationId { get; private set; }
        public bool Connected { get; private set; }
    }

    public sealed class InspectionTcpTransport : IDisposable
    {
        private sealed class PeerConnection : IDisposable
        {
            public string Key;
            public int? StationId;
            public bool IsOutgoing;
            public TcpClient Client;
            public StreamReader Reader;
            public StreamWriter Writer;
            public SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);
            public DateTime LastReceivedUtc = DateTime.UtcNow;
            public int Closed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref Closed, 1) != 0)
                {
                    return;
                }
                try { Client.Close(); } catch { }
                try { Reader.Dispose(); } catch { }
                try { Writer.Dispose(); } catch { }
                WriteLock.Dispose();
            }
        }

        private readonly object peersLock = new object();
        private readonly Dictionary<string, PeerConnection> peers =
            new Dictionary<string, PeerConnection>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> outgoingPeerKeys = new Dictionary<int, string>();
        private readonly Timer heartbeatTimer;
        private TcpListener listener;
        private CancellationTokenSource listenerCancellation;
        private int heartbeatTimeoutSeconds = 15;
        private bool disposed;

        public InspectionTcpTransport()
        {
            heartbeatTimer = new Timer(HeartbeatTimer_Tick, null, 2000, 2000);
        }

        public event EventHandler<InspectionTcpMessageEventArgs> MessageReceived;
        public event EventHandler<InspectionTcpPeerEventArgs> PeerStateChanged;

        public void SetHeartbeatTimeout(int seconds)
        {
            Interlocked.Exchange(ref heartbeatTimeoutSeconds, Math.Max(3, Math.Min(300, seconds)));
        }

        public void StartListener(IPAddress address, int port)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("InspectionTcpTransport");
            }
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (port < 1024 || port > 65535)
            {
                throw new ArgumentOutOfRangeException("port");
            }
            StopListener();
            var nextListener = new TcpListener(address, port);
            nextListener.Start(20);
            var cancellation = new CancellationTokenSource();
            lock (peersLock)
            {
                listener = nextListener;
                listenerCancellation = cancellation;
            }
            Task.Run(() => AcceptLoopAsync(nextListener, cancellation.Token));
        }

        public void StopListener()
        {
            TcpListener currentListener;
            CancellationTokenSource cancellation;
            List<PeerConnection> incomingPeers;
            lock (peersLock)
            {
                currentListener = listener;
                listener = null;
                cancellation = listenerCancellation;
                listenerCancellation = null;
                incomingPeers = peers.Values.Where(peer => !peer.IsOutgoing).ToList();
            }
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
            if (currentListener != null)
            {
                try { currentListener.Stop(); } catch { }
            }
            foreach (PeerConnection peer in incomingPeers)
            {
                RemovePeer(peer);
            }
        }

        public bool IsStationConnected(int stationId)
        {
            lock (peersLock)
            {
                string key;
                PeerConnection peer;
                return outgoingPeerKeys.TryGetValue(stationId, out key) &&
                    peers.TryGetValue(key, out peer) && Volatile.Read(ref peer.Closed) == 0;
            }
        }

        public async Task<bool> ConnectToStationAsync(
            int stationId,
            string address,
            int port,
            int timeoutSeconds)
        {
            if (disposed || stationId < 1 || stationId > 10 || port < 1 || port > 65535)
            {
                return false;
            }
            IPAddress parsedAddress;
            if (!IPAddress.TryParse(address, out parsedAddress) ||
                parsedAddress.AddressFamily != AddressFamily.InterNetwork ||
                parsedAddress.Equals(IPAddress.Any))
            {
                return false;
            }
            if (IsStationConnected(stationId))
            {
                return true;
            }

            var client = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                Task connectTask = client.ConnectAsync(parsedAddress, port);
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
                if (await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false) != connectTask)
                {
                    client.Close();
                    return false;
                }
                await connectTask.ConfigureAwait(false);
                string key = "station:" + stationId.ToString();
                var peer = CreatePeer(key, stationId, client, true);
                lock (peersLock)
                {
                    if (disposed)
                    {
                        peer.Dispose();
                        return false;
                    }
                    PeerConnection previous;
                    if (peers.TryGetValue(key, out previous))
                    {
                        peers.Remove(key);
                        previous.Dispose();
                    }
                    peers[key] = peer;
                    outgoingPeerKeys[stationId] = key;
                }
                RaisePeerStateChanged(new InspectionTcpPeerEventArgs(key, stationId, true));
                _ = Task.Run(() => ReadLoopAsync(peer));
                await SendToPeerAsync(key, new InspectionTcpMessage
                {
                    MessageType = "heartbeat",
                    StationId = 0
                }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                client.Close();
                return false;
            }
        }

        public Task<bool> SendToStationAsync(int stationId, InspectionTcpMessage message)
        {
            string key;
            lock (peersLock)
            {
                if (!outgoingPeerKeys.TryGetValue(stationId, out key))
                {
                    return Task.FromResult(false);
                }
            }
            return SendToPeerAsync(key, message);
        }

        public async Task<int> BroadcastAsync(InspectionTcpMessage message)
        {
            List<string> keys;
            lock (peersLock)
            {
                keys = new List<string>(peers.Keys);
            }
            int sentCount = 0;
            foreach (string key in keys)
            {
                if (await SendToPeerAsync(key, message).ConfigureAwait(false))
                {
                    sentCount++;
                }
            }
            return sentCount;
        }

        public async Task<bool> SendToPeerAsync(string peerKey, InspectionTcpMessage message)
        {
            PeerConnection peer;
            lock (peersLock)
            {
                if (disposed || !peers.TryGetValue(peerKey, out peer) ||
                    Volatile.Read(ref peer.Closed) != 0)
                {
                    return false;
                }
            }

            try
            {
                string serialized = Serialize(message);
                await peer.WriteLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (Volatile.Read(ref peer.Closed) != 0)
                    {
                        return false;
                    }
                    await peer.Writer.WriteLineAsync(serialized).ConfigureAwait(false);
                    await peer.Writer.FlushAsync().ConfigureAwait(false);
                    return true;
                }
                finally
                {
                    if (Volatile.Read(ref peer.Closed) == 0)
                    {
                        peer.WriteLock.Release();
                    }
                }
            }
            catch
            {
                RemovePeer(peer);
                return false;
            }
        }

        public void DisconnectStation(int stationId)
        {
            string key;
            PeerConnection peer;
            lock (peersLock)
            {
                if (!outgoingPeerKeys.TryGetValue(stationId, out key) || !peers.TryGetValue(key, out peer))
                {
                    return;
                }
            }
            RemovePeer(peer);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            heartbeatTimer.Dispose();
            StopListener();
            List<PeerConnection> currentPeers;
            lock (peersLock)
            {
                currentPeers = new List<PeerConnection>(peers.Values);
                peers.Clear();
                outgoingPeerKeys.Clear();
            }
            foreach (PeerConnection peer in currentPeers)
            {
                peer.Dispose();
            }
        }

        private async Task AcceptLoopAsync(TcpListener currentListener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await currentListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    string key = "incoming:" + Guid.NewGuid().ToString("N");
                    var peer = CreatePeer(key, null, client, false);
                    lock (peersLock)
                    {
                        if (disposed || cancellationToken.IsCancellationRequested)
                        {
                            peer.Dispose();
                            continue;
                        }
                        peers[key] = peer;
                    }
                    RaisePeerStateChanged(new InspectionTcpPeerEventArgs(key, null, true));
                    _ = Task.Run(() => ReadLoopAsync(peer));
                    client = null;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch
                {
                    if (client != null)
                    {
                        client.Close();
                    }
                }
            }
        }

        private async Task ReadLoopAsync(PeerConnection peer)
        {
            try
            {
                while (Volatile.Read(ref peer.Closed) == 0)
                {
                    string line = await peer.Reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null || line.Length > 4 * 1024 * 1024)
                    {
                        break;
                    }
                    InspectionTcpMessage message = Deserialize(line);
                    if (message == null)
                    {
                        continue;
                    }
                    peer.LastReceivedUtc = DateTime.UtcNow;
                    bool peerIdentityLearned = false;
                    if (!peer.StationId.HasValue && message.StationId > 0)
                    {
                        peer.StationId = message.StationId;
                        peerIdentityLearned = true;
                    }
                    if (peerIdentityLearned)
                    {
                        RaisePeerStateChanged(new InspectionTcpPeerEventArgs(
                            peer.Key, peer.StationId, true));
                    }
                    if (!string.Equals(message.MessageType, "heartbeat", StringComparison.Ordinal))
                    {
                        RaiseMessageReceived(new InspectionTcpMessageEventArgs(
                            peer.Key,
                            peer.IsOutgoing ? peer.StationId : null,
                            message,
                            peer.IsOutgoing));
                    }
                }
            }
            catch
            {
            }
            finally
            {
                RemovePeer(peer);
            }
        }

        private void HeartbeatTimer_Tick(object state)
        {
            List<PeerConnection> currentPeers;
            lock (peersLock)
            {
                if (disposed)
                {
                    return;
                }
                currentPeers = new List<PeerConnection>(peers.Values);
            }

            DateTime now = DateTime.UtcNow;
            int timeoutSeconds = Volatile.Read(ref heartbeatTimeoutSeconds);
            foreach (PeerConnection peer in currentPeers)
            {
                if ((now - peer.LastReceivedUtc).TotalSeconds > timeoutSeconds)
                {
                    RemovePeer(peer);
                    continue;
                }
                Task.Run(() => SendToPeerAsync(peer.Key, new InspectionTcpMessage
                {
                    MessageType = "heartbeat"
                }));
            }
        }

        private PeerConnection CreatePeer(string key, int? stationId, TcpClient client, bool isOutgoing)
        {
            NetworkStream stream = client.GetStream();
            return new PeerConnection
            {
                Key = key,
                StationId = stationId,
                IsOutgoing = isOutgoing,
                Client = client,
                Reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true),
                Writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true)
            };
        }

        private void RemovePeer(PeerConnection peer)
        {
            bool removed;
            lock (peersLock)
            {
                removed = peers.Remove(peer.Key);
                if (peer.StationId.HasValue)
                {
                    string outgoingKey;
                    if (outgoingPeerKeys.TryGetValue(peer.StationId.Value, out outgoingKey) &&
                        string.Equals(outgoingKey, peer.Key, StringComparison.Ordinal))
                    {
                        outgoingPeerKeys.Remove(peer.StationId.Value);
                    }
                }
            }
            peer.Dispose();
            if (removed)
            {
                RaisePeerStateChanged(new InspectionTcpPeerEventArgs(peer.Key, peer.StationId, false));
            }
        }

        private void RaiseMessageReceived(InspectionTcpMessageEventArgs e)
        {
            EventHandler<InspectionTcpMessageEventArgs> handler = MessageReceived;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
            }
        }

        private void RaisePeerStateChanged(InspectionTcpPeerEventArgs e)
        {
            EventHandler<InspectionTcpPeerEventArgs> handler = PeerStateChanged;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
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

        private static InspectionTcpMessage Deserialize(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(InspectionTcpMessage));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as InspectionTcpMessage;
            }
        }
    }
}
