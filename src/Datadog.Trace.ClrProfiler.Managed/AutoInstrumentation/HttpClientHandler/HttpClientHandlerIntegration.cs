using System;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
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
            Scope scope = null;
            HttpTags tags = null;

            if (IsTracingEnabled(requestMessage.Headers))
            {
                scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, requestMessage.Method.Method, requestMessage.RequestUri, IntegrationId, out tags);
                if (scope != null)
                {
                    tags.HttpClientHandlerType = instance.GetType().FullName;

                    // add distributed tracing headers to the HTTP request
                    SpanContextPropagator.Instance.Inject(scope.Span.Context, new HttpHeadersCollection(requestMessage.Headers));
                }
            }

            return new CallTargetState(new IntegrationState(scope, tags));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            IntegrationState integrationState = (IntegrationState)state.State;
            if (integrationState.Scope != null)
            {
                // Before returning the control flow we need to restore the parent Scope setted by ScopeFactory.CreateOutboundHttpScope
                // This doesn't affect to OnAsyncMethodEnd async continuation, an ExecutionContext is captured
                // by the inner await.
                IScopeManager scopeManager = ((IDatadogTracer)Tracer.Instance).ScopeManager;
                if (scopeManager.Active == integrationState.Scope)
                {
                    scopeManager.Close(integrationState.Scope);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
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
            IntegrationState integrationState = (IntegrationState)state.State;
            if (integrationState.Scope is null)
            {
                return responseMessage;
            }

            try
            {
                if (exception is null)
                {
                    if (integrationState.Tags != null)
                    {
                        integrationState.Tags.HttpStatusCode = HttpTags.ConvertStatusCodeToString(responseMessage.StatusCode);
                    }
                }
                else
                {
                    integrationState.Scope.Span.SetException(exception);
                }
            }
            finally
            {
                integrationState.Scope.Dispose();
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

        private readonly struct IntegrationState
        {
            public readonly Scope Scope;
            public readonly HttpTags Tags;

            public IntegrationState(Scope scope, HttpTags tags)
            {
                Scope = scope;
                Tags = tags;
            }
        }
    }
}
