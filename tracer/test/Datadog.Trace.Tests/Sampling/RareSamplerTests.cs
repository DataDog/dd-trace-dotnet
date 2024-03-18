// <copyright file="RareSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent.TraceSamplers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class RareSamplerTests
    {
        [Fact]
        public async Task SampleUniqueSpans()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.RareSamplerEnabled, true } });
            var sampler = new RareSampler(new ImmutableTracerSettings(settings));

            var trace1 = new[] { tracer.StartSpan("1"), tracer.StartSpan("1") };
            trace1[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            var trace2 = new[] { tracer.StartSpan("2"), tracer.StartSpan("1") };
            trace2[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            var trace3 = new[] { tracer.StartSpan("1"), tracer.StartSpan("1") };
            trace3[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            sampler.Sample(new(trace1)).Should().BeTrue();
            sampler.Sample(new(trace2)).Should().BeTrue();
            sampler.Sample(new(trace3)).Should().BeFalse();

            trace1.Select(s => s.GetMetric(Metrics.RareSpan)).Should().Equal(1.0, null);
            trace2.Select(s => s.GetMetric(Metrics.RareSpan)).Should().Equal(1.0, null);
            trace3.Select(s => s.GetMetric(Metrics.RareSpan)).Should().Equal((double?)null, null);
        }

        [Theory]
        [InlineData(SamplingPriorityValues.UserReject, true)]
        [InlineData(SamplingPriorityValues.AutoReject, true)]
        [InlineData(SamplingPriorityValues.UserKeep, false)]
        [InlineData(SamplingPriorityValues.AutoKeep, false)]
        public async Task OnlySampleRejectPriorities(int priority, bool expected)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.RareSamplerEnabled, true } });
            var sampler = new RareSampler(new ImmutableTracerSettings(settings));

            var trace = new[] { tracer.StartSpan("1") };
            trace[0].Context.TraceContext.SetSamplingPriority(priority);

            sampler.Sample(new(trace)).Should().Be(expected);

            if (expected)
            {
                trace[0].GetMetric(Metrics.RareSpan).Should().Be(1.0);
            }
            else
            {
                trace[0].GetMetric(Metrics.RareSpan).Should().BeNull();
            }
        }

        [Fact]
        public void DisabledByDefault()
        {
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings()));

            sampler.IsEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Configuration(bool enabled)
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.RareSamplerEnabled, enabled } });
            var sampler = new RareSampler(new ImmutableTracerSettings(settings));

            sampler.IsEnabled.Should().Be(enabled);
        }

        [Fact]
        public void DoNotSampleIfDisabled()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.RareSamplerEnabled, false } });
            var sampler = new RareSampler(new ImmutableTracerSettings(settings));

            var trace = new[] { Tracer.Instance.StartSpan("1") };
            trace[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            sampler.Sample(new(trace)).Should().BeFalse();
            trace.Single().GetMetric(Metrics.RareSpan).Should().Be(null);
        }

        [Fact]
        public async Task OnlySampleTopLevelSpans()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.RareSamplerEnabled, true } });
            var sampler = new RareSampler(new ImmutableTracerSettings(settings));

            var knownTrace = new[] { tracer.StartSpan("1") };
            knownTrace[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            // Show span "1" to the RareSampler
            sampler.Sample(new(knownTrace)).Should().BeTrue();

            using var scope1 = tracer.StartActiveInternal("1");
            scope1.Span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            using var scope2 = tracer.StartActiveInternal("2");

            // Create a trace with the interesting span ("2") as a child
            var trace = new[] { scope1.Span, scope2.Span };

            sampler.Sample(new(trace)).Should().BeFalse();
        }

        [Theory]
        [InlineData(Tags.Measured)]
        [InlineData(Tags.PartialSnapshot)]
        public async Task SampleSpecialMetrics(string metricName)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.RareSamplerEnabled, true } });
            var sampler = new RareSampler(new ImmutableTracerSettings(settings));

            var knownTrace = new[] { tracer.StartSpan("1") };
            knownTrace[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            // Show span "1" to the RareSampler
            sampler.Sample(new(knownTrace)).Should().BeTrue();

            using var scope1 = tracer.StartActiveInternal("1");
            using var scope2 = tracer.StartActiveInternal("2");
            scope2.Span.SetMetric(metricName, 1.0);

            // Create a trace with the interesting span ("2") as a child
            var trace = new[] { scope1.Span, scope2.Span };
            trace[0].Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            sampler.Sample(new(trace)).Should().BeTrue();
        }
    }
}
