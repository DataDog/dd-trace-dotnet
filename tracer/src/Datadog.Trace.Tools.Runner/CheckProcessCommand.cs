// <copyright file="CheckProcessCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;
using Spectre.Console.Cli;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckProcessCommand : AsyncCommand<CheckProcessSettings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, CheckProcessSettings settings)
        {
            AnsiConsole.WriteLine("Running checks on process " + settings.Pid);

            var process = ProcessInfo.GetProcessInfo(settings.Pid);

            if (process == null)
            {
                Utils.WriteError("Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.");
                return 1;
            }

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
                return 1;
            }

            foundIssue = !await AgentConnectivityCheck.Run(process).ConfigureAwait(false);

            if (foundIssue)
            {
                return 1;
            }

            Utils.WriteSuccess("No issue found with the target process.");

            return 0;
        }
    }
}
