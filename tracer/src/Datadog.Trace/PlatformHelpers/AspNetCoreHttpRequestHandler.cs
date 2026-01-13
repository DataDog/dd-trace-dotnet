// <copyright file="AspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Helpers;
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

namespace Datadog.Trace.PlatformHelpers
{
    internal sealed class AspNetCoreHttpRequestHandler
    {
        internal const string HttpContextItemsKey = "__Datadog.AspNetCoreHttpRequestHandler.Tracking";
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

        private void AddHeaderTagsToSpan(ISpan span, HttpRequest request, Tracer tracer, ReadOnlyDictionary<string, string> headerTagsInternal)
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

        public Scope StartAspNetCorePipelineScope(Tracer tracer, Security security, Iast.Iast iast, HttpContext httpContext, string resourceName)
        {
            var routeTemplateResourceNames = tracer.Settings.RouteTemplateResourceNamesEnabled;
            var tags = routeTemplateResourceNames ? new AspNetCoreEndpointTags() : new AspNetCoreTags();
            return StartAspNetCorePipelineScope(tracer, security, iast, httpContext, resourceName, tags, useSingleSpanRequestTracking: false);
        }

#if NET6_0_OR_GREATER
        public Scope StartAspNetCoreSingleSpanPipelineScope(Tracer tracer, Security security, Iast.Iast iast, HttpContext httpContext, string resourceName)
            => StartAspNetCorePipelineScope(tracer, security, iast, httpContext, resourceName, new AspNetCoreSingleSpanTags(), useSingleSpanRequestTracking: true);
#endif

        private Scope StartAspNetCorePipelineScope<T>(Tracer tracer, Security security, Iast.Iast iast, HttpContext httpContext, string resourceName, T tags, bool useSingleSpanRequestTracking)
            where T : WebTags
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

            var scope = tracer.StartActiveInternal(_requestInOperationName, extractedContext.SpanContext, tags: tags, links: extractedContext.Links);
            scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, userAgent, tags);

            var headerTagsInternal = tracer.CurrentTraceSettings.Settings.HeaderTags;
            if (headerTagsInternal.Count != 0)
            {
                AddHeaderTagsToSpan(scope.Span, request, tracer, headerTagsInternal);
            }

            tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(scope.Span, extractedContext.Baggage, tracer.Settings.BaggageTagKeys);

            var originalPath = request.PathBase.HasValue ? request.PathBase.Add(request.Path) : request.Path;
#if NET6_0_OR_GREATER
            httpContext.Items[HttpContextItemsKey] = useSingleSpanRequestTracking
                                                         ? new SingleSpanRequestTrackingFeature(originalPath, scope, proxyContext?.Scope)
                                                         : new RequestTrackingFeature(originalPath, scope, proxyContext?.Scope);
#else
            httpContext.Items[HttpContextItemsKey] = new RequestTrackingFeature(originalPath, scope, proxyContext?.Scope);
#endif

            if (tracer.Settings.IpHeaderEnabled || security.AppsecEnabled)
            {
                var peerIp = new Headers.Ip.IpInfo(httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Connection.RemotePort);
                string GetRequestHeaderFromKey(string key) => request.Headers.TryGetValue(key, out var value) ? value : string.Empty;
                Headers.Ip.RequestIpExtractor.AddIpToTags(peerIp, request.IsHttps, GetRequestHeaderFromKey, tracer.Settings.IpHeader, tags);
            }

            if (iast.Settings.Enabled && iast.OverheadController.AcquireRequest())
            {
                // If the overheadController disables the vulnerability detection for this request, we do not initialize the iast context of TraceContext
                scope.Span.Context?.TraceContext?.EnableIastInRequest();
            }

            tags.SetAnalyticsSampleRate(_integrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(_integrationId);

            return scope;
        }

        public void StopAspNetCorePipelineScope(Tracer tracer, Security security, Scope rootScope, HttpContext httpContext)
            => StopAspNetCorePipelineScope(tracer, security, rootScope, httpContext, proxyScope: (httpContext.Items[HttpContextItemsKey] as RequestTrackingFeature)?.ProxyScope);

        public void StopAspNetCorePipelineScope(Tracer tracer, Security security, Scope rootScope, HttpContext httpContext, Scope proxyScope)
        {
            if (rootScope != null)
            {
                // We may need to update the resource name if none of the routing/mvc events updated it.
                // If we had an unhandled exception, the status code will already be updated correctly,
                // but if the span was manually marked as an error, we still need to record the status code

                // WARNING: This code assumes that the rootSpan passed in is the aspnetcore.request
                // root span. In "normal" operation, this will be the same span returned by
                // Tracer.Instance.ActiveScope, but if a customer is not disposing a span somewhere,
                // that will not necessarily be true, so make sure you use the RequestTrackingFeature.
                var span = rootScope.Span;
                var isMissingHttpStatusCode = !span.HasHttpStatusCode();

                var settings = tracer.CurrentTraceSettings.Settings;
                if (string.IsNullOrEmpty(span.ResourceName) || isMissingHttpStatusCode)
                {
                    if (string.IsNullOrEmpty(span.ResourceName))
                    {
                        span.ResourceName = GetDefaultResourceName(httpContext.Request);
                    }

                    if (isMissingHttpStatusCode)
                    {
                        span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, settings);
                    }
                }

                span.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);

                if (proxyScope?.Span != null)
                {
                    proxyScope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, settings);
                    proxyScope.Span.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
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

