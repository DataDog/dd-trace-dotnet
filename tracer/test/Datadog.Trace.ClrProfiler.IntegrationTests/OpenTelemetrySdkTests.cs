// <copyright file="OpenTelemetrySdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private readonly Regex _traceIdRegex = new(@"^([a-fA-F0-9]{32})$");
        private readonly Regex _spanIdRegex = new(@"^([a-fA-F0-9]{16})$");

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
                // Reduce CI flake by only testing the Datadog SDK. We can test the OTel SDK manualy if needed.
                // yield return [packageVersion[0], "false", "true", "http/protobuf", false];
                yield return [packageVersion[0], "true", "false", "http/json", false];
                yield return [packageVersion[0], "true", "false", "http/json", true];
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

#if NET6_0_OR_GREATER
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(GetOtlpTracesTestData))]
        public async Task SubmitsOtlpTraces(string packageVersion, string datadogTracesEnabled, string otelTracesEnabled, string protocol, bool useAgentHostBackup)
        {
            SetServiceVersion("1.0.x"); // We need this to be consistent with the in-code 1.0.x version set in the OTel SDK builder

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

            snapshotName = otelTracesEnabled.Equals("true") ? $"_OTELv{snapshotName}" : $"{snapshotName}_DD_{protocol.Replace("/", "_")}";

            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";
            var otlpPort = protocol == "grpc" ? 4317 : 4318;

            await ClearTestAgentSession(testAgentHost);

            // This is the key configuration that is set differently from previous test cases:
            // OTEL_TRACES_EXPORTER=otlp enables the DD SDK to emit traces (and trace stats) via OTLP
            SetEnvironmentVariable("OTEL_TRACES_EXPORTER", datadogTracesEnabled == "true" ? "otlp" : "none");

            SetEnvironmentVariable("DD_TRACE_DEBUG", "true");

            SetEnvironmentVariable("DD_ENV", string.Empty);
            SetEnvironmentVariable("DD_SERVICE", string.Empty);

            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENABLED", otelTracesEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            if (useAgentHostBackup)
            {
                SetEnvironmentVariable("DD_AGENT_HOST", testAgentHost);
            }
            else
            {
                SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:{otlpPort}");
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
                using var httpClient = new System.Net.Http.HttpClient();
                var tracesResponse = await httpClient.GetAsync($"http://{testAgentHost}:4318/test/session/traces");
                tracesResponse.EnsureSuccessStatusCode();

                var tracesJson = await tracesResponse.Content.ReadAsStringAsync();
                var tracesRequests = JToken.Parse(tracesJson);

                tracesRequests.Should().NotBeNullOrEmpty();

                // Normalize the data in resource attributes and spans
                var resourceSpansKey = isJson ? "resourceSpans" : "resource_spans";
                var scopeSpansKey = isJson ? "scopeSpans" : "scope_spans";
                var stringValueKey = isJson ? "stringValue" : "string_value";
                var traceIdKey = isJson ? "traceId" : "trace_id";
                var spanIdKey = isJson ? "spanId" : "span_id";
                var parentSpanIdKey = isJson ? "parentSpanId" : "parent_span_id";
                var startTimeUnixNanoKey = isJson ? "startTimeUnixNano" : "start_time_unix_nano";
                var endTimeUnixNanoKey = isJson ? "endTimeUnixNano" : "end_time_unix_nano";
                var timeUnixNanoKey = isJson ? "timeUnixNano" : "time_unix_nano";

                foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
                {
                    attribute["value"]![stringValueKey] = "sdk-version";
                }

                foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.name')]"))
                {
                    attribute["value"]![stringValueKey] = "sdk-name";
                }

                foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'git.commit.sha')]"))
                {
                    attribute["value"]![stringValueKey] = "normalized-git-commit-sha";
                }

                foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
                {
                    static string ToHexString(byte[] bytes, int length)
                    {
                        bytes.Length.Should().Be(length);

                        var traceId = new byte[length * 2];
                        for (int i = 0; i < length; i++)
                        {
                            traceId[2 * i] = (byte)(bytes[i] >> 4);         // high 4 bits
                            traceId[(2 * i) + 1] = (byte)(bytes[i] & 0x0F); // low 4 bits
                        }

                        // Convert each nibble (0-15) to its hex character
                        var result = new char[length * 2];
                        for (int i = 0; i < length * 2; i++)
                        {
                            result[i] = (char)(traceId[i] < 10 ? '0' + traceId[i] : 'a' + traceId[i] - 10);
                        }

                        return new string(result);
                    }

                    static string ToTraceId(byte[] bytes) => ToHexString(bytes, 16);

                    static string ToSpanId(byte[] bytes) => ToHexString(bytes, 8);

                    // Parse unstable information from the span
                    string traceIdData = isJson ? span[traceIdKey].ToString()
                                                : ToTraceId(Convert.FromBase64String(span[traceIdKey].ToString()));
                    string spanIdData = isJson ? span[spanIdKey].ToString()
                                                : ToSpanId(Convert.FromBase64String(span[spanIdKey].ToString()));
                    var spanStartTimeUnixNano = long.Parse(span[startTimeUnixNanoKey].ToString());
                    var spanEndTimeUnixNano = long.Parse(span[endTimeUnixNanoKey].ToString());

                    // Add strong assertions on unstable span information
                    spanStartTimeUnixNano.Should().BeGreaterThanOrEqualTo(applicationStartTimeUnixNano);
                    spanEndTimeUnixNano.Should().BeGreaterThanOrEqualTo(spanStartTimeUnixNano);
                    traceIdData.Should().MatchRegex(_traceIdRegex);
                    spanIdData.Should().MatchRegex(_spanIdRegex);
                    if (span[parentSpanIdKey] != null)
                    {
                        string parentSpanIdData = isJson ? span[parentSpanIdKey]?.ToString()
                                                        : ToSpanId(Convert.FromBase64String(span[parentSpanIdKey].ToString()));
                        parentSpanIdData.Should().MatchRegex(_spanIdRegex);
                    }

                    // Normalize the unstable span information for our snapshots
                    span[startTimeUnixNanoKey] = "0";
                    span[endTimeUnixNanoKey] = "0";
                    span[traceIdKey] = "normalized-trace-id";
                    span[spanIdKey] = "normalized-span-id";
                    if (span[parentSpanIdKey] != null)
                    {
                        span[parentSpanIdKey] = "normalized-parent-span-id";
                    }
                }

                foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'otel.trace_id')]"))
                {
                    attribute["value"]![stringValueKey] = "normalized-otel-trace-id";
                }

                foreach (var link in tracesRequests.SelectTokens("$..links[*]"))
                {
                    if (isJson)
                    {
                        link[traceIdKey].ToString().Should().MatchRegex(_traceIdRegex);
                        link[spanIdKey].ToString().Should().MatchRegex(_spanIdRegex);
                    }
                    else
                    {
                        // We need to emit each byte as a character, so use ASCII encoding
                        // var decodedTraceId = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(link[traceIdKey].ToString()));
                        // var decodedSpanId = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(link[spanIdKey].ToString()));
                        // decodedTraceId.Should().MatchRegex(_traceIdRegex);
                        // decodedSpanId.Should().MatchRegex(_spanIdRegex);
                    }

                    link[traceIdKey] = "normalized-trace-id";
                    link[spanIdKey] = "normalized-span-id";
                }

                foreach (var @event in tracesRequests.SelectTokens("$..events[*]"))
                {
                    if (@event[timeUnixNanoKey] != null)
                    {
                        @event[timeUnixNanoKey] = "0";
                    }
                }

                // For the Datadog SDK, perform more sanitization
                string finalJson;
                if (datadogTracesEnabled.Equals("true"))
                {
                    // First, for the DD SDK, assert that the resource attributes for all requests are identical
                    // This is analogous to DD_SERVICE, DD_VERSION, DD_ENV, etc. that define
                    // metadata for the telemetry at an application and host level.
                    // This is different for OTel SDK application since the in-app code uses the SDK to create a
                    // 2nd, completely distinct, Traces SDK instance

                    JToken previousResourceAttributes = null;
                    foreach (var tracesRequest in tracesRequests)
                    {
                        tracesRequest[resourceSpansKey].Should().HaveCount(1);
                        var resourceAttributes = tracesRequest[resourceSpansKey][0]["resource"]["attributes"];

                        if (previousResourceAttributes == null)
                        {
                            previousResourceAttributes = resourceAttributes;
                        }
                        else
                        {
                            JToken.DeepEquals(previousResourceAttributes, resourceAttributes).Should().BeTrue();
                            previousResourceAttributes = resourceAttributes;
                        }
                    }

                    // Next, assert that we only have a singular InstrumentationScope in each request.
                    // In OpenTelemetry, an InstrumentationScope is a way to group spans by the library that produced them.
                    // We should be respecting this for each library/ActivitySource, but right now the DD SDK doesn't
                    // keep track of that information, so consolidate them into one single, empty InstrumentationScope.
                    // TODO: Properly track spans per instrumentation scope.
                    JArray firstSpans = null;
                    foreach (var tracesRequest in tracesRequests)
                    {
                        tracesRequest[resourceSpansKey][0][scopeSpansKey].Should().HaveCount(1);
                        var spans = tracesRequest[resourceSpansKey][0][scopeSpansKey][0]["spans"] as JArray;

                        if (firstSpans == null)
                        {
                            firstSpans = spans;
                        }
                        else
                        {
                            foreach (var span in spans)
                            {
                                firstSpans.Add(span);
                            }
                        }
                    }

                    // Now re-order and trim down to one single request
                    // This means the output is not a true 1:1 mapping of the input spans, but it's good enough for now
                    // and will make the results stable.
                    // Also, sort the spans by name to stabilize
                    var sortedSpans = new JArray(firstSpans.OrderBy(s => s["name"]!.ToString()));
                    tracesRequests[0][resourceSpansKey][0][scopeSpansKey][0]["spans"] = sortedSpans;
                    finalJson = tracesRequests[0].ToString(Formatting.Indented);
                }
                else
                {
                    // Sort the spans by name to stabilize
                    foreach (var scopeSpan in tracesRequests.SelectTokens($"$..{scopeSpansKey}[*]"))
                    {
                        if (scopeSpan["spans"] is JArray spansArray)
                        {
                            var sorted = new JArray(spansArray.OrderBy(s => s["name"]?.ToString()));
                            scopeSpan["spans"] = sorted;
                        }
                    }

                    finalJson = tracesRequests.ToString(Formatting.Indented);
                }

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddRegexScrubber(_exceptionStacktraceOtlpRegex, @"string_value"": ""System.ArgumentException: Example argument exception""");
                settings.AddRegexScrubber(_exceptionStacktraceOtlpJsonRegex, @"stringValue"": ""System.ArgumentException: Example argument exception""");
                var fileName = $"{nameof(OpenTelemetrySdkTests)}.SubmitsOtlpTraces{snapshotName}";

                await Verifier.Verify(finalJson, settings)
                              .UseFileName(fileName)
                              .DisableRequireUniquePrefix();
            }
        }
