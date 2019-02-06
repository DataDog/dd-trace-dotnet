using System;
using System.Net;
using System.Threading.Tasks;

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
        /// <param name="request">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Net",
            TargetType = "System.Net.WebRequest")]
        public static object GetResponse(object request)
        {
            var executeAsync = DynamicMethodBuilder<Func<WebRequest, WebResponse>>
               .GetOrCreateMethodCallDelegate(
                    request.GetType(),
                    nameof(GetResponse));

            var webRequest = (WebRequest)request;

            using (var scope = CreateScope(webRequest))
            {
                try
                {
                    // add distributed tracing headers
                    // request.Headers.Inject(scope.Span.Context);

                    return executeAsync(webRequest);
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
            var executeAsync = DynamicMethodBuilder<Func<WebRequest, Task<WebResponse>>>
               .GetOrCreateMethodCallDelegate(
                    request.GetType(),
                    nameof(GetResponseAsync));

            using (var scope = CreateScope(request))
            {
                try
                {
                    // add distributed tracing headers
                    // request.Headers.Inject(scope.Span.Context);

                    return await executeAsync(request);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(WebRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
