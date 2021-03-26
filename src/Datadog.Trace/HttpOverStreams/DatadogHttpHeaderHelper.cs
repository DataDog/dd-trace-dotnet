using System.IO;
using System.Linq;
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
                    var headers = AgentHttpHeaderNames.DefaultHeaders.Select(kvp => $"{kvp.Key}: {kvp.Value}{DatadogHttpValues.CrLf}");
                    _metadataHeaders = string.Concat(headers);
                }

                return _metadataHeaders;
            }
        }

        public static Task WriteLeadingHeaders(HttpRequest request, StreamWriter writer)
        {
            var leadingHeaders =
                $"{request.Verb} {request.Path} HTTP/1.1{DatadogHttpValues.CrLf}Host: {request.Host}{DatadogHttpValues.CrLf}Accept-Encoding: identity{DatadogHttpValues.CrLf}Content-Length: {request.Content.Length ?? 0}{DatadogHttpValues.CrLf}{MetadataHeaders}";
            return writer.WriteAsync(leadingHeaders);
        }

        public static Task WriteHeader(StreamWriter writer, HttpHeaders.HttpHeader header)
        {
            return writer.WriteAsync($"{header.Name}: {header.Value}{DatadogHttpValues.CrLf}");
        }

        public static Task WriteEndOfHeaders(StreamWriter writer)
        {
            return writer.WriteAsync($"Content-Type: application/msgpack{DatadogHttpValues.CrLf}{DatadogHttpValues.CrLf}");
        }
    }
}
