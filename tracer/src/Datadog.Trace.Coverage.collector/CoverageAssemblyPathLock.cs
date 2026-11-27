// <copyright file="CoverageAssemblyPathLock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Datadog.Trace.Util;

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
            Locks = new(FilePathComparison.Comparer);
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
        /// Checks whether an assembly file exists, waiting for an in-progress rewrite of the path when the file looks missing.
        /// On Windows, <see cref="File.Replace(string, string, string)"/> briefly removes the target name, so a check that
        /// doesn't take the lock can miss an assembly while another thread publishes its rewrite.
        /// </summary>
        /// <param name="path">The assembly path to check.</param>
        /// <returns><see langword="true"/> if the file exists.</returns>
        public static bool FileExists(string path) => FileExists(path, DefaultTimeout);

        /// <summary>
        /// Checks whether an assembly file exists, waiting for an in-progress rewrite of the path when the file looks missing.
        /// </summary>
        /// <param name="path">The assembly path to check.</param>
        /// <param name="timeout">The maximum time to wait for a rewrite before failing with an <see cref="IOException"/>.</param>
        /// <returns><see langword="true"/> if the file exists.</returns>
        internal static bool FileExists(string path, TimeSpan timeout)
        {
            if (File.Exists(path))
            {
                return true;
            }

            var normalizedPath = NormalizePath(path);
            ReaderWriterLockSlim? pathLock;
            lock (Locks)
            {
                Locks.TryGetValue(normalizedPath, out pathLock);
            }

            // A rewrite takes the path lock before replacing the file, so a path without a lock entry isn't being rewritten.
            if (pathLock is null)
            {
                return false;
            }

            using var readLock = EnterRead(normalizedPath, timeout);
            return File.Exists(path);
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
