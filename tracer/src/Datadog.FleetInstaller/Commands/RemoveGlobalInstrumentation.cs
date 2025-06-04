// <copyright file="RemoveGlobalInstrumentation.cs" company="Datadog">
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
internal class RemoveGlobalInstrumentation : CommandBase
{
    private const string Command = "remove-global-instrumentation";
    private const string CommandDescription = "Removes instrumentation globally with the .NET library, including from IIS";

    public RemoveGlobalInstrumentation()
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
    internal ReturnCode Execute(ILogger log)
    {
        log.WriteInfo("Removing global instrumentation for .NET tracer");

        if (!GlobalEnvVariableHelper.RemoveMachineEnvironmentVariables(log))
        {
            log.WriteError("Failed to remove global environment variables. Apps may continue to be instrumented");
            return ReturnCode.ErrorRemovingGlobalEnvironmentVariables;
        }

        var iisResult = RemoveIisInstrumentation.Execute(log);
        if (iisResult != ReturnCode.Success)
        {
            log.WriteError("Failed to remove IIS instrumentation. Apps may continue to be instrumented");
        }

        return iisResult;
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
