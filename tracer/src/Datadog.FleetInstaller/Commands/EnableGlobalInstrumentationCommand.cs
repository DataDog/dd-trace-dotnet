// <copyright file="EnableGlobalInstrumentationCommand.cs" company="Datadog">
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
/// Enables global instrumentation of .NET apps, including IIS instrumentation, for an already installed version of the .NET tracer
/// </summary>
internal class EnableGlobalInstrumentationCommand : CommandBase
{
    private const string Command = "enable-global-instrumentation";
    private const string CommandDescription = "Enables instrumentation globally with the .NET library, including for apps running in IIS, for an already installed version of the .NET library";

    private readonly Option<string> _versionedPathOption = new("--home-path", () => null!)
    {
        Description = "Path to the tracer-home-directory",
        IsRequired = true,
    };

    public EnableGlobalInstrumentationCommand()
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

        var result = Execute(log, tracerValues);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
    internal static ReturnCode Execute(ILogger log, TracerValues tracerInstallValues)
    {
        log.WriteInfo("Enabling instrumentation for .NET tracer");

        if (!GlobalEnvVariableHelper.SetMachineEnvironmentVariables(log, tracerInstallValues, out var previousVariables))
        {
            log.WriteError("Failed to set global environment variables");
            return ReturnCode.ErrorSettingGlobalEnvironmentVariables;
        }

        // We can't enable iis instrumentation if IIS is not available or it's too low of a version
        if (!HasValidIIsVersion(out var errorMessage))
        {
            // nothing more to do in this case
            log.WriteInfo("Skipping IIS instrumentation. " + errorMessage);
            log.WriteInfo("Instrumentation complete");
            return ReturnCode.Success;
        }

        var iisResult = EnableIisInstrumentationCommand.Execute(log, tracerInstallValues);
        if (iisResult is ReturnCode.Success)
        {
            log.WriteInfo("Instrumentation complete");
            return ReturnCode.Success;
        }

        log.WriteInfo("Reverting global environment variables after failed install");
        GlobalEnvVariableHelper.RevertMachineEnvironmentVariables(log, previousVariables);

        log.WriteError("Instrumentation failed");
        return iisResult;
    }

    private void Validate(CommandResult commandResult)
    {
        if (!IsValidEnvironment(commandResult))
        {
            return;
        }

        var path = commandResult.GetValueForOption(_versionedPathOption);
        if (path is not null && !FileHelper.TryVerifyFilesExist(Log.Instance, new TracerValues(path), out var err))
        {
            commandResult.ErrorMessage = err;
        }
    }
}
