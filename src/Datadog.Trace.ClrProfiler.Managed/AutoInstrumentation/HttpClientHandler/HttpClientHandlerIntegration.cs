using System;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HttpClientHandler
{
    /// <summary>
    /// System.Net.Http.HttpClientHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "System.Net.Http",
        Type = "System.Net.Http.HttpClientHandler",
        Method = "SendAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParametersTypesNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "4.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = IntegrationName)]
    public class HttpClientHandlerIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.HttpMessageHandler);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TRequest">Type of the request</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="requestMessage">HttpRequest message instance</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken)
            where TRequest : IHttpRequestMessage
        {
            if (IsTracingEnabled(requestMessage.Headers))
            {
                Scope scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, requestMessage.Method.Method, requestMessage.RequestUri, IntegrationId, out HttpTags tags);
                if (scope != null)
                {
                    tags.HttpClientHandlerType = instance.GetType().FullName;

                    // add distributed tracing headers to the HTTP request
                    SpanContextPropagator.Instance.Inject(scope.Span.Context, new HttpHeadersCollection(requestMessage.Headers));

                    return new CallTargetState(scope);
                }
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="responseMessage">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, CallTargetState state)
            where TResponse : IHttpResponseMessage
        {
            Scope scope = state.Scope;

            if (scope is null)
            {
                return responseMessage;
            }

            try
            {
                scope.Span.SetHttpStatusCode(responseMessage.StatusCode, isServer: false);

                if (exception != null)
                {
                    scope.Span.SetException(exception);
                }
            }
            finally
            {
                scope.Dispose();
            }

            return responseMessage;
        }

        private static bool IsTracingEnabled(IRequestHeaders headers)
        {
            if (headers.TryGetValues(HttpHeaderNames.TracingEnabled, out var headerValues))
            {
                if (headerValues is string[] arrayValues)
                {
                    for (var i = 0; i < arrayValues.Length; i++)
                    {
                        if (string.Equals(arrayValues[i], "false", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                if (headerValues != null && headerValues.Any(s => string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)))
                {
                    // tracing is disabled for this request via http header
                    return false;
                }
            }

            return true;
        }
    }
}
