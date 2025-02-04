// <copyright file="InstallCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Install a new version of the .NET Tracer. Could be the first version, or simply a new version
/// </summary>
internal class InstallCommand : Command
{
    private readonly Option<string> _symlinkPathOption = new("--symlink-path", () => null!) { IsRequired = true };
    private readonly Option<string> _versionedPathOption = new("--versioned-path", () => null!) { IsRequired = true };

    public InstallCommand()
        : this("install")
    {
    }

    protected InstallCommand(string command)
        : base(command)
    {
        AddOption(_symlinkPathOption);
        AddOption(_versionedPathOption);

        AddValidator(Validate);

        this.SetHandler(ExecuteAsync);
    }

    /// <inheritdoc cref="Command"/>
    public Task ExecuteAsync(InvocationContext context)
    {
        var symlinkPath = context.ParseResult.GetValueForOption(_symlinkPathOption)!;
        var versionedPath = context.ParseResult.GetValueForOption(_versionedPathOption)!;
        var log = Log.Instance;

        var result = ExecuteAsync(
            log,
            new SymlinkedTracerValues(symlinkPath),
            new VersionedTracerValues(versionedPath),
            Defaults.TracerLogDirectory,
            Defaults.CrashTrackingRegistryKey);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
    internal static ReturnCodes ExecuteAsync(
        ILogger log,
        SymlinkedTracerValues symlinkTracerValues,
        VersionedTracerValues versionedTracerValues,
        string tracerLogDirectory,
        string registryKeyName)
    {
        log.WriteInfo("Installing .NET tracer");

        // Check prerequisites
        if (!FileHelper.VerifyFiles(log, symlinkTracerValues, versionedTracerValues))
        {
            return ReturnCodes.ErrorDuringPrerequisiteVerification;
        }

        if (!FileHelper.CreateLogDirectory(log, tracerLogDirectory))
        {
            // This probably isn't a reason to bail out
        }

        if (!GacInstaller.TryGacInstall(log, versionedTracerValues))
        {
            // definitely bail out
            return ReturnCodes.ErrorDuringGacInstallation;
        }

        if (!AppHostHelper.SetAllEnvironmentVariables(log, symlinkTracerValues))
        {
            // hard to be sure exactly of the state at this point
            return ReturnCodes.ErrorSettingAppPoolVariables;
        }

        if (!RegistryHelper.AddCrashTrackingKey(log, versionedTracerValues, registryKeyName))
        {
            // Probably Don't need to bail out of installation just because we failed to add crash tracking?
        }

        // success
        return ReturnCodes.Success;
    }

    private void Validate(CommandResult commandResult)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            commandResult.ErrorMessage = $"This installer is only intended to run on Windows, it cannot be used on {RuntimeInformation.OSDescription}";
        }

        var path = commandResult.GetValueForOption(_symlinkPathOption);

        // TODO: do the file exists etc validation here?
    }
}
