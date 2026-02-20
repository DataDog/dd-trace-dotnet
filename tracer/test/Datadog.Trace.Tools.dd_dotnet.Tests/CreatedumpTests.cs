// <copyright file="CreatedumpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.Tests;

public class CreatedumpTests
{
    [SkippableTheory]
    [InlineData("invalid", false, 0, null, null, IntPtr.Zero)]
    [InlineData("--crashthread 5 --blabla 100 --signal 3 --aaaaaa", true, 100, 3, 5, 42424242)]
    [InlineData("--crashthread 5 --blabla 99999 --signal 3 --aaaaaa 99998", false, 0, 3, 5, IntPtr.Zero)] // Two potential PIDs, that probably don't exist
    [InlineData("10", true, 10, null, null, IntPtr.Zero)]
    public void ParseCommandLine(string commandLine, bool expectedResult, int expectedPid, int? expectedSignal, int? expectedCrashThread, IntPtr? expectedThreadContext)
    {
        var result = CreatedumpCommand.ParseArguments(commandLine.Split(' '), out var pid, out var signal, out var crashThread, out var threadContext);

        result.Should().Be(expectedResult);

        if (result)
        {
            pid.Should().Be(expectedPid);
            signal.Should().Be(expectedSignal);
            crashThread.Should().Be(expectedCrashThread);
            threadContext.Should().Be(expectedThreadContext);
        }
    }

    [SkippableFact]
    public void UseValidPidIfMultiple()
    {
        // When there are multiple arguments looking like PIDs, the parser tests them and return the first valid one
        var currentPid = Process.GetCurrentProcess().Id;
        var commandLine = $"999999 999998 {currentPid} 999997";

        var result = CreatedumpCommand.ParseArguments(commandLine.Split(' '), out var pid, out var signal, out var crashThread, out var threadContext);

        result.Should().Be(true);
        pid.Should().Be(currentPid);
        signal.Should().BeNull();
        crashThread.Should().BeNull();
        threadContext.Should().Be(IntPtr.Zero);
    }
}
