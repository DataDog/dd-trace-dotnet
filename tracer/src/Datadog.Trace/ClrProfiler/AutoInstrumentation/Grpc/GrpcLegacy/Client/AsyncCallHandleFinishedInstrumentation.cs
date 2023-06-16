// <copyright file="AsyncCallHandleFinishedInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    /// <summary>
    /// Grpc.Core.Internal calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.Core",
        TypeName = "Grpc.Core.Internal.AsyncCall`2",
        MethodName = "HandleFinished",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Bool, "Grpc.Core.Internal.ClientSideStatus" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncCallHandleFinishedInstrumentation
    {
        internal static CallTargetState OnMethodBegin<TTarget, TStatus>(TTarget instance, bool success, in TStatus clientSideStatus)
        {
            var tracer = Tracer.Instance;
            if (GrpcCoreApiVersionHelper.IsSupported && tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc))
            {
                var asyncCall = instance.DuckCast<AsyncCallStruct>();

                // using CreateFrom to avoid boxing ClientSideStatus struct
                var receivedStatus = DuckType.CreateCache<ClientSideStatusWithMetadataStruct>.CreateFrom(clientSideStatus);
                var status = receivedStatus.Status;
                var scope = GrpcLegacyClientCommon.CreateClientSpan(tracer, in asyncCall.Details, in status);
                if (scope?.Span is { } span)
                {
                    if (receivedStatus.Trailers is { Count: > 0 })
                    {
                        span.SetHeaderTags(new MetadataHeadersCollection(receivedStatus.Trailers), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                    }
                    else if (asyncCall.ResponseHeadersAsync is { IsCompleted: true } task)
                    {
                        var metadata = task.DuckCast<TaskOfMetadataStruct>().Result;
                        if (metadata?.Count > 0)
                        {
                            span.SetHeaderTags(new MetadataHeadersCollection(metadata), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                        }
                    }
                }

                scope?.Dispose();
            }

            return CallTargetState.GetDefault();
        }
    }
}
