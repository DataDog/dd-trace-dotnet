namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetCoreMvcSpanExpectation : WebServerSpanExpectation
    {
        public AspNetCoreMvcSpanExpectation(string serviceName, string operationName)
            : base(serviceName, operationName)
        {
        }
    }
}
