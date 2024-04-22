// <copyright file="ParsingPrimitivesReadRawStringIntegration.cs" company="Datadog">
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
using Datadog.Trace.Iast;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer.IAST;

/// <summary>
/// System.String Google.Protobuf.ParsingPrimitives::ReadRawString calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Google.Protobuf",
    TypeName = "Google.Protobuf.ParsingPrimitives",
    MethodName = "ReadRawString",
    ReturnTypeName = ClrNames.String,
    ParameterTypeNames = ["System.ReadOnlySpan`1[System.Byte]&", "Google.Protobuf.ParserInternalState&", ClrNames.Int32],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(Grpc),
    InstrumentationCategory = InstrumentationCategory.Iast)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ParsingPrimitivesReadRawStringIntegration
{
    internal static CallTargetReturn<string?> OnMethodEnd<TTarget>(string? returnValue, Exception? exception, in CallTargetState state)
    {
        if (returnValue is not null)
        {
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            taintedObjects?.TaintInputString(returnValue, new Source(SourceType.GrpcRequestBody, null, returnValue));
        }

        return new CallTargetReturn<string?>(returnValue);
    }
}
