using System;
using System.Web;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

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
        private readonly string _httpContextEndRequestCountKey;
        private readonly string _httpContextErrorCountKey;
        private readonly string _requestOperationName;

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
            _requestOperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));

            _httpContextScopeKey = string.Concat("__Datadog.Trace.AspNet.TracingHttpModule-", _requestOperationName);
            _httpContextEndRequestCountKey = string.Concat(_httpContextScopeKey, "-endrequestcount");
            _httpContextErrorCountKey = string.Concat(_httpContextScopeKey, "-errorcount");
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

                var httpContext = (sender as HttpApplication)?.Context;

                if (httpContext == null)
                {
                    return;
                }

                if (httpContext.Items.TryGetValue<int>(_httpContextEndRequestCountKey, out var endRequestCount))
                {
                    httpContext.Items[_httpContextEndRequestCountKey] = endRequestCount + 1;
                    httpContext.Items[_httpContextErrorCountKey] = endRequestCount + 1;
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

                scope = tracer.StartActive(_requestOperationName, propagatedContext);
                scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                scope.Span.SetMetric(Tags.Analytics, analyticsSampleRate);

                httpContext.Items[_httpContextScopeKey] = scope;
                httpContext.Items[_httpContextEndRequestCountKey] = 1;
                httpContext.Items[_httpContextErrorCountKey] = 1;
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

                var httpContext = (sender as HttpApplication)?.Context;

                if (httpContext != null &&
                    httpContext.Items.TryGetValue<int>(_httpContextEndRequestCountKey, out var endRequestCount))
                {
                    endRequestCount--;
                    httpContext.Items[_httpContextEndRequestCountKey] = endRequestCount;

                    if (endRequestCount == 0 &&
                        httpContext.Items[_httpContextScopeKey] is Scope scope)
                    {
                        scope.Dispose();
                    }
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
                var httpContext = (sender as HttpApplication)?.Context;

                if (httpContext?.Error != null &&
                    httpContext.Items.TryGetValue<int>(_httpContextErrorCountKey, out var errorCount))
                {
                    errorCount--;
                    httpContext.Items[_httpContextErrorCountKey] = errorCount;

                    if (errorCount == 0 &&
                        httpContext.Items[_httpContextScopeKey] is Scope scope)
                    {
                        scope.Span.SetException(httpContext.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }
    }
}
