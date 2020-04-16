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
    /// Tracer integration for HttpClient.
    /// </summary>
    public static class HttpClientIntegration
    {
        private const string IntegrationName = "HttpClient";
        private const string SystemNetHttp = "System.Net.Http";
        private const string Major4 = "4";
        private const string HttpClientTarget = "System.Net.Http.HttpClient";
        private const string SendAsync = "SendAsync";
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpClientIntegration));

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpClient.SendAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClient"/> instance to instrument.</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that represents the current HTTP request.</param>
        /// <param name="completionOption">The <see cref="HttpCompletionOption"/> that is passed in the current request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = SendAsync,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.HttpCompletionOption, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_SendAsync(
            object handler,
            object request,
            int completionOption,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var compOption = (HttpCompletionOption)completionOption;
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, HttpRequestMessage, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, HttpRequestMessage, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(request, compOption, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.HttpCompletionOption, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: SendAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (HttpRequestMessage)request,
                compOption,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> SendAsyncInternal(
            Func<HttpClient, HttpRequestMessage, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>> sendAsync,
            Type reportedType,
            HttpClient handler,
            HttpRequestMessage request,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            if (!IsTracingEnabled(request))
            {
                // skip instrumentation
                return await sendAsync(handler, request, completionOption, cancellationToken).ConfigureAwait(false);
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

                    HttpResponseMessage response = await sendAsync(handler, request, completionOption, cancellationToken).ConfigureAwait(false);

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
