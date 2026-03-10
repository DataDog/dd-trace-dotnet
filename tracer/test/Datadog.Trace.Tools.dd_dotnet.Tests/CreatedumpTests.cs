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
    [InlineData("invalid", false, 0, null, null, null, null)]
    [InlineData("--crashthread 5 --blabla 100 --signal 3 --aaaaaa --code 4 --dd-native-exception-code 3221225477", true, 100, 3, 4, 5, 0xC0000005)]
    [InlineData("--crashthread 5 --blabla 99999 --signal 3 --aaaaaa 99998 --code 4 --dd-native-exception-code 3221225477", false, 0, 3, 4, 5, 0xC0000005)] // Two potential PIDs, that probably don't exist
    [InlineData("10", true, 10, null, null, null, null)]
    public void ParseCommandLine(string commandLine, bool expectedResult, int expectedPid, int? expectedSignal, int? expectedSignalCode, int? expectedCrashThread, uint? expectedExceptionCode)
    {
        var result = CreatedumpCommand.ParseArguments(commandLine.Split(' '), out var pid, out var signal, out var signalCode, out var crashThread, out var exceptionCode);

        result.Should().Be(expectedResult);

        if (result)
        {
            pid.Should().Be(expectedPid);
            signal.Should().Be(expectedSignal);
            signalCode.Should().Be(expectedSignalCode);
            crashThread.Should().Be(expectedCrashThread);
            exceptionCode.Should().Be(expectedExceptionCode);
        }
    }

    [SkippableFact]
    public void UseValidPidIfMultiple()
    {
        // When there are multiple arguments looking like PIDs, the parser tests them and return the first valid one
        var currentPid = Process.GetCurrentProcess().Id;
        var commandLine = $"999999 999998 {currentPid} 999997";

        var result = CreatedumpCommand.ParseArguments(commandLine.Split(' '), out var pid, out var signal, out var signalCode, out var crashThread, out var exceptionCode);

        result.Should().Be(true);
        pid.Should().Be(currentPid);
        signal.Should().BeNull();
        signalCode.Should().BeNull();
        crashThread.Should().BeNull();
        exceptionCode.Should().BeNull();
    }
}
