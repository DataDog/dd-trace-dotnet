// <copyright file="CheckProcessCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.Shared;
using Spectre.Console;
using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class CheckProcessCommand : Command
{
    private readonly Argument<int> _pidArgument = new("pid");

    public CheckProcessCommand()
        : base("process")
    {
        AddArgument(_pidArgument);

        this.SetHandler(ExecuteAsync);
    }

    private async Task ExecuteAsync(InvocationContext context)
    {
        var pid = _pidArgument.GetValue(context);

        AnsiConsole.WriteLine("Running checks on process " + pid);

        ProcessInfo process;

        try
        {
            process = ProcessInfo.GetProcessInfo(pid);
        }
        catch (Exception ex)
        {
            Utils.WriteError("Error while trying to fetch process information: " + ex.Message);
            Utils.WriteError("Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.");
            context.ExitCode = 1;
            return;
        }

        AnsiConsole.WriteLine("Process name: " + process.Name);

        var mainModule = process.MainModule != null ? Path.GetFileName(process.MainModule) : null;

        if (mainModule == "w3wp.exe" || mainModule == "iisexpress.exe")
        {
            if (process.EnvironmentVariables.ContainsKey("APP_POOL_ID"))
            {
                Utils.WriteWarning(IisProcess);
            }
        }

        var foundIssue = !ProcessBasicCheck.Run(process);

        if (foundIssue)
        {
            context.ExitCode = 1;
            return;
        }

        var configurationSource = process.ExtractConfigurationSource(null, null);
        foundIssue = !await AgentConnectivityCheck.RunAsync(configurationSource).ConfigureAwait(false);

        if (foundIssue)
        {
            context.ExitCode = 1;
            return;
        }

        Utils.WriteSuccess("No issue found with the target process.");
    }
}
