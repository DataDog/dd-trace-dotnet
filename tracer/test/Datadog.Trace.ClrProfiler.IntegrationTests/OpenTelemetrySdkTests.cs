// <copyright file="OpenTelemetrySdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class OpenTelemetrySdkTests : TracingIntegrationTest
    {
        private static readonly string CustomServiceName = "CustomServiceName";
        private static readonly HashSet<string> Resources = new HashSet<string>
        {
            "service.instance.id",
            "service.name",
            "service.version",
        };

        private static readonly HashSet<string> ExcludeTags = new HashSet<string>
        {
            "events",
            "attribute-string",
            "attribute-int",
            "attribute-bool",
            "attribute-double",
            "attribute-stringArray.0",
            "attribute-stringArray.1",
            "attribute-stringArray.2",
            "attribute-stringArrayEmpty",
            "attribute-intArray.0",
            "attribute-intArray.1",
            "attribute-intArray.2",
            "attribute-intArrayEmpty",
            "attribute-boolArray.0",
            "attribute-boolArray.1",
            "attribute-boolArray.2",
            "attribute-boolArrayEmpty",
            "attribute-doubleArray.0",
            "attribute-doubleArray.1",
            "attribute-doubleArray.2",
            "attribute-doubleArrayEmpty",
            "telemetry.sdk.name",
            "telemetry.sdk.language",
            "telemetry.sdk.version",
            "http.status_code",
            // excluding all OperationName mapping tags
            "http.request.method",
            "db.system",
            "messaging.system",
            "messaging.operation",
            "rpc.system",
            "rpc.service",
            "faas.invoked_provider",
            "faas.invoked_name",
            "faas.trigger",
            "graphql.operation.type",
            "network.protocol.name"
        };

        private readonly Regex _versionRegex = new(@"telemetry.sdk.version: (0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)");
        private readonly Regex _timeUnixNanoRegex = new(@"time_unix_nano"":([0-9]{10}[0-9]+)");
        private readonly Regex _exceptionStacktraceRegex = new(@"exception.stacktrace"":""System.ArgumentException: Example argument exception.*"",""");

        public OpenTelemetrySdkTests(ITestOutputHelper output)
            : base("OpenTelemetrySdk", output)
        {
            SetServiceName(CustomServiceName);
            SetServiceVersion(string.Empty);
        }

        public static IEnumerable<object[]> GetData() => PackageVersions.OpenTelemetry;

        public static IEnumerable<object[]> GetOtlpTestData()
        {
            foreach (var packageVersion in PackageVersions.OpenTelemetry)
            {
                yield return [packageVersion[0], "false", "true", "grpc"];
                yield return [packageVersion[0], "true", "false", "grpc"];
                yield return [packageVersion[0], "false", "true", "http/protobuf"];
                yield return [packageVersion[0], "true", "false", "http/protobuf"];
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsOpenTelemetry(metadataSchemaVersion, Resources, ExcludeTags);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(GetData))]
        public async Task SubmitsTraces(string packageVersion)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                const int expectedSpanCount = 38;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var otelSpans = spans.Where(s => s.Service == "MyServiceName");
                var activitySourceSpans = spans.Where(s => s.Service == CustomServiceName);

                otelSpans.Count().Should().Be(expectedSpanCount - 3); // there is another span w/ service == ServiceNameOverride
                activitySourceSpans.Count().Should().Be(2);

                ValidateIntegrationSpans(otelSpans, metadataSchemaVersion: "v0", expectedServiceName: "MyServiceName", isExternalSpan: false);
                ValidateIntegrationSpans(activitySourceSpans, metadataSchemaVersion: "v0", expectedServiceName: CustomServiceName, isExternalSpan: false);

                // there's a bug in < 1.2.0 where they get the span parenting wrong
                // so use a separate snapshot
                var filename = nameof(OpenTelemetrySdkTests) + GetSuffix(packageVersion);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                var traceStatePRegex = new Regex("p:[0-9a-fA-F]+");
                var traceIdRegexHigh = new Regex("TraceIdLow: [0-9]+");
                var traceIdRegexLow = new Regex("TraceIdHigh: [0-9]+");
                settings.AddRegexScrubber(traceStatePRegex, "p:TsParentId");
                settings.AddRegexScrubber(traceIdRegexHigh, "TraceIdHigh: LinkIdHigh");
                settings.AddRegexScrubber(traceIdRegexLow, "TraceIdLow: LinkIdLow");
                settings.AddRegexScrubber(_versionRegex, "telemetry.sdk.version: sdk-version");
                settings.AddRegexScrubber(_timeUnixNanoRegex, @"time_unix_nano"":<DateTimeOffset.Now>");
                settings.AddRegexScrubber(_exceptionStacktraceRegex, @"exception.stacktrace"":""System.ArgumentException: Example argument exception"",""");
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(filename)
                                  .DisableRequireUniquePrefix();

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.OpenTelemetry);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
        public async Task SubmitsTracesWithActivitySource(string packageVersion)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");
            SetEnvironmentVariable("ADD_ADDITIONAL_ACTIVITY_SOURCE", "true");

            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                const int expectedSpanCount = 38;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                using var s = new AssertionScope();
                var otelSpans = spans.Where(s => s.Service == "MyServiceName");

                otelSpans.Count().Should().Be(expectedSpanCount - 2); // there is another span w/ service == ServiceNameOverride

                ValidateIntegrationSpans(otelSpans, metadataSchemaVersion: "v0", expectedServiceName: "MyServiceName", isExternalSpan: false);

                // there's a bug in < 1.2.0 where they get the span parenting wrong
                // so use a separate snapshot
                var filename = nameof(OpenTelemetrySdkTests) + "WithActivitySource" + GetSuffix(packageVersion);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddRegexScrubber(_versionRegex, "telemetry.sdk.version: sdk-version");
                var traceStatePRegex = new Regex("p:[0-9a-fA-F]+");
                var traceIdRegexHigh = new Regex("TraceIdLow: [0-9]+");
                var traceIdRegexLow = new Regex("TraceIdHigh: [0-9]+");
                settings.AddRegexScrubber(traceStatePRegex, "p:TsParentId");
                settings.AddRegexScrubber(traceIdRegexHigh, "TraceIdHigh: LinkIdHigh");
                settings.AddRegexScrubber(traceIdRegexLow, "TraceIdLow: LinkIdLow");
                settings.AddRegexScrubber(_timeUnixNanoRegex, @"time_unix_nano"":<DateTimeOffset.Now>");
                settings.AddRegexScrubber(_exceptionStacktraceRegex, @"exception.stacktrace"":""System.ArgumentException: Example argument exception"",""");
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(filename)
                                  .DisableRequireUniquePrefix();

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.OpenTelemetry);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
        public async Task IntegrationDisabled(string packageVersion)
        {
            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.Spans;

                using var s = new AssertionScope();
                spans.Should().BeEmpty();
                await telemetry.AssertIntegrationDisabledAsync(IntegrationId.OpenTelemetry);
            }
        }

