using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams.HttpContent
{
    internal class BufferContent : IHttpContent
    {
        private readonly ArraySegment<byte> _buffer;

        public BufferContent(ArraySegment<byte> buffer)
        {
            _buffer = buffer;
        }

        public long? Length => _buffer.Count;

        public Task CopyToAsync(Stream destination, int? bufferSize)
        {
            return destination.WriteAsync(_buffer.Array, _buffer.Offset, _buffer.Count);
        }
    }
}
