// <copyright file="CrankCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Datadog.Trace.Tools.Runner.Crank;

internal class CrankCommand : CommandWithExamples
{
    private readonly Argument<string> _inputFileArgument = new("input-file");

    public CrankCommand()
        : base("crank-import", "Import a Microsoft Crank json file")
    {
        AddArgument(_inputFileArgument);
        AddExample("dd-trace ci crank-import ./crank-results.json");
        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        var inputFile = _inputFileArgument.GetValue(context);

        context.ExitCode = Importer.Process(inputFile);
    }
}
