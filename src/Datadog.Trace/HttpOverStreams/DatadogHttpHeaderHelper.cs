using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams
{
    internal class DatadogHttpHeaderHelper
    {
        public const string CrLf = "\r\n";
        public static readonly int CrLfLength = CrLf.Length;

        public static async Task WriteLeadingHeaders(HttpRequest request, StreamWriter writer)
        {
            // optimization opportunity: cache the ascii-encoded bytes of commonly-used headers
            await writer.WriteAsync($"{request.Verb} {request.Path} HTTP/1.1{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"Host: {request.Host}{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"Accept-Encoding: identity{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"User-Agent: dd-trace-dotnet/{TracerConstants.Major}.{TracerConstants.Minor}{CrLf}").ConfigureAwait(false);
            // await writer.WriteAsync($"Connection: close{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"Content-Length: {request.Content.Length ?? 0}{CrLf}").ConfigureAwait(false);
        }

        public static async Task WriteHeader(StreamWriter writer, HttpHeaders.HttpHeader header)
        {
            await writer.WriteAsync($"{header.Name}: {header.Value}{CrLf}").ConfigureAwait(false);
        }

        public static async Task WriteEndOfHeaders(StreamWriter writer)
        {
            await writer.WriteAsync($"Content-Type: application/msgpack{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync(CrLf).ConfigureAwait(false);
        }
    }
}
