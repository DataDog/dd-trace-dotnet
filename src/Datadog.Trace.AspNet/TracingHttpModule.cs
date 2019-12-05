using System;
using System.Web;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AspNet
{
    /// <summary>
    ///     IHttpModule used to trace within an ASP.NET HttpApplication request
    /// </summary>
    public class TracingHttpModule : IHttpModule
    {
        internal const string IntegrationName = "AspNet";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(TracingHttpModule));

        private readonly string _httpContextScopeKey;
        private readonly string _operationName;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TracingHttpModule" /> class.
        /// </summary>
        public TracingHttpModule()
            : this("aspnet.request")
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TracingHttpModule" /> class.
        /// </summary>
        /// <param name="operationName">The operation name to be used for the trace/span data generated</param>
        public TracingHttpModule(string operationName)
        {
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));

            _httpContextScopeKey = string.Concat("__Datadog.Trace.ClrProfiler.Integrations.TracingHttpModule-", _operationName);
        }

        /// <inheritdoc />
        public void Init(HttpApplication httpApplication)
        {
            httpApplication.BeginRequest += OnBeginRequest;
            httpApplication.EndRequest += OnEndRequest;
            httpApplication.Error += OnError;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Nothing to do...
        }

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            Scope scope = null;

            try
            {
                var tracer = Tracer.Instance;

                if (!tracer.Settings.IsIntegrationEnabled(IntegrationName))
                {
                    // integration disabled
                    return;
                }

                var httpContext = GetHttpContext(sender);

                if (httpContext == null)
                {
                    return;
                }

                HttpRequest httpRequest = httpContext.Request;
                SpanContext propagatedContext = null;

                if (tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = httpRequest.Headers.Wrap();
                        propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                string host = httpRequest.Headers.Get("Host");
                string httpMethod = httpRequest.HttpMethod.ToUpperInvariant();
                string url = httpRequest.RawUrl.ToLowerInvariant();
                string path = UriHelpers.GetRelativeUrl(httpRequest.Url, tryRemoveIds: true);
                string resourceName = $"{httpMethod} {path.ToLowerInvariant()}";

                scope = tracer.StartActive(_operationName, propagatedContext);
                scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                scope.Span.SetMetric(Tags.Analytics, analyticsSampleRate);

                httpContext.Items[_httpContextScopeKey] = scope;
            }
            catch (Exception ex)
            {
                // Dispose here, as the scope won't be in context items and won't get disposed on request end in that case...
                scope?.Dispose();
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
                {
                    // integration disabled
                    return;
                }

                var httpContext = GetHttpContext(sender);

                if (httpContext?.Items[_httpContextScopeKey] is Scope scope)
                {
                    scope.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void OnError(object sender, EventArgs eventArgs)
        {
            try
            {
                var httpContext = GetHttpContext(sender);

                if (httpContext?.Error != null &&
                    httpContext.Items[_httpContextScopeKey] is Scope scope)
                {
                    scope.Span.SetException(httpContext.Error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private HttpContext GetHttpContext(object sender)
        {
            if (sender is HttpApplication httpApp)
            {
                return httpApp.Context;
            }

            return null;
        }
    }
}
