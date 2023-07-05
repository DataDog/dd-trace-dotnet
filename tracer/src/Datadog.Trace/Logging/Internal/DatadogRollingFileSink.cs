// <copyright file="DatadogRollingFileSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Debugging;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using Datadog.Trace.Vendors.Serilog.Sinks.File;
using Clock = Datadog.Trace.Vendors.Serilog.Sinks.File.Clock;

namespace Datadog.Trace.Logging;

internal sealed class DatadogRollingFileSink : ILogEventSink, IFlushableFileSink, IDisposable
{
    private readonly PathRoller _roller;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;
    private readonly int? _retainedFileCountLimit;
    private readonly Encoding _encoding;
    private readonly bool _rollOnFileSizeLimit;

    private bool _isDisposed;
    private DateTime? _nextCheckpoint;
    private IFileSink _currentFile;
    private int? _currentFileSequence;

    public DatadogRollingFileSink(
        string path,
        ITextFormatter textFormatter,
        long? fileSizeLimitBytes,
        int? retainedFileCountLimit,
        Encoding encoding,
        RollingInterval rollingInterval,
        bool rollOnFileSizeLimit)
    {
        if (path == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(path));
        }

        if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0)
        {
            ThrowHelper.ThrowArgumentException("Negative value provided; file size limit must be non-negative.");
        }

        if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1)
        {
            ThrowHelper.ThrowArgumentException("Zero or negative value provided; retained file count limit must be at least 1.");
        }

        _roller = new PathRoller(path, rollingInterval);
        _textFormatter = textFormatter;
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _retainedFileCountLimit = retainedFileCountLimit;
        _encoding = encoding;
        _rollOnFileSizeLimit = rollOnFileSizeLimit;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            if (logEvent == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logEvent));
            }

            lock (_roller)
            {
                if (_isDisposed)
                {
                    ThrowHelper.ThrowObjectDisposedException("The log file has been disposed.");
                }

                var now = TraceClock.Instance.UtcNow.ToLocalTime().DateTime;
                AlignCurrentFileTo(now);

                while (_currentFile?.EmitOrOverflow(logEvent) == false && _rollOnFileSizeLimit)
                {
                    AlignCurrentFileTo(now, nextSequence: true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void AlignCurrentFileTo(DateTime now, bool nextSequence = false)
    {
        if (!_nextCheckpoint.HasValue)
        {
            OpenFile(now);
        }
        else if (nextSequence || now >= _nextCheckpoint.Value)
        {
            int? minSequence = null;
            if (nextSequence)
            {
                if (_currentFileSequence == null)
                {
                    minSequence = 1;
                }
                else
                {
                    minSequence = _currentFileSequence.Value + 1;
                }
            }

            CloseFile();
            OpenFile(now, minSequence);
        }
    }

    private void OpenFile(DateTime now, int? minSequence = null)
    {
        var currentCheckpoint = _roller.GetCurrentCheckpoint(now);

        // We only try periodically because repeated failures
        // to open log files REALLY slow an app down.
        _nextCheckpoint = _roller.GetNextCheckpoint(now) ?? now.AddMinutes(30);

        var existingFiles = Enumerable.Empty<string>();
        try
        {
            if (Directory.Exists(_roller.LogFileDirectory))
            {
                existingFiles = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                                         .Select(Path.GetFileName);
            }
        }
        catch (DirectoryNotFoundException) { }

        var latestForThisCheckpoint = _roller
                                     .SelectMatches(existingFiles)
                                     .Where(m => m.DateTime == currentCheckpoint)
                                     .OrderByDescending(m => m.SequenceNumber)
                                     .FirstOrDefault();

        var sequence = latestForThisCheckpoint?.SequenceNumber;
        if (minSequence != null)
        {
            if (sequence == null || sequence.Value < minSequence.Value)
            {
                sequence = minSequence;
            }
        }

        const int maxAttempts = 3;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            _roller.GetLogFilePath(now, sequence, out var path);

            try
            {
                _currentFile = new DatadogSharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding);
                _currentFileSequence = sequence;
            }
            catch (IOException ex)
            {
                if (IOErrors.IsLockedFile(ex))
                {
                    SelfLog.WriteLine("File target {0} was locked, attempting to open next in sequence (attempt {1})", path, attempt + 1);
                    sequence = (sequence ?? 0) + 1;
                    continue;
                }

                throw;
            }

            ApplyRetentionPolicy(path);
            return;
        }
    }

    private void ApplyRetentionPolicy(string currentFilePath)
    {
        if (_retainedFileCountLimit == null)
        {
            return;
        }

        var currentFileName = Path.GetFileName(currentFilePath);

        // We consider the current file to exist, even if nothing's been written yet,
        // because files are only opened on response to an event being processed.
        var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                                        .Select(Path.GetFileName)
                                        .Union(new[] { currentFileName });

        var newestFirst = _roller
                         .SelectMatches(potentialMatches)
                         .OrderByDescending(m => m.DateTime)
                         .ThenByDescending(m => m.SequenceNumber)
                         .Select(m => m.Filename);

        var toRemove = newestFirst
                      .Where(n => StringComparer.OrdinalIgnoreCase.Compare(currentFileName, n) != 0)
                      .Skip(_retainedFileCountLimit.Value - 1)
                      .ToList();

        foreach (var obsolete in toRemove)
        {
            var fullPath = Path.Combine(_roller.LogFileDirectory, obsolete);
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Error {0} while processing obsolete log file {1}", ex, fullPath);
            }
        }
    }

    public void Dispose()
    {
        lock (_roller)
        {
            if (_currentFile == null)
            {
                return;
            }

            CloseFile();
            _isDisposed = true;
        }
    }

    private void CloseFile()
    {
        if (_currentFile != null)
        {
            (_currentFile as IDisposable)?.Dispose();
            _currentFile = null;
        }

        _nextCheckpoint = null;
    }

    public void FlushToDisk()
    {
        lock (_roller)
        {
            _currentFile?.FlushToDisk();
        }
    }
}
