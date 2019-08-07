using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpClientHandler.
    /// </summary>
    public static class HttpMessageHandlerIntegration
    {
        private const string IntegrationName = "HttpMessageHandler";
        private const string SystemNetHttp = "System.Net.Http";
        private const string Major4 = "4";

        private const string HttpMessageHandler = "System.Net.Http.HttpMessageHandler";
        private const string HttpClientHandler = "System.Net.Http.HttpClientHandler";
        private const string SendAsync = "SendAsync";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(HttpMessageHandlerIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpMessageHandler.SendAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpMessageHandler"/> instance to instrument.</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that represents the current HTTP request.</param>
        /// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> that can be used to cancel this <c>async</c> operation.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = SystemNetHttp,
            TargetType = HttpMessageHandler,
            TargetMethod = SendAsync,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object HttpMessageHandler_SendAsync(
            object handler,
            object request,
            object cancellationTokenSource,
            int opCode,
            int mdToken)
        {
            // original signature:
            // Task<HttpResponseMessage> HttpMessageHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var callOpCode = (OpCodeValue)opCode;
            var httpMessageHandler = typeof(HttpMessageHandler); // Note the HttpMessageHandler to match the method call we replaced

            Func<HttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode, SendAsync)
                       .WithConcreteType(httpMessageHandler)
                       .WithParameters(request, cancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error resolving {HttpMessageHandler}.{SendAsync}(...)", ex);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpMessageHandler : handler.GetType(),
                (HttpMessageHandler)handler,
                (HttpRequestMessage)request,
                cancellationToken);
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpClientHandler.SendAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClientHandler"/> instance to instrument.</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that represents the current HTTP request.</param>
        /// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> that can be used to cancel this <c>async</c> operation.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = SystemNetHttp,
            TargetType = HttpClientHandler,
            TargetMethod = SendAsync,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object HttpClientHandler_SendAsync(
            object handler,
            object request,
            object cancellationTokenSource,
            int opCode,
            int mdToken)
        {
            // original signature:
            // Task<HttpResponseMessage> HttpClientHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var callOpCode = (OpCodeValue)opCode;
            var httpClientHandler = typeof(HttpClientHandler); // Note the HttpClientHandler to match the method call we replaced

            Func<HttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode, SendAsync)
                       .WithConcreteType(httpClientHandler)
                       .WithParameters(request, cancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error resolving {HttpClientHandler}.{SendAsync}(...)", ex);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClientHandler : handler.GetType(),
                (HttpMessageHandler)handler,
                (HttpRequestMessage)request,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> SendAsyncInternal(
            Func<HttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync,
            Type reportedType,
            HttpMessageHandler handler,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!(handler is HttpClientHandler) || !IsTracingEnabled(request))
            {
                // skip instrumentation
                return await sendAsync(handler, request, cancellationToken);
            }

            string httpMethod = request.Method?.Method;

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpMethod, request.RequestUri, IntegrationName))
            {
                try
                {
                    if (scope != null)
                    {
                        scope.Span.SetTag("http-client-handler-type", reportedType.FullName);

                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());
                    }

                    HttpResponseMessage response = await sendAsync(handler, request, cancellationToken);

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
