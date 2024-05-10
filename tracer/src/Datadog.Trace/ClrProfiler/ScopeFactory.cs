// <copyright file="ScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Convenience class that creates scopes and populates them with some standard details.
    /// </summary>
    internal static class ScopeFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ScopeFactory));

        public static Scope GetActiveHttpScope(Tracer tracer)
        {
            if (tracer.InternalActiveScope is { Span: { Type: SpanTypes.Http } parent } scope && HasInstrumentationNameTag(parent))
            {
                return scope;
            }

            return null;

            static bool HasInstrumentationNameTag(Span span)
            {
                if (span.Tags is HttpTags httpTags)
                {
                    return httpTags.InstrumentationName != null;
                }

                return span.GetTag(Tags.InstrumentationName) != null;
            }
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
        internal static Scope CreateOutboundHttpScope(Tracer tracer, string httpMethod, Uri requestUri, IntegrationId integrationId, out HttpTags tags, TraceId traceId = default, ulong spanId = 0, DateTimeOffset? startTime = null)
        {
            if (CreateInactiveOutboundHttpSpan(tracer, httpMethod, requestUri, integrationId, out tags, traceId, spanId, startTime, addToTraceContext: true) is { } span)
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
        internal static Span CreateInactiveOutboundHttpSpan(Tracer tracer, string httpMethod, Uri requestUri, IntegrationId integrationId, out HttpTags tags, TraceId traceId, ulong spanId, DateTimeOffset? startTime, bool addToTraceContext)
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

                var operationName = tracer.CurrentTraceSettings.Schema.Client.GetOperationNameForProtocol("http");
                var serviceName = tracer.CurrentTraceSettings.Schema.Client.GetServiceName(component: "http-client");
                tags = tracer.CurrentTraceSettings.Schema.Client.CreateHttpTags();

                span = tracer.StartSpan(operationName, tags, serviceName: serviceName, traceId: traceId, spanId: spanId, startTime: startTime, addToTraceContext: addToTraceContext);

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{httpMethod} {resourceUrl}";

                tags.HttpMethod = httpMethod?.ToUpperInvariant();
                if (requestUri is not null)
                {
                    tags.HttpUrl = HttpRequestUtils.GetUrl(requestUri, tracer.TracerManager.QueryStringManager);
                    tags.Host = HttpRequestUtils.GetNormalizedHost(requestUri.Host);
                }

                tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
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
