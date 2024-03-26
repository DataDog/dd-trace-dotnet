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
            var settings = new ImmutableTracerSettings(CreateConfigurationSource());

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            path.Should().BeNull();
        }

        [Fact]
        public void GetMiniAgentPathValidInGCPFunction()
        {
            var settings = new ImmutableTracerSettings(CreateConfigurationSource(
                (ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, "value"),
                (ConfigurationKeys.GCPFunction.DeprecatedProjectKey, "value")));

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            var expectedPath = System.IO.Path.Combine(Path.DirectorySeparatorChar.ToString(), "layers", "google.dotnet.publish", "publish", "bin", "datadog-serverless-agent-linux-amd64", "datadog-serverless-trace-mini-agent");
            path.Should().Be(expectedPath);
        }

        [Fact]
        public void GetMiniAgentPathValidInLinuxAzureFunction()
        {
            var settings = new ImmutableTracerSettings(CreateConfigurationSource(
                (ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1"),
                (ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, "value"),
                (ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "value"),
                (ConfigurationKeys.AzureAppService.WebsiteSKU, "Dynamic")));

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix, settings);
            var expectedPath = System.IO.Path.Combine(Path.DirectorySeparatorChar.ToString(), "home", "site", "wwwroot", "datadog-serverless-agent-linux-amd64", "datadog-serverless-trace-mini-agent");
            path.Should().Be(expectedPath);
        }

        [Fact]
        public void GetMiniAgentPathValidInWindowsAzureFunction()
        {
            var settings = new ImmutableTracerSettings(CreateConfigurationSource(
                (ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1"),
                (ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, "value"),
                (ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "value"),
                (ConfigurationKeys.AzureAppService.WebsiteSKU, "Dynamic")));

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Win32NT, settings);
            var expectedPath = System.IO.Path.Combine(Path.DirectorySeparatorChar.ToString(), "home", "site", "wwwroot", "datadog-serverless-agent-windows-amd64", "datadog-serverless-trace-mini-agent.exe");
            path.Should().Be(expectedPath);
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
            var level = "INFO";
            var log = rawLog;
            ServerlessMiniAgent.ProcessMiniAgentLog(rawLog, out level, out log);
            expectedLevel.Should().Be(level);
            expectedLog.Should().Be(log);
        }
    }
}
