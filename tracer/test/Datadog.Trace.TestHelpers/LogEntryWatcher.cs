// <copyright file="LogEntryWatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public class LogEntryWatcher : IDisposable
{
    private readonly ITestOutputHelper _testOutput;
    private readonly FileSystemWatcher _fileWatcher;
    private StreamReader _reader;
    private DirectoryInfo _dir;

    public LogEntryWatcher(string logFilePattern, string logDirectory = null, ITestOutputHelper testOutput = null)
    {
        _testOutput = testOutput;
        var logPath = logDirectory ?? DatadogLoggingFactory.GetLogDirectory();
        _fileWatcher = new FileSystemWatcher { Path = logPath, Filter = logFilePattern, EnableRaisingEvents = true };

        _dir = new DirectoryInfo(logPath);
        var lastFile = _dir
                      .GetFiles(logFilePattern)
                      .OrderBy(info => info.LastWriteTime)
                      .LastOrDefault();

        if (lastFile != null && lastFile.LastWriteTime.Date == DateTime.Today)
        {
            SetStream(lastFile.FullName);
            _reader.ReadToEnd();
            _testOutput?.WriteLine("LogEntryWatcher: Read file to end.");
        }

        _testOutput?.WriteLine("LogEntryWatcher: Could not find file. " + GetDiagnosticMessage());
        _fileWatcher.Created += NewLogFileCreated;
        _fileWatcher.Error += FileWatcherError;
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _reader?.Dispose();
    }

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
            }
        }

        if (i != logEntries.Length)
        {
            throw new InvalidOperationException(_reader == null ? $"Log file was not found for path: {_fileWatcher.Path} with file pattern {_fileWatcher.Filter}. {GetDiagnosticMessage()}" : $"Log entry was not found {logEntries[i]} in {_fileWatcher.Path} with filter {_fileWatcher.Filter}. Cancellation token reached: {cancellationSource.IsCancellationRequested}");
        }
    }

    private string GetDiagnosticMessage()
    {
        var listOfFilesInFolder = string.Join(", ", _dir.GetFiles().Select(f => f.Name));
        var message = $"LogEntryWatcher: Searched directory: {_dir.FullName} which currently contains the following files files: {listOfFilesInFolder}";
        return message;
    }

    private void SetStream(string filePath)
    {
        var reader = _reader;

        try
        {
            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            _reader = new StreamReader(fileStream, Encoding.UTF8);
        }
        finally
        {
            reader?.Dispose();
        }
    }

    private void NewLogFileCreated(object sender, FileSystemEventArgs e)
    {
        _testOutput?.WriteLine("LogEntryWatcher: Found file {0}", e.FullPath);
        SetStream(e.FullPath);
    }

    private void FileWatcherError(object sender, ErrorEventArgs e)
    {
        _testOutput.WriteLine("LogEntryWatcher: There was an error! THAT EXPLAINS IT. " + e.GetException());
    }
}
