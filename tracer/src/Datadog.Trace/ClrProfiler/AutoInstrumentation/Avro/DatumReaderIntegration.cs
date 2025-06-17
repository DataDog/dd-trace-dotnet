// <copyright file="DatumReaderIntegration.cs" company="Datadog">
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
/// T Avro.Generic.PreresolvingDatumReader`1::Read(T,Avro.IO.Decoder) calltarget instrumentation
///
/// PreresolvingDatumReader inherits from DatumReader (an interface), which would be better to instrument,
/// but it seems we currently don't have the capability to instrument templated interfaces,
/// so this abstract class is a good fallback that covers most use cases
/// </summary>
[InstrumentMethod(
    AssemblyName = "Avro",
    TypeName = "Avro.Generic.PreresolvingDatumReader`1",
    MethodName = "Read",
    ReturnTypeName = "!0",
    ParameterTypeNames = ["!0", "Avro.IO.Decoder"],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Avro))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DatumReaderIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TReuse, TDecoder>(TTarget instance, ref TReuse? reuse, ref TDecoder? decoder)
        // cannot use type constraints because of limitation on instrumenting methods on generic type: https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/development/AutomaticInstrumentation.md#current-limitations
    {
        if (instance.TryDuckCast<PreresolvingDatumReaderProxy>(out var instanceProxy))
        {
            SchemaExtractor.EnrichActiveSpanWith(instanceProxy.ReaderSchema, "deserialization");
        }

        return CallTargetState.GetDefault();
    }
}
