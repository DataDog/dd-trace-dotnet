namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebServerSpanExpectation : SpanExpectation
    {
        public WebServerSpanExpectation(string serviceName, string operationName, string resourceName)
            : this(serviceName, operationName, resourceName, SpanTypes.Web)
        {
        }

        public WebServerSpanExpectation(string serviceName, string operationName, string resourceName, string type)
        : base(serviceName, operationName, resourceName, type)
        {
            // Expectations for all spans of a web server variety should go here
            RegisterTagExpectation(nameof(Tags.HttpStatusCode), expected: StatusCode);
            RegisterTagExpectation(nameof(Tags.HttpMethod), expected: HttpMethod);
        }

        public string OriginalUri { get; set; }

        public string StatusCode { get; set; }

        public string HttpMethod { get; set; }
    }
}
