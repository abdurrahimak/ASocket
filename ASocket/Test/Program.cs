using System;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASocket
{

    class Program
    {
        static string ip = "127.0.0.1";
        static int port = 27000;
        static TcpSocketListener listener = new TcpSocketListener();
        static TcpSocketClient client = new TcpSocketClient();

        private static UdpSocket udpListener = new UdpSocket();
        private static UdpSocket udpclient = new UdpSocket();
        static void Main(string[] args)
        {
            listener.Connected += (socket) =>
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("Hoş geldin.");
                listener.Send(socket, bytes, bytes.Length);
            };

            listener.MessageReceived += (Socket socket, ref byte[] buffer, int bytes) =>
            {
                var epFrom = socket.RemoteEndPoint;
                var stringMessage = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine($"RECV: {epFrom.ToString()}: {bytes}, {stringMessage}");
            };

            client.Connected += () =>
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("Hoş Buldum kardeş.");
                client.Send(bytes, bytes.Length);
            };

            listener.Start(ip, port);
            client.Connect(ip, port);

            SocketClient client2 = new SocketClient();
            client2.Connect(ip, port);

            SocketClient client3 = new SocketClient();
            client3.Connect(ip, port);
            
            SocketClient client4 = new SocketClient();
            client4.Connect(ip, port);
            
            udpListener.StartServer(ip, port);
            udpclient.StartClient(ip, port);

            string readLine = Console.ReadLine();
            while (!readLine.Equals("q"))
            {
                var commands = readLine.Split(' ');
                if (commands.Length > 1)
                {
                    if (commands[0].Equals("t"))
                    {
                        if (commands[1].Equals("s"))
                        {
                            if (commands[2].Equals("da"))
                            {
                                listener.DisconnectAll();
                            }
                            else
                            {
                                var bytes = System.Text.Encoding.UTF8.GetBytes(commands[2]);
                                listener.SendAll(bytes, bytes.Length);
                            }
                        }
                        else if (commands[1].Equals("c"))
                        {
                            if (commands[2].Equals("d"))
                            {
                                client.Disconnect();
                            }
                            else if (commands[2].Equals("c"))
                            {
                                client.Connect(ip, port);
                            }
                            else
                            {
                                var bytes = System.Text.Encoding.UTF8.GetBytes(commands[2]);
                                client.Send(bytes, bytes.Length);
                            }
                        }
                    }
                    else if (commands[0].Equals("u"))
                    {
                        if (commands[1].Equals("s"))
                        {
                            if (commands[2].Equals("da"))
                            {
                                listener.DisconnectAll();
                            }
                            else
                            {
                                var bytes = System.Text.Encoding.UTF8.GetBytes(commands[2]);
                                //udpListener.Send(udpListener.ClientEP as IPEndPoint, bytes);
                            }
                        }
                        else if (commands[1].Equals("c"))
                        {
                            if (commands[2].Equals("d"))
                            {
                                client.Disconnect();
                            }
                            else if (commands[2].Equals("c"))
                            {
                                client.Connect(ip, port);
                            }
                            else
                            {
                                var bytes = System.Text.Encoding.UTF8.GetBytes(commands[2]);
                                udpclient.Send(bytes, bytes.Length);
                            }
                        }
                    }
                }
                readLine = Console.ReadLine();
            }

            Console.WriteLine("Closing");
            listener.Dispose();
            client.Dispose();
        }
    }

}
