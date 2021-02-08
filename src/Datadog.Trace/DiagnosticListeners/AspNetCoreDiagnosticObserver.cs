#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
#if NETCOREAPP
using Microsoft.AspNetCore.Routing.Patterns;
#endif
using Microsoft.AspNetCore.Routing.Template;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Instruments ASP.NET Core.
    /// <para/>
    /// Unfortunately, ASP.NET Core only uses one <see cref="System.Diagnostics.DiagnosticListener"/> instance
    /// for everything so we also only create one observer to ensure best performance.
    /// <para/>
    /// Hosting events: https://github.com/dotnet/aspnetcore/blob/master/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs
    /// </summary>
    internal sealed class AspNetCoreDiagnosticObserver : DiagnosticObserver
    {
        public static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AspNetCore));

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string NoHostSpecified = "UNKNOWN_HOST";

        private static readonly int PrefixLength = "Microsoft.AspNetCore.".Length;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AspNetCoreDiagnosticObserver>();
        private readonly Tracer _tracer;

        private string _hostingHttpRequestInStartEventKey;
        private string _mvcBeforeActionEventKey;
        private string _hostingUnhandledExceptionEventKey;
        private string _diagnosticsUnhandledExceptionEventKey;
        private string _hostingHttpRequestInStopEventKey;
#if NETCOREAPP
        private string _routingEndpointMatchedKey;
#endif

        public AspNetCoreDiagnosticObserver()
            : this(null)
        {
        }

        public AspNetCoreDiagnosticObserver(Tracer tracer)
        {
            _tracer = tracer;
        }

        protected override string ListenerName => DiagnosticListenerName;

#if NETCOREAPP
        protected override void OnNext(string eventName, object arg)
        {
            var lastChar = eventName[^1];

            if (lastChar == 't')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStartEventKey))
                {
                    OnHostingHttpRequestInStart(arg);
                }
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Hosting.HttpRequestIn.Start"))
                {
                    _hostingHttpRequestInStartEventKey = eventName;
                    OnHostingHttpRequestInStart(arg);
                }

                return;
            }

            if (lastChar == 'n')
            {
                if (ReferenceEquals(eventName, _mvcBeforeActionEventKey))
                {
                    OnMvcBeforeAction(arg);
                    return;
                }
                else if (ReferenceEquals(eventName, _hostingUnhandledExceptionEventKey) ||
                    ReferenceEquals(eventName, _diagnosticsUnhandledExceptionEventKey))
                {
                    OnHostingUnhandledException(arg);
                    return;
                }

                var suffix = eventName.AsSpan().Slice(PrefixLength);

                if (suffix.SequenceEqual("Mvc.BeforeAction"))
                {
                    _mvcBeforeActionEventKey = eventName;
                    OnMvcBeforeAction(arg);
                }
                else if (suffix.SequenceEqual("Hosting.UnhandledException"))
                {
                    _hostingUnhandledExceptionEventKey = eventName;
                    OnHostingUnhandledException(arg);
                }
                else if (suffix.SequenceEqual("Diagnostics.UnhandledException"))
                {
                    _diagnosticsUnhandledExceptionEventKey = eventName;
                    OnHostingUnhandledException(arg);
                }

                return;
            }

            if (lastChar == 'p')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStopEventKey))
                {
                    OnHostingHttpRequestInStop(arg);
                }
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Hosting.HttpRequestIn.Stop"))
                {
                    _hostingHttpRequestInStopEventKey = eventName;
                    OnHostingHttpRequestInStop(arg);
                }

                return;
            }

            if (lastChar == 'd')
            {
                if (ReferenceEquals(eventName, _routingEndpointMatchedKey))
                {
                    OnRoutingEndpointMatched(arg);
                }
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Routing.EndpointMatched"))
                {
                    _routingEndpointMatchedKey = eventName;
                    OnRoutingEndpointMatched(arg);
                }

                return;
            }
        }
