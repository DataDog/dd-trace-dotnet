using System;
using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;

partial class Build
{
    public IReadOnlyCollection<Output> DotNetTestWithCiApp(DotNetTestSettings toolSettings)
    {
        if (!InternalCiAppToolEnabled)
        {
            return DotNetTasks.DotNetTest(toolSettings);
        }

        Log.Information("Using dd-trace-ciapp to execute dotnet test");

        // Add dd-trace-ciapp arguments to wrap the execution
        var arguments = new Arguments();
        arguments
            .Add("ci")
            .Add("run")
            .Add("--")
            .Add("{value}", toolSettings.ProcessToolPath); // path to `dotnet`

        arguments.Concatenate((Arguments) toolSettings.GetProcessArguments());

        var process= ProcessTasks.StartProcess(
            InternalCiAppToolPath, // toolSettings.ProcessToolPath,
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

    public IReadOnlyCollection<Output> DotNetTestWithCiApp(Configure<DotNetTestSettings> configurator)
        => DotNetTestWithCiApp(configurator(new DotNetTestSettings()));

    public IEnumerable<(DotNetTestSettings Settings, IReadOnlyCollection<Output> Output)> DotNetTestWithCiApp(CombinatorialConfigure<DotNetTestSettings> configurator, int degreeOfParallelism = 1, bool completeOnFailure = false)
        => configurator.Invoke(DotNetTestWithCiApp, DotNetTasks.DotNetLogger, degreeOfParallelism, completeOnFailure);
}
