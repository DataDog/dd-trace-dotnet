using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class StringContent : IHttpContent
    {
        public string Value { get; }

        public Encoding Encoding { get; }

        public long? Length { get; }

        public StringContent(string value, Encoding encoding)
        {
            Value = value;
            Encoding = encoding;
            Length = Encoding.GetByteCount(Value);
        }

        public void WriteTo(Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding, 2048, leaveOpen: true))
            {
                writer.Write(Value);
            }
        }

        public async Task WriteToAsync(Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding, 2048, leaveOpen: true))
            {
                await writer.WriteAsync(Value).ConfigureAwait(false);
            }
        }
    }
}
