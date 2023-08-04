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

    private StreamReader _activeReader;

    public LogEntryWatcher(string logFilePattern, string logDirectory = null)
    {
        var logPath = logDirectory ?? DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance);
        _fileWatcher = new FileSystemWatcher { Path = logPath, Filter = logFilePattern, EnableRaisingEvents = true };
        _readers = new();

        var dir = new DirectoryInfo(logPath);
        var lastFile = dir
                      .GetFiles(logFilePattern)
                      .OrderBy(info => info.LastWriteTime)
                      .LastOrDefault();

        if (lastFile != null && lastFile.LastWriteTime.Date == DateTime.Today)
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
            throw new InvalidOperationException(_readers.IsEmpty ? $"Log file was not found for path: {_fileWatcher.Path} with file pattern {_fileWatcher.Filter}. Logs read so far: {string.Join("\r\n", foundLogs)}" : $"Log entry was not found {logEntries[i]} in {_fileWatcher.Path} with filter {_fileWatcher.Filter}. Cancellation token reached: {cancellationSource.IsCancellationRequested}");
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
}
