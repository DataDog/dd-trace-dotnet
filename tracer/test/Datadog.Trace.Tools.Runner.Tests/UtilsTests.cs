// <copyright file="UtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class UtilsTests
{
    public static IEnumerable<object?[]> ParseWhereIsOutputData()
    {
        yield return ["dotnet: /usr/bin/dotnet /usr/lib/dotnet /etc/dotnet", new[] { "/usr/bin/dotnet", "/usr/lib/dotnet", "/etc/dotnet" }];
        yield return ["git: /usr/bin/git", new[] { "/usr/bin/git" }];
        yield return ["cmake: /usr/bin/cmake /usr/lib/cmake /usr/share/cmake", new[] { "/usr/bin/cmake", "/usr/lib/cmake", "/usr/share/cmake" }];
        yield return ["docker:", Array.Empty<string>()];
        yield return [string.Empty, Array.Empty<string>()];
        yield return [null, Array.Empty<string>()];
    }

    [Theory]
    [MemberData(nameof(ParseWhereIsOutputData))]
    public void ParseWhereisOutputTests(string line, string[] expectedPaths)
    {
        var actualPath = ProcessHelpers.ParseWhereisOutput(line);
        actualPath.Should().BeEquivalentTo(expectedPaths);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void RunProcessTimeoutKillsDescendantTree()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var childPidPath = Path.Combine(directory, "child.pid");
        try
        {
            var startInfo = new ProcessStartInfo { UseShellExecute = false };
            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "powershell.exe";
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add($"$child = Start-Process powershell.exe -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 30' -PassThru; Set-Content -NoNewline -Path '{childPidPath}' -Value $child.Id; Wait-Process -Id $child.Id");
            }
            else
            {
                startInfo.FileName = "/bin/sh";
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add($"sleep 30 & child=$!; printf %s $child > '{childPidPath}'; wait $child");
            }

            var stopwatch = Stopwatch.StartNew();
            var result = Utils.RunProcess(startInfo, CancellationToken.None, TimeSpan.FromSeconds(1));
            stopwatch.Stop();

            result.TimedOut.Should().BeTrue();
            result.TreeKillAttempted.Should().BeTrue();
            result.TreeKillSucceeded.Should().BeTrue();
            result.Reaped.Should().BeTrue();
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
            File.Exists(childPidPath).Should().BeTrue();
            int.TryParse(File.ReadAllText(childPidPath), out var childProcessId).Should().BeTrue();
            WaitForProcessExit(result.RootProcessId).Should().BeTrue();
            WaitForProcessExit(childProcessId).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool WaitForProcessExit(int processId)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
    }
#endif
}
