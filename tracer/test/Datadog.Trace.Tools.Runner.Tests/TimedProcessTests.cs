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
    [Fact]
    public void StopsAProcessThatDoesNotFinishWithinTheTimeout()
    {
        var stopwatch = Stopwatch.StartNew();

        // A process that can't start throws, so false means it started and was stopped at the timeout.
        var finished = EnvironmentTools.IsWindows()
                           ? TimedProcess.TryRun("ping", "-n 300 127.0.0.1", Path.GetTempPath(), TimeSpan.FromSeconds(1), out _, out _, out _)
                           : TimedProcess.TryRun("sh", "-c \"exec sleep 300\"", Path.GetTempPath(), TimeSpan.FromSeconds(1), out _, out _, out _);

        finished.Should().BeFalse();

        // Far below the 300 seconds the process would take, so the result can't depend on the machine's load.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void ReadsBothStreamsWhenTheProcessFillsStandardErrorFirst()
    {
        // More than a pipe holds on standard error before anything on standard output.
        var finished = EnvironmentTools.IsWindows()
                           ? TimedProcess.TryRun("powershell", "-NoProfile -Command \"[Console]::Error.Write('x' * 200000); [Console]::Out.Write('done')\"", Path.GetTempPath(), TimeSpan.FromMinutes(1), out var output, out var error, out var exitCode)
                           : TimedProcess.TryRun("sh", "-c \"head -c 200000 /dev/zero | tr '\\0' x 1>&2; printf done\"", Path.GetTempPath(), TimeSpan.FromMinutes(1), out output, out error, out exitCode);

        finished.Should().BeTrue();
        output.Should().Be("done");
        error.Should().Be(new string('x', 200000));
        exitCode.Should().Be(0);
    }
}
