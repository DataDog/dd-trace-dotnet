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

        public Task CopyToAsync(Stream destination, int? bufferSize)
        {
            if (bufferSize == null)
            {
                return Stream.CopyToAsync(destination);
            }

            return Stream.CopyToAsync(destination, bufferSize.Value);
        }
    }
}
