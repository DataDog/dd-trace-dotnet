// <copyright file="TracerHomeCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Manages the user-local cached tracer home used to shorten runner paths without trusting shared temporary directories.
/// </summary>
internal static class TracerHomeCache
{
    private const string CacheIntegrityFileName = ".dd-trace-runner-cache.integrity";
    private const string CacheMarkerFileName = ".dd-trace-runner-cache";
    private const string CacheIntegrityManifestVersion = "v2";
    private const string CacheLockFileExtension = ".lock";
    private const string CacheStagingDirectorySuffix = ".tmp.";
    private const int CacheKeyLength = 64;
    private const int CacheLockAcquireTimeoutMilliseconds = 10_000;
    private const int CacheLockRetryDelayMilliseconds = 50;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TracerHomeCache));

    /// <summary>
    /// Defines which owner is trusted for an existing directory.
    /// </summary>
    private enum DirectoryOwnerRequirement
    {
        /// <summary>No owner validation is required.</summary>
        None,

        /// <summary>The directory must be owned by the current user.</summary>
        CurrentUser,

        /// <summary>
        /// The directory must be owned by the current user, LocalSystem, or Builtin Administrators on Windows.
        /// </summary>
        TrustedWindowsOwner
    }

    /// <summary>
    /// Returns a validated cached tracer home path when that cache path is shorter than the original path.
    /// </summary>
    /// <param name="tracerHome">The source tracer home path.</param>
    /// <returns>The cached tracer home path, or <paramref name="tracerHome"/> when caching is unnecessary or unsafe.</returns>
    internal static string GetOrCreateCachedTracerHomeIfShorter(string tracerHome)
    {
        return GetOrCreateCachedTracerHomeIfShorter(tracerHome, Thread.Sleep);
    }

    /// <summary>
    /// Returns a validated cached tracer home path when that cache path is shorter than the original path.
    /// </summary>
    /// <param name="tracerHome">The source tracer home path.</param>
    /// <param name="cacheLockRetryDelay">The delay callback invoked between cache lock acquisition attempts.</param>
    /// <returns>The cached tracer home path, or <paramref name="tracerHome"/> when caching is unnecessary or unsafe.</returns>
    internal static string GetOrCreateCachedTracerHomeIfShorter(string tracerHome, Action<int> cacheLockRetryDelay)
    {
        string cachedTracerHome = null;
        try
        {
            var cacheRoot = GetCacheRoot();
            if (Path.Combine(cacheRoot, new string('0', CacheKeyLength)).Length >= tracerHome.Length)
            {
                return tracerHome;
            }

            var integrityManifest = CreateCacheIntegrityManifest(tracerHome);
            cachedTracerHome = Path.Combine(cacheRoot, integrityManifest.CacheKey);
            Ensure(tracerHome, cachedTracerHome, integrityManifest, cacheLockRetryDelay);
            return cachedTracerHome;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Unable to copy tracer home to a shorter temporary path.");
            if (cachedTracerHome is not null && ex is not CacheLockUnavailableException)
            {
                TryDelete(cachedTracerHome);
            }

            return tracerHome;
        }
    }

    /// <summary>
    /// Gets the user-local root directory used for runner tracer home caches.
    /// </summary>
    /// <returns>The cache root path.</returns>
    private static string GetCacheRoot()
    {
        // Keep the cache under a user-local root instead of shared temp to avoid cross-user path hijacking.
        var cacheRoot = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LOCALAPPDATA" : "XDG_CACHE_HOME");
        if (string.IsNullOrEmpty(cacheRoot))
        {
            cacheRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrEmpty(cacheRoot))
        {
            throw new InvalidOperationException("Unable to locate a user-local cache directory.");
        }

        return Path.Combine(cacheRoot, "Datadog", "dd-trace", "runner", "tracer-home");
    }

    /// <summary>
    /// Ensures the cached tracer home exists and matches the expected integrity manifest.
    /// </summary>
    /// <param name="tracerHome">The source tracer home path.</param>
    /// <param name="cachedTracerHome">The target cached tracer home path.</param>
    /// <param name="integrityManifest">The expected cache identity and content manifest.</param>
    /// <param name="cacheLockRetryDelay">The delay callback invoked between cache lock acquisition attempts.</param>
    private static void Ensure(string tracerHome, string cachedTracerHome, CacheIntegrityManifest integrityManifest, Action<int> cacheLockRetryDelay)
    {
        var cacheParent = Path.GetDirectoryName(Path.GetFullPath(cachedTracerHome));
        if (string.IsNullOrEmpty(cacheParent))
        {
            throw new IOException($"Unable to locate parent directory for cached tracer home '{cachedTracerHome}'.");
        }

        // The parent is validated before the lock file is opened; otherwise the lock itself could be created
        // in a shared writable directory and used as an attacker-controlled synchronization point.
        CreatePrivateDirectory(cacheParent);
        using var cacheLock = AcquireCacheLock(cachedTracerHome, cacheLockRetryDelay);
        if (IsCachedTracerHomeReady(cachedTracerHome, integrityManifest))
        {
            return;
        }

        var stagingTracerHome = cachedTracerHome + CacheStagingDirectorySuffix + Guid.NewGuid().ToString("N");
        try
        {
            TryDelete(stagingTracerHome);
            // Copy into a private staging directory and publish with a final rename. The child process only sees
            // cachedTracerHome after the copy, integrity validation, and marker write have all succeeded.
            CreatePrivateDirectory(stagingTracerHome);
            CopyFilesRecursively(tracerHome, stagingTracerHome);
            if (!ValidateCachedTracerHomeIntegrity(stagingTracerHome, integrityManifest))
            {
                throw new IOException($"Cached tracer home '{stagingTracerHome}' failed integrity validation.");
            }

            File.WriteAllText(Path.Combine(stagingTracerHome, CacheIntegrityFileName), integrityManifest.Content);
            // The marker is written last so interrupted copies are not reused by later runs.
            File.WriteAllText(Path.Combine(stagingTracerHome, CacheMarkerFileName), integrityManifest.CacheKey);

            TryDelete(cachedTracerHome);
            if (Directory.Exists(cachedTracerHome))
            {
                throw new IOException($"Unable to replace cached tracer home '{cachedTracerHome}'.");
            }

            Directory.Move(stagingTracerHome, cachedTracerHome);
        }
        finally
        {
            TryDelete(stagingTracerHome);
        }
    }

    /// <summary>
    /// Attempts to delete a private cache directory without interrupting fallback to the original tracer home.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                ValidateExistingPrivateDirectory(path);
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup only. The original tracer home remains in use when this fails.
        }
    }

    /// <summary>
    /// Copies enumerated tracer home files and directories into a target directory using normalized relative paths.
    /// </summary>
    /// <param name="sourcePath">The source tracer home path.</param>
    /// <param name="targetPath">The target tracer home path.</param>
    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        targetPath = Path.GetFullPath(targetPath);

        foreach (var entry in EnumerateTracerHomeEntries(sourcePath, ignoreRootCacheMetadata: false))
        {
            var targetEntryPath = Path.Combine(targetPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(targetEntryPath);
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(targetEntryPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.Copy(entry.FullPath, targetEntryPath, overwrite: true);
        }
    }

    /// <summary>
    /// Checks whether an existing cached tracer home can be reused for the expected manifest.
    /// </summary>
    /// <param name="cachedTracerHome">The cached tracer home path.</param>
    /// <param name="integrityManifest">The expected cache identity and content manifest.</param>
    /// <returns><c>true</c> when the cache marker, manifest, and copied contents all match.</returns>
    private static bool IsCachedTracerHomeReady(string cachedTracerHome, CacheIntegrityManifest integrityManifest)
    {
        if (!Directory.Exists(cachedTracerHome))
        {
            return false;
        }

        ValidateExistingPrivateDirectory(cachedTracerHome);
        var markerPath = Path.Combine(cachedTracerHome, CacheMarkerFileName);
        var integrityPath = Path.Combine(cachedTracerHome, CacheIntegrityFileName);
        return FileContentEquals(markerPath, integrityManifest.CacheKey) &&
               FileContentEquals(integrityPath, integrityManifest.Content) &&
               ValidateCachedTracerHomeIntegrity(cachedTracerHome, integrityManifest);
    }

    /// <summary>
    /// Opens the per-cache lock file used to serialize cache reuse and replacement.
    /// </summary>
    /// <param name="cachedTracerHome">The cached tracer home path.</param>
    /// <param name="cacheLockRetryDelay">The delay callback invoked between cache lock acquisition attempts.</param>
    /// <returns>An exclusive lock file stream held by the caller.</returns>
    private static FileStream AcquireCacheLock(string cachedTracerHome, Action<int> cacheLockRetryDelay)
    {
        var lockPath = cachedTracerHome + CacheLockFileExtension;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            if (File.Exists(lockPath) && !IsRegularFile(lockPath))
            {
                throw new IOException($"Cache lock path '{lockPath}' must be a regular file.");
            }

            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!IsRegularFile(lockPath))
                {
                    throw;
                }

                if (stopwatch.ElapsedMilliseconds >= CacheLockAcquireTimeoutMilliseconds)
                {
                    throw new CacheLockUnavailableException($"Timed out waiting for cache lock '{lockPath}'.", ex);
                }

                cacheLockRetryDelay(CacheLockRetryDelayMilliseconds);
            }
        }
    }

    /// <summary>
    /// Reads the Datadog.Trace.dll assembly version used to separate caches for different tracer builds.
    /// </summary>
    /// <param name="tracerHome">The tracer home path.</param>
    /// <returns>The assembly version string, or an empty string when it cannot be read.</returns>
    private static string GetTracerHomeAssemblyVersion(string tracerHome)
    {
        var tracerAssemblyPath = Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.dll");
        if (!File.Exists(tracerAssemblyPath))
        {
            return string.Empty;
        }

        try
        {
            return AssemblyName.GetAssemblyName(tracerAssemblyPath).Version?.ToString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "Unable to read Datadog.Trace.dll version from tracer home.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates the cache key and integrity manifest from the same enumerated source tracer home entries.
    /// </summary>
    /// <param name="tracerHome">The source tracer home path.</param>
    /// <returns>The cache key, sorted entries, and serialized integrity manifest.</returns>
    private static CacheIntegrityManifest CreateCacheIntegrityManifest(string tracerHome)
    {
        // Build the expected manifest from the source tracer home on every run; a cached manifest is never trusted by itself.
        tracerHome = Path.GetFullPath(tracerHome);
        var entries = CreateCacheIntegrityEntries(tracerHome, ignoreRootCacheMetadata: false);

        var cacheKeyBuilder = StringBuilderCache.Acquire();
        cacheKeyBuilder.Append(tracerHome);
        cacheKeyBuilder.Append('|');
        cacheKeyBuilder.Append(GetTracerHomeAssemblyVersion(tracerHome));
        cacheKeyBuilder.Append('|');

        var integrityBuilder = StringBuilderCache.Acquire();
        integrityBuilder.AppendLine(CacheIntegrityManifestVersion);
        foreach (var entry in entries)
        {
            cacheKeyBuilder.Append(entry.RelativePath);
            cacheKeyBuilder.Append('|');
            cacheKeyBuilder.Append(entry.IsDirectory ? 'd' : 'f');
            cacheKeyBuilder.Append('|');
            if (!entry.IsDirectory)
            {
                cacheKeyBuilder.Append(entry.Length);
                cacheKeyBuilder.Append('|');
                cacheKeyBuilder.Append(entry.LastWriteTimeUtcTicks);
            }

            cacheKeyBuilder.Append(';');

            integrityBuilder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.RelativePath)));
            integrityBuilder.Append('|');
            integrityBuilder.Append(entry.IsDirectory ? 'd' : 'f');
            integrityBuilder.Append('|');
            integrityBuilder.Append(entry.Length);
            integrityBuilder.Append('|');
            integrityBuilder.Append(entry.Sha256);
            integrityBuilder.AppendLine();
        }

        using var sha256 = SHA256.Create();
        var cacheKeyHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(StringBuilderCache.GetStringAndRelease(cacheKeyBuilder)));
        var cacheKey = BitConverter.ToString(cacheKeyHash).Replace("-", string.Empty).ToLowerInvariant();
        return new CacheIntegrityManifest(cacheKey, entries, StringBuilderCache.GetStringAndRelease(integrityBuilder));
    }

    /// <summary>
    /// Builds sorted integrity entries for a tracer home directory tree.
    /// </summary>
    /// <param name="tracerHome">The tracer home path to inspect.</param>
    /// <param name="ignoreRootCacheMetadata">Whether root cache metadata files should be ignored during cache validation.</param>
    /// <returns>The sorted integrity entries.</returns>
    private static CacheIntegrityEntry[] CreateCacheIntegrityEntries(string tracerHome, bool ignoreRootCacheMetadata)
    {
        var entries = new List<CacheIntegrityEntry>();
        foreach (var entry in EnumerateTracerHomeEntries(tracerHome, ignoreRootCacheMetadata))
        {
            if (entry.IsDirectory)
            {
                entries.Add(new CacheIntegrityEntry(entry.RelativePath, true, 0, 0, string.Empty));
                continue;
            }

            var fileInfo = new FileInfo(entry.FullPath);
            entries.Add(new CacheIntegrityEntry(entry.RelativePath, false, fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks, ComputeSha256(entry.FullPath)));
        }

        entries.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal));
        return entries.ToArray();
    }

    /// <summary>
    /// Verifies that a copied cached tracer home matches the expected source manifest.
    /// </summary>
    /// <param name="cachedTracerHome">The cached tracer home path to validate.</param>
    /// <param name="integrityManifest">The expected source manifest.</param>
    /// <returns><c>true</c> when all expected entries exist and match their content metadata.</returns>
    private static bool ValidateCachedTracerHomeIntegrity(string cachedTracerHome, CacheIntegrityManifest integrityManifest)
    {
        CacheIntegrityEntry[] actualEntries;
        try
        {
            actualEntries = CreateCacheIntegrityEntries(cachedTracerHome, ignoreRootCacheMetadata: true);
        }
        catch
        {
            return false;
        }

        if (actualEntries.Length != integrityManifest.Entries.Length)
        {
            return false;
        }

        var relativePathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var actualByPath = new Dictionary<string, CacheIntegrityEntry>(relativePathComparer);
        foreach (var actualEntry in actualEntries)
        {
            if (!actualByPath.TryAdd(actualEntry.RelativePath, actualEntry))
            {
                return false;
            }
        }

        foreach (var expectedEntry in integrityManifest.Entries)
        {
            if (!actualByPath.TryGetValue(expectedEntry.RelativePath, out var actualEntry))
            {
                return false;
            }

            if (actualEntry.IsDirectory != expectedEntry.IsDirectory ||
                actualEntry.Length != expectedEntry.Length ||
                !string.Equals(actualEntry.Sha256, expectedEntry.Sha256, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares a regular file's content with an expected string.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    /// <param name="expectedContent">The expected file content.</param>
    /// <returns><c>true</c> when the path is a regular file with the expected content.</returns>
    private static bool FileContentEquals(string path, string expectedContent)
    {
        return IsRegularFile(path) && File.ReadAllText(path) == expectedContent;
    }

    /// <summary>
    /// Checks whether a path exists as a regular file and is not a reparse point.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    /// <returns><c>true</c> when the path is a regular file.</returns>
    private static bool IsRegularFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Directory) == 0 &&
                   (attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Computes the lowercase SHA-256 hash for a file.
    /// </summary>
    /// <param name="path">The file path to hash.</param>
    /// <returns>The lowercase hexadecimal SHA-256 hash.</returns>
    private static string ComputeSha256(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Enumerates tracer home directories and files using normalized relative paths.
    /// </summary>
    /// <param name="rootPath">The root tracer home path.</param>
    /// <param name="ignoreRootCacheMetadata">Whether root cache metadata files should be skipped.</param>
    /// <returns>The tracer home entries below <paramref name="rootPath"/>.</returns>
    private static IEnumerable<TracerHomeEntry> EnumerateTracerHomeEntries(string rootPath, bool ignoreRootCacheMetadata)
    {
        rootPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count != 0)
        {
            var directoryPath = pendingDirectories.Pop();
            var entryPaths = new List<string>(Directory.EnumerateFileSystemEntries(directoryPath));
            entryPaths.Sort(StringComparer.Ordinal);
            foreach (var entryPath in entryPaths)
            {
                var attributes = GetTracerHomeEntryAttributes(entryPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    // Symlinks would let a source or cache entry escape the validated tree between enumeration
                    // and copy/hash. Reject them instead of trying to canonicalize every possible target.
                    throw new IOException($"Tracer home entry '{entryPath}' must not be a symbolic link or reparse point.");
                }

                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var relativePath = GetRelativePath(rootPath, entryPath);
                if (IsCacheMetadataRelativePath(relativePath))
                {
                    if (ignoreRootCacheMetadata && !isDirectory)
                    {
                        continue;
                    }

                    throw new IOException($"Tracer home entry '{entryPath}' conflicts with runner cache metadata.");
                }

                yield return new TracerHomeEntry(entryPath, relativePath, isDirectory);
                if (isDirectory)
                {
                    pendingDirectories.Push(entryPath);
                }
            }
        }
    }

    /// <summary>
    /// Reads file-system attributes for a tracer home entry and normalizes access errors.
    /// </summary>
    /// <param name="entryPath">The entry path to inspect.</param>
    /// <returns>The file-system attributes for the entry.</returns>
    private static FileAttributes GetTracerHomeEntryAttributes(string entryPath)
    {
        try
        {
            return File.GetAttributes(entryPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SystemException)
        {
            throw new IOException($"Unable to inspect tracer home entry '{entryPath}'.", ex);
        }
    }

    /// <summary>
    /// Checks whether a normalized relative path targets runner cache metadata.
    /// </summary>
    /// <param name="relativePath">The normalized relative path.</param>
    /// <returns><c>true</c> when the path is reserved for runner cache metadata.</returns>
    private static bool IsCacheMetadataRelativePath(string relativePath)
    {
        return string.Equals(relativePath, CacheIntegrityFileName, StringComparison.Ordinal) ||
               string.Equals(relativePath, CacheMarkerFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Converts an absolute path below a root directory into a slash-normalized relative path.
    /// </summary>
    /// <param name="rootPath">The root directory path.</param>
    /// <param name="path">The child path to normalize.</param>
    /// <returns>The slash-normalized relative path.</returns>
    private static string GetRelativePath(string rootPath, string path)
    {
        rootPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
        path = Path.GetFullPath(path);
        var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(rootPath, pathComparison))
        {
            throw new IOException($"Path '{path}' is not under root '{rootPath}'.");
        }

        var relativePath = path.Substring(rootPath.Length)
                               .Replace(Path.DirectorySeparatorChar, '/');
        if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
        {
            relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, '/');
        }

        return relativePath;
    }

    /// <summary>
    /// Ensures a directory path ends with a directory separator.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The path with a trailing directory separator.</returns>
    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
               path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                   ? path
                   : path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Creates a private directory after validating the nearest existing parent.
    /// </summary>
    /// <param name="path">The directory path to create or validate.</param>
    private static void CreatePrivateDirectory(string path)
    {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path))
        {
            ValidateExistingPrivateDirectory(path);
            ValidateExistingCacheParentAncestors(path);
            return;
        }

        // Validate the nearest existing parent before creating the next segment. On POSIX this prevents
        // creating our private cache below a group/world-writable directory such as /tmp.
        var parentPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parentPath) && !Directory.Exists(parentPath))
        {
            CreatePrivateDirectory(parentPath);
        }
        else if (!string.IsNullOrEmpty(parentPath))
        {
            ValidateExistingCacheParentDirectory(parentPath);
            ValidateExistingCacheParentAncestors(parentPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (Directory.Exists(path))
            {
                throw new IOException($"Temporary tracer home directory '{path}' already exists.");
            }

            WindowsDirectoryAccess.CreatePrivateDirectory(path);
            ValidateExistingPrivateDirectory(path);
            return;
        }

        // Directory.CreateDirectory does not let us request 0700 on all supported TFMs, so call mkdir(2)
        // directly and then validate the resulting owner/mode before trusting the path.
        PosixDirectoryAccess.CreatePrivateDirectory(path);
        ValidateExistingPrivateDirectory(path);
    }

    /// <summary>
    /// Validates that an existing directory is owned by the current user and is not broadly writable.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    private static void ValidateExistingPrivateDirectory(string path)
    {
        ValidateExistingDirectory(path, DirectoryOwnerRequirement.CurrentUser, allowGroupOrOtherWrite: false);
    }

    /// <summary>
    /// Validates an existing parent directory before creating a private cache child below it.
    /// </summary>
    /// <param name="path">The parent directory path to validate.</param>
    private static void ValidateExistingCacheParentDirectory(string path)
    {
        var ownerRequirement = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                   ? DirectoryOwnerRequirement.TrustedWindowsOwner
                                   : DirectoryOwnerRequirement.CurrentUser;

        ValidateExistingDirectory(
            path,
            ownerRequirement,
            allowGroupOrOtherWrite: false);
    }

    /// <summary>
    /// Validates POSIX ancestors above the private cache parent.
    /// </summary>
    /// <param name="path">The nearest existing private cache parent directory.</param>
    private static void ValidateExistingCacheParentAncestors(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var currentPath = Path.GetDirectoryName(Path.GetFullPath(path));
        while (!string.IsNullOrEmpty(currentPath))
        {
            ValidateExistingPosixCacheParentAncestor(currentPath);

            var parentPath = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(parentPath) || string.Equals(parentPath, currentPath, StringComparison.Ordinal))
            {
                return;
            }

            currentPath = parentPath;
        }
    }

    /// <summary>
    /// Validates an existing POSIX cache ancestor without rejecting trusted symlink ancestors such as /tmp on macOS.
    /// </summary>
    /// <param name="path">The ancestor directory path to validate.</param>
    private static void ValidateExistingPosixCacheParentAncestor(string path)
    {
        PosixDirectoryAccess.ValidateDirectoryAccess(
            path,
            requireCurrentUserOwner: false,
            allowGroupOrOtherWrite: false,
            allowStickyGroupOrOtherWrite: true,
            allowTrustedSymlink: true);
    }

    /// <summary>
    /// Validates that an existing directory is not a reparse point and satisfies platform access requirements.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <param name="ownerRequirement">The ownership requirement for the directory.</param>
    /// <param name="allowGroupOrOtherWrite">Whether broad write access is allowed.</param>
    /// <param name="allowStickyGroupOrOtherWrite">Whether POSIX sticky directories may be group or other writable.</param>
    private static void ValidateExistingDirectory(
        string path,
        DirectoryOwnerRequirement ownerRequirement,
        bool allowGroupOrOtherWrite,
        bool allowStickyGroupOrOtherWrite = false)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            // A symlinked cache root can redirect the copy into an attacker-controlled tree even when the link
            // itself sits below a trusted parent, so reject reparse points before permission checks.
            throw new IOException($"Directory '{path}' must not be a symbolic link or reparse point.");
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Path '{path}' must be a directory.");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsDirectoryAccess.ValidateDirectoryAccess(
                path,
                requireCurrentUserOwner: ownerRequirement == DirectoryOwnerRequirement.CurrentUser,
                requireTrustedOwner: ownerRequirement != DirectoryOwnerRequirement.None,
                allowBroadWrite: allowGroupOrOtherWrite);
            return;
        }

        PosixDirectoryAccess.ValidateDirectoryAccess(
            path,
            ownerRequirement == DirectoryOwnerRequirement.CurrentUser,
            allowGroupOrOtherWrite,
            allowStickyGroupOrOtherWrite);
    }

    /// <summary>
    /// Represents a sorted tracer home entry with metadata used for cache identity and integrity checks.
    /// </summary>
    /// <param name="RelativePath">The slash-normalized relative path.</param>
    /// <param name="IsDirectory">Whether the entry is a directory.</param>
    /// <param name="Length">The file length, or zero for directories.</param>
    /// <param name="LastWriteTimeUtcTicks">The source file timestamp ticks used only for cache identity.</param>
    /// <param name="Sha256">The file content hash, or an empty string for directories.</param>
    private readonly record struct CacheIntegrityEntry(string RelativePath, bool IsDirectory, long Length, long LastWriteTimeUtcTicks, string Sha256);

    /// <summary>
    /// Represents a discovered tracer home file-system entry.
    /// </summary>
    /// <param name="FullPath">The absolute entry path.</param>
    /// <param name="RelativePath">The slash-normalized relative path.</param>
    /// <param name="IsDirectory">Whether the entry is a directory.</param>
    private readonly record struct TracerHomeEntry(string FullPath, string RelativePath, bool IsDirectory);

    /// <summary>
    /// Groups the cache key, expected entries, and serialized manifest for a source tracer home enumeration.
    /// </summary>
    /// <param name="CacheKey">The cache directory key derived from source path, tracer version, paths, sizes, and timestamps.</param>
    /// <param name="Entries">The sorted expected entries for copied-content validation.</param>
    /// <param name="Content">The serialized integrity manifest written into the cache.</param>
    private sealed record CacheIntegrityManifest(string CacheKey, CacheIntegrityEntry[] Entries, string Content);

    /// <summary>
    /// Signals that the cache lock could not be acquired because another runner kept it held.
    /// </summary>
    private sealed class CacheLockUnavailableException : IOException
    {
        public CacheLockUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
