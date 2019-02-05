using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for HttpMessageHandler.
    /// </summary>
    public static class HttpMessageHandlerIntegration
    {
        internal const string OperationName = "http.request";

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
            var executeAsync = DynamicMethodBuilder<Func<object, object, CancellationToken, Task<HttpResponseMessage>>>
               .GetOrCreateMethodCallDelegate(
                    handler.GetType(),
                    nameof(SendAsync));

            using (var scope = CreateScope(handler, request))
            {
                try
                {
                    // add distributed tracing headers
                    request.Headers.Inject(scope.Span.Context);

                    return await executeAsync(handler, request, cancellationToken);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(HttpMessageHandler handler, HttpRequestMessage request)
        {
            string httpMethod = request.Method.ToString().ToUpperInvariant();
            string url = request.RequestUri.OriginalString;
            string resourceName = $"{httpMethod} {url}";

            var tracer = Tracer.Instance;
            var scope = tracer.StartActive(OperationName, serviceName: tracer.DefaultServiceName);
            var span = scope.Span;

            span.Type = SpanTypes.Http;
            span.ResourceName = resourceName;

            span.SetTag(Tags.HttpMethod, httpMethod);
            span.SetTag(Tags.HttpUrl, url);
            span.SetTag("handler-type", handler.GetType().FullName);

            return scope;
        }
    }
}
