using System.Collections.Generic;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

public class InternalCiAppToolExtensions
{
    public static IReadOnlyCollection<Output> DotNetTestWithCiApp(Configure<DotNetTestSettings> configurator)
    {
        // TODO: update this as required
        var ciappToolPath = "dd-trace";
        var toolSettings = configurator(new DotNetTestSettings());

        // Add dd-trace-ciapp arguments to wrap the execution
        var arguments = new Arguments();
        arguments
            .Add("ci")
            .Add("run")
            .Add("--")
            .Add("{value}", toolSettings.ProcessToolPath); // path to `dotnet`

        arguments.Concatenate((Arguments) toolSettings.GetProcessArguments());

        var process= ProcessTasks.StartProcess(
            ciappToolPath, // toolSettings.ProcessToolPath,
            arguments.RenderForExecution(),
            toolSettings.ProcessWorkingDirectory,
            toolSettings.ProcessEnvironmentVariables,
            toolSettings.ProcessExecutionTimeout,
            toolSettings.ProcessLogOutput,
            toolSettings.ProcessLogInvocation,
            toolSettings.ProcessCustomLogger,
            arguments.FilterSecrets);

        process.AssertZeroExitCode();
        return process.Output;
    }
    
}
