// <copyright file="CurlHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.CurlHandler
{
    /// <summary>
    /// System.Net.Http.CurlHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Net.Http",
        TypeName = "System.Net.Http.CurlHandler",
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class CurlHandlerIntegration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.HttpMessageHandler);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.HttpMessageHandler;
        private const IntegrationId CurlHandlerIntegrationId = IntegrationId.CurlHandler;

#if NETCOREAPP
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, System.Net.Http.HttpRequestMessage requestMessage, CancellationToken cancellationToken)
#else
        internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken)
            where TRequest : IHttpRequestMessage
#endif
        {
            return HttpMessageHandlerCommon.OnMethodBegin(instance, requestMessage, cancellationToken, IntegrationId, implementationIntegrationId: CurlHandlerIntegrationId);
        }

#if NETCOREAPP
        internal static System.Net.Http.HttpResponseMessage OnAsyncMethodEnd<TTarget>(TTarget instance, System.Net.Http.HttpResponseMessage responseMessage, Exception exception, in CallTargetState state)
#else
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, in CallTargetState state)
            where TResponse : IHttpResponseMessage
#endif
        {
            return HttpMessageHandlerCommon.OnMethodEnd(instance, responseMessage, exception, in state);
        }
    }
}
