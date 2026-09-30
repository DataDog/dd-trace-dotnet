// <copyright file="CircularChannel.Lock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Util;
using Microsoft.Win32.SafeHandles;

namespace Datadog.Trace.Ci.Ipc;

internal sealed partial class CircularChannel
{
    /// <summary>
    /// The outcome of waiting for the channel lock.
    /// </summary>
    internal enum LockAcquisition
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
    internal interface IChannelLock : IDisposable
    {
        LockAcquisition Acquire(int timeout);

        void Release();
    }

    private static IChannelLock CreateLock(string fileName)
        => FrameworkDescription.Instance.IsWindows()
               ? new MutexChannelLock(GetMutexName(fileName))
               : new FileChannelLock(GetLockFilePath(fileName));

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
    /// mutexes or don't exclude each other at all. An exclusive <c>flock</c> on a lock file works the same way on every
    /// runtime, and the kernel drops it when the owner dies.
    /// We call <c>flock</c> ourselves instead of relying on the one <see cref="FileStream"/> takes for
    /// <see cref="FileShare.None"/>, because the runtime skips it when file locking is disabled
    /// (<c>DOTNET_SYSTEM_IO_DISABLEFILELOCKING</c>) and ignores most errors taking it. That way every process uses the
    /// same lock whatever its configuration, and a file system without <c>flock</c> fails loudly instead of silently
    /// not excluding anything.
    /// </remarks>
    internal sealed class FileChannelLock : IChannelLock
    {
        private const int MaxRetryDelay = 10;

        // flock operations, the same on Linux and macOS
        private const int LockExclusive = 2;
        private const int LockNonBlocking = 4;
        private const int LockUnlock = 8;

        private const int ErrorInterrupted = 4; // EINTR

        private static readonly int ErrorWouldBlock = FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux ? 11 : 35; // EWOULDBLOCK

        private readonly string _path;

        // flock also excludes other channel instances in this process, because each one opens its own file description.
        // Threads sharing this instance share its descriptor too, so they are serialized locally first.
        private readonly SemaphoreSlim _localLock = new(1, 1);
        private FileStream? _lockFile;
        private long _disposed;

        public FileChannelLock(string path)
        {
            _path = path;
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
                    // Opened once and kept open, so taking the lock is a single syscall from then on
                    var lockFile = _lockFile ??= OpenLockFile(_path);
                    var error = Flock(lockFile.SafeFileHandle, LockExclusive | LockNonBlocking);
                    if (error == 0)
                    {
                        return LockAcquisition.Acquired;
                    }

                    if (error != ErrorWouldBlock && error != ErrorInterrupted)
                    {
                        throw new IOException($"CircularChannel: Failed to lock {_path} (errno {error}). Inter-process communication needs a file system that supports flock.");
                    }
                }
                catch (IOException ex) when (_lockFile is null)
                {
                    // Opening the file fails while another owner holds the lock, see OpenLockFile
                    lastException = ex;
                }
                catch (ObjectDisposedException)
                {
                    // Disposed while we were trying, which also closed the descriptor
                    return LockAcquisition.Disposed;
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
            try
            {
                if (_lockFile is { } lockFile && Flock(lockFile.SafeFileHandle, LockUnlock) != 0)
                {
                    // Closing the descriptor drops the lock as well. The next acquisition opens the file again.
                    _lockFile = null;
                    lockFile.Dispose();
                }
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

            _lockFile?.Dispose();
            _localLock.Dispose();
        }

        private static FileStream OpenLockFile(string path)
        {
            // Unless file locking is disabled, the runtime takes a shared flock while opening the file. That makes the
            // open fail while another owner holds the exclusive lock, and once open it would keep every other owner from
            // ever taking the exclusive lock, so drop it straight away. The descriptor then only holds the locks we take.
            var lockFile = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize: 1);
            Flock(lockFile.SafeFileHandle, LockUnlock);
            return lockFile;
        }

        /// <summary>
        /// Calls <c>flock</c> on the descriptor behind <paramref name="handle"/>.
        /// </summary>
        /// <returns>Zero on success; otherwise the errno of the failure.</returns>
        private static int Flock(SafeFileHandle handle, int operation)
        {
            var addedRef = false;
            try
            {
                // Keeps the descriptor from being closed, and its number reused, while we use it
                handle.DangerousAddRef(ref addedRef);
                return NativeFlock((int)handle.DangerousGetHandle(), operation) == 0 ? 0 : Marshal.GetLastWin32Error();
            }
            finally
            {
                if (addedRef)
                {
                    handle.DangerousRelease();
                }
            }
        }

        [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
        private static extern int NativeFlock(int fd, int operation);
    }
}
