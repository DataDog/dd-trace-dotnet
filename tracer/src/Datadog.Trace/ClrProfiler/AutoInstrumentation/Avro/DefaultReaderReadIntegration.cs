// <copyright file="DefaultReaderReadIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Avro;

/// <summary>
/// System.Object Avro.Generic.DefaultReader::Read(System.Object,Avro.Schema,Avro.Schema,Avro.IO.Decoder) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Avro",
    TypeName = "Avro.Generic.DefaultReader",
    MethodName = "Read",
    ReturnTypeName = ClrNames.Object,
    ParameterTypeNames = [ClrNames.Object, "Avro.Schema", "Avro.Schema", "Avro.IO.Decoder"],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DefaultReaderReadIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TWriterSchema, TReaderSchema, TD>(TTarget instance, ref object? reuse, TWriterSchema writerSchema, TReaderSchema readerSchema, ref TD? d)
        where TWriterSchema : ISchemaProxy
        where TReaderSchema : ISchemaProxy
    {
        SchemaExtractor.EnrichActiveSpanWith(readerSchema, "deserialization");

        return CallTargetState.GetDefault();
    }
}
