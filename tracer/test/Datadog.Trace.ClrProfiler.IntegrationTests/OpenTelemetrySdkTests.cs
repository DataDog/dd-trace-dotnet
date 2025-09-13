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
using Datadog.Trace.TestHelpers;
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
        private readonly Regex _timeUnixNanoRegexMetrics = new(@"TimeUnixNano: ([0-9]{10}[0-9]+)");
        private readonly Regex _exceptionStacktraceRegex = new(@"exception.stacktrace"":""System.ArgumentException: Example argument exception.*"",""");

        public OpenTelemetrySdkTests(ITestOutputHelper output)
            : base("OpenTelemetrySdk", output)
        {
            SetServiceName(CustomServiceName);
            SetServiceVersion(string.Empty);
        }

        public static IEnumerable<object[]> GetData() => PackageVersions.OpenTelemetry;

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
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
        public async Task SubmitsOtlpMetrics(string packageVersion)
        {
            var parsedVersion = Version.Parse(!string.IsNullOrEmpty(packageVersion) ? packageVersion : "1.12.0");
            var runtimeMajor = Environment.Version.Major;

            var snapshotName = runtimeMajor switch
            {
                6 when parsedVersion >= new Version("1.3.2") && parsedVersion < new Version("1.5.0") => ".NET_6",
                7 or 8 when parsedVersion >= new Version("1.5.1") && parsedVersion < new Version("1.10.0") => ".NET_7_8",
                >= 9 when parsedVersion >= new Version("1.10.0") => string.Empty,
                _ => throw new SkipException($"Skipping test due to irrelevant runtime and OTel versions mix: .NET {runtimeMajor} & Otel v{parsedVersion}")
            };

            var initialAgentPort = TcpPortProvider.GetOpenPort();

            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", "true");
            SetEnvironmentVariable("DD_METRICS_OTEL_METER_NAMES", "OpenTelemetryMetricsMeter");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://127.0.0.1:{initialAgentPort}");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", "delta");

            using var agent = EnvironmentHelper.GetMockAgent(fixedPort: initialAgentPort);
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion ?? "1.12.0"))
            {
                var metricRequests = agent.OtlpRequests
                                          .Where(r => r.PathAndQuery.StartsWith("/v1/metrics"))
                                          .ToList();

                metricRequests.Should().NotBeEmpty("Expected OTLP metric requests were not received.");

                // Group the scope metrics by the resource metrics and schema URL (should only be one unique combination)
                var resourceMetricByResource = metricRequests
                                        .SelectMany(r => r.MetricsData.ResourceMetrics)
                                        .GroupBy(r => new Tuple<global::OpenTelemetry.Proto.Resource.V1.Resource, string>(r.Resource, r.SchemaUrl))
                                        .Should()
                                        .ContainSingle()
                                        .Subject;

                // Group the individual metrics by scope metric and schema URL (should only be one unique combination since we're only using one ActivitySource)
                // This may result in multiple entries for metrics that are repeated multiple times before the test exits
                var scopeMetricsByResource = resourceMetricByResource
                                        .SelectMany(r => r.ScopeMetrics)
                                        .GroupBy(r => new Tuple<global::OpenTelemetry.Proto.Common.V1.InstrumentationScope, string>(r.Scope, r.SchemaUrl))
                                        .OrderBy(group => group.Key.Item1.Name);

                var scopeMetrics = new List<object>();
                foreach (var scopeMetricByResource in scopeMetricsByResource)
                {
                    var metrics = scopeMetricByResource
                                    .SelectMany(r => r.Metrics)
                                    .GroupBy(r => r.Name)
                                    .OrderBy(group => group.Key)
                                    .Select(group => group.First())
                                    .ToList();

                    scopeMetrics.Add(new
                    {
                        Scope = scopeMetricByResource.Key.Item1,
                        Metrics = metrics,
                        SchemaUrl = scopeMetricByResource.Key.Item2
                    });
                }

                // Filter out the telemetry resource name, if any
                foreach (var attribute in resourceMetricByResource.Key.Item1.Attributes)
                {
                    if (attribute.Key.Equals("telemetry.sdk.version"))
                    {
                        attribute.Value.StringValue = "sdk-version";
                    }
                }

                // Although there's only one resource, let's still emit snapshot data in the expected array format
                var resourceMetrics = new object[]
                {
                    new
                    {
                        Resource = resourceMetricByResource.Key.Item1,
                        ScopeMetrics = scopeMetrics,
                        SchemaUrl = resourceMetricByResource.Key.Item2,
                    }
                };

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddRegexScrubber(_timeUnixNanoRegexMetrics, @"TimeUnixNano"": <DateTimeOffset.Now>");

                var suffix = GetSuffix(packageVersion);
                var fileName = $"{nameof(OpenTelemetrySdkTests)}.SubmitsOtlpMetrics{suffix}{snapshotName}";

                await Verifier.Verify(resourceMetrics, settings)
                              .UseFileName(fileName)
                              .DisableRequireUniquePrefix();
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MeterListenerCapturesAllMetrics()
        {
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", "delta");

            OTelMetrics.MetricReader.Initialize();

            // Create a test meter
            using var meter = new System.Diagnostics.Metrics.Meter("TestMeter");

            var testTags = new KeyValuePair<string, object>[]
            {
                new("http.method", "GET"),
                new("rid", "1234567890")
            };

            var counter = meter.CreateCounter<long>("test.counter");
            meter.CreateObservableCounter<long>("test.async.counter", () => 22L);
            meter.CreateObservableGauge<double>("test.async.gauge", () => 88L);
            var histogram = meter.CreateHistogram<double>("test.histogram");

            counter.Add(11L, testTags);
            histogram.Record(33L, testTags);

            var expectedMetricsCount = 4;

#if NET7_0_OR_GREATER
            var upDownCounter = meter.CreateUpDownCounter<long>("test.upDownCounter");
            meter.CreateObservableUpDownCounter<long>("test.async.upDownCounter", () => 66L);

            upDownCounter.Add(55L, testTags);
            expectedMetricsCount += 2;
#elif NET9_0_OR_GREATER
            var gauge = meter.CreateGauge<double>("test.gauge");
            gauge.Record(77L, testTags);
            expectedMetricsCount += 1;
#endif

            // Trigger async collection and get metrics for testing
            OTelMetrics.MetricReader.CollectObservableInstruments();
            var capturedMetrics = OTelMetrics.MetricReaderHandler.GetCapturedMetricsForTesting();

            capturedMetrics.Count.Should().Be(expectedMetricsCount, $"Should capture exactly {expectedMetricsCount} metrics based on .NET version");

            var counterMetric = capturedMetrics["TestMeter.test.counter"];
            counterMetric.InstrumentType.Should().Be("Counter");
            counterMetric.AggregationTemporality.Should().Be("Delta");
            counterMetric.IsIntegerValue.Should().Be(true);
            counterMetric.RunningSum.Should().Be(11.0);
            counterMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            counterMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");

            var asyncCounterMetric = capturedMetrics["TestMeter.test.async.counter"];
            asyncCounterMetric.InstrumentType.Should().Be("Counter");
            asyncCounterMetric.AggregationTemporality.Should().Be("Delta");
            asyncCounterMetric.IsIntegerValue.Should().Be(true);
            asyncCounterMetric.RunningSum.Should().Be(22.0);
            asyncCounterMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var asyncGaugeMetric = capturedMetrics["TestMeter.test.async.gauge"];
            asyncGaugeMetric.InstrumentType.Should().Be("Gauge");
            asyncGaugeMetric.AggregationTemporality.Should().Be("Delta");
            asyncGaugeMetric.IsIntegerValue.Should().Be(false);
            asyncGaugeMetric.RunningSum.Should().Be(88.0);
            asyncGaugeMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var histogramMetric = capturedMetrics["TestMeter.test.histogram"];
            histogramMetric.InstrumentType.Should().Be("Histogram");
            histogramMetric.AggregationTemporality.Should().Be("Delta");
            histogramMetric.IsIntegerValue.Should().Be(false);
            histogramMetric.RunningCount.Should().Be(1L);
            histogramMetric.RunningSum.Should().Be(33.0);
            histogramMetric.RunningMin.Should().Be(33.0);
            histogramMetric.RunningMax.Should().Be(33.0);
            histogramMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            histogramMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");
            var bucketCounts = histogramMetric.RunningBucketCounts;
            bucketCounts[4].Should().Be(1L, "Value 33.0 should fall in bucket 4 (25 < 33.0 <= 50)");

#if NET7_0_OR_GREATER
            if (capturedMetrics.ContainsKey("TestMeter.test.upDownCounter"))
            {
                var upDownMetric = capturedMetrics["TestMeter.test.upDownCounter"];
                upDownMetric.InstrumentType.Should().Be("UpDownCounter");
                upDownMetric.RunningSum.Should().Be(55.0);
                upDownMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            }

            if (capturedMetrics.ContainsKey("TestMeter.test.async.upDownCounter"))
            {
                var asyncUpDownMetric = capturedMetrics["TestMeter.test.async.upDownCounter"];
                asyncUpDownMetric.InstrumentType.Should().Be("UpDownCounter");
                asyncUpDownMetric.RunningSum.Should().Be(66.0);
                asyncUpDownMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");
            }
#elif NET9_0_OR_GREATER
            if (capturedMetrics.ContainsKey("TestMeter.test.gauge"))
            {
                var gaugeMetric = capturedMetrics["TestMeter.test.gauge"];
                gaugeMetric.InstrumentType.Should().Be("Gauge");
                gaugeMetric.RunningSum.Should().Be(77.0);
                gaugeMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            }
#endif
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
