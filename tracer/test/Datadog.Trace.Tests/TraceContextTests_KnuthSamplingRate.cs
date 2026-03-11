// <copyright file="TraceContextTests_KnuthSamplingRate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Sampling;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class TraceContextTests_KnuthSamplingRate
{
        [Theory]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 0.5f, "0.5")]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 1.0f, "1")]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 0.1f, "0.1")]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 0.7654321f, "0.765432")]
        [InlineData(SamplingPriorityValues.UserKeep, SamplingMechanism.LocalTraceSamplingRule, 0.25f, "0.25")]
        [InlineData(SamplingPriorityValues.UserKeep, SamplingMechanism.RemoteUserSamplingRule, 0.75f, "0.75")]
        [InlineData(SamplingPriorityValues.UserKeep, SamplingMechanism.RemoteAdaptiveSamplingRule, 0.333333f, "0.333333")]
        public void SetSamplingPriority_SetsKsrTag_ForApplicableMechanisms(
            int samplingPriority, string samplingMechanism, float rate, string expectedKsr)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(samplingPriority, samplingMechanism, rate);

            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().Be(expectedKsr);
        }

        [Theory]
        [InlineData(SamplingPriorityValues.AutoReject, SamplingMechanism.AgentRate, 0.5f)]
        [InlineData(SamplingPriorityValues.UserReject, SamplingMechanism.LocalTraceSamplingRule, 0.25f)]
        [InlineData(SamplingPriorityValues.UserReject, SamplingMechanism.RemoteUserSamplingRule, 0.75f)]
        [InlineData(SamplingPriorityValues.UserReject, SamplingMechanism.RemoteAdaptiveSamplingRule, 0.1f)]
        public void SetSamplingPriority_SetsKsrTag_EvenForDropDecisions(
            int samplingPriority, string samplingMechanism, float rate)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(samplingPriority, samplingMechanism, rate);

            // KSR should be set regardless of keep/drop since it uses TryAddTag
            // and records the original sampling rate
            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().NotBeNull();
        }

        [Theory]
        [InlineData(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual, 0.5f)]
        [InlineData(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm, 0.5f)]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.Default, 0.5f)]
        public void SetSamplingPriority_DoesNotSetKsrTag_ForNonApplicableMechanisms(
            int samplingPriority, string samplingMechanism, float rate)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(samplingPriority, samplingMechanism, rate);

            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().BeNull();
        }

        [Fact]
        public void SetSamplingPriority_DoesNotSetKsrTag_WhenRateIsNull()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, rate: null);

            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().BeNull();
        }

        [Fact]
        public void SetSamplingPriority_OverwritesExistingKsrTag()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 0.5f);
            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().Be("0.5");

            // second call should overwrite the first KSR value (SetTag semantics)
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.LocalTraceSamplingRule, 0.75f);
            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().Be("0.75");
        }

        [Theory]
        [InlineData(0.0f, "0")]
        [InlineData(1.0f, "1")]
        [InlineData(0.5f, "0.5")]
        [InlineData(0.1f, "0.1")]
        [InlineData(0.123456f, "0.123456")]
        [InlineData(0.1234567f, "0.123457")]
        [InlineData(0.00001f, "1E-05")]
        public void KsrTag_FormattedWithSixSignificantDigits(float rate, string expected)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, rate);

            traceContext.Tags.GetTag(Tags.Propagated.KnuthSamplingRate).Should().Be(expected);
        }
}
