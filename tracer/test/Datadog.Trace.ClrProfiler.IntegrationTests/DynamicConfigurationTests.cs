// <copyright file="DynamicConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
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
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.HeartbeatIntervalSeconds, "1");
            SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "5");
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task UpdateConfiguration()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", LogDirectory);
            using var sample = await StartSample(agent, "wait", string.Empty, aspNetCorePort: 5000);

            try
            {
                _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    new Config
                    {
                        TraceEnabled = false,
                        // RuntimeMetricsEnabled = true,
                        // DebugLogsEnabled = true,
                        // DataStreamsEnabled = true,
                        LogInjectionEnabled = true,
                        // SpanSamplingRules = "[{\"service\": \"cart*\"}]",
                        TraceSampleRate = .5,
                        TraceSamplingRules = "[{\"sample_rate\":0.1}]",
                        // ServiceNameMapping = "[{\"from_key\":\"foo\", \"to_name\":\"bar\"}]",
                        TraceHeaderTags = "[{ \"header\": \"User-Agent\", \"tag_name\": \"http.user_agent\" }]",
                        GlobalTags = "[\"foo1:bar1\",\"foo2:bar2\"]"
                    },
                    new Config
                    {
                        TraceEnabled = false,
                        // RuntimeMetricsEnabled = true,
                        // DebugLogsEnabled = true,
                        // DataStreamsEnabled = true,
                        LogInjectionEnabled = true,
                        // SpanSamplingRules = "[{\"service\": \"cart*\"}]",
                        TraceSampleRate = .5,
                        TraceSamplingRules = "[{\"sample_rate\":0.1}]",
                        // ServiceNameMapping = "foo:bar",
                        TraceHeaderTags = "User-Agent:http.user_agent",
                        GlobalTags = "[\"foo1:bar1\",\"foo2:bar2\"]"
                    });

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    new Config());
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
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", LogDirectory);

            SetEnvironmentVariable("DD_TRACE_SAMPLE_RATE", "0.9");
            using var sample = await StartSample(agent, "wait", string.Empty, aspNetCorePort: 5000);

            try
            {
                _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    new Config
                    {
                        TraceEnabled = false,
                        TraceSampleRate = .5,
                    });

                await UpdateAndValidateConfig(
                    agent,
                    logEntryWatcher,
                    config: new Config(),
                    expectedConfig: new Config
                    {
                        // When clearing the key from dynamic configuration,
                        // it should revert back to the initial value
                        TraceEnabled = true,
                        TraceSampleRate = .9
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

        private static IEnumerable<ConfigurationKeyValue> ExtractConfiguration(TelemetryData wrapper)
        {
            if (wrapper.IsRequestType(TelemetryRequestTypes.AppClientConfigurationChanged))
            {
                var configurationChanged = wrapper.TryGetPayload<AppClientConfigurationChangedPayload>(TelemetryRequestTypes.AppClientConfigurationChanged);
                return configurationChanged.Configuration;
            }

            return [];
        }

        private async Task UpdateAndValidateConfig(MockTracerAgent agent, LogEntryWatcher logEntryWatcher, Config config, Config expectedConfig = null)
        {
            const string diagnosticLogRegex = @".+ (?<diagnosticLog>\{.+\})\s+(?<context>\{.+\})";

            expectedConfig ??= config;

            var fileId = Guid.NewGuid().ToString();

            var request = await agent.SetupRcmAndWait(Output, new[] { ((object)new { lib_config = config }, DynamicConfigurationManager.ProductName, fileId) });

            // copy the byte array and reverse the bytes to create a BitArray with the correct order
            var capabilityBytes = new byte[request.Client.Capabilities.Length];
            Array.Copy(request.Client.Capabilities, capabilityBytes, request.Client.Capabilities.Length);
            Array.Reverse(capabilityBytes);

            // Validate capabilities
            var capabilities = new BitArray(capabilityBytes);

            capabilities[12].Should().BeTrue(); // APM_TRACING_SAMPLE_RATE
            capabilities[13].Should().BeTrue(); // APM_TRACING_LOGS_INJECTION
            capabilities[14].Should().BeTrue(); // APM_TRACING_HTTP_HEADER_TAGS
            capabilities[15].Should().BeTrue(); // APM_TRACING_CUSTOM_TAGS
            capabilities[19].Should().BeTrue(); // APM_TRACING_TRACING_ENABLED
            capabilities[29].Should().BeTrue(); // APM_TRACING_SAMPLE_RULES

            request.Client.State.ConfigStates.Should().ContainSingle(f => f.Id == fileId)
               .Subject.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);

            var log = await logEntryWatcher.WaitForLogEntries([DiagnosticLog]);

            using (var context = new AssertionScope())
            {
                for (int i = 0; i < log.Length; i++)
                {
                    context.AddReportable($"log {i}", log[i]);
                }

                var diagnosticLog = log.First(l => l.Contains(DiagnosticLog));

                var match = Regex.Match(diagnosticLog, diagnosticLogRegex);

                match.Success.Should().BeTrue();

                var json = JObject.Parse(match.Groups["diagnosticLog"].Value);

                static string FlattenJsonArray(JToken json)
                {
                    if (json is JArray array)
                    {
                        return string.Join(";", array);
                    }

                    return string.Empty;
                }

                // json["runtime_metrics_enabled"]?.Value<bool>().Should().Be(expectedConfig.RuntimeMetricsEnabled);
                // json["debug"]?.Value<bool>().Should().Be(expectedConfig.DebugLogsEnabled);
                json["log_injection_enabled"]?.Value<bool>().Should().Be(expectedConfig.LogInjectionEnabled);
                json["sample_rate"]?.Value<double?>().Should().Be(expectedConfig.TraceSampleRate);
                JsonConfigurationSource.JTokenToString(json["sampling_rules"]).Should().Be(expectedConfig.TraceSamplingRules);
                // json["span_sampling_rules"]?.Value<string>().Should().Be(expectedConfig.SpanSamplingRules);
                // json["data_streams_enabled"]?.Value<bool>().Should().Be(expectedConfig.DataStreamsEnabled);
                FlattenJsonArray(json["header_tags"]).Should().Be(expectedConfig.TraceHeaderTags ?? string.Empty);
                // FlattenJsonArray(json["service_mapping"]).Should().Be(expectedConfig.ServiceNameMapping ?? string.Empty);
                json["tags"]?.ToString(Formatting.None).Should().Be(expectedConfig.GlobalTags ?? "[]");
            }

            WaitForTelemetry(agent);

            AssertConfigurationChanged(agent.Telemetry, config);
        }

        private bool WaitForTelemetry(MockTracerAgent agent)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);

            while (DateTime.UtcNow < deadline)
            {
                foreach (var item in agent.Telemetry)
                {
                    if (ExtractConfiguration((TelemetryData)item).Any(c => c.Origin == "remote_config"))
                    {
                        return true;
                    }
                }

                Thread.Sleep(500);
            }

            return false;
        }

        private void AssertConfigurationChanged(ConcurrentStack<object> events, Config config)
        {
            var expectedKeys = new List<(string Key, object Value)>
            {
                (ConfigurationKeys.TraceEnabled, config.TraceEnabled),
                // (ConfigurationKeys.RuntimeMetricsEnabled, config.RuntimeMetricsEnabled),
                // (ConfigurationKeys.DebugEnabled, config.DebugLogsEnabled),
                (ConfigurationKeys.LogsInjectionEnabled, config.LogInjectionEnabled),
                (ConfigurationKeys.GlobalSamplingRate, config.TraceSampleRate),
                (ConfigurationKeys.CustomSamplingRules, config.TraceSamplingRules == null ? null : JToken.Parse(config.TraceSamplingRules).ToString(Formatting.None)),
                // (ConfigurationKeys.SpanSamplingRules, config.SpanSamplingRules),
                // (ConfigurationKeys.DataStreamsMonitoring.Enabled, config.DataStreamsEnabled),
                (ConfigurationKeys.HeaderTags, config.TraceHeaderTags == null ? null : JToken.Parse(config.TraceHeaderTags).ToString()),
                // (ConfigurationKeys.ServiceNameMappings, config.ServiceNameMapping == null ? string.Empty : JToken.Parse(config.ServiceNameMapping).ToString())
                (ConfigurationKeys.GlobalTags, config.GlobalTags == null ? null : JToken.Parse(config.GlobalTags).ToString())
            };

            var expectedCount = expectedKeys.Count(k => k.Value is not null);

            var latestConfig = new Dictionary<string, object>();

            var now = DateTime.UtcNow;

            while (latestConfig.Count < expectedCount)
            {
                while (events.TryPop(out var obj))
                {
                    var wrapper = (TelemetryData)obj;

                    if (!wrapper.IsRequestType(TelemetryRequestTypes.AppClientConfigurationChanged))
                    {
                        continue;
                    }

                    foreach (var key in ExtractConfiguration(wrapper))
                    {
                        if (key.Origin == "remote_config")
                        {
                            key.Error.Should().BeNull();
                            latestConfig[key.Name] = key.Value;
                        }
                    }
                }

                if ((DateTime.UtcNow - now).TotalSeconds > 20)
                {
                    break;
                }

                if (latestConfig.Count < expectedCount)
                {
                    Thread.Sleep(500);
                }
            }

            using var context = new AssertionScope();
            context.AddReportable("configuration", string.Join("; ", latestConfig));

            foreach (var (key, value) in expectedKeys)
            {
                if (value == null)
                {
                    latestConfig.Should().NotContainKey(key);
                }
                else
                {
                    latestConfig.Should().Contain(key, value);
                }
            }

            latestConfig.Should().HaveCount(expectedCount);
        }

        private class PlainJsonStringConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return reader.Value;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteRawValue((string)value);
            }
        }

        private record Config
        {
            [JsonProperty("tracing_enabled")]
            public bool TraceEnabled { get; init; }

            // [JsonProperty("runtime_metrics_enabled")]
            // public bool RuntimeMetricsEnabled { get; init; }

            // [JsonProperty("tracing_debug")]
            // public bool DebugLogsEnabled { get; init; }

            [JsonProperty("log_injection_enabled")]
            public bool LogInjectionEnabled { get; init; }

            [JsonProperty("tracing_sampling_rate")]
            public double? TraceSampleRate { get; init; }

            [JsonProperty("tracing_sampling_rules")]
            [JsonConverter(typeof(PlainJsonStringConverter))]
            public string TraceSamplingRules { get; init; }

            // [JsonProperty("span_sampling_rules")]
            // public string SpanSamplingRules { get; init; }

            // [JsonProperty("data_streams_enabled")]
            // public bool DataStreamsEnabled { get; init; }

            [JsonProperty("tracing_header_tags")]
            [JsonConverter(typeof(PlainJsonStringConverter))]
            public string TraceHeaderTags { get; init; }

            [JsonProperty("tracing_tags")]
            [JsonConverter(typeof(PlainJsonStringConverter))]
            public string GlobalTags { get; init; }

            // [JsonProperty("tracing_service_mapping")]
            // [JsonConverter(typeof(PlainJsonStringConverter))]
            // public string ServiceNameMapping { get; init; }
        }
    }
}
