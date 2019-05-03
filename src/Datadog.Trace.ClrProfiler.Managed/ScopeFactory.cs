using System;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Convenience class that creates scopes and populates them with some standard details.
    /// </summary>
    internal static class ScopeFactory
    {
        public const string OperationName = "http.request";
        public const string ServiceName = "http-client";
        public const string UrlIdReplacement = "*";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ScopeFactory));

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="tracer">The tracer instance to use to create the new scope.</param>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationName">The name of the integration creating this scope.</param>
        /// <returns>A new prepopulated scope.</returns>
        public static Scope CreateOutboundHttpScope(Tracer tracer, string httpMethod, Uri requestUri, string integrationName)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(OperationName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ServiceName = $"{tracer.DefaultServiceName}-{ServiceName}";

                span.ResourceName = string.Join(
                    " ",
                    httpMethod,
                    CleanUri(requestUri, tryRemoveIds: true));

                span.SetTag(Tags.SpanKind, SpanKinds.Client);
                span.SetTag(Tags.HttpMethod, httpMethod?.ToUpperInvariant());
                span.SetTag(Tags.HttpUrl, CleanUri(requestUri, tryRemoveIds: false));
                span.SetTag(Tags.InstrumentationName, integrationName);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        public static string CleanUri(Uri uri, bool tryRemoveIds)
        {
            // try to remove segments that look like ids
            string path = tryRemoveIds
                              ? string.Concat(uri.Segments.Select(CleanUriSegment))
                              : uri.AbsolutePath;

            // keep only scheme, authority, and path.
            // remove username, password, query, and fragment
            return $"{uri.Scheme}{Uri.SchemeDelimiter}{uri.Authority}{path}";
        }

        public static string CleanUriSegment(string segment)
        {
            bool hasTrailingSlash = segment.EndsWith("/", StringComparison.Ordinal);

            // remove trailing slash
            if (hasTrailingSlash)
            {
                segment = segment.Substring(0, segment.Length - 1);
            }

            // remove path segments that look like int or guid
            segment = int.TryParse(segment, out _) || Guid.TryParse(segment, out _)
                          ? UrlIdReplacement
                          : segment;

            return hasTrailingSlash
                       ? segment + "/"
                       : segment;
        }
    }
}
