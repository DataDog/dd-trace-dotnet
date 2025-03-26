// <copyright file="BufferMessageInternalMergeFromIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

/// <summary>
/// System.Void Google.Protobuf.IBufferMessage::InternalMergeFrom(Google.Protobuf.ParseContext) calltarget instrumentation
/// </summary>
// FIXME: Commenting out due to throwing errors.
// [InstrumentMethod(
//    AssemblyName = "Google.Protobuf",
//    TypeName = "Google.Protobuf.IBufferMessage",
//    MethodName = "InternalMergeFrom",
//    ParameterTypeNames = ["Google.Protobuf.ParseContext&"],
//    ReturnTypeName = ClrNames.Void,
//    MinimumVersion = "3.15.0",
//    MaximumVersion = "3.*.*",
//    IntegrationName = nameof(Configuration.IntegrationId.Protobuf),
//    CallTargetIntegrationKind = CallTargetKind.Interface)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class BufferMessageInternalMergeFromIntegration
{
    // For performance reasons, we want to do the actual instrumentation work with a Duck constraint,
    // but to be able to disable the instrumentation we need the raw type
    // so we use 2 different methods to have access to both when we need it.
    // Note: Disabling OnMethodBegin means the OnMethodEnd will not be called afterward.

    internal static CallTargetState OnMethodBegin<TTarget, TOutput>(TTarget instance, ref TOutput? output)
    {
        Helper.DisableInstrumentationIfInternalProtobufType<TTarget>();
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        where TTarget : IMessageProxy
    {
        if (instance.Instance != null)
        {
            SchemaExtractor.EnrichActiveSpanWith(instance.Descriptor, "deserialization");
        }

        return CallTargetReturn.GetDefault();
    }
}
