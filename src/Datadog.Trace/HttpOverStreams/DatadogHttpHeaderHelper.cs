using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams
{
    internal class DatadogHttpHeaderHelper
    {
        public const char CarriageReturn = '\r';
        public const char LineFeed = '\n';
        public static readonly string CrLf = Environment.NewLine;
        public static readonly int CrLfLength = CrLf.Length;

        public static async Task WriteLeadingHeaders(HttpRequest request, StreamWriter writer)
        {
            // optimization opportunity: cache the ascii-encoded bytes of commonly-used headers
            await writer.WriteAsync($"{request.Verb} {request.Path} HTTP/1.1{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"Host: {request.Host}{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"Accept-Encoding: identity{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"Content-Length: {request.Content.Length ?? 0}{CrLf}").ConfigureAwait(false);

            await writer.WriteAsync($"{AgentHttpHeaderNames.Language}: .NET{CrLf}").ConfigureAwait(false);
            await writer.WriteAsync($"{AgentHttpHeaderNames.TracerVersion}: {TracerConstants.AssemblyVersion}{CrLf}").ConfigureAwait(false);
            // don't add automatic instrumentation to requests from datadog code
            await writer.WriteAsync($"{HttpHeaderNames.TracingEnabled}: false{CrLf}").ConfigureAwait(false);
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

        public static void SkipFeed(StreamReader reader)
        {
            if (CrLfLength > 1)
            {
                // Skip the newline indicator
                reader.Read();
            }
        }
    }
}
