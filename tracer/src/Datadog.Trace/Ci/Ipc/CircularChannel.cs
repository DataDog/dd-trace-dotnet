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

internal sealed partial class CircularChannel : IChannel
{
    private const int HeaderSize = 2 * sizeof(ushort); // 1 read pointer + 1 write pointer

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CircularChannel));

    private readonly MemoryMappedFile _mmf;
    private readonly Mutex _mutex;
    private readonly CircularChannelSettings _settings;

    private long _disposed;
    private Writer? _writer;
    private Reader? _reader;

    public CircularChannel(string fileName)
        : this(fileName, new CircularChannelSettings())
    {
    }

    public CircularChannel(string fileName, CircularChannelSettings settings)
    {
        _settings = settings;

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
                    Log.Warning(ex, "Failed to create temporary directory for memory mapped file. Switch to use the default temp path.");
                    folder = Path.GetTempPath();
                }

                fileName = Path.Combine(folder, Path.GetFileName(fileName));
            }
        }
        else if (Path.GetDirectoryName(fileName) is { } directoryName)
        {
            try
            {
                Directory.CreateDirectory(directoryName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create temporary directory for memory mapped file.");
            }
        }

        _disposed = 0;
        _mutex = new Mutex(initiallyOwned: false, GetMutexName(fileName));

        var acquisition = WaitForMutex();
        if (acquisition == MutexAcquisition.Abandoned)
        {
            // A previous owner died while holding the mutex. The wait still succeeded and we own the
            // mutex now, so carry on initializing the channel; the finally below releases it as usual.
            Log.Warning("CircularChannel: Mutex was abandoned by a previous owner. Recovering ownership.");
        }
        else if (acquisition != MutexAcquisition.Acquired)
        {
            throw new TimeoutException("CircularChannel: Failed to acquire mutex within the time limit.");
        }

        try
        {
            // Let's open or create the file we want to map
            var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            // Ensure we have the correct size
            stream.SetLength(_settings.BufferSize);

            // Create the memory mapped file from the stream
            _mmf = MemoryMappedFile.CreateFromFile(stream, mapName: null, _settings.BufferSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);

            // Initialize the write and read pointer
            using var accessor = _mmf.CreateViewAccessor();
            accessor.Write(0, (ushort)0); // Write pointer
            accessor.Write(2, (ushort)0); // Read pointer
        }
        finally
        {
            ReleaseMutex();
        }
    }

    /// <summary>
    /// The outcome of waiting for the channel mutex.
    /// </summary>
    private enum MutexAcquisition
    {
        /// <summary>The wait timed out. The mutex is not owned by the caller.</summary>
        TimedOut,

        /// <summary>The mutex was acquired normally.</summary>
        Acquired,

        /// <summary>
        /// A previous owner exited without releasing. The wait still succeeded and ownership has
        /// transferred to the caller, so it must be released like any other successful acquisition.
        /// </summary>
        Abandoned,

        /// <summary>The mutex was disposed while waiting. The mutex is not owned by the caller.</summary>
        Disposed,
    }

    private int BufferSize => _settings.BufferSize;

    public int BufferBodySize => _settings.BufferSize - HeaderSize;

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

    internal static string GetMutexName(string fileName)
        => FrameworkDescription.Instance.IsWindows()
               ? @$"Global\{Path.GetFileNameWithoutExtension(fileName)}"
               : $"{Path.GetFileNameWithoutExtension(fileName)}";

    /// <summary>
    /// Waits for the channel mutex.
    /// </summary>
    /// <remarks>
    /// <see cref="WaitHandle.WaitOne(int)"/> throws <see cref="AbandonedMutexException"/> when a previous owner
    /// exited without releasing, but the wait itself still succeeds and ownership transfers to this thread.
    /// Treating that as a failed acquisition leaks ownership and poisons the channel for every process using
    /// it, so callers must release whenever this returns <see cref="MutexAcquisition.Acquired"/> or
    /// <see cref="MutexAcquisition.Abandoned"/>.
    /// </remarks>
    /// <returns>The outcome of the wait.</returns>
    private MutexAcquisition WaitForMutex()
    {
        try
        {
            return _mutex.WaitOne(_settings.MutexTimeout) ? MutexAcquisition.Acquired : MutexAcquisition.TimedOut;
        }
        catch (AbandonedMutexException)
        {
            return MutexAcquisition.Abandoned;
        }
        catch (ObjectDisposedException)
        {
            return MutexAcquisition.Disposed;
        }
    }

    private void ReleaseMutex()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ObjectDisposedException)
        {
            // The mutex was disposed while we held it, nothing to do
        }
        catch (ApplicationException)
        {
            // We are not the owner, which can happen if the channel was disposed underneath us. Nothing to do.
        }
    }
}
