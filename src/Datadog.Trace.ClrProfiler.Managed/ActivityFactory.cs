using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler
{
    internal static class ActivityFactory
    {
        public const string OperationName = "http.request";
        public const string ServiceName = "http-client";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(ScopeFactory));

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationName">The name of the integration creating this scope.</param>
        /// <returns>A new pre-populated scope.</returns>
        public static Activity CreateOutboundHttpActivity(string httpMethod, Uri requestUri, string integrationName)
        {
            /*
            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }
            */

            Activity activity = null;

            try
            {
                /* Add de-duplication logic later
                Span parent = tracer.ActiveScope?.Span;

                if (parent != null &&
                    parent.Type == SpanTypes.Http &&
                    parent.GetTag(Tags.InstrumentationName) != null)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. HttpClientHandler.SendAsync() -> SocketsHttpHandler.SendAsync()
                    return null;
                }
                */

                string resourceUrl = requestUri != null ? UriHelpers.CleanUri(requestUri, removeScheme: true, tryRemoveIds: true) : null;
                string httpUrl = requestUri != null ? UriHelpers.CleanUri(requestUri, removeScheme: false, tryRemoveIds: false) : null;

                activity = ActivityCollector.Default.StartActivity(OperationName, System.Diagnostics.ActivityKind.Client);
                activity.SetCustomProperty("Type", SpanTypes.Http);
                activity.DisplayName = $"{httpMethod} {resourceUrl}";

                // TODO: Change to SetTag API once we upgrade our DiagnosticSource ref, to better align with existing SetTag logic
                activity.AddTag(Tags.SpanKind, SpanKinds.Client);
                activity.AddTag(Tags.HttpMethod, httpMethod?.ToUpperInvariant());
                activity.AddTag(Tags.HttpUrl, httpUrl);
                activity.AddTag(Tags.InstrumentationName, integrationName);
                activity.AddTag(Tags.SpanSource, "Activity");

                activity.SetCustomProperty("Metrics", new Dictionary<string, double>());
                activity.SetCustomProperty(Tags.InstrumentationName, integrationName); // Duplicated right now because we cannot GET a tag
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return activity;
        }
    }
}
