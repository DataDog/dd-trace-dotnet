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

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer.IAST;

/// <summary>
/// System.String Google.Protobuf.ParsingPrimitives::ReadRawString calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Google.Protobuf",
    TypeName = "Google.Protobuf.ParsingPrimitives",
    MethodName = "ReadRawString",
    ReturnTypeName = ClrNames.String,
    ParameterTypeNames = ["System.Object&", "Google.Protobuf.ParserInternalState&", ClrNames.Int32],
    MinimumVersion = "3.18.1",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(Grpc))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ParsingPrimitivesReadRawStringIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TBuffer, TState>(ref TBuffer buffer, ref TState state, ref int length)
    {
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<string?> OnMethodEnd<TTarget>(string? returnValue, Exception? exception, in CallTargetState state)
    {
        return new CallTargetReturn<string?>(returnValue);
    }
}
