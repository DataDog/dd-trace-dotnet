// <copyright file="TimedProcessTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class TimedProcessTests
{
    [SkippableFact]
    public void StopsAProcessThatDoesNotFinishWithinTheTimeout()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);
        var stopwatch = Stopwatch.StartNew();

        TimedProcess.TryRun("sh", "-c \"exec sleep 60\"", Path.GetTempPath(), TimeSpan.FromSeconds(1), out _, out _, out _).Should().BeFalse();

        // It waited for the timeout, so the process did start, and didn't wait for the process to finish.
        stopwatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(900)).And.BeLessThan(TimeSpan.FromSeconds(30));
    }

    [SkippableFact]
    public void ReadsBothStreamsWhenTheProcessFillsStandardErrorFirst()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        // More than a pipe buffer on standard error before anything on standard output.
        TimedProcess.TryRun("sh", "-c \"head -c 200000 /dev/zero 1>&2; echo done\"", Path.GetTempPath(), TimeSpan.FromMinutes(1), out var output, out var error, out var exitCode).Should().BeTrue();

        output.Trim().Should().Be("done");
        error.Length.Should().Be(200000);
        exitCode.Should().Be(0);
    }
}
