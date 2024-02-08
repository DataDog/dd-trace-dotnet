// <copyright file="GacUninstallCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.Versioning;
using Datadog.Trace.Tools.Runner.Gac;

namespace Datadog.Trace.Tools.Runner;

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal class GacUninstallCommand : CommandWithExamples
{
    private readonly ApplicationContext _applicationContext;
    private readonly Argument<string> _nameArgument = new("assembly-path") { Arity = ArgumentArity.ZeroOrOne };

    public GacUninstallCommand(ApplicationContext applicationContext)
        : base("uninstall", "Uninstall a .NET Framework assembly from the GAC")
    {
        _applicationContext = applicationContext;
        AddArgument(_nameArgument);

        AddExample("dd-trace gac uninstall assemblyName");

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        if (!AdministratorHelper.IsElevated)
        {
            Utils.WriteError("This command requires Administrator permissions.");
            context.ExitCode = 1;
            return;
        }
    }
}
