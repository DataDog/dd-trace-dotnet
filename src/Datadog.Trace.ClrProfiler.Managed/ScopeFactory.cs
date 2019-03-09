using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Convenience class that creates scopes and populates them with some standard details.
    /// </summary>
    internal static class ScopeFactory
    {
        internal const string OperationName = "http.request";
        internal const string ServiceName = "http-client";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ScopeFactory));

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationName">The name of the integration creating this scope.</param>
        /// <returns>A new prepopulated scope.</returns>
        public static Scope CreateOutboundHttpScope(string httpMethod, Uri requestUri, string integrationName)
        {
            Scope scope = null;

            try
            {
                var tracer = Tracer.Instance;
                scope = tracer.StartActive(OperationName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ServiceName = $"{tracer.DefaultServiceName}-{ServiceName}";
                span.ResourceName = httpMethod;
                span.SetTag(Tags.SpanKind, SpanKinds.Client);
                span.SetTag(Tags.HttpMethod, httpMethod);
                span.SetTag(Tags.HttpUrl, requestUri.OriginalString);
                span.SetTag(Tags.InstrumentationName, integrationName);
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
