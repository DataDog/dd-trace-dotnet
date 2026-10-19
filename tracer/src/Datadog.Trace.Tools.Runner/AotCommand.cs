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
        : base("aot", "Build-time tools of the NativeAOT instrumentation")
    {
        AddExample("dd-trace aot check-declarations --datadog-trace Datadog.Trace.dll --maps ./ducktype-aot-maps");

        AddCommand(new AotPrepareCommand());
        AddCommand(new AotCheckDeclarationsCommand());
    }
}
