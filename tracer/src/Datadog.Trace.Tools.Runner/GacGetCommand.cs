// <copyright file="GacGetCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.Versioning;
using Datadog.Trace.Tools.Runner.Gac;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner;

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal class GacGetCommand : CommandWithExamples
{
    private readonly Argument<string> _assemblyNameArgument = new("assembly-name") { Arity = ArgumentArity.ExactlyOne };

    public GacGetCommand()
        : base("get", "Get information from a .NET Framework assembly from the GAC")
    {
        AddArgument(_assemblyNameArgument);

        AddExample("dd-trace gac get assemblyName");

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        var assemblyName = _assemblyNameArgument.GetValue(context);
        using var container = NativeMethods.CreateAssemblyCache();
        var asmInfo = new AssemblyInfo();
        var hr = container.AssemblyCache.QueryAssemblyInfo(QueryAssemblyInfoFlag.QUERYASMINFO_FLAG_GETSIZE, assemblyName!, ref asmInfo);
        if (hr == 0)
        {
            var asmFlags = asmInfo.AssemblyFlags switch
            {
                AssemblyInfoFlags.None => "None",
                AssemblyInfoFlags.ASSEMBLYINFO_FLAG_INSTALLED => "Installed",
                AssemblyInfoFlags.ASSEMBLYINFO_FLAG_PAYLOADRESIDENT => "Payload resident",
                _ => string.Empty
            };

            AnsiConsole.WriteLine($"Assembly Found!");
            AnsiConsole.WriteLine($"  Flag={asmFlags}");
            AnsiConsole.WriteLine($"  Path={asmInfo.CurrentAssemblyPath}");
            AnsiConsole.WriteLine($"  SizeInKb={asmInfo.AssemblySizeInKb}");
        }
        else
        {
            Utils.WriteWarning($"Error getting '{assemblyName}' from the GAC. HRESULT={hr}");
        }

        context.ExitCode = hr;
    }
}
