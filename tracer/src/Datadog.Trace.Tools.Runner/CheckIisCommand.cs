// <copyright file="CheckIisCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Runner.Checks;
using Microsoft.Web.Administration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckIisCommand : AsyncCommand<CheckIisSettings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, CheckIisSettings settings)
        {
            var values = settings.SiteName.Split('/');

            var siteName = values[0];
            var applicationName = values.Length > 1 ? $"/{values[1]}" : "/";

            AnsiConsole.WriteLine($"Fetching application {applicationName} from site {siteName}");

            var serverManager = new ServerManager();

            var site = serverManager.Sites[siteName];

            if (site == null)
            {
                Utils.WriteError($"Could not find site {siteName}");
                Utils.WriteError("Available sites:");

                foreach (var s in serverManager.Sites)
                {
                    Utils.WriteError($" - {s.Name}");
                }

                return 1;
            }

            var application = site.Applications[applicationName];

            if (application == null)
            {
                Utils.WriteError($"Could not find application {applicationName} in site {siteName}");
                Utils.WriteError("Available applications:");

                foreach (var app in site.Applications)
                {
                    Utils.WriteError($" - {app.Path}");
                }

                return 1;
            }

            var pool = serverManager.ApplicationPools[application.ApplicationPoolName];

            var workerProcesses = pool.WorkerProcesses;

            if (workerProcesses.Count == 0)
            {
                Utils.WriteWarning("No worker process found, to perform additional checks make sure the application is active");
            }
            else
            {
                AnsiConsole.WriteLine($"Inspecting worker process {workerProcesses[0].ProcessId}");

                var rootDirectory = application.VirtualDirectories.FirstOrDefault(d => d.Path == "/")?.PhysicalPath;

                var config = application.GetWebConfiguration();
                var appSettings = config.GetSection("appSettings");
                var collection = appSettings.GetCollection();

                var configurationSource = new DictionaryConfigurationSource(
                    collection.ToDictionary(c => (string)c.Attributes["key"].Value, c => (string)c.Attributes["value"].Value));

                var process = ProcessInfo.GetProcessInfo(workerProcesses[0].ProcessId, rootDirectory, configurationSource);

                if (!ProcessBasicCheck.Run(process))
                {
                    return 1;
                }

                if (!await AgentConnectivityCheck.RunAsync(process).ConfigureAwait(false))
                {
                    return 1;
                }
            }

            if (!GacCheck.Run())
            {
                return 1;
            }

            Utils.WriteSuccess("No issue found with the IIS site.");

            return 0;
        }

        public override ValidationResult Validate(CommandContext context, CheckIisSettings settings)
        {
            var result = base.Validate(context, settings);

            if (result.Successful)
            {
                // Perform additional validation
                if (settings.SiteName.Count(c => c == '/') > 1)
                {
                    return ValidationResult.Error($"IIS site names can't have multiple / in their name: {settings.SiteName}");
                }
            }

            return result;
        }
    }
}
