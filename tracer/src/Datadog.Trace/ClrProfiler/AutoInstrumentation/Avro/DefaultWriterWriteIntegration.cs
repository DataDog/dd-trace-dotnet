// <copyright file="DefaultWriterWriteIntegration.cs" company="Datadog">
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
/// System.Void Avro.Generic.DefaultWriter::Write(Avro.Schema,System.Object,Avro.IO.Encoder) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Avro",
    TypeName = "Avro.Generic.DefaultWriter",
    MethodName = "Write",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Avro.Schema", ClrNames.Object, "Avro.IO.Encoder"],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DefaultWriterWriteIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSchema, TEncoder>(TTarget instance, ref TSchema? schema, ref object? value, ref TEncoder? encoder)
    {
        if (schema.TryDuckCast<ISchemaProxy>(out var schemaProxy))
        {
            SchemaExtractor.EnrichActiveSpanWith(schemaProxy, "serialization");
        }

        return CallTargetState.GetDefault();
    }
}
