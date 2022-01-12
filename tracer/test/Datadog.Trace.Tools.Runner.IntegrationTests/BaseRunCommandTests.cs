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

        [Fact]
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
            using var agent = EnableCiVisibilityMode ? new MockTracerAgent(TcpPortProvider.GetOpenPort()) : null;

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

            environmentVariables["DD_ENV"].Should().Be("TestEnv");
            environmentVariables["DD_SERVICE"].Should().Be("TestService");
            environmentVariables["DD_VERSION"].Should().Be("TestVersion");
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be("TestTracerHome");
            environmentVariables["DD_TRACE_AGENT_URL"].Should().Be(agentUrl);
            environmentVariables["VAR1"].Should().Be("A");
            environmentVariables["VAR2"].Should().Be("B");

            if (EnableCiVisibilityMode)
            {
                environmentVariables["DD_CIVISIBILITY_ENABLED"].Should().Be("1");
            }
            else
            {
                environmentVariables.Should().NotContainKey("DD_CIVISIBILITY_ENABLED");
            }
        }

        [Fact]
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
            using var agent = EnableCiVisibilityMode ? new MockTracerAgent(TcpPortProvider.GetOpenPort()) : null;

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
    }
}
