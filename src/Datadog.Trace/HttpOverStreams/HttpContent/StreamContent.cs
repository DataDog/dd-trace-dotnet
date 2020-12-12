using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams.HttpContent
{
    internal class StreamContent : IHttpContent
    {
        private const int BufferSize = 10240;

        public StreamContent(Stream stream, long? length)
        {
            Stream = stream;
            Length = length;
        }

        public Stream Stream { get; }

        public long? Length { get; }

        public async Task CopyToAsync(Stream destination)
        {
            if (Length != null)
            {
                await Stream.CopyToAsync(destination, (int)Length).ConfigureAwait(false);
            }
            else
            {
                await Stream.CopyToAsync(destination).ConfigureAwait(false);
            }
        }

        public async Task CopyToAsync(Stream destination, int count)
        {
            var bytes = new byte[BufferSize];
            int bytesLeft = count;

            while (bytesLeft > 0)
            {
                int bytesRead = await Stream.ReadAsync(bytes, 0, count).ConfigureAwait(false);
                await destination.WriteAsync(bytes, 0, bytesRead).ConfigureAwait(false);
                bytesLeft -= bytesRead;
            }
        }
    }
}
