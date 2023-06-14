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
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ServerlessMiniAgentTests : IDisposable
    {
        private readonly Dictionary<string, string> _originalEnvVars;

        public ServerlessMiniAgentTests()
        {
            _originalEnvVars = new()
            {
                { ConfigurationKeys.AzureAppService.SiteNameKey, Environment.GetEnvironmentVariable(ConfigurationKeys.AzureAppService.SiteNameKey) },
                { ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, Environment.GetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey) },
                { ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey) },
                { ConfigurationKeys.GCPFunction.DeprecatedProjectKey, Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey) },
                { ConfigurationKeys.GCPFunction.FunctionNameKey, Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey) },
                { ConfigurationKeys.GCPFunction.FunctionTargetKey, Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey) },
            };
        }

        public void Dispose()
        {
            foreach (var originalEnvVar in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(originalEnvVar.Key, originalEnvVar.Value);
            }
        }

        [Fact]
        public void GetMiniAgentPathNullInNonFunctionEnvironments()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, null);

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix);
            Assert.Null(path);
        }

        [Fact]
        public void GetMiniAgentPathValidInDeprecatedGCPFunction()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, "dummy_function");
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey, "dummy_project");

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix);
            Assert.Equal("/layers/google.dotnet.publish/publish/bin/datadog-serverless-agent-linux-amd64/datadog-serverless-trace-mini-agent", path);
        }

        [Fact]
        public void GetMiniAgentPathValidInNewerGCPFunction()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey, "dummy_function");
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey, "dummy_target");

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix);
            Assert.Equal("/layers/google.dotnet.publish/publish/bin/datadog-serverless-agent-linux-amd64/datadog-serverless-trace-mini-agent", path);
        }

        [Fact]
        public void GetMiniAgentPathValidInLinuxAzureFunction()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.SiteNameKey, "function_name");
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "4");

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Unix);
            Assert.Equal("/home/site/wwwroot/datadog-serverless-agent-linux-amd64/datadog-serverless-trace-mini-agent", path);
        }

        [Fact]
        public void GetMiniAgentPathValidInWindowsAzureFunction()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.SiteNameKey, "function_name");
            Environment.SetEnvironmentVariable(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "4");

            var path = ServerlessMiniAgent.GetMiniAgentPath(System.PlatformID.Win32NT);
            Assert.Equal("C:\\home\\site\\wwwroot\\datadog-serverless-agent-windows-amd64\\datadog-serverless-trace-mini-agent.exe", path);
        }

        [Theory]
        [InlineData("[2023-06-06T01:31:30Z DEBUG datadog_trace_mini_agent::mini_agent] Random Log", "DEBUG", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z ERROR datadog_trace_mini_agent::mini_agent] Random Log", "ERROR", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z WARN datadog_trace_mini_agent::mini_agent] Random Log", "WARN", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z INFO datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z YELL datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "[2023-06-06T01:31:30Z YELL datadog_trace_mini_agent::mini_agent] Random Log")]
        [InlineData("Random Log", "INFO", "Random Log")]
        internal void CleanAndProperlyLogMiniAgentLogs(string rawLog, string expectedLevel, string expectedLog)
        {
            var logTuple = ServerlessMiniAgent.ProcessMiniAgentLog(rawLog);
            string level = logTuple.Item1;
            string processedLog = logTuple.Item2;
            Assert.Equal(expectedLevel, level);
            Assert.Equal(expectedLog, processedLog);
        }
    }
}
