// <copyright file="RareSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent.TraceSamplers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class RareSamplerTests
    {
        [Fact]
        public void SampleUniqueSpans()
        {
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings { IsRareSamplerEnabled = true }));

            var trace1 = new[] { Tracer.Instance.StartSpan("1"), Tracer.Instance.StartSpan("1") };
            var trace2 = new[] { Tracer.Instance.StartSpan("2"), Tracer.Instance.StartSpan("1") };
            var trace3 = new[] { Tracer.Instance.StartSpan("1"), Tracer.Instance.StartSpan("1") };

            sampler.Sample(new(trace1)).Should().BeTrue();
            sampler.Sample(new(trace2)).Should().BeTrue();
            sampler.Sample(new(trace3)).Should().BeFalse();

            trace1.Select(s => s.GetMetric(Metrics.RareSpan)).Should().Equal(1.0, null);
            trace2.Select(s => s.GetMetric(Metrics.RareSpan)).Should().Equal(1.0, null);
            trace3.Select(s => s.GetMetric(Metrics.RareSpan)).Should().Equal((double?)null, null);
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
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings { IsRareSamplerEnabled = enabled }));

            sampler.IsEnabled.Should().Be(enabled);
        }

        [Fact]
        public void DoNotSampleIfDisabled()
        {
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings { IsRareSamplerEnabled = false }));

            var trace = new[] { Tracer.Instance.StartSpan("1") };

            sampler.Sample(new(trace)).Should().BeFalse();
            trace.Single().GetMetric(Metrics.RareSpan).Should().Be(null);
        }

        [Fact]
        public void DoNotSampleManualPriority()
        {
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings { IsRareSamplerEnabled = true }));

            var trace1 = new[] { Tracer.Instance.StartSpan("1") };

            trace1[0].Context.TraceContext.Tags.SetTag(Tags.Propagated.DecisionMaker, SamplingMechanism.Manual.ToString());

            sampler.Sample(new(trace1)).Should().BeFalse();
        }

        [Fact]
        public void OnlySampleTopLevelSpans()
        {
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings { IsRareSamplerEnabled = true }));

            var knownTrace = new[] { Tracer.Instance.StartSpan("1") };

            // Show span "1" to the RareSampler
            sampler.Sample(new(knownTrace)).Should().BeTrue();

            using var scope1 = Tracer.Instance.StartActiveInternal("1");
            using var scope2 = Tracer.Instance.StartActiveInternal("2");

            // Create a trace with the interesting span ("2") as a child
            var trace = new[] { scope1.Span, scope2.Span };

            sampler.Sample(new(trace)).Should().BeFalse();
        }

        [Theory]
        [InlineData(Tags.Measured)]
        [InlineData(Tags.PartialSnapshot)]
        public void SampleSpecialMetrics(string metricName)
        {
            var sampler = new RareSampler(new ImmutableTracerSettings(new TracerSettings { IsRareSamplerEnabled = true }));

            var knownTrace = new[] { Tracer.Instance.StartSpan("1") };

            // Show span "1" to the RareSampler
            sampler.Sample(new(knownTrace)).Should().BeTrue();

            using var scope1 = Tracer.Instance.StartActiveInternal("1");
            using var scope2 = Tracer.Instance.StartActiveInternal("2");
            scope2.Span.SetMetric(metricName, 1.0);

            // Create a trace with the interesting span ("2") as a child
            var trace = new[] { scope1.Span, scope2.Span };

            sampler.Sample(new(trace)).Should().BeTrue();
        }
    }
}
