// <copyright file="ManualTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DistributedTracer
{
    public class ManualTracerTests
    {
        [Fact]
        public void GetSpanContext()
        {
            var expectedSpanContext = new SpanContext(1, 2, SamplingPriority.UserKeep, "Service", "Origin");

            var automaticTracer = new Mock<IAutomaticTracer>();
            automaticTracer.Setup(t => t.GetDistributedTrace()).Returns(expectedSpanContext);

            var manualTracer = new ManualTracer(automaticTracer.Object);

            var actualSpanContext = ((IDistributedTracer)manualTracer).GetSpanContext();

            actualSpanContext.Should().BeEquivalentTo(expectedSpanContext);
        }

        [Fact]
        public void SetSpanContext()
        {
            var automaticTracer = new Mock<IAutomaticTracer>();
            var manualTracer = new ManualTracer(automaticTracer.Object);

            var expectedSpanContext = new SpanContext(1, 2, SamplingPriority.UserKeep, "Service", "Origin");

            ((IDistributedTracer)manualTracer).SetSpanContext(expectedSpanContext);

            automaticTracer.Verify(t => t.SetDistributedTrace(expectedSpanContext), Times.Once());
        }

        [Fact]
        public void SetSamplingPriority()
        {
            var automaticTracer = new Mock<IAutomaticTracer>();

            var manualTracer = new ManualTracer(automaticTracer.Object);

            ((IDistributedTracer)manualTracer).SetSamplingPriority(SamplingPriority.UserKeep);

            automaticTracer.Verify(t => t.SetSamplingPriority((int?)SamplingPriority.UserKeep), Times.Once());
        }
    }
}
