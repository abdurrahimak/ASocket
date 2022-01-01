using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ASocket
{
    internal class UdpSocket : IDisposable
    {
        private enum UdpType
        {
            None = 0,
            Listener,
            Client
        }
        
        private Socket _socket;
        private State _state = new State();
        private EndPoint _endPointFrom = new IPEndPoint(IPAddress.Any, 0);
        private UdpType _udpType = UdpType.None;
        
        private AsyncCallback _receiveCallback = null;
        private AsyncCallback _sendCallback = null;

        public EndPoint LocalEndPoint { get; private set; }
        private EndPoint _bindEndPoint;
        public event Action<EndPoint, ReadOnlyMemory<byte>> MessageReceived;

        private class State
        {
            public byte[] Buffer = new byte[PacketInformation.PacketSize];
            public ArraySegment<byte> SegmentBuffer;

            public State()
            {
                SegmentBuffer = new ArraySegment<byte>(Buffer);
            }
        }

        public UdpSocket()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            _receiveCallback = new AsyncCallback(EndReceive);
            _sendCallback = new AsyncCallback(EndSend);
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }

        public void StartServer(string address, int port)
        {
            StartServer(new IPEndPoint(IPAddress.Parse(address), port));
        }

        public void StartServer(IPEndPoint endPoint)
        {
            try
            {
                _bindEndPoint = endPoint;
                _udpType = UdpType.Listener;
                
                //TODO IMMEDIATE: This codes running on windows. control after ios & android.
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    byte[] inValue = new byte[] {0};
                    byte[] outValue = new byte[] {0};
                    _socket.IOControl(SIO_UDP_CONNRESET, inValue, outValue);
                }
                
                _socket.Bind(endPoint);
                ASocket.Log.Log.Info($"[{nameof(UdpSocket)}], Udp Server Listening on {endPoint}");
                Receive();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void StartClient(string address, int port)
        {
            StartClient(new IPEndPoint(IPAddress.Parse(address), port));
        }

        public void StartClient(IPEndPoint endPoint)
        {
            _udpType = UdpType.Client;
            _socket.Connect(endPoint);
            LocalEndPoint = _socket.LocalEndPoint;
            Receive();
        }
        
        public void Disconnect()
        {
            _socket.Close();
        }

        #region Send

        /// <summary>
        /// Client Interface
        /// </summary>
        /// <param name="data">data to be sent to the server</param>
        /// <param name="length">Data length</param>
        /// <exception cref="Exception"></exception>
        public void Send(byte[] data, int length)
        {
            if (_udpType == UdpType.Listener)
            {
                throw new Exception($"Send Interface only the clients. If socket is listener then use the SendTo interface");
            }
            if (!_socket.Connected)
            {
                return;
            }
            try
            {
                _socket.BeginSend(data, 0, length, SocketFlags.None, _sendCallback, _state);
            }
            catch (Exception ex)
            {
                ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [Send], {ex}");
            }
        }

        public void Send(ReadOnlyMemory<byte> data)
        {
            if (_udpType == UdpType.Listener)
            {
                throw new Exception($"Send Interface only the clients. If socket is listener then use the SendTo interface");
            }
            if (!_socket.Connected)
            {
                return;
            }
            
            var task = Task.Factory.StartNew(async () =>
            {
                try
                {
                    await _socket.SendAsync(data, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [SendAsync] \n {ex}");
                }
            });
        }
        
        /// <summary>
        /// Server Interface
        /// </summary>
        /// <param name="endPoint">data to be sent to the client</param>
        /// <param name="data">Array buffer</param>
        /// <param name="length">Data length</param>
        /// <exception cref="Exception"></exception>
        public void SendTo(EndPoint endPoint, byte[] data, int length)
        {
            if (_udpType == UdpType.Client)
            {
                throw new Exception($"Send Interface only the server. If socket is client then use the Send interface");
            }
            try
            {
                _socket.BeginSendTo(data, 0, length, SocketFlags.None, endPoint, _sendCallback, _state);
            }
            catch (Exception ex)
            {
                ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [SendTo], {ex}");
            }
        }
        
        public void SendTo(EndPoint endPoint, ReadOnlyMemory<byte> data)
        {
            if (_udpType == UdpType.Client)
            {
                throw new Exception($"Send Interface only the server. If socket is client then use the Send interface");
            }
            try
            {
                _socket.BeginSendTo(data.ToArray(), 0, data.Length, SocketFlags.None, endPoint, _sendCallback, _state);
            }
            catch (Exception ex)
            {
                ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [SendTo], {ex}");
            }
        }
        
        private void EndSend(IAsyncResult ar)
        {
            try
            {
                _socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [EndSend], {ex}");
            }
        }
        
        #endregion

        #region Receive

        private void Receive()
        {
            try
            {
                _socket.BeginReceiveFrom(_state.Buffer, 0, _state.Buffer.Length, SocketFlags.None, ref _endPointFrom, _receiveCallback, _state);
            }
            catch (Exception ex)
            {
                ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [Receive], {ex}");
            }
        }
        
        private void EndReceive(IAsyncResult ar)
        {
            try
            {
                State state = (State)ar.AsyncState;
                int bytes = _socket.EndReceiveFrom(ar, ref _endPointFrom);
                if (bytes > 0)
                {
                    //Log($"RECV: {_endPointFrom.ToString()}: {bytes}, {Encoding.ASCII.GetString(state.Buffer, 0, bytes)}");
                    MessageReceived?.Invoke(_endPointFrom, state.SegmentBuffer[..bytes].AsMemory());
                }
                Receive();
            }
            catch (Exception ex)
            {
                ASocket.Log.Log.Error($"[{nameof(UdpSocket)}], [EndReceive], {ex}, {ex.StackTrace}");
            }
        }
        
        #endregion
    }
}
