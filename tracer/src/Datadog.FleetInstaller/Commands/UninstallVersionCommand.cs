// <copyright file="UninstallVersionCommand.cs" company="Datadog">
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
/// Uninstall a single version of the fleet-installed .NET tracer
/// </summary>
internal class UninstallVersionCommand : Command
{
    private readonly Option<string> _symlinkPathOption = new("--symlink-path", () => null!) { IsRequired = true };
    private readonly Option<string> _versionedPathOption = new("--versioned-path", () => null!) { IsRequired = true };

    public UninstallVersionCommand()
        : base("uninstall-version")
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
            new VersionedTracerValues(versionedPath),
            Defaults.CrashTrackingRegistryKey);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
    internal static ReturnCodes ExecuteAsync(
        ILogger log,
        VersionedTracerValues versionedTracerValues,
        string registryKeyName)
    {
        log.WriteInfo("Uninstalling .NET tracer package");

        // We check the prerequisites for GAC uninstall in there, so no additional checks first
        // TODO: We _could_ check that the symlink _doesn't_ point to the versioned file?
        if (!GacInstaller.TryGacUninstall(log, versionedTracerValues))
        {
            // definitely bail out
            return ReturnCodes.ErrorDuringGacUninstallation;
        }

        // We don't uninstall from the app host, as they should _already_ point to different values
        if (!RegistryHelper.RemoveCrashTrackingKey(log, versionedTracerValues, registryKeyName))
        {
            // Probably Don't need to bail out of installation just because we failed to remove the crash tracker?
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
