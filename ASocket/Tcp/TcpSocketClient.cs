using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace ASocket
{
    internal class TcpSocketClient : IDisposable
    {
        public delegate void MessageReceivedDelegate(ref byte[] buffer, int bytes);
        private class State
        {
            public byte[] Buffer = new byte[PacketInformation.PacketSize];
            public int LastReceivedBytes { get; set; }
            
            public TcpSocketClient TcpSocketClient { get; set; }
        }
        
        private Socket _socket;
        private IPEndPoint _remoteEndPoint;
        private EndPoint _localEndPoint;
        private State _state;

        private bool _logEnable = false;

        private AsyncCallback _connectAsyncCallback;
        private AsyncCallback _receiveAsyncCallback;
        private AsyncCallback _sendAsyncCallback;

        private static Action<State> ConnectedInterval;
        private static Action<State> ConnectionFailedInterval;
        private static Action<State> DisconnectedInterval;
        private static Action<State> MessageReceivedInterval;

        public event Action Connected;
        public event Action ConnectionFailed;
        public event Action Disconnected;
        public event MessageReceivedDelegate MessageReceived;

        public bool IsConnected => _socket != null && _socket.Connected;
        public EndPoint LocalEndPoint => _localEndPoint;
        public EndPoint RemoteEndPoint => _remoteEndPoint;

        public TcpSocketClient(bool logEnabled = false)
        {
            _logEnable = logEnabled;
            _connectAsyncCallback = new AsyncCallback(EndConnect);
            _receiveAsyncCallback = new AsyncCallback(EndReceive);
            _sendAsyncCallback = new AsyncCallback(EndSend);
            _state = new State()
            {
                TcpSocketClient = this,
            };
            Register();
        }

        public void Dispose()
        {
            Unregister();
            SocketDispose();
        }

        private void SocketDispose()
        {
            _socket?.Close();
            _socket?.Dispose();
            _socket = null;
        }

        private void Register()
        {
            ConnectedInterval += OnConnectedInternal;
            ConnectionFailedInterval += OnConnectionFailedInternal;
            DisconnectedInterval += OnDisconnectedInternal;
            MessageReceivedInterval += OnMessageReceivedInternal;
        }

        private void Unregister()
        {
            ConnectedInterval -= OnConnectedInternal;
            ConnectionFailedInterval -= OnConnectionFailedInternal;
            DisconnectedInterval -= OnDisconnectedInternal;
            MessageReceivedInterval -= OnMessageReceivedInternal;
        }
        
        #region Connection
        
        public void Connect(IPEndPoint remoteEndPoint)
        {
            if (IsConnected)
                return;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _remoteEndPoint = remoteEndPoint;
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

            BeginConnect();
        }

        public void Connect(string ipAddress, int port)
        {
            var ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            Connect(ipEndPoint);
        }

        public void Disconnect()
        {
            _socket?.Close();
            //OnDisconnectedInternal(_state);
        }
        
        private void BeginConnect()
        {
            try
            {
                _socket.BeginConnect(_remoteEndPoint, _connectAsyncCallback, this);
            }
            catch (SocketException socketException)
            {
                Log($"Exception when connecting to server.");
                ConnectionFailedInterval?.Invoke(_state);
                throw socketException;
            }
        }

        private void EndConnect(IAsyncResult asyncResult)
        {
            try
            {
                _socket.EndConnect(asyncResult); 
                ConnectedInterval?.Invoke(_state);
                BeginReceive();
                _localEndPoint = _socket.LocalEndPoint;
            }
            catch (SocketException socketException)
            {
                Log($"Exception when connecting to server.");
                ConnectionFailedInterval?.Invoke(_state);
                throw socketException;
            }
        }
        
        #endregion

        #region Send

        public void Send(byte[] data, int length)
        {
            BeginSend(data, length);
        }

        private void BeginSend(byte[] data, int length)
        {
            //TODO: Handle exceptions
            try
            {
                _socket.BeginSend(data, 0, length, SocketFlags.None, _sendAsyncCallback, _state);
            }
            catch (SocketException socketException)
            {
                throw socketException;
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                throw argumentOutOfRangeException;
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Log($"Socket dispossed.");
            }
        }
        
        private void EndSend(IAsyncResult ar)
        {
            int bytes = _socket.EndSend(ar, out var errorCode);
            //TODO: Handle Error code
            //Log($"EndSend Error Code is {errorCode}");
        }
        
        #endregion

        #region Receive

        private void BeginReceive()
        {
            //TODO: Handle exceptions
            try
            {
                _socket.BeginReceive(_state.Buffer, 0, _state.Buffer.Length, SocketFlags.None, _receiveAsyncCallback, _state);
            }
            catch (SocketException socketException)
            {
                DisconnectedInterval?.Invoke(_state);
                return;
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                throw argumentOutOfRangeException;
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Log($"Socket dispossed.");
            }
        }
        
        private void EndReceive(IAsyncResult ar)
        {
            try
            {
                State state = (State)ar.AsyncState;
                int bytes = _socket.EndReceive(ar, out var errorCode);
                if (bytes > 0)
                {
                    state.LastReceivedBytes = bytes;
                    //Log($"Error code when Receive : {errorCode}");
                    //TODO : Update this lines with use the socket error.
                    MessageReceivedInterval?.Invoke(state);
                }
                BeginReceive();
            }
            catch (NullReferenceException nullReferenceException)
            {
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                DisconnectedInterval?.Invoke(_state);
            }
        }
        
        #endregion

        #region Event Listeners

        private void OnConnectedInternal(State state)
        {
            if (state.TcpSocketClient != this)
                return;
            Log($"Connected.");
            Connected?.Invoke();
        }

        private void OnConnectionFailedInternal(State state)
        {
            if (state.TcpSocketClient != this)
                return;
            
            Log($"ConnectionFailed.");
            ConnectionFailed?.Invoke();
        }

        private void OnDisconnectedInternal(State state)
        {
            if (state.TcpSocketClient != this)
                return;

            Log($"Disconnected.");
            SocketDispose();
            Disconnected?.Invoke();
        }

        private void OnMessageReceivedInternal(State state)
        {
            if (state.TcpSocketClient != this)
                return;

            var epFrom = _socket.RemoteEndPoint;
            var bytes = state.LastReceivedBytes;
            var stringMessage = Encoding.ASCII.GetString(state.Buffer, 0, bytes);
            //Log($"RECV: {epFrom}: {bytes}, {stringMessage}");
            MessageReceived?.Invoke(ref state.Buffer, state.LastReceivedBytes);
        }
        
        #endregion

        private void Log(string log)
        {
            if (_logEnable)
            {
                Console.WriteLine($"[{nameof(TcpSocketClient)}], {log}");
            }
        }
    }
}
