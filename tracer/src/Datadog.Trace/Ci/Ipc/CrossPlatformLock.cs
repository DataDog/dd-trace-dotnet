// <copyright file="CrossPlatformLock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.Ipc;

// Potential infinite loop
#pragma warning disable DD0001

/// <summary>
/// Cross-platform locking mechanism that uses named mutexes on Windows
/// and file-based locking on Unix-like systems (including macOS)
/// </summary>
internal sealed class CrossPlatformLock : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CrossPlatformLock));

    private readonly ILockImplementation _lockImpl;
    private volatile bool _disposed;

    public CrossPlatformLock(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Lock name cannot be null or empty", nameof(name));
        }

        // Use platform-specific locking mechanism
        if (FrameworkDescription.Instance.IsWindows())
        {
            _lockImpl = new WindowsMutexLock(name);
        }
        else if (FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux)
        {
// This call site is reachable on all platforms.
#pragma warning disable CA1416
            _lockImpl = new LinuxFileLock(name);
#pragma warning restore CA1416
        }
        else
        {
            // macOS and other Unix systems - use exclusive file access
            _lockImpl = new MacOSFileLock(name);
        }
    }

    private CrossPlatformLock(ILockImplementation lockImpl)
    {
        _lockImpl = lockImpl;
    }

    public static bool TryOpenExisting(string name, out CrossPlatformLock? existingLock)
    {
        existingLock = null;

        try
        {
            if (FrameworkDescription.Instance.IsWindows())
            {
                var mutexName = $@"Global\{name}";
                if (Mutex.TryOpenExisting(mutexName, out var existingMutex))
                {
                    existingLock = new CrossPlatformLock(new WindowsMutexLock(existingMutex));
                    return true;
                }
            }
            else
            {
                // For Unix systems, we can't really "open existing" in the same way
                // So we just create a new lock pointing to the same file
                existingLock = new CrossPlatformLock(name);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to open existing lock {Name}", name);
        }

        return false;
    }

    public bool WaitOne(int timeoutMs)
    {
        ThrowIfDisposed();
        return _lockImpl.WaitOne(timeoutMs);
    }

    public void ReleaseMutex()
    {
        ThrowIfDisposed();
        _lockImpl.Release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lockImpl?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CrossPlatformLock));
        }
    }

#pragma warning disable SA1201
    private interface ILockImplementation : IDisposable
#pragma warning restore SA1201
    {
        bool WaitOne(int timeoutMs);

        void Release();
    }

    private sealed class WindowsMutexLock : ILockImplementation
    {
        private readonly Mutex _mutex;
        private volatile bool _disposed;

        public WindowsMutexLock(string name)
        {
            var mutexName = $@"Global\{name}";
            _mutex = new Mutex(initiallyOwned: false, name: mutexName);
        }

        public WindowsMutexLock(Mutex existingMutex)
        {
            _mutex = existingMutex ?? throw new ArgumentNullException(nameof(existingMutex));
        }

        public bool WaitOne(int timeoutMs)
        {
            return _mutex.WaitOne(timeoutMs);
        }

        public void Release()
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was not owned by current thread, ignore
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _mutex?.Dispose();
        }
    }

#if NET6_0_OR_GREATER
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("macos")]
    [UnsupportedOSPlatform("tvos")]
