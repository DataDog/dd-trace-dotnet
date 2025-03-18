// <copyright file="EnableIisInstrumentationCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Enables instrumentation of IIS for an already installed version of the .NET tracer
/// </summary>
internal class EnableIisInstrumentationCommand : CommandBase
{
    private const string Command = "enable-iis-instrumentation";
    private const string CommandDescription = "Enables instrumentation with the .NET library on IIS, for an already installed version of the .NET library";

    private readonly Option<string> _versionedPathOption = new("--home-path", () => null!)
    {
        Description = "Path to the tracer-home-directory",
        IsRequired = true,
    };

    public EnableIisInstrumentationCommand()
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
        log.WriteInfo("Enabling IIS instrumentation for .NET tracer");

        bool tryIisRollback;

        try
        {
            log.WriteInfo("Checking IIS app pools for pre-existing instrumentation variable");
            if (AppHostHelper.GetAppPoolEnvironmentVariable(log, Defaults.InstrumentationInstallTypeKey, out var value))
            {
                var expectedValue = Defaults.InstrumentationInstallTypeValue;
                if (expectedValue.Equals(value, StringComparison.Ordinal))
                {
                    log.WriteInfo($"Found existing instrumentation install type with value {expectedValue}. Won't rollback IIS instrumentation if install fails");
                    tryIisRollback = false;
                }
                else
                {
                    log.WriteInfo($"Found instrumentation install type {value}, but did not have expected value {expectedValue}. Will rollback IIS instrumentation if install fails");
                    tryIisRollback = true;
                }
            }
            else
            {
                log.WriteInfo("No existing fleet installer instrumentation install type found. Will rollback IIS instrumentation if install fails");
                tryIisRollback = true;
            }
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error reading IIS app pools, installation failed");
            return ReturnCode.ErrorReadingIisConfiguration;
        }

        if (!AppHostHelper.SetAllEnvironmentVariables(log, tracerValues))
        {
            // hard to be sure exactly of the state at this point
            if (tryIisRollback)
            {
                log.WriteInfo("Attempting IIS variable rollback");

                // We ignore failures here
                AppHostHelper.RemoveAllEnvironmentVariables(log);
            }

            return ReturnCode.ErrorSettingAppPoolVariables;
        }

        return ReturnCode.Success;
    }

    private void Validate(CommandResult commandResult)
    {
        if (!IsValidEnvironment(commandResult))
        {
            return;
        }

        // We can't enable iis instrumentation if IIS is not available or is to low a version
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
