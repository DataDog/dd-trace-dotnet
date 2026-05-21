// <copyright file="CoverageAssemblyPathLock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Datadog.Trace.Coverage.Collector
{
    /// <summary>
    /// Coordinates coverage reads and rewrites for individual assembly paths inside the current process.
    /// </summary>
    internal static class CoverageAssemblyPathLock
    {
        // Keep lock entries for the collector process lifetime so every same-path access shares one lock object.
        private static readonly Dictionary<string, ReaderWriterLockSlim> Locks;
        private static readonly TimeSpan DefaultTimeout;

        static CoverageAssemblyPathLock()
        {
            DefaultTimeout = TimeSpan.FromSeconds(5);
            var pathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            Locks = new(pathComparer);
        }

        /// <summary>
        /// Acquires shared read access for a dependency assembly path.
        /// </summary>
        /// <param name="path">The assembly path to protect.</param>
        /// <returns>A disposable lock scope.</returns>
        public static IDisposable EnterRead(string path) => EnterRead(path, DefaultTimeout);

        /// <summary>
        /// Acquires shared read access for a dependency assembly path.
        /// </summary>
        /// <param name="path">The assembly path to protect.</param>
        /// <param name="timeout">The maximum time to wait before failing with an <see cref="IOException"/>.</param>
        /// <returns>A disposable lock scope.</returns>
        internal static IDisposable EnterRead(string path, TimeSpan timeout)
        {
            var normalizedPath = NormalizePath(path);
            var pathLock = GetLock(normalizedPath);
            if (!pathLock.TryEnterReadLock(timeout))
            {
                throw new IOException($"Timed out waiting for read access to assembly: {normalizedPath}");
            }

            return new LockScope(pathLock, isWriteLock: false);
        }

        /// <summary>
        /// Acquires exclusive write access for a target assembly path.
        /// </summary>
        /// <param name="path">The assembly path to protect.</param>
        /// <returns>A disposable lock scope.</returns>
        public static IDisposable EnterWrite(string path) => EnterWrite(path, DefaultTimeout);

        /// <summary>
        /// Acquires exclusive write access for a target assembly path.
        /// </summary>
        /// <param name="path">The assembly path to protect.</param>
        /// <param name="timeout">The maximum time to wait before failing with an <see cref="IOException"/>.</param>
        /// <returns>A disposable lock scope.</returns>
        internal static IDisposable EnterWrite(string path, TimeSpan timeout)
        {
            var normalizedPath = NormalizePath(path);
            var pathLock = GetLock(normalizedPath);
            if (!pathLock.TryEnterWriteLock(timeout))
            {
                throw new IOException($"Timed out waiting for write access to assembly: {normalizedPath}");
            }

            return new LockScope(pathLock, isWriteLock: true);
        }

        /// <summary>
        /// Normalizes an assembly path to the key used by the lock registry.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The full path used as the lock key.</returns>
        internal static string NormalizePath(string path) => Path.GetFullPath(path);

        private static ReaderWriterLockSlim GetLock(string normalizedPath)
        {
            lock (Locks)
            {
                if (!Locks.TryGetValue(normalizedPath, out var pathLock))
                {
                    pathLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
                    Locks[normalizedPath] = pathLock;
                }

                return pathLock;
            }
        }

        private sealed class LockScope : IDisposable
        {
            private readonly ReaderWriterLockSlim _pathLock;
            private readonly bool _isWriteLock;
            private int _disposed;

            public LockScope(ReaderWriterLockSlim pathLock, bool isWriteLock)
            {
                _pathLock = pathLock;
                _isWriteLock = isWriteLock;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                if (_isWriteLock)
                {
                    _pathLock.ExitWriteLock();
                }
                else
                {
                    _pathLock.ExitReadLock();
                }
            }
        }
    }
}
