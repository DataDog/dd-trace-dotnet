using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class WebTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] WebTagsProperties =
            InstrumentationTagsProperties.Concat(
                new Property<WebTags, string>(Trace.Tags.HttpStatusCode, t => t.StatusCode, (t, v) => t.StatusCode = v),
                new Property<WebTags, string>(Trace.Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
                new Property<WebTags, string>(Trace.Tags.HttpRequestHeadersHost, t => t.HttpRequestHeadersHost, (t, v) => t.HttpRequestHeadersHost = v),
                new Property<WebTags, string>(Trace.Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v),
                new ReadOnlyProperty<WebTags, string>(Trace.Tags.Language, t => t.Language));

        public override string SpanKind => SpanKinds.Server;

        public string HttpMethod { get; set; }

        public string HttpRequestHeadersHost { get; set; }

        public string HttpUrl { get; set; }

        public string Language => TracerConstants.Language;

        public string StatusCode { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => WebTagsProperties;
    }
}
