using System;
using System.IO;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class StreamContent : IHttpContent
    {
        private const int _bufferSize = 10240;

        public Stream Stream { get; }

        public long? Length { get; }

        public StreamContent(Stream stream, long? length)
        {
            Stream = stream;
            Length = length;
        }

        public void CopyTo(Stream destination)
        {
            if (Length != null)
            {
                CopyTo(destination, (int)Length);
            }
            else
            {
                Stream.CopyTo(destination);
            }
        }

        public void CopyTo(Stream destination, int count)
        {
            byte[] bytes = new byte[Math.Min(count, _bufferSize)];
            int bytesLeft = count;

            while (bytesLeft > 0)
            {
                int bytesRead = Stream.Read(bytes, 0, bytes.Length);
                destination.Write(bytes, 0, bytesRead);
                bytesLeft -= bytesRead;
            }
        }

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
            byte[] bytes = new byte[_bufferSize];
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
