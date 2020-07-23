#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Abstractions;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;

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
        public const string IntegrationName = "AspNetCore";

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string ComponentName = "aspnet_core";
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string NoHostSpecified = "UNKNOWN_HOST";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<AspNetCoreDiagnosticObserver>();

        private static readonly PropertyFetcher HttpRequestInStartHttpContextFetcher = new PropertyFetcher("HttpContext");
        private static readonly PropertyFetcher HttpRequestInStopHttpContextFetcher = new PropertyFetcher("HttpContext");
        private static readonly PropertyFetcher UnhandledExceptionHttpContextFetcher = new PropertyFetcher("HttpContext");
        private static readonly PropertyFetcher UnhandledExceptionExceptionFetcher = new PropertyFetcher("Exception");
        private static readonly PropertyFetcher BeforeActionHttpContextFetcher = new PropertyFetcher("httpContext");
        private static readonly PropertyFetcher BeforeActionActionDescriptorFetcher = new PropertyFetcher("actionDescriptor");

        private readonly IDatadogTracer _tracer;
        private readonly AspNetCoreDiagnosticOptions _options;
        private readonly bool _isLogLevelDebugEnabled = Log.IsEnabled(LogEventLevel.Debug);

        public AspNetCoreDiagnosticObserver(IDatadogTracer tracer, AspNetCoreDiagnosticOptions options)
            : base(tracer)
        {
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        protected override string ListenerName => DiagnosticListenerName;

        protected override void OnNext(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                    OnHostingHttpRequestInStart(arg);
                    break;

                case "Microsoft.AspNetCore.Mvc.BeforeAction":
                    OnMvcBeforeAction(arg);
                    break;

                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                    OnHostingHttpRequestInStop(arg);
                    break;

                case "Microsoft.AspNetCore.Hosting.UnhandledException":
                case "Microsoft.AspNetCore.Diagnostics.UnhandledException":
                    OnHostingUnhandledException(arg);
                    break;
            }
        }

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
                Log.SafeLogError(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(HttpRequest request, IDatadogTracer tracer)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.ExtractHeaderTags(new HeadersCollectionAdapter(requestHeaders), tracer.Settings.HeaderTags);
                }
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Error extracting propagated HTTP headers.");
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private bool ShouldIgnore(HttpContext httpContext)
        {
            foreach (Func<HttpContext, bool> ignore in _options.IgnorePatterns)
            {
                if (ignore(httpContext))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnHostingHttpRequestInStart(object arg)
        {
            var httpContext = HttpRequestInStartHttpContextFetcher.Fetch<HttpContext>(arg);

            if (ShouldIgnore(httpContext))
            {
                if (_isLogLevelDebugEnabled)
                {
                    Log.Debug("Ignoring request");
                }
            }
            else
            {
                HttpRequest request = httpContext.Request;
                string host = request.Host.Value;
                string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                string url = GetUrl(request);

                string resourceUrl = UriHelpers.GetRelativeUrl(new Uri(url), tryRemoveIds: true)
                                               .ToLowerInvariant();

                string resourceName = $"{httpMethod} {resourceUrl}";

                SpanContext propagatedContext = ExtractPropagatedContext(request);
                var tagsFromHeaders = ExtractHeaderTags(request, _tracer);

                Span span = _tracer.StartSpan(HttpRequestInOperationName, propagatedContext)
                                   .SetTag(Tags.InstrumentationName, ComponentName);

                span.DecorateWebServerSpan(resourceName, httpMethod, host, url, tagsFromHeaders);

                // set analytics sample rate if enabled
                var analyticsSampleRate = _tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);

                Scope scope = _tracer.ActivateSpan(span);

                _options.OnRequest?.Invoke(scope.Span, httpContext);
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            var httpContext = BeforeActionHttpContextFetcher.Fetch<HttpContext>(arg);

            if (ShouldIgnore(httpContext))
            {
                if (_isLogLevelDebugEnabled)
                {
                    Log.Debug("Ignoring request");
                }
            }
            else
            {
                Span span = _tracer.ScopeManager.Active?.Span;

                if (span != null)
                {
                    // NOTE: This event is the start of the action pipeline. The action has been selected, the route
                    //       has been selected but no filters have run and model binding hasn't occured.
                    var actionDescriptor = BeforeActionActionDescriptorFetcher.Fetch<ActionDescriptor>(arg);
                    HttpRequest request = httpContext.Request;

                    string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                    string controllerName = actionDescriptor.RouteValues["controller"];
                    string actionName = actionDescriptor.RouteValues["action"];
                    string routeTemplate = actionDescriptor.AttributeRouteInfo?.Template ?? $"{controllerName}/{actionName}";
                    string resourceName = $"{httpMethod} {routeTemplate}";

                    // override the parent's resource name with the MVC route template
                    span.ResourceName = resourceName;
                }
            }
        }

        private void OnHostingHttpRequestInStop(object arg)
        {
            IScope scope = _tracer.ScopeManager.Active;

            if (scope != null)
            {
                var httpContext = HttpRequestInStopHttpContextFetcher.Fetch<HttpContext>(arg);
                scope.Span.SetTag(Tags.HttpStatusCode, httpContext.Response.StatusCode.ToString());

                if (httpContext.Response.StatusCode / 100 == 5)
                {
                    // 5xx codes are server-side errors
                    scope.Span.Error = true;
                }

                scope.Dispose();
            }
        }

        private void OnHostingUnhandledException(object arg)
        {
            ISpan span = _tracer.ScopeManager.Active?.Span;

            if (span != null)
            {
                var exception = UnhandledExceptionExceptionFetcher.Fetch<Exception>(arg);
                var httpContext = UnhandledExceptionHttpContextFetcher.Fetch<HttpContext>(arg);

                span.SetException(exception);
                _options.OnError?.Invoke(span, exception, httpContext);
            }
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
