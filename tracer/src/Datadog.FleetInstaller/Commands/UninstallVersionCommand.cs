// <copyright file="UninstallVersionCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Uninstall a single version of the fleet-installed .NET tracer
/// </summary>
internal class UninstallVersionCommand : CommandBase
{
    public UninstallVersionCommand()
        : base("uninstall-version")
    {
    }

    // Internal for testing
    internal static ReturnCode ExecuteAsync(
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
            // Probably Don't need to bail out of installation just because we failed to remove the crash tracker?
        }

        // success
        return ReturnCode.Success;
    }

    protected override Task<ReturnCode> ExecuteAsync(ILogger log, InvocationContext context, TracerValues versionedPath)
        => Task.FromResult(ExecuteAsync(log, versionedPath, Defaults.CrashTrackingRegistryKey));
}
