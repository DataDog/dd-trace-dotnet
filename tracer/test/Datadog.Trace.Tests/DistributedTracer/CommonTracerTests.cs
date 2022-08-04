// <copyright file="CommonTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DistributedTracer
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class CommonTracerTests
    {
        [Fact]
        public void SetSamplingPriority()
        {
            var commonTracer = new CommonTracerImpl();

            var expectedSamplingPriority = SamplingPriorityValues.UserKeep;
            var expectedSamplingDecision = new SamplingDecision(expectedSamplingPriority);

            using var scope = (Scope)Tracer.Instance.StartActive("Test");

            commonTracer.SetSamplingPriority(expectedSamplingPriority);

            var samplingDecision = scope.Span.Context.TraceContext.SamplingDecision;
            samplingDecision?.Should().Be(expectedSamplingDecision, "SetSamplingPriority should have successfully set the active trace sampling priority");
        }

        private class CommonTracerImpl : CommonTracer
        {
        }
    }
}
