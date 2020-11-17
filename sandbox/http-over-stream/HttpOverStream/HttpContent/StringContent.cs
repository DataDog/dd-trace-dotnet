using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class StringContent : IHttpContent
    {
        private const int bufferSize = 10240;

        public string Value { get; }

        public Encoding Encoding { get; }

        public long? Length { get; }

        public StringContent(string value, Encoding encoding)
        {
            Value = value;
            Encoding = encoding;
            Length = Encoding.GetByteCount(Value);
        }

        public void CopyTo(Stream destination)
        {
            using (var writer = new StreamWriter(destination, Encoding, bufferSize, leaveOpen: true))
            {
                writer.Write(Value);
            }
        }

        public async Task CopyToAsync(Stream destination)
        {
            using (var writer = new StreamWriter(destination, Encoding, bufferSize, leaveOpen: true))
            {
                await writer.WriteAsync(Value).ConfigureAwait(false);
            }
        }
    }
}
