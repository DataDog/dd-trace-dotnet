using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpMessageHandler.
    /// </summary>
    public static class HttpMessageHandlerIntegration
    {
        internal const string OperationName = "http.request";
        internal const string ServiceName = "http-client";

        // internal readonly  string Name = nameof(HttpMessageHandlerIntegration).Substring(0, )

        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpMessageHandler.SendAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpMessageHandler"/> instance to instrument.</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that represents the current HTTP request.</param>
        /// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> that can be used to cancel this <c>async</c> operation.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Net.Http",
            TargetType = "System.Net.Http.HttpMessageHandler")]
        [InterceptMethod(
            TargetAssembly = "System.Net.Http",
            TargetType = "System.Net.Http.HttpClientHandler")]
        public static object SendAsync(
            object handler,
            object request,
            object cancellationTokenSource)
        {
            // HttpMessageHandler
            // Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            return SendAsyncInternal(
                (HttpMessageHandler)handler,
                (HttpRequestMessage)request,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> SendAsyncInternal(
            HttpMessageHandler handler,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var executeAsync = DynamicMethodBuilder<Func<HttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>
               .GetOrCreateMethodCallDelegate(
                    handler.GetType(),
                    nameof(SendAsync));

            if (!IsTracingEnabled(request))
            {
                return await executeAsync(handler, request, cancellationToken).ConfigureAwait(false);
            }

            string httpMethod = request.Method.ToString().ToUpperInvariant();
            string integrationName = typeof(HttpMessageHandlerIntegration).Name.TrimEnd("Integration", StringComparison.OrdinalIgnoreCase);

            using (var scope = ScopeFactory.CreateOutboundHttpScope(httpMethod, request.RequestUri, integrationName))
            {
                try
                {
                    if (scope != null)
                    {
                        // add distributed tracing headers
                        request.Headers.Inject(scope.Span.Context);
                    }

                    HttpResponseMessage response = await executeAsync(handler, request, cancellationToken).ConfigureAwait(false);

                    scope?.Span.SetTag(Tags.HttpStatusCode, ((int)response.StatusCode).ToString());
                    return response;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static bool IsTracingEnabled(HttpRequestMessage request)
        {
            if (request.Headers.TryGetValues(HttpHeaderNames.TracingEnabled, out var headerValues))
            {
                if (headerValues.Any(s => string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)))
                {
                    // tracing is disabled for this request via http header
                    return false;
                }
            }

            return true;
        }
    }
}
