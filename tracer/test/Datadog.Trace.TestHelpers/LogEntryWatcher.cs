// <copyright file="LogEntryWatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public class LogEntryWatcher : IDisposable
{
    private readonly ITestOutputHelper? _testOutput;
    private readonly string _logDirectory;
    private readonly string _logFilePattern;
    private StreamReader? _reader;

    public LogEntryWatcher(string logFilePattern, string? logDirectory = null, ITestOutputHelper? testOutput = null)
    {
        _logFilePattern = logFilePattern;
        _testOutput = testOutput;
        _logDirectory = logDirectory ?? DatadogLoggingFactory.GetLogDirectory();

        _reader = OpenReaderOnLatestFile(DateTime.Today);

        if (_reader != null)
        {
            _reader.ReadToEnd();
        }

        _testOutput?.WriteLine("LogEntryWatcher: Could not find file. Will wait for new file.");
    }

    private string CurrentLogFileFullPath => ((FileStream)_reader!.BaseStream).Name;

    public Task WaitForLogEntry(string logEntry, TimeSpan? timeout = null)
    {
        return WaitForLogEntries(new[] { logEntry }, timeout);
    }

    public async Task WaitForLogEntries(string[] logEntries, TimeSpan? timeout = null)
    {
        using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(value: 20));

        var i = 0;
        while (logEntries.Length > i && !cancellationSource.IsCancellationRequested)
        {
            if (_reader == null)
            {
                await Task.Delay(millisecondsDelay: 100);
                _reader = OpenReaderOnLatestFile(DateTime.Today);
                continue;
            }

            var line = await _reader.ReadLineAsync();
            if (line != null)
            {
                if (line.Contains(logEntries[i]))
                {
                    i++;
                }
            }
            else
            {
                await Task.Delay(millisecondsDelay: 100);
                RollOverToNewFileIfAvailable();
            }
        }

        if (i != logEntries.Length)
        {
            throw new TimeoutException(_reader == null ? $"Log file was not found for path: {_logDirectory} with file pattern {_logFilePattern}." : $"Log entry was not found {logEntries[i]} in {CurrentLogFileFullPath} with filter {_logFilePattern}. Cancellation token reached: {cancellationSource.IsCancellationRequested}");
        }
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }

    private StreamReader? OpenReaderOnLatestFile(DateTime minimumLastWriteTime)
    {
        var latestFile = new DirectoryInfo(_logDirectory)
                        .GetFiles(_logFilePattern)
                        .Where(info => info.LastWriteTime > minimumLastWriteTime)
                        .OrderBy(info => info.LastWriteTime)
                        .LastOrDefault();

        if (latestFile == null)
        {
            return null;
        }

        _testOutput?.WriteLine("LogEntryWatcher: Starting to read from log file " + latestFile.FullName);

        var fileStream = new FileStream(
            latestFile.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        return new StreamReader(fileStream, Encoding.UTF8);
    }

    private void RollOverToNewFileIfAvailable()
    {
        // We're purposely avoiding using the FileSystemWatcher here because we found it to be unreliable -
        // it would fail randomly on Windows / .NET 7 (it's `Error` event would fire with a `Win32Exception `).
        var currentFileLastWriteTime = new FileInfo(CurrentLogFileFullPath).LastWriteTime;
        var newerFile = OpenReaderOnLatestFile(currentFileLastWriteTime);
        if (newerFile != null)
        {
            // We've rolled over to a new file, so close the old one and start reading from the new one
            _reader!.Dispose();
            _reader = newerFile;
            _testOutput?.WriteLine("LogEntryWatcher: Rolled over to new log file " + CurrentLogFileFullPath);
        }
    }
}
