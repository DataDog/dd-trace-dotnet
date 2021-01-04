using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamResponse : IApiResponse
    {
        private string _responseCache;

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
            if (_responseCache == null)
            {
                using (var reader = new StreamReader(ResponseStream, Encoding, detectEncodingFromByteOrderMarks: false, (int)ContentLength, leaveOpen: true))
                {
                    _responseCache = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            return _responseCache;
        }
    }
}
