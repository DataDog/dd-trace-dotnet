// <copyright file="SocketsHttpHandlerIntegration.cs" company="Datadog">
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
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "4.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SocketsHttpHandlerIntegration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.HttpMessageHandler);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.HttpMessageHandler;
        private const IntegrationId SocketHandlerIntegrationId = IntegrationId.HttpSocketsHandler;

        internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken)
            where TRequest : IHttpRequestMessage
        {
            return HttpMessageHandlerCommon.OnMethodBegin(instance, requestMessage, cancellationToken, IntegrationId, implementationIntegrationId: SocketHandlerIntegrationId);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, in CallTargetState state)
            where TResponse : IHttpResponseMessage
        {
            return HttpMessageHandlerCommon.OnMethodEnd(instance, responseMessage, exception, in state);
        }
    }
}
