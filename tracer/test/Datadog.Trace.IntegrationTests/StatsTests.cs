// <copyright file="StatsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class StatsTests
    {
        private const int StatsComputationIntervalSeconds = 10;

        [Fact]
        public async Task SendsStatsWithProcessing_Normalizer()
        {
            string serviceTooLongString = new string('s', 150);
            string truncatedServiceString = new string('s', 100);

            string serviceInvalidString = "bad$service";
            string serviceNormalizedString = "bad_service";

            string nameTooLongString = new string('n', 150);
            string truncatedNameString = new string('n', 100);

            string nameInvalidString = "bad$name";
            string nameNormalizedString = "bad_name";

            string typeTooLongString = new string('t', 150);
            string truncatedTypeString = new string('t', 100);

            var agentConfiguration = new MockTracerAgent.AgentConfiguration();
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort(), configuration: agentConfiguration);

            var settings = new TracerSettings
            {
                StatsComputationEnabled = true,
                ServiceName = "default-service",
                ServiceVersion = "v1",
                Environment = "test",
                Exporter = new ExporterSettings
                {
                    AgentUri = new Uri($"http://localhost:{agent.Port}"),
                }
            };

            var immutableSettings = settings.Build();
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);
            List<Span> spans = new();
            Span span;
            SpinWait.SpinUntil(() => tracer.CanComputeStats, 5_000);

            // Service
            // - If service is empty, it is set to DefaultServiceName
            // - If service is too long, it is truncated to 100 characters
            // - Normalized to match dogstatsd tag format
            span = CreateDefaultSpan();
            span.ServiceName = serviceTooLongString;
            span.Finish();
            spans.Add(span);

            span = CreateDefaultSpan();
            span.ServiceName = serviceInvalidString;
            span.Finish();
            spans.Add(span);

            // Name
            // - If empty, it is set to "unnamed_operation"
            // - If too long, it is truncated to 100 characters
            // - Normalized to match Datadog metric name normalization
            span = CreateDefaultSpan();
            span.OperationName = string.Empty;
            span.Finish();
            spans.Add(span);

            span = CreateDefaultSpan();
            span.OperationName = nameTooLongString;
            span.Finish();
            spans.Add(span);

            span = CreateDefaultSpan();
            span.OperationName = nameInvalidString;
            span.Finish();
            spans.Add(span);

            // Resource
            // - If empty, it is set to the same value as Name
            span = CreateDefaultSpan();
            span.ResourceName = string.Empty;
            span.Finish();
            spans.Add(span);

            // Duration
            // - If smaller than 0, it is set to 0
            // - If larger than math.MaxInt64 - Start, it is set to 0
            span = CreateDefaultSpan();
            span.Finish(TimeSpan.FromSeconds(-1));
            spans.Add(span);

            span = CreateDefaultSpan();
            span.Finish(TimeSpan.FromTicks(long.MaxValue / TimeConstants.NanoSecondsPerTick));
            spans.Add(span);

            // Start
            // - If smaller than Y2K, set to (now - Duration) or 0 if the result is negative
            var beforeY2KDuration = TimeSpan.FromMilliseconds(2000);
            var year2000Time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            span = CreateDefaultSpan();
            span.SetStartTime(year2000Time.AddDays(-1));
            span.Finish(beforeY2KDuration);
            spans.Add(span);

            // Type
            // - If too long, it is truncated to 100 characters
            span = CreateDefaultSpan();
            span.Type = typeTooLongString;
            span.Finish();
            spans.Add(span);

            // Meta
            // - "http.status_code" key is deleted if it's an invalid numeric value smaller than 100 or bigger than 600
            span = CreateDefaultSpan();
            span.SetTag(Tags.HttpStatusCode, "invalid");
            span.Finish();
            spans.Add(span);

            span = CreateDefaultSpan();
            span.SetTag(Tags.HttpStatusCode, "99");
            span.Finish();
            spans.Add(span);

            span = CreateDefaultSpan();
            span.SetTag(Tags.HttpStatusCode, "600");
            span.Finish();
            spans.Add(span);

            await tracer.FlushAsync();

            var statsPayload = agent.WaitForStats(1);
            statsPayload.Should().HaveCount(1);
            statsPayload[0].Stats.Should().HaveCount(1);

            var buckets = statsPayload[0].Stats[0].Stats;
            buckets.Sum(stats => stats.Hits).Should().Be(13);

            using var assertionScope = new AssertionScope();

            // Assert normaliztion of service names
            buckets.Where(s => s.Service == truncatedServiceString).Should().ContainSingle("service names are truncated at 100 characters");
            buckets.Where(s => s.Service == serviceNormalizedString).Should().ContainSingle("service names are normalized");
            buckets.Where(s => s.Service != truncatedServiceString && s.Service != serviceNormalizedString).Should().OnlyContain(s => s.Service == "default-service");

            spans.Where(s => s.ServiceName == truncatedServiceString).Should().ContainSingle("service names are truncated at 100 characters");
            spans.Where(s => s.ServiceName == serviceNormalizedString).Should().ContainSingle("service names are normalized");
            spans.Where(s => s.ServiceName != truncatedServiceString && s.ServiceName != serviceNormalizedString).Should().OnlyContain(s => s.ServiceName == "default-service");

            // Assert normaliztion of operation names
            // Note: "-" are replaced with "_" for operation name normalization, which has the Datadog metric name normalization rules
            buckets.Where(s => s.Name == "unnamed_operation").Should().ContainSingle("empty operation names should be set to \"unnamed_operation\"");
            buckets.Where(s => s.Name == truncatedNameString).Should().ContainSingle("operation names are truncated at 100 characters");
            buckets.Where(s => s.Name == nameNormalizedString).Should().ContainSingle("operation names are normalized");
            buckets.Where(s => s.Name != "unnamed_operation" && s.Name != truncatedNameString && s.Name != nameNormalizedString).Should().OnlyContain(s => s.Name == "default_operation");

            spans.Where(s => s.OperationName == "unnamed_operation").Should().ContainSingle("empty operation names should be set to \"unnamed_operation\"");
            spans.Where(s => s.OperationName == truncatedNameString).Should().ContainSingle("operation names are truncated at 100 characters");
            spans.Where(s => s.OperationName == nameNormalizedString).Should().ContainSingle("operation names are normalized");
            spans.Where(s => s.OperationName != "unnamed_operation" && s.OperationName != truncatedNameString && s.OperationName != nameNormalizedString).Should().OnlyContain(s => s.OperationName == "default_operation");

            // Assert normaliztion of resource names
            buckets.Where(s => s.Resource == "default_operation").Should().ContainSingle("empty resource names should be set to the same value as Name");
            buckets.Where(s => s.Resource != "default_operation").Should().OnlyContain(s => s.Resource == "default-resource");

            spans.Where(s => s.ResourceName == "default_operation").Should().ContainSingle("empty resource names should be set to the same value as Name");
            spans.Where(s => s.ResourceName != "default_operation").Should().OnlyContain(s => s.ResourceName == "default-resource");

            // Assert normalization of duration
            // Assert normalization of start
            var durationStartBuckets = buckets.Where(s => s.Name == "default_operation" && s.Resource == "default-resource" && s.Service == "default-service" && s.Synthetics == false && s.Type == "default-type" && s.HttpStatusCode == 200);
            durationStartBuckets.Should().HaveCount(1);
            durationStartBuckets.Single().Hits.Should().Be(3);
            durationStartBuckets.Single().Duration.Should().Be(beforeY2KDuration.ToNanoseconds());

            var durationStartSpans = spans.Where(s => s.OperationName == "default_operation" && s.ResourceName == "default-resource" && s.ServiceName == "default-service" && s.Context.Origin != "synthetics" && s.Type == "default-type" && s.GetTag(Tags.HttpStatusCode) == "200");
            durationStartSpans.Should().HaveCount(3);
            durationStartSpans.Sum(s => s.Duration.ToNanoseconds()).Should().Be(beforeY2KDuration.ToNanoseconds());

            // Assert normaliztion of types
            buckets.Where(s => s.Type == truncatedTypeString).Should().ContainSingle("types are truncated at 100 characters");
            buckets.Where(s => s.Type != truncatedTypeString).Should().OnlyContain(s => s.Type == "default-type");

            spans.Where(s => s.Type == truncatedTypeString).Should().ContainSingle("types are truncated at 100 characters");
            spans.Where(s => s.Type != truncatedTypeString).Should().OnlyContain(s => s.Type == "default-type");

            // Assert normaliztion of http status codes
            buckets.Where(s => s.HttpStatusCode == 0).Sum(s => s.Hits).Should().Be(3, "http.status_code key is deleted if it's an invalid numeric value smaller than 100 or bigger than 600");
            buckets.Where(s => s.HttpStatusCode != 0).Should().OnlyContain(s => s.HttpStatusCode == 200);

            spans.Where(s => s.GetTag(Tags.HttpStatusCode) is null).Should().HaveCount(3, "http.status_code key is deleted if it's an invalid numeric value smaller than 100 or bigger than 600");
            spans.Where(s => s.GetTag(Tags.HttpStatusCode) is not null).Should().OnlyContain(s => s.GetTag(Tags.HttpStatusCode) == "200");

            Span CreateDefaultSpan()
            {
                using (var scope = tracer.StartActiveInternal("default-operation", finishOnClose: false))
                {
                    var span = scope.Span;
                    span.ResourceName = "default-resource";
                    span.Type = "default-type";
                    span.SetTag(Tags.HttpStatusCode, "200");
                    return span;
                }
            }
        }

        [Fact]
        public async Task SendsStatsWithProcessing_Obfuscator()
        {
            var agentConfiguration = new MockTracerAgent.AgentConfiguration();
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort(), configuration: agentConfiguration);

            var settings = new TracerSettings
            {
                StatsComputationEnabled = true,
                ServiceName = "default-service",
                ServiceVersion = "v1",
                Environment = "test",
                Exporter = new ExporterSettings
                {
                    AgentUri = new Uri($"http://localhost:{agent.Port}"),
                }
            };

            var immutableSettings = settings.Build();
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);
            List<Span> spans = new();
            SpinWait.SpinUntil(() => tracer.CanComputeStats, 5_000);

            spans.Add(CreateDefaultSpan(type: "sql", resource: "SELECT * FROM TABLE WHERE userId = 'abc1287681964'"));
            spans.Add(CreateDefaultSpan(type: "sql", resource: "SELECT * FROM TABLE WHERE userId = 'abc\\'1287\\'681\\'\\'\\'\\'964'"));

            spans.Add(CreateDefaultSpan(type: "cassandra", resource: "SELECT * FROM TABLE WHERE userId = 'abc1287681964'"));
            spans.Add(CreateDefaultSpan(type: "cassandra", resource: "SELECT * FROM TABLE WHERE userId = 'abc\\'1287\\'681\\'\\'\\'\\'964'"));

            spans.Add(CreateDefaultSpan(type: "redis", resource: "SET le_key le_value"));
            spans.Add(CreateDefaultSpan(type: "redis", resource: "SET another_key another_value"));

            await tracer.FlushAsync();

            var statsPayload = agent.WaitForStats(1);
            statsPayload.Should().HaveCount(1);
            statsPayload[0].Stats.Should().HaveCount(1);

            var buckets = statsPayload[0].Stats[0].Stats;
            buckets.Sum(stats => stats.Hits).Should().Be(6);
            buckets.Should().HaveCount(3, "obfuscator should reduce the cardinality of resource names");

            using var assertionScope = new AssertionScope();

            var sqlBuckets = buckets.Where(stats => stats.Type == "sql");
            sqlBuckets.Should().ContainSingle();
            sqlBuckets.Single().Hits.Should().Be(2);
            sqlBuckets.Single().Resource.Should().Be("SELECT * FROM TABLE WHERE userId = ?");

            var cassandraBuckets = buckets.Where(stats => stats.Type == "cassandra");
            cassandraBuckets.Should().ContainSingle();
            cassandraBuckets.Single().Hits.Should().Be(2);
            cassandraBuckets.Single().Resource.Should().Be("SELECT * FROM TABLE WHERE userId = ?");

            var redisBuckets = buckets.Where(stats => stats.Type == "redis");
            redisBuckets.Should().ContainSingle();
            redisBuckets.Single().Hits.Should().Be(2);
            redisBuckets.Single().Resource.Should().Be("SET");

            Span CreateDefaultSpan(string type, string resource)
            {
                using (var scope = tracer.StartActiveInternal("default-operation"))
                {
                    var span = scope.Span;
                    span.ResourceName = resource;
                    span.Type = type;
                    return span;
                }
            }
        }

        [Fact]
        public async Task SendStats()
        {
            await SendStatsHelper(statsComputationEnabled: true, expectStats: true);
        }

        [Fact]
        public async Task SendsStatsAndDropsSpansWhenSampleRateIsZero_TS007()
        {
            await SendStatsHelper(statsComputationEnabled: true, expectStats: true, expectAllTraces: false, globalSamplingRate: 0.0);
        }

        [Fact]
        public async Task SendsStatsOnlyAfterSpansAreFinished_TS008()
        {
            await SendStatsHelper(statsComputationEnabled: true, expectStats: false, finishSpansOnClose: false);
        }

        [Fact]
        public async Task IsDisabledThroughConfiguration_TS010()
        {
            await SendStatsHelper(statsComputationEnabled: false, expectStats: false);
        }

        private async Task SendStatsHelper(bool statsComputationEnabled, bool expectStats, double? globalSamplingRate = null, bool expectAllTraces = true, bool finishSpansOnClose = true)
        {
            expectStats &= statsComputationEnabled && finishSpansOnClose;
            var statsWaitEvent = new AutoResetEvent(false);
            var tracesWaitEvent = new AutoResetEvent(false);

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());

            List<string> droppedP0TracesHeaderValues = new();
            List<string> droppedP0SpansHeaderValues = new();
            agent.RequestReceived += (sender, args) =>
            {
                var context = args.Value;
                if (context.Request.RawUrl.EndsWith("/traces"))
                {
                    droppedP0TracesHeaderValues.Add(context.Request.Headers.Get("Datadog-Client-Dropped-P0-Traces"));
                    droppedP0SpansHeaderValues.Add(context.Request.Headers.Get("Datadog-Client-Dropped-P0-Spans"));
                }
            };

            agent.StatsDeserialized += (_, _) =>
            {
                statsWaitEvent.Set();
            };

            agent.RequestDeserialized += (_, _) =>
            {
                tracesWaitEvent.Set();
            };

            var settings = new TracerSettings
            {
                GlobalSamplingRate = globalSamplingRate,
                StatsComputationEnabled = statsComputationEnabled,
                StatsComputationInterval = StatsComputationIntervalSeconds,
                ServiceVersion = "V",
                Environment = "Test",
                Exporter = new ExporterSettings
                {
                    AgentUri = new Uri($"http://localhost:{agent.Port}"),
                }
            };

            var immutableSettings = settings.Build();

            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);

            // Scenario 1: Send server span with 200 status code (success). This is a unique stats point so this trace is kept
            Span span1;
            using (var scope = tracer.StartActiveInternal("operationName", finishOnClose: finishSpansOnClose))
            {
                span1 = scope.Span;
                span1.ResourceName = "resourceName";
                span1.SetHttpStatusCode(200, isServer: true, immutableSettings);
                span1.Type = "span1";
            }

            await tracer.FlushAsync();

            // Scenario 2: Send the same server span as before, but it is not an error and it is not a new point,
            // so this trace can be dropped when ClientDropP0s is true
            Span span2;
            using (var scope = tracer.StartActiveInternal("operationName", finishOnClose: finishSpansOnClose))
            {
                span2 = scope.Span;
                span2.ResourceName = "resourceName";
                span2.SetHttpStatusCode(200, isServer: true, immutableSettings);
                span2.Type = "span1";
            }

            await tracer.FlushAsync();

            // Scenario 3: Send server span with 200 status code but with an error
            Span span3;
            using (var scope = tracer.StartActiveInternal("operationName", finishOnClose: finishSpansOnClose))
            {
                span3 = scope.Span;
                span3.ResourceName = "resourceName";
                span3.SetHttpStatusCode(200, isServer: true, immutableSettings);
                span3.Type = "span1";
                span3.Error = true;
            }

            await tracer.FlushAsync();
            await tracer.FlushAndCloseAsync(); // Flushes and closes both traces and stats

            WaitForStats(statsWaitEvent, expectStats);
            WaitForTraces(tracesWaitEvent, finishSpansOnClose); // The last span was an error, so we expect to receive it as long as it closed

            if (expectStats)
            {
                var payload = agent.WaitForStats(1);
                payload.Should().HaveCount(1);

                var stats1 = payload[0];
                stats1.Sequence.Should().Be(1);

                var totalDuration = span1.Duration.ToNanoseconds() + span2.Duration.ToNanoseconds() + span3.Duration.ToNanoseconds();
                AssertStats(stats1, span1, totalDuration);
            }

            // Assert header values
            if (!finishSpansOnClose)
            {
                // If we never finish the spans, there will be no requests to the trace agent
                droppedP0TracesHeaderValues.Should().BeEquivalentTo(new string[] { });
                droppedP0SpansHeaderValues.Should().BeEquivalentTo(new string[] { });
            }
            else if (!expectStats)
            {
                // If we don't send stats, then we won't add the headers
                droppedP0TracesHeaderValues.Should().BeEquivalentTo(new string[] { null, null, null });
                droppedP0SpansHeaderValues.Should().BeEquivalentTo(new string[] { null, null, null });
            }
            else if (expectAllTraces)
            {
                // If we still expect all the traces to come in, each request will have 0 dropped traces/spans
                droppedP0TracesHeaderValues.Should().BeEquivalentTo(new string[] { "0", "0", "0" });
                droppedP0SpansHeaderValues.Should().BeEquivalentTo(new string[] { "0", "0", "0" });
            }
            else
            {
                droppedP0TracesHeaderValues.Should().BeEquivalentTo(new string[] { "0", "1" });
                droppedP0SpansHeaderValues.Should().BeEquivalentTo(new string[] { "0", "1" });
            }

            void WaitForTraces(AutoResetEvent e, bool expected)
            {
                if (expected)
                {
                    e.WaitOne(TimeSpan.FromSeconds(15)).Should().Be(true, "timeout while waiting for traces");
                }
                else
                {
                    e.WaitOne(TimeSpan.FromSeconds(15)).Should().Be(false, "No traces should be received");
                }
            }

            void WaitForStats(AutoResetEvent e, bool expected)
            {
                if (expected)
                {
                    e.WaitOne(TimeSpan.FromSeconds(StatsComputationIntervalSeconds * 2)).Should().Be(true, "timeout while waiting for stats");
                }
                else
                {
                    e.WaitOne(TimeSpan.FromSeconds(StatsComputationIntervalSeconds * 2)).Should().Be(false, "No stats should be received");
                }
            }

            void AssertStats(MockClientStatsPayload stats, Span span, long totalDuration)
            {
                stats.Env.Should().Be(settings.Environment);
                stats.Hostname.Should().Be(HostMetadata.Instance.Hostname);
                stats.Version.Should().Be(settings.ServiceVersion);
                stats.TracerVersion.Should().Be(TracerConstants.AssemblyVersion);
                stats.AgentAggregation.Should().Be(null);
                stats.Lang.Should().Be(TracerConstants.Language);
                stats.RuntimeId.Should().Be(Tracer.RuntimeId);
                stats.Stats.Should().HaveCount(1);

                var bucket = stats.Stats[0];
                bucket.AgentTimeShift.Should().Be(0);
                bucket.Duration.Should().Be(TimeSpan.FromSeconds(StatsComputationIntervalSeconds).ToNanoseconds());
                bucket.Start.Should().NotBe(0);

                bucket.Stats.Should().HaveCount(1);

                var group = bucket.Stats[0];

                group.DbType.Should().BeNull();
                group.Duration.Should().Be(totalDuration);
                group.Errors.Should().Be(1);
                group.ErrorSummary.Should().NotBeEmpty();
                group.Hits.Should().Be(3);
                group.HttpStatusCode.Should().Be(int.Parse(span.GetTag(Tags.HttpStatusCode)));
                group.Name.Should().Be(span.OperationName);
                group.OkSummary.Should().NotBeEmpty();
                group.Synthetics.Should().Be(false);
                group.TopLevelHits.Should().Be(3);
                group.Type.Should().Be(span.Type);
            }
        }
    }
}
