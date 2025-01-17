// <copyright file="BufferMessageInternalMergeFromIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

/// <summary>
/// System.Void Google.Protobuf.IBufferMessage::InternalMergeFrom(Google.Protobuf.ParseContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Google.Protobuf",
    TypeName = "Google.Protobuf.IBufferMessage",
    MethodName = "InternalMergeFrom",
    ParameterTypeNames = ["Google.Protobuf.ParseContext&"],
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "3.15.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(Configuration.IntegrationId.Protobuf),
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class BufferMessageInternalMergeFromIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TOutput>(TTarget instance, ref TOutput? output)
        where TTarget : IMessageProxy
    {
        if (Helper.TryGetDescriptor(instance, out var descriptor))
        {
            SchemaExtractor.EnrichActiveSpanWith(descriptor, "deserialization");
        }

        return CallTargetState.GetDefault();
    }
}
