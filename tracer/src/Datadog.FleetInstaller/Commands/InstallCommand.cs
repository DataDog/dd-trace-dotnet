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
internal class InstallCommand : CommandBase
{
    public InstallCommand()
        : this("install")
    {
    }

    protected InstallCommand(string command)
        : base(command)
    {
    }

    // Internal for testing
    internal static ReturnCode ExecuteAsync(
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
            // Probably Don't need to bail out of installation just because we failed to add crash tracking?
        }

        // success
        return ReturnCode.Success;
    }

    protected override Task<ReturnCode> ExecuteAsync(ILogger log, InvocationContext context, TracerValues versionedPath)
        => Task.FromResult(ExecuteAsync(log, versionedPath, Defaults.TracerLogDirectory, Defaults.CrashTrackingRegistryKey));
}