#else
        protected override void OnNext(string eventName, object arg)
        {
            var lastChar = eventName[eventName.Length - 1];

            if (lastChar == 't')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStartEventKey))
                {
                    OnHostingHttpRequestInStart(arg);
                }
                else if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")
                {
                    _hostingHttpRequestInStartEventKey = eventName;
                    OnHostingHttpRequestInStart(arg);
                }

                return;
            }

            if (lastChar == 'n')
            {
                if (ReferenceEquals(eventName, _mvcBeforeActionEventKey))
                {
                    OnMvcBeforeAction(arg);
                    return;
                }
                else if (ReferenceEquals(eventName, _hostingUnhandledExceptionEventKey) ||
                    ReferenceEquals(eventName, _diagnosticsUnhandledExceptionEventKey))
                {
                    OnHostingUnhandledException(arg);
                    return;
                }

                switch (eventName)
                {
                    case "Microsoft.AspNetCore.Mvc.BeforeAction":
                        _mvcBeforeActionEventKey = eventName;
                        OnMvcBeforeAction(arg);
                        break;

                    case "Microsoft.AspNetCore.Hosting.UnhandledException":
                        _hostingUnhandledExceptionEventKey = eventName;
                        OnHostingUnhandledException(arg);
                        break;
                    case "Microsoft.AspNetCore.Diagnostics.UnhandledException":
                        _diagnosticsUnhandledExceptionEventKey = eventName;
                        OnHostingUnhandledException(arg);
                        break;
                }

                return;
            }

            if (lastChar == 'p')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStopEventKey))
                {
                    OnHostingHttpRequestInStop(arg);
                }
                else if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")
                {
                    _hostingHttpRequestInStopEventKey = eventName;
                    OnHostingHttpRequestInStop(arg);
                }

                return;
            }
        }
