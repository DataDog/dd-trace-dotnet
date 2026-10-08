// <copyright file="AotCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.Runner.Aot;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// NativeAOT support: build-time instrumentation of the assemblies of an application.
/// </summary>
internal class AotCommand : CommandWithExamples
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AotCommand"/> class.
    /// </summary>
    public AotCommand()
        : base("aot", "Instrument assemblies at build time for NativeAOT applications")
    {
        AddExample("dd-trace aot instrument --native-tracer Datadog.Tracer.Native.so --datadog-trace Datadog.Trace.dll --assembly ./obj/MyApp.dll --reference-dir ./runtime --output ./obj/datadog-aot");

        AddCommand(new AotInstrumentCommand());
    }
}