        public void HandleAspNetCoreException(Tracer tracer, Security security, Span rootSpan, HttpContext httpContext, Exception exception)
            => HandleAspNetCoreException(tracer, security, rootSpan, httpContext, exception, proxyScope: (httpContext.Items[HttpContextItemsKey] as RequestTrackingFeature)?.ProxyScope);

        public void HandleAspNetCoreException(Tracer tracer, Security security, Span rootSpan, HttpContext httpContext, Exception exception, Scope proxyScope)
        {
            // WARNING: This code assumes that the rootSpan passed in is the aspnetcore.request
            // root span. In "normal" operation, this will be the same span returned by
            // Tracer.Instance.ActiveScope, but if a customer is not disposing a span somewhere,
            // that will not necessarily be true, so make sure you use the RequestTrackingFeature.
            if (rootSpan != null && httpContext is not null && exception is not null)
            {
                var statusCode = 500;

                if (exception.TryDuckCast<AspNetCoreDiagnosticObserver.BadHttpRequestExceptionStruct>(out var badRequestException))
                {
                    statusCode = badRequestException.StatusCode;
                }

                // Generic unhandled exceptions are converted to 500 errors by Kestrel
                rootSpan.SetHttpStatusCode(statusCode: statusCode, isServer: true, tracer.CurrentTraceSettings.Settings);

                if (proxyScope?.Span != null)
                {
                    proxyScope.Span.SetHttpStatusCode(statusCode, isServer: true, tracer.CurrentTraceSettings.Settings);
                }

                if (BlockException.GetBlockException(exception) is null)
                {
                    rootSpan.SetException(exception);
                    if (proxyScope?.Span != null)
                    {
                        proxyScope.Span.SetException(exception);
                    }

                    security.CheckAndBlock(httpContext, rootSpan);
                }
            }
        }

        public void CopyAspNetCoreActivityTagsIfRequired(Span span)
        {
            // TODO: Should we still run this code if _tracer.Settings.IsActivityListenerEnabled = false?

            // Extract data from the Activity if there is one, and it's the one we expect
            // We're using GetCurrentActivityObject rather than GetCurrentActivity because
            // we don't actually need to duck cast as IActivity6 or IW3CActivity
            var rawActivity = ActivityListener.GetCurrentActivityObject();
            if (rawActivity is null)
            {
                return;
            }

            // AFAICT this has been static since at least .NET Core 2.1
            // https://github.com/dotnet/aspnetcore/blob/v2.1.33/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L18C46-L18C88
            // https://github.com/dotnet/aspnetcore/blob/v10.0.1/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L20
            const string aspnetcoreActivityOperationName = "Microsoft.AspNetCore.Hosting.HttpRequestIn";

            try
            {
                if (rawActivity.DuckAs<IActivity5>() is { } activity5
                 && string.Equals(activity5.OperationName, aspnetcoreActivityOperationName, StringComparison.Ordinal)
                 && activity5.HasTagObjects())
                {
                    var state = new OtelTagsEnumerationState(span);
                    ActivityEnumerationHelper.EnumerateTagObjects(
                        activity5,
                        ref state,
                        static (ref s, kvp) =>
                        {
                            OtlpHelpers.SetTagObject(s.Span, kvp.Key, kvp.Value);
                            return true;
                        });
                }
                else if (rawActivity.DuckAs<IActivity>() is { } activity
                      && string.Equals(activity.OperationName, aspnetcoreActivityOperationName, StringComparison.Ordinal)
                      && activity.HasTags())
                {
                    var state = new OtelTagsEnumerationState(span);
                    ActivityEnumerationHelper.EnumerateTags(
                        activity,
                        ref state,
                        static (ref s, kvp) =>
                        {
                            OtlpHelpers.SetTagObject(s.Span, kvp.Key, kvp.Value);
                            return true;
                        });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error extracting activity data.");
            }
        }

        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal sealed class RequestTrackingFeature
        {
            public RequestTrackingFeature(PathString originalPath, Scope rootAspNetCoreScope, Scope proxyScope)
            {
                OriginalPath = originalPath;
                RootScope = rootAspNetCoreScope;
                ProxyScope = proxyScope;
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

#if NET6_0_OR_GREATER
        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal sealed class SingleSpanRequestTrackingFeature(PathString originalPath, Scope rootScope, [MaybeNull] Scope proxyScope)
        {
            /// <summary>
            /// Gets the root ASP.NET Core Scope
            /// </summary>
            [NotNull]
            public Scope RootScope { get; } = rootScope;

            /// <summary>
            /// Gets the inferred ASP.NET Core Scope created from headers.
            /// </summary>
            [MaybeNull]
            public Scope ProxyScope { get; } = proxyScope;

            /// <summary>
            /// Gets a value indicating the original combined Path and PathBase
            /// </summary>
            public PathString OriginalPath { get; } = originalPath;

            public bool MatchesOriginalPath(HttpRequest request)
            {
                if (!request.PathBase.HasValue)
                {
                    return OriginalPath.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
                }

                return OriginalPath.StartsWithSegments(
                           request.PathBase,
                           StringComparison.OrdinalIgnoreCase)
                    && OriginalPath.Value.AsSpan(request.PathBase.Value.Length)
                                   .Equals(request.Path.Value, StringComparison.OrdinalIgnoreCase);
            }
        }
#endif
    }
}
#endif
