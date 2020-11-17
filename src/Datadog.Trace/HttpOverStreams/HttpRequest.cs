using HttpOverStream;

namespace Datadog.Trace.HttpOverStreams
{
    internal class HttpRequest : HttpMessage
    {
        public HttpRequest(string verb, string host, string path, HttpHeaders headers, IHttpContent content)
            : base(headers, content)
        {
            Verb = verb;
            Host = host;
            Path = path;
        }

        public string Verb { get; }

        public string Host { get; }

        public string Path { get; }
    }
}
