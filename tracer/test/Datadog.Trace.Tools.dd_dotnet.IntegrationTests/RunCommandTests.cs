// <copyright file="RunCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.IntegrationTests;

[Collection(nameof(ConsoleTestsCollection))]
public class RunCommandTests
{
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void Run()
    {
        string? command = null;
        string? arguments = null;
        Dictionary<string, string>? environmentVariables = null;
        bool callbackInvoked = false;

        Program.CallbackForTests = (c, a, e) =>
        {
            command = c;
            arguments = a;
            environmentVariables = e;
            callbackInvoked = true;
        };

        var commandLine = $"run test.exe --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url http://localhost:1111 --set-env VAR1=A --set-env VAR2=B";

        using var console = ConsoleHelper.Redirect();

        var exitCode = Program.Main(commandLine.Split(' '));

        using var scope = new AssertionScope();

        scope.AddReportable("output", console.Output);

        exitCode.Should().Be(0);
        callbackInvoked.Should().BeTrue();

        command.Should().Be("test.exe");
        arguments.Should().BeNullOrEmpty();
        environmentVariables.Should().NotBeNull();

        environmentVariables.Should().Contain("DD_ENV", "TestEnv");
        environmentVariables.Should().Contain("DD_SERVICE", "TestService");
        environmentVariables.Should().Contain("DD_VERSION", "TestVersion");
        environmentVariables.Should().Contain("DD_DOTNET_TRACER_HOME", Path.GetFullPath("TestTracerHome"));
        environmentVariables.Should().Contain("DD_TRACE_AGENT_URL", "http://localhost:1111");
        environmentVariables.Should().Contain("VAR1", "A");
        environmentVariables.Should().Contain("VAR2", "B");
        environmentVariables.Should().NotContainKey("DD_CIVISIBILITY_ENABLED");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void AdditionalArguments()
    {
        string? command = null;
        string? arguments = null;
        Dictionary<string, string>? environmentVariables = null;
        bool callbackInvoked = false;

        Program.CallbackForTests = (c, a, e) =>
        {
            command = c;
            arguments = a;
            environmentVariables = e;
            callbackInvoked = true;
        };

        // dd-env is an argument for the target application and therefore shouldn't set the DD_ENV variable
        var commandLine = $"run --tracer-home dummyFolder --agent-url http://localhost:1111 -- test.exe --dd-env test";

        using var console = ConsoleHelper.Redirect();

        var exitCode = Program.Main(commandLine.Split(' '));

        using var scope = new AssertionScope();

        scope.AddReportable("output", console.Output);

        exitCode.Should().Be(0);
        callbackInvoked.Should().BeTrue();

        command.Should().Be("test.exe");
        arguments.Should().Be("--dd-env test");
        environmentVariables.Should().NotContainKey("DD_ENV");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void EmptyCommand()
    {
        bool callbackInvoked = false;

        Program.CallbackForTests = (_, _, _) =>
        {
            callbackInvoked = true;
        };

        // dd-env is an argument for the target application and therefore shouldn't set the DD_ENV variable
        var commandLine = $"run --tracer-home dummyFolder --agent-url http://localhost:1111";

        using var console = ConsoleHelper.Redirect();

        var exitCode = Program.Main(commandLine.Split(' '));

        using var scope = new AssertionScope();

        scope.AddReportable("output", console.Output);

        exitCode.Should().Be(1);
        callbackInvoked.Should().BeFalse();
        console.Output.Should().Contain("Empty command");
    }
}
