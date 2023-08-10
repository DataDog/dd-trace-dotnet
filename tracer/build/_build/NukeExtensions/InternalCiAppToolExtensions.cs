using System;
using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

partial class Build
{
    public IReadOnlyCollection<Output> DotNetTestWithCiApp(DotNetTestSettings toolSettings)
    {
        // install the tool
        // dotnet tool update -g dd-trace-ciapp --version $(ToolVersion) --add-source $(Agent.TempDirectory)
        var internalCiAppToolVersion = Environment.GetEnvironmentVariable("CIAPPINTERNALTOOL_VERSION");
        var internalCiAppToolDir = Environment.GetEnvironmentVariable("CIAPPINTERNALTOOL_DIR");

        var installPath = TemporaryDirectory / "dd-trace-ciapp";
        DotNetTasks.DotNetToolUpdate(x => x
            .SetVersion(internalCiAppToolVersion)
            .SetSources(internalCiAppToolDir)
            .SetPackageName("dd-trace-ciapp")
            .SetToolInstallationPath(installPath));

        var extension = EnvironmentInfo.IsWin ? ".exe" : string.Empty;
        var ciappToolPath = installPath / $"dd-trace-ciapp{extension}";

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

    public IReadOnlyCollection<Output> DotNetTestWithCiApp(Configure<DotNetTestSettings> configurator)
        => DotNetTestWithCiApp(configurator(new DotNetTestSettings()));

    public IEnumerable<(DotNetTestSettings Settings, IReadOnlyCollection<Output> Output)> DotNetTestWithCiApp(CombinatorialConfigure<DotNetTestSettings> configurator, int degreeOfParallelism = 1, bool completeOnFailure = false)
        => configurator.Invoke(DotNetTestWithCiApp, DotNetTasks.DotNetLogger, degreeOfParallelism, completeOnFailure);
}
