// <copyright file="CheckIisCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tools.Runner.Checks;
using Microsoft.Web.Administration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckIisCommand : AsyncCommand<CheckIisSettings>
    {
        public override Task<int> ExecuteAsync(CommandContext context, CheckIisSettings settings)
        {
            return ExecuteAsync(settings, null, null);
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

        internal static async Task<int> ExecuteAsync(CheckIisSettings settings, string applicationHostConfigurationPath, int? pid)
        {
            var values = settings.SiteName.Split('/');

            var siteName = values[0];
            var applicationName = values.Length > 1 ? $"/{values[1]}" : "/";

            AnsiConsole.WriteLine($"Fetching application {applicationName} from site {siteName}");

            var serverManager = new ServerManager(readOnly: true, applicationHostConfigurationPath);

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

            // The WorkerProcess part of ServerManager doesn't seem to be compatible with IISExpress
            // so we skip this bit when launched from the tests
            if (pid == null)
            {
                var pool = serverManager.ApplicationPools[application.ApplicationPoolName];

                var workerProcesses = pool.WorkerProcesses;

                if (workerProcesses.Count > 0)
                {
                    // If there are multiple worker processes, we just take the first one
                    // In theory, all worker processes have the same configuration
                    pid = workerProcesses[0].ProcessId;
                }
            }

            if (pid == null)
            {
                Utils.WriteWarning("No worker process found, to perform additional checks make sure the application is active");
            }
            else
            {
                AnsiConsole.WriteLine($"Inspecting worker process {pid.Value}");

                var rootDirectory = application.VirtualDirectories.FirstOrDefault(d => d.Path == "/")?.PhysicalPath;

                IConfigurationSource appSettingsConfigurationSource = null;

                try
                {
                    var config = application.GetWebConfiguration();
                    var appSettings = config.GetSection("appSettings");
                    var collection = appSettings.GetCollection();

                    appSettingsConfigurationSource = new DictionaryConfigurationSource(
                        collection.ToDictionary(c => (string)c.Attributes["key"].Value, c => (string)c.Attributes["value"].Value));
                }
                catch (Exception ex)
                {
                    Utils.WriteWarning("Could not extract configuration from site: " + ex.Message);
                }

                var process = ProcessInfo.GetProcessInfo(pid.Value, rootDirectory, appSettingsConfigurationSource);

                if (process == null)
                {
                    Utils.WriteError("Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.");
                    return 1;
                }

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
    }
}
