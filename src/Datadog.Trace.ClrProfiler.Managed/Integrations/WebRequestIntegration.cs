using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for WebRequest.
    /// </summary>
    public static class WebRequestIntegration
    {
        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponse"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = "System.Net.WebRequest")]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = "System.Net.WebRequest")]
        public static object GetResponse(object webRequest)
        {
            var request = (WebRequest)webRequest;

            if (!IsTracingEnabled(request))
            {
                return request.GetResponse();
            }

            string httpMethod = request.Method.ToUpperInvariant();
            string integrationName = typeof(WebRequestIntegration).Name.TrimEnd("Integration", StringComparison.OrdinalIgnoreCase);

            using (var scope = ScopeFactory.CreateOutboundHttpScope(httpMethod, request.RequestUri, integrationName))
            {
                try
                {
                    if (scope != null)
                    {
                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());
                    }

                    WebResponse response = request.GetResponse();

                    if (response is HttpWebResponse webResponse)
                    {
                        scope?.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
                    }

                    return response;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
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

            string httpMethod = request.Method.ToUpperInvariant();
            string integrationName = typeof(WebRequestIntegration).Name.TrimEnd("Integration", StringComparison.OrdinalIgnoreCase);

            using (var scope = ScopeFactory.CreateOutboundHttpScope(httpMethod, request.RequestUri, integrationName))
            {
                try
                {
                    if (scope != null)
                    {
                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());
                    }

                    WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);

                    if (response is HttpWebResponse webResponse)
                    {
                        scope?.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
                    }

                    return response;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static bool IsTracingEnabled(WebRequest request)
        {
            // check if tracing is disabled for this request via http header
            string value = request.Headers[HttpHeaderNames.TracingEnabled];
            return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
