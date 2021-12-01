// <copyright file="AutomaticTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DistributedTracer
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class AutomaticTracerTests
    {
        [Fact]
        public void GetSpanContext_NoChild()
        {
            var automaticTracer = new AutomaticTracer();

            automaticTracer.GetDistributedTrace().Should().BeNull();

            automaticTracer.SetDistributedTrace(new SpanContext(1, 2));

            ((IDistributedTracer)automaticTracer).GetSpanContext().Should().BeNull("automatic tracer shouldn't read the distributed trace unless there is a child tracer");
        }

        [Fact]
        public void GetSpanContext()
        {
            var automaticTracer = new AutomaticTracer();
            automaticTracer.Register(Mock.Of<ICommonTracer>());

            automaticTracer.GetDistributedTrace().Should().BeNull();

            var expectedSpanContext = new SpanContext(1, 2, SamplingPriority.UserKeep, "Service", "Origin");

            automaticTracer.SetDistributedTrace(expectedSpanContext);

            var actualSpanContext = ((IDistributedTracer)automaticTracer).GetSpanContext();

            actualSpanContext.Should().BeEquivalentTo(expectedSpanContext);
        }

        [Fact]
        public void SetSpanContext()
        {
            var automaticTracer = new AutomaticTracer();
            automaticTracer.Register(Mock.Of<ICommonTracer>());

            var distributedTracer = (IDistributedTracer)automaticTracer;
            var expectedSpanContext = new SpanContext(1, 2);

            distributedTracer.SetSpanContext(expectedSpanContext);
            distributedTracer.GetSpanContext().Should().BeEquivalentTo(expectedSpanContext);
        }

        [Fact]
        public void LockSamplingPriority()
        {
            var manualTracer = new Mock<ICommonTracer>();

            var automaticTracer = new AutomaticTracer();
            automaticTracer.Register(manualTracer.Object);

            ((IDistributedTracer)automaticTracer).LockSamplingPriority();

            manualTracer.Verify(t => t.LockSamplingPriority(), Times.Once);
        }

        [Fact]
        public void TrySetSamplingPriority_NoChild()
        {
            var automaticTracer = new AutomaticTracer();

            var samplingPriority = ((IDistributedTracer)automaticTracer).TrySetSamplingPriority(SamplingPriority.UserKeep);

            samplingPriority.Should().Be(SamplingPriority.UserKeep, "TrySetSamplingPriority should be pass-through when there is no child");
        }

        [Fact]
        public void TrySetSamplingPriority()
        {
            var manualTracer = new Mock<ICommonTracer>();
            manualTracer.Setup(t => t.TrySetSamplingPriority(It.IsAny<int?>())).Returns((int?)SamplingPriority.UserReject);

            var automaticTracer = new AutomaticTracer();
            automaticTracer.Register(manualTracer.Object);

            var samplingPriority = ((IDistributedTracer)automaticTracer).TrySetSamplingPriority(SamplingPriority.UserKeep);

            samplingPriority.Should().Be(SamplingPriority.UserReject, "TrySetSamplingPriority should return the value given by the child");
        }

        [Fact]
        public void GetDistributedTrace()
        {
            var automaticTracer = new AutomaticTracer();

            automaticTracer.GetDistributedTrace().Should().BeNull();

            using (var scope = Tracer.Instance.StartActive("Test"))
            {
                var spanContext = SpanContextPropagator.Instance.Extract(automaticTracer.GetDistributedTrace());

                spanContext.Should().NotBeNull();
                spanContext.TraceId.Should().Be(scope.Span.TraceId);
                spanContext.SpanId.Should().Be(scope.Span.SpanId);
            }

            automaticTracer.GetDistributedTrace().Should().BeNull();
        }
    }
}
