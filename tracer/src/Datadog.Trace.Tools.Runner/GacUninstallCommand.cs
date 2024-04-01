// <copyright file="GacUninstallCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
    private readonly Argument<string> _assemblyNameArgument = new("assembly-name") { Arity = ArgumentArity.ExactlyOne };

    public GacUninstallCommand()
        : base("uninstall", "Uninstall a .NET Framework assembly from the GAC")
    {
        AddArgument(_assemblyNameArgument);

        AddExample("dd-trace gac uninstall assemblyName");

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        var assemblyName = _assemblyNameArgument.GetValue(context);

        if (!AdministratorHelper.IsElevated)
        {
            Utils.WriteError("This command requires Administrator permissions.");
            context.ExitCode = 1;
            return;
        }

        using var container = NativeMethods.CreateAssemblyCache();
        var hr = container.AssemblyCache.UninstallAssembly(UninstallAssemblyFlags.None, assemblyName!, IntPtr.Zero, out var position);

        switch (position)
        {
            case UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_ALREADY_UNINSTALLED:
                Utils.WriteWarning($"Assembly '{assemblyName}' was already uninstalled from the GAC.");
                context.ExitCode = hr;
                break;
            case UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_REFERENCE_NOT_FOUND:
                Utils.WriteWarning($"Assembly '{assemblyName}' not found in the GAC.");
                context.ExitCode = hr;
                break;
            case UninstallDisposition.IASSEMBLYCACHE_UNINSTALL_DISPOSITION_STILL_IN_USE:
                Utils.WriteWarning($"Assembly '{assemblyName}' is still in use.");
                context.ExitCode = hr;
                break;
        }

        if (hr == 0)
        {
            Utils.WriteSuccess($"Assembly '{assemblyName}' was uninstalled from the GAC successfully.");
        }
        else
        {
            Utils.WriteError($"Error uninstalling '{assemblyName}' from the GAC. HRESULT={hr}");
        }

        context.ExitCode = hr;
    }
}
