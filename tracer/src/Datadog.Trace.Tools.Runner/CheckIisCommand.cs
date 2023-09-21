// <copyright file="CheckIisCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tools.Runner.Checks;
using Microsoft.Web.Administration;
using Spectre.Console;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckIisCommand : Command
    {
        private readonly Argument<string> _siteNameArgument = new("siteName") { Arity = ArgumentArity.ZeroOrOne };

        public CheckIisCommand()
            : base("iis")
        {
            AddArgument(_siteNameArgument);

            this.SetHandler(ExecuteAsync);
        }

        public async Task ExecuteAsync(InvocationContext context)
        {
            var siteName = _siteNameArgument.GetValue(context);

            var result = await ExecuteAsync(siteName, null, null).ConfigureAwait(false);

            context.ExitCode = result;
        }

        internal static async Task<int> ExecuteAsync(string siteAndApplicationName, string applicationHostConfigurationPath, int? pid, IRegistryService registryService = null)
        {
            static IEnumerable<string> GetAllApplicationNames(ServerManager sm)
            {
                return from s in sm.Sites
                       from a in s.Applications
                       select $"{s.Name}{a.Path}";
            }

            var serverManager = new ServerManager(readOnly: true, applicationHostConfigurationPath);

            if (siteAndApplicationName == null)
            {
                AnsiConsole.WriteLine(IisApplicationNotProvided());

                var allApplicationNames = GetAllApplicationNames(serverManager);
                AnsiConsole.WriteLine(ListAllIisApplications(allApplicationNames));

                return 1;
            }

            var values = siteAndApplicationName.Split('/', 2);

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
                    CheckAppPoolsEnvVars(pool, serverManager);
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

        internal static void CheckAppPoolsEnvVars(ApplicationPool pool, ServerManager serverManager)
        {
            var relevantProfilerConfiguration = new Dictionary<string, string>
            {
                { "COR_ENABLE_PROFILING", "1" },
                { "CORECLR_ENABLE_PROFILING", "1" },
                { "COR_PROFILER", Utils.Profilerid },
                { "CORECLR_PROFILER", Utils.Profilerid }
            };

            var relevantProfilerPathConfiguration = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "COR_PROFILER_PATH",
                "COR_PROFILER_PATH_32",
                "COR_PROFILER_PATH_64",
                "CORECLR_PROFILER_PATH",
                "CORECLR_PROFILER_PATH_32",
                "CORECLR_PROFILER_PATH_64"
            };

            if (pool != null)
            {
                CheckEnvironmentVariables(pool.GetCollection("environmentVariables"), relevantProfilerConfiguration, relevantProfilerPathConfiguration, pool.Name);
            }

            if (serverManager != null)
            {
                var defaultEnvironmentVariables = serverManager.ApplicationPools?.GetChildElement("applicationPoolDefaults").GetCollection("environmentVariables");
                if (defaultEnvironmentVariables != null)
                {
                    CheckEnvironmentVariables(defaultEnvironmentVariables, relevantProfilerConfiguration, relevantProfilerPathConfiguration, "applicationPoolDefaults");
                }
            }
        }

        private static void CheckEnvironmentVariables(ConfigurationElementCollection environmentVariablesCollection, Dictionary<string, string> relevantProfilerConfiguration, HashSet<string> relevantProfilerPathConfiguration, string poolName)
        {
            var foundVariables = new Dictionary<string, string>();
            var foundPathVariables = new Dictionary<string, string>();

            foreach (var variable in environmentVariablesCollection)
            {
                var name = (string)variable.Attributes["name"].Value;
                var value = (string)variable.Attributes["value"].Value;

                if (relevantProfilerConfiguration.ContainsKey(name) && value != relevantProfilerConfiguration[name])
                {
                    foundVariables[name] = value;
                }
                else if (relevantProfilerPathConfiguration.Contains(name) && !ProcessBasicCheck.IsExpectedProfilerFile(value))
                {
                    foundPathVariables[name] = value;
                }
            }

            if (foundVariables.Count != 0 || foundPathVariables.Count != 0)
            {
                Utils.WriteWarning(AppPoolCheckFindings(poolName));

                foreach (var variable in foundVariables)
                {
                    Utils.WriteError(WrongEnvironmentVariableFormat(variable.Key, relevantProfilerConfiguration[variable.Key], variable.Value.ToString()));
                }

                foreach (var profilerPath in foundPathVariables)
                {
                    Utils.WriteError(WrongProfilerEnvironment(profilerPath.Key, profilerPath.Value));
                }
            }
        }
    }
}
