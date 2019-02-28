using System;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler
{
    internal static class ScopeHelper
    {
        public static Scope CreateOutboundHttpScope(string httpMethod, Uri requestUri, Type type)
        {
            var tracer = Tracer.Instance;
            string serviceName = $"{tracer.DefaultServiceName}-http-client";
            string url = requestUri.OriginalString;

            var scope = tracer.StartActive("http.request", serviceName: serviceName);
            var span = scope.Span;
            span.Type = SpanTypes.Http;
            span.ResourceName = httpMethod;
            span.SetTag(Tags.HttpMethod, httpMethod);
            span.SetTag(Tags.HttpUrl, url);
            span.SetTag(Tags.InstrumentationName, type.Name.TrimEnd("Integration"));

            return scope;
        }
    }
}
