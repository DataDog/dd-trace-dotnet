// <copyright file="CallTargetAotCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.Runner.CallTargetAot;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Represents the public command family used to generate and apply NativeAOT CallTarget artifacts.
/// </summary>
internal class CallTargetAotCommand : CommandWithExamples
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotCommand"/> class.
    /// </summary>
    public CallTargetAotCommand()
        : base("calltarget-aot", "Generate and apply NativeAOT CallTarget artifacts")
    {
        AddExample("dd-trace calltarget-aot generate --tracer-assembly Datadog.Trace.dll --target-folder ./bin --target-filter MyApp.dll --output Datadog.Trace.CallTarget.AotRegistry.dll");
        AddExample("dd-trace calltarget-aot rewrite --manifest Datadog.Trace.CallTarget.AotRegistry.dll.manifest.json --application-assembly ./obj/Release/net8.0/MyApp.dll --output-directory ./obj/Release/net8.0/datadog/calltarget-aot/rewritten");

        AddCommand(new CallTargetAotGenerateCommand());
        AddCommand(new CallTargetAotRewriteCommand { IsHidden = true });
    }
}
