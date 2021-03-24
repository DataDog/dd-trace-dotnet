namespace Datadog.Trace.Tagging
{
    internal abstract class HttpTags : InstrumentationTags, IHasStatusCode
    {
        public override string SpanKind => SpanKinds.Client;

        public string InstrumentationName { get; set; }

        public string HttpMethod { get; set; }

        public string HttpUrl { get; set; }

        public string HttpClientHandlerType { get; set; }

        public string HttpStatusCode { get; set; }
    }
}