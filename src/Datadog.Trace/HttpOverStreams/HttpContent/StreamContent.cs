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

        public Task CopyToAsync(Stream destination, int maxBufferSize)
        {
            var maxLengthToRead = maxBufferSize;

            if (Length != null)
            {
                if (Length > maxBufferSize)
                {
                    throw new DatadogHttpRequestException($"Content length ({Length}) is above bounds of expected size ({maxBufferSize}), terminating request.");
                }

                maxLengthToRead = (int)Length;
            }

            return Stream.CopyToAsync(destination, maxLengthToRead);
        }
    }
}
