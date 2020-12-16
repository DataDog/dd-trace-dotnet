using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams
{
    internal class DatadogHttpHeaderHelper
    {
        public const char CarriageReturn = '\r';
        public static readonly string NewLine = Environment.NewLine;
        public static readonly int CrLfLength = NewLine.Length;

        public static async Task WriteLeadingHeaders(HttpRequest request, StreamWriter writer)
        {
            // optimization opportunity: cache the ascii-encoded bytes of commonly-used headers
            await writer.WriteAsync($"{request.Verb} {request.Path} HTTP/1.1{NewLine}").ConfigureAwait(false);
            await writer.WriteAsync($"Host: {request.Host}{NewLine}").ConfigureAwait(false);
            await writer.WriteAsync($"Accept-Encoding: identity{NewLine}").ConfigureAwait(false);
            await writer.WriteAsync($"Content-Length: {request.Content.Length ?? 0}{NewLine}").ConfigureAwait(false);

            await writer.WriteAsync($"{AgentHttpHeaderNames.Language}: .NET{NewLine}").ConfigureAwait(false);
            await writer.WriteAsync($"{AgentHttpHeaderNames.TracerVersion}: {TracerConstants.AssemblyVersion}{NewLine}").ConfigureAwait(false);
            // don't add automatic instrumentation to requests from datadog code
            await writer.WriteAsync($"{HttpHeaderNames.TracingEnabled}: false{NewLine}").ConfigureAwait(false);
        }

        public static Task WriteHeader(StreamWriter writer, HttpHeaders.HttpHeader header)
        {
            return writer.WriteAsync($"{header.Name}: {header.Value}{NewLine}");
        }

        public static Task WriteEndOfHeaders(StreamWriter writer)
        {
            return writer.WriteAsync($"Content-Type: application/msgpack{NewLine}{NewLine}");
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
