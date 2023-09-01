// <copyright file="CoverageMergerCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Datadog.Trace.Tools.Runner;

internal class CoverageMergerCommand : CommandWithExamples
{
    private readonly Argument<string> _inputFolderArgument = new("input-folder", "Sets the folder path where the code coverage json files are located.");
    private readonly Argument<string> _outputFolderArgument = new("output-file", "Sets the output json file.");

    public CoverageMergerCommand()
        : base("coverage-merge", "Merges all coverage json files into a single one.")
    {
        AddArgument(_inputFolderArgument);
        AddArgument(_outputFolderArgument);

        AddExample(@"dd-trace coverage-merge c:\coverage_folder\ total-coverage.json");

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        var inputFolder = _inputFolderArgument.GetValue(context);
        var outputFile = _outputFolderArgument.GetValue(context);

        context.ExitCode = CoverageUtils.TryCombineAndGetTotalCoverage(inputFolder, outputFile, true) ? 0 : 1;
    }
}
