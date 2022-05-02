// <copyright file="ServerCallHandlerInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Server
{
    /// <summary>
    /// Grpc.Core.Internal calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.Core",
#pragma warning disable SA1118 // Parameters should not span multiple lines
        TypeNames = new[]
        {
            "Grpc.Core.Internal.UnaryServerCallHandler`2",
            "Grpc.Core.Internal.ServerStreamingServerCallHandler`2",
            "Grpc.Core.Internal.ClientStreamingServerCallHandler`2",
            "Grpc.Core.Internal.DuplexStreamingServerCallHandler`2",
        },
#pragma warning restore SA1118
        MethodName = "HandleCall",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Grpc.Core.Internal.ServerRpcNew", "Grpc.Core.Internal.CompletionQueueSafeHandle" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ServerCallHandlerInstrumentation
    {
        internal static CallTargetState OnMethodBegin<TTarget, TServerRpc, TCompletionQueue>(TTarget instance, TServerRpc serverRpc, in TCompletionQueue completionQueue)
            where TServerRpc : IServerRpcNew
        {
            var tracer = Tracer.Instance;
            if (GrpcCoreApiVersionHelper.IsSupported && tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc))
            {
                var scope = GrpcLegacyServerCommon.CreateServerSpan(tracer, instance, serverRpc.RequestMetadata);
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (exception is not null
             && state.Scope is { Span.Tags : GrpcServerTags } scope)
            {
                // The only way I've seen for an exception to be thrown here is when the deadline is exceeded.
                // In that situation AsyncCallServer.HandleFinishedServerside() is called with cancelled=false, but
                // that's called on a completely different thread, and there's no way to tie it back to this request.
                // So instead, we  explicitly set it as cancelled here.
                GrpcCommon.RecordFinalStatus(scope.Span, grpcStatusCode: 4, errorMessage: "DeadlineExceeded", exception);
                state.Scope.Dispose();
            }
            else
            {
                // Otherwise, just record the exception as normal
                state.Scope?.DisposeWithException(exception);
            }

            return returnValue;
        }
    }
}
