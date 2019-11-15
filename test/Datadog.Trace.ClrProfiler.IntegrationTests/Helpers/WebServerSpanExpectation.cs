using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebServerSpanExpectation : SpanExpectation
    {
        public WebServerSpanExpectation(string serviceName, string operationName)
            : this(serviceName, operationName, SpanTypes.Web)
        {
        }

        public WebServerSpanExpectation(string serviceName, string operationName, string type)
        : base(serviceName, operationName, type)
        {
            // Expectations for all spans of a web server variety should go here
            RegisterTagExpectation(nameof(Tags.HttpStatusCode), expected: StatusCode);
            RegisterTagExpectation(nameof(Tags.HttpMethod), expected: HttpMethod);
        }

        public string OriginalUri { get; set; }

        public string StatusCode { get; set; }

        public string HttpMethod { get; set; }

        public override bool Matches(MockTracerAgent.Span span)
        {
            var spanUri = GetTag(span, Tags.HttpUrl);
            if (spanUri == null || !spanUri.Contains(OriginalUri))
            {
                return false;
            }

            return base.Matches(span);
        }
    }
}
