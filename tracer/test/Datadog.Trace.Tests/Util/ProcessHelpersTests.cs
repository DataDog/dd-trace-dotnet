// <copyright file="ProcessHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class ProcessHelpersTests
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void StopsAProcessThatDoesNotFinishWithinTheTimeout()
    {
        // Without the timeout, the process runs for about a minute.
        var command = IsWindows
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
    [Fact]
    public void StopsTheChildProcessesThatKeepTheOutputOpen()
    {
        // The shell starts a child process that runs for about a minute and inherits the output pipes.
        var command = IsWindows
                          ? new ProcessHelpers.Command("cmd", "/c \"ping -n 60 127.0.0.1\"", timeout: TimeSpan.FromSeconds(1))
                          : new ProcessHelpers.Command("sh", "-c \"sleep 60; echo done\"", timeout: TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        var output = ProcessHelpers.RunCommand(command);

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeTrue();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }
#endif

    [Fact]
    public void StopsAProcessThatDoesNotReadItsInputWithinTheTimeout()
    {
        // More input than the pipe holds, so writing it blocks until the process reads it, after the timeout.
        var input = new string('a', 1024 * 1024);
        var command = IsWindows
                          ? new ProcessHelpers.Command("ping", "-n 60 127.0.0.1", timeout: TimeSpan.FromMilliseconds(200))
                          : new ProcessHelpers.Command("sh", "-c \"sleep 5; cat >/dev/null\"", timeout: TimeSpan.FromMilliseconds(200));

        var output = ProcessHelpers.RunCommand(command, input);

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeTrue();
        output.ExitCode.Should().Be(-1);
    }

    [Fact]
    public void ReadsBothStreamsWhenTheProcessFillsStandardErrorFirst()
    {
        // More than a pipe holds on standard error before anything on standard output.
        var command = IsWindows
                          ? new ProcessHelpers.Command("powershell", "-NoProfile -Command \"[Console]::Error.Write('x' * 200000); [Console]::Out.Write('done')\"", timeout: TimeSpan.FromMinutes(1))
                          : new ProcessHelpers.Command("sh", "-c \"head -c 200000 /dev/zero | tr '\\0' x 1>&2; printf done\"", timeout: TimeSpan.FromMinutes(1));

        var output = ProcessHelpers.RunCommand(command);

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeFalse();
        output.ExitCode.Should().Be(0);
        output.Output.Should().Be("done");
        output.Error.Should().Be(new string('x', 200000));
    }

    [Fact]
    public void ReturnsTheOutputOfAProcessThatFinishesWithinTheTimeout()
    {
        var command = IsWindows
                          ? new ProcessHelpers.Command("cmd", "/c \"echo out& echo err 1>&2& exit /b 3\"", timeout: TimeSpan.FromMinutes(1))
                          : new ProcessHelpers.Command("sh", "-c \"echo out; echo err 1>&2; exit 3\"", timeout: TimeSpan.FromMinutes(1));

        var output = ProcessHelpers.RunCommand(command);

        output.Should().NotBeNull();
        output!.TimedOut.Should().BeFalse();
        output.ExitCode.Should().Be(3);
        output.Output.Trim().Should().Be("out");
        output.Error.Trim().Should().Be("err");
    }
}
