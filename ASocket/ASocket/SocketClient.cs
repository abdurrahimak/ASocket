using System;
using System.Net;
namespace ASocket
{
    public class SocketClient : SocketBase
    {
        private TcpSocketClient _tcpSocketClient;
        private UdpSocket _udpSocket;
        private IPEndPoint _remoteEndPoint;

        public event Action Connected;
        public event Action ConnectionFailed;
        public event Action Disconnected;
        public event Action<byte[]> MessageReceived;

        private PacketBuffer _sendBuffer = new PacketBuffer();
        private PacketBuffer _readTcpBuffer = new PacketBuffer();
        private PacketBuffer _readUdpBuffer = new PacketBuffer();

        public EndPoint LocalTcpEndPoint => _tcpSocketClient.LocalEndPoint;
        public EndPoint RemoteTcpEndPoint => _tcpSocketClient.RemoteEndPoint;
        public EndPoint LocalUdpEndPoint => _udpSocket.LocalEndPoint;

        public SocketClient(ISocketUpdater socketUpdater) : base(socketUpdater)
        {
            _tcpSocketClient = new TcpSocketClient();
            _udpSocket = new UdpSocket();

            Register();
        }

        private void Register()
        {
            _tcpSocketClient.Connected += OnTcpSocketConnected;
            _tcpSocketClient.Disconnected += OnTcpSocketDisconnected;
            _tcpSocketClient.ConnectionFailed += OnTcpSocketConnectionFailed;
            _tcpSocketClient.MessageReceived += OnTcpSocketMessageReceived;

            _udpSocket.MessageReceived += OnUdpSocketMessageReceived;
        }

        private void Unregister()
        {
            _tcpSocketClient.Connected -= OnTcpSocketConnected;
            _tcpSocketClient.Disconnected -= OnTcpSocketDisconnected;
            _tcpSocketClient.MessageReceived -= OnTcpSocketMessageReceived;

            _udpSocket.MessageReceived -= OnUdpSocketMessageReceived;
        }

        #region Start
        public void Connect(string address, int port)
        {
            Connect(new IPEndPoint(IPAddress.Parse(address), port));
        }

        public void Connect(IPEndPoint remoteEndPoint)
        {
            _remoteEndPoint = remoteEndPoint;

            try
            {
                _tcpSocketClient.Connect(_remoteEndPoint);
            }
            catch (Exception ex)
            {
                AddDispatcherQueue(() =>
                {
                    ASocket.Log.Log.Error($"[{nameof(SocketClient)}], Exception occured when Client connecting.. \n {ex}");
                });
                Disconnect();
            }
        }

        public void Disconnect()
        {
            _tcpSocketClient?.Disconnect();
            _udpSocket?.Disconnect();
        }

        public override void Destroy()
        {
            base.Destroy();
            Unregister();
            Disconnect();
            _tcpSocketClient?.Dispose();
            _udpSocket?.Dispose();
        }
        #endregion

        public void Send(byte[] data, PacketFlag packetFlag)
        {
            if (!_tcpSocketClient.IsConnected)
            {
                AddDispatcherQueue(() =>
                {
                    ASocket.Log.Log.Info($"[{nameof(SocketClient)}] [Send], Client not connected.");
                });
                return;
            }

            var messageLength = _sendBuffer.CreateMessage(MessageId.None, data.AsSpan());
            SendMessage(messageLength, packetFlag);
        }

        public void Send(ReadOnlyMemory<byte> data, PacketFlag packetFlag)
        {
            if (!_tcpSocketClient.IsConnected)
            {
                AddDispatcherQueue(() =>
                {
                    ASocket.Log.Log.Info($"[{nameof(SocketClient)}] [Send], Client not connected.");
                });
                return;
            }

            var messageLength = _sendBuffer.CreateMessage(MessageId.None, data.Span);
            SendMessage(messageLength, packetFlag);
        }

        public void Send(ReadOnlySpan<byte> data, PacketFlag packetFlag)
        {
            if (!_tcpSocketClient.IsConnected)
            {
                AddDispatcherQueue(() =>
                {
                    ASocket.Log.Log.Info($"[{nameof(SocketClient)}] [Send], Client not connected.");
                });
                return;
            }

            var messageLength = _sendBuffer.CreateMessage(MessageId.None, data);
            SendMessage(messageLength, packetFlag);
        }

        private void SendMessage(int length, PacketFlag packetFlag)
        {
            if (packetFlag == PacketFlag.Tcp)
            {
                _tcpSocketClient.Send(_sendBuffer.BufferArray, length);
            }
            else if (packetFlag == PacketFlag.Udp)
            {
                _udpSocket.Send(_sendBuffer.BufferArray, length);
            }
        }

        private void SendUdpInformation()
        {
            var udpEndPoint = _udpSocket.LocalEndPoint as IPEndPoint;
            var addressBytes = udpEndPoint.Address.GetAddressBytes();
            var portBytes = BitConverter.GetBytes(udpEndPoint.Port);
            var data = new byte[addressBytes.Length + portBytes.Length];
            System.Buffer.BlockCopy(addressBytes, 0, data, 0, addressBytes.Length);
            System.Buffer.BlockCopy(portBytes, 0, data, addressBytes.Length, portBytes.Length);
            var length = _sendBuffer.CreateMessage(MessageId.UdpInformation, data.AsSpan());
            _tcpSocketClient.Send(_sendBuffer.BufferArray, length);
        }

        #region Event Listeners
        private void OnTcpSocketConnected()
        {
            _udpSocket.StartClient(_remoteEndPoint);
            SendUdpInformation();
            AddDispatcherQueue(() =>
            {
                ASocket.Log.Log.Info($"[{nameof(SocketClient)}] Connected.");
                Connected?.Invoke();
            });
        }

        private void OnTcpSocketConnectionFailed()
        {
            AddDispatcherQueue(() =>
            {
                ASocket.Log.Log.Info($"[{nameof(SocketClient)}] ConnectionFailed.");
                ConnectionFailed?.Invoke();
            });
        }

        private void OnTcpSocketDisconnected()
        {
            AddDispatcherQueue(() =>
            {
                ASocket.Log.Log.Info($"[{nameof(SocketClient)}] Disconnected.");
                Disconnected?.Invoke();
            });
        }

        private void OnTcpSocketMessageReceived(ReadOnlyMemory<byte> buffer)
        {
            if (_readTcpBuffer.PacketCompleted)
            {
                _readTcpBuffer.Reset();
            }

            _readTcpBuffer.WriteToBuffer(buffer.Span);

            if (_readTcpBuffer.PacketCompleted)
            {
                var message = _readTcpBuffer.GetMessage();
                AddDispatcherQueue(() =>
                {
                    MessageReceived?.Invoke(message);
                });
            }
        }

        private void OnUdpSocketMessageReceived(EndPoint endPoint, ref byte[] buffer, int bytes, EndPoint @from)
        {
            if (_readUdpBuffer.PacketCompleted)
            {
                _readUdpBuffer.Reset();
            }

            _readUdpBuffer.WriteToBuffer(buffer, bytes);

            if (_readUdpBuffer.PacketCompleted)
            {
                var message = _readUdpBuffer.GetMessage();
                AddDispatcherQueue(() =>
                {
                    MessageReceived?.Invoke(message);
                });
            }
        }
        #endregion
    }
}
