using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        
        public delegate void MessageReceivedDelegate(EndPoint endPoint, ref byte[] buffer, int bytes, EndPoint from);
        
        private Socket _socket;
        private State _state = new State();
        private EndPoint _endPointFrom = new IPEndPoint(IPAddress.Any, 0);
        private UdpType _udpType = UdpType.None;
        
        private bool _logEnable = false;
        
        private AsyncCallback _receiveCallback = null;
        private AsyncCallback _sendCallback = null;
        
        public EndPoint LocalEndPoint { get; private set; }
        public event MessageReceivedDelegate MessageReceived;

        private class State
        {
            public byte[] Buffer = new byte[PacketInformation.PacketSize];
        }

        public UdpSocket(bool logEnabled = false)
        {
            _logEnable = logEnabled;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
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
                _udpType = UdpType.Listener;
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
                _socket.Bind(endPoint);
                Receive();
                Log($"Udp Server Listening on {endPoint}");
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
            try
            {
                _socket.BeginSend(data, 0, length, SocketFlags.None, _sendCallback, _state);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
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
                Log(ex.ToString());
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
                Log(ex.ToString());
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
                Log(ex.ToString());
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
                    MessageReceived?.Invoke(_endPointFrom, ref state.Buffer, bytes, _endPointFrom);
                }
                Receive();
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return;
            }
        }
        
        #endregion

        private void Log(string log)
        {
            if (_logEnable)
            {
                Console.WriteLine($"[{nameof(UdpSocket)}] [{_udpType}], {log}");
            }
        }
        public void Disconnect()
        {
            _socket.Close();
        }
    }
}
