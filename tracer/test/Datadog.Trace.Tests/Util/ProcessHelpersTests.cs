// <copyright file="ProcessHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class ProcessHelpersTests
{
    [SkippableFact]
    public void StopsAProcessThatDoesNotFinishWithinTheTimeout()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);
        var stopwatch = Stopwatch.StartNew();

        var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command("sh", "-c \"exec sleep 60\"", timeout: TimeSpan.FromSeconds(1)));

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeTrue();
        output.ExitCode.Should().Be(-1);
        stopwatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(900)).And.BeLessThan(TimeSpan.FromSeconds(30));
    }

    [SkippableFact]
    public void ReadsBothStreamsWhenTheProcessFillsStandardErrorFirst()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        // More than a pipe buffer on standard error before anything on standard output.
        var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command("sh", "-c \"head -c 200000 /dev/zero 1>&2; echo done\"", timeout: TimeSpan.FromMinutes(1)));

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeFalse();
        output.ExitCode.Should().Be(0);
        output.Output.Trim().Should().Be("done");
        output.Error.Length.Should().Be(200000);
    }

    [SkippableFact]
    public void PassesTheInputToTheProcess()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command("cat", timeout: TimeSpan.FromMinutes(1)), input: "hello");

        output.Should().NotBeNull();
        output!.ExitCode.Should().Be(0);
        output.Output.Should().Be("hello");
    }
}
