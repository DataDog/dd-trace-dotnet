// <copyright file="TotalProcessorCountTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics;

public class TotalProcessorCountTests
{
    [Fact]
    public void Value_IsPositive()
    {
        TotalProcessorCount.Value.Should().BePositive();
    }

    [Fact]
    public void Value_IsStableAcrossCalls()
    {
        var first = TotalProcessorCount.Value;
        var second = TotalProcessorCount.Value;

        second.Should().Be(first);
    }

    [Fact]
    public void Resolve_NeverThrows_AndReturnsPositive()
    {
        var result = TotalProcessorCount.GetTotalProcessorCount();

        result.Should().BePositive();
    }

    [SkippableFact]
    public void GetTotalProcessorCount_ReturnsExpectedValueInCI()
    {
        // NOTE: this test will fail locally unless you happen to happen to have 4 logical processors!
        var result = TotalProcessorCount.GetTotalProcessorCount();

        // All CI machines currently have 4 CPUs, but that won't always be the case, so update this test as appropriate!
        const int expectedCpus = 4;
        result.Should().Be(expectedCpus);
    }

    [SkippableFact]
    public void GetTotalProcessorCount_OnWindows_SucceedsAndReturnsPositive()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var result = TotalProcessorCount.WindowsProcessorCount.GetTotalProcessorCount();

        result.Should().BePositive();
    }

    [SkippableFact]
    public void Linux_GetTotalProcessorCount_SucceedsAndReturnsPositive()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Linux);

        var result = TotalProcessorCount.LinuxProcessorCount.GetTotalProcessorCount();

        result.Should().BePositive();
    }

    [SkippableFact]
    public void MacOs_GetTotalProcessorCount_SucceedsAndReturnsPositive()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.MacOs);

        var result = TotalProcessorCount.MacOsProcessorCount.GetTotalProcessorCount();

        result.Should().BePositive();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\n", null)]
    [InlineData("0", 1)]
    [InlineData("0-7", 8)]
    [InlineData("0-3,8-11", 8)]
    [InlineData("0,2,4", 3)]
    [InlineData("0-3,5,7-8", 7)]
    [InlineData("0-7\n", 8)]
    [InlineData("  0-7  ", 8)]
    [InlineData("0-7,", null)]
    [InlineData("abc", null)]
    [InlineData("7-3", null)]
    [InlineData("-1-3", null)]
    [InlineData("0--3", null)]
    public void Linux_TryParseOnlineCpuRanges_ResolvesExpectedValue(string contents, int? expectedCount)
    {
        var result = TotalProcessorCount.LinuxProcessorCount.TryParseOnlineCpuRanges(contents.AsSpan());

        result.Should().Be(expectedCount);
    }
}
#endif
