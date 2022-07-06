// <copyright file="LegacyCommandLineArgumentsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class LegacyCommandLineArgumentsTests
    {
        [SkippableFact]
        public void InvalidArgument()
        {
            // This test makes sure that wrong arguments will return a non-zero exit code

            // Redirecting the console here isn't necessary, but it removes some noise in the CI output
            using var console = ConsoleHelper.Redirect();

            var exitCode = Program.Main(new[] { "--dummy-wrong-argument" });

            exitCode.Should().NotBe(0);
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void Run(bool withArguments)
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
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());

            var agentUrl = $"http://localhost:{agent.Port}";

            string commandLine;

            if (withArguments)
            {
                commandLine = $"--dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --ci-visibility --env-vars VAR1=A,VAR2=B -- test.exe --dd-env arg";
            }
            else
            {
                commandLine = $"test.exe --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --ci-visibility --env-vars VAR1=A,VAR2=B";
            }

            // Redirecting the console here isn't necessary, but it removes some noise in the CI output
            using var console = ConsoleHelper.Redirect();

            var exitCode = Program.Main(commandLine.Split(' '));

            exitCode.Should().Be(0);
            callbackInvoked.Should().BeTrue();

            command.Should().Be("test.exe");

            if (withArguments)
            {
                arguments.Should().Be("--dd-env arg");
            }
            else
            {
                arguments.Should().BeNullOrEmpty();
            }

            environmentVariables.Should().NotBeNull();

            environmentVariables.Should().Contain("DD_ENV", "TestEnv");
            environmentVariables.Should().Contain("DD_SERVICE", "TestService");
            environmentVariables.Should().Contain("DD_VERSION", "TestVersion");
            environmentVariables.Should().Contain("DD_DOTNET_TRACER_HOME", "TestTracerHome");
            environmentVariables.Should().Contain("DD_TRACE_AGENT_URL", agentUrl);
            environmentVariables.Should().Contain("DD_CIVISIBILITY_ENABLED", "1");
            environmentVariables.Should().Contain("VAR1", "A");
            environmentVariables.Should().Contain("VAR2", "B");
        }

        [SkippableTheory]
        [InlineData(' ')]
        [InlineData('=')]
        public void SetCi(char separator)
        {
            var tfBuild = Environment.GetEnvironmentVariable("TF_BUILD");

            try
            {
                Environment.SetEnvironmentVariable("TF_BUILD", "1");

                using var console = ConsoleHelper.Redirect();

                var commandLine = $"--set-ci --dd-env{separator}TestEnv --dd-service{separator}TestService --dd-version{separator}TestVersion --tracer-home{separator}TestTracerHome --agent-url{separator}TestAgentUrl --env-vars{separator}VAR1=A,VAR2=B";

                var exitCode = Program.Main(commandLine.Split(' '));

                exitCode.Should().Be(0);

                var environmentVariables = new Dictionary<string, string>();

                foreach (var line in console.ReadLines())
                {
                    // ##vso[task.setvariable variable=DD_DOTNET_TRACER_HOME]TestTracerHome
                    var match = Regex.Match(line, @"##vso\[task.setvariable variable=(?<name>[A-Z1-9_]+)\](?<value>.*)");

                    if (match.Success)
                    {
                        environmentVariables.Add(match.Groups["name"].Value, match.Groups["value"].Value);
                    }
                }

                environmentVariables.Should().Contain("DD_ENV", "TestEnv");
                environmentVariables.Should().Contain("DD_SERVICE", "TestService");
                environmentVariables.Should().Contain("DD_VERSION", "TestVersion");
                environmentVariables.Should().Contain("DD_DOTNET_TRACER_HOME", "TestTracerHome");
                environmentVariables.Should().Contain("DD_TRACE_AGENT_URL", "TestAgentUrl");
                environmentVariables.Should().Contain("VAR1", "A");
                environmentVariables.Should().Contain("VAR2", "B");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TF_BUILD", tfBuild);
            }
        }
    }
}
