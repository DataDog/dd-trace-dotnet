// <copyright file="UninstallAllCommand.cs" company="Datadog">
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
/// Remove all version of the .NET tracer. Should be called for each version to be removed.
/// </summary>
internal class UninstallAllCommand : Command
{
    private readonly Option<string> _symlinkPathOption = new("--symlink-path", () => null!) { IsRequired = true };
    private readonly Option<string> _versionedPathOption = new("--versioned-path", () => null!) { IsRequired = true };

    public UninstallAllCommand()
        : base("uninstall-all")
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
        log.WriteInfo("Uninstalling .NET tracer product");

        if (!RegistryHelper.RemoveCrashTrackingKey(log, versionedTracerValues, registryKeyName))
        {
            // Not a big deal if this isn't actually removed
        }

        // Remove the tracer references
        if (!AppHostHelper.RemoveAllEnvironmentVariables(log, symlinkTracerValues))
        {
            // hard to be sure exactly of the state at this point,
            // but probably don't want to do anything else if we couldn't remove the variables,
            // as apps may fail
            return ReturnCodes.ErrorRemovingAppPoolVariables;
        }

        if (!GacInstaller.TryGacUninstall(log, versionedTracerValues))
        {
            // We don't actually care if this fails (and it probably _will_, if we haven't yet deleted the tracer files)
            // as it just leaves some files around
        }

        // Should we clean up/delete the log folder? Probably not, as it may contain useful information
        // Plus if things are instrumented then we can't anyway

        // success
        return 0;
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