#if NET6_0_OR_GREATER
        [SkippableTheory]
        [Flaky("New test agent seems to not always be ready", maxRetries: 3)]
        [Trait("Category", "EndToEnd")]
        [Trait("RequiresDockerDependency", "true")]
        [Trait("DockerGroup", "1")]
        [MemberData(nameof(GetOtlpTestData))]
        public async Task SubmitsOtlpMetrics(string packageVersion, string datadogMetricsEnabled, string otelMetricsEnabled, string protocol)
        {
            var parsedVersion = Version.Parse(!string.IsNullOrEmpty(packageVersion) ? packageVersion : "1.13.1");
            var runtimeMajor = Environment.Version.Major;

            var snapshotName = runtimeMajor switch
            {
                6 when parsedVersion >= new Version("1.3.2") && parsedVersion < new Version("1.5.0") => ".NET_6",
                7 or 8 when parsedVersion >= new Version("1.5.1") && parsedVersion < new Version("1.10.0") => ".NET_7_8",
                >= 9 when parsedVersion >= new Version("1.10.0") => string.Empty,
                _ => throw new SkipException($"Skipping test due to irrelevant runtime and OTel versions mix: .NET {runtimeMajor} & Otel v{parsedVersion}")
            };

            snapshotName = otelMetricsEnabled.Equals("true") ? $"{snapshotName}_OTEL" : $"{snapshotName}_DD";

            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";
            var otlpPort = protocol == "grpc" ? 4317 : 4318;

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                await httpClient.GetAsync($"http://{testAgentHost}:4318/test/session/clear");
            }

            SetEnvironmentVariable("DD_ENV", string.Empty);
            SetEnvironmentVariable("DD_SERVICE", string.Empty);
            SetEnvironmentVariable("DD_METRICS_OTEL_METER_NAMES", "OpenTelemetryMetricsMeter");
            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", datadogMetricsEnabled);
            SetEnvironmentVariable("OTEL_METRICS_EXPORTER_ENABLED", otelMetricsEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:{otlpPort}");
            SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");

            // Up until Sdk version 1.6.0 Otel didn't support reading from the env var
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", runtimeMajor >= 9 ? "delta" : "cumulative");

            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion ?? "1.13.1"))
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var metricsResponse = await httpClient.GetAsync($"http://{testAgentHost}:4318/test/session/metrics");
                metricsResponse.EnsureSuccessStatusCode();

                var metricsJson = await metricsResponse.Content.ReadAsStringAsync();
                var metricsData = JToken.Parse(metricsJson);

                metricsData.Should().NotBeNullOrEmpty();

                foreach (var attribute in metricsData.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
                {
                    attribute["value"]!["string_value"] = "sdk-version";
                }

                foreach (var attribute in metricsData.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.name')]"))
                {
                    attribute["value"]!["string_value"] = "sdk-name";
                }

                foreach (var dataPoint in metricsData.SelectTokens("$..data_points[*]"))
                {
                    dataPoint["start_time_unix_nano"] = "0";
                    dataPoint["time_unix_nano"] = "0";
                }

                foreach (var scopeMetric in metricsData.SelectTokens("$..scope_metrics[*]"))
                {
                    if (scopeMetric["metrics"] is JArray metricsArray)
                    {
                        var sorted = new JArray(metricsArray.OrderBy(m => m["name"]?.ToString()));
                        scopeMetric["metrics"] = sorted;
                    }
                }

                var formattedJson = metricsData.ToString(Formatting.Indented);
                var settings = VerifyHelper.GetSpanVerifierSettings();
                var suffix = GetSuffix(packageVersion);
                var fileName = $"{nameof(OpenTelemetrySdkTests)}.SubmitsOtlpMetrics{suffix}{snapshotName}";

                await Verifier.Verify(formattedJson, settings)
                              .UseFileName(fileName)
                              .DisableRequireUniquePrefix();
            }
        }
