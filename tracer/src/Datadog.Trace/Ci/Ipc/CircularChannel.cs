// <copyright file="CircularChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.Ipc;

internal partial class CircularChannel : IChannel
{
    private const int DefaultBufferSize = 65536;
    private const int HeaderSize = 4;

    private const int PollingInterval = 10;
    private const int MutexTimeout = 5000;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CircularChannel));

    private readonly MemoryMappedFile _mmf;
    private readonly Mutex _mutex;
    private readonly int _bufferSize;
    private bool _disposed;

    private Writer? _writer;
    private Receiver? _receiver;

    public CircularChannel(string fileName)
        : this(fileName, DefaultBufferSize)
    {
    }

    public CircularChannel(string fileName, int bufferSize)
    {
        // Check if the file name is an absolute path, if not let's use a temporary directory
        if (!Path.IsPathRooted(fileName))
        {
            if (FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux)
            {
                // Use /dev/shm to store the memory mapped file on Linux
                fileName = Path.Combine("/dev/shm", Path.GetFileName(fileName));
            }
            else
            {
                var folder = Path.Combine(Path.GetTempPath(), "shm");
                Directory.CreateDirectory(folder);
                fileName = Path.Combine(folder, Path.GetFileName(fileName));
            }
        }

        _bufferSize = bufferSize;
        var fileInfo = new FileInfo(fileName);
        if (fileInfo.Exists && fileInfo.Length != _bufferSize)
        {
            // Resize file if it exists but is too small
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(_bufferSize);
        }

        _mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.OpenOrCreate, null, _bufferSize);
        _mutex = new Mutex(false, $"{Path.GetFileNameWithoutExtension(fileName)}.mutex");
        InitializeHeader();
    }

    protected int BufferSize => _bufferSize;

    public int BufferBodySize => _bufferSize - HeaderSize;

    private void InitializeHeader()
    {
        var hasHandle = _mutex.WaitOne(MutexTimeout);
        if (!hasHandle)
        {
            throw new TimeoutException("CircularChannel: Failed to acquire mutex within the time limit.");
        }

        try
        {
            using var accessor = _mmf.CreateViewAccessor();
            accessor.Write(0, (ushort)0); // Write pointer
            accessor.Write(2, (ushort)0); // Read pointer
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public IChannelReceiver GetReceiver()
    {
        return _receiver ??= new Receiver(this);
    }

    public IChannelWriter GetWriter()
    {
        return _writer ??= new Writer(this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writer?.Dispose();
        _receiver?.Dispose();
        _mmf.Dispose();
        _mutex.Dispose();
        _disposed = true;
    }
}
