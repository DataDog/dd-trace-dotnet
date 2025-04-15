// <copyright file="DefaultWriterWriteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

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
    MinimumVersion = "1.12.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DefaultWriterWriteIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSchema, TEncoder>(TTarget instance, ref TSchema? schema, ref object? value, ref TEncoder? encoder)
        where TSchema : ISchemaProxy
    {
        SchemaExtractor.EnrichActiveSpanWith(schema, "deserialization");

        return CallTargetState.GetDefault();
    }
}
