// <copyright file="OldMessageMergeFromIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

/// <summary>
/// System.Void Google.Protobuf.IMessage::MergeFrom(Google.Protobuf.CodedInputStream) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Google.Protobuf",
    TypeName = "Google.Protobuf.IMessage",
    MethodName = "MergeFrom",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Google.Protobuf.CodedInputStream"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.12.*",
    IntegrationName = nameof(Configuration.IntegrationId.Protobuf),
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OldMessageMergeFromIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TOutput>(TTarget instance, ref TOutput? output)
    {
        // versions < 3.13 have recursive calls of this method for sub-messages,
        // so we have to do the instrumentation on method begin to make sure we capture the top-most message
        if (!Helper.DisableInstrumentationIfInternalProtobufType<TTarget>())
        {
            if (instance.TryDuckCast<IMessageProxy>(out var message))
            {
                SchemaExtractor.EnrichActiveSpanWith(message.Descriptor, "deserialization");
            }
        }

        return CallTargetState.GetDefault();
    }
}
