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
                await Stream.CopyToAsync(destination, (int)Length).ConfigureAwait(false);
            }
            else
            {
                await Stream.CopyToAsync(destination).ConfigureAwait(false);
            }
        }
    }
}
