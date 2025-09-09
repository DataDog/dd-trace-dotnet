// <copyright file="HandsOffConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(HandsOffConfigurationTests), DisableParallelization = true)]
    [Collection(nameof(HandsOffConfigurationTests))]
    [EnvironmentRestorer("DD_TRACE_LOG_DIRECTORY", "DD_APPSEC_ENABLED", "DD_TRACE_DEBUG", "DD_IAST_ENABLED", "DD_PROFILER_ENABLED")]
    public class HandsOffConfigurationTests(ITestOutputHelper output) : TestHelper("Console", output)
    {
        private const string LogFileNamePrefix = "dotnet-tracer-managed-";
        private const string ConfigLog = "DATADOG TRACER CONFIGURATION";

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("DD_APPSEC_ENABLED", "true", "appsec_enabled", "true", true)]
        [InlineData("DD_APPSEC_ENABLED", "false", "appsec_enabled", "false", true)]
        [InlineData("DD_APPSEC_ENABLED", "true", "appsec_enabled", "true", false)]
        [InlineData("DD_APPSEC_ENABLED", "false", "appsec_enabled", "false", false)]
        public async Task HandsOffConfigurationIsTakenIntoAccount(string key, string value, string logKey, string expectedValue, bool local)
        {
            await SetupAndCheck(nameof(HandsOffConfigurationIsTakenIntoAccount), local ? key : null, local ? value : null, !local ? key : null, !local ? value : null, logKey, expectedValue);
        }

        [SkippableFact]
        public async Task FleetShouldWinOverEnvVar()
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "true");
            await SetupAndCheck(
                logFolder: nameof(FleetShouldWinOverEnvVar),
                localKey: null,
                localValue: null,
                fleetKey: ConfigurationKeys.DebugEnabled,
                fleetValue: "false",
                logKey: "debug",
                expectedValue: "false");
        }

        [SkippableFact]
        public async Task EnVarShouldWinOverLocal()
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "true");
            await SetupAndCheck(
                logFolder: nameof(EnVarShouldWinOverLocal),
                localKey: ConfigurationKeys.DebugEnabled,
                localValue: "false",
                fleetKey: null,
                fleetValue: null,
                logKey: "debug",
                expectedValue: "true");
        }

        [SkippableFact]
        public async Task FleetShouldWinOverLocalAndEnvVar()
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "false");
            await SetupAndCheck(
                logFolder: nameof(FleetShouldWinOverLocalAndEnvVar),
                localKey: ConfigurationKeys.DebugEnabled,
                localValue: "false",
                fleetKey: ConfigurationKeys.DebugEnabled,
                fleetValue: "true",
                logKey: "debug",
                expectedValue: "true");
        }

        private static (string LocalPath, string FleetPath) CreateHandsOffConfigFiles((string Key, string Value)[] localValues, (string Key, string Value)[] fleetValues)
        {
            const string applicationMonitoringYaml = "application_monitoring.yaml";
            var localDestPathFolder = string.Empty;
            var fleetDestPathFolder = string.Empty;
            var localDestPath = string.Empty;
            var fleetDestPath = string.Empty;
            if (EnvironmentTools.IsLinux())
            {
                localDestPathFolder = "/etc/datadog-agent/";
                fleetDestPathFolder = "/etc/datadog-agent/managed/datadog-agent/stable/";
                localDestPath = Path.Combine(localDestPathFolder, applicationMonitoringYaml);
                fleetDestPath = Path.Combine(fleetDestPathFolder, applicationMonitoringYaml);
            }
            else if (EnvironmentTools.IsWindows())
            {
                localDestPathFolder = "C:\\ProgramData\\Datadog\\";
                fleetDestPathFolder = "C:\\ProgramData\\Datadog\\managed\\datadog-agent\\stable\\";
                localDestPath = Path.Combine(localDestPathFolder, applicationMonitoringYaml);
                fleetDestPath = Path.Combine(fleetDestPathFolder, applicationMonitoringYaml);
            }
            else if (EnvironmentTools.IsOsx())
            {
                // even if it's skipped on mac keep it for local debugging purpose
                localDestPathFolder = "/opt/datadog-agent/etc/";
                fleetDestPathFolder = "/opt/datadog-agent/etc/stable/";
                localDestPath = Path.Combine(localDestPathFolder, applicationMonitoringYaml);
                fleetDestPath = Path.Combine(fleetDestPathFolder, applicationMonitoringYaml);
            }

            if (File.Exists(localDestPath))
            {
                File.Delete(localDestPath);
            }

            if (File.Exists(fleetDestPath))
            {
                File.Delete(fleetDestPath);
            }

            var localLines = localValues.Select(localValue => $"  {localValue.Key}: {localValue.Value}").ToList();
            if (localLines.Any())
            {
                if (!Directory.Exists(localDestPathFolder))
                {
                    Directory.CreateDirectory(localDestPathFolder);
                }

                localLines.Insert(0, "apm_configuration_default:");
                File.AppendAllLines(localDestPath, localLines);
            }

            var fleetLines = fleetValues.Select(fleetValue => $"  {fleetValue.Key}: {fleetValue.Value}").ToList();
            if (fleetLines.Any())
            {
                if (!Directory.Exists(fleetDestPathFolder))
                {
                    Directory.CreateDirectory(fleetDestPathFolder);
                }

                fleetLines.Insert(0, "apm_configuration_default:");
                File.AppendAllLines(fleetDestPath, fleetLines);
            }

            return (localDestPath, fleetDestPath);
        }

        private async Task SetupAndCheck(string logFolder, string? localKey, string? localValue, string? fleetKey, string? fleetValue, string logKey, string expectedValue)
        {
            var logDir = Path.Combine(LogDirectory, logFolder);
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

            using var agent = MockTracerAgent.Create(Output, useTelemetry: false);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", logDir, Output);

            var (localDestPath, fleetDestPath) = CreateHandsOffConfigFiles(
                localKey is not null ? [(Key: localKey, Value: localValue!)] : [],
                fleetKey is not null ? [(Key: fleetKey, Value: fleetValue!)] : []);

            using var sample = await StartSample(agent, "wait", string.Empty, aspNetCorePort: 5000);

            try
            {
                var entry = await logEntryWatcher.WaitForLogEntry(ConfigLog);
                entry.Should().NotBeNull();
                var part = Regex.Match(entry, $"\"{logKey}\":([^,]*),");
                var actualValue = part.Groups[1].Value.Trim();
                actualValue.Should().Be(expectedValue, $"Expected {logKey} to be {expectedValue}, but actual value turned out to be {actualValue}");
            }
            finally
            {
                if (File.Exists(localDestPath))
                {
                    File.Delete(localDestPath);
                }

                if (File.Exists(fleetDestPath))
                {
                    File.Delete(fleetDestPath);
                }

                if (!sample.HasExited)
                {
                    sample.Kill();
                }
            }
        }
    }
}