#endif

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

            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";
            var otlpPort = protocol == "grpc" ? 4317 : 4318;

            await ClearTestAgentSession(testAgentHost);

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
                SetEnvironmentVariable("DD_AGENT_HOST", testAgentHost);
            }
            else
            {
                SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:{otlpPort}");
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
                var metricsData = await WaitForTestAgentData($"http://{testAgentHost}:4318/test/session/metrics");
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
            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";

            await ClearTestAgentSession(testAgentHost);

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "true");
            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", "true");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:4318");
            SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "60000");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", "delta");

            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            using (await RunSampleAndWaitForExit(agent, packageVersion: "1.13.1"))
            {
                var metricsData = await WaitForTestAgentData($"http://{testAgentHost}:4318/test/session/metrics");
                metricsData.Should().NotBeNullOrEmpty();

                var dedupedMetrics = new JArray(
                    metricsData
                        .SelectTokens("$..scope_metrics[*].metrics[*]")
                        .GroupBy(m => m["name"]?.ToString())
                        .Select(g => g.First())
                        .OrderBy(m => m["name"]?.ToString()));

                var collapsed = metricsData[0]!.DeepClone();
                ((JArray)collapsed.SelectToken("$.resource_metrics")!).RemoveAll();
                var firstExport = metricsData.SelectToken("$[0].resource_metrics[0]")!.DeepClone();
                firstExport.SelectToken("$.scope_metrics[0].metrics")!.Replace(dedupedMetrics);
                ((JArray)collapsed["resource_metrics"]!).Add(firstExport);

                foreach (var section in collapsed.SelectTokens("$..metrics[*].*"))
                {
                    if (section is JObject obj && obj["data_points"] is JArray)
                    {
                        obj["data_points"] = new JArray();
                    }
                }

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
                var tfm = Environment.Version.Major >= 9 ? "net9" : "net6";

                await Verifier.Verify(formattedJson, settings)
                              .UseFileName($"{nameof(OpenTelemetrySdkTests)}.SubmitsOtlpRuntimeMetrics_{tfm}")
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

            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";
            var otlpPort = protocol == "grpc" ? 4317 : 4318;

            await ClearTestAgentSession(testAgentHost);

            SetEnvironmentVariable("DD_ENV", "testing");
            SetEnvironmentVariable("DD_SERVICE", "OtlpLogsService");
            SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", "service.name=OtlpLogsService,deployment.environment=testing");
            SetEnvironmentVariable("DD_LOGS_OTEL_ENABLED", datadogLogsEnabled);
            SetEnvironmentVariable("OTEL_LOGS_EXPORTER_ENABLED", otelLogsEnabled);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            // Short delay gives the OTel SDK multiple periodic exports before LoggerProviderSdk.Dispose() hits its 5s shutdown timeout.
            // This is especially important for gRPC, where the first export warms the HTTP/2 connection.
            SetEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "500");
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL", "Verbose");

            if (useAgentHostBackup)
            {
                SetEnvironmentVariable("DD_AGENT_HOST", testAgentHost);
            }
            else
            {
                SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:{otlpPort}");
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

                var logsData = await WaitForTestAgentData($"http://{testAgentHost}:4318/test/session/logs");
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

        /// <summary>
        /// Clears the test-agent session, retrying if the agent is not yet ready.
        /// Ensures the OTLP HTTP endpoint is accepting connections before tests proceed.
        /// </summary>
        private static async Task ClearTestAgentSession(string testAgentHost, int maxRetries = 5, int delayMs = 1000)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://{testAgentHost}:4318/test/session/clear";

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }

            // Final attempt -- let it throw if it fails
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Polls the test-agent for data until non-empty results are returned or timeout is reached.
        /// The sample app exports data during shutdown, so there can be a brief delay
        /// between process exit and data appearing in the test-agent. The timeout is generous
        /// because first-time gRPC connections (TCP+HTTP/2+TLS handshake) plus tracer shutdown
        /// flushing can stack up on slower CI runners.
        /// </summary>
        private static async Task<JToken> WaitForTestAgentData(string url, int timeoutSeconds = 60, int pollIntervalMs = 500)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JToken.Parse(json);

                if (data.HasValues)
                {
                    return data;
                }

                await Task.Delay(pollIntervalMs);
            }

            // Final attempt -- return whatever we get so the caller's assertion shows the actual value
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
            var finalJson = await finalResponse.Content.ReadAsStringAsync();
            return JToken.Parse(finalJson);
        }

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
