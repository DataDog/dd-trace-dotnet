using System.IO;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class StreamContent : IHttpContent
    {
        public Stream Stream { get; }

        public long? Length => Stream.CanSeek ? Stream.Length - Stream.Position : null;

        public StreamContent(Stream stream)
        {
            Stream = stream;
        }

        public void WriteTo(Stream stream)
        {
            Stream.CopyTo(stream);
        }

        public async Task WriteToAsync(Stream stream)
        {
            await Stream.CopyToAsync(stream).ConfigureAwait(false);
        }
    }
}
