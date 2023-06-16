// <copyright file="AsyncCallHandleUnaryResponseInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
        MethodName = "HandleUnaryResponse",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Bool, "Grpc.Core.Internal.ClientSideStatus", "Grpc.Core.Internal.IBufferReader", "Grpc.Core.Metadata" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncCallHandleUnaryResponseInstrumentation
    {
        internal static CallTargetState OnMethodBegin<TTarget, TStatus, TBufferReader, TMetadata>(
            TTarget instance,
            bool success,
            in TStatus clientSideStatus,
            in TBufferReader receivedMessageReader,
            in TMetadata responseHeaders)
        {
            // receivedStatus can change in the middle of this method, so we need to wait for the method to finish,
            // to grab the final status.
            var tracer = Tracer.Instance;
            if (GrpcCoreApiVersionHelper.IsSupported && tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc))
            {
                // using CreateFrom to avoid boxing ClientSideStatus struct
                var receivedStatus = DuckType.CreateCache<ClientSideStatusWithMetadataStruct>.CreateFrom(clientSideStatus);
                var status = receivedStatus.Status;
                var asyncCall = instance.DuckCast<AsyncCallStruct>();
                var scope = GrpcLegacyClientCommon.CreateClientSpan(tracer, in asyncCall.Details, in status);
                if (scope?.Span is { } span)
                {
                    if (receivedStatus.Trailers is { Count: > 0 })
                    {
                        span.SetHeaderTags(new MetadataHeadersCollection(receivedStatus.Trailers), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                    }
                    else if (responseHeaders is not null)
                    {
                        var responseMetadata = responseHeaders.DuckCast<IMetadata>();
                        if (responseMetadata.Count > 0)
                        {
                            span.SetHeaderTags(new MetadataHeadersCollection(responseMetadata), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                        }
                    }
                }

                // need to pass the finish time so that you don't get traces
                // where the child span closes after the parent
                return new CallTargetState(scope, state: null, startTime: DateTimeOffset.UtcNow);
            }

            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        {
            if (state.Scope is { Span.Tags: GrpcClientTags tags } scope)
            {
                // The status code will only change during this method if the response
                // was originally "success" (due to serialization errors)
                if (exception is not null || tags.StatusCode == "0")
                {
                    // can't use constraints as in a generic type
                    var asyncCall = instance.DuckCast<AsyncCallStruct>();

                    var status = asyncCall.FinishedStatus.Value.Status;
                    GrpcCommon.RecordFinalStatus(scope.Span, status.StatusCode, status.Detail, status.DebugException ?? exception);
                }

                // Explicitly close so we use the time at the start of the method
                // The caller of the original Grpc method will already have transferred control back
                // as this is running on a separate thread, so this should give better traces
                var finishTime = state.StartTime ?? DateTimeOffset.UtcNow;
                scope.Span.Finish(finishTime);
                scope.Dispose();
            }

            return new CallTargetReturn();
        }
    }
}
