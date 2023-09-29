// <copyright file="CheckIisCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;
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

        if (pid == default)
        {
            // The WorkerProcess part of ServerManager doesn't seem to be compatible with IISExpress
            if (string.IsNullOrEmpty(applicationHostConfigurationPath))
            {
                pid = application.GetWorkerProcess();
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

            var process = ProcessInfo.GetProcessInfo(pid, rootDirectory, appSettings);

            if (process == null)
            {
                Utils.WriteError(GetProcessError);
                return 1;
            }

            if (process.DotnetRuntime.HasFlag(ProcessInfo.Runtime.NetCore) && !string.IsNullOrEmpty(application.GetManagedRuntimeVersion()))
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

    private void Validate(CommandResult commandResult)
    {
        var path = commandResult.GetValueForOption(_configurationPathOption);

        if (path != null && !File.Exists(path))
        {
            commandResult.ErrorMessage = $"Cannot find the file {path}";
        }
    }
}
