// <copyright file="DatumWriterIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Avro;

/// <summary>
/// System.Void Avro.Generic.PreresolvingDatumWriter`1::Write(T,Avro.IO.Encoder) calltarget instrumentation
///
/// PreresolvingDatumWriter inherits from DatumWriter (an interface), which would be better to instrument,
/// but it seems we currently don't have the capability to instrument templated interfaces,
/// so this abstract class is a good fallback that covers most use cases
/// </summary>
[InstrumentMethod(
    AssemblyName = "Avro",
    TypeName = "Avro.Generic.PreresolvingDatumWriter`1",
    MethodName = "Write",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["!0", "Avro.IO.Encoder"],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DatumWriterIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TDatum, TEncoder>(TTarget instance, ref TDatum? datum, ref TEncoder? encoder)
        // where TTarget : IPreresolvingDatumWriterProxy
    {
        if (instance.TryDuckCast<IPreresolvingDatumWriterProxy>(out var instanceP))
        {
            SchemaExtractor.EnrichActiveSpanWith(instanceP.Schema, "serialization");
        }

        return CallTargetState.GetDefault();
    }
}
