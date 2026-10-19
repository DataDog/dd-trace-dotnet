// <copyright file="AotCheckDeclarationsCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Checks that the duck typing mappings applications created under JIT (recorded with DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH, e.g. by
/// the integration tests) are declared, so a NativeAOT build generates them without the recording (see AotDeclarationsCheck).
/// </summary>
internal class AotCheckDeclarationsCommand : CommandWithExamples
{
    private readonly Option<string> _datadogTraceOption = new("--datadog-trace", "Path to the Datadog.Trace.dll whose declarations are checked.") { IsRequired = true };
    private readonly Option<string[]> _mapsOption = new("--maps", "Recorded map file, or directory of map files (*.json). Can be provided multiple times.") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
    private readonly Option<string[]> _excludeOption = new("--exclude", "Namespace prefix of Datadog proxies left out of the check (e.g. integrations NativeAOT applications don't use). Can be provided multiple times.") { AllowMultipleArgumentsPerToken = true };
    private readonly Option<string?> _reportOption = new("--report", "Optional JSON report path.");

    public AotCheckDeclarationsCommand()
        : base("check-declarations", "Check that recorded duck typing mappings are declared for NativeAOT builds")
    {
        AddExample("dd-trace aot check-declarations --datadog-trace Datadog.Trace.dll --maps ./build_data/ducktype-aot-maps");

        AddOption(_datadogTraceOption);
        AddOption(_mapsOption);
        AddOption(_excludeOption);
        AddOption(_reportOption);

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        context.ExitCode = AotDeclarationsCheck.Run(
            _datadogTraceOption.GetValue(context),
            (_mapsOption.GetValue(context) ?? Array.Empty<string>()).ToList(),
            (_excludeOption.GetValue(context) ?? Array.Empty<string>()).ToList(),
            _reportOption.GetValue(context));
    }
}
