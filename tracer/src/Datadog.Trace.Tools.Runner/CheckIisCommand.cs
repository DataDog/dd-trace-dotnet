// <copyright file="CheckIisCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tools.Runner.Checks;
using Microsoft.Web.Administration;
using Spectre.Console;
using Spectre.Console.Cli;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

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
                if (settings.SiteName?.Count(c => c == '/') > 1)
                {
                    return ValidationResult.Error($"IIS site names can't have multiple / in their name: {settings.SiteName}");
                }
            }

            return result;
        }

        internal static async Task<int> ExecuteAsync(CheckIisSettings settings, string applicationHostConfigurationPath, int? pid, IRegistryService registryService = null)
        {
            static IEnumerable<string> GetAllApplicationNames(ServerManager sm)
            {
                return from s in sm.Sites
                       from a in s.Applications
                       select $"{s.Name}{a.Path}";
            }

            var serverManager = new ServerManager(readOnly: true, applicationHostConfigurationPath);

            if (settings.SiteName == null)
            {
                AnsiConsole.WriteLine(IisApplicationNotProvided());

                var allApplicationNames = GetAllApplicationNames(serverManager);
                AnsiConsole.WriteLine(ListAllIisApplications(allApplicationNames));

                return 1;
            }

            var values = settings.SiteName.Split('/');

            var siteName = values[0];
            var applicationName = values.Length > 1 ? $"/{values[1]}" : "/";

            AnsiConsole.WriteLine(FetchingApplication(siteName, applicationName));

            var site = serverManager.Sites[siteName];
            var application = site?.Applications[applicationName];

            if (site == null || application == null)
            {
                Utils.WriteError(CouldNotFindIisApplication(siteName, applicationName));

                var allApplicationNames = GetAllApplicationNames(serverManager);
                Utils.WriteError(ListAllIisApplications(allApplicationNames));

                return 1;
            }

            var pool = serverManager.ApplicationPools[application.ApplicationPoolName];

            // The WorkerProcess part of ServerManager doesn't seem to be compatible with IISExpress
            // so we skip this bit when launched from the tests
            if (pid == null)
            {
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
                Utils.WriteWarning(NoWorkerProcess);
            }
            else
            {
                AnsiConsole.WriteLine(InspectingWorkerProcess(pid.Value));

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
                    Utils.WriteWarning(ErrorExtractingConfiguration(ex.Message));
                }

                var process = ProcessInfo.GetProcessInfo(pid.Value, rootDirectory, appSettingsConfigurationSource);

                if (process == null)
                {
                    Utils.WriteError(GetProcessError);
                    return 1;
                }

                if (process.DotnetRuntime.HasFlag(ProcessInfo.Runtime.NetCore) && !string.IsNullOrEmpty(pool.ManagedRuntimeVersion))
                {
                    Utils.WriteWarning(IisMixedRuntimes);
                }

                if (process.Modules.Any(m => Path.GetFileName(m).Equals("aspnetcorev2_outofprocess.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    // IIS site is hosting aspnetcore in out-of-process mode
                    // Trying to locate the actual application process
                    AnsiConsole.WriteLine(OutOfProcess);

                    var childProcesses = process.GetChildProcesses();

                    // Get either the first process that is dotnet, or the first that is not conhost
                    int? dotnetPid = null;
                    int? fallbackPid = null;

                    foreach (var childPid in childProcesses)
                    {
                        using var childProcess = Process.GetProcessById(childPid);

                        if (childProcess.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                        {
                            dotnetPid = childPid;
                            break;
                        }

                        if (!childProcess.ProcessName.Equals("conhost", StringComparison.OrdinalIgnoreCase))
                        {
                            fallbackPid = childPid;
                        }
                    }

                    var aspnetCorePid = dotnetPid ?? fallbackPid;

                    if (aspnetCorePid == null)
                    {
                        Utils.WriteError(AspNetCoreProcessNotFound);
                        return 1;
                    }

                    AnsiConsole.WriteLine(AspNetCoreProcessFound(aspnetCorePid.Value));

                    process = ProcessInfo.GetProcessInfo(aspnetCorePid.Value);

                    if (process == null)
                    {
                        Utils.WriteError(GetProcessError);
                        return 1;
                    }
                }

                if (!ProcessBasicCheck.Run(process, registryService))
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

            Utils.WriteSuccess(IisNoIssue);

            return 0;
        }
    }
}
