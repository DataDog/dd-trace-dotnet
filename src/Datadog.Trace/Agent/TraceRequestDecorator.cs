namespace Datadog.Trace.Agent
{
    internal static class TraceRequestDecorator
    {
        public static void AddHeaders(IApiRequest request)
        {
            request.AddHeader(AgentHttpHeaderNames.Language, ".NET");
            request.AddHeader(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);
            // don't add automatic instrumentation to requests from datadog code
            request.AddHeader(HttpHeaderNames.TracingEnabled, "false");
        }
    }
}
