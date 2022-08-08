// <copyright file="ScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Convenience class that creates scopes and populates them with some standard details.
    /// </summary>
    internal static class ScopeFactory
    {
        public const string OperationName = "http.request";
        public const string ServiceName = "http-client";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ScopeFactory));

        public static Scope GetActiveHttpScope(Tracer tracer)
        {
            var scope = tracer.InternalActiveScope;

            var parent = scope?.Span;

            if (parent != null &&
                parent.Type == SpanTypes.Http &&
                parent.GetTag(Tags.InstrumentationName) != null)
            {
                return scope;
            }

            return null;
        }

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="tracer">The tracer instance to use to create the new scope.</param>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationId">The id of the integration creating this scope.</param>
        /// <param name="tags">The tags associated to the scope</param>
        /// <param name="traceId">The trace id - this id will be ignored if there's already an active trace</param>
        /// <param name="spanId">The span id</param>
        /// <param name="startTime">The start time that should be applied to the span</param>
        /// <returns>A new pre-populated scope.</returns>
        internal static Scope CreateOutboundHttpScope(Tracer tracer, string httpMethod, Uri requestUri, IntegrationId integrationId, out HttpTags tags, ulong? traceId = null, ulong? spanId = null, DateTimeOffset? startTime = null)
        {
            var span = CreateInactiveOutboundHttpSpan(tracer, httpMethod, requestUri, integrationId, out tags, traceId, spanId, startTime, addToTraceContext: true);

            if (span != null)
            {
                return tracer.ActivateSpan(span);
            }

            return null;
        }

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="tracer">The tracer instance to use to create the new scope.</param>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationId">The id of the integration creating this scope.</param>
        /// <param name="tags">The tags associated to the scope</param>
        /// <param name="traceId">The trace id - this id will be ignored if there's already an active trace</param>
        /// <param name="spanId">The span id</param>
        /// <param name="startTime">The start time that should be applied to the span</param>
        /// <param name="addToTraceContext">Set to false if the span is meant to be discarded. In that case, the span won't be added to the TraceContext.</param>
        /// <returns>A new pre-populated scope.</returns>
        internal static Span CreateInactiveOutboundHttpSpan(Tracer tracer, string httpMethod, Uri requestUri, IntegrationId integrationId, out HttpTags tags, ulong? traceId, ulong? spanId, DateTimeOffset? startTime, bool addToTraceContext)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(integrationId) || PlatformHelpers.PlatformStrategy.ShouldSkipClientSpan(tracer.InternalActiveScope) || HttpBypassHelper.UriContainsAnyOf(requestUri, tracer.Settings.HttpClientExcludedUrlSubstrings))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Span span = null;

            try
            {
                if (GetActiveHttpScope(tracer) != null)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. HttpClientHandler.SendAsync() -> SocketsHttpHandler.SendAsync()
                    return null;
                }

                string resourceUrl = requestUri != null ? UriHelpers.CleanUri(requestUri, removeScheme: true, tryRemoveIds: true) : null;
                string httpUrl = requestUri != null ? UriHelpers.CleanUri(requestUri, removeScheme: false, tryRemoveIds: false) : null;

                tags = new HttpTags();

                string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                span = tracer.StartSpan(OperationName, tags, serviceName: serviceName, traceId: traceId, spanId: spanId, startTime: startTime, addToTraceContext: addToTraceContext);

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{httpMethod} {resourceUrl}";

                tags.HttpMethod = httpMethod?.ToUpperInvariant();
                tags.HttpUrl = httpUrl;
                tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);

                if (!addToTraceContext && span.Context.TraceContext.SamplingPriority == null && tracer.TracerManager.Sampler != null)
                {
                    // If we don't add the span to the trace context, then we need to manually call the sampler
                    var (samplingPriority, samplingMechanism) = tracer.TracerManager.Sampler.MakeSamplingDecision(span);
                    span.Context.TraceContext.SetSamplingPriority(samplingPriority, samplingMechanism);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating span.");
            }

            // always returns the span, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return span;
        }
    }
}
