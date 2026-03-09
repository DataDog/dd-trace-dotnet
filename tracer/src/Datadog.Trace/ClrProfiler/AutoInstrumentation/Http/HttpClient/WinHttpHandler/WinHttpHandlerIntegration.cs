// <copyright file="WinHttpHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.WinHttpHandler
{
    /// <summary>
    /// System.Net.Http.WinHttpHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "System.Net.Http", "System.Net.Http.WinHttpHandler" },
        TypeName = "System.Net.Http.WinHttpHandler",
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "4.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class WinHttpHandlerIntegration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.HttpMessageHandler);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.HttpMessageHandler;
        private const IntegrationId WinHttpHandlerIntegrationId = IntegrationId.WinHttpHandler;

#if NETCOREAPP
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, in System.Net.Http.HttpRequestMessage requestMessage, CancellationToken cancellationToken)
#else
        internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken)
            where TRequest : IHttpRequestMessage
#endif
        {
            return HttpMessageHandlerCommon.OnMethodBegin(instance, requestMessage, cancellationToken, IntegrationId, implementationIntegrationId: WinHttpHandlerIntegrationId);
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
