// <copyright file="BaseRunCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    public abstract class BaseRunCommandTests
    {
        protected BaseRunCommandTests(string commandPrefix, bool enableCiVisibilityMode)
        {
            CommandPrefix = commandPrefix;
            EnableCiVisibilityMode = enableCiVisibilityMode;
        }

        protected string CommandPrefix { get; }

        protected bool EnableCiVisibilityMode { get; }

        [SkippableFact]
        public void Run()
        {
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool callbackInvoked = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
                callbackInvoked = true;
            };

            // CI visibility mode checks if there's a running agent
            using var agent = EnableCiVisibilityMode ? MockTracerAgent.Create(TcpPortProvider.GetOpenPort()) : null;

            var agentUrl = $"http://localhost:{agent?.Port ?? 1111}";

            var commandLine = $"{CommandPrefix} test.exe --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --set-env VAR1=A --set-env VAR2=B";

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
            environmentVariables.Should().Contain("DD_DOTNET_TRACER_HOME", "TestTracerHome");
            environmentVariables.Should().Contain("DD_TRACE_AGENT_URL", agentUrl);
            environmentVariables.Should().Contain("VAR1", "A");
            environmentVariables.Should().Contain("VAR2", "B");

            if (EnableCiVisibilityMode)
            {
                environmentVariables.Should().Contain("DD_CIVISIBILITY_ENABLED", "1");
            }
            else
            {
                environmentVariables.Should().NotContainKey("DD_CIVISIBILITY_ENABLED");
            }
        }

        [SkippableFact]
        public void AdditionalArguments()
        {
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool callbackInvoked = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
                callbackInvoked = true;
            };

            // CI visibility mode checks if there's a running agent
            using var agent = EnableCiVisibilityMode ? MockTracerAgent.Create(TcpPortProvider.GetOpenPort()) : null;

            var agentUrl = $"http://localhost:{agent?.Port ?? 1111}";

            // dd-env is an argument for the target application and therefore shouldn't set the DD_ENV variable
            var commandLine = $"{CommandPrefix} --tracer-home dummyFolder --agent-url {agentUrl} -- test.exe --dd-env test";

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
        public void EmptyCommand()
        {
            bool callbackInvoked = false;

            Program.CallbackForTests = (_, _, _) =>
            {
                callbackInvoked = true;
            };

            // CI visibility mode checks if there's a running agent
            using var agent = EnableCiVisibilityMode ? MockTracerAgent.Create(TcpPortProvider.GetOpenPort()) : null;

            var agentUrl = $"http://localhost:{agent?.Port ?? 1111}";

            // dd-env is an argument for the target application and therefore shouldn't set the DD_ENV variable
            var commandLine = $"{CommandPrefix} --tracer-home dummyFolder --agent-url {agentUrl}";

            using var console = ConsoleHelper.Redirect();

            var exitCode = Program.Main(commandLine.Split(' '));

            using var scope = new AssertionScope();

            scope.AddReportable("output", console.Output);

            exitCode.Should().Be(1);
            callbackInvoked.Should().BeFalse();
            console.Output.Should().Contain("Error: Missing command");
        }
    }
}
