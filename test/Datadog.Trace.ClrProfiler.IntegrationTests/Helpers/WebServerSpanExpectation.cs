namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebServerSpanExpectation : SpanExpectation
    {
        public WebServerSpanExpectation(string serviceName, string operationName)
            : base(serviceName, operationName, SpanTypes.Web)
        {
        }

        public WebServerSpanExpectation(string serviceName, string operationName, string type)
        : base(serviceName, operationName, type)
        {
            // Expectations for all spans of a web server variety should go here
            RegisterTagExpectation(nameof(Tags.HttpStatusCode), expected: StatusCode, when: Always);
            RegisterTagExpectation(nameof(Tags.HttpMethod), expected: HttpMethod, when: Always);
        }

        public string OriginalUri { get; set; }

        public string StatusCode { get; set; }

        public string HttpMethod { get; set; }
    }
}
