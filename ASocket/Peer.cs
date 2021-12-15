using System.Net;
using System.Net.Sockets;
namespace ASocket
{
    public class Peer
    {
        public EndPoint UdpRemoteEndPoint { get; private set; }
        public EndPoint TcpRemoteEndPoint { get; private set; }

        internal Socket TcpSocket { get; private set; }
        internal PacketBuffer SendBuffer = new PacketBuffer();
        internal PacketBuffer ReadTcpBuffer = new PacketBuffer();
        internal PacketBuffer ReadUdpBuffer = new PacketBuffer();

        internal bool UdpReady { get; private set; }

        internal void SetUdpEndpoint(EndPoint endPoint)
        {
            UdpReady = true;
            UdpRemoteEndPoint = endPoint;
        }

        internal void SetTcpSocket(Socket socket)
        {
            TcpSocket = socket;
            TcpRemoteEndPoint = TcpSocket.RemoteEndPoint;
        }
    }
}
