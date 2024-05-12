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
    private const int HeaderSize = 2 * sizeof(ushort); // 1 read pointer + 1 write pointer

    private const int PollingInterval = 25;
    private const int MutexTimeout = 5000;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CircularChannel));

    private readonly MemoryMappedFile _mmf;
    private readonly Mutex _mutex;
    private readonly int _bufferSize;
    private long _disposed;

    private Writer? _writer;
    private Reader? _reader;

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
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to create temporary directory for memory mapped file. Switch to use th default temp path.");
                    folder = Path.GetTempPath();
                }

                fileName = Path.Combine(folder, Path.GetFileName(fileName));
            }
        }

        _disposed = 0;
        _bufferSize = bufferSize;
        _mutex = new Mutex(false, $"{Path.GetFileNameWithoutExtension(fileName)}.mutex");

        var hasHandle = _mutex.WaitOne(MutexTimeout);
        if (!hasHandle)
        {
            throw new TimeoutException("CircularChannel: Failed to acquire mutex within the time limit.");
        }

        try
        {
            // Let's open or create the file we want to map
            var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            // Ensure we have the correct size
            stream.SetLength(_bufferSize);

            // Create the memory mapped file from the stream
            _mmf = MemoryMappedFile.CreateFromFile(stream, mapName: null, _bufferSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);

            // Initialize the write and read pointer
            using var accessor = _mmf.CreateViewAccessor();
            accessor.Write(0, (ushort)0); // Write pointer
            accessor.Write(2, (ushort)0); // Read pointer
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    protected int BufferSize => _bufferSize;

    public int BufferBodySize => _bufferSize - HeaderSize;

    public IChannelReader GetReader()
    {
        return _reader ??= new Reader(this);
    }

    public IChannelWriter GetWriter()
    {
        return _writer ??= new Writer(this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _writer?.Dispose();
        _reader?.Dispose();
        _mmf.Dispose();
        _mutex.Dispose();
    }
}
