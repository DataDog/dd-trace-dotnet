// <copyright file="ConfigureCiCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class ConfigureCiCommandTests
    {
        [SkippableFact]
        public void ConfigureCi()
        {
            var commandLine = "ci configure azp --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url TestAgentUrl";

            using var console = ConsoleHelper.Redirect();

            var result = Program.Main(commandLine.Split(' '));

            result.Should().Be(0);

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
        }

        [SkippableTheory]
        [InlineData("TF_BUILD", "1", 0, "Detected CI AzurePipelines.")]
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

                var commandLine = "ci configure --tracer-home tracerHome";

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
