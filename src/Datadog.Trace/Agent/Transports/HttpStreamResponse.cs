using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamResponse : IApiResponse
    {
        public HttpStreamResponse(int statusCode, long contentLength, Encoding encoding, Stream responseStream)
        {
            StatusCode = statusCode;
            ContentLength = contentLength;
            Encoding = encoding;
            ResponseStream = responseStream;
        }

        public int StatusCode { get; }

        public long ContentLength { get; }

        public Encoding Encoding { get; }

        public Stream ResponseStream { get; }

        public void Dispose()
        {
        }

        public async Task<string> ReadAsStringAsync()
        {
            using (var reader = new StreamReader(ResponseStream, Encoding, detectEncodingFromByteOrderMarks: false, DatadogHttpClient.MaxResponseBufferSize, leaveOpen: true))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
    }
}
