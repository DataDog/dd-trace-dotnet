// <copyright file="SamplingHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling;

public class SamplingHelpersTests
{
    // see https://github.com/DataDog/system-tests/blob/main/tests/fixtures/sampling_rates.csv
    [Theory]
    [InlineData(1, 0.5, true)]                     // Test very small traceID
    [InlineData(10, 0.5, false)]                   // Test very small traceID
    [InlineData(100, 0.5, true)]                   // Test very small traceID
    [InlineData(1000, 0.5, true)]                  // Test very small traceID
    [InlineData(18444899399302180860, 0.5, false)] // Test random very large traceID
    [InlineData(18444899399302180861, 0.5, false)] // Test random very large traceID
    [InlineData(18444899399302180862, 0.5, true)]  // Test random very large traceID
    [InlineData(18444899399302180863, 0.5, true)]  // Test random very large traceID
    [InlineData(18446744073709551615, 0.5, false)] // Test the maximum traceID value 2**64-1
    [InlineData(9223372036854775809, 0.5, false)]  // Test 2**63+1
    [InlineData(9223372036854775807, 0.5, true)]   // Test 2**63-1
    [InlineData(4611686018427387905, 0.5, false)]  // Test 2**62+1
    [InlineData(4611686018427387903, 0.5, false)]  // Test 2**62-1
    [InlineData(646771306295669658, 0.5, true)]    // random traceIDs
    [InlineData(1882305164521835798, 0.5, true)]   // random traceIDs
    [InlineData(5198373796167680436, 0.5, false)]  // random traceIDs
    [InlineData(6272545487220484606, 0.5, true)]   // random traceIDs
    [InlineData(8696342848850656916, 0.5, true)]   // random traceIDs
    [InlineData(10197320802478874805, 0.5, true)]  // random traceIDs
    [InlineData(10350218024687037124, 0.5, true)]  // random traceIDs
    [InlineData(12078589664685934330, 0.5, false)] // random traceIDs
    [InlineData(13794769880582338323, 0.5, true)]  // random traceIDs
    [InlineData(14629469446186818297, 0.5, false)] // random traceIDs
    public void SampleByRate(ulong traceId, double rate, bool expected)
    {
        SamplingHelpers.SampleByRate(traceId, rate).Should().Be(expected);
    }
}