#endif
    private sealed class LinuxFileLock : ILockImplementation
    {
        private readonly string _lockFilePath;
        private readonly FileStream _lockFile;
        private volatile bool _disposed;
        private volatile bool _isLocked;

        public LinuxFileLock(string name)
        {
            // Create lock file in a system temp directory
            var tempDir = Path.GetTempPath();
            var lockDir = Path.Combine(tempDir, "dd-trace-locks");

            try
            {
                Directory.CreateDirectory(lockDir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create lock directory {LockDir}, using temp directory", lockDir);
                lockDir = tempDir;
            }

            // Sanitize the name to be safe for filesystem
            var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            _lockFilePath = Path.Combine(lockDir, $"{safeName}.lock");

            Log.Debug("Using Linux lock file: {LockFilePath}", _lockFilePath);

            // Open the file once and keep it open for the lifetime of this lock
            _lockFile = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite, // Allow other processes to open the file
                bufferSize: 1024);

            // Write current process ID to the lock file for debugging
            try
            {
                var processInfo = $"PID:{System.Diagnostics.Process.GetCurrentProcess().Id} Created:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(processInfo);
                _lockFile.Write(bytes, 0, bytes.Length);
                _lockFile.Flush();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to write process info to lock file");
            }
        }

        public bool WaitOne(int timeoutMs)
        {
            if (_disposed)
            {
                return false;
            }

            var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < endTime && !_disposed)
            {
                try
                {
                    // Try to acquire exclusive lock on the entire file (Linux supports FileStream.Lock)
                    _lockFile.Lock(0, _lockFile.Length > 0 ? _lockFile.Length : 1);
                    _isLocked = true;
                    return true;
                }
                catch (IOException)
                {
                    // File region is locked by another process, wait a bit and retry
                    Thread.Sleep(10); // Small delay before retry
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Unexpected error acquiring file lock");
                    return false;
                }
            }

            return false; // Timeout reached
        }

        public void Release()
        {
            if (_isLocked && !_disposed)
            {
                try
                {
                    _lockFile.Unlock(0, _lockFile.Length > 0 ? _lockFile.Length : 1);
                    _isLocked = false;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to unlock file {LockFilePath}", _lockFilePath);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Release the lock if we still have it
            Release();

            // Close the file stream
            try
            {
                _lockFile?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to dispose lock file stream");
            }

            // Clean up the lock file
            try
            {
                if (File.Exists(_lockFilePath))
                {
                    File.Delete(_lockFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to delete lock file {LockFilePath}", _lockFilePath);
            }
        }
    }

    private sealed class MacOSFileLock : ILockImplementation
    {
        private readonly string _lockFilePath;
        private FileStream? _lockFile;
        private volatile bool _disposed;

        public MacOSFileLock(string name)
        {
            // Create lock file in a system temp directory
            var tempDir = Path.GetTempPath();
            var lockDir = Path.Combine(tempDir, "dd-trace-locks");

            try
            {
                Directory.CreateDirectory(lockDir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create lock directory {LockDir}, using temp directory", lockDir);
                lockDir = tempDir;
            }

            // Sanitize the name to be safe for filesystem
            var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            _lockFilePath = Path.Combine(lockDir, $"{safeName}.lock");

            Log.Debug("Using macOS lock file: {LockFilePath}", _lockFilePath);
        }

        public bool WaitOne(int timeoutMs)
        {
            if (_disposed)
            {
                return false;
            }

            var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < endTime && !_disposed)
            {
                try
                {
                    // On macOS, use exclusive file access since FileStream.Lock is not supported
                    _lockFile = new FileStream(
                        _lockFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None, // Exclusive access - this is the locking mechanism
                        bufferSize: 1);

                    // Write current process ID to the lock file for debugging
                    var processInfo = $"PID:{System.Diagnostics.Process.GetCurrentProcess().Id} Created:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(processInfo);
                    _lockFile.Write(bytes, 0, bytes.Length);
                    _lockFile.Flush();

                    return true;
                }
                catch (IOException)
                {
                    // File is locked by another process, wait a bit and retry
                    _lockFile?.Dispose();
                    _lockFile = null;

                    Thread.Sleep(10); // Small delay before retry
                }
                catch (UnauthorizedAccessException)
                {
                    // Permission issue, wait a bit and retry
                    _lockFile?.Dispose();
                    _lockFile = null;

                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Unexpected error acquiring file lock");
                    _lockFile?.Dispose();
                    _lockFile = null;
                    return false;
                }
            }

            return false; // Timeout reached
        }

        public void Release()
        {
            if (!_disposed)
            {
                _lockFile?.Dispose();
                _lockFile = null;

                // Clean up the lock file
                try
                {
                    if (File.Exists(_lockFilePath))
                    {
                        File.Delete(_lockFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to delete lock file {LockFilePath}", _lockFilePath);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Release();
        }
    }
}
