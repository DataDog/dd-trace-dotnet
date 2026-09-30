// <copyright file="CircularChannel.Lock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Ipc;

internal sealed partial class CircularChannel
{
    /// <summary>
    /// The outcome of waiting for the channel lock.
    /// </summary>
    private enum LockAcquisition
    {
        /// <summary>The wait timed out. The lock is not owned by the caller.</summary>
        TimedOut,

        /// <summary>The lock was acquired normally.</summary>
        Acquired,

        /// <summary>
        /// A previous owner exited without releasing. The wait still succeeded and ownership has
        /// transferred to the caller, so it must be released like any other successful acquisition.
        /// Only the named mutex lock reports this.
        /// </summary>
        Abandoned,

        /// <summary>The lock was disposed while waiting. The lock is not owned by the caller.</summary>
        Disposed,
    }

    /// <summary>
    /// Serializes access to the shared buffer across every process and thread using the channel.
    /// </summary>
    private interface IChannelLock : IDisposable
    {
        LockAcquisition Acquire(int timeout);

        void Release();
    }

    private static IChannelLock CreateLock(string fileName)
    {
        if (FrameworkDescription.Instance.IsWindows())
        {
            return new MutexChannelLock(GetMutexName(fileName));
        }

        if (FileChannelLock.IsFileLockingDisabled())
        {
            // Without flock the file lock would not exclude anything, so fall back to the named mutex. That
            // still works as long as every process using the channel runs on the same runtime major version.
            Log.Debug("CircularChannel: File locking is disabled for this process, falling back to a named mutex.");
            return new MutexChannelLock(GetMutexName(fileName));
        }

        return new FileChannelLock(GetLockFilePath(fileName));
    }

    /// <summary>
    /// Named mutex lock. Used on Windows, where it is a kernel object that behaves the same for every runtime.
    /// </summary>
    private sealed class MutexChannelLock : IChannelLock
    {
        private readonly Mutex _mutex;

        public MutexChannelLock(string name)
        {
            _mutex = new Mutex(initiallyOwned: false, name);
        }

        public LockAcquisition Acquire(int timeout)
        {
            try
            {
                return _mutex.WaitOne(timeout) ? LockAcquisition.Acquired : LockAcquisition.TimedOut;
            }
            catch (AbandonedMutexException)
            {
                // The wait still succeeded and this thread owns the mutex now, so the caller has to release it.
                // Treating this as a failure leaks ownership and poisons the channel for every process using it.
                return LockAcquisition.Abandoned;
            }
            catch (ObjectDisposedException)
            {
                return LockAcquisition.Disposed;
            }
        }

        public void Release() => _mutex.ReleaseMutex();

        public void Dispose() => _mutex.Dispose();
    }

    /// <summary>
    /// File lock used everywhere except Windows.
    /// </summary>
    /// <remarks>
    /// Named mutexes can't be used on Unix because .NET 11 reimplemented them with a shared memory layout older runtimes
    /// don't understand (dotnet/runtime#134491). Processes on different runtimes, such as the VSTest data collector
    /// running on the SDK runtime and a test host targeting an older framework, then either see phantom abandoned
    /// mutexes or don't exclude each other at all. Opening a file with <see cref="FileShare.None"/> takes an exclusive
    /// <c>flock</c> instead, which every .NET runtime implements the same way and the kernel drops when the owner dies.
    /// </remarks>
    private sealed class FileChannelLock : IChannelLock
    {
        private const int MaxRetryDelay = 10;

        private readonly string _path;

        // flock also excludes other channel instances in this process, but threads sharing this instance share
        // _lockStream too, so they are serialized locally first.
        private readonly SemaphoreSlim _localLock = new(1, 1);
        private FileStream? _lockStream;
        private long _disposed;

        public FileChannelLock(string path)
        {
            _path = path;
        }

        public static bool IsFileLockingDisabled()
        {
            // Same precedence as the runtime: the AppContext switch wins over the environment variable
            if (AppContext.TryGetSwitch(PlatformKeys.AppContextSystemIODisableFileLocking, out var disabled))
            {
                return disabled;
            }

            var value = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetSystemIODisableFileLocking);
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public LockAcquisition Acquire(int timeout)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                if (!_localLock.Wait(timeout))
                {
                    return LockAcquisition.TimedOut;
                }
            }
            catch (ObjectDisposedException)
            {
                return LockAcquisition.Disposed;
            }

            Exception? lastException = null;
            var retryDelay = 1;
            while (true)
            {
                if (Interlocked.Read(ref _disposed) == 1)
                {
                    return LockAcquisition.Disposed;
                }

                try
                {
                    _lockStream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
                    return LockAcquisition.Acquired;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Usually another process, or another channel instance in this process, holds the lock
                    lastException = ex;
                }
                catch
                {
                    _localLock.Release();
                    throw;
                }

                var remaining = timeout - (int)StopwatchHelpers.GetElapsedMilliseconds(Stopwatch.GetTimestamp() - start);
                if (remaining <= 0)
                {
                    Log.Debug(lastException, "CircularChannel: Timed out waiting for the lock file {Path}", _path);
                    _localLock.Release();
                    return LockAcquisition.TimedOut;
                }

                Thread.Sleep(Math.Min(retryDelay, remaining));
                retryDelay = Math.Min(retryDelay * 2, MaxRetryDelay);
            }
        }

        public void Release()
        {
            var lockStream = _lockStream;
            _lockStream = null;
            try
            {
                lockStream?.Dispose();
            }
            finally
            {
                _localLock.Release();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _lockStream?.Dispose();
            _localLock.Dispose();
        }
    }
}
