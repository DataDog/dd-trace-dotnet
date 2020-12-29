using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams
{
    internal class DatadogHttpHeaderHelper
    {
        private static string _metadataHeaders = null;

        private static string MetadataHeaders
        {
            get
            {
                if (_metadataHeaders == null)
                {
                    _metadataHeaders =
                        $"{AgentHttpHeaderNames.Language}: .NET{DatadogHttpValues.NewLine}{AgentHttpHeaderNames.TracerVersion}: {TracerConstants.AssemblyVersion}{DatadogHttpValues.NewLine}{HttpHeaderNames.TracingEnabled}: false{DatadogHttpValues.NewLine}";
                }

                return _metadataHeaders;
            }
        }

        public static Task WriteLeadingHeaders(HttpRequest request, StreamWriter writer)
        {
            var leadingHeaders =
                $"{request.Verb} {request.Path} HTTP/1.1{DatadogHttpValues.NewLine}Host: {request.Host}{DatadogHttpValues.NewLine}Accept-Encoding: identity{DatadogHttpValues.NewLine}Content-Length: {request.Content.Length ?? 0}{DatadogHttpValues.NewLine}{MetadataHeaders}";
            return writer.WriteAsync(leadingHeaders);
        }

        public static Task WriteHeader(StreamWriter writer, HttpHeaders.HttpHeader header)
        {
            return writer.WriteAsync($"{header.Name}: {header.Value}{DatadogHttpValues.NewLine}");
        }

        public static Task WriteEndOfHeaders(StreamWriter writer)
        {
            return writer.WriteAsync($"Content-Type: application/msgpack{DatadogHttpValues.NewLine}{DatadogHttpValues.NewLine}");
        }
    }
}
