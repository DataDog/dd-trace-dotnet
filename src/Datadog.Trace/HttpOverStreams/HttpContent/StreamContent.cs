using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams.HttpContent
{
    internal class StreamContent : IHttpContent
    {
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
                await CopyToAsync(destination, (int)Length).ConfigureAwait(false);
            }
            else
            {
                await Stream.CopyToAsync(destination).ConfigureAwait(false);
            }
        }

        public async Task CopyToAsync(Stream destination, int count)
        {
            // Because this is only ever used in the context of reading responses from the datadog agent:
            // Use what is specified in the client
            var bytes = new byte[DatadogHttpClient.MaxResponseBufferSize];
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
