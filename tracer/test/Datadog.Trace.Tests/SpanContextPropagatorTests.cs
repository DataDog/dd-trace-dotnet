// <copyright file="SpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanContextPropagatorTests
    {
        [Fact]
        public void SpanContextRoundTrip()
        {
            const ulong expectedTraceId = 1;
            const ulong expectedSpanId = 2;
            const SamplingPriority expectedSamplingPriority = SamplingPriority.UserKeep;
            const string expectedOrigin = "origin";

            var spanContext = new SpanContext(expectedTraceId, expectedSpanId, expectedSamplingPriority, null, expectedOrigin);

            var result = SpanContextPropagator.Instance.Extract(spanContext);

            result.TraceId.Should().Be(expectedTraceId);
            result.SpanId.Should().Be(expectedSpanId);
            result.SamplingPriority.Should().Be(expectedSamplingPriority);
            result.Origin.Should().Be(expectedOrigin);
        }
    }
}