#endif

#if NETCOREAPP3_1_OR_GREATER
        [SkippableTheory]
        [Flaky("New test agent seems to not always be ready", maxRetries: 3)]
        [Trait("Category", "EndToEnd")]
        [Trait("RequiresDockerDependency", "true")]
        [Trait("DockerGroup", "1")]
        [MemberData(nameof(GetOtlpTestData))]
        public async Task SubmitsOtlpLogs(string packageVersion, string datadogLogsEnabled, string otelLogsEnabled, string protocol)
        {
            var parsedVersion = Version.Parse(!string.IsNullOrEmpty(packageVersion) ? packageVersion : "1.13.1");
            var runtimeMajor = Environment.Version.Major;

            _ = runtimeMajor switch
            {
                >= 8 when parsedVersion >= new Version("1.9.0") => string.Empty,
                6 or 7 when parsedVersion >= new Version("1.9.0") && otelLogsEnabled.Equals("true") && protocol.Equals("grpc") => throw new SkipException($"Unable to send insecure GRPC Logs using OpenTelemetry in .NET {runtimeMajor}."),
                6 or 7 when parsedVersion >= new Version("1.9.0") => string.Empty,
                _ => throw new SkipException($"Skipping test due to irrelevant runtime and OTel versions mix: .NET {runtimeMajor} & Otel v{parsedVersion}")
            };

            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";
            var otlpPort = protocol == "grpc" ? 4317 : 4318;

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                await httpClient.GetAsync($"http://{testAgentHost}:4318/test/session/clear");
            }

            SetEnvironmentVariable("DD_ENV", "testing");
            SetEnvironmentVariable("DD_SERVICE", "OtlpLogsService");
            SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", "service.name=OtlpLogsService,deployment.environment=testing");
            SetEnvironmentVariable("DD_LOGS_OTEL_ENABLED", datadogLogsEnabled);
            SetEnvironmentVariable("OTEL_LOGS_EXPORTER_ENABLED", otelLogsEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:{otlpPort}");
            SetEnvironmentVariable("OTEL_LOG_EXPORT_INTERVAL", "1000");
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL", "Verbose");

            var startTimeNanoseconds = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion ?? "1.13.1"))
            {
                var endTimeNanoseconds = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

                using var httpClient = new System.Net.Http.HttpClient();
                var logsResponse = await httpClient.GetAsync($"http://{testAgentHost}:4318/test/session/logs");
                logsResponse.EnsureSuccessStatusCode();

                var logsJson = await logsResponse.Content.ReadAsStringAsync();
                var logsData = JToken.Parse(logsJson);

                logsData.Should().NotBeNullOrEmpty();
                logsData.SelectTokens("$..log_records[*]").Should().AllSatisfy(logRecord =>
                {
                    var timeUnixNano = logRecord.Value<long>("time_unix_nano");
                    var observedTimeUnixNano = logRecord.Value<long>("observed_time_unix_nano");

                    timeUnixNano.Should().Be(observedTimeUnixNano);
                    timeUnixNano.Should().BeInRange(startTimeNanoseconds, endTimeNanoseconds);
                });

                foreach (var attribute in logsData.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
                {
                    attribute["value"]!["string_value"] = "sdk-version";
                }

                foreach (var attribute in logsData.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.name')]"))
                {
                    attribute["value"]!["string_value"] = "sdk-name";
                }

                foreach (var logRecord in logsData.SelectTokens("$..log_records[*]"))
                {
                    logRecord["time_unix_nano"] = "0";
                    logRecord["observed_time_unix_nano"] = "0";

                    if (logRecord["trace_id"] != null)
                    {
                        logRecord["trace_id"] = "normalized-trace-id";
                    }

                    if (logRecord["span_id"] != null)
                    {
                        logRecord["span_id"] = "normalized-span-id";
                    }
                }

                var formattedJson = logsData.ToString(Formatting.Indented);
                var settings = VerifyHelper.GetSpanVerifierSettings();
                var suffix = GetSuffix(packageVersion);
                var fileName = $"{nameof(OpenTelemetrySdkTests)}.SubmitsOtlpLogs{suffix}";

                await Verifier.Verify(formattedJson, settings)
                              .UseFileName(fileName)
                              .DisableRequireUniquePrefix();
            }
        }
#endif

        private static string GetSuffix(string packageVersion)
        {
            // The snapshots are only different in .NET Core 2.1 - .NET 5 with package version 1.0.1
#if !NET6_0_OR_GREATER
            if (!string.IsNullOrEmpty(packageVersion)
             && new Version(packageVersion) < new Version("1.2.0"))
            {
                return "_1_0";
            }
#endif

            // New tags added in v1.5.1
            if (!string.IsNullOrEmpty(packageVersion)
            && new Version(packageVersion) <= new Version("1.5.0"))
            {
                return "_up_to_1_5_0";
            }

            // v1.7.0 fixed StartRootSpan to not be a child of the active span
            if (!string.IsNullOrEmpty(packageVersion)
             && new Version(packageVersion) < new Version("1.7.0"))
            {
                return "_up_to_1_7_0";
            }

            return string.Empty;
        }
    }
}
