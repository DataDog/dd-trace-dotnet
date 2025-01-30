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
internal class UninstallAllCommand : CommandBase
{
    public UninstallAllCommand()
        : base("uninstall-all")
    {
    }

    // Internal for testing
    internal static ReturnCode ExecuteAsync(
        ILogger log,
        TracerValues tracerValues,
        string tracerLogDirectory,
        string registryKeyName)
    {
        log.WriteInfo("Uninstalling .NET tracer product");

        if (!RegistryHelper.RemoveCrashTrackingKey(log, tracerValues, registryKeyName))
        {
            // Not a big deal if this isn't actually removed
        }

        // Remove the tracer references
        if (!AppHostHelper.RemoveAllEnvironmentVariables(log, tracerValues))
        {
            // hard to be sure exactly of the state at this point,
            // but probably don't want to do anything else if we couldn't remove the variables,
            // as apps may fail
            return ReturnCode.ErrorRemovingAppPoolVariables;
        }

        if (!GacInstaller.TryGacUninstall(log, tracerValues))
        {
            // We don't actually care if this fails (and it probably _will_, if we haven't yet deleted the tracer files)
            // as it just leaves some files around
        }

        // Should we clean up/delete the log folder? Probably not, as it may contain useful information
        // Plus if things are instrumented then we can't anyway

        // success
        return 0;
    }

    protected override Task<ReturnCode> ExecuteAsync(ILogger log, InvocationContext context, TracerValues versionedPath)
        => Task.FromResult(ExecuteAsync(log, versionedPath, Defaults.TracerLogDirectory, Defaults.CrashTrackingRegistryKey));
}
