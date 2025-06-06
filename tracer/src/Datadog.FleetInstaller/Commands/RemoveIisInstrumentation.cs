// <copyright file="RemoveIisInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Remove IIS instrumentation completely
/// </summary>
internal class RemoveIisInstrumentation : CommandBase
{
    private const string Command = "remove-iis-instrumentation";
    private const string CommandDescription = "Removes instrumentation with the .NET library from IIS";

    public RemoveIisInstrumentation()
        : base(Command, CommandDescription)
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
        log.WriteInfo("Removing IIS instrumentation for .NET tracer");

        if (!HasValidIIsVersion(out var errorMessage))
        {
            // IIS isn't available, weird because it means they removed it _after_ successfully installing the product
            // but whatever, there's no variables there if that's the case!
            Log.Instance.WriteInfo(errorMessage);
            Log.Instance.WriteInfo("Unable to uninstall from IIS, skipping IIS removal and continuing");
        }
        else
        {
            // Remove the tracer references
            if (!AppHostHelper.RemoveAllEnvironmentVariables(log))
            {
                // hard to be sure exactly of the state at this point,
                // but probably don't want to do anything else if we couldn't remove the variables,
                // as apps may fail
                return ReturnCode.ErrorRemovingAppPoolVariables;
            }
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
