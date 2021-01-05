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

        public Task CopyToAsync(Stream destination, int maxBufferSize)
        {
            var maxLengthToRead = maxBufferSize;

            if (Length != null)
            {
                if (Length > int.MaxValue)
                {
                    throw new Exception($"Content length is above integer maximum, this is unexpected and requests cannot be sent.");
                }

                maxLengthToRead = (int)Length;
            }

            return Stream.CopyToAsync(destination, maxLengthToRead);
        }
    }
}
