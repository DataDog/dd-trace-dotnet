using System.IO;
using System.Text;
using System.Threading.Tasks;
using HttpOverStream;

namespace Datadog.Trace.HttpOverStreams.HttpContent
{
    internal class StringContent : IHttpContent
    {
        private const int BufferSize = 10240;

        public StringContent(string value, Encoding encoding)
        {
            Value = value;
            Encoding = encoding;
            Length = Encoding.GetByteCount(Value);
        }

        public string Value { get; }

        public Encoding Encoding { get; }

        public long? Length { get; }

        public void CopyTo(Stream destination)
        {
            using (var writer = new StreamWriter(destination, Encoding, BufferSize, leaveOpen: true))
            {
                writer.Write(Value);
            }
        }

        public async Task CopyToAsync(Stream destination)
        {
            using (var writer = new StreamWriter(destination, Encoding, BufferSize, leaveOpen: true))
            {
                await writer.WriteAsync(Value).ConfigureAwait(false);
            }
        }
    }
}
