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
    private readonly IChannelLock _lock;
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
        _lock = CreateLock(fileName);

        var acquisition = WaitForLock();
        if (acquisition == LockAcquisition.Abandoned)
        {
            // A previous owner died while holding the mutex. The wait still succeeded and we own the
            // mutex now, so carry on initializing the channel; the finally below releases it as usual.
            Log.Warning("CircularChannel: Mutex was abandoned by a previous owner. Recovering ownership.");
        }
        else if (acquisition != LockAcquisition.Acquired)
        {
            _lock.Dispose();
            throw new TimeoutException("CircularChannel: Failed to acquire the channel lock within the time limit.");
        }

        try
        {
            // Let's open or create the file we want to map
            var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            // Only a file we have just created, or one with an unexpected size, needs initializing. The other side of
            // the channel may already be using an existing one, and resetting its pointers drops every unread message.
            var initialize = stream.Length != _settings.BufferSize;
            if (initialize)
            {
                stream.SetLength(_settings.BufferSize);
            }

            // Create the memory mapped file from the stream
            _mmf = MemoryMappedFile.CreateFromFile(stream, mapName: null, _settings.BufferSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);

            if (initialize)
            {
                // Initialize the write and read pointer
                using var accessor = _mmf.CreateViewAccessor();
                accessor.Write(0, (ushort)0); // Write pointer
                accessor.Write(2, (ushort)0); // Read pointer
            }
        }
        finally
        {
            ReleaseLock();
        }
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
        _lock.Dispose();
    }

    internal static string GetMutexName(string fileName)
        => FrameworkDescription.Instance.IsWindows()
               ? @$"Global\{Path.GetFileNameWithoutExtension(fileName)}"
               : $"{Path.GetFileNameWithoutExtension(fileName)}";

    internal static string GetLockFilePath(string fileName) => fileName + ".lock";

    /// <summary>
    /// Waits for the channel lock.
    /// </summary>
    /// <remarks>
    /// Callers must release whenever this returns <see cref="LockAcquisition.Acquired"/> or
    /// <see cref="LockAcquisition.Abandoned"/>, because in both cases the caller owns the lock.
    /// </remarks>
    /// <returns>The outcome of the wait.</returns>
    private LockAcquisition WaitForLock() => _lock.Acquire(_settings.MutexTimeout);

    private void ReleaseLock()
    {
        try
        {
            _lock.Release();
        }
        catch (ObjectDisposedException)
        {
            // The lock was disposed while we held it, nothing to do
        }
        catch (Exception ex) when (Interlocked.Read(ref _disposed) == 1)
        {
            // The channel is being torn down underneath us, so losing ownership here is expected
            Log.Debug(ex, "CircularChannel: Could not release the channel lock while disposing.");
        }
        catch (Exception ex)
        {
            // We acquired the lock but could not give it back, so it may stay held and every process using
            // this channel would time out from now on. Never swallow this silently.
            Log.Error(ex, "CircularChannel: Failed to release the channel lock. The channel may now be unusable.");
        }
    }
}
