// <copyright file="DatumReaderGeneric1ReadIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Avro;

/// <summary>
/// T Avro.Generic.DatumReader`1::Read(T,Avro.IO.Decoder) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Avro",
    TypeName = "Avro.Generic.DatumReader`1",
    MethodName = "Read",
    ReturnTypeName = "!0",
    ParameterTypeNames = ["!0", "Avro.IO.Decoder"],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro),
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DatumReaderGeneric1ReadIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TReuse, TDecoder>(TTarget instance, ref TReuse? reuse, ref TDecoder? decoder)
        where TTarget : IDatumReaderGeneric1Proxy
    {
        SchemaExtractor.EnrichActiveSpanWith(instance.ReaderSchema, "deserialization");

        return CallTargetState.GetDefault();
    }
}
