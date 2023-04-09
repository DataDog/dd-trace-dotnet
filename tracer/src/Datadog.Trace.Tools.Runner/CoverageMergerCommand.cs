// <copyright file="CoverageMergerCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner;

internal class CoverageMergerCommand : Command<CoverageMergerSettings>
{
    public CoverageMergerCommand(ApplicationContext applicationContext)
    {
        ApplicationContext = applicationContext;
    }

    protected ApplicationContext ApplicationContext { get; }

    public override int Execute(CommandContext context, CoverageMergerSettings settings)
    {
        return CoverageUtils.TryCombineAndGetTotalCoverage(settings.InputFolder, settings.OutputFile, true) ? 0 : 1;
    }
}
