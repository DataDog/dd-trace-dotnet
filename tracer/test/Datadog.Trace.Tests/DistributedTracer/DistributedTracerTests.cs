// <copyright file="DistributedTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DistributedTracer
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [DistributedTracerRestorer]
    [TracerRestorer]
    public class DistributedTracerTests
    {
        [Fact]
        public void GetSpanContext()
        {
            var distributedTracer = new Mock<IDistributedTracer>();

            var spanContext = new SpanContext(1, 2, SamplingPriority.UserKeep);

            distributedTracer.Setup(t => t.GetSpanContext()).Returns(spanContext);
            distributedTracer.Setup(t => t.GetRuntimeId()).Returns(Guid.NewGuid().ToString());

            Datadog.Trace.ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(distributedTracer.Object);

            using var parentScope = Tracer.Instance.StartActive("Parent");
            using var scope = (Scope)Tracer.Instance.StartActive("Test");

            distributedTracer.Verify(t => t.GetSpanContext(), Times.Exactly(2));
            scope.Span.TraceId128.Should().Be(spanContext.TraceId128);
            ((ISpan)scope.Span).TraceId.Should().Be(spanContext.TraceId);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(spanContext.SamplingPriority);
        }

        [Fact]
        public void SetSpanContext()
        {
            var distributedTracer = new Mock<IDistributedTracer>();

            distributedTracer.Setup(t => t.GetRuntimeId()).Returns(Guid.NewGuid().ToString());

            Datadog.Trace.ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(distributedTracer.Object);

            using (var scope = (Scope)Tracer.Instance.StartActive("Test"))
            {
                distributedTracer.Verify(t => t.SetSpanContext(scope.Span.Context), Times.Once);
            }

            distributedTracer.Verify(t => t.SetSpanContext(null), Times.Once);
            distributedTracer.Verify(t => t.SetSpanContext(It.IsAny<SpanContext>()), Times.Exactly(2));
        }

        [Fact]
        public void DistributedContextTakesPrecedenceOverProcessLifetimeRoot()
        {
            var distributedTracer = new Mock<IDistributedTracer>();
            var distributedContext = new SpanContext(123, 456, SamplingPriority.UserKeep);
            var processRoot = new TestProcessLifetimeRootSpan(new SpanContext(789, 1011, SamplingPriority.UserKeep));
            distributedTracer.Setup(t => t.GetSpanContext()).Returns(distributedContext);
            distributedTracer.Setup(t => t.GetRuntimeId()).Returns(Guid.NewGuid().ToString());
            Datadog.Trace.ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(distributedTracer.Object);

            var tracer = new Tracer(
                new TracerSettings(),
                Mock.Of<IAgentWriter>(),
                Mock.Of<ITraceSampler>(),
                scopeManager: null,
                statsd: null);
            tracer.TracerManager.InitializeProcessLifetimeRootSpan(() => processRoot);

            using var scope = tracer.StartActiveInternal("distributed-child");

            scope.Span.Context.ParentId.Should().Be(distributedContext.SpanId);
            scope.Span.Context.TraceId128.Should().Be(distributedContext.TraceId128);
        }

        private sealed class TestProcessLifetimeRootSpan : IProcessLifetimeRootSpan
        {
            public TestProcessLifetimeRootSpan(SpanContext spanContext) => SpanContext = spanContext;

            public SpanContext SpanContext { get; }

            public void Complete(Exception exception)
            {
            }
        }
    }
}
