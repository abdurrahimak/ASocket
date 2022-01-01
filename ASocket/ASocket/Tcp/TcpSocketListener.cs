using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ASocket
{
    internal class TcpSocketListener : IDisposable
    {
        private class ClientState : IDisposable
        {
            public Socket Socket;
            
            //TODO: change the memory buffer if unity support the .netstandart 2.1 all
            //public readonly IMemoryOwner<byte> Buffer = MemoryPool<byte>.Shared.Rent(PacketInformation.PacketSize);
            public readonly ArraySegment<byte> Buffer = new ArraySegment<byte>(new byte[PacketInformation.PacketSize]);
            public int LastReceivedBytes;

            public TcpSocketListener TcpSocketListener;
            public EndPoint RemoteEndPoint { get; set; }
            
            public void Dispose()
            {
                Socket?.Dispose();
                //Buffer?.Dispose();
            }
        }

        private Socket _socket;
        private IPEndPoint _localEP;
        private List<ClientState> _clientStates;

        private static Action<TcpSocketListener, Socket> ConnectionAccepted;
        private static Action<ClientState> MessageReceivedInterval;
        private static Action<ClientState> DisconnectedInternal;

        public event Action<Socket> Connected;
        public event Action<Socket> Disconnected;
        public event Action<Socket, ReadOnlyMemory<byte>> MessageReceived;

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
            MessageReceivedInterval += OnMessageReceivedInternal;
            DisconnectedInternal += OnDisconnectedInternal;
        }

        private void Unregister()
        {
            ConnectionAccepted -= OnConnectionAccepted;
            MessageReceivedInterval -= OnMessageReceivedInternal;
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
            try
            {
                //TODO : End accept byte buffer
                TcpSocketListener tcpSocketListener = (TcpSocketListener)ar.AsyncState;
                var socket = _socket.EndAccept(ar);
                ConnectionAccepted?.Invoke(tcpSocketListener, socket);
                BeginAccept();
            }
            catch (Exception ex)
            {
                DisconnectAll();
            }
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

        public void Send(Socket socket, ReadOnlyMemory<byte> data)
        {
            var clientState = FindClientSate(socket);
            if (clientState == null)
            {
                ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Cannot find socket for send data.");
                return;
            }
            BeginSend(clientState, data.ToArray(), data.Length);
        }

        public void SendAll(byte[] data, int length)
        {
            foreach (var clientState in _clientStates)
            {
                BeginSend(clientState, data, length);
            }
        }

        private void BeginSend(ClientState clientState, byte[] data, int length)
        {
            //TODO: Handle Exceptions.
            try
            {
                clientState.Socket.BeginSend(data, 0, length, SocketFlags.None, _endSendCallback, clientState);
            }
            catch (SocketException socketException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {socketException}, {socketException.StackTrace}");
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], {argumentOutOfRangeException}, {argumentOutOfRangeException.StackTrace}");
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
        private void CreateReadThread(ClientState clientState)
        {
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var bytes = await clientState.Socket.ReceiveAsync(clientState.Buffer, SocketFlags.None);
                        if (bytes > 0)
                        {
                            clientState.LastReceivedBytes = bytes;
                            MessageReceivedInterval?.Invoke(clientState);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], [CreateReadThread], receive error disonnecting...");
                    ASocket.Log.Log.Error($"[{nameof(TcpSocketListener)}], [CreateReadThread], {ex}, {ex.StackTrace}");
                    DisconnectedInternal?.Invoke(clientState);
                }
            });
        }

        private void BeginReceive(ClientState clientState)
        {
            try
            {
                //TODO: Closed.
                //clientState.Socket.BeginReceive(clientState.Buffer, 0, clientState.Buffer.Length, SocketFlags.None, _endReceiveCallback, clientState);
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
            CreateReadThread(clientState);
        }

        private void OnMessageReceivedInternal(ClientState clientState)
        {
            if (clientState.TcpSocketListener != this)
                return;

            var bytes = clientState.LastReceivedBytes;
            var readonlyMemory = clientState.Buffer[..bytes].AsMemory();
            MessageReceived?.Invoke(clientState.Socket, readonlyMemory);
        }

        private void OnDisconnectedInternal(ClientState clientState)
        {
            if (clientState.TcpSocketListener != this)
                return;
            _clientStates.Remove(clientState);
            clientState.Dispose();
            ASocket.Log.Log.Info($"[{nameof(TcpSocketListener)}], Disconnected {clientState.RemoteEndPoint}");
            Disconnected?.Invoke(clientState.Socket);
        }
        #endregion
    }
}
