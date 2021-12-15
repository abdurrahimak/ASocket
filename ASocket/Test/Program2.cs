using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ASocket.Log;
namespace ASocket
{
    class Program2
    {
        static string address = "127.0.0.1";
        static int port = 27000;
        private static SocketServer _socketServer = new SocketServer(null);
        private static List<Peer> _peers = new List<Peer>();
        private static List<SocketClient> _socketClients = new List<SocketClient>();
        static void Main(string[] args)
        {
            Log.Log.SetLogLevel(LogLevel.Verbose | LogLevel.Info | LogLevel.Error);

            _socketServer.PeerConnected += (peer) =>
            {
                _peers.Add(peer);
                Console.WriteLine($"[SocketServer] PeerConnected : {peer.TcpRemoteEndPoint}");
            };
            _socketServer.PeerDisconnected += (peer) =>
            {
                _peers.Remove(peer);
                Console.WriteLine($"[SocketServer] PeerDisconnected : {peer.TcpRemoteEndPoint}");
            };
            _socketServer.MessageReceived += (peer, data) =>
            {
                var stringMessage = Encoding.UTF8.GetString(data, 0, data.Length);
                Console.WriteLine($"[SocketServer] MessageReceived : {peer.TcpRemoteEndPoint} : {stringMessage}");
            };
            _socketServer.Start(address, port);
            
            var readLine = Console.ReadLine();
            while (!readLine.Equals("q"))
            {
                try
                {
                    var commands = readLine.Split(' ');
                    if (commands[0].Equals("create"))
                    {
                        SocketClient socketClient = new SocketClient(null);

                        socketClient.Connected += () =>
                        {
                            Console.WriteLine($"[SocketClient] Connected, {socketClient.LocalTcpEndPoint}");
                            _socketClients.Add(socketClient);
                        };

                        socketClient.Disconnected += () =>
                        {
                            Console.WriteLine($"[SocketClient] Disconnected, {socketClient.LocalTcpEndPoint}");
                            _socketClients.Remove(socketClient);
                        };

                        socketClient.ConnectionFailed += () =>
                        {
                            Console.WriteLine($"[SocketClient] ConnectionFailed, {socketClient.LocalTcpEndPoint}");
                        };

                        socketClient.MessageReceived += (byte[] data) =>
                        {
                            var stringMessage = Encoding.UTF8.GetString(data, 0, data.Length);
                            Console.WriteLine($"[SocketClient] MessageReceived, {socketClient.LocalTcpEndPoint} : {stringMessage}");
                        };

                        socketClient.Connect(address, port);
                    }
                    else if (commands[0].Equals("s"))
                    {
                        if (commands[1].Equals("dis"))
                        {
                            //s dis 0
                            int index = Int32.Parse(commands[2]);
                            _socketServer.Disconnect(_peers[index]);
                        }
                        else if (commands[1].Equals("send"))
                        {
                            //s send t 0 message
                            //c send u 0 message
                            //s send t all message
                            //s send u all message
                            PacketFlag packetFlag = commands[2].Equals("t") ? PacketFlag.Tcp : PacketFlag.Udp;
                            bool sendAll = commands[3].Equals("all");
                            string message = commands[4];
                            var data = Encoding.UTF8.GetBytes(message);

                            if (sendAll)
                            {
                                _socketServer.SendAll(data, packetFlag);
                            }
                            else
                            {
                                int index = Int32.Parse(commands[3]);
                                var peer = _peers[index];
                                _socketServer.Send(peer, data, packetFlag);
                            }
                        }
                        Console.WriteLine($"");
                    }
                    else if (commands[0].Equals("c"))
                    {
                        if (commands[1].Equals("dis"))
                        {
                            //c dis 0
                            int index = Int32.Parse(commands[2]);
                            _socketClients[index].Disconnect();
                        }
                        else if (commands[1].Equals("send"))
                        {
                            //c send t 0 message
                            //c send u 0 message
                            PacketFlag packetFlag = commands[2].Equals("t") ? PacketFlag.Tcp : PacketFlag.Udp;
                            int index = Int32.Parse(commands[3]);
                            string message = commands[4];
                            var data = Encoding.UTF8.GetBytes(message);
                            _socketClients[index].Send(data, packetFlag);
                        }
                        Console.WriteLine($"");
                    }
                    else if (commands[0].Equals("lp"))
                    {
                        Console.WriteLine($"");
                        Console.WriteLine($"------ List of Peers --------");
                        for (var index = 0; index < _peers.Count; index++)
                        {
                            var peer = _peers[index];
                            Console.WriteLine($" {index} - {peer.TcpRemoteEndPoint} - {peer.UdpRemoteEndPoint}");
                        }
                        Console.WriteLine($"");
                    }
                    else if (commands[0].Equals("lc"))
                    {
                        Console.WriteLine($"");
                        Console.WriteLine($"------ List of Clients --------");
                        for (var index = 0; index < _socketClients.Count; index++)
                        {
                            var socketClient = _socketClients[index];
                            Console.WriteLine($" {index} - {socketClient.RemoteTcpEndPoint} - {socketClient.LocalUdpEndPoint}");
                        }
                        Console.WriteLine($"");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                readLine = Console.ReadLine();
            }
            
            _socketServer.Destroy();
        }
    }
}
