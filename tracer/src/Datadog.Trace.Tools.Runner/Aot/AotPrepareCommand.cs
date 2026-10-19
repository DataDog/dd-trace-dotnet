// <copyright file="AotPrepareCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Datadog.Trace.Tools.Runner.Aot.Native;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Writes the analyses of a Datadog.Trace build that don't depend on the application next to it (see DatadogTraceAnalysis):
/// Datadog.Trace.Aot packs them, so the instrumentation of each application doesn't compute them again.
/// </summary>
internal class AotPrepareCommand : CommandWithExamples
{
    private readonly Option<string> _datadogTraceOption = new("--datadog-trace", "Path to the Datadog.Trace.dll to analyze.") { IsRequired = true };
    private readonly Option<string?> _outputOption = new("--output", "Path of the analysis (default: Datadog.Trace.aot.json next to Datadog.Trace.dll).");

    public AotPrepareCommand()
        : base("prepare", "Analyze a Datadog.Trace build once for the NativeAOT instrumentation of applications")
    {
        AddExample("dd-trace aot prepare --datadog-trace Datadog.Trace.dll");

        AddOption(_datadogTraceOption);
        AddOption(_outputOption);

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
#if NET6_0_OR_GREATER
        var datadogTracePath = Path.GetFullPath(_datadogTraceOption.GetValue(context));
        var output = _outputOption.GetValue(context) ?? Path.Combine(Path.GetDirectoryName(datadogTracePath)!, DatadogTraceAnalysis.FileName);
        using var datadogTrace = dnlib.DotNet.ModuleDefMD.Load(datadogTracePath);
        var analysis = DatadogTraceAnalysis.Create(datadogTrace, datadogTracePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        analysis.Write(output);
        AotLog.Info($"Analysis of {datadogTracePath} ({analysis.JsonModels} JSON models, {analysis.DelegateWrappers} delegate wrappers, {analysis.ReflectionTypeNames.Count} type names, {analysis.DuckTypeMappings.Count} duck typing mappings) -> {output}");
        context.ExitCode = 0;
#else
        AotLog.Error("The analysis of Datadog.Trace requires the .NET 6+ runner.");
        context.ExitCode = 1;
#endif
    }
}
