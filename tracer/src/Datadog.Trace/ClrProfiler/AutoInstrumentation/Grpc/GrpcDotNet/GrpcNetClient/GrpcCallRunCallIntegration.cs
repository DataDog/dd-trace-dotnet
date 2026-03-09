// <copyright file="GrpcCallRunCallIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NET461

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient;

/// <summary>
/// Grpc.Net.Client.Internal.GrpcCall calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Grpc.Net.Client",
    TypeName = "Grpc.Net.Client.Internal.GrpcCall`2",
    MethodName = "RunCall",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, "System.Nullable`1[System.TimeSpan]" },
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(Grpc))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GrpcCallRunCallIntegration
{
#if NETCOREAPP
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, in System.Net.Http.HttpRequestMessage requestMessage, TimeSpan? timeout)
#else
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, TimeSpan? timeout)
        where TRequest : IHttpRequestMessage
#endif
    {
        if (GrpcCoreApiVersionHelper.IsSupported)
        {
            var scope = GrpcDotNetClientCommon.CreateClientSpan(Tracer.Instance, instance, requestMessage);
            return new CallTargetState(scope);
        }

        return CallTargetState.GetDefault();
    }

    internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
    {
        // Not setting exception here as it will always be null (any error would be stored in TCS on the instance)
        // We set the error (if any) in the FinishCall integration instead
        state.Scope?.Dispose();
        return response;
    }
}
#endif
