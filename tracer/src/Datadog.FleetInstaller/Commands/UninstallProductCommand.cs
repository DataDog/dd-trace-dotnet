// <copyright file="UninstallProductCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Remove all version of the .NET tracer. Should be called for each version to be removed.
/// </summary>
internal class UninstallProductCommand : CommandBase
{
    public UninstallProductCommand()
        : base("uninstall-product")
    {
        AddValidator(Validate);
        this.SetHandler(ExecuteAsync);
    }

    public Task ExecuteAsync(InvocationContext context)
    {
        var log = Log.Instance;

        var result = Execute(log);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
    internal static ReturnCode Execute(ILogger log)
    {
        log.WriteInfo("Uninstalling .NET tracer product");

        // Remove the tracer references
        if (!AppHostHelper.RemoveAllEnvironmentVariables(log))
        {
            // hard to be sure exactly of the state at this point,
            // but probably don't want to do anything else if we couldn't remove the variables,
            // as apps may fail
            return ReturnCode.ErrorRemovingAppPoolVariables;
        }

        // Should we clean up/delete the log folder? Probably not, as it may contain useful information
        // Plus if things are instrumented then we can't anyway

        return ReturnCode.Success;
    }

    private void Validate(CommandResult commandResult)
    {
        if (!IsValidEnvironment(commandResult))
        {
            return;
        }

        // If the tracer files already don't exist etc then that's fine
    }
}
