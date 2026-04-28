// <copyright file="OpenTelemetrySdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
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
        private readonly Regex _exceptionStacktraceOtlpRegex = new(@"string_value"": ""System.ArgumentException: Example argument exception.*""");
        private readonly Regex _exceptionStacktraceOtlpJsonRegex = new(@"stringValue"": ""System.ArgumentException: Example argument exception.*""");
        private readonly OtlpTestAgentSession _otlpSession = new(); // Do not decorate this class with IAsyncLifetime because it is not used in every test case.

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
                yield return [packageVersion[0], "false", "true", "grpc", false];
                yield return [packageVersion[0], "true", "false", "grpc", false];
                yield return [packageVersion[0], "true", "false", "grpc", true];
                yield return [packageVersion[0], "false", "true", "http/protobuf", false];
                yield return [packageVersion[0], "true", "false", "http/protobuf", false];
                yield return [packageVersion[0], "true", "false", "http/protobuf", true];
            }
        }

        public static IEnumerable<object[]> GetOtlpTracesTestData()
        {
            foreach (var packageVersion in PackageVersions.OpenTelemetry)
            {
                // Reduce CI flake by only testing the Datadog SDK. We can test the OTel SDK manually if needed.
                // yield return [packageVersion[0], "false", "true", "http/protobuf", false, false];
                yield return [packageVersion[0], "true", "false", "http/json", false, false];
                yield return [packageVersion[0], "true", "false", "http/json", true, false];
                yield return [packageVersion[0], "true", "false", "http/protobuf", false, false];
                yield return [packageVersion[0], "true", "false", "http/protobuf", true, false];
                yield return [packageVersion[0], "true", "false", "http/json", false, true];
                yield return [packageVersion[0], "true", "false", "http/json", true, true];
                yield return [packageVersion[0], "true", "false", "http/protobuf", false, true];
                yield return [packageVersion[0], "true", "false", "http/protobuf", true, true];
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsOpenTelemetry(metadataSchemaVersion, Resources, ExcludeTags);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(GetData))]
        public async Task SubmitsTraces(string packageVersion)
        {
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

        /// <summary>
        /// Validates that CallTarget-based Activity interception produces spans nearly identical
        /// to the managed ActivityListener approach. Uses a dedicated <c>.Interception</c> snapshot
        /// because of one outstanding gap: when an in-process child is started via an explicit
        /// <c>ActivityContext</c> parent (e.g. <c>StartActiveSpan(name, kind, parentTelemetrySpan)</c>),
        /// the OTel SDK does not set <c>Activity.Parent</c> on the child, so the interception path
        /// can't find the parent <see cref="Scope"/> and treats it as a remote parent. The child
        /// span ends up as a local trace root (extra <c>runtime-id</c> tag and Metrics block) instead
        /// of being attached to the parent's <c>TraceContext</c>. This is the same parentage class
        /// as the W3C-only-parent gap and is tracked alongside it.
        /// </summary>
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(GetData))]
        public async Task SubmitsTracesWithInterception(string packageVersion)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ACTIVITY_INTERCEPTION_ENABLED", "true");

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

                var filename = nameof(OpenTelemetrySdkTests) + ".Interception" + GetSuffix(packageVersion);

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
        public async Task IntegrationDisabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "false");
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

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(GetOtlpTracesTestData))]
        public async Task SubmitsOtlpTraces(string packageVersion, string datadogTracesEnabled, string otelTracesEnabled, string protocol, bool useAgentHostBackup, bool openTelemetrySemanticsEnabled)
        {
            SetServiceVersion("1.0.x"); // We need this to be consistent with the in-code 1.0.x version set in the OTel SDK builder
            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            var parsedVersion = Version.Parse(!string.IsNullOrEmpty(packageVersion) ? packageVersion : "1.13.1");
            var runtimeMajor = Environment.Version.Major;
            var isJson = protocol == "http/json" && datadogTracesEnabled.Equals("true");

            var snapshotName = otelTracesEnabled switch
            {
                "true" when parsedVersion >= new Version("1.15.0") => "1_15_0",
                "true" when parsedVersion >= new Version("1.5.1") => "1_5_1",
                "true" when parsedVersion >= new Version("1.3.2") => "1_3_2",
                "true" when parsedVersion <= new Version("1.0.1") => throw new SkipException($"Skipping test due to unrelated issue with OTel SDK version 1.0.1"),
                _ => string.Empty
            };

            snapshotName = otelTracesEnabled.Equals("true") ? $"_OTELv{snapshotName}" : $"{snapshotName}_DD{(openTelemetrySemanticsEnabled ? "_OtelSemantics" : string.Empty)}";

            // Establishes this token as a real ddapm test-agent session, so that
            // /test/session/traces only ever returns requests sent after this point.
            await _otlpSession.StartSessionAsync();

            // This is the key configuration that is set differently from previous test cases:
            // OTEL_TRACES_EXPORTER=otlp enables the DD SDK to emit traces (and trace stats) via OTLP
            SetEnvironmentVariable("OTEL_TRACES_EXPORTER", datadogTracesEnabled == "true" ? "otlp" : "none");

            SetEnvironmentVariable("DD_TRACE_DEBUG", "true");

            SetEnvironmentVariable("DD_ENV", string.Empty);
            SetEnvironmentVariable("DD_SERVICE", string.Empty);

            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENABLED", otelTracesEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", $"X-Datadog-Test-Session-Token={_otlpSession.SessionToken}"); // Isolates OTLP to this test
            if (useAgentHostBackup)
            {
                SetEnvironmentVariable("DD_AGENT_HOST", _otlpSession.TestAgentHost);
            }
            else
            {
                SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", _otlpSession.GetExporterEndpoint(protocol));
            }

            var applicationStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
            using var agent = EnvironmentHelper.GetMockAgent();
            // When DD_AGENT_HOST=test-agent is set above, it also redirects the APM trace agent
            // URL via the DD_TRACE_AGENT_HOSTNAME alias (the primary key wins). That points APM
            // traces at test-agent:<mock-agent-port>, which does not exist, so AgentWriter
            // retries fill the tracer's shutdown window and can starve the DirectLogSubmission
            // final flush. Pin the APM URL back to the in-process MockAgent.
            if (useAgentHostBackup && agent is MockTracerAgent.TcpUdpAgent tcpAgent)
            {
                SetEnvironmentVariable("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{tcpAgent.Port}");
            }

            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion ?? "1.13.1"))
            {
                // The sample exports traces during shutdown, so there can be a brief delay
                // between process exit and the data appearing in the test-agent. Poll with
                // retries to avoid a race, matching the pattern used by SubmitsOtlpMetrics
                // and SubmitsOtlpLogs.
                var tracesRequests = await _otlpSession.WaitForTracesAsync();

                tracesRequests.Should().NotBeNullOrEmpty();

                // Normalize the data in resource attributes and spans
                var names = OtlpFieldNames.For(isJson);
                OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
                OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, applicationStartTimeUnixNano);

                // For the Datadog SDK, perform more sanitization
                string finalJson;
                if (datadogTracesEnabled.Equals("true"))
                {
                    finalJson = OtlpSnapshotHelper.MergeDatadogRequests(tracesRequests, names)
                                                  .ToString(Formatting.Indented);
                }
                else
                {
                    OtlpSnapshotHelper.SortSpansPerScope(tracesRequests, names);
                    finalJson = tracesRequests.ToString(Formatting.Indented);
                }

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddRegexScrubber(_exceptionStacktraceOtlpRegex, @"string_value"": ""System.ArgumentException: Example argument exception""");
                settings.AddRegexScrubber(_exceptionStacktraceOtlpJsonRegex, @"stringValue"": ""System.ArgumentException: Example argument exception""");

                // Add scrubbers only for http/protobuf
                if (protocol == "http/protobuf")
                {
                    OtlpSnapshotHelper.AddProtobufToJsonScrubbers(settings);
                }

                var fileName = $"{nameof(OpenTelemetrySdkTests)}.SubmitsOtlpTraces{snapshotName}";

                await Verifier.Verify(finalJson, settings)
                              .UseFileName(fileName)
                              .DisableRequireUniquePrefix();
            }
        }

