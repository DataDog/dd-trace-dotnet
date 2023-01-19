// <copyright file="LogEntryWatcherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

/// <summary>
/// The <see cref="LogEntryWatcher"/> is only used in integration tests, not production code,
/// but it is a critical enough piece of our integration tests that it warrants its own testing,
/// since if it doesn't behave as expected, it can cause our integration tests to fail in mysterious and random ways.
/// </summary>
public class LogEntryWatcherTests
{
    private readonly string _folder;

    public LogEntryWatcherTests()
    {
        _folder = GetTemporaryDirectory();
    }

    [Fact]
    public async Task ReadsContinuouslyFromSameFile()
    {
        var logEntryWatcher = new LogEntryWatcher("foo*.txt", _folder);

        WriteToLog("foo1.txt", new[] { "a", "b", "c" });

        await logEntryWatcher.WaitForLogEntry("a", TimeSpan.FromSeconds(value: 5));

        // 'd' is not there yet
        await Assert.ThrowsAsync<TimeoutException>(async () => await logEntryWatcher.WaitForLogEntry("d", TimeSpan.Zero));

        WriteToLog("foo1.txt", new[] { "d" });

        // 'd' is now there
        await logEntryWatcher.WaitForLogEntry("d", TimeSpan.FromSeconds(value: 5));
    }

    [Fact]
    public async Task IgnoresLogEntriesThatWereWrittenBeforeItWasCreated()
    {
        WriteToLog("foo1.txt", new[] { "a", "b", "c" });

        var logEntryWatcher = new LogEntryWatcher("foo*.txt", _folder);

        // 'a' was written before the watcher was created, so it should be ignored,
        // since we assume it was written by a previous test.
        await Assert.ThrowsAsync<TimeoutException>(async () => await logEntryWatcher.WaitForLogEntry("a", TimeSpan.Zero));
    }

    [Fact]
    public async Task RollsOverToNextFileAutomatically()
    {
        var logEntryWatcher = new LogEntryWatcher("foo*.txt", _folder);

        WriteToLog("foo1.txt", new[] { "a", "b", "c" });

        await logEntryWatcher.WaitForLogEntry("a", TimeSpan.FromSeconds(value: 5));

        WriteToLog("foo2.txt", new[] { "d" });

        await logEntryWatcher.WaitForLogEntry("d", TimeSpan.FromSeconds(value: 5));
    }

    private static string GetTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private void WriteToLog(string logFileName, string[] lines)
    {
        var filePath = Path.Combine(_folder, logFileName);
        File.AppendAllLines(filePath, lines);
    }
}
