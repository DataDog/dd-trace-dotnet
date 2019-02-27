using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for WebRequest.
    /// </summary>
    public static class WebRequestIntegration
    {
        internal const string OperationName = "http.request";
        internal const string ServiceName = "http-client";

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponse"/>.
        /// </summary>
        /// <param name="request">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = "System.Net.WebRequest")]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = "System.Net.WebRequest")]
        public static object GetResponse(object request)
        {
            var webRequest = (WebRequest)request;

            if (!IsTracingEnabled(webRequest))
            {
                return webRequest.GetResponse();
            }

            using (var scope = CreateScope(webRequest, nameof(GetResponse)))
            {
                try
                {
                    // add distributed tracing headers
                    webRequest.Headers.Inject(scope.Span.Context);

                    WebResponse response = webRequest.GetResponse();

                    if (response is HttpWebResponse webResponse)
                    {
                        scope.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponseAsync"/>.
        /// </summary>
        /// <param name="request">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Net",
            TargetType = "System.Net.WebRequest")]
        public static object GetResponseAsync(object request)
        {
            return GetResponseAsyncInternal((WebRequest)request);
        }

        private static async Task<WebResponse> GetResponseAsyncInternal(WebRequest request)
        {
            if (!IsTracingEnabled(request))
            {
                return await request.GetResponseAsync().ConfigureAwait(false);
            }

            using (var scope = CreateScope(request, nameof(GetResponseAsync)))
            {
                try
                {
                    // add distributed tracing headers
                    request.Headers.Inject(scope.Span.Context);

                    WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);

                    if (response is HttpWebResponse webResponse)
                    {
                        scope.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static bool IsTracingEnabled(WebRequest request)
        {
            // check if tracing is disabled for this request via http header
            string value = request.Headers[HttpHeaderNames.TracingEnabled];
            return !string.Equals(value, "false", StringComparison.InvariantCultureIgnoreCase);
        }

        private static Scope CreateScope(WebRequest request, string methodName)
        {
            var tracer = Tracer.Instance;

            string httpMethod = request.Method.ToUpperInvariant();
            string url = request.RequestUri.OriginalString;
            string resourceName = $"{httpMethod} {url}";
            string serviceName = $"{tracer.DefaultServiceName}-{ServiceName}";

            var scope = tracer.StartActive(OperationName, serviceName: serviceName);
            var span = scope.Span;
            span.Type = SpanTypes.Http;
            span.ResourceName = resourceName;
            span.SetTag(Tags.HttpMethod, httpMethod);
            span.SetTag(Tags.HttpUrl, url);
            span.SetTag(Tags.InstrumentationName, nameof(WebRequestIntegration).TrimEnd("Integration"));
            span.SetTag(Tags.InstrumentationMethod, $"{request.GetType().FullName}.{methodName}");

            return scope;
        }
    }
}