#if NET6_0_OR_GREATER
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(GetOtlpTestData))]
        public async Task SubmitsOtlpMetrics(string packageVersion, string datadogMetricsEnabled, string otelMetricsEnabled, string protocol, bool useAgentHostBackup)
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

            // Establishes this token as a real ddapm test-agent session, so that
            // /test/session/traces only ever returns requests sent after this point.
            await _otlpSession.StartSessionAsync();

            SetEnvironmentVariable("DD_ENV", string.Empty);
            SetEnvironmentVariable("DD_SERVICE", string.Empty);
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "false");
            SetEnvironmentVariable("DD_METRICS_OTEL_METER_NAMES", "OpenTelemetryMetricsMeter");
            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", datadogMetricsEnabled);
            SetEnvironmentVariable("OTEL_METRICS_EXPORTER_ENABLED", otelMetricsEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            // 60s so only the shutdown flush fires; periodic exports of observable instruments produce duplicate batches that break snapshot comparison
            SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "60000");

            if (useAgentHostBackup)
            {
                SetEnvironmentVariable("DD_AGENT_HOST", _otlpSession.TestAgentHost);
            }
            else
            {
                SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", _otlpSession.GetExporterEndpoint(protocol));
            }

            // Up until Sdk version 1.6.0 Otel didn't support reading from the env var
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", runtimeMajor >= 9 ? "delta" : "cumulative");

            using var agent = EnvironmentHelper.GetMockAgent();
            // See comment in SubmitsOtlpTraces. DD_AGENT_HOST=test-agent also redirects the APM
            // trace agent URL; pin it back to the in-process MockAgent.
            if (useAgentHostBackup && agent is MockTracerAgent.TcpUdpAgent tcpAgent)
            {
                SetEnvironmentVariable("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{tcpAgent.Port}");
            }

            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion ?? "1.13.1"))
            {
                var metricsData = await _otlpSession.WaitForMetricsAsync();
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

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsOtlpRuntimeMetrics()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            // Establishes this token as a real ddapm test-agent session, so that
            // /test/session/traces only ever returns requests sent after this point.
            await _otlpSession.StartSessionAsync();

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "true");
            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", "true");
            SetEnvironmentVariable("DD_METRICS_OTEL_METER_NAMES", "NoneExistingMeter");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", _otlpSession.GetExporterEndpoint("http/protobuf"));
            SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "60000");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", "delta");

            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            using (await RunSampleAndWaitForExit(agent))
            {
                var metricsData = await _otlpSession.WaitForMetricsAsync();
                metricsData.Should().NotBeNullOrEmpty();

                // Deduplicate metrics across multiple export intervals, keeping one per metric name
                var dedupedMetrics = new JArray(
                    metricsData
                        .SelectTokens("$..scope_metrics[*].metrics[*]")
                        .GroupBy(m => m["name"]?.ToString())
                        .Select(g => g.First())
                        .OrderBy(m => m["name"]?.ToString()));

                // Collapse all exports into a single resource_metrics structure for snapshot comparison
                var collapsed = metricsData[0]!.DeepClone();
                ((JArray)collapsed.SelectToken("$.resource_metrics")!).RemoveAll();
                var firstExport = metricsData.SelectToken("$[0].resource_metrics[0]")!.DeepClone();
                firstExport.SelectToken("$.scope_metrics[0].metrics")!.Replace(dedupedMetrics);
                ((JArray)collapsed["resource_metrics"]!).Add(firstExport);

                // Clear data_points for each metric to ensure consistency between runs
                foreach (var section in collapsed.SelectTokens("$..metrics[*].*"))
                {
                    if (section is JObject obj && obj["data_points"] is JArray)
                    {
                        obj["data_points"] = new JArray();
                    }
                }

                // Replace resource attribute values with placeholders to avoid volatile data
                foreach (var attribute in collapsed.SelectTokens("$..resource.attributes[*]"))
                {
                    var key = attribute["key"]?.ToString();
                    if (key is not null)
                    {
                        attribute["value"] = JToken.FromObject(new { string_value = $"<{key}>" });
                    }
                }

                var formattedJson = new JArray(collapsed).ToString(Formatting.Indented);
                var settings = VerifyHelper.GetSpanVerifierSettings();
                // Single snapshot for all TFMs: the OTel SDK sample transitively references
                // System.Diagnostics.DiagnosticSource 9.0+, which gets loaded into the host process
                // even on .NET 6. Reflection in MeterObservableUpDownCounterReflection then resolves
                // CreateObservableUpDownCounter on every TFM, so the polyfill produces identical wire
                // output across .NET 6/7/8/9/10+.
                await Verifier.Verify(formattedJson, settings)
                              .UseFileName($"{nameof(OpenTelemetrySdkTests)}.{nameof(SubmitsOtlpRuntimeMetrics)}")
                              .DisableRequireUniquePrefix();

                agent.StatsdRequests.Should().BeEmpty(
                    "StatsD runtime metrics should be disabled when OTLP runtime metrics are active");
            }
        }
