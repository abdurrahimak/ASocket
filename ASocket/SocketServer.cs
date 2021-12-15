using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
namespace ASocket
{
    public class SocketServer
    {
        private TcpSocketListener _tcpSocketListener;
        private UdpSocket _udpSocketListener;
        private IPEndPoint _localEndPoint;
        private bool _logEnable = true;

        private Dictionary<Socket, Peer> _peersBySocket = new Dictionary<Socket, Peer>();
        private Dictionary<EndPoint, Peer> _peersByUdpEndpoint = new Dictionary<EndPoint, Peer>();

        public Action<Peer> PeerConnected;
        public Action<Peer> PeerDisconnected;
        public Action<Peer, byte[]> MessageReceived;

        public SocketServer(bool logEnabled = false)
        {
            _logEnable = logEnabled;
            _tcpSocketListener = new TcpSocketListener(_logEnable);
            _udpSocketListener = new UdpSocket(_logEnable);

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

            try
            {
                _tcpSocketListener.Start(_localEndPoint);
                _udpSocketListener.StartServer(_localEndPoint);
            }
            catch (Exception ex)
            {
                Log($"Exception occured when Server starting..");
                Log(ex.ToString());
            }
        }

        public void Destroy()
        {
            Unregister();
        }

        public void Disconnect(Peer peer)
        {
            _tcpSocketListener.Disconnect(peer.TcpSocket);
        }
        
        #endregion

        #region Send Message

        public void Send(Peer peer, byte[] data, PacketFlag packetFlag)
        {
            var length= peer.SendBuffer.SetMessage(MessageId.None, data);
            if (packetFlag == PacketFlag.Tcp)
            {
                _tcpSocketListener.Send(peer.TcpSocket, peer.SendBuffer.Buffer, length);
            }
            else if(packetFlag == PacketFlag.Udp && peer.UdpReady)
            {
                _udpSocketListener.SendTo(peer.UdpRemoteEndPoint, peer.SendBuffer.Buffer, length);
            }
        }

        public void SendAll(byte[] data, PacketFlag packetFlag)
        {
            foreach (var peerBySocketKvp in _peersBySocket)
            {
                Send(peerBySocketKvp.Value, data, packetFlag);
            }
        }
        
        #endregion

        #region Event Listeners
        private void OnTcpSocketConnected(Socket socket)
        {
            Peer peer = new Peer();
            peer.SetTcpSocket(socket);
            _peersBySocket.Add(socket, peer);
        }

        private void OnTcpSocketDisconnected(Socket socket)
        {
            if (_peersBySocket.TryGetValue(socket, out var peer))
            {
                _peersBySocket.Remove(socket);
                if (_peersByUdpEndpoint.ContainsKey(peer.UdpRemoteEndPoint))
                {
                    _peersByUdpEndpoint.Remove(peer.UdpRemoteEndPoint);
                }
                PeerDisconnected?.Invoke(peer);
            }
        }

        private void OnTcpSocketMessageReceived(Socket socket, ref byte[] buffer, int bytes)
        {
            var peer = _peersBySocket[socket];

            if (peer.ReadTcpBuffer.PacketCompleted)
            {
                peer.ReadTcpBuffer.Reset();
            }

            peer.ReadTcpBuffer.WriteBuffer(buffer, bytes);

            if (peer.ReadTcpBuffer.PacketCompleted)
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
                    Log($"Peer udp endpoint is {endPoint}");
                    _peersByUdpEndpoint.Add(endPoint, peer);
                    PeerConnected?.Invoke(peer);
                }
                else
                {
                    MessageReceived?.Invoke(peer, peer.ReadTcpBuffer.GetMessage());
                }
            }
        }

        private void OnUdpSocketMessageReceived(EndPoint endPoint, ref byte[] buffer, int bytes, EndPoint @from)
        {
            if (!_peersByUdpEndpoint.TryGetValue(endPoint, out var peer))
            {
                return;
            }

            if (peer.ReadUdpBuffer.PacketCompleted)
            {
                peer.ReadUdpBuffer.Reset();
            }

            peer.ReadUdpBuffer.WriteBuffer(buffer, bytes);

            if (peer.ReadUdpBuffer.PacketCompleted)
            {
                MessageReceived?.Invoke(peer, peer.ReadUdpBuffer.GetMessage());
            }
        }
        #endregion

        private void Log(string log)
        {
            if (_logEnable)
            {
                Console.WriteLine($"[{nameof(SocketServer)}], {log}");
            }
        }
    }
}
