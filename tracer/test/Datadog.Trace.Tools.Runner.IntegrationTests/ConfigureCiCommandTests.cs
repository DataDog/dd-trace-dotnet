// <copyright file="ConfigureCiCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class ConfigureCiCommandTests(ITestOutputHelper output)
    {
        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("azp", @"##vso\[task.setvariable variable=(?<name>[A-Z1-9_]+);\](?<value>.*)")]
        [InlineData("jenkins", @"(?<name>[A-Z1-9_]+)=(?<value>.*)")]
        [InlineData("github", @"(?<name>[A-Z1-9_]+)=(?<value>.*)", "GITHUB_ENV")]
        public void ConfigureCi(string ciProviderName, string pattern, string envKeyWithFilePath = null)
        {
            using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
            var agentUrl = $"http://localhost:{agent.Port}";

            var commandLine = $"ci configure {ciProviderName} --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

            string envKeyWithFilePathOriginalValue = null;
            string envKeyWithFilePathNewValue = null;
            if (!string.IsNullOrEmpty(envKeyWithFilePath))
            {
                envKeyWithFilePathOriginalValue = EnvironmentHelpers.GetEnvironmentVariable(envKeyWithFilePath);
                envKeyWithFilePathNewValue = Path.GetTempFileName();
                EnvironmentHelpers.SetEnvironmentVariable(envKeyWithFilePath, envKeyWithFilePathNewValue);
            }

            using var console = ConsoleHelper.Redirect();

            var result = Program.Main(commandLine.Split(' '));

            result.Should().Be(0);

            var environmentVariables = new Dictionary<string, string>();

            IEnumerable<string> lines = Array.Empty<string>();
            if (!string.IsNullOrEmpty(envKeyWithFilePathNewValue))
            {
                lines = File.ReadAllLines(envKeyWithFilePathNewValue);
            }
            else
            {
                lines = console.ReadLines();
            }

            foreach (var line in lines)
            {
                var match = Regex.Match(line, pattern);
                if (match.Success)
                {
                    environmentVariables.Add(match.Groups["name"].Value, match.Groups["value"].Value);
                }
            }

            environmentVariables.Should().Contain("DD_ENV", "TestEnv");
            environmentVariables.Should().Contain("DD_SERVICE", "TestService");
            environmentVariables.Should().Contain("DD_VERSION", "TestVersion");
            environmentVariables.Should().Contain("DD_DOTNET_TRACER_HOME", Path.GetFullPath("TestTracerHome"));
            environmentVariables.Should().Contain("DD_TRACE_AGENT_URL", agentUrl);

            if (!string.IsNullOrEmpty(envKeyWithFilePath))
            {
                EnvironmentHelpers.SetEnvironmentVariable(envKeyWithFilePath, envKeyWithFilePathOriginalValue);
            }
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("TF_BUILD", "1", 0, "Detected CI AzurePipelines.")]
        [InlineData("GITHUB_SHA", "1", 0, "Detected CI GithubActions.")]
        [InlineData("Nope", "0", 1, "Failed to autodetect CI.")]
        public void AutodetectCi(string key, string value, int expectedStatusCode, string expectedMessage)
        {
            var originalEnvVars = Environment.GetEnvironmentVariables();

            // Clear all environment variables
            foreach (string envKey in originalEnvVars.Keys)
            {
                Environment.SetEnvironmentVariable(envKey, null);
            }

            try
            {
                Environment.SetEnvironmentVariable(key, value);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";

                var commandLine = $"ci configure --tracer-home tracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();

                var result = Program.Main(commandLine.Split(' '));

                result.Should().Be(expectedStatusCode);

                console.Output.Should().Contain(expectedMessage);
            }
            finally
            {
                // Restore all environment variables
                // Clear all environment variables
                foreach (string envKey in originalEnvVars.Keys)
                {
                    Environment.SetEnvironmentVariable(envKey, (string)originalEnvVars[envKey]);
                }
            }
        }
    }
}
