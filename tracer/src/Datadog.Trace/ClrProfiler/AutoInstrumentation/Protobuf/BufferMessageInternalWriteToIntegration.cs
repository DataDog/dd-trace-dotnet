// <copyright file="BufferMessageInternalWriteToIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

/// <summary>
/// System.Void Google.Protobuf.IBufferMessage::InternalWriteTo(Google.Protobuf.WriteContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Google.Protobuf",
    TypeName = "Google.Protobuf.IBufferMessage",
    MethodName = "InternalWriteTo",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Google.Protobuf.WriteContext&"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = "Protobuf")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class BufferMessageInternalWriteToIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCtx>(TTarget instance, ref TCtx ctx)
        where TTarget : IMessageProxy
    {
        SchemaExtractor.EnrichActiveSpanWith(instance.Descriptor, "deserialization");
        return CallTargetState.GetDefault();
    }
}
