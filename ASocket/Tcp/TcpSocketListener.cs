using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace ASocket
{
    internal class TcpSocketListener : IDisposable
    {
        public delegate void MessageReceivedDelegate(Socket socket, ref byte[] buffer, int bytes);

        private class ClientState
        {
            public Socket Socket;
            public byte[] Buffer = new byte[PacketInformation.PacketSize];
            public int LastReceivedBytes;

            public TcpSocketListener TcpSocketListener;
            public EndPoint RemoteEndPoint { get; set; }
        }

        private Socket _socket;
        private IPEndPoint _localEP;
        private List<ClientState> _clientStates;

        private static Action<TcpSocketListener, Socket> ConnectionAccepted;
        private static Action<ClientState> MessageReceivedInterval;
        private static Action<ClientState> DisconnectedInternal;

        public event Action<Socket> Connected;
        public event Action<Socket> Disconnected;
        public event MessageReceivedDelegate MessageReceived;

        private AsyncCallback _endAcceptCallback;
        private AsyncCallback _endReceiveCallback;
        private AsyncCallback _endSendCallback;
        private AsyncCallback _endDisconnectCallback;

        public TcpSocketListener()
        {
            _clientStates = new List<ClientState>();
            InitializeCallbacks();
            Register();
        }

        private void InitializeCallbacks()
        {
            _endAcceptCallback = new AsyncCallback(EndAccept);
            _endReceiveCallback = new AsyncCallback(EndReceive);
            _endSendCallback = new AsyncCallback(EndSend);
            _endDisconnectCallback = new AsyncCallback(EndDisconnect);
        }

        private void Register()
        {
            ConnectionAccepted += OnConnectionAccepted;
            MessageReceivedInterval += OnMessageReceivedInterval;
            DisconnectedInternal += OnDisconnectedInternal;
        }

        private void Unregister()
        {
            ConnectionAccepted -= OnConnectionAccepted;
            MessageReceivedInterval -= OnMessageReceivedInterval;
            DisconnectedInternal -= OnDisconnectedInternal;
        }

        #region Binding
        public void Start(string address, int port)
        {
            Start(new IPEndPoint(IPAddress.Parse(address), port));
        }

        public void Dispose()
        {
            Unregister();
            _socket?.Dispose();
            _socket = null;
        }

        public void Start(IPEndPoint localEP)
        {
            try
            {
                _localEP = localEP;
                _socket = new Socket(_localEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Start listening on {_localEP}");
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                _socket.Bind(_localEP);
                _socket.Listen(100);
                ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Started listening on {_localEP}");
                
                BeginAccept();
            }
            catch (SocketException socketException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {socketException}");
                throw socketException;
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {objectDisposedException}");
                throw objectDisposedException;
            }
        }

        public void Send(Socket socket, byte[] data, int length)
        {
            var clientState = FindClientSate(socket);
            if (clientState == null)
            {
                ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Cannot find socket for send data.");
                return;
            }
            BeginSend(clientState, data, length);
        }

        public void SendAll(byte[] data, int length)
        {
            foreach (var clientState in _clientStates)
            {
                BeginSend(clientState, data, length);
            }
        }

        private ClientState FindClientSate(Socket socket)
        {
            foreach (var clientState in _clientStates)
            {
                if (clientState.Socket == socket)
                {
                    return clientState;
                }
            }
            return null;
        }
        #endregion

        #region Connection Accept
        private void BeginAccept()
        {
            //TODO: Handle Exceptions.
            _socket.BeginAccept(_endAcceptCallback, this);
        }

        private void EndAccept(IAsyncResult ar)
        {
            //TODO : End accept byte buffer
            TcpSocketListener tcpSocketListener = (TcpSocketListener)ar.AsyncState;
            var socket = _socket.EndAccept(ar);
            ConnectionAccepted?.Invoke(tcpSocketListener, socket);
            BeginAccept();
        }
        #endregion

        #region Disconnect
        public void DisconnectAll()
        {
            var queue = new Queue<ClientState>(_clientStates);
            while (queue.Count > 0)
            {
                Disconnect(queue.Dequeue().Socket);
            }
        }

        public void Disconnect(Socket socket)
        {
            var clientState = FindClientSate(socket);
            if (clientState == null)
            {
                ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Cannot find socket for disconnect.");
                return;
            }
            clientState.Socket.Close();
            //BeginDisconnect(clientState);
        }

        private void BeginDisconnect(ClientState clientState)
        {
            //TODO: Handle exceptions.
            clientState.Socket.BeginDisconnect(false, _endDisconnectCallback, clientState);
        }

        private void EndDisconnect(IAsyncResult ar)
        {
            ClientState clientState = (ClientState)ar.AsyncState;
            clientState.Socket.EndDisconnect(ar);
            clientState.Socket.Shutdown(SocketShutdown.Both);
            DisconnectedInternal?.Invoke(clientState);
        }
        #endregion

        #region Send
        private void BeginSend(ClientState clientState, byte[] data, int length)
        {
            //TODO: Handle Exceptions.
            try
            {
                clientState.Socket.BeginSend(data, 0, length, SocketFlags.None, _endSendCallback, clientState);
            }
            catch (SocketException socketException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {socketException}.");
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {argumentOutOfRangeException}.");
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], Socket dispossed.");
            }
        }

        private void EndSend(IAsyncResult ar)
        {
            ClientState clientState = (ClientState)ar.AsyncState;
            clientState.Socket.EndSend(ar, out var errorCode);
            //Log($"Error code when Send : {errorCode}");
            //TODO: Disconnect when error
        }
        #endregion

        #region Receive
        private void BeginReceive(ClientState clientState)
        {
            //TODO: Handle Exceptions.
            try
            {
                clientState.Socket.BeginReceive(clientState.Buffer, 0, clientState.Buffer.Length, SocketFlags.None, _endReceiveCallback, clientState);
            }
            catch (SocketException socketException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {socketException}");
                DisconnectedInternal?.Invoke(clientState);
                return;
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                throw argumentOutOfRangeException;
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {objectDisposedException}");
                DisconnectedInternal?.Invoke(clientState);
                return;
            }
        }

        private void EndReceive(IAsyncResult ar)
        {
            ClientState state = (ClientState)ar.AsyncState;
            try
            {
                int bytes = state.Socket.EndReceive(ar, out var errorCode);
                if (bytes > 0)
                {
                    state.LastReceivedBytes = bytes;
                    //Log($"Error code when Receive : {errorCode}");
                    //TODO : Update this lines with use the socket error.
                    MessageReceivedInterval?.Invoke(state);
                }
                BeginReceive(state);
            }
            catch (SocketException socketException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {socketException}");
                DisconnectedInternal?.Invoke(state);
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {objectDisposedException}");
                DisconnectedInternal?.Invoke(state);
            }
        }
        #endregion

        #region Event Listeners
        private void OnConnectionAccepted(TcpSocketListener tcpSocketListener, Socket socket)
        {
            if (tcpSocketListener != this)
                return;

            ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], OnConnectionAccepted {socket.RemoteEndPoint}");
            ClientState clientState = new ClientState()
            {
                Socket = socket,
                TcpSocketListener = tcpSocketListener,
                RemoteEndPoint = socket.RemoteEndPoint,
            };
            _clientStates.Add(clientState);
            Connected?.Invoke(socket);
            BeginReceive(clientState);
        }

        private void OnMessageReceivedInterval(ClientState clientState)
        {
            if (clientState.TcpSocketListener != this)
                return;

            var epFrom = clientState.Socket.RemoteEndPoint;
            var bytes = clientState.LastReceivedBytes;
            var stringMessage = Encoding.UTF8.GetString(clientState.Buffer, 0, bytes);
            //Log($"RECV: {epFrom.ToString()}: {bytes}, {stringMessage}");
            MessageReceived?.Invoke(clientState.Socket, ref clientState.Buffer, clientState.LastReceivedBytes);
        }

        private void OnDisconnectedInternal(ClientState clientState)
        {
            if (clientState.TcpSocketListener != this)
                return;
            _clientStates.Remove(clientState);
            ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Disconnected {clientState.RemoteEndPoint}");
            Disconnected?.Invoke(clientState.Socket);
        }
        #endregion
    }
}
