// <copyright file="DistributedTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler;
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

            ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(distributedTracer.Object);

            using var parentScope = Tracer.Instance.StartActive("Parent");

            using var scope = (Scope)Tracer.Instance.StartActive("Test");

            distributedTracer.Verify(t => t.GetSpanContext(), Times.Exactly(2));
            scope.Span.TraceId.Should().Be(spanContext.TraceId);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(spanContext.SamplingPriority);
        }

        [Fact]
        public void SetSpanContext()
        {
            var distributedTracer = new Mock<IDistributedTracer>();

            distributedTracer.Setup(t => t.GetRuntimeId()).Returns(Guid.NewGuid().ToString());

            ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(distributedTracer.Object);

            using (var scope = (Scope)Tracer.Instance.StartActive("Test"))
            {
                distributedTracer.Verify(t => t.SetSpanContext(scope.Span.Context), Times.Once);
            }

            distributedTracer.Verify(t => t.SetSpanContext(null), Times.Once);
            distributedTracer.Verify(t => t.SetSpanContext(It.IsAny<SpanContext>()), Times.Exactly(2));
        }
    }
}
