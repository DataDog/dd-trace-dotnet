// <copyright file="RunCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests;

public class RunCommandTests : ConsoleTestHelper
{
    public RunCommandTests(ITestOutputHelper output)
        : base(output)
    {
    }

#if NETFRAMEWORK
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task Run()
    {
        // This test is only for .NET Framework because it uses a syntax that doesn't allow to pass arguments to the target application
        // So it won't work with samples that must be started as dotnet <sampleName>

        var startInfo = PrepareSampleApp(EnvironmentHelper);

        var commandLine = $"run {startInfo.Executable} --dd-env TestEnv --dd-service TestService --dd-version TestVersion";

        var (standardOutput, errorOutput, exitCode) = await RunTool(commandLine);

        standardOutput.Should().Contain("Profiler attached: True")
            .And.NotContain("Args:");

        errorOutput.Should().BeEmpty();
        exitCode.Should().Be(0);
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task AdditionalArguments()
    {
        using var agent = MockTracerAgent.Create(Output);

        var startInfo = PrepareSampleApp(EnvironmentHelper);

        // dd-env is an argument for the target application and therefore shouldn't set the DD_ENV variable
        var commandLine = $"run --agent-url http://localhost:{agent.Port} --dd-service TestService --dd-env TestEnv --dd-version TestVersion -- {startInfo.Executable} {startInfo.Args} traces 5 --dd-env test";

        var (standardOutput, errorOutput, exitCode) = await RunTool(commandLine);

        using var scope = new AssertionScope();
        scope.AddReportable("StandardOutput", standardOutput);
        scope.AddReportable("ErrorOutput", errorOutput);
        scope.AddReportable("ExitCode", exitCode.ToString());

        standardOutput.Should().Contain("Args: traces 5 --dd-env test");
        errorOutput.Should().BeEmpty();
        exitCode.Should().Be(0);

        var spans = agent.WaitForSpans(5);

        spans.Should().HaveCount(5)
            .And.OnlyContain(s => s.Service == "TestService" && s.Tags[Tags.Version] == "TestVersion" && s.Tags[Tags.Env] == "TestEnv");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void RedirectInput()
    {
        using var agent = MockTracerAgent.Create(Output);

        var startInfo = PrepareSampleApp(EnvironmentHelper);

        // dd-env is an argument for the target application and therefore shouldn't set the DD_ENV variable
        var commandLine = $"run -- {startInfo.Executable} {startInfo.Args} echo";

        System.Console.InputEncoding = Encoding.ASCII;

        var process = RunToolInteractive(commandLine);

        try
        {
            while (process.StandardOutput.ReadLine() is not "Ready" or null)
            {
            }

            process.StandardInput.WriteLine("Hello World!");

            var output = process.StandardOutput.ReadLine();

            output.Should().Be("Echo: Hello World!");
        }
        finally
        {
            process.Kill();
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task EmptyCommand()
    {
        var (_, errorOutput, exitCode) = await RunTool("run --tracer-home dummyFolder --agent-url http://localhost:1111");

        errorOutput.Should().Contain("Empty command");
        exitCode.Should().Be(1);
    }
}
