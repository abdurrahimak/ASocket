using System;
namespace ASocket
{
    internal class PacketBuffer
    {
        private int _currentSize = 0;
        private int _packetSize = -1;
        
        public byte[] Buffer = new byte[PacketInformation.PacketSize];
        public int CurrentSize => _currentSize;
        public bool PacketCompleted => _currentSize >= _packetSize && _packetSize > 0;
        internal MessageId MessageID => (MessageId)Buffer[PacketInformation.MessageIdPacketIndex];

        internal void Reset()
        {
            _currentSize = 0;
            _packetSize = -1;
        }

        internal void WriteBuffer(in byte[] buffer, int size)
        {
            System.Buffer.BlockCopy(buffer, 0, Buffer, _currentSize, size);
            if (_currentSize < 4 && (_currentSize + size) >= 4)
            {
                _packetSize = BitConverter.ToInt32(Buffer, 0);
            }
            _currentSize += size;
        }

        internal int SetMessage(MessageId messageId, in byte[] data)
        {
            var lengthArray = BitConverter.GetBytes(data.Length);
            System.Buffer.BlockCopy(lengthArray, 0, Buffer, 0, lengthArray.Length);
            Buffer[PacketInformation.MessageIdPacketIndex] = (byte)messageId;
            System.Buffer.BlockCopy(data, 0, Buffer, PacketInformation.PacketMessageStartIndex, data.Length);
            return data.Length + lengthArray.Length + 1;
        }

        internal byte[] GetMessage()
        {
            var startIndex = PacketInformation.PacketMessageStartIndex;
            var size = _currentSize - startIndex;
            return GetBlockOfBuffer(startIndex, size);
        }

        internal void CopyBuffer(byte[] dest, int destOffset, int startIndex, int size)
        {
            System.Buffer.BlockCopy(Buffer, startIndex, dest, destOffset, size);
        }

        internal byte[] GetBlockOfBuffer(int startIndex, int size)
        {
            //TODO: Write a bytearray pool.
            var arr = new byte[size];
            System.Buffer.BlockCopy(Buffer, startIndex, arr, 0, size);
            return arr;
        }
    }
}
