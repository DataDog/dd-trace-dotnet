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

        public Task CopyToAsync(Stream destination)
        {
            return destination.WriteAsync(_buffer.Array, _buffer.Offset, _buffer.Count);
        }

        public Task CopyToAsync(byte[] buffer)
        {
            if (_buffer.Count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(buffer),
                    $"Buffer of size {buffer.Length} is not large enough to hold content of size {_buffer.Count}");
            }

            Buffer.BlockCopy(
                src: _buffer.Array,
                srcOffset: _buffer.Offset,
                dst: buffer,
                dstOffset: 0,
                count: _buffer.Count);
#if NET45
            return Task.FromResult(false);
#else
            return Task.CompletedTask;
#endif
        }
    }
}
