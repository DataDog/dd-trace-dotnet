// <copyright file="TraceContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.Util;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TraceContextTests
    {
        // Values shared by the OpenTelemetry trace-state sampling tests below.
        private const ulong OtelTraceStateExampleTraceIdLower = 0xfff972474538efff;
        private const ulong OtelTraceStateImprecisionClampTraceIdLower = 0x03a93ee8b1999f00;
        private const ulong OtelTraceStateMinimumTraceIdLower = 1;
        private const double OtelTraceStateExampleSamplingRate = 0.1;
        private const double OtelTraceStateImprecisionClampSamplingRate = 0.1;
        private const float OtelTraceStateRateLimiterRate = 0.05f;
        private const float NeverSampleRate = 0f;
        private const float AlwaysSampleRate = 1f;

        // Expected values generated from the fixed trace ID at the sampling rate.
        private const string OtelTraceStateExampleRandomValue = "ef284ace7a91e1";
        private const string OtelTraceStateExampleThreshold = "e6666666666668";
        private const string OtelTraceStateMaximumThreshold = "ffffffffffffff";
        private const string OtelTraceStateMinimumThreshold = "0";
        private const string OtelTraceStateUnrelatedValue = "foo:bar";
        private const string OtelTraceStateExample = "rv:" + OtelTraceStateExampleRandomValue + ";th:" + OtelTraceStateExampleThreshold;
        private const string OtelTraceStateExampleWithoutThreshold = "rv:" + OtelTraceStateExampleRandomValue;
        private const string OtelTraceStateExampleWithUnrelatedValue = OtelTraceStateExample + ";" + OtelTraceStateUnrelatedValue;
        private const string OtelTraceStateExampleWithoutThresholdWithUnrelatedValue = OtelTraceStateExampleWithoutThreshold + ";" + OtelTraceStateUnrelatedValue;

        private readonly StubDatadogTracer _tracerMock = new();

        [Fact]
        public void UtcNow_GivesLegitTime()
        {
            var traceContext = new TraceContext(_tracerMock);

            var now = traceContext.Clock.UtcNow;
            var expectedNow = DateTimeOffset.UtcNow;

            // We cannot assume that expectedNow > now due to the difference of accuracy of QPC and UtcNow.
            var allowedVariance = EnvironmentTools.IsOsx()
                                        ? TimeSpan.FromMilliseconds(200) // The clock in virtualized osx is terrible
                                        : TimeSpan.FromMilliseconds(30);
            now.Should().BeCloseTo(expectedNow, allowedVariance);
        }

        [Fact]
        public void UtcNow_IsMonotonic()
        {
            var traceContext = new TraceContext(_tracerMock);

            var t1 = traceContext.Clock.UtcNow;
            DateTimeOffset t2;
            do
            {
                t2 = traceContext.Clock.UtcNow;
            }
            while (t1 == t2);

            t2.Should().BeAfter(t1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FlushPartialTraces(bool partialFlush)
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.PartialFlushEnabled, partialFlush },
                    { ConfigurationKeys.PartialFlushMinSpans, 5 },
                });
            var tracer = new StubDatadogTracer(settings);

            var traceContext = new TraceContext(tracer);

            void AddAndCloseSpan()
            {
                var span = new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            var rootSpan = new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

            traceContext.AddSpan(rootSpan);

            for (int i = 0; i < 4; i++)
            {
                AddAndCloseSpan();
            }

            // At this point in time, we have 4 closed spans in the trace
            tracer.WrittenChunks.Should().BeEmpty();

            AddAndCloseSpan();

            // Now we have 5 closed spans, partial flush should kick-in if activated
            if (partialFlush)
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(5);
                tracer.WrittenChunks.Clear();
            }
            else
            {
                tracer.WrittenChunks.Should().BeEmpty();
            }

            for (int i = 0; i < 5; i++)
            {
                AddAndCloseSpan();
            }

            // We have 5 more closed spans, partial flush should kick-in a second time if activated
            if (partialFlush)
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(5);
                tracer.WrittenChunks.Clear();
            }
            else
            {
                tracer.WrittenChunks.Should().BeEmpty();
            }

            traceContext.CloseSpan(rootSpan);

            // Now the remaining spans are flushed
            if (partialFlush)
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(1);
            }
            else
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(11);
            }
        }

        [Fact]
        public void FullFlushShouldNotPropagateSamplingPriority()
        {
            const int partialFlushThreshold = 3;

            Span CreateSpan() => new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

            var tracer = new StubDatadogTracer(TracerSettings.Create(new()
            {
                { ConfigurationKeys.PartialFlushEnabled, true },
                { ConfigurationKeys.PartialFlushMinSpans, partialFlushThreshold },
            }));

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);

            var rootSpan = CreateSpan();

            traceContext.AddSpan(rootSpan);

            for (int i = 0; i < partialFlushThreshold - 1; i++)
            {
                var span = CreateSpan();
                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            // At this point, only one span is missing to reach the threshold for partial flush
            tracer.WrittenChunks.Should().BeEmpty("partial flush should not have been triggered");

            // Closing the root span brings the number of closed spans to the threshold
            // but a full flush should be triggered rather than a partial, because every span in the trace has been closed
            traceContext.CloseSpan(rootSpan);

            tracer.WrittenChunks.Should().NotBeNullOrEmpty("a full flush should have been triggered");

            rootSpan.GetMetric(Metrics.SamplingPriority).Should().BeNull("because sampling priority is not added until serialization");

            tracer.WrittenChunks.Should().ContainSingle().Which.Should().OnlyContain(s => s.GetMetric(Metrics.SamplingPriority) == null, "because sampling priority is not added until serialization");
        }

        [Fact]
        public void PartialFlushShouldPropagateMetadata()
        {
            const int partialFlushThreshold = 2;

            Span CreateSpan() => new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

            var tracer = new StubDatadogTracer(TracerSettings.Create(new()
            {
                { ConfigurationKeys.PartialFlushEnabled, true },
                { ConfigurationKeys.PartialFlushMinSpans, partialFlushThreshold },
            }));

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);

            var rootSpan = CreateSpan();

            // Root span will stay open for the duration of the test
            traceContext.AddSpan(rootSpan);

            // Add enough child spans to trigger partial flush
            for (int i = 0; i < partialFlushThreshold; i++)
            {
                var span = CreateSpan();
                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            tracer.WrittenChunks.Should().NotBeEmpty("partial flush should have been triggered");
            tracer.WrittenChunks.Should().ContainSingle().Which.Should().OnlyContain(s => s.GetMetric(Metrics.SamplingPriority) == null, "because sampling priority is not added until serialization");
        }

        [Fact]
        public async Task Null_Service_Names_Dont_Throw()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            await using var tracer = TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);

            var span = tracer.StartSpan("operation");
            span.SetService(null, null);
            span.Finish(); // should not throw
        }

        [Fact]
        public void SetSamplingPriority_RootProbabilityKeep_DerivesRvTh_MatchesRfcWorkedExample()
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateExampleTraceIdLower);

            traceContext.SetSamplingPriority(
                priority: SamplingPriorityValues.UserKeep,
                mechanism: SamplingMechanism.LocalTraceSamplingRule,
                rate: OtelTraceStateExampleSamplingRate,
                sample: true);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(OtelTraceStateExample);
        }

        [Fact]
        public void SetSamplingPriority_RootProbabilityDrop_StillEmitsTh()
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateExampleTraceIdLower);

            traceContext.SetSamplingPriority(
                priority: SamplingPriorityValues.UserReject,
                mechanism: SamplingMechanism.LocalTraceSamplingRule,
                rate: OtelTraceStateExampleSamplingRate,
                sample: false);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Contain("th:" + OtelTraceStateExampleThreshold);
        }

        [Fact]
        public void SetSamplingPriority_WithoutW3CInjection_DoesNotDeriveOtelTraceState()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.PropagationStyleInject, ContextPropagationHeaderStyle.Datadog } });
            var traceContext = new TraceContext(new StubDatadogTracer(settings));
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)OtelTraceStateMinimumTraceIdLower, spanId: RandomIdGenerator.Shared.NextSpanId());
            traceContext.AddSpan(new Span(spanContext, DateTimeOffset.UtcNow));

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.LocalTraceSamplingRule, rate: OtelTraceStateExampleSamplingRate, sample: true);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(string.Empty);
        }

        [Fact]
        public void SetSamplingPriority_ImprecisionClamp_ForcesAgreementWithDdDecision()
        {
            // The Datadog decision and the 56-bit OTel representation can land on
            // opposite sides of a sampling boundary after rate conversion.
            var sample = SamplingHelpers.SampleByRate(OtelTraceStateImprecisionClampTraceIdLower, OtelTraceStateImprecisionClampSamplingRate);
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(OtelTraceStateImprecisionClampTraceIdLower);

            traceContext.SetSamplingPriority(
                priority: sample ? SamplingPriorityValues.UserKeep : SamplingPriorityValues.UserReject,
                mechanism: SamplingMechanism.LocalTraceSamplingRule,
                rate: (float)OtelTraceStateImprecisionClampSamplingRate,
                sample: sample);

            var rv = traceContext.OtelTraceState!.RandomValue;
            var th = traceContext.OtelTraceState.Threshold;
            (rv >= th).Should().Be(sample);
        }

        [Theory]
        [InlineData(SamplingMechanism.Manual)]
        [InlineData(SamplingMechanism.Asm)]
        public void SetSamplingPriority_NonProbabilityOverride_RemovesLocallyGeneratedRv(string mechanism)
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateMinimumTraceIdLower);
            traceContext.SetSamplingPriority(
                SamplingPriorityValues.UserKeep,
                SamplingMechanism.LocalTraceSamplingRule,
                rate: OtelTraceStateExampleSamplingRate,
                sample: true);

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, mechanism);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(string.Empty);
        }

        [Fact]
        public void SetSamplingPriority_RateLimiterDemotesKeep_StripsThButKeepsLocallyGeneratedRv()
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateExampleTraceIdLower);

            traceContext.SetSamplingPriority(
                priority: SamplingPriorityValues.UserReject,
                mechanism: SamplingMechanism.LocalTraceSamplingRule,
                rate: OtelTraceStateExampleSamplingRate,
                limiterRate: OtelTraceStateRateLimiterRate,
                sample: true);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(OtelTraceStateExampleWithoutThreshold);
        }

        [Fact]
        public void TraceSampler_LimiterDemotesKeep_KeepsRv_ViaGetOrMakeSamplingDecision()
        {
            var builder = new TraceSampler.Builder(new TracerRateLimiter(maxTracesPerInterval: 0, intervalMilliseconds: null));
            builder.RegisterRule(new GlobalSamplingRateRule(AlwaysSampleRate));
            var sampler = builder.Build();

            var tracer = new StubDatadogTracer(sampler);
            var rootSpan = new Span(new SpanContext(OtelTraceStateExampleTraceIdLower, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);
            var traceContext = new TraceContext(tracer);
            traceContext.AddSpan(rootSpan);

            traceContext.GetOrMakeSamplingDecision();

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(OtelTraceStateExampleWithoutThreshold);
        }

        [Fact]
        public void SetSamplingPriority_RateLimiterDemotesKeep_StripsInheritedThButKeepsRvAndUnknownItems()
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateExampleTraceIdLower);
            traceContext.OtelTraceState = OtelTraceState.Parse(OtelTraceStateExampleWithUnrelatedValue);

            traceContext.SetSamplingPriority(
                priority: SamplingPriorityValues.UserReject,
                mechanism: SamplingMechanism.LocalTraceSamplingRule,
                rate: OtelTraceStateExampleSamplingRate,
                limiterRate: OtelTraceStateRateLimiterRate,
                sample: true);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(OtelTraceStateExampleWithoutThresholdWithUnrelatedValue);
        }

        [Theory]
        // `th:ffffffffffffff` encodes a zero sampling rate, so the trace must drop.
        [InlineData(NeverSampleRate, OtelTraceStateExampleRandomValue, OtelTraceStateMaximumThreshold)]
        // `th:0` encodes a 100% sampling rate, so the trace must keep; `rv:0` is
        // valid, but also represents a keep decision at this threshold.
        [InlineData(AlwaysSampleRate, OtelTraceStateExampleRandomValue, OtelTraceStateMinimumThreshold)]
        public void SetSamplingPriority_BoundaryRate_ProducesValidOtelTraceState(double rate, string expectedRandomValue, string expectedThreshold)
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateExampleTraceIdLower);
            var sample = SamplingHelpers.SampleByRate(OtelTraceStateExampleTraceIdLower, rate);

            traceContext.SetSamplingPriority(
                priority: sample ? SamplingPriorityValues.UserKeep : SamplingPriorityValues.UserReject,
                mechanism: SamplingMechanism.LocalTraceSamplingRule,
                rate: rate,
                sample: sample);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be($"rv:{expectedRandomValue};th:{expectedThreshold}");
        }

        [Fact]
        public void SetSamplingPriority_ManualOverride_StripsInheritedThButKeepsRv()
        {
            var traceContext = TraceContextTestHelpers.CreateTraceContextWithRootSpan(traceIdLower: OtelTraceStateMinimumTraceIdLower);
            traceContext.OtelTraceState = OtelTraceState.Parse(OtelTraceStateExample);

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);

            WriteOtelTraceStateHeader(traceContext.OtelTraceState).Should().Be(OtelTraceStateExampleWithoutThreshold);
        }

        private static ulong ParseThForTest(string otelTraceState)
        {
            foreach (var item in otelTraceState.Split(';'))
            {
                if (item.StartsWith("th:", StringComparison.Ordinal))
                {
                    return Convert.ToUInt64(item.Substring(3), 16);
                }
            }

            throw new InvalidOperationException("no th found");
        }

        private static string WriteOtelTraceStateHeader(OtelTraceState traceState)
        {
            var sb = new StringBuilder();
            OtelTraceStateHelpers.SetRvTh(sb, traceState?.CachedHeaderString, traceState?.RandomValue, traceState?.Threshold);
            return sb.ToString();
        }
    }
}
