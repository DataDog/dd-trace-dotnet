// <copyright file="IastAspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Feeds IAST with the ASP.NET Core request lifecycle: request start / end, the matched route and endpoint, and the request data
    /// that becomes taint sources.
    /// <para/>
    /// ASP.NET Core only uses one <see cref="System.Diagnostics.DiagnosticListener"/> instance for everything, so there is only one observer.
    /// <para/>
    /// Hosting events: https://github.com/dotnet/aspnetcore/blob/master/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs
    /// </summary>
    internal sealed class IastAspNetCoreDiagnosticObserver : DiagnosticObserver
    {
        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string RequestInfoKey = "__Datadog.Iast.AspNetCoreRequestInfo";

        private static readonly Type? EndpointFeatureType =
            Assembly.GetAssembly(typeof(RouteValueDictionary))
                   ?.GetType("Microsoft.AspNetCore.Http.Features.IEndpointFeature", throwOnError: false);

        private readonly Iast.Iast _iast;

        public IastAspNetCoreDiagnosticObserver(Iast.Iast iast)
        {
            _iast = iast;
        }

        protected override string ListenerName => DiagnosticListenerName;

        protected override void OnNext(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                    OnHostingHttpRequestInStart(arg);
                    break;
                case "Microsoft.AspNetCore.Routing.EndpointMatched":
                    OnRoutingEndpointMatched(arg);
                    break;
                case "Microsoft.AspNetCore.Mvc.BeforeAction":
                    OnMvcBeforeAction(arg);
                    break;
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                    OnHostingHttpRequestInStop(arg);
                    break;
            }
        }

        private static bool MatchesOriginalPath(RequestInfo info, HttpRequest request)
        {
            if (!request.PathBase.HasValue)
            {
                return info.OriginalPath.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
            }

            return info.OriginalPath.StartsWithSegments(request.PathBase, StringComparison.OrdinalIgnoreCase, out var remaining)
                && remaining.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
        }

        private static RouteEndpoint? GetRouteEndpoint(HttpContext httpContext)
        {
            if (EndpointFeatureType is null)
            {
                return null;
            }

            var rawEndpointFeature = httpContext.Features[EndpointFeatureType];
            if (rawEndpointFeature is null)
            {
                return null;
            }

            if (rawEndpointFeature.TryDuckCast<EndpointFeatureProxy>(out var endpointFeatureInterface)
             && endpointFeatureInterface.GetEndpoint().TryDuckCast<RouteEndpoint>(out var routeEndpointObj))
            {
                return routeEndpointObj;
            }

            if (rawEndpointFeature.TryDuckCast<EndpointFeatureStruct>(out var endpointFeatureStruct)
             && endpointFeatureStruct.Endpoint.TryDuckCast<RouteEndpoint>(out var routeEndpointObj2))
            {
                return routeEndpointObj2;
            }

            // Unable to cast to either type
            return null;
        }

        private void OnHostingHttpRequestInStart(object arg)
        {
            if (!_iast.Settings.Enabled || !Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AspNetCore))
            {
                return;
            }

            if (arg.TryDuckCast<HttpRequestInStartStruct>(out var requestStruct) && requestStruct.HttpContext is { } httpContext)
            {
                var request = httpContext.Request;
                var method = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                var url = request.GetUrlForSpan(Tracer.Instance.TracerManager.QueryStringManager);

                httpContext.Items[RequestInfoKey] = new RequestInfo(request.PathBase.HasValue ? request.PathBase.Add(request.Path) : request.Path);

                // If the overhead controller disables the vulnerability detection for this request, no iast context is created
                httpContext.Items[IastLifecycle.HttpContextKey] = IastLifecycle.BeginRequest(null, _iast.OverheadController.AcquireRequest(), method, url);
            }
        }

        private void OnRoutingEndpointMatched(object arg)
        {
            if (!_iast.Settings.Enabled)
            {
                return;
            }

            if (arg.TryDuckCast<HttpRequestInEndpointMatchedStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && IastLifecycle.GetContext(httpContext.Items) is { } iastContext
             && httpContext.Items[RequestInfoKey] is RequestInfo info)
            {
                // NOTE: This event is when the routing middleware selects an endpoint. Additional middleware (e.g
                //       Authorization/CORS) may still run, and the endpoint itself has not started executing.
                var isFirstExecution = info.IsFirstPipelineExecution;
                if (isFirstExecution)
                {
                    info.IsUsingEndpointRouting = true;
                    info.IsFirstPipelineExecution = false;

                    if (!MatchesOriginalPath(info, httpContext.Request))
                    {
                        // URL has changed from original, so treat this execution as a "subsequent" request
                        // Typically occurs for 404s for example
                        isFirstExecution = false;
                    }
                }

                var routeEndpoint = GetRouteEndpoint(httpContext);
                if (routeEndpoint is null)
                {
                    return;
                }

                if (isFirstExecution)
                {
                    iastContext.SetEndpoint(routeEndpoint.Value.DisplayName);
                    var routePattern = routeEndpoint.Value.RoutePattern.DuckCast<RoutePattern>();
                    iastContext.SetRoute(routePattern.RawText?.ToLowerInvariant());
                }

                var request = httpContext.Request.DuckCast<HttpRequestStruct>();
                iastContext.AddRequestData(httpContext.Request, request.RouteValues);
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            if (!_iast.Settings.Enabled)
            {
                return;
            }

            if (arg.TryDuckCast<BeforeActionStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && IastLifecycle.GetContext(httpContext.Items) is { } iastContext
             && httpContext.Items[RequestInfoKey] is RequestInfo info)
            {
                var isFirstExecution = info.IsFirstPipelineExecution;
                if (isFirstExecution)
                {
                    info.IsFirstPipelineExecution = false;
                    if (!MatchesOriginalPath(info, httpContext.Request))
                    {
                        // URL has changed from original, so treat this execution as a "subsequent" request
                        isFirstExecution = false;
                    }
                }

                if (!info.IsUsingEndpointRouting && isFirstExecution)
                {
                    // Not using endpoint routing: the route comes from the MVC action
                    iastContext.SetRoute(GetLegacyRoute(typedArg, httpContext));
                }

                iastContext.AddRequestData(httpContext.Request, typedArg.RouteData?.Values);
            }
        }

        private string? GetLegacyRoute(BeforeActionStruct typedArg, HttpContext httpContext)
        {
            ActionDescriptor actionDescriptor = typedArg.ActionDescriptor;
            string? rawRouteTemplate = actionDescriptor.AttributeRouteInfo?.Template;
            RouteTemplate? routeTemplate = null;
            if (rawRouteTemplate is not null)
            {
                try
                {
                    routeTemplate = TemplateParser.Parse(rawRouteTemplate);
                }
                catch
                {
                    // the route template cannot be parsed, try with the router
                }
            }

            if (routeTemplate is null)
            {
                var routeData = httpContext.Features.Get<IRoutingFeature>()?.RouteData;
                if (routeData is not null)
                {
                    var route = routeData.Routers.OfType<RouteBase>().FirstOrDefault();
                    routeTemplate = route?.ParsedTemplate;
                }
            }

            return routeTemplate?.TemplateText.ToLowerInvariant();
        }

        private void OnHostingHttpRequestInStop(object arg)
        {
            if (arg.TryDuckCast<HttpRequestInStopStruct>(out var stopStruct) && stopStruct.HttpContext is { } httpContext)
            {
                IastLifecycle.EndRequest(IastLifecycle.GetState(httpContext.Items));
            }
        }

        [DuckCopy]
        internal struct HttpRequestInStartStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        internal struct HttpRequestInStopStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        internal struct BeforeActionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public ActionDescriptor ActionDescriptor;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public RouteData RouteData;
        }

        [DuckCopy]
        internal struct HttpRequestInEndpointMatchedStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        /// <summary>
        /// Proxy for ducktyping IEndpointFeature when the interface is not implemented explicitly
        /// </summary>
        /// <seealso cref="EndpointFeatureProxy"/>
        [DuckCopy]
        internal struct EndpointFeatureStruct
        {
            public object Endpoint;
        }

        [DuckCopy]
        internal struct HttpRequestStruct
        {
            public string Method;
            public RouteValueDictionary RouteValues;
            public PathString PathBase;
        }

        private sealed class RequestInfo
        {
            public RequestInfo(PathString originalPath)
            {
                OriginalPath = originalPath;
            }

            /// <summary>
            /// Gets the original combined Path and PathBase
            /// </summary>
            public PathString OriginalPath { get; }

            /// <summary>
            /// Gets or sets a value indicating whether the pipeline uses endpoint routing
            /// </summary>
            public bool IsUsingEndpointRouting { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is the first pipeline execution
            /// </summary>
            public bool IsFirstPipelineExecution { get; set; } = true;
        }
    }
}
#endif
