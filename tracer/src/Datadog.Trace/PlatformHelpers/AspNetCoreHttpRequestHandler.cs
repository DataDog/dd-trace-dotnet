// <copyright file="AspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

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

        private PropagationContext ExtractPropagatedContext(Tracer tracer, HttpRequest request)
        {
            try
            {
                // extract propagation details from http headers
                if (request.Headers is { } headers)
                {
                    return tracer.TracerManager.SpanContextPropagator.Extract(new HeadersCollectionAdapter(headers));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return default;
        }

        private void AddHeaderTagsToSpan(ISpan span, HttpRequest request, Tracer tracer)
        {
            var headerTagsInternal = tracer.CurrentTraceSettings.Settings.HeaderTags;

            if (!headerTagsInternal.IsNullOrEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    if (request.Headers is { } requestHeaders)
                    {
                        tracer.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(
                            span,
                            new HeadersCollectionAdapter(requestHeaders),
                            headerTagsInternal,
                            defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }
        }

        public Scope StartAspNetCorePipelineScope(Tracer tracer, Security security, HttpContext httpContext, string resourceName)
        {
            var request = httpContext.Request;
            string host = request.Host.Value;
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            string url = request.GetUrlForSpan(tracer.TracerManager.QueryStringManager);
            var userAgent = request.Headers[HttpHeaderNames.UserAgent];

            resourceName ??= GetDefaultResourceName(request);
            var extractedContext = ExtractPropagatedContext(tracer, request).MergeBaggageInto(Baggage.Current);
            InferredProxyScopePropagationContext? proxyContext = null;

            if (tracer.Settings.InferredProxySpansEnabled && request.Headers is { } headers)
            {
                proxyContext = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(tracer, new HeadersCollectionAdapter(headers), extractedContext);
                if (proxyContext != null)
                {
                    extractedContext = proxyContext.Value.Context;
                }
            }

            var spanContext = tracer.CreateSpanContext(_requestInOperationName, resourceName, extractedContext.SpanContext);
            var scope = spanContext switch
            {
                UnrecordedSpanContext unrecorded => tracer.StartActiveInternal(unrecorded),
                RecordedSpanContext recorded => CreateRecordedScope(tracer, security, httpContext, resourceName, extractedContext, httpMethod, host, url, userAgent, request, proxyContext, recorded),
                _ => null, // can't be hit
            };

            scope?.Span.Type = SpanTypes.Web;
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(_integrationId);

            return scope;

            Scope CreateRecordedScope(
                Tracer tracer1,
                Security security1,
                HttpContext httpContext1,
                string resourceName,
                PropagationContext propagationContext,
                string httpMethod1,
                string host1,
                string url1,
                StringValues stringValues,
                HttpRequest httpRequest,
                in InferredProxyScopePropagationContext? inferredProxyScopePropagationContext,
                RecordedSpanContext recorded)
            {
                var routeTemplateResourceNames = tracer1.Settings.RouteTemplateResourceNamesEnabled;
                var tags = routeTemplateResourceNames ? new AspNetCoreEndpointTags() : new AspNetCoreTags();

                var scope1 = tracer1.StartActiveInternal(recorded, tags: tags, links: propagationContext.Links);
                var span = (Span)scope1.Span;
                span.DecorateWebServerSpan(resourceName, httpMethod1, host1, url1, stringValues, tags);
                AddHeaderTagsToSpan(span, httpRequest, tracer1);
                tracer1.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(span, propagationContext.Baggage, tracer1.Settings.BaggageTagKeys);

                var originalPath = httpRequest.PathBase.HasValue ? httpRequest.PathBase.Add(httpRequest.Path) : httpRequest.Path;
                var requestTrackingFeature = new RequestTrackingFeature(originalPath, scope1);

                if (inferredProxyScopePropagationContext?.Scope is { } proxyScope)
                {
                    requestTrackingFeature.ProxyScope = proxyScope;
                }

                httpContext1.Features.Set(requestTrackingFeature);

                if (tracer1.Settings.IpHeaderEnabled || security1.AppsecEnabled)
                {
                    var peerIp = new Headers.Ip.IpInfo(httpContext1.Connection.RemoteIpAddress?.ToString(), httpContext1.Connection.RemotePort);
                    string GetRequestHeaderFromKey(string key) => httpRequest.Headers.TryGetValue(key, out var value) ? value : string.Empty;
                    Headers.Ip.RequestIpExtractor.AddIpToTags(peerIp, httpRequest.IsHttps, GetRequestHeaderFromKey, tracer1.Settings.IpHeader, tags);
                }

                var iastInstance = Iast.Iast.Instance;
                if (iastInstance.Settings.Enabled && iastInstance.OverheadController.AcquireRequest())
                {
                    // If the overheadController disables the vulnerability detection for this request, we do not initialize the iast context of TraceContext
                    scope1.Span.Context?.TraceContext?.EnableIastInRequest();
                }

                tags.SetAnalyticsSampleRate(_integrationId, tracer1.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);
                return scope1;
            }
        }

        public void StopAspNetCorePipelineScope(Tracer tracer, Security security, Scope rootScope, HttpContext httpContext)
        {
            if (rootScope is { Span: Span span })
            {
                var requestFeature = httpContext.Features.Get<RequestTrackingFeature>();
                var proxyScope = requestFeature?.ProxyScope;
                // We may need to update the resource name if none of the routing/mvc events updated it.
                // If we had an unhandled exception, the status code will already be updated correctly,
                // but if the span was manually marked as an error, we still need to record the status code

                // WARNING: This code assumes that the rootSpan passed in is the aspnetcore.request
                // root span. In "normal" operation, this will be the same span returned by
                // Tracer.Instance.ActiveScope, but if a customer is not disposing a span somewhere,
                // that will not necessarily be true, so make sure you use the RequestTrackingFeature.
                var isMissingHttpStatusCode = !span.HasHttpStatusCode();

                var settings = tracer.CurrentTraceSettings.Settings;
                if (string.IsNullOrEmpty(span.ResourceName) || isMissingHttpStatusCode)
                {
                    // if (string.IsNullOrEmpty(span.ResourceName))
                    // {
                    //     span.ResourceName = GetDefaultResourceName(httpContext.Request);
                    // }

                    if (isMissingHttpStatusCode)
                    {
                        span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, settings);
                    }
                }

                span.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);

                if (proxyScope is { Span: Span proxySpan })
                {
                    proxySpan.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, settings);
                    proxySpan.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                }

                if (security.AppsecEnabled)
                {
                    var securityCoordinator = SecurityCoordinator.Get(security, span, new SecurityCoordinator.HttpTransport(httpContext));
                    securityCoordinator.Reporter.AddResponseHeadersToSpan();
                }

                CoreHttpContextStore.Instance.Remove();

                rootScope.Dispose();
                proxyScope?.Dispose();
            }
        }

        public void HandleAspNetCoreException(Tracer tracer, Security security, SpanBase spanBase, HttpContext httpContext, Exception exception)
        {
            // WARNING: This code assumes that the rootSpan passed in is the aspnetcore.request
            // root span. In "normal" operation, this will be the same span returned by
            // Tracer.Instance.ActiveScope, but if a customer is not disposing a span somewhere,
            // that will not necessarily be true, so make sure you use the RequestTrackingFeature.
            if (spanBase is Span rootSpan && httpContext is not null && exception is not null)
            {
                var statusCode = 500;

                if (exception.TryDuckCast<AspNetCoreDiagnosticObserver.BadHttpRequestExceptionStruct>(out var badRequestException))
                {
                    statusCode = badRequestException.StatusCode;
                }

                // Generic unhandled exceptions are converted to 500 errors by Kestrel
                rootSpan.SetHttpStatusCode(statusCode: statusCode, isServer: true, tracer.CurrentTraceSettings.Settings);

                var requestFeature = httpContext.Features.Get<RequestTrackingFeature>();
                var proxyScope = requestFeature?.ProxyScope;
                if (proxyScope is { Span: Span proxySpan })
                {
                    proxySpan.SetHttpStatusCode(statusCode, isServer: true, tracer.CurrentTraceSettings.Settings);
                }

                if (BlockException.GetBlockException(exception) is null)
                {
                    rootSpan.SetException(exception);
                    if (proxyScope is { Span: Span proxySpan2 })
                    {
                        proxySpan2.SetException(exception);
                    }

                    security.CheckAndBlock(httpContext, rootSpan);
                }
            }
        }

        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal class RequestTrackingFeature
        {
            public RequestTrackingFeature(PathString originalPath, Scope rootAspNetCoreScope)
            {
                OriginalPath = originalPath;
                RootScope = rootAspNetCoreScope;
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

            /// <summary>
            /// Gets the root ASP.NET Core Scope
            /// </summary>
            public Scope RootScope { get; }

            /// <summary>
            /// Gets or sets the inferred ASP.NET Core Scope created from headers.
            /// </summary>
            public Scope ProxyScope { get; set; }

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
