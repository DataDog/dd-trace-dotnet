// <copyright file="AspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.PlatformHelpers
{
    internal sealed class AspNetCoreHttpRequestHandler
    {
        private readonly IDatadogLogger _log;
        private readonly IntegrationId _integrationId;
        private readonly string _requestInOperationName;

        public AspNetCoreHttpRequestHandler(
            IDatadogLogger log,
            string requestInOperationName,
            IntegrationId integrationInfo)
        {
            _log = log;
            _integrationId = integrationInfo;
            _requestInOperationName = requestInOperationName;
        }

        public string GetDefaultResourceName(HttpRequest request)
        {
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";

            string absolutePath = request.PathBase.HasValue
                                      ? request.PathBase.ToUriComponent() + request.Path.ToUriComponent()
                                      : request.Path.ToUriComponent();

            string resourceUrl = UriHelpers.GetCleanUriPath(absolutePath)
                                           .ToLowerInvariant();

            return $"{httpMethod} {resourceUrl}";
        }

        private SpanContext ExtractPropagatedContext(HttpRequest request)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.Extract(new HeadersCollectionAdapter(requestHeaders));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(HttpRequest request, Tracer tracer)
        {
            var settings = tracer.Settings;

            if (!settings.HeaderTags.IsNullOrEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    var requestHeaders = request.Headers;

                    if (requestHeaders != null)
                    {
                        return SpanContextPropagator.Instance.ExtractHeaderTags(new HeadersCollectionAdapter(requestHeaders), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public Scope StartAspNetCorePipelineScope(Tracer tracer, HttpContext httpContext, HttpRequest request, string resourceName)
        {
            string host = request.Host.Value;
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            var url = request.GetUrlWithQueryString(tracer.Settings.EnableQueryStringReporting, tracer.Settings.ObfuscationQueryStringRegex);
            var userAgent = request.Headers[HttpHeaderNames.UserAgent];
            resourceName ??= GetDefaultResourceName(request);

            SpanContext propagatedContext = ExtractPropagatedContext(request);
            var tagsFromHeaders = ExtractHeaderTags(request, tracer);

            AspNetCoreTags tags;

            if (tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                var originalPath = request.PathBase.HasValue ? request.PathBase.Add(request.Path) : request.Path;
                httpContext.Features.Set(new RequestTrackingFeature(originalPath));
                tags = new AspNetCoreEndpointTags();
            }
            else
            {
                tags = new AspNetCoreTags();
            }

            var scope = tracer.StartActiveInternal(_requestInOperationName, propagatedContext, tags: tags);
            scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, userAgent, tags, tagsFromHeaders);
            var peerIp = new Headers.Ip.IpInfo(httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Connection.RemotePort);
            if (!tracer.Settings.IpHeaderDisabled)
            {
                Headers.Ip.RequestIpExtractor.AddIpToTags(peerIp, request.IsHttps, key => request.Headers.TryGetValue(key, out var value) ? value : string.Empty, tracer.Settings.IpHeader, tags);
            }

            tags.SetAnalyticsSampleRate(_integrationId, tracer.Settings, enabledWithGlobalSetting: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(_integrationId);

            return scope;
        }

        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal class RequestTrackingFeature
        {
            public RequestTrackingFeature(PathString originalPath)
            {
                OriginalPath = originalPath;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the pipeline using endpoint routing
            /// </summary>
            public bool IsUsingEndpointRouting { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is the first pipeline execution
            /// </summary>
            public bool IsFirstPipelineExecution { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating the route as calculated by endpoint routing (if available)
            /// </summary>
            public string Route { get; set; }

            /// <summary>
            /// Gets or sets a value indicating the resource name as calculated by the endpoint routing(if available)
            /// </summary>
            public string ResourceName { get; set; }

            /// <summary>
            /// Gets a value indicating the original combined Path and PathBase
            /// </summary>
            public PathString OriginalPath { get; }

            public bool MatchesOriginalPath(HttpRequest request)
            {
                if (!request.PathBase.HasValue)
                {
                    return OriginalPath.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
                }

                return OriginalPath.StartsWithSegments(
                           request.PathBase,
                           StringComparison.OrdinalIgnoreCase,
                           out var remaining)
                    && remaining.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
#endif
