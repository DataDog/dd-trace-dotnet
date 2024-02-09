// <copyright file="GacInstallCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.Versioning;
using Datadog.Trace.Tools.Runner.Gac;

namespace Datadog.Trace.Tools.Runner;

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal class GacInstallCommand : CommandWithExamples
{
    private readonly Argument<string> _assemblyPathArgument = new("assembly-path") { Arity = ArgumentArity.ExactlyOne };

    public GacInstallCommand()
        : base("install", "Install a .NET Framework assembly to the GAC")
    {
        AddArgument(_assemblyPathArgument);

        AddExample("dd-trace gac install c:\\assemblies\\assemblyName.dll");

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

        var assemblyPath = _assemblyPathArgument.GetValue(context);
        if (!File.Exists(assemblyPath))
        {
            Utils.WriteError($"File '{assemblyPath}' does not exist.");
            context.ExitCode = 1;
            return;
        }

        using var container = NativeMethods.CreateAssemblyCache();
        var hr = container.AssemblyCache.InstallAssembly(AssemblyCacheInstallFlags.None, assemblyPath, IntPtr.Zero);
        if (hr == 0)
        {
            Utils.WriteSuccess($"Assembly '{assemblyPath}' was installed in the GAC successfully.");
        }
        else
        {
            Utils.WriteError($"Error installing '{assemblyPath}' in the GAC. HRESULT={hr}");
        }

        context.ExitCode = hr;
    }
}
