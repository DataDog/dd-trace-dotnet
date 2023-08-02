// <copyright file="ServerlessMiniAgentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ServerlessMiniAgentTests : SettingsTestsBase
    {
        [Fact]
        public void GetMiniAgentPathNullInNonFunctionEnvironments()
        {
            var settings = new ImmutableTracerSettings(CreateConfigurationSource(("IsRunningInGCPFunctions", "false"), ("IsRunningInAzureFunctionsConsumptionPlan", "false")));

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            path.Should().BeNull();
        }

        [Fact]
        public void GetMiniAgentPathValidInDeprecatedGCPFunction()
        {
            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, "dummy_function");
            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey, "project_1");

            var settings = new ImmutableTracerSettings(CreateConfigurationSource());

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            path.Should().Be("/layers/google.dotnet.publish/publish/bin/datadog-serverless-agent-linux-amd64/datadog-serverless-trace-mini-agent");

            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, null);
            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey, null);
        }

        [Fact]
        public void GetMiniAgentPathValidInNonDeprecatedGCPFunction()
        {
            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey, "dummy_function");
            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey, "dummy_target");
            var source = CreateConfigurationSource();
            var settings = new ImmutableTracerSettings(source);

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            path.Should().Be("/layers/google.dotnet.publish/publish/bin/datadog-serverless-agent-linux-amd64/datadog-serverless-trace-mini-agent");

            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey, null);
            System.Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey, null);
        }

        [Fact]
        public void GetMiniAgentPathValidInLinuxAzureFunction()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, "dotnet");
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "4");

            var settings = new ImmutableTracerSettings(CreateConfigurationSource());

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            path.Should().Be("/home/site/wwwroot/datadog-serverless-agent-linux-amd64/datadog-serverless-trace-mini-agent");

            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, null);
        }

        [Fact]
        public void GetMiniAgentPathValidInWindowsAzureFunction()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, "dotnet");
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "4");

            var settings = new ImmutableTracerSettings(CreateConfigurationSource());

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Win32NT, settings);
            path.Should().Be("/home/site/wwwroot/datadog-serverless-agent-windows-amd64/datadog-serverless-trace-mini-agent.exe");

            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, null);
        }

        [Theory]
        [InlineData("[2023-06-06T01:31:30Z DEBUG datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "[DEBUG] Random Log")]
        [InlineData("[2023-06-06T01:31:30Z ERROR datadog_trace_mini_agent::mini_agent] Random Log", "ERROR", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z WARN datadog_trace_mini_agent::mini_agent] Random Log", "WARN", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z INFO datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z YELL datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "[2023-06-06T01:31:30Z YELL datadog_trace_mini_agent::mini_agent] Random Log")]
        [InlineData("DEBUG Log", "INFO", "DEBUG Log")]
        [InlineData("DEBUG  Log", "INFO", "DEBUG  Log")]
        [InlineData("Random Log", "INFO", "Random Log")]
        [InlineData("log", "INFO", "log")]
        internal void CleanAndProperlyLogMiniAgentLogs(string rawLog, string expectedLevel, string expectedLog)
        {
            var level = string.Empty;
            var log = string.Empty.
            ServerlessMiniAgent.ProcessMiniAgentLog(rawLog, level, log);
            expectedLevel.Should().Be(level);
            expectedLog.Should().Be(log);
        }
    }
}
