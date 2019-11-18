using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetCoreMvcSpanExpectation : WebServerSpanExpectation
    {
        public AspNetCoreMvcSpanExpectation(string serviceName, string operationName)
            : base(serviceName, operationName)
        {
        }

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
