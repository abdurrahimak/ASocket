using System;
using System.Buffers;
using System.Runtime.InteropServices;
namespace ASocket
{
    internal class PacketBuffer : IDisposable
    {
        private int _currentSize = 0;
        private int _packetSize = -1;


        public readonly byte[] BufferArray;
        public readonly Memory<byte> BufferMem;
        public int CurrentSize => _currentSize;
        public bool PacketCompleted => _currentSize >= _packetSize && _packetSize > 0;
        internal MessageId MessageID => (MessageId)BufferMem.Span[PacketInformation.MessageIdPacketIndex];

        public PacketBuffer()
        {
            BufferArray = new byte[PacketInformation.PacketSize];
            BufferMem = new Memory<byte>(BufferArray);
        }

        internal void Reset()
        {
            _currentSize = 0;
            _packetSize = -1;
        }


        #region WriteToBuffer

        internal void WriteToBuffer(in byte[] data, int size)
        {
            var dataSpan = data.AsSpan(0, size);
            WriteToBuffer(dataSpan);
        }

        internal void WriteToBuffer(ReadOnlySpan<byte> dataSpan)
        {
            var size = dataSpan.Length;
            var slicedBufferSpan = BufferMem.Span.Slice(_currentSize, size);
            dataSpan.CopyTo(slicedBufferSpan);
            if (_currentSize < 4 && (_currentSize + size) >= 4)
            {
                _packetSize = MemoryMarshal.Read<int>(BufferMem.Span[..4]);
            }
            _currentSize += size;
        }
        
        #endregion

        #region CreateMessage
        internal int CreateMessage(MessageId messageId, ReadOnlySpan<byte> dataSpan)
        {
            var dataLength = dataSpan.Length;
            var bufferSpan = BufferMem.Span;
            dataSpan.CopyTo(bufferSpan.Slice(PacketInformation.PacketMessageStartIndex, dataLength));

            // BitConverter.TryWriteBytes(bufferSpan[..4], dataLength);
            MemoryMarshal.Write(bufferSpan[..4], ref dataLength);

            bufferSpan[4] = (byte)messageId;
            var size = dataLength + 4 + 1;
            return size;
        }

        internal int CreateMessage(MessageId messageId, ReadOnlyMemory<byte> dataMem)
        {
            var dataLength = dataMem.Length;
            dataMem.CopyTo(BufferMem.Slice(PacketInformation.PacketMessageStartIndex, dataLength));

            // BitConverter.TryWriteBytes(bufferSpan[..4], dataLength);
            MemoryMarshal.Write(BufferMem.Span[..4], ref dataLength);

            BufferMem.Span[4] = (byte)messageId;
            var size = dataLength + 4 + 1;
            return size;
        }
        
        #endregion

        #region GetBytes & GetMessage

        internal byte[] GetMessage()
        {
            return GetMessageSpan().ToArray();
        }

        internal ReadOnlySpan<byte> GetMessageSpan()
        {
            var startIndex = PacketInformation.PacketMessageStartIndex;
            var size = _currentSize - startIndex;
            var bufferSpan = BufferMem.Span;
            return bufferSpan.Slice(startIndex, size);
        }

        internal byte[] GetBlockOfBuffer(int startIndex, int size)
        {
            return GetBlockOfBufferSpan(startIndex, size).ToArray();
        }

        internal ReadOnlySpan<byte> GetBlockOfBufferSpan(int startIndex, int size)
        {
            return BufferMem.Span.Slice(startIndex, size);
        }
        
        #endregion

        public void Dispose()
        {
        }
    }
}
