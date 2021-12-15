using System;
using System.Net;
namespace ASocket
{
    public class SocketClient
    {
        private TcpSocketClient _tcpSocketClient;
        private UdpSocket _udpSocket;
        private IPEndPoint _remoteEndPoint;
        private bool _logEnable = true;

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

        public SocketClient(bool logEnabled = false)
        {
            _logEnable = logEnabled;
            _tcpSocketClient = new TcpSocketClient(_logEnable);
            _udpSocket = new UdpSocket(_logEnable);

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
                Log($"Exception occured when Client connectiing..");
                Log(ex.ToString());
                _tcpSocketClient.Disconnect();
                _udpSocket?.Disconnect();
            }
        }

        public void Disconnect()
        {
            _tcpSocketClient.Disconnect();
            _udpSocket?.Disconnect();
        }

        public void Destroy()
        {
            Unregister();
        }
        #endregion

        public void Send(byte[] data, PacketFlag packetFlag)
        {
            if (!_tcpSocketClient.IsConnected)
            {
                return;
            }
            
            var length = _sendBuffer.SetMessage(MessageId.None, data);
            if (packetFlag == PacketFlag.Tcp)
            {
                _tcpSocketClient.Send(_sendBuffer.Buffer, length);
            }
            else if(packetFlag == PacketFlag.Udp)
            {
                _udpSocket.Send(_sendBuffer.Buffer, length);
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
            var length = _sendBuffer.SetMessage(MessageId.UdpInformation, data);
            _tcpSocketClient.Send(_sendBuffer.Buffer, length);
        }

        #region Event Listeners
        private void OnTcpSocketConnected()
        {
            _udpSocket.StartClient(_remoteEndPoint);
            SendUdpInformation();
            Connected?.Invoke();
        }
        
        private void OnTcpSocketConnectionFailed()
        {
            ConnectionFailed?.Invoke();
        }

        private void OnTcpSocketDisconnected()
        {
            Disconnected?.Invoke();
        }

        private void OnTcpSocketMessageReceived(ref byte[] buffer, int bytes)
        {
            if (_readTcpBuffer.PacketCompleted)
            {
                _readTcpBuffer.Reset();
            }

            _readTcpBuffer.WriteBuffer(buffer, bytes);

            if (_readTcpBuffer.PacketCompleted)
            {
                MessageReceived?.Invoke(_readTcpBuffer.GetMessage());
            }
        }

        private void OnUdpSocketMessageReceived(EndPoint endPoint, ref byte[] buffer, int bytes, EndPoint @from)
        {
            if (_readUdpBuffer.PacketCompleted)
            {
                _readUdpBuffer.Reset();
            }

            _readUdpBuffer.WriteBuffer(buffer, bytes);

            if (_readUdpBuffer.PacketCompleted)
            {
                MessageReceived?.Invoke(_readUdpBuffer.GetMessage());
            }
        }
        
        #endregion

        private void Log(string log)
        {
            if (_logEnable)
            {
                Console.WriteLine($"[{nameof(SocketClient)}], {log}");
            }
        }
    }
}
