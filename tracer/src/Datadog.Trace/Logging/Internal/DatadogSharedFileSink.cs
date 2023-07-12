// <copyright file="DatadogSharedFileSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Debugging;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging;

/// <summary>
/// Datadog Shared File Sink is based on SharedFileSink code but instead of mutex locking for every log item
/// the mutex is acquire in each flush, reducing the impact of the logger while keeping the mutex logic
/// </summary>
internal sealed class DatadogSharedFileSink : IFileSink, IDisposable
{
    private const int BufferSize = 8192;
    private readonly TextWriter _output;
    private readonly MutexStream _mutexStream;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;

    public DatadogSharedFileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null)
    {
        if (path == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(path));
        }

        if (fileSizeLimitBytes is < 0)
        {
            ThrowHelper.ThrowArgumentException("Negative value provided; file size limit must be non-negative");
        }

        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _mutexStream = new MutexStream(path, new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize: BufferSize));
        _output = new StreamWriter(_mutexStream, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: BufferSize);
    }

    ~DatadogSharedFileSink()
    {
        Dispose();
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(logEvent));
        }

        if (_fileSizeLimitBytes != null && _mutexStream.Length >= _fileSizeLimitBytes.Value)
        {
            return false;
        }

        if (logEvent.Level >= LogEventLevel.Error)
        {
            _output.Flush();
        }

        _textFormatter.Format(logEvent, _output);
        return true;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(logEvent));
        }

        if (_fileSizeLimitBytes == null || _mutexStream.Length < _fileSizeLimitBytes.Value)
        {
            if (logEvent.Level >= LogEventLevel.Error)
            {
                _output.Flush();
            }

            _textFormatter.Format(logEvent, _output);
        }
    }

    public void Dispose()
    {
        _output?.Dispose();
        _mutexStream?.Dispose();
    }

    public void FlushToDisk()
    {
        _output.Flush();
    }

    private class MutexStream : Stream
    {
        private const string MutexNameSuffix = ".serilog";
        private const int MutexWaitTimeout = 10000;
        private readonly FileStream _stream;
        private readonly Mutex _mutex;
        private int _mutexCount;

        public MutexStream(string path, FileStream stream)
        {
            _stream = stream;
            var mutexName = Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, ':') + MutexNameSuffix;
            _mutex = new Mutex(false, mutexName);
        }

        public override bool CanRead => false;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                lock (_mutex)
                {
                    if (!TryAcquireMutex())
                    {
                        return -1;
                    }

                    try
                    {
                        return _stream.Length;
                    }
                    finally
                    {
                        ReleaseMutex();
                    }
                }
            }
        }

        public override long Position
        {
            get
            {
                lock (_mutex)
                {
                    if (!TryAcquireMutex())
                    {
                        return -1;
                    }

                    try
                    {
                        return _stream.Position;
                    }
                    finally
                    {
                        ReleaseMutex();
                    }
                }
            }

            set
            {
                lock (_mutex)
                {
                    if (!TryAcquireMutex())
                    {
                        return;
                    }

                    try
                    {
                        _stream.Position = value;
                    }
                    finally
                    {
                        ReleaseMutex();
                    }
                }
            }
        }

        public override void Flush()
        {
            // Flush is done in every write so is nothing is needed here.
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            lock (_mutex)
            {
                if (!TryAcquireMutex())
                {
                    return;
                }

                try
                {
                    _stream.SetLength(value);
                }
                finally
                {
                    ReleaseMutex();
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_mutex)
            {
                if (!TryAcquireMutex())
                {
                    return;
                }

                try
                {
                    _stream.Seek(0, SeekOrigin.End);
                    _stream.Write(buffer, offset, count);
                    _stream.Flush(true);
                }
                finally
                {
                    ReleaseMutex();
                }
            }
        }

        public new void Dispose()
        {
            lock (_mutex)
            {
                _stream.Dispose();
                _mutex.Dispose();
            }

            base.Dispose();
        }

        private bool TryAcquireMutex()
        {
            try
            {
                if (Interlocked.Increment(ref _mutexCount) == 1 && !_mutex.WaitOne(MutexWaitTimeout))
                {
                    Interlocked.Decrement(ref _mutexCount);
                    SelfLog.WriteLine("Shared file mutex could not be acquired within {0} ms", MutexWaitTimeout);
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                SelfLog.WriteLine("Inherited shared file mutex after abandonment by another process");
            }

            return true;
        }

        private void ReleaseMutex()
        {
            if (Interlocked.Decrement(ref _mutexCount) == 0)
            {
                _mutex.ReleaseMutex();
            }
        }
    }
}
