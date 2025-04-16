// <copyright file="DatumWriterGeneric1WriteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Avro;

/// <summary>
/// System.Void Avro.Generic.DatumWriter`1::Write(T,Avro.IO.Encoder) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Avro",
    TypeName = "Avro.Generic.DatumWriter`1",
    MethodName = "Write",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["T", "Avro.IO.Encoder"],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro),
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DatumWriterGeneric1WriteIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TDatum, TEncoder>(TTarget instance, ref TDatum? datum, ref TEncoder? encoder)
        where TTarget : IDatumWriterGeneric1Proxy
    {
        SchemaExtractor.EnrichActiveSpanWith(instance.Schema, "deserialization");

        return CallTargetState.GetDefault();
    }
}
