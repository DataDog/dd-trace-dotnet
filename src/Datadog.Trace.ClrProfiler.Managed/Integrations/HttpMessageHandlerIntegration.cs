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
            TargetType = "System.Net.Http.HttpClientHandler")] // .NET Framework and .NET Core 2.0 and earlier
        /*
        [InterceptMethod(
            TargetAssembly = "System.Net.Http",
            TargetType = "System.Net.Http.SocketsHttpHandler")] // .NET Core 2.1 and later
        */
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

            var handlerTypeName = handler.GetType().FullName;

            if (handlerTypeName != "System.Net.Http.HttpClientHandler" || !IsTracingEnabled(request))
            {
                // skip instrumentation
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
                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());
                    }

                    HttpResponseMessage response = await executeAsync(handler, request, cancellationToken).ConfigureAwait(false);

                    // this tag can only be set after the response is returned
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
