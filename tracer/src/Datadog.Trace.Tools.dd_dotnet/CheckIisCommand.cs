// <copyright file="CheckIisCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;
using Datadog.Trace.Tools.Shared;
using Spectre.Console;

using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet;

[SupportedOSPlatform("windows")]
internal class CheckIisCommand : Command
{
    private readonly Argument<string> _siteNameArgument = new("siteName") { Arity = ArgumentArity.ZeroOrOne };

    private readonly Option<string?> _configurationPathOption = new("--iisConfigPath", () => null) { IsHidden = true };
    private readonly Option<int> _workerProcessOption = new("--workerProcess", () => 0) { IsHidden = true };

    public CheckIisCommand()
        : base("iis")
    {
        AddArgument(_siteNameArgument);
        AddOption(_configurationPathOption);
        AddOption(_workerProcessOption);

        AddValidator(Validate);

        this.SetHandler(ExecuteAsync);
    }

    public async Task ExecuteAsync(InvocationContext context)
    {
        var siteName = _siteNameArgument.GetValue(context);

        // Hidden options used for tests
        var configurationPath = _configurationPathOption.GetValue(context);

        var pid = _workerProcessOption.GetValue(context);

        var result = await ExecuteAsync(siteName, configurationPath, pid).ConfigureAwait(false);

        context.ExitCode = result;
    }

    internal static async Task<int> ExecuteAsync(string? siteAndApplicationName, string? applicationHostConfigurationPath, int pid, IRegistryService? registryService = null)
    {
        using var serverManager = IisManager.Create(applicationHostConfigurationPath);

        if (serverManager == null)
        {
            return 1;
        }

        if (siteAndApplicationName == null)
        {
            AnsiConsole.WriteLine(IisApplicationNotProvided());

            var allApplicationNames = serverManager.GetApplicationNames();
            AnsiConsole.WriteLine(ListAllIisApplications(allApplicationNames));

            return 1;
        }

        var values = siteAndApplicationName.Split('/', 2);

        var siteName = values[0];
        var applicationName = values.Length > 1 ? $"/{values[1]}" : "/";

        AnsiConsole.WriteLine(FetchingApplication(siteName, applicationName));

        using var application = serverManager.GetApplication(siteName, applicationName);

        if (application == null)
        {
            Utils.WriteError(CouldNotFindIisApplication(siteName, applicationName));

            var allApplicationNames = serverManager.GetApplicationNames();
            Utils.WriteError(ListAllIisApplications(allApplicationNames));

            return 1;
        }

        using var pool = application.GetApplicationPool();

        if (pid == default)
        {
            // The WorkerProcess part of ServerManager doesn't seem to be compatible with IISExpress
            if (string.IsNullOrEmpty(applicationHostConfigurationPath))
            {
                try
                {
                    pid = pool?.GetWorkerProcess() ?? 0;
                }
                catch (Win32Exception ex)
                {
                    Utils.WriteError(IisWorkerProcessError(Marshal.GetPInvokeErrorMessage(ex.NativeErrorCode)));
                    return 1;
                }
            }
            else
            {
                Utils.WriteWarning(IisExpressWorkerProcess);
            }
        }

        if (pid == default)
        {
            Utils.WriteWarning(NoWorkerProcess);
        }
        else
        {
            AnsiConsole.WriteLine(InspectingWorkerProcess(pid));

            var rootDirectory = application.GetRootDirectory();

            IReadOnlyDictionary<string, string>? appSettings = null;

            try
            {
                appSettings = application.GetAppSettings();
            }
            catch (Exception ex)
            {
                Utils.WriteWarning(ErrorExtractingConfiguration(ex.Message));
            }

            ProcessInfo process;

            try
            {
                process = ProcessInfo.GetProcessInfo(pid);
            }
            catch (Exception ex)
            {
                Utils.WriteError(GetProcessError(ex.Message));
                return 1;
            }

            if (process.DotnetRuntime.HasFlag(ProcessInfo.Runtime.NetCore) && !string.IsNullOrEmpty(pool?.GetManagedRuntimeVersion()))
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

                try
                {
                    process = ProcessInfo.GetProcessInfo(aspnetCorePid.Value);
                }
                catch (Exception ex)
                {
                    Utils.WriteError(GetProcessError(ex.Message));
                    return 1;
                }
            }
            else if (process.Modules.Any(m => Path.GetFileName(m).Equals("aspnetcorev2.dll", StringComparison.OrdinalIgnoreCase)))
            {
                // aspnetcorev2 is found but not aspnetcorev2_outofprocess
                // It could mean that the process is using in-process hosting,
                // but it could also mean that it's using out-of-process hosting and hasn't received a request yet.
                if (process.DotnetRuntime == ProcessInfo.Runtime.Unknown)
                {
                    Utils.WriteError(AspNetCoreOutOfProcessNotFound);
                    return 1;
                }
            }

            if (!ProcessBasicCheck.Run(process, registryService))
            {
                CheckAppPoolsEnvVars(pool, serverManager);
                return 1;
            }

            var configurationSource = process.ExtractConfigurationSource(rootDirectory, appSettings);

            if (!await AgentConnectivityCheck.RunAsync(configurationSource).ConfigureAwait(false))
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

    internal static void CheckAppPoolsEnvVars(ApplicationPool? pool, IisManager? serverManager)
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
            CheckEnvironmentVariables(pool.GetEnvironmentVariables(), relevantProfilerConfiguration, relevantProfilerPathConfiguration, pool.GetName());
        }

        if (serverManager != null)
        {
            var defaultEnvironmentVariables = serverManager.GetDefaultEnvironmentVariables();
            if (defaultEnvironmentVariables != null)
            {
                CheckEnvironmentVariables(defaultEnvironmentVariables, relevantProfilerConfiguration, relevantProfilerPathConfiguration, "applicationPoolDefaults");
            }
        }
    }

    private static void CheckEnvironmentVariables(IReadOnlyDictionary<string, string> environmentVariablesCollection, Dictionary<string, string> relevantProfilerConfiguration, HashSet<string> relevantProfilerPathConfiguration, string poolName)
    {
        var foundVariables = new Dictionary<string, string>();
        var foundPathVariables = new Dictionary<string, string>();

        foreach (var (name, value) in environmentVariablesCollection)
        {
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
            AnsiConsole.WriteLine(AppPoolCheckFindings(poolName));

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

    private void Validate(CommandResult commandResult)
    {
        var path = commandResult.GetValueForOption(_configurationPathOption);

        if (path != null && !File.Exists(path))
        {
            commandResult.ErrorMessage = $"Cannot find the file {path}";
        }
    }
}
