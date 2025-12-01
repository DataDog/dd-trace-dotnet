// <copyright file="TraceContextTests_SetSamplingDecision.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Sampling;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests;

public class TraceContextTests_SetSamplingDecision
{
        [Theory]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.Default)]
        [InlineData(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate)]
        [InlineData(SamplingPriorityValues.AutoReject, SamplingMechanism.LocalTraceSamplingRule)]
        [InlineData(SamplingPriorityValues.UserReject, SamplingMechanism.Manual)]
        [InlineData(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm)]
        public void SetSamplingDecision(int samplingPriority, string samplingMechanism)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(samplingPriority, samplingMechanism);

            traceContext.SamplingPriority.Should().Be(samplingPriority);

            if (samplingPriority > 0)
            {
                traceContext.Tags.GetTag("_dd.p.dm").Should().Be(samplingMechanism);
            }
            else
            {
                traceContext.Tags.GetTag("_dd.p.dm").Should().BeNull();
            }
        }

        [Fact]
        public void SetSamplingDecision_KeepsFirstMechanism()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate);
            traceContext.SamplingPriority.Should().Be(SamplingPriorityValues.AutoKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be(SamplingMechanism.AgentRate);

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);
            traceContext.SamplingPriority.Should().Be(SamplingPriorityValues.UserKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be(SamplingMechanism.AgentRate);
        }

        [Fact]
        public void SetSamplingDecision_RemovesMechanismOnDrop()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate);
            traceContext.SamplingPriority.Should().Be(SamplingPriorityValues.AutoKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be(SamplingMechanism.AgentRate);

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            traceContext.SamplingPriority.Should().Be(SamplingPriorityValues.UserReject);
            traceContext.Tags.GetTag("_dd.p.dm").Should().BeNull();

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            traceContext.SamplingPriority.Should().Be(SamplingPriorityValues.UserKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be(SamplingMechanism.Asm);
        }

        [Theory]
        [InlineData(SamplingMechanism.AgentRate, 0.5f, "0.5")]
        [InlineData(SamplingMechanism.AgentRate, 0.123456f, "0.123456")]
        [InlineData(SamplingMechanism.AgentRate, 0.9999f, "0.9999")]
        [InlineData(SamplingMechanism.AgentRate, 0.0001f, "0.0001")]
        [InlineData(SamplingMechanism.AgentRate, 1.0f, "1")]
        [InlineData(SamplingMechanism.AgentRate, 0.0f, "0")]
        [InlineData(SamplingMechanism.LocalTraceSamplingRule, 0.5f, "0.5")]
        [InlineData(SamplingMechanism.LocalTraceSamplingRule, 0.123456f, "0.123456")]
        [InlineData(SamplingMechanism.RemoteUserSamplingRule, 0.75f, "0.75")]
        [InlineData(SamplingMechanism.RemoteAdaptiveSamplingRule, 0.25f, "0.25")]
        public void SetSamplingDecision_SetsKnuthSamplingRate_ForApplicableMechanisms(string mechanism, float rate, string expectedValue)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, mechanism, rate);

            traceContext.Tags.GetTag("_dd.p.ksr").Should().Be(expectedValue);
        }

        [Theory]
        [InlineData(SamplingMechanism.Default)]
        [InlineData(SamplingMechanism.Manual)]
        [InlineData(SamplingMechanism.Asm)]
        public void SetSamplingDecision_DoesNotSetKnuthSamplingRate_ForNonApplicableMechanisms(string mechanism)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, mechanism, 0.5f);

            traceContext.Tags.GetTag("_dd.p.ksr").Should().BeNull();
        }

        [Fact]
        public void SetSamplingDecision_RemovesKnuthSamplingRateOnDrop()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            // Set with AgentRate mechanism (should set _dd.p.ksr)
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 0.5f);
            traceContext.Tags.GetTag("_dd.p.ksr").Should().Be("0.5");

            // Drop the trace (should remove _dd.p.ksr)
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            traceContext.Tags.GetTag("_dd.p.ksr").Should().BeNull();
        }

        [Fact]
        public void SetSamplingDecision_KeepsFirstKnuthSamplingRate()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            // Set initial rate
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, 0.5f);
            traceContext.Tags.GetTag("_dd.p.ksr").Should().Be("0.5");

            // Try to override (should keep original value)
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.LocalTraceSamplingRule, 0.75f);
            traceContext.Tags.GetTag("_dd.p.ksr").Should().Be("0.5");
        }

        [Fact]
        public void SetSamplingDecision_DoesNotSetKnuthSamplingRate_WhenRateIsNull()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);

            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate, rate: null);

            traceContext.Tags.GetTag("_dd.p.ksr").Should().BeNull();
        }
}
