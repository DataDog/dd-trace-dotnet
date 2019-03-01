using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    internal static class ScopeHelper
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(ScopeHelper));

        public static Scope CreateOutboundHttpScope(string httpMethod, Uri requestUri, Type type)
        {
            Scope scope = null;

            try
            {
                var tracer = Tracer.Instance;
                scope = tracer.StartActive("http.request");
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ServiceName = $"{tracer.DefaultServiceName}-http-client";
                span.ResourceName = httpMethod;
                span.SetTag(Tags.HttpMethod, httpMethod);
                span.SetTag(Tags.HttpUrl, requestUri.OriginalString);
                span.SetTag(Tags.InstrumentationName, type.Name.TrimEnd("Integration", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }
    }
}
