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
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class StatsTests
    {
        private const int StatsComputationIntervalSeconds = 3;

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
            WaitForStats(statsWaitEvent, expectStats);
            WaitForTraces(tracesWaitEvent, finishSpansOnClose); // The last span was an error, so we expect to receive it as long as it closed

            if (expectStats)
            {
                var payload = agent.WaitForStats(1);
                payload.Should().HaveCount(1);

                var stats1 = payload[0];
                stats1.Sequence.Should().Be(1);

                var totalDuration = span1.Duration + span2.Duration + span3.Duration;
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

            void AssertStats(MockClientStatsPayload stats, Span span, TimeSpan totalDuration)
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
                group.Duration.Should().Be(totalDuration.ToNanoseconds());
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
