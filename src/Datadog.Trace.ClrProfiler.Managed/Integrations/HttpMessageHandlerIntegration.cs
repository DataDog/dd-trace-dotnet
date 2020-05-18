using System;
using System.Linq;
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

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpMessageHandlerIntegration));

        /// <summary>
        /// Instrumentation wrapper for System.Net.Http.HttpMessageHandler.SendAsync.
        /// </summary>
        /// <param name="handler">The <see cref="HttpMessageHandler"/> instance to instrument.</param>
        /// <param name="request">The HttpRequestMessage that represents the current HTTP request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
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
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // original signature:
            // Task<HttpResponseMessage> HttpMessageHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpMessageHandler = handler.GetInstrumentedType(HttpMessageHandler);

            Func<object, object, CancellationToken, Task<object>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, Task<object>>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpMessageHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpMessageHandler,
                    methodName: SendAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpMessageHandler : handler.GetType(),
                handler,
                request,
                cancellationToken);
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClientHandler.SendAsync.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClientHandler"/> instance to instrument.</param>
        /// <param name="request">The HttpRequestMessage that represents the current HTTP request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
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
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // original signature:
            // Task<HttpResponseMessage> HttpClientHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClientHandler = handler.GetInstrumentedType(HttpClientHandler);

            Func<object, object, CancellationToken, Task<object>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, Task<object>>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpClientHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientHandler,
                    methodName: SendAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClientHandler : handler.GetType(),
                handler,
                request,
                cancellationToken);
        }

        private static async Task<object> SendAsyncInternal(
            Func<object, object, CancellationToken, Task<object>> sendAsync,
            Type reportedType,
            object handler,
            dynamic request,
            CancellationToken cancellationToken)
        {
            // if (!(handler is HttpClientHandler || IsSocketsHttpHandlerEnabled(reportedType)) ||
            //    !IsTracingEnabled(request))
            // if (!(handler is HttpClientHandler))
            // {
            //    // skip instrumentation
            //    return await sendAsync(handler, request, cancellationToken).ConfigureAwait(false);
            // }

            // if (!(IsSocketsHttpHandlerEnabled(reportedType)))
            // {
            //    // skip instrumentation
            //    return await sendAsync(handler, request, cancellationToken).ConfigureAwait(false);
            // }

            // if (!IsTracingEnabled(request))
            // {
            //    // skip instrumentation
            //    return await sendAsync(handler, request, cancellationToken).ConfigureAwait(false);
            // }

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

                    dynamic response = await sendAsync(handler, request, cancellationToken).ConfigureAwait(false);

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

        private static bool IsSocketsHttpHandlerEnabled(Type reportedType)
        {
            return Tracer.Instance.Settings.IsOptInIntegrationEnabled("HttpSocketsHandler") && reportedType.FullName.Equals("System.Net.Http.SocketsHttpHandler", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTracingEnabled(dynamic request)
        {
            if (request.Headers.TryGetValues(HttpHeaderNames.TracingEnabled, out dynamic headerValues))
            {
                foreach (var hdr in headerValues)
                {
                    if (((string)hdr).Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        // tracing is disabled for this request via http header
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
