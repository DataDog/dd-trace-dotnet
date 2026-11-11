// <copyright file="WafRunTotalsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;
using Datadog.Trace.AppSec.Waf;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class WafRunTotalsTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 999, 0)]
    [InlineData(100, 1_900, 190)]
    [InlineData(3, 1_000_000, 3_000)]
    public void GivenRuns_WhenTheTotalsAreRead_ThenOnlyTheTotalIsConvertedToMicroseconds(int runs, long nanosPerRun, ulong expectedMicroseconds)
    {
        var totals = default(WafRunTotals);
        var ticksPerRun = Stopwatch.Frequency * nanosPerRun / 1_000_000_000;
        for (var i = 0; i < runs; i++)
        {
            totals.AddDuration((ulong)nanosPerRun);
            totals.AddDurationWithBindings(ticksPerRun);
        }

        totals.DurationMicroseconds.Should().Be(expectedMicroseconds);
        totals.DurationWithBindingsMicroseconds.Should().BeCloseTo(expectedMicroseconds, 1);
    }
}
