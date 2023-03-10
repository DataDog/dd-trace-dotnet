// <copyright file="AutomaticTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Propagators;
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

            var expectedSpanContext = new SpanContext((TraceId)1, 2, SamplingPriorityValues.UserKeep, "Service", "Origin");

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
        public void SetSamplingPriority_NoChild()
        {
            var automaticTracer = new AutomaticTracer();

            ((IDistributedTracer)automaticTracer).SetSamplingPriority(SamplingPriorityValues.UserKeep);
        }

        [Fact]
        public void SetSamplingPriority()
        {
            var manualTracer = new Mock<ICommonTracer>();

            var automaticTracer = new AutomaticTracer();
            automaticTracer.Register(manualTracer.Object);

            ((IDistributedTracer)automaticTracer).SetSamplingPriority(SamplingPriorityValues.UserKeep);

            manualTracer.Verify(t => t.SetSamplingPriority(SamplingPriorityValues.UserKeep), Times.Once);
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
                spanContext.TraceId128.Should().Be(((Scope)scope).Span.TraceId128);
                spanContext.TraceId.Should().Be(scope.Span.TraceId); // ISpan only has the ulong TraceId
                spanContext.SpanId.Should().Be(scope.Span.SpanId);
            }

            automaticTracer.GetDistributedTrace().Should().BeNull();
        }

        [Fact]
        public void RuntimeId()
        {
            var automaticTracer = new AutomaticTracer();

            var runtimeId = automaticTracer.GetAutomaticRuntimeId();

            Guid.TryParse(runtimeId, out _).Should().BeTrue();

            automaticTracer.GetAutomaticRuntimeId().Should().Be(runtimeId, "runtime id should remain the same");

            ((IDistributedTracer)automaticTracer).GetRuntimeId().Should().Be(runtimeId, "distributed tracer API should return the same runtime id");
        }

        [Fact]
        public void IsChildTracer()
        {
            var automaticTracer = new AutomaticTracer();
            ((IDistributedTracer)automaticTracer).IsChildTracer.Should().BeFalse();
        }
    }
}
