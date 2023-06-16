// <copyright file="AsyncCallServerSendStatusFromServerAsyncInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
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
        internal static CallTargetState OnMethodBegin<TTarget, TStatus, TMetadata, TResponse>(TTarget instance, in TStatus status, in TMetadata trailers, in TResponse response)
        {
            var tracer = Tracer.Instance;
            if (tracer.ActiveScope is Scope { Span: { Tags: GrpcServerTags } span })
            {
                // Use CreateFrom to avoid boxing
                var clientStatus = DuckType.CreateCache<StatusStruct>.CreateFrom(status);
                GrpcCommon.RecordFinalStatus(span, clientStatus.StatusCode, clientStatus.Detail, clientStatus.DebugException);

                if (trailers is not null)
                {
                    var metadata = trailers.DuckCast<IMetadata>();
                    if (metadata.Count > 0)
                    {
                        span.SetHeaderTags(new MetadataHeadersCollection(metadata), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                    }
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
