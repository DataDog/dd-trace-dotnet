// <copyright file="UninstallVersionCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Uninstall a single version of the fleet-installed .NET tracer
/// </summary>
internal sealed class UninstallVersionCommand : CommandBase
{
    private const string Command = "uninstall-version";
    private const string CommandDescription = "Uninstalls a single of the .NET library";

    private readonly Option<string> _versionedPathOption = new("--home-path", () => null!)
    {
        Description = "Path to the tracer-home-directory",
        IsRequired = true
    };

    public UninstallVersionCommand()
        : base(Command, CommandDescription)
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

        var result = Execute(log, tracerValues, Defaults.CrashTrackingRegistryKey);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
    internal static ReturnCode Execute(
        ILogger log,
        TracerValues tracerValues,
        string registryKeyName)
    {
        log.WriteInfo("Uninstalling .NET tracer package");

        if (!FileHelper.TryDeleteNativeLoaders(log, tracerValues))
        {
            // definitely bail - the files are in use
            return ReturnCode.ErrorRemovingNativeLoaderFiles;
        }

        // We check the prerequisites for GAC uninstall in here again, though they should have been checked in the above step
        if (!GacInstaller.TryGacUninstall(log, tracerValues))
        {
            // definitely bail out
            return ReturnCode.ErrorDuringGacUninstallation;
        }

        // We don't uninstall from the app host, as they should _already_ point to different values
        if (!RegistryHelper.RemoveCrashTrackingKey(log, tracerValues, registryKeyName))
        {
            // Probably don't _need_ to bail out of installation just because we failed to remove the crash tracker?
            // but returning an error here ensures that the fleet installer tries again later
            return ReturnCode.ErrorRemovingCrashTrackerKey;
        }

        return ReturnCode.Success;
    }

    private void Validate(CommandResult commandResult)
    {
        if (!IsValidEnvironment(commandResult))
        {
            return;
        }

        // If the tracer files already don't exist then that's fine
        // This could happen if there's an error part way through installation
    }
}