#endif

#if NETCOREAPP3_1_OR_GREATER
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(GetOtlpTestData))]
        public async Task SubmitsOtlpLogs(string packageVersion, string datadogLogsEnabled, string otelLogsEnabled, string protocol, bool useAgentHostBackup)
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

            // Establishes this token as a real ddapm test-agent session, so that
            // /test/session/traces only ever returns requests sent after this point.
            await _otlpSession.StartSessionAsync();

            SetEnvironmentVariable("DD_ENV", "testing");
            SetEnvironmentVariable("DD_SERVICE", "OtlpLogsService");
            SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", "service.name=OtlpLogsService,deployment.environment=testing");
            SetEnvironmentVariable("DD_LOGS_OTEL_ENABLED", datadogLogsEnabled);
            SetEnvironmentVariable("OTEL_LOGS_EXPORTER_ENABLED", otelLogsEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            // Short delay gives the OTel SDK multiple periodic exports before LoggerProviderSdk.Dispose() hits its 5s shutdown timeout.
            // This is especially important for gRPC, where the first export warms the HTTP/2 connection.
            SetEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "100");
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL", "Verbose");

            if (useAgentHostBackup)
            {
                SetEnvironmentVariable("DD_AGENT_HOST", _otlpSession.TestAgentHost);
            }
            else
            {
                SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", _otlpSession.GetExporterEndpoint(protocol));
            }

            var startTimeNanoseconds = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            using var agent = EnvironmentHelper.GetMockAgent();
            // See comment in SubmitsOtlpTraces. DD_AGENT_HOST=test-agent also redirects the APM
            // trace agent URL; pin it back to the in-process MockAgent so AgentWriter retries
            // don't starve the DirectLogSubmission final flush during shutdown.
            if (useAgentHostBackup && agent is MockTracerAgent.TcpUdpAgent tcpAgent)
            {
                SetEnvironmentVariable("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{tcpAgent.Port}");
            }

            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion ?? "1.13.1"))
            {
                var endTimeNanoseconds = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

                var logsData = await _otlpSession.WaitForLogsAsync();
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

                    // This is sometimes added, sometimes not, so just remove it
                    if (logRecord is JObject jObj)
                    {
                        jObj.Remove("flags");
                    }
                }

                // The OTel SDK can emit multiple OTLP batches during the sample run (periodic export(s) plus shutdown flush),
                // and the test-agent can return them as separate top-level elements in any order. Collapse everything
                // into a single batch, then sort scope_logs by scope.name and log_records by body.string_value so the
                // snapshot is stable regardless of batch split, arrival order, or the exporter's internal scope grouping.
                if (logsData is JArray logsArray && logsArray.Count > 1)
                {
                    // Add all the subsequent logs to the first array
                    var mergedScopeLogs = (JArray)logsArray[0]["resource_logs"][0]["scope_logs"];
                    for (var i = 1; i < logsArray.Count; i++)
                    {
                        foreach (var scopeLog in (JArray)logsArray[i]["resource_logs"][0]["scope_logs"])
                        {
                            var scopeName = scopeLog["scope"]?["name"]?.ToString();
                            var existing = mergedScopeLogs.FirstOrDefault(s => s["scope"]?["name"]?.ToString() == scopeName);
                            if (existing != null && scopeLog["log_records"] is JArray incoming)
                            {
                                var existingRecords = (JArray)existing["log_records"];
                                foreach (var record in incoming)
                                {
                                    existingRecords.Add(record);
                                }
                            }
                            else
                            {
                                mergedScopeLogs.Add(scopeLog);
                            }
                        }
                    }

                    while (logsArray.Count > 1)
                    {
                        logsArray.RemoveAt(1);
                    }
                }

                // Fix the ordering to ensure it's deterministic
                foreach (var resourceLog in logsData.SelectTokens("$[0].resource_logs[*]"))
                {
                    if (resourceLog["scope_logs"] is JArray scopeLogsArray)
                    {
                        foreach (var scopeLog in scopeLogsArray)
                        {
                            if (scopeLog["log_records"] is JArray recordsArray)
                            {
                                scopeLog["log_records"] = new JArray(recordsArray.OrderBy(r => r["body"]?["string_value"]?.ToString()));
                            }
                        }

                        resourceLog["scope_logs"] = new JArray(scopeLogsArray.OrderBy(s => s["scope"]?["name"]?.ToString()));
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
