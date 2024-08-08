// <copyright file="TraceContextTests_SetSamplingDecision.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests;

public class TraceContextTests_SetSamplingDecision
{
        [Theory]
        [InlineData(InternalSamplingPriorityValues.AutoKeep, SamplingMechanism.Default)]
        [InlineData(InternalSamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate)]
        [InlineData(InternalSamplingPriorityValues.AutoReject, SamplingMechanism.LocalTraceSamplingRule)]
        [InlineData(InternalSamplingPriorityValues.UserReject, SamplingMechanism.Manual)]
        [InlineData(InternalSamplingPriorityValues.UserKeep, SamplingMechanism.Asm)]
        public void SetSamplingDecision(int samplingPriority, int samplingMechanism)
        {
            var tracer = new Mock<IDatadogTracer>();
            var traceContext = new TraceContext(tracer.Object);
            traceContext.SetSamplingPriority(samplingPriority, samplingMechanism);

            traceContext.SamplingPriority.Should().Be(samplingPriority);

            if (samplingPriority > 0)
            {
                traceContext.Tags.GetTag("_dd.p.dm").Should().Be($"-{samplingMechanism}");
            }
            else
            {
                traceContext.Tags.GetTag("_dd.p.dm").Should().BeNull();
            }
        }

        [Fact]
        public void SetSamplingDecision_KeepsFirstMechanism()
        {
            var tracer = new Mock<IDatadogTracer>();
            var traceContext = new TraceContext(tracer.Object);

            traceContext.SetSamplingPriority(InternalSamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate);
            traceContext.SamplingPriority.Should().Be(InternalSamplingPriorityValues.AutoKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be($"-{SamplingMechanism.AgentRate}");

            traceContext.SetSamplingPriority(InternalSamplingPriorityValues.UserKeep, SamplingMechanism.Manual);
            traceContext.SamplingPriority.Should().Be(InternalSamplingPriorityValues.UserKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be($"-{SamplingMechanism.AgentRate}");
        }

        [Fact]
        public void SetSamplingDecision_RemovesMechanismOnDrop()
        {
            var tracer = new Mock<IDatadogTracer>();
            var traceContext = new TraceContext(tracer.Object);

            traceContext.SetSamplingPriority(InternalSamplingPriorityValues.AutoKeep, SamplingMechanism.AgentRate);
            traceContext.SamplingPriority.Should().Be(InternalSamplingPriorityValues.AutoKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be($"-{SamplingMechanism.AgentRate}");

            traceContext.SetSamplingPriority(InternalSamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            traceContext.SamplingPriority.Should().Be(InternalSamplingPriorityValues.UserReject);
            traceContext.Tags.GetTag("_dd.p.dm").Should().BeNull();

            traceContext.SetSamplingPriority(InternalSamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            traceContext.SamplingPriority.Should().Be(InternalSamplingPriorityValues.UserKeep);
            traceContext.Tags.GetTag("_dd.p.dm").Should().Be($"-{SamplingMechanism.Asm}");
        }
}
