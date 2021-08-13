// <copyright file="SocketsHttpHandlerSyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.SocketsHttpHandler
{
    /// <summary>
    /// System.Net.Http.SocketsHttpHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Net.Http",
        TypeName = "System.Net.Http.SocketsHttpHandler",
        MethodName = "Send",
        ReturnTypeName = ClrNames.HttpResponseMessage,
        ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SocketsHttpHandlerSyncIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.HttpMessageHandler);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
        private static readonly IntegrationInfo SocketHandlerIntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.HttpSocketsHandler));
        private static readonly Func<bool> IsIntegrationEnabledFunc = () => Tracer.Instance.Settings.IsIntegrationEnabled(SocketHandlerIntegrationId, defaultValue: true);

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
            return HttpMessageHandlerCommon.OnMethodBegin(instance, requestMessage, cancellationToken, IntegrationId, IsIntegrationEnabledFunc);
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
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, CallTargetState state)
            where TResponse : IHttpResponseMessage
        {
            return new CallTargetReturn<TResponse>(HttpMessageHandlerCommon.OnMethodEnd(instance, responseMessage, exception, state));
        }
    }
}
