namespace HttpOverStream
{
    public class HttpRequest : HttpMessage
    {
        public string Verb { get; }

        public string Host { get; }

        public string Path { get; }

        public HttpRequest(string verb, string host, string path, HttpHeaders headers, IHttpContent content)
            : base(headers, content)
        {
            Verb = verb;
            Host = host;
            Path = path;
        }
    }
}
