using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class WebServerIntegrationContext : IDisposable
    {
        internal static readonly string HttpContextKey = "__Datadog_http_integration_context__";
        internal static readonly string DefaultOperationName = "http-request";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(WebServerIntegrationContext));
        private readonly ConcurrentStack<IDisposable> _disposables = new ConcurrentStack<IDisposable>();
        private readonly object _httpContext;
        private readonly Scope _rootScope;
        private readonly Scope _scope;

        private WebServerIntegrationContext(string integrationName, object httpContext)
        {
            try
            {
                _httpContext = httpContext;
                var request = _httpContext.GetProperty("Request").GetValueOrDefault();
                RegisterForDisposalWithPipeline(httpContext, this);

                GetTagValues(
                    request,
                    out string absoluteUri,
                    out string httpMethod,
                    out string host,
                    out string resourceName);

                SpanContext propagatedContext = null;
                var tracer = Tracer.Instance;

                if (tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var requestHeaders = request.GetProperty<IEnumerable>("Headers").GetValueOrDefault();

                        if (requestHeaders != null)
                        {
                            var headersCollection = new DictionaryHeadersCollection();

                            foreach (object header in requestHeaders)
                            {
                                var key = header.GetProperty<string>("Key").GetValueOrDefault();
                                var values = header.GetProperty<IList<string>>("Value").GetValueOrDefault();

                                if (key != null && values != null)
                                {
                                    headersCollection.Add(key, values);
                                }
                            }

                            propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException("Error extracting propagated HTTP headers.", ex);
                    }
                }

                _rootScope = _scope = tracer.StartActive(DefaultOperationName, propagatedContext);

                RegisterForDisposal(_rootScope);

                var span = _rootScope.Span;

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: httpMethod,
                    host: host,
                    httpUrl: absoluteUri);

                var statusCode = _httpContext.GetProperty("Response").GetProperty<int>("StatusCode");

                if (statusCode.HasValue)
                {
                    span.SetTag(Tags.HttpStatusCode, statusCode.Value.ToString());
                }

                var analyticSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting: true);
                span.SetMetric(Tags.Analytics, analyticSampleRate);
            }
            catch (Exception ex)
            {
                // unreachable code
                Log.Error($"Exception when initializing {nameof(WebServerIntegrationContext)}.", ex);
                throw;
            }
        }

        public void Dispose()
        {
            while (_disposables.TryPop(out IDisposable nextDisposable))
            {
                nextDisposable?.Dispose();
            }
        }

        internal static WebServerIntegrationContext Initialize(object httpContext)
        {
            WebServerIntegrationContext context = new WebServerIntegrationContext(DefaultOperationName, httpContext);

            if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
            {
                contextItems[HttpContextKey] = context;
            }

            return context;
        }

        internal static WebServerIntegrationContext RetrieveFromHttpContext(object httpContext)
        {
            WebServerIntegrationContext context = null;

            try
            {
                if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    if (contextItems?.ContainsKey(HttpContextKey) ?? false)
                    {
                        context = contextItems[HttpContextKey] as WebServerIntegrationContext;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error accessing {nameof(WebServerIntegrationContext)}.", ex);
            }

            return context;
        }

        internal void RegisterForDisposal(IDisposable disposable)
        {
            _disposables.Push(disposable);
        }

        internal Scope GetRootScope()
        {
            var currentLevel = _scope;

            while (currentLevel.Parent != null)
            {
                currentLevel = currentLevel.Parent;
            }

            return currentLevel;
        }

        internal void SetStatusCode(int statusCode)
        {
            SetTagOnRootSpan(Tags.HttpStatusCode, statusCode.ToString());
        }

        internal void SetTagOnRootSpan(string tag, string value)
        {
            _rootScope?.Span?.SetTag(tag, value);
        }

        internal void SetMetricOnRootSpan(string tag, double? value)
        {
            _rootScope?.Span?.SetMetric(tag, value);
        }

        internal bool SetExceptionOnRootSpan(Exception ex)
        {
            _rootScope?.Span?.SetException(ex);
            // Return false for use in exception filters
            return false;
        }

        internal void ResetWebServerRootTags(
            string operationName,
            string resourceName,
            string method,
            string host,
            string httpUrl)
        {
            if (_rootScope?.Span != null)
            {
                _rootScope.Span.OperationName = operationName;
                _rootScope.Span.ResourceName = resourceName?.Trim();
                SetTagOnRootSpan(Tags.SpanKind, SpanKinds.Server);
                SetTagOnRootSpan(Tags.HttpMethod, method);
                SetTagOnRootSpan(Tags.HttpRequestHeadersHost, host);
                SetTagOnRootSpan(Tags.HttpUrl, httpUrl);
            }
        }

        private static bool ExceptionFilterDispose(IDisposable disposable)
        {
            disposable?.Dispose();
            return false;
        }

        private static void GetTagValues(
            object request,
            out string url,
            out string httpMethod,
            out string host,
            out string resourceName)
        {
            host = request.GetProperty("Host").GetProperty<string>("Value").GetValueOrDefault();

            httpMethod = request.GetProperty<string>("Method").GetValueOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";

            string pathBase = request.GetProperty("PathBase").GetProperty<string>("Value").GetValueOrDefault();

            string path = request.GetProperty("Path").GetProperty<string>("Value").GetValueOrDefault();

            string queryString = request.GetProperty("QueryString").GetProperty<string>("Value").GetValueOrDefault();

            string scheme = request.GetProperty<string>("Scheme").GetValueOrDefault()?.ToUpperInvariant() ?? "http";

            url = $"{pathBase}{path}{queryString}";

            string resourceUrl = UriHelpers.GetRelativeUrl(new Uri($"{scheme}://{host}{url}"), tryRemoveIds: true).ToLowerInvariant();

            resourceName = $"{httpMethod} {resourceUrl}";
        }

        private static void RegisterForDisposalWithPipeline(object httpContext, IDisposable disposable)
        {
            try
            {
                var response = httpContext.GetProperty("Response").GetValueOrDefault();

                if (response == null)
                {
                    Log.Error($"HttpContext.Response is null, unable to register {disposable.GetType().FullName}");
                    return;
                }

                var disposalRegisterMethod = response.GetType().GetMethod("RegisterForDispose");
                disposalRegisterMethod.Invoke(response, new object[] { disposable });
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to register {disposable.GetType().FullName}", ex);
            }
        }
    }
}
