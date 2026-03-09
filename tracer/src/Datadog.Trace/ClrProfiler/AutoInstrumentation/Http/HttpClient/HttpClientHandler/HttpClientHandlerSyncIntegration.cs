// <copyright file="HttpClientHandlerSyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler
{
    /// <summary>
    /// System.Net.Http.HttpClientHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Net.Http",
        TypeName = "System.Net.Http.HttpClientHandler",
        MethodName = "Send",
        ReturnTypeName = ClrNames.HttpResponseMessage,
        ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HttpClientHandlerSyncIntegration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.HttpMessageHandler);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.HttpMessageHandler;

#if NETCOREAPP
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, in System.Net.Http.HttpRequestMessage requestMessage, CancellationToken cancellationToken)
#else
        internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken)
            where TRequest : IHttpRequestMessage
#endif
        {
            return HttpMessageHandlerCommon.OnMethodBegin(instance, requestMessage, cancellationToken, IntegrationId, implementationIntegrationId: null);
        }

#if NETCOREAPP
        internal static CallTargetReturn<System.Net.Http.HttpResponseMessage> OnMethodEnd<TTarget>(TTarget instance, System.Net.Http.HttpResponseMessage responseMessage, Exception exception, in CallTargetState state)
#else
        internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, in CallTargetState state)
            where TResponse : IHttpResponseMessage
#endif
        {
            return new(HttpMessageHandlerCommon.OnMethodEnd(instance, responseMessage, exception, in state));
        }
    }
}
