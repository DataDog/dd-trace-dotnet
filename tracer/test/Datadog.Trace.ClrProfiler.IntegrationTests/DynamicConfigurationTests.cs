// <copyright file="DynamicConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(DynamicConfigurationTests), DisableParallelization = true)]
    [Collection(nameof(DynamicConfigurationTests))]
    public class DynamicConfigurationTests : TestHelper
    {
        private const string LogFileNamePrefix = "dotnet-tracer-managed-";
        private const string DiagnosticLog = "DATADOG TRACER CONFIGURATION";

        public DynamicConfigurationTests(ITestOutputHelper output)
            : base("Console", output)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task UpdateConfiguration()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*");
            using var sample = StartSample(agent, string.Empty, string.Empty, aspNetCorePort: 5000);

            try
            {
                _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    new Config
                    {
                        RuntimeMetricsEnabled = true,
                        DebugLogsEnabled = true,
                        DataStreamsEnabled = true,
                        LogsInjectionEnabled = true,
                        SpanSamplingRules = "[{\"service\": \"cart*\"}]",
                        TraceSampleRate = .5,
                        CustomSamplingRules = "[{\"sample_rate\":0.1}]"
                    });

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    new Config
                    {
                        RuntimeMetricsEnabled = false,
                        DebugLogsEnabled = false,
                        DataStreamsEnabled = false,
                        LogsInjectionEnabled = false,
                        SpanSamplingRules = string.Empty,
                        TraceSampleRate = null,
                        CustomSamplingRules = string.Empty
                    });
            }
            finally
            {
                if (!sample.HasExited)
                {
                    sample.Kill();
                }
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task RestoreInitialConfiguration()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*");

            EnvironmentHelper.CustomEnvironmentVariables["DD_TRACE_SAMPLE_RATE"] = "0.9";
            using var sample = StartSample(agent, string.Empty, string.Empty, aspNetCorePort: 5000);

            try
            {
                _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    new Config
                    {
                        TraceSampleRate = .5,
                    });

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    config: new Config(),
                    expectedConfig: new Config
                    {
                        TraceSampleRate = .9 // When clearing the key from dynamic configuration, it should revert back to the initial value
                    });
            }
            finally
            {
                if (!sample.HasExited)
                {
                    sample.Kill();
                }
            }
        }

        private async Task UpdateAndValidateConfig(MockTracerAgent agent, LogEntryWatcher logEntryWatcher, Config config, Config expectedConfig = null)
        {
            const string diagnosticLogRegex = @".+ (?<diagnosticLog>\{.+\})\s+(?<context>\{.+\})";

            expectedConfig ??= config;

            await agent.SetupRcmAndWait(Output, new[] { ((object)config, DynamicConfigurationManager.ProductName, "1") });
            var log = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

            using var context = new AssertionScope();
            context.AddReportable("log", log);

            var match = Regex.Match(log, diagnosticLogRegex);

            match.Success.Should().BeTrue();

            var json = JObject.Parse(match.Groups["diagnosticLog"].Value);

            json["runtime_metrics_enabled"]?.Value<bool>().Should().Be(expectedConfig.RuntimeMetricsEnabled);
            json["debug"]?.Value<bool>().Should().Be(expectedConfig.DebugLogsEnabled);
            json["log_injection_enabled"]?.Value<bool>().Should().Be(expectedConfig.LogsInjectionEnabled);
            json["sample_rate"]?.Value<double?>().Should().Be(expectedConfig.TraceSampleRate);
            json["sampling_rules"]?.Value<string>().Should().Be(expectedConfig.CustomSamplingRules);
            json["span_sampling_rules"]?.Value<string>().Should().Be(expectedConfig.SpanSamplingRules);
            json["data_streams_enabled"]?.Value<bool>().Should().Be(expectedConfig.DataStreamsEnabled);
        }

        // Missing: TraceHeaderTags, ServiceMapping
        public record Config
        {
            public bool RuntimeMetricsEnabled { get; init; }

            public bool DebugLogsEnabled { get; init; }

            public bool LogsInjectionEnabled { get; init; }

            public double? TraceSampleRate { get; init; }

            public string CustomSamplingRules { get; init; }

            public string SpanSamplingRules { get; init; }

            public bool DataStreamsEnabled { get; init; }
        }
    }
}
