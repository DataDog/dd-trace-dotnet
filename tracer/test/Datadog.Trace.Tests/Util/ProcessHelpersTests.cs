// <copyright file="ProcessHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class ProcessHelpersTests
{
    [Fact]
    public void StopsAProcessThatDoesNotFinishWithinTheTimeout()
    {
        // Without the timeout, the process runs for about a minute.
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                          ? new ProcessHelpers.Command("ping", "-n 60 127.0.0.1", timeout: TimeSpan.FromSeconds(1))
                          : new ProcessHelpers.Command("sh", "-c \"exec sleep 60\"", timeout: TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        var output = ProcessHelpers.RunCommand(command);

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeTrue();
        output.ExitCode.Should().Be(-1);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    public void StopsTheChildProcessesThatKeepTheOutputOpen()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        // The shell starts sleep as a child process, which inherits the output pipes.
        var stopwatch = Stopwatch.StartNew();

        var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command("sh", "-c \"sleep 60; echo done\"", timeout: TimeSpan.FromSeconds(1)));

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeTrue();
        output.Output.Should().BeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }
#endif

    [SkippableFact]
    public void StopsAProcessThatDoesNotReadItsInputWithinTheTimeout()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        // More input than the pipe holds, so writing it blocks until the process reads it, after the timeout.
        var input = new string('a', 1024 * 1024);

        var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command("sh", "-c \"sleep 5; cat >/dev/null\"", timeout: TimeSpan.FromMilliseconds(200)), input);

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeTrue();
        output.ExitCode.Should().Be(-1);
    }

    [SkippableFact]
    public void ReturnsTheOutputOfAProcessThatFinishesWithinTheTimeout()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command("sh", "-c \"echo out; echo err 1>&2; exit 3\"", timeout: TimeSpan.FromMinutes(1)));

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeFalse();
        output.ExitCode.Should().Be(3);
        output.Output.Trim().Should().Be("out");
        output.Error.Trim().Should().Be("err");
    }
}
