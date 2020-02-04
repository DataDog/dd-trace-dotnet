using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Name of the Integration
        /// </summary>
        public const string IntegrationName = "AspNet";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(TracingHttpModule));

        private static HashSet<HttpApplication> registeredEventHandlers = new HashSet<HttpApplication>();

        private readonly string _httpContextScopeKey;
        private readonly string _requestOperationName;
        private HttpApplication _httpApplication;

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
        }

        /// <inheritdoc />
        public void Init(HttpApplication httpApplication)
        {
            // Intent: The first HttpModule to run Init for this HttpApplication will register for events
            // Actual: Each HttpApplication that comes through here is potentially a new .NET object, even
            //         if it refers to the same web application. Based on my reading, it appears that initialization
            //         is done for several types of resources. Read more in this SO article -- look at Sunday Ironfoot's
            //         (yes, reliable sounding name of course) response toward the end of this article:
            //         https://stackoverflow.com/questions/1140915/httpmodule-init-method-is-called-several-times-why
            //         I've discovered that not letting each of these unique application objects be added, and thus
            //         the event handlers be registered within each HttpApplication object, leads to the runtime
            //         weirdness: at one point it crashed consistently for me, and later, I saw no spans at all.
            if (registeredEventHandlers.Add(httpApplication))
            {
                _httpApplication = httpApplication;
                httpApplication.BeginRequest += OnBeginRequest;
                httpApplication.EndRequest += OnEndRequest;
                httpApplication.Error += OnError;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Remove the HttpApplication mapping so we don't keep the object alive
            registeredEventHandlers.Remove(_httpApplication);
            _httpApplication = null;
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

                if (sender is HttpApplication app &&
                    app.Context.Items[_httpContextScopeKey] is Scope scope)
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
                var httpContext = (sender as HttpApplication)?.Context;

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
    }
}
