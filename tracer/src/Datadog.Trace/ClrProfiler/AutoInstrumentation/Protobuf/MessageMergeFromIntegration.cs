// <copyright file="MessageMergeFromIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

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
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(Configuration.IntegrationId.Protobuf),
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class MessageMergeFromIntegration
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
