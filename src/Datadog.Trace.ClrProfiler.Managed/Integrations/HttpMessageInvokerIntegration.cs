using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpClientInvoker.
    /// </summary>
    public static class HttpMessageInvokerIntegration
    {
        private const string IntegrationName = "HttpMessageInvoker";
        private const string SystemNetHttp = "System.Net.Http";
        private const string Major4 = "4";

        private const string HttpMessageInvoker = "System.Net.Http.HttpMessageInvoker";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpMessageInvokerIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpMessageInvoker.SendAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpMessageInvoker"/> instance to instrument.</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that represents the current HTTP request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
        TargetAssembly = SystemNetHttp,
        TargetType = HttpMessageInvoker,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object SendAsync(
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

            const string methodName = nameof(SendAsync);

            // original signature:
            // Task<HttpResponseMessage> HttpMessageInvoker.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpMessageInvoker = handler.GetInstrumentedType(HttpMessageInvoker);

            Func<HttpMessageInvoker, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpMessageInvoker, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(httpMessageInvoker)
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
                    instrumentedType: HttpMessageInvoker,
                    methodName: methodName,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpMessageInvoker : handler.GetType(),
                (HttpMessageInvoker)handler,
                (HttpRequestMessage)request,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> SendAsyncInternal(
            Func<HttpMessageInvoker, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync,
            Type reportedType,
            HttpMessageInvoker handler,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!IsTracingEnabled(request))
            {
                // skip instrumentation
                return await sendAsync(handler, request, cancellationToken).ConfigureAwait(false);
            }

            string httpMethod = request.Method?.Method;

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpMethod, request.RequestUri, IntegrationName))
            {
                try
                {
                    if (scope != null)
                    {
                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());
                    }

                    HttpResponseMessage response = await sendAsync(handler, request, cancellationToken).ConfigureAwait(false);

                    // this tag can only be set after the response is returned
                    scope?.Span.SetTag(Tags.HttpStatusCode, ((int)response.StatusCode).ToString());

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
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
