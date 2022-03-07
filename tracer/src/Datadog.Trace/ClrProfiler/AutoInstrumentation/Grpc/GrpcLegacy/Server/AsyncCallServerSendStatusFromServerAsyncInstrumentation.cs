// <copyright file="AsyncCallServerSendStatusFromServerAsyncInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Server
{
    /// <summary>
    /// Grpc.Core.Internal calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.Core",
        TypeName = "Grpc.Core.Internal.AsyncCallServer`2",
        MethodName = "SendStatusFromServerAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Grpc.Core.Status", "Grpc.Core.Metadata", "System.Nullable`1[ResponseWithFlags[!0,!1]]" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncCallServerSendStatusFromServerAsyncInstrumentation
    {
        internal static CallTargetState OnMethodBegin<TTarget, TStatus, TMetadata, TResponse>(TTarget instance, TStatus status, TMetadata trailers, in TResponse response)
            where TStatus : IStatus
            where TMetadata : IMetadata
        {
            var tracer = Tracer.Instance;
            if (tracer.ActiveScope is Scope { Span: { Tags: GrpcServerTags } span })
            {
                GrpcCommon.RecordFinalStatus(span, status.StatusCode, status.Detail, status.DebugException);
                if (trailers is { Count: > 0 })
                {
                    span.SetHeaderTags(new MetadataHeadersCollection<TMetadata>(trailers), tracer.Settings.GrpcTags, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
