// <copyright file="LegacyCommandLineArgumentsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.Runner.ArtifactTests
{
    [CollectionDefinition(nameof(LegacyCommandLineArgumentsTests), DisableParallelization = true)]
    [Collection(nameof(LegacyCommandLineArgumentsTests))]
    public class LegacyCommandLineArgumentsTests : RunnerTests
    {
        [Fact]
        public void InvalidArgument()
        {
            // This test makes sure that wrong arguments will return a non-zero exit code

            using var helper = StartProcess("--dummy-wrong-argument");

            helper.Process.WaitForExit();
            helper.Drain();

            using var scope = StartAssertionScope(helper);

            helper.Process.ExitCode.Should().NotBe(0);
        }

        [Theory]
        [InlineData(' ')]
        [InlineData('=')]
        public void SetCi(char separator)
        {
            var commandLine = $"--set-ci --dd-env{separator}TestEnv --dd-service{separator}TestService --dd-version{separator}TestVersion --tracer-home{separator}TestTracerHome --agent-url{separator}TestAgentUrl --env-vars{separator}VAR1=A,VAR2=B";

            using var helper = StartProcess(commandLine, ("TF_BUILD", "1"));

            helper.Process.WaitForExit();
            helper.Drain();

            helper.Process.ExitCode.Should().Be(0);

            var environmentVariables = new Dictionary<string, string>();

            foreach (var line in helper.StandardOutput.Split(Environment.NewLine))
            {
                // ##vso[task.setvariable variable=DD_DOTNET_TRACER_HOME]TestTracerHome;
                var match = Regex.Match(line, @"##vso\[task.setvariable variable=(?<name>[A-Z1-9_]+);\](?<value>.*)");

                if (match.Success)
                {
                    environmentVariables.Add(match.Groups["name"].Value, match.Groups["value"].Value);
                }
            }

            environmentVariables["DD_ENV"].Should().Be("TestEnv");
            environmentVariables["DD_SERVICE"].Should().Be("TestService");
            environmentVariables["DD_VERSION"].Should().Be("TestVersion");
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath("TestTracerHome"));
            environmentVariables["DD_TRACE_AGENT_URL"].Should().Be("TestAgentUrl");
            environmentVariables["VAR1"].Should().Be("A");
            environmentVariables["VAR2"].Should().Be("B");
        }

        [Fact]
        public void LocateTracerHome()
        {
            var commandLine = "--set-ci";

            using var helper = StartProcess(commandLine, ("TF_BUILD", "1"));

            helper.Process.WaitForExit();
            helper.Drain();

            helper.Process.ExitCode.Should().Be(0);
            helper.StandardOutput.Should().NotContainEquivalentOf("error");
            helper.ErrorOutput.Should().BeEmpty();
        }

        private static AssertionScope StartAssertionScope(ProcessHelper processHelper)
        {
            var scope = new AssertionScope();

            scope.AddReportable("Standard output", processHelper.StandardOutput);
            scope.AddReportable("Error output", processHelper.ErrorOutput);
            scope.AddReportable("Exit code", processHelper.Process.ExitCode.ToString());

            return scope;
        }
    }
}
