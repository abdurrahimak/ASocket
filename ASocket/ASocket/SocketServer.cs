using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace ASocket
{

    public class SocketServer : SocketBase
    {
        private TcpSocketListener _tcpSocketListener;
        private UdpSocket _udpSocketListener;
        private IPEndPoint _localEndPoint;

        private Dictionary<Socket, Peer> _peersBySocket = new Dictionary<Socket, Peer>();
        private Dictionary<EndPoint, Peer> _peersByUdpEndpoint = new Dictionary<EndPoint, Peer>();

        public event Action<Peer> PeerConnected;
        public event Action<Peer> PeerDisconnected;
        public event Action<Peer, byte[]> MessageReceived;

        public SocketServer(ISocketUpdater socketUpdater) : base(socketUpdater)
        {
            _tcpSocketListener = new TcpSocketListener();
            _udpSocketListener = new UdpSocket();

            Register();
        }

        private void Register()
        {
            _tcpSocketListener.Connected += OnTcpSocketConnected;
            _tcpSocketListener.Disconnected += OnTcpSocketDisconnected;
            _tcpSocketListener.MessageReceived += OnTcpSocketMessageReceived;

            _udpSocketListener.MessageReceived += OnUdpSocketMessageReceived;
        }

        private void Unregister()
        {
            _tcpSocketListener.Connected -= OnTcpSocketConnected;
            _tcpSocketListener.Disconnected -= OnTcpSocketDisconnected;
            _tcpSocketListener.MessageReceived -= OnTcpSocketMessageReceived;

            _udpSocketListener.MessageReceived -= OnUdpSocketMessageReceived;
        }

        #region Start
        public void Start(string address, int port)
        {
            Start(new IPEndPoint(IPAddress.Parse(address), port));
        }

        public void Start(IPEndPoint localEndPoint)
        {
            _localEndPoint = localEndPoint;

            AddDispatcherQueue(() =>
            {
                ASocket.Log.Log.Info($"[{nameof(SocketServer)}], Socket Server Starting on {localEndPoint}");
            });
            try
            {
                _tcpSocketListener.Start(_localEndPoint);
                _udpSocketListener.StartServer(_localEndPoint);
                AddDispatcherQueue(() =>
                {
                    ASocket.Log.Log.Info($"[{nameof(SocketServer)}], Socket Server Started on {localEndPoint}");
                });
            }
            catch (Exception ex)
            {
                AddDispatcherQueue(() =>
                {
                    ASocket.Log.Log.Error($"[{nameof(SocketServer)}], Exception occured when Server starting.. \n {ex}");
                });
            }
        }

        public override void Destroy()
        {
            base.Destroy();
            Unregister();
            _tcpSocketListener?.Dispose();
            _udpSocketListener?.Dispose();
        }

        public void Disconnect(Peer peer)
        {
            _tcpSocketListener.Disconnect(peer.TcpSocket);
        }
        #endregion

        #region Send Message
        public void Send(Peer peer, byte[] data, PacketFlag packetFlag)
        {
            var length = peer.SendBuffer.CreateMessage(MessageId.None, data.AsSpan());
            SendMessage(peer, length, packetFlag);
        }

        public void Send(Peer peer, ReadOnlyMemory<byte> data, PacketFlag packetFlag)
        {
            var length = peer.SendBuffer.CreateMessage(MessageId.None, data);
            SendMessage(peer, length, packetFlag);
        }

        public void Send(Peer peer, ReadOnlySpan<byte> data, PacketFlag packetFlag)
        {
            //TODO: 1KB Allocation when send any message. Find it.
            var length = peer.SendBuffer.CreateMessage(MessageId.None, data);
            SendMessage(peer, length, packetFlag);
        }

        public void SendAll(byte[] data, PacketFlag packetFlag)
        {
            foreach (var peerBySocketKvp in _peersBySocket)
            {
                Send(peerBySocketKvp.Value, data, packetFlag);
            }
        }

        public void SendAll(ReadOnlyMemory<byte> data, PacketFlag packetFlag)
        {
            foreach (var peerBySocketKvp in _peersBySocket)
            {
                Send(peerBySocketKvp.Value, data, packetFlag);
            }
        }

        public void SendAll(ReadOnlySpan<byte> data, PacketFlag packetFlag)
        {
            foreach (var peerBySocketKvp in _peersBySocket)
            {
                Send(peerBySocketKvp.Value, data, packetFlag);
            }
        }

        private void SendMessage(Peer peer, int length, PacketFlag packetFlag)
        {
            if (packetFlag == PacketFlag.Tcp)
            {
                _tcpSocketListener.Send(peer.TcpSocket, peer.SendBuffer.BufferArray, length);
            }
            else if (packetFlag == PacketFlag.Udp && peer.UdpReady)
            {
                _udpSocketListener.SendTo(peer.UdpRemoteEndPoint, peer.SendBuffer.BufferArray, length);
            }
        }

        private void SendInternalMessage(Peer peer, ReadOnlySpan<byte> data, PacketFlag packetFlag, MessageId messageId)
        {
            //TODO: 1KB Allocation when send any message. Find it.
            var length = peer.SendBuffer.CreateMessage(messageId, data);
            SendMessage(peer, length, packetFlag);
        }
        #endregion

        #region Event Listeners
        private Dictionary<Peer, CancellationTokenSource> _cancellationTokenSourcesByPeer = new();

        private void OnTcpSocketConnected(Socket socket)
        {
            Peer peer = new Peer();
            peer.SetTcpSocket(socket);
            _peersBySocket.Add(socket, peer);

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSourcesByPeer.Add(peer, cancellationTokenSource);
            Task.Run(async () =>
            {
                var tPeer = peer;
                var mem = new Memory<byte>(new byte[4]);
                mem.Span.Fill(0);
                try
                {

                    while (true)
                    {
                        await Task.Delay(1000, cancellationTokenSource.Token);
                        SendInternalMessage(tPeer, mem.Span, PacketFlag.Tcp, MessageId.Ping);
                    }
                }
                catch (Exception ex)
                {
                    Log.Log.Error($"[SocketServer] [PingTask] {ex} \n {ex.StackTrace}");
                }
            }, cancellationTokenSource.Token);
        }

        private void OnTcpSocketDisconnected(Socket socket)
        {
            if (_peersBySocket.TryGetValue(socket, out var peer))
            {
                if (_cancellationTokenSourcesByPeer.TryGetValue(peer, out var cancellationTokenSource))
                {
                    cancellationTokenSource.Cancel();
                    _cancellationTokenSourcesByPeer.Remove(peer);
                }

                _peersBySocket.Remove(socket);
                if (_peersByUdpEndpoint.ContainsKey(peer.UdpRemoteEndPoint))
                {
                    _peersByUdpEndpoint.Remove(peer.UdpRemoteEndPoint);
                }
                peer.Dispose();
                AddDispatcherQueue(() =>
                {
                    PeerDisconnected?.Invoke(peer);
                });
            }
        }

        private void OnTcpSocketMessageReceived(Socket socket, ReadOnlyMemory<byte> buffer)
        {
            var peer = _peersBySocket[socket];

            if (peer.ReadTcpBuffer.PacketCompleted)
            {
                peer.ReadTcpBuffer.Reset();
            }

            var leftSpan = peer.ReadTcpBuffer.WriteToBuffer(buffer.Span);

            //Process packet.
            while (peer.ReadTcpBuffer.PacketCompleted)
            {
                MessageId messageId = peer.ReadTcpBuffer.MessageID;
                if (messageId == MessageId.UdpInformation)
                {
                    var startIndex = PacketInformation.PacketMessageStartIndex;
                    var addressBytes = peer.ReadTcpBuffer.GetBlockOfBuffer(startIndex, 4);

                    startIndex += 4;
                    var portBytes = peer.ReadTcpBuffer.GetBlockOfBuffer(startIndex, 4);
                    var port = BitConverter.ToInt32(portBytes, 0);
                    var endPoint = new IPEndPoint(new IPAddress(addressBytes), port);
                    peer.SetUdpEndpoint(endPoint);
                    _peersByUdpEndpoint.Add(endPoint, peer);
                    AddDispatcherQueue(() =>
                    {
                        ASocket.Log.Log.Verbose($"[{nameof(SocketServer)}], Peer udp endpoint is {endPoint}");
                        PeerConnected?.Invoke(peer);
                    });
                }
                else
                {
                    var message = peer.ReadTcpBuffer.GetMessage();
                    AddDispatcherQueue(() =>
                    {
                        MessageReceived?.Invoke(peer, message);
                    });
                }
                
                peer.ReadTcpBuffer.Reset();
                leftSpan = peer.ReadTcpBuffer.WriteToBuffer(leftSpan);
            }
        }

        private void OnUdpSocketMessageReceived(EndPoint endPoint, ReadOnlyMemory<byte> buffer)
        {
            if (!_peersByUdpEndpoint.TryGetValue(endPoint, out var peer))
            {
                return;
            }

            if (peer.ReadUdpBuffer.PacketCompleted)
            {
                peer.ReadUdpBuffer.Reset();
            }

            peer.ReadUdpBuffer.WriteToBuffer(buffer.Span);

            if (peer.ReadUdpBuffer.PacketCompleted)
            {
                var message = peer.ReadUdpBuffer.GetMessage();
                AddDispatcherQueue(() =>
                {
                    MessageReceived?.Invoke(peer, message);
                });
            }
        }
        #endregion
    }
}
