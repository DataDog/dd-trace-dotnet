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

        private static string _metadataHeaders = null;

        private static string MetadataHeaders
        {
            get
            {
                if (_metadataHeaders == null)
                {
                    _metadataHeaders =
                        $"{AgentHttpHeaderNames.Language}: .NET{NewLine}{AgentHttpHeaderNames.TracerVersion}: {TracerConstants.AssemblyVersion}{NewLine}{HttpHeaderNames.TracingEnabled}: false{NewLine}";
                }

                return _metadataHeaders;
            }
        }

        public static Task WriteLeadingHeaders(HttpRequest request, StreamWriter writer)
        {
            var leadingHeaders =
                $"{request.Verb} {request.Path} HTTP/1.1{NewLine}Host: {request.Host}{NewLine}Accept-Encoding: identity{NewLine}Content-Length: {request.Content.Length ?? 0}{NewLine}{MetadataHeaders}";
            return writer.WriteAsync(leadingHeaders);
        }

        public static Task WriteHeader(StreamWriter writer, HttpHeaders.HttpHeader header)
        {
            return writer.WriteAsync($"{header.Name}: {header.Value}{NewLine}");
        }

        public static Task WriteEndOfHeaders(StreamWriter writer)
        {
            return writer.WriteAsync($"Content-Type: application/msgpack{NewLine}{NewLine}");
        }
    }
}
