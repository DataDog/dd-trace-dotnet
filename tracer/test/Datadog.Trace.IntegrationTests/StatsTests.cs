// <copyright file="StatsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
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

            var beforeY2KDuration = TimeSpan.FromMilliseconds(2000);
            var year2KDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());

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

            var discovery = DiscoveryService.Create(settings.Build().Exporter);
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null, discoveryService: discovery);
            Span span;

            // Wait until the discovery service has been reached and we've confirmed that we can send stats
            var spinSucceeded = SpinWait.SpinUntil(() => tracer.TracerManager.AgentWriter is AgentWriter { CanComputeStats: true }, 5_000);
            spinSucceeded.Should().BeTrue();

            // Service
            // - If service is empty, it is set to DefaultServiceName
            // - If service is too long, it is truncated to 100 characters
            // - Normalized to match dogstatsd tag format
            CreateDefaultSpan(serviceName: serviceTooLongString);
            CreateDefaultSpan(serviceName: serviceInvalidString);

            // Name
            // - If empty, it is set to "unnamed_operation"
            // - If too long, it is truncated to 100 characters
            // - Normalized to match Datadog metric name normalization
            CreateDefaultSpan(operationName: string.Empty);
            CreateDefaultSpan(operationName: nameTooLongString);
            CreateDefaultSpan(operationName: nameInvalidString);

            // Resource
            // - If empty, it is set to the same value as Name
            CreateDefaultSpan(resourceName: string.Empty);

            // Duration
            // - If smaller than 0, it is set to 0
            // - If larger than math.MaxInt64 - Start, it is set to 0
            CreateDefaultSpan(finishOnClose: false).Finish(TimeSpan.FromSeconds(-1));
            CreateDefaultSpan(finishOnClose: false).Finish(TimeSpan.FromTicks(long.MaxValue / TimeConstants.NanoSecondsPerTick));

            // Start
            // - If smaller than Y2K, set to (now - Duration) or 0 if the result is negative
            span = CreateDefaultSpan(finishOnClose: false);
            span.SetStartTime(year2KDateTime.AddDays(-1));
            span.Finish(beforeY2KDuration);

            // Type
            // - If too long, it is truncated to 100 characters
            CreateDefaultSpan(type: typeTooLongString);

            // Meta
            // - "http.status_code" key is deleted if it's an invalid numeric value smaller than 100 or bigger than 600
            CreateDefaultSpan(httpStatusCode: "invalid");
            CreateDefaultSpan(httpStatusCode: "99");
            CreateDefaultSpan(httpStatusCode: "600");

            await tracer.TracerManager.ShutdownAsync(); // Flushes and closes both traces and stats

            var statsPayload = agent.WaitForStats(1);
            var spans = agent.WaitForSpans(13);

            statsPayload.Should().HaveCount(1);
            statsPayload[0].Stats.Should().HaveCount(1);

            var stats = statsPayload[0].Stats[0].Stats;
            stats.Sum(stats => stats.Hits).Should().Be(13);

            using var assertionScope = new AssertionScope();

            // Assert normaliztion of service names
            stats.Where(s => s.Service == truncatedServiceString).Should().ContainSingle("service names are truncated at 100 characters");
            stats.Where(s => s.Service == serviceNormalizedString).Should().ContainSingle("service names are normalized");
            stats.Where(s => s.Service != truncatedServiceString && s.Service != serviceNormalizedString).Should().OnlyContain(s => s.Service == "default-service");

            spans.Where(s => s.Service == truncatedServiceString).Should().ContainSingle("service names are truncated at 100 characters");
            spans.Where(s => s.Service == serviceNormalizedString).Should().ContainSingle("service names are normalized");
            spans.Where(s => s.Service != truncatedServiceString && s.Service != serviceNormalizedString).Should().OnlyContain(s => s.Service == "default-service");

            // Assert normaliztion of operation names
            // Note: "-" are replaced with "_" for operation name normalization, which has the Datadog metric name normalization rules
            stats.Where(s => s.Name == "unnamed_operation").Should().ContainSingle("empty operation names should be set to \"unnamed_operation\"");
            stats.Where(s => s.Name == truncatedNameString).Should().ContainSingle("operation names are truncated at 100 characters");
            stats.Where(s => s.Name == nameNormalizedString).Should().ContainSingle("operation names are normalized");
            stats.Where(s => s.Name != "unnamed_operation" && s.Name != truncatedNameString && s.Name != nameNormalizedString).Should().OnlyContain(s => s.Name == "default_operation");

            spans.Where(s => s.Name == "unnamed_operation").Should().ContainSingle("empty operation names should be set to \"unnamed_operation\"");
            spans.Where(s => s.Name == truncatedNameString).Should().ContainSingle("operation names are truncated at 100 characters");
            spans.Where(s => s.Name == nameNormalizedString).Should().ContainSingle("operation names are normalized");
            spans.Where(s => s.Name != "unnamed_operation" && s.Name != truncatedNameString && s.Name != nameNormalizedString).Should().OnlyContain(s => s.Name == "default_operation");

            // Assert normaliztion of resource names
            stats.Where(s => s.Resource == "default_operation").Should().ContainSingle("empty resource names should be set to the same value as Name");
            stats.Where(s => s.Resource != "default_operation").Should().OnlyContain(s => s.Resource == "default-resource");

            spans.Where(s => s.Resource == "default_operation").Should().ContainSingle("empty resource names should be set to the same value as Name");
            spans.Where(s => s.Resource != "default_operation").Should().OnlyContain(s => s.Resource == "default-resource");

            // Assert normalization of duration
            // Assert normalization of start
            var durationStartBuckets = stats.Where(s => s.Name == "default_operation" && s.Resource == "default-resource" && s.Service == "default-service" && s.Synthetics == false && s.Type == "default-type" && s.HttpStatusCode == 200);
            durationStartBuckets.Should().HaveCount(1);
            durationStartBuckets.Single().Hits.Should().Be(3);
            durationStartBuckets.Single().Duration.Should().Be(beforeY2KDuration.ToNanoseconds());

            var durationStartSpans = spans.Where(s => s.Name == "default_operation" && s.Resource == "default-resource" && s.Service == "default-service" && s.GetTag(Tags.Origin) != "synthetics" && s.Type == "default-type" && s.GetTag(Tags.HttpStatusCode) == "200");
            durationStartSpans.Should().HaveCount(3);
            durationStartSpans.Sum(s => s.Duration).Should().Be(beforeY2KDuration.ToNanoseconds());

            // Assert normaliztion of types
            stats.Where(s => s.Type == truncatedTypeString).Should().ContainSingle("types are truncated at 100 characters");
            stats.Where(s => s.Type != truncatedTypeString).Should().OnlyContain(s => s.Type == "default-type");

            spans.Where(s => s.Type == truncatedTypeString).Should().ContainSingle("types are truncated at 100 characters");
            spans.Where(s => s.Type != truncatedTypeString).Should().OnlyContain(s => s.Type == "default-type");

            // Assert normaliztion of http status codes
            stats.Where(s => s.HttpStatusCode == 0).Sum(s => s.Hits).Should().Be(3, "http.status_code key is deleted if it's an invalid numeric value smaller than 100 or bigger than 600");
            stats.Where(s => s.HttpStatusCode != 0).Should().OnlyContain(s => s.HttpStatusCode == 200);

            spans.Where(s => s.GetTag(Tags.HttpStatusCode) is null).Should().HaveCount(3, "http.status_code key is deleted if it's an invalid numeric value smaller than 100 or bigger than 600");
            spans.Where(s => s.GetTag(Tags.HttpStatusCode) is not null).Should().OnlyContain(s => s.GetTag(Tags.HttpStatusCode) == "200");

            Span CreateDefaultSpan(string serviceName = null, string operationName = null, string resourceName = null, string type = null, string httpStatusCode = null, bool finishOnClose = true)
            {
                using (var scope = tracer.StartActiveInternal(operationName ?? "default-operation", finishOnClose: finishOnClose))
                {
                    var span = scope.Span;

                    span.ResourceName = resourceName ?? "default-resource";
                    span.Type = type ?? "default-type";
                    span.SetTag(Tags.HttpStatusCode, httpStatusCode ?? "200");
                    span.ServiceName = serviceName ?? "default-service";

                    return span;
                }
            }
        }

        [Fact]
        public async Task SendsStatsWithProcessing_Obfuscator()
        {
            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());

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

            var discovery = DiscoveryService.Create(settings.Build().Exporter);
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null, discoveryService: discovery);

            // Wait until the discovery service has been reached and we've confirmed that we can send stats
            var spinSucceeded = SpinWait.SpinUntil(() => tracer.TracerManager.AgentWriter is AgentWriter { CanComputeStats: true }, 5_000);
            spinSucceeded.Should().BeTrue();

            CreateDefaultSpan(type: "sql", resource: "SELECT * FROM TABLE WHERE userId = 'abc1287681964'");
            CreateDefaultSpan(type: "sql", resource: "SELECT * FROM TABLE WHERE userId = 'abc\\'1287\\'681\\'\\'\\'\\'964'");

            CreateDefaultSpan(type: "cassandra", resource: "SELECT * FROM TABLE WHERE userId = 'abc1287681964'");
            CreateDefaultSpan(type: "cassandra", resource: "SELECT * FROM TABLE WHERE userId = 'abc\\'1287\\'681\\'\\'\\'\\'964'");

            CreateDefaultSpan(type: "redis", resource: "SET le_key le_value");
            CreateDefaultSpan(type: "redis", resource: "SET another_key another_value");

            await tracer.TracerManager.ShutdownAsync(); // Flushes and closes both traces and stats

            var statsPayload = agent.WaitForStats(1);
            var spans = agent.WaitForSpans(6);

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
            spans.Where(s => s.Type == "sql").Should()
                .HaveCount(2)
                .And.OnlyContain(s => s.Resource == "SELECT * FROM TABLE WHERE userId = ?");

            var cassandraBuckets = buckets.Where(stats => stats.Type == "cassandra");
            cassandraBuckets.Should().ContainSingle();
            cassandraBuckets.Single().Hits.Should().Be(2);
            cassandraBuckets.Single().Resource.Should().Be("SELECT * FROM TABLE WHERE userId = ?");
            spans.Where(s => s.Type == "cassandra").Should()
                .HaveCount(2)
                .And.OnlyContain(s => s.Resource == "SELECT * FROM TABLE WHERE userId = ?");

            var redisBuckets = buckets.Where(stats => stats.Type == "redis");
            redisBuckets.Should().ContainSingle();
            redisBuckets.Single().Hits.Should().Be(2);
            redisBuckets.Single().Resource.Should().Be("SET");
            spans.Where(s => s.Type == "redis").Should()
                .HaveCount(2)
                .And.OnlyContain(s => s.Resource == "SET");

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

        [Fact]
        public async Task IsDisabledWhenIncompatibleAgentDetected_TS011()
        {
            await SendStatsHelper(statsComputationEnabled: true, expectStats: false, statsEndpointEnabled: false);
        }

        [Fact]
        public async Task IsDisabledWhenAgentDropP0sIsFalse()
        {
            await SendStatsHelper(statsComputationEnabled: true, expectStats: false, expectAllTraces: true, globalSamplingRate: 0.0, clientDropP0sEnabled: false);
        }

        private async Task SendStatsHelper(bool statsComputationEnabled, bool expectStats, double? globalSamplingRate = null, bool expectAllTraces = true, bool finishSpansOnClose = true, bool statsEndpointEnabled = true, bool clientDropP0sEnabled = true)
        {
            expectStats &= statsComputationEnabled && finishSpansOnClose;
            var statsWaitEvent = new AutoResetEvent(false);
            var tracesWaitEvent = new AutoResetEvent(false);

            // Counters
            int tracesCount = 0;
            int spansCount = 0;
            int p0DroppedSpansCount = 0;

            // Configure the mock agent
            var agentConfiguration = new MockTracerAgent.AgentConfiguration();
            agentConfiguration.ClientDropP0s = clientDropP0sEnabled;
            if (!statsEndpointEnabled)
            {
                agentConfiguration.Endpoints = agentConfiguration.Endpoints.Where(s => s != "/v0.6/stats").ToArray();
            }

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort(), agentConfiguration: agentConfiguration);

            List<string> droppedP0TracesHeaderValues = new();
            List<string> droppedP0SpansHeaderValues = new();
            agent.RequestReceived += (sender, args) =>
            {
                var context = args.Value;
                if (context.PathAndQuery.EndsWith("/traces"))
                {
                    droppedP0TracesHeaderValues.Add(context.Headers.TryGetValue("Datadog-Client-Dropped-P0-Traces", out var droppedP0Traces) ? droppedP0Traces : null);
                    droppedP0SpansHeaderValues.Add(context.Headers.TryGetValue("Datadog-Client-Dropped-P0-Spans", out var droppedP0Spans) ? droppedP0Spans : null);
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

            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new()
                    {
                        { ConfigurationKeys.GlobalSamplingRate, globalSamplingRate.ToString() },
                        { ConfigurationKeys.StatsComputationEnabled, statsComputationEnabled.ToString() },
                        { ConfigurationKeys.StatsComputationInterval, StatsComputationIntervalSeconds.ToString() },
                        { ConfigurationKeys.RareSamplerEnabled, statsComputationEnabled.ToString() },
                        { ConfigurationKeys.ServiceVersion, "V" },
                        { ConfigurationKeys.Environment, "Test" },
                        { ConfigurationKeys.AgentUri, $"http://localhost:{agent.Port}" },
                    }));

            var immutableSettings = settings.Build();

            var discovery = DiscoveryService.Create(immutableSettings.Exporter);
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null, discoveryService: discovery);

            // Wait until the discovery service has been reached and we've confirmed that we can send stats
            if (expectStats)
            {
                var spinSucceeded = SpinWait.SpinUntil(() => tracer.TracerManager.AgentWriter is AgentWriter { CanComputeStats: true }, 5_000);
                spinSucceeded.Should().BeTrue();
            }

            // Scenario 1: Send the common span, but add an error
            // ClientDropP0s + UserReject Expectation: Kept because the trace contains error spans
            // Note: This is also a "rare" trace, which will be asserted later
            tracesCount++;
            spansCount++;

            Span span1;
            using (var scope = CreateCommonSpan(tracer, finishSpansOnClose, immutableSettings))
            {
                span1 = scope.Span;
                span1.Error = true;
            }

            await tracer.FlushAsync();

            // Scenario 2: Send the common span
            // ClientDropP0s + UserReject Expectation: Dropped because it is not a "rare" trace (this same stats point was seen before) and it does not contain any error spans
            tracesCount++;
            spansCount++;
            p0DroppedSpansCount++;

            Span span2;
            using (var scope = CreateCommonSpan(tracer, finishSpansOnClose, immutableSettings))
            {
                span2 = scope.Span;
            }

            await tracer.FlushAsync();

            // Scenario 3: Send the common span, but add an error
            // ClientDropP0s + UserReject Expectation: Kept because the trace contains error spans
            tracesCount++;
            spansCount++;

            Span span3;
            using (var scope = CreateCommonSpan(tracer, finishSpansOnClose, immutableSettings))
            {
                span3 = scope.Span;
                span3.Error = true;
            }

            await tracer.FlushAsync();

            // Scenario 4: Send the common span, but with a child span that has an error
            // ClientDropP0s + UserReject Expectation: Kept because the trace contains error spans
            tracesCount++;
            spansCount += 2;

            Span span4;
            using (var scope = CreateCommonSpan(tracer, finishSpansOnClose, immutableSettings))
            {
                span4 = scope.Span;

                using var innerScope = tracer.StartActiveInternal("child", finishOnClose: finishSpansOnClose);
                innerScope.Span.Error = true;
            }

            await tracer.FlushAsync();

            // Scenario 5: Send the common span, but with a child span that has an "analytic event" sample rate set to 0
            // ClientDropP0s + UserReject Expectation: Dropped because the trace was not kept by any samplers and the span with an "analytic event" sample rate was not sampled
            tracesCount++;
            spansCount += 2;
            p0DroppedSpansCount += 2;

            Span span5;
            using (var scope = CreateCommonSpan(tracer, finishSpansOnClose, immutableSettings))
            {
                span5 = scope.Span;

                using var innerScope = tracer.StartActiveInternal("child", finishOnClose: finishSpansOnClose);
                innerScope.Span.SetMetric(Tags.Analytics, 0);
            }

            await tracer.FlushAsync();

            // Scenario 6: Send the common span, but with a child span that has an "analytic event" sample rate set to 1
            // ClientDropP0s + UserReject Expectation: Kept because the trace contains a span that was sampled by its "analytic event" sample rate
            tracesCount++;
            spansCount += 2;

            Span span6;
            using (var scope = CreateCommonSpan(tracer, finishSpansOnClose, immutableSettings))
            {
                span6 = scope.Span;

                using var innerScope = tracer.StartActiveInternal("child", finishOnClose: finishSpansOnClose);
                innerScope.Span.SetMetric(Tags.Analytics, 1);
            }

            await tracer.FlushAsync();

            // Flush and close both traces and stats
            await tracer.TracerManager.ShutdownAsync();
            WaitForStats(statsWaitEvent, expectStats);
            WaitForTraces(tracesWaitEvent, finishSpansOnClose); // The last span was an error, so we expect to receive it as long as it closed

            if (expectStats)
            {
                var payload = agent.WaitForStats(1);
                payload.Should().HaveCount(1);

                var stats1 = payload[0];
                stats1.Sequence.Should().Be(1);

                var totalDuration = span1.Duration.ToNanoseconds()
                                    + span2.Duration.ToNanoseconds()
                                    + span3.Duration.ToNanoseconds()
                                    + span4.Duration.ToNanoseconds()
                                    + span5.Duration.ToNanoseconds()
                                    + span6.Duration.ToNanoseconds();
                AssertStats(stats1, span1, totalDuration);
            }

            // Make assertions when we send stats for completed traces
            if (finishSpansOnClose)
            {
                var numberOfSpans = expectAllTraces ? spansCount : spansCount - p0DroppedSpansCount;
                var payload = agent.WaitForSpans(numberOfSpans);
                payload.Should().HaveCount(numberOfSpans);

                AssertTraces(payload, expectStats);
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
                droppedP0TracesHeaderValues.Should().HaveCount(tracesCount).And.OnlyContain(s => s == null);
                droppedP0SpansHeaderValues.Should().HaveCount(tracesCount).And.OnlyContain(s => s == null);
            }
            else if (expectAllTraces)
            {
                // If we still expect all the traces to come in, each request will have 0 dropped traces/spans
                droppedP0TracesHeaderValues.Should().HaveCount(tracesCount).And.OnlyContain(s => s == "0");
                droppedP0SpansHeaderValues.Should().HaveCount(tracesCount).And.OnlyContain(s => s == "0");
            }
            else
            {
                droppedP0TracesHeaderValues.Should().BeEquivalentTo(new string[] { "0", "1", "0", "1" });
                droppedP0SpansHeaderValues.Should().BeEquivalentTo(new string[] { "0", "1", "0", "2" });
            }

            Scope CreateCommonSpan(Tracer tracer, bool finishSpansOnClose, ImmutableTracerSettings tracerSettings)
            {
                var scope = tracer.StartActiveInternal("operationName", finishOnClose: finishSpansOnClose);
                var span = scope.Span;
                span.ResourceName = "resourceName";
                span.SetHttpStatusCode(200, isServer: true, tracerSettings);
                span.Type = "span1";

                return scope;
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

            void AssertTraces(IReadOnlyList<MockSpan> payload, bool expectStats)
            {
                // All the spans generate the same stats point, so only the first
                // occurrence would be considered "rare". However, the RareSampler
                // only runs after the PrioritySampler, so assert the metric "_dd.rare"
                // based on the SamplingPriority
                bool expectRareMetric = expectStats && payload[0].GetMetric(Metrics.SamplingPriority) <= 0;
                if (expectRareMetric)
                {
                    payload[0].GetMetric("_dd.rare").Should().Be(1);
                }
                else
                {
                    payload[0].GetMetric("_dd.rare").Should().BeNull();
                }

                payload.Skip(1).Should().OnlyContain(span => span.GetMetric("_dd.rare") == null);
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
                group.Errors.Should().Be(2);
                group.ErrorSummary.Should().NotBeEmpty();
                group.Hits.Should().Be(tracesCount);
                group.HttpStatusCode.Should().Be(int.Parse(span.GetTag(Tags.HttpStatusCode)));
                group.Name.Should().Be(span.OperationName);
                group.OkSummary.Should().NotBeEmpty();
                group.Synthetics.Should().Be(false);
                group.TopLevelHits.Should().Be(tracesCount);
                group.Type.Should().Be(span.Type);
            }
        }
    }
}
