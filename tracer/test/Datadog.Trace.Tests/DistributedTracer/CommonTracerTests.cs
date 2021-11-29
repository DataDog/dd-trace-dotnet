// <copyright file="CommonTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DistributedTracer
{
    public class CommonTracerTests
    {
        [Fact]
        public void LockSamplingPriority()
        {
            var commonTracer = new CommonTracerImpl();

            using (var scope = Tracer.Instance.StartActive("Test"))
            {
                scope.Span.Context.TraceContext.IsSamplingPriorityLocked().Should().BeFalse();

                commonTracer.LockSamplingPriority();

                scope.Span.Context.TraceContext.IsSamplingPriorityLocked().Should().BeTrue();
            }
        }

        [Fact]
        public void LockSamplingPriority_NoActiveTrace()
        {
            var commonTracer = new CommonTracerImpl();

            // Just making sure that it doesn't throw when there is no active trace
            commonTracer.LockSamplingPriority();
        }

        [Fact]
        public void TrySetSamplingPriority_NoTrace()
        {
            var commonTracer = new CommonTracerImpl();

            var expectedSamplingPriority = (int?)SamplingPriority.UserKeep;

            var actualSamplingPriority = commonTracer.TrySetSamplingPriority(expectedSamplingPriority);

            actualSamplingPriority.Should().Be(expectedSamplingPriority, "TrySetSamplingPriority should be pass-through when there is no active trace");
        }

        [Fact]
        public void TrySetSamplingPriority_LockedPriority()
        {
            var commonTracer = new CommonTracerImpl();

            var expectedSamplingPriority = SamplingPriority.UserKeep;

            using var scope = Tracer.Instance.StartActive("Test");
            scope.Span.Context.TraceContext.SetSamplingPriority(expectedSamplingPriority);
            scope.Span.Context.TraceContext.LockSamplingPriority();

            var actualSamplingPriority = (SamplingPriority?)commonTracer.TrySetSamplingPriority((int?)SamplingPriority.UserReject);

            actualSamplingPriority.Should().Be(expectedSamplingPriority, "TrySetSamplingPriority should return the current sampling priority when locked");
        }

        [Fact]
        public void TrySetSamplingPriority()
        {
            var commonTracer = new CommonTracerImpl();

            var expectedSamplingPriority = SamplingPriority.UserKeep;

            using var scope = Tracer.Instance.StartActive("Test");

            var actualSamplingPriority = (SamplingPriority?)commonTracer.TrySetSamplingPriority((int?)expectedSamplingPriority);

            actualSamplingPriority.Should().Be(expectedSamplingPriority);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(expectedSamplingPriority, "TrySetSamplingPriority should have successfully set the active trace sampling priority");
        }

        private class CommonTracerImpl : CommonTracer
        {
        }
    }
}
