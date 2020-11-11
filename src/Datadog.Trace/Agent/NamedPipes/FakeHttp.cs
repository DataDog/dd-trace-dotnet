using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal static class FakeHttp
    {
        private static readonly FormatterResolverWrapper FormatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);

        public static async Task<string> CreatePost(TraceRequest request)
        {
            var headers = string.Join(Environment.NewLine, request.Headers.Select(h => $"{h.Key}: {h.Value}"));
            var traceBytes = await CachedSerializer.Instance.SerializeToByteArray(request.Traces, FormatterResolver);
            var stringBytes = System.Text.Encoding.Default.GetString(traceBytes);
            var message = $@"{request.Method} {request.Path} HTTP/{request.Version}
Host: {request.Host}
Content-Type: application/msgpack
User-Agent: dotnet-dd-named-pipes-client/1.0
{headers}{Environment.NewLine}{Environment.NewLine}
{stringBytes}";

            return message;
        }

        public static void ReadResponse(TraceResponse response, string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                // TODO: For mock testing, to remove
                response.Body = @"{'rate_by_service':1}";
                response.ContentLength = response.Body.Length;
                response.StatusCode = 200;

                return;
            }

            var lines = responseText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var foundEmptyLine = false;
            var remainder = string.Empty;

            foreach (var line in lines)
            {
                if (foundEmptyLine)
                {
                    remainder += line;
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    foundEmptyLine = true;
                }
                else if (line.Contains("HTTP/"))
                {
                    ParseStatusLine(response, line);
                }
            }

            // TODO: We may need to deserialize?
            response.Body = remainder;
        }

        private static void ParseStatusLine(TraceResponse response, string line)
        {
            const int MinStatusLineLength = 12; // "HTTP/1.x 123"
            if (line.Length < MinStatusLineLength || line[8] != ' ')
            {
                throw new Exception("Invalid response, expecting HTTP/1.0 or 1.1, was:" + line);
            }

            if (!line.StartsWith("HTTP/1."))
            {
                throw new Exception("Invalid response, expecting HTTP/1.0 or 1.1, was:" + line);
            }

            // response.Version = _httpVersion;
            // Set the status code
            if (int.TryParse(line.Substring(9, 3), out int statusCode))
            {
                response.StatusCode = statusCode;
            }
            else
            {
                throw new Exception("Invalid response, can't parse status code. Line was:" + line);
            }

            // Parse (optional) reason phrase
            if (line.Length == MinStatusLineLength)
            {
                response.ReasonPhrase = string.Empty;
            }
            else if (line[MinStatusLineLength] == ' ')
            {
                response.ReasonPhrase = line.Substring(MinStatusLineLength + 1);
            }
            else
            {
                throw new Exception("Invalid response");
            }
        }
    }
}
