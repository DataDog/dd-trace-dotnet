// <copyright file="DatadogSharedFileSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Vendors.Serilog.Debugging;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging;

internal sealed class DatadogSharedFileSink : IFileSink, IDisposable
{
    private readonly TextWriter _output;
    private readonly MutexStream _mutexStream;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;

    public DatadogSharedFileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (fileSizeLimitBytes is < 0)
        {
            throw new ArgumentException("Negative value provided; file size limit must be non-negative");
        }

        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _mutexStream = new MutexStream(path, File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        _output = new StreamWriter(_mutexStream, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent == null)
        {
            throw new ArgumentNullException(nameof(logEvent));
        }

        if (_fileSizeLimitBytes != null)
        {
            if (_mutexStream.Length >= _fileSizeLimitBytes.Value)
            {
                return false;
            }
        }

        _textFormatter.Format(logEvent, _output);
        return true;
    }

    public void Emit(LogEvent logEvent)
    {
        ((IFileSink)this).EmitOrOverflow(logEvent);
    }

    public void Dispose()
    {
        _output.Dispose();
        _mutexStream.Dispose();
    }

    public void FlushToDisk()
    {
        _mutexStream.Flush();
    }

    private class MutexStream : Stream
    {
        private const string MutexNameSuffix = ".serilog";
        private const int MutexWaitTimeout = 10000;
        private readonly FileStream _stream;
        private readonly Mutex _mutex;
        private readonly object _syncRoot;
        private long _mutexCount;

        public MutexStream(string path, FileStream stream)
        {
            _syncRoot = new object();
            _stream = stream;
            var mutexName = Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, ':') + MutexNameSuffix;
            _mutex = new Mutex(false, mutexName);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                lock (_syncRoot)
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
                lock (_syncRoot)
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
                lock (_syncRoot)
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
            lock (_syncRoot)
            {
                if (!TryAcquireMutex())
                {
                    return;
                }

                try
                {
                    _stream.Seek(0, SeekOrigin.End);
                    _stream.Flush(true);
                }
                finally
                {
                    ReleaseMutex();
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            lock (_syncRoot)
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
            lock (_syncRoot)
            {
                if (!TryAcquireMutex())
                {
                    return;
                }

                try
                {
                    _stream.Seek(0, SeekOrigin.End);
                    _stream.Write(buffer, offset, count);
                }
                finally
                {
                    ReleaseMutex();
                }
            }
        }

        public new void Dispose()
        {
            lock (_syncRoot)
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
