// <copyright file="AsyncCallServerSendInitialMetadataAsyncInstrumentation.cs" company="Datadog">
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
        MethodName = "SendInitialMetadataAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Grpc.Core.Metadata" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncCallServerSendInitialMetadataAsyncInstrumentation
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMetadata>(TTarget instance, in TMetadata? headers)
        {
            var tracer = Tracer.Instance;
            if (tracer.ActiveScope is Scope { Span: { Tags: GrpcServerTags } span } && headers is not null)
            {
                var metadata = headers.DuckCast<IMetadata>();
                if (metadata.Count > 0)
                {
                    span.SetHeaderTags(new MetadataHeadersCollection(metadata), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
