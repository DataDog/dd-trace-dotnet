// <copyright file="LogEntryWatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;

namespace Datadog.Trace.TestHelpers;

public class LogEntryWatcher : IDisposable
{
    private readonly FileSystemWatcher _fileWatcher;

    // When the log file gets too big, the logger will roll over and create a new file, but we may still be reading from the old file
    // We must finish reading from the old one before switching to the new one, hence the queue
    private readonly ConcurrentQueue<StreamReader> _readers;
    private readonly DateTime _initialLogFileWriteTime;
    private StreamReader _activeReader;

    public LogEntryWatcher(string logFilePattern, string logDirectory = null)
    {
        var logPath = logDirectory ?? DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance);
        _fileWatcher = new FileSystemWatcher { Path = logPath, Filter = logFilePattern, EnableRaisingEvents = true };
        _readers = new();
        var lastFile = GetLastWrittenLogFile(logFilePattern, logPath);
        _initialLogFileWriteTime = DateTime.Now;

        if (lastFile != null && lastFile.LastWriteTime.Date.Day == _initialLogFileWriteTime.Day)
        {
            var reader = OpenStream(lastFile.FullName);
            reader.ReadToEnd();
            _readers.Enqueue(reader);
        }

        _fileWatcher.Created += NewLogFileCreated;
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();

        while (_readers.TryDequeue(out var reader))
        {
            reader.Dispose();
        }

        Interlocked.Exchange(ref _activeReader, null)?.Dispose();
    }

    public async Task<string> WaitForLogEntry(string logEntry, TimeSpan? timeout = null)
    {
        var logs = await WaitForLogEntries(new[] { logEntry }, timeout);
        return logs.Single();
    }

    public async Task<string[]> WaitForLogEntries(string[] logEntries, TimeSpan? timeout = null)
    {
        using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));

        var i = 0;

        var foundLogs = new string[logEntries.Length];

        while (logEntries.Length > i && !cancellationSource.IsCancellationRequested)
        {
            if (_activeReader == null)
            {
                if (!_readers.TryDequeue(out _activeReader))
                {
                    await Task.Delay(100);
                    continue;
                }
            }

            var line = await _activeReader.ReadLineAsync();

            if (line != null)
            {
                if (line.Contains(logEntries[i]))
                {
                    foundLogs[i] = line;
                    i++;
                }
            }
            else
            {
                // Ensure we're still reading from the latest file, otherwise switch to the new reader
                if (_readers.TryDequeue(out var reader))
                {
                    Interlocked.Exchange(ref _activeReader, reader)?.Dispose();
                    continue;
                }

                await Task.Delay(100);
            }
        }

        if (i != logEntries.Length)
        {
            var message = GetDetailedExceptionMessage(logEntries, timeout, foundLogs, i, cancellationSource.IsCancellationRequested);
            throw new InvalidOperationException(message);
        }

        return foundLogs;
    }

    private StreamReader OpenStream(string filePath)
    {
        var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        return new StreamReader(fileStream, Encoding.UTF8);
    }

    private void NewLogFileCreated(object sender, FileSystemEventArgs e)
    {
        _readers.Enqueue(OpenStream(e.FullPath));
    }

    private FileInfo GetLastWrittenLogFile(string logFilePattern, string logPath)
    {
        var dir = new DirectoryInfo(logPath);
        var lastFile = dir
                      .GetFiles(logFilePattern)
                      .OrderBy(info => info.LastWriteTime)
                      .LastOrDefault();
        return lastFile;
    }

    private string GetDetailedExceptionMessage(string[] logEntries, TimeSpan? timeout, string[] foundLogs, int i, bool isCanceled)
    {
        var foundEntries = foundLogs.Take(i).Where(log => log != null).ToArray();
        var missingEntries = logEntries.Skip(i).ToArray();

        var lastFile = GetLastWrittenLogFile(_fileWatcher.Filter, _fileWatcher.Path);

        var lastFileName = lastFile != null && lastFile.LastWriteTime > _initialLogFileWriteTime
                               ? $"{lastFile.Name} (Last write: {lastFile.LastWriteTime})"
                               : "No relevant log files found. No new entries have been written since monitoring began.";

        var message = _readers.IsEmpty
                          ? $"Log file was not found for path: {_fileWatcher.Path} with file pattern {_fileWatcher.Filter}.{Environment.NewLine}Timeout: {timeout?.TotalSeconds ?? 20}s.{Environment.NewLine}Found {i}/{logEntries.Length} expected log entries.{Environment.NewLine}Last file: {lastFileName}"
                          : $"Timed out waiting for log entries in {_fileWatcher.Path} with filter {_fileWatcher.Filter}.{Environment.NewLine}Found {i}/{logEntries.Length} expected entries.{Environment.NewLine}Timeout: {timeout?.TotalSeconds ?? 20}s.{Environment.NewLine}Cancellation: {isCanceled}.{Environment.NewLine}Last file: {lastFileName}";

        message += $"{Environment.NewLine}Found entries ({foundEntries.Length}):{Environment.NewLine}{string.Join(Environment.NewLine, foundEntries.Select((log, index) => $"[{index}] {log}"))}";
        message += $"{Environment.NewLine}Missing entries ({missingEntries.Length}):{Environment.NewLine}{string.Join(Environment.NewLine, missingEntries.Select((entry, index) => $"[{i + index}] {entry}"))}";
        message += Environment.NewLine;
        return message;
    }
}
