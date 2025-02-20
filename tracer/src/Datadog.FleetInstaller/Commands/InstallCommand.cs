// <copyright file="InstallCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Install a new version of the .NET Tracer. Could be the first version, or simply a new version
/// </summary>
internal class InstallCommand : CommandBase
{
    private readonly Option<string> _versionedPathOption = new("--home-path", () => null!)
    {
        Description = "Path to the tracer-home-directory",
        IsRequired = true,
    };

    public InstallCommand()
        : this("install")
    {
    }

    protected InstallCommand(string command)
        : base(command)
    {
        AddOption(_versionedPathOption);
        AddValidator(Validate);
        this.SetHandler(ExecuteAsync);
    }

    public Task ExecuteAsync(InvocationContext context)
    {
        var versionedPath = context.ParseResult.GetValueForOption(_versionedPathOption)!;
        var tracerValues = new TracerValues(versionedPath);
        var log = Log.Instance;

        var result = Execute(log, tracerValues, Defaults.TracerLogDirectory, Defaults.CrashTrackingRegistryKey);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
    internal static ReturnCode Execute(
        ILogger log,
        TracerValues tracerValues,
        string tracerLogDirectory,
        string registryKeyName)
    {
        log.WriteInfo("Installing .NET tracer");

        if (!FileHelper.CreateLogDirectory(log, tracerLogDirectory))
        {
            // This probably isn't a reason to bail out
        }

        if (!GacInstaller.TryGacInstall(log, tracerValues))
        {
            // definitely bail out
            return ReturnCode.ErrorDuringGacInstallation;
        }

        if (!AppHostHelper.SetAllEnvironmentVariables(log, tracerValues))
        {
            // hard to be sure exactly of the state at this point
            return ReturnCode.ErrorSettingAppPoolVariables;
        }

        if (!RegistryHelper.AddCrashTrackingKey(log, tracerValues, registryKeyName))
        {
            // Don't need to bail out of installation just because we failed to add crash tracking
            // The tracer itself can manage this at runtime if required
        }

        return ReturnCode.Success;
    }

    private void Validate(CommandResult commandResult)
    {
        if (!IsValidEnvironment(commandResult))
        {
            return;
        }

        if (!HasValidIIsVersion(Log.Instance, out var errorMessage))
        {
            commandResult.ErrorMessage = errorMessage;
            return;
        }

        var path = commandResult.GetValueForOption(_versionedPathOption);
        if (path is not null && !FileHelper.TryVerifyFilesExist(Log.Instance, new TracerValues(path), out var err))
        {
            commandResult.ErrorMessage = err;
        }
    }
}
