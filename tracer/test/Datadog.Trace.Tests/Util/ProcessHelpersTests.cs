// <copyright file="ProcessHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class ProcessHelpersTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WhenCallingRunCommand_ShouldTraceProcessStart_ShouldReturnSetValue(bool doNotTrace)
    {
        RunAndIgnoreErrors(doNotTrace);
        ProcessHelpers.ShouldTraceProcessStart().Should().Be(!doNotTrace);

        // the second time, should always be do not trace
        RunAndIgnoreErrors(doNotTrace: false);
        ProcessHelpers.ShouldTraceProcessStart().Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WhenCallingRunCommandAsync_ShouldTraceProcessStart_ShouldReturnSetValue(bool doNotTrace)
    {
        RunAndIgnoreErrorsAsync(doNotTrace);
        ProcessHelpers.ShouldTraceProcessStart().Should().Be(!doNotTrace);

        RunAndIgnoreErrorsAsync(doNotTrace: false);
        ProcessHelpers.ShouldTraceProcessStart().Should().BeTrue();
    }

    private static void RunAndIgnoreErrors(bool doNotTrace)
    {
        try
        {
            var command = new ProcessHelpers.Command("nonexisting1.exe", doNotTrace: doNotTrace);
            _ = ProcessHelpers.RunCommand(command);
        }
        catch (Win32Exception)
        {
             // expected
        }
    }

    private static void RunAndIgnoreErrorsAsync(bool doNotTrace)
    {
        try
        {
            var command = new ProcessHelpers.Command("nonexisting1.exe", doNotTrace: doNotTrace);
            _ = ProcessHelpers.RunCommandAsync(command);
        }
        catch (Win32Exception)
        {
             // expected
        }
    }
}
