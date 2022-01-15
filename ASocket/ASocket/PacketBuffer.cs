using System;
using System.Buffers;
using System.Runtime.InteropServices;
namespace ASocket
{
    internal class PacketBuffer : IDisposable
    {
        private int _currentSize = 0;
        private int _packetDataSize = -1;
        private int _packetTotalSize => _packetDataSize + 4;


        public readonly byte[] BufferArray;
        public readonly Memory<byte> BufferMem;
        public int CurrentSize => _currentSize;
        public bool PacketCompleted => _currentSize >= _packetTotalSize && _packetDataSize > 0;
        internal MessageId MessageID => (MessageId)BufferMem.Span[PacketInformation.MessageIdPacketIndex];

        public PacketBuffer()
        {
            BufferArray = new byte[PacketInformation.PacketSize];
            BufferMem = new Memory<byte>(BufferArray);
        }

        internal void Reset()
        {
            _currentSize = 0;
            _packetDataSize = -1;
        }


        #region WriteToBuffer

        internal void WriteToBuffer(in byte[] data, int size)
        {
            var dataSpan = data.AsSpan(0, size);
            WriteToBuffer(dataSpan);
        }

        internal ReadOnlySpan<byte> WriteToBuffer(ReadOnlySpan<byte> dataSpan)
        {
            if (_currentSize < 4)
            {
                var size = dataSpan.Length;
                if((_currentSize + size) >= 4)
                {
                    var left = 4 - _currentSize;
                    var sizeSpan = BufferMem.Span.Slice(_currentSize, left);
                    dataSpan.Slice(0, left).CopyTo(sizeSpan);
                    dataSpan = dataSpan[left..];
                    _currentSize = 4;
                    _packetDataSize = MemoryMarshal.Read<int>(BufferMem.Span[..4]);
                }
                else
                {
                    var slicedBufferSpan = BufferMem.Span.Slice(_currentSize, size);
                    dataSpan.CopyTo(slicedBufferSpan);
                    dataSpan = new Span<byte>(); 
                    _currentSize += size;
                }
            }

            if (dataSpan.Length > 0)
            {
                var dataSize = dataSpan.Length;
                var leftSizeForCurrentPacket = _packetTotalSize - _currentSize;
                var copySize = leftSizeForCurrentPacket > dataSize ? dataSize : leftSizeForCurrentPacket;
                
                var slicedBufferSpan = BufferMem.Span.Slice(_currentSize, copySize);
                dataSpan.Slice(0, copySize).CopyTo(slicedBufferSpan);
                _currentSize += copySize;
                dataSpan = dataSpan[copySize..];
            }

            return dataSpan;
        }
        
        #endregion

        #region CreateMessage
        internal int CreateMessage(MessageId messageId, ReadOnlySpan<byte> dataSpan)
        {
            var dataLength = dataSpan.Length;
            var bufferSpan = BufferMem.Span;
            dataSpan.CopyTo(bufferSpan.Slice(PacketInformation.PacketMessageStartIndex, dataLength));
            
            // For message id
            dataLength += 1;
            
            MemoryMarshal.Write(bufferSpan[..4], ref dataLength);

            bufferSpan[4] = (byte)messageId;
            var size = dataLength + 4; // data length + message size.
            return size;
        }

        internal int CreateMessage(MessageId messageId, ReadOnlyMemory<byte> dataMem)
        {
            var dataLength = dataMem.Length;
            dataMem.CopyTo(BufferMem.Slice(PacketInformation.PacketMessageStartIndex, dataLength));

            // For message id
            dataLength += 1;
            MemoryMarshal.Write(BufferMem.Span[..4], ref dataLength);

            BufferMem.Span[4] = (byte)messageId;
            var size = dataLength + 4; // data length + message size.
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