#endif

        private static string GetUrl(HttpRequest request)
        {
            if (request.Host.HasValue)
            {
                return $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}{request.Path.Value}";
            }

            // HTTP 1.0 requests are not required to provide a Host to be valid
            // Since this is just for display, we can provide a string that is
            // not an actual Uri with only the fields that are specified.
            // request.GetDisplayUrl(), used above, will throw an exception
            // if request.Host is null.
            return $"{request.Scheme}://{NoHostSpecified}{request.PathBase.Value}{request.Path.Value}";
        }

        private static SpanContext ExtractPropagatedContext(HttpRequest request)
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
                Log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(HttpRequest request, IDatadogTracer tracer)
        {
            var settings = tracer.Settings;

            if (!settings.HeaderTags.IsEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    var requestHeaders = request.Headers;

                    if (requestHeaders != null)
                    {
                        return SpanContextPropagator.Instance.ExtractHeaderTags(new HeadersCollectionAdapter(requestHeaders), settings.HeaderTags);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private void OnHostingHttpRequestInStart(object arg)
        {
            var tracer = _tracer ?? Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.TryDuckCast<HttpRequestInStartStruct>(out var requestStruct))
            {
                HttpRequest request = requestStruct.HttpContext.Request;
                string host = request.Host.Value;
                string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                string url = GetUrl(request);

                string absolutePath = request.Path.Value;

                if (request.PathBase.HasValue)
                {
                    absolutePath = request.PathBase.Value + absolutePath;
                }

                string resourceUrl = UriHelpers.GetCleanUriPath(absolutePath)
                                               .ToLowerInvariant();

                string resourceName = $"{httpMethod} {resourceUrl}";

                SpanContext propagatedContext = ExtractPropagatedContext(request);
                var tagsFromHeaders = ExtractHeaderTags(request, tracer);

                var tags = new AspNetCoreTags();
                var scope = tracer.StartActiveWithTags(HttpRequestInOperationName, propagatedContext, tags: tags);

                scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, tags, tagsFromHeaders);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            var tracer = _tracer ?? Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            Span span = tracer.ActiveScope?.Span;

            if (span != null && arg.TryDuckCast<BeforeActionStruct>(out var typedArg))
            {
                // if we've already set these tags, we can bail to avoid duplicating work
                var tags = span.Tags as AspNetCoreTags;
                if (!string.IsNullOrEmpty(tags?.AspNetEndpoint))
                {
                    return;
                }

                // NOTE: This event is the start of the action pipeline. The action has been selected, the route
                //       has been selected but no filters have run and model binding hasn't occurred.
                ActionDescriptor actionDescriptor = typedArg.ActionDescriptor;
                HttpRequest request = typedArg.HttpContext.Request;
                IDictionary<string, string> routeValues = actionDescriptor.RouteValues;

                string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";

                string controllerName = routeValues.TryGetValue("controller", out controllerName)
                    ? controllerName?.ToLowerInvariant()
                    : null;
                string actionName = routeValues.TryGetValue("action", out actionName)
                    ? actionName?.ToLowerInvariant()
                    : null;
                string areaName = routeValues.TryGetValue("area", out areaName)
                    ? areaName?.ToLowerInvariant()
                    : null;
                string pagePath = routeValues.TryGetValue("page", out pagePath)
                    ? pagePath?.ToLowerInvariant()
                    : null;

                string rawRouteTemplate = actionDescriptor.AttributeRouteInfo?.Template;
                RouteTemplate routeTemplate = null;
                if (rawRouteTemplate is not null)
                {
                    try
                    {
                        routeTemplate = TemplateParser.Parse(rawRouteTemplate);
                    }
                    catch { }
                }

                if (routeTemplate is null)
                {
                    var routeData = typedArg.HttpContext.Features.Get<IRoutingFeature>()?.RouteData;
                    if (routeData is not null)
                    {
                        var route = routeData.Routers.OfType<RouteBase>().FirstOrDefault();
                        routeTemplate = route?.ParsedTemplate;
                    }
                }

                string resourcePathName = null;
                if (routeTemplate is not null)
                {
                    var routeSegments = routeTemplate.Segments
                        .Select(segment =>
                            string.Join(string.Empty, segment.Parts
                                .Where(part => !part.IsOptional || routeValues.ContainsKey(part.Name))
                                .Select(part =>
                                {
                                    // Necessary to strip out the defaults from in-line templates
                                    if (part.IsParameter)
                                    {
                                        return "{" + (part.IsCatchAll ? "*" : string.Empty) + part.Name + (part.IsOptional ? "?" : string.Empty) + "}";
                                    }
                                    else
                                    {
                                        return part.Text;
                                    }
                                })))
                        .Where(segment => !string.IsNullOrEmpty(segment));

                    var cleanedRouteTemplate = string.Join("/", routeSegments)?.ToLowerInvariant();

                    resourcePathName =
                        "/" + cleanedRouteTemplate
                            .Replace("{area}", areaName)
                            .Replace("{controller}", controllerName)
                            .Replace("{action}", actionName);
                }

                if (string.IsNullOrEmpty(resourcePathName))
                {
                    // fallback, if all else fails
                    // NOTE: this includes the /prefix
                    resourcePathName = UriHelpers.GetRelativeUrl(request.Path, tryRemoveIds: true).ToLowerInvariant();
                }

                string resourceName = $"{httpMethod} {request.PathBase}{resourcePathName}";

                // override the parent's resource name with the MVC route template
                span.ResourceName = resourceName;

                if (tags is not null)
                {
                    tags.AspNetAction = actionName;
                    tags.AspNetController = controllerName;
                    tags.AspNetArea = areaName;
                    tags.AspNetPage = pagePath;
                    tags.AspNetRoute = routeTemplate?.TemplateText.ToLowerInvariant();
                }
            }
        }

#if NETCOREAPP
        private void OnRoutingEndpointMatched(object arg)
        {
            var tracer = _tracer ?? Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            Span span = tracer.ActiveScope?.Span;

            if (span != null)
            {
                // NOTE: This event is when the routing middleware selects an endpoint. Additional middleware (e.g
                //       Authorization/CORS) may still run, and the endpoint itself has not started executing.
                HttpContext httpContext = arg.As<HttpRequestInEndpointMatchedStruct>().HttpContext;
                var endpoint = httpContext.Features.Get<IEndpointFeature>()?.Endpoint as RouteEndpoint;

                if (endpoint is null)
                {
                    return;
                }

                HttpRequest request = httpContext.Request;
                RouteValueDictionary routeValues = request.RouteValues;
                var routePattern = endpoint.RoutePattern;

                string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";

                object raw;
                string controllerName = routeValues.TryGetValue("controller", out raw)
                                        ? raw?.ToString()?.ToLowerInvariant()
                                        : null;
                string actionName = routeValues.TryGetValue("action", out raw)
                                        ? raw?.ToString()?.ToLowerInvariant()
                                        : null;
                string areaName = routeValues.TryGetValue("area", out raw)
                                      ? raw?.ToString()?.ToLowerInvariant()
                                      : null;
                string pagePath = routeValues.TryGetValue("page", out raw)
                                      ? raw?.ToString()?.ToLowerInvariant()
                                      : null;

                string resourcePathName = null;
                if (routePattern is not null)
                {
                    var simplifiedRouteTemplate = SimplifyRoutePattern(routePattern, routeValues);

                    resourcePathName =
                        "/" + simplifiedRouteTemplate
                            .Replace("{area}", areaName)
                            .Replace("{controller}", controllerName)
                            .Replace("{action}", actionName);
                }

                if (string.IsNullOrEmpty(resourcePathName))
                {
                    // fallback, if all else fails
                    // NOTE: this includes the /prefix
                    resourcePathName = UriHelpers.GetRelativeUrl(request.Path, tryRemoveIds: true).ToLowerInvariant();
                }

                string resourceName = $"{httpMethod} {request.PathBase}{resourcePathName}";

                // override the parent's resource name with the MVC route template
                span.ResourceName = resourceName;

                var tags = span.Tags as AspNetCoreTags;
                if (tags is not null)
                {
                    tags.AspNetAction = actionName;
                    tags.AspNetController = controllerName;
                    tags.AspNetArea = areaName;
                    tags.AspNetPage = pagePath;
                    tags.AspNetRoute = routePattern?.RawText?.ToLowerInvariant();
                    tags.AspNetEndpoint = endpoint.DisplayName;
                }
            }
        }

        private string SimplifyRoutePattern(
            RoutePattern routePattern,
            RouteValueDictionary routeValueDictionary)
        {
            var allSegments =
                routePattern
                   .PathSegments
                   .Select(segment => string.Join(string.Empty, segment.Parts.Select(x => SegmentToString(x, routeValueDictionary))))
                   .Where(segment => !string.IsNullOrEmpty(segment));

            return string.Join("/", allSegments)?.ToLowerInvariant();

            static string SegmentToString(RoutePatternPart part, RouteValueDictionary values)
            {
                return part switch
                {
                    RoutePatternLiteralPart literal => literal.Content,
                    RoutePatternSeparatorPart separator => separator.Content,
                    RoutePatternParameterPart parameter => parameter.IsOptional && !values.ContainsKey(parameter.Name)
                                                               ? string.Empty
                                                               : "{" + (parameter.IsCatchAll
                                                                            ? (parameter.EncodeSlashes ? "**" : "*")
                                                                            : string.Empty)
                                                                     + parameter.Name
                                                                     + (parameter.IsOptional ? "?" : string.Empty)
                                                                     + "}",
                    _ => string.Empty,
                };
            }
        }
#endif

        private void OnHostingHttpRequestInStop(object arg)
        {
            var tracer = _tracer ?? Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            var scope = tracer.ActiveScope;

            if (scope != null)
            {
                // if we had an unhandled exception, the status code is already updated
                if (!scope.Span.Error && arg.TryDuckCast<HttpRequestInStopStruct>(out var httpRequest))
                {
                    HttpContext httpContext = httpRequest.HttpContext;
                    scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true);
                }

                scope.Dispose();
            }
        }

        private void OnHostingUnhandledException(object arg)
        {
            var tracer = _tracer ?? Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            var span = tracer.ActiveScope?.Span;

            if (span != null && arg.TryDuckCast<UnhandledExceptionStruct>(out var unhandledStruct))
            {
                span.SetException(unhandledStruct.Exception);
                int statusCode = 500;

                if (unhandledStruct.Exception.TryDuckCast<BadHttpRequestExceptionStruct>(out var badRequestException))
                {
                    statusCode = badRequestException.StatusCode;
                }

                // Generic unhandled exceptions are converted to 500 errors by Kestrel
                span.SetHttpStatusCode(statusCode: statusCode, isServer: true);
            }
        }

        [DuckCopy]
        public struct HttpRequestInStartStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        public struct HttpRequestInStopStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        public struct UnhandledExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public Exception Exception;
        }

        [DuckCopy]
        public struct BeforeActionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public ActionDescriptor ActionDescriptor;
        }

        [DuckCopy]
        public struct BadHttpRequestExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase | BindingFlags.NonPublic)]
            public int StatusCode;
        }

        [DuckCopy]
        public struct HttpRequestInEndpointMatchedStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        private readonly struct HeadersCollectionAdapter : IHeadersCollection
        {
            private readonly IHeaderDictionary _headers;

            public HeadersCollectionAdapter(IHeaderDictionary headers)
            {
                _headers = headers;
            }

            public IEnumerable<string> GetValues(string name)
            {
                if (_headers.TryGetValue(name, out var values))
                {
                    return values.ToArray();
                }

                return Enumerable.Empty<string>();
            }

            public void Set(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Add(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Remove(string name)
            {
                throw new NotImplementedException();
            }
        }
    }
}
#endif
