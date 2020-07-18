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
        /// <param name="tracer">The tracer instance to use to create the new scope.</param>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationName">The name of the integration creating this scope.</param>
        /// <returns>A new pre-populated scope.</returns>
        public static Activity CreateOutboundHttpActivity(Tracer tracer, string httpMethod, Uri requestUri, string integrationName)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

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
                activity.SetCustomProperty("ServiceName", tracer.DefaultServiceName + ServiceName);
                SetDefaultTags(tracer, activity);

                activity.SetCustomProperty("Type", SpanTypes.Http);
                activity.DisplayName = $"{httpMethod} {resourceUrl}";

                activity.AddTag(Tags.SpanKind, SpanKinds.Client);
                activity.AddTag(Tags.HttpMethod, httpMethod?.ToUpperInvariant());
                activity.AddTag(Tags.HttpUrl, httpUrl);
                activity.AddTag(Tags.InstrumentationName, integrationName);
                activity.AddTag(Tags.SpanSource, "Activity");

                // set analytics sample rate if enabled
                if (integrationName != null)
                {
                    double? analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting: false);
                    var metrics = new Dictionary<string, double>();

                    if (analyticsSampleRate != null)
                    {
                        metrics.Add(Tags.Analytics, analyticsSampleRate.Value);
                    }

                    activity.SetCustomProperty("Metrics", new Dictionary<string, double>());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return activity;
        }

        private static void SetDefaultTags(Tracer tracer, Activity activity)
        {
            TracerSettings settings = tracer.Settings;

            // Apply any global tags
            if (settings.GlobalTags.Count > 0)
            {
                foreach (var entry in settings.GlobalTags)
                {
                    activity.AddTag(entry.Key, entry.Value);
                }
            }

            // automatically add the "env" tag if defined, taking precedence over an "env" tag set from a global tag
            var env = settings.Environment;
            if (!string.IsNullOrWhiteSpace(env))
            {
                activity.AddTag(Tags.Env, env);
            }

            // automatically add the "version" tag if defined, taking precedence over an "version" tag set from a global tag
            // This doesn't make sense for "dependency" activities so don't add version
            /*
            var version = settings.ServiceVersion;
            if (!string.IsNullOrWhiteSpace(version) && string.Equals(finalServiceName, DefaultServiceName))
            {
                activity.AddTag(Tags.Version, version);
            }
            */
        }
    }
}
