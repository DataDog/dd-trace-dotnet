// <copyright file="CIEnvironmentValues.CodeOwners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Datadog.Trace.Ci.CiEnvironment;

internal abstract partial class CIEnvironmentValues
{
    private const int CodeOwnersSearchCacheLimit = 256;

    private static readonly StringComparer CodeOwnersSearchComparer = FrameworkDescription.Instance.IsWindows()
                                                                          ? StringComparer.OrdinalIgnoreCase
                                                                          : StringComparer.Ordinal;

    private static readonly char[] ForwardSlashCharacters = { '/' };

    private readonly object _codeOwnersLock = new();
    private readonly HashSet<string> _codeOwnersSearchStarts = new(CodeOwnersSearchComparer);

    public CodeOwners? CodeOwners { get; protected set; }

    internal string? CodeOwnersRoot { get; private set; }

    /// <summary>
    /// Returns the source-root-relative path when possible, falling back to the CODEOWNERS root
    /// for compiler paths that were recorded relative to a different CI workspace.
    /// </summary>
    /// <param name="sourceFilePath">The compiler-recorded source file path.</param>
    /// <param name="useOSSeparator">Whether to use the current operating system's directory separator.</param>
    /// <returns>The normalized path relative to the source root or CODEOWNERS root.</returns>
    internal string MakeRelativePathFromSourceRootWithFallback(string sourceFilePath, bool useOSSeparator = true)
    {
        var sourceRelativePath = MakeRelativePathFromSourceRoot(sourceFilePath, useOSSeparator);
        return TryGetCodeOwnersRelativePath(sourceFilePath, useOSSeparator, out var codeOwnersRelativePath)
                   ? codeOwnersRelativePath
                   : sourceRelativePath;
    }

    /// <summary>
    /// Resolves a source path to a safe repository-relative path that can be matched against the
    /// loaded CODEOWNERS file, discovering the file lazily when necessary.
    /// </summary>
    /// <param name="sourceFilePath">The compiler-recorded source file path.</param>
    /// <param name="useOSSeparator">Whether to use the current operating system's directory separator.</param>
    /// <param name="codeOwnersRelativePath">The path relative to the root containing the loaded CODEOWNERS file.</param>
    /// <returns><c>true</c> when the source path can be safely resolved; otherwise, <c>false</c>.</returns>
    internal bool TryGetCodeOwnersRelativePath(string sourceFilePath, bool useOSSeparator, [NotNullWhen(true)] out string? codeOwnersRelativePath)
    {
        codeOwnersRelativePath = null;

        if (StringUtil.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }

        // Algorithm: load CODEOWNERS (with fallback), resolve roots, then normalize source file to repo-relative.
        // Ensure CODEOWNERS is loaded (or discovered via fallback) before attempting normalization.
        EnsureCodeOwnersFromFallback(sourceFilePath);

        if (CodeOwners is null || StringUtil.IsNullOrWhiteSpace(CodeOwnersRoot))
        {
            return false;
        }

        var codeOwnersRoot = CodeOwnersRoot;
        if (!Path.IsPathRooted(codeOwnersRoot))
        {
            // If SourceRoot was relative, re-anchor to WorkspacePath before matching.
            if (StringUtil.IsNullOrWhiteSpace(WorkspacePath) ||
                !TryResolvePathWithinBase(codeOwnersRoot, WorkspacePath, out var resolvedRoot))
            {
                return false;
            }

            // Require a CODEOWNERS file at the resolved root to avoid mismatched roots.
            // Avoid mixing CODEOWNERS content from one root with a different resolved root.
            if (!TryGetCodeOwnersPath(resolvedRoot, GetCodeOwnersPlatform(resolvedRoot), logLookup: false, out _))
            {
                return false;
            }

            codeOwnersRoot = resolvedRoot;
        }

        if (TryAnchorPathToCodeOwnersRoot(sourceFilePath, codeOwnersRoot, useOSSeparator, out codeOwnersRelativePath))
        {
            return true;
        }

        // Only match when the source file can be resolved under the CODEOWNERS root.
        string absolutePath;
        if (Path.IsPathRooted(sourceFilePath) || Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            // Absolute paths are already resolved, no workspace anchoring needed.
            absolutePath = sourceFilePath;
        }
        else
        {
            // For relative paths, enforce that they stay within the CODEOWNERS root.
            // Relative paths must stay within the codeowners root; otherwise we try to anchor them.
            if (!TryResolvePathWithinBase(sourceFilePath, codeOwnersRoot, out var resolvedPath))
            {
                return false;
            }

            absolutePath = resolvedPath;
        }

        // Normalize to a repo-relative path before matching the CODEOWNERS rules.
        var relativePath = MakeRelativePath(codeOwnersRoot, absolutePath, useOSSeparator);
        // Guard against paths that escape the root or remain absolute after normalization.
        if (StringUtil.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            Uri.TryCreate(relativePath, UriKind.Absolute, out _) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith("../", StringComparison.Ordinal) ||
            relativePath.StartsWith("..\\", StringComparison.Ordinal))
        {
            return false;
        }

        codeOwnersRelativePath = relativePath;
        return true;
    }

    /// <summary>
    /// Resolves a candidate source path and returns the directory from which an ancestor
    /// CODEOWNERS search should start, without falling back to the current working directory.
    /// </summary>
    /// <param name="path">The source file or directory path used to start the search.</param>
    /// <param name="basePath">The absolute base used to resolve a relative <paramref name="path"/>.</param>
    /// <returns>The resolved search directory, or <c>null</c> when the path cannot be resolved safely.</returns>
    private static string? GetCodeOwnersSearchStart(string? path, string? basePath)
    {
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string? resolvedPath = null;
        try
        {
            if (Path.IsPathRooted(path) || Uri.TryCreate(path, UriKind.Absolute, out _))
            {
                resolvedPath = path;
            }
            else if (!StringUtil.IsNullOrWhiteSpace(basePath) && Path.IsPathRooted(basePath))
            {
                // Keep relative paths anchored to a known workspace and reject escapes (no CWD fallback).
                TryResolvePathWithinBase(path, basePath, out resolvedPath);
            }

            if (StringUtil.IsNullOrWhiteSpace(resolvedPath))
            {
                return null;
            }

            // Start searching from the directory containing the candidate path.
            if (Directory.Exists(resolvedPath))
            {
                return resolvedPath;
            }

            return Path.GetDirectoryName(resolvedPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS search start for '{Path}'", resolvedPath ?? path);
            return null;
        }
    }

    /// <summary>
    /// Detects a repository boundary represented by either a <c>.git</c> directory or a worktree <c>.git</c> file.
    /// </summary>
    /// <param name="path">The directory in which to look for the Git marker.</param>
    /// <returns><c>true</c> when the directory contains a Git marker; otherwise, <c>false</c>.</returns>
    private static bool HasGitDirectory(string path)
    {
        var gitPath = Path.Combine(path, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    /// <summary>
    /// Anchors a relative path to an absolute base directory and rejects rooted inputs or traversal
    /// that would escape that base.
    /// </summary>
    /// <param name="relativePath">The relative path to resolve.</param>
    /// <param name="basePath">The absolute directory that must contain the resolved path.</param>
    /// <param name="absolutePath">The resolved absolute path when resolution succeeds.</param>
    /// <returns><c>true</c> when the path resolves within the base directory; otherwise, <c>false</c>.</returns>
    private static bool TryResolvePathWithinBase(string relativePath, string basePath, [NotNullWhen(true)] out string? absolutePath)
    {
        absolutePath = null;

        if (StringUtil.IsNullOrWhiteSpace(relativePath) || StringUtil.IsNullOrWhiteSpace(basePath))
        {
            return false;
        }

        try
        {
            // Only combine relative paths; rooted or absolute inputs bypass base anchoring.
            if (Path.IsPathRooted(relativePath) || Uri.TryCreate(relativePath, UriKind.Absolute, out _))
            {
                return false;
            }

            if (!Path.IsPathRooted(basePath))
            {
                return false;
            }

            // Normalize to full paths and ensure the combined path stays within the base.
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var fullBasePath = Path.GetFullPath(basePath);
            var fullBasePathWithSeparator = fullBasePath;
            if (!fullBasePathWithSeparator.EndsWith(Path.DirectorySeparatorChar.ToString(), comparison) &&
                !fullBasePathWithSeparator.EndsWith(Path.AltDirectorySeparatorChar.ToString(), comparison))
            {
                fullBasePathWithSeparator += Path.DirectorySeparatorChar;
            }

            var combinedPath = Path.Combine(fullBasePath, relativePath);
            var fullCombinedPath = Path.GetFullPath(combinedPath);
            // Reject traversal that escapes the base directory.
            if (!fullCombinedPath.StartsWith(fullBasePathWithSeparator, comparison) &&
                !string.Equals(fullCombinedPath, fullBasePath, comparison))
            {
                return false;
            }

            absolutePath = fullCombinedPath;
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving relative path '{Path}' within base '{BasePath}'", relativePath, basePath);
        }

        return false;
    }

    /// <summary>
    /// Probes the platform-specific CODEOWNERS locations in priority order and returns the first
    /// existing file.
    /// </summary>
    /// <param name="sourceRoot">The repository root under which to search.</param>
    /// <param name="platform">The platform whose CODEOWNERS lookup order should be used.</param>
    /// <param name="logLookup">Whether each candidate path should be logged.</param>
    /// <param name="codeOwnersPath">The first existing CODEOWNERS path.</param>
    /// <returns><c>true</c> when a CODEOWNERS file is found; otherwise, <c>false</c>.</returns>
    private static bool TryGetCodeOwnersPath(string sourceRoot, CodeOwners.Platform platform, bool logLookup, [NotNullWhen(true)] out string? codeOwnersPath)
    {
        foreach (var path in GetCodeOwnersPaths(sourceRoot, platform))
        {
            if (logLookup)
            {
                Log.Debug("Looking for CODEOWNERS file in: {Path}", path);
            }

            if (File.Exists(path))
            {
                codeOwnersPath = path;
                return true;
            }
        }

        codeOwnersPath = null;
        return false;
    }

    /// <summary>
    /// Infers the CODEOWNERS dialect from standard repository URLs and SCP-style SSH URLs.
    /// </summary>
    /// <param name="repository">The repository URL to inspect.</param>
    /// <param name="platform">The platform inferred from the repository host.</param>
    /// <returns><c>true</c> when the repository host identifies a supported platform; otherwise, <c>false</c>.</returns>
    private static bool TryGetCodeOwnersPlatformFromRepository(string? repository, out CodeOwners.Platform platform)
    {
        platform = default;
        if (StringUtil.IsNullOrWhiteSpace(repository))
        {
            return false;
        }

        string? host = null;
        if (Uri.TryCreate(repository, UriKind.Absolute, out var repositoryUri) && !StringUtil.IsNullOrEmpty(repositoryUri.Host))
        {
            host = repositoryUri.Host;
        }
        else
        {
            // Handle SCP-style SSH URLs such as git@gitlab.com:group/project.git.
            var hostStart = repository.IndexOf('@') + 1;
            var hostEnd = repository.IndexOf(':', hostStart);
            if (hostStart > 0 && hostEnd > hostStart)
            {
                host = repository.Substring(hostStart, hostEnd - hostStart);
            }
        }

        if (IsGitLabHost(host))
        {
            platform = CodeOwners.Platform.GitLab;
            return true;
        }

        if (string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            platform = CodeOwners.Platform.GitHub;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Recognizes <c>gitlab.com</c> and common self-managed GitLab host naming conventions.
    /// </summary>
    /// <param name="host">The repository host name.</param>
    /// <returns><c>true</c> when the host identifies GitLab; otherwise, <c>false</c>.</returns>
    private static bool IsGitLabHost(string? host)
        => string.Equals(host, "gitlab.com", StringComparison.OrdinalIgnoreCase) ||
           (host?.StartsWith("gitlab.", StringComparison.OrdinalIgnoreCase) ?? false) ||
           (host?.IndexOf(".gitlab.", StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>
    /// Enumerates the supported CODEOWNERS locations in the order defined by each platform,
    /// including all known locations when the platform value is unknown.
    /// </summary>
    /// <param name="sourceRoot">The repository root under which candidate paths are built.</param>
    /// <param name="platform">The platform whose lookup order should be used.</param>
    /// <returns>The candidate CODEOWNERS paths in lookup order.</returns>
    private static IEnumerable<string> GetCodeOwnersPaths(string sourceRoot, CodeOwners.Platform platform)
    {
        if (platform == CodeOwners.Platform.GitHub)
        {
            // GitHub searches .github first, then the repository root, then docs.
            yield return Path.Combine(sourceRoot, ".github", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "docs", "CODEOWNERS");
        }
        else if (platform == CodeOwners.Platform.GitLab)
        {
            // GitLab searches the repository root first, then docs, then .gitlab.
            yield return Path.Combine(sourceRoot, "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "docs", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".gitlab", "CODEOWNERS");
        }
        else
        {
            // Unknown platform: search all known locations in a reasonable order.
            yield return Path.Combine(sourceRoot, "CODEOWNERS");
            yield return Path.Combine(sourceRoot, "docs", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".github", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".gitlab", "CODEOWNERS");
        }
    }

    /// <summary>
    /// Clears the loaded parser, its repository root, and cached fallback search locations.
    /// </summary>
    private void ResetCodeOwners()
    {
        CodeOwners = null;
        CodeOwnersRoot = null;
        lock (_codeOwnersLock)
        {
            _codeOwnersSearchStarts.Clear();
        }
    }

    /// <summary>
    /// Performs the initial CODEOWNERS lookup at <c>SourceRoot</c> using the detected platform semantics.
    /// </summary>
    private void LoadCodeOwners()
    {
        if (!StringUtil.IsNullOrEmpty(SourceRoot))
        {
            var platform = GetCodeOwnersPlatform(SourceRoot);
            if (TryGetCodeOwnersPath(SourceRoot, platform, logLookup: true, out var codeOwnersPath))
            {
                Log.Information("CODEOWNERS file found: {Path}", codeOwnersPath);
                if (CodeOwners.TryLoad(codeOwnersPath, platform, out var parser))
                {
                    CodeOwners = parser;
                    CodeOwnersRoot = SourceRoot;
                }
            }
        }
    }

    /// <summary>
    /// Re-anchors an unresolved relative compiler path by selecting the longest file suffix that
    /// exists below the CODEOWNERS root; absolute, ambiguous, and traversing paths are rejected.
    /// </summary>
    /// <param name="sourceFilePath">The unresolved compiler-recorded source path.</param>
    /// <param name="codeOwnersRoot">The repository root containing the loaded CODEOWNERS file.</param>
    /// <param name="useOSSeparator">Whether to use the current operating system's directory separator.</param>
    /// <param name="codeOwnersRelativePath">The existing suffix relative to the CODEOWNERS root.</param>
    /// <returns><c>true</c> when an unambiguous existing suffix is found; otherwise, <c>false</c>.</returns>
    private bool TryAnchorPathToCodeOwnersRoot(string sourceFilePath, string codeOwnersRoot, bool useOSSeparator, [NotNullWhen(true)] out string? codeOwnersRelativePath)
    {
        // Compiler-recorded paths can be relative to a different base directory than the current
        // workspace (e.g. "../../../_/tracer/test/SampleTests.cs" on CI agents). When strict
        // resolution fails, anchor the path by finding the longest suffix that exists under the
        // CODEOWNERS root, independently of the CI provider layout that produced the prefix.
        codeOwnersRelativePath = null;
        if (StringUtil.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }

        var normalizedPath = sourceFilePath.Replace('\\', '/');
        var segments = normalizedPath.Split(ForwardSlashCharacters, StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(sourceFilePath) || Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            return false;
        }

        var pathWithoutForeignPrefix = normalizedPath;
        while (pathWithoutForeignPrefix.StartsWith("../", StringComparison.Ordinal) ||
               pathWithoutForeignPrefix.StartsWith("./", StringComparison.Ordinal))
        {
            var prefixLength = pathWithoutForeignPrefix.StartsWith("../", StringComparison.Ordinal) ? 3 : 2;
            pathWithoutForeignPrefix = pathWithoutForeignPrefix.Substring(prefixLength);
        }

        if (Path.IsPathRooted(pathWithoutForeignPrefix) || Uri.TryCreate(pathWithoutForeignPrefix, UriKind.Absolute, out _))
        {
            // Reject absolute paths hidden after leading navigation segments.
            return false;
        }

        if (segments.Length < 2)
        {
            // Never anchor bare file names: too easy to match an unrelated file.
            return false;
        }

        // Leading navigation segments belong to the compiler's foreign base directory.
        var start = 0;
        while (start < segments.Length && (segments[start] == "." || segments[start] == ".."))
        {
            start++;
        }

        // Never anchor paths with interior navigation segments: their resolution depends on the
        // unknown base directory and would produce malformed repository-relative paths.
        for (var i = start; i < segments.Length; i++)
        {
            if (segments[i] == "." || segments[i] == "..")
            {
                return false;
            }
        }

        for (var i = start; i < segments.Length - 1; i++)
        {
            var candidateSuffix = string.Join(Path.DirectorySeparatorChar.ToString(), segments, i, segments.Length - i);
            if (TryResolvePathWithinBase(candidateSuffix, codeOwnersRoot, out var candidatePath) && File.Exists(candidatePath))
            {
                var separator = useOSSeparator ? Path.DirectorySeparatorChar.ToString() : "/";
                codeOwnersRelativePath = string.Join(separator, segments, i, segments.Length - i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lazily discovers CODEOWNERS from the source path or workspace when the initial <c>SourceRoot</c>
    /// lookup did not load one, while serializing concurrent fallback attempts.
    /// </summary>
    /// <param name="sourceFilePath">The source path from which to begin the most specific fallback search.</param>
    private void EnsureCodeOwnersFromFallback(string? sourceFilePath)
    {
        if (CodeOwners is not null)
        {
            return;
        }

        lock (_codeOwnersLock)
        {
            if (CodeOwners is not null)
            {
                return;
            }

            // Search order: source file path (most specific), then workspace root.
            // Prefer a source-file-anchored search before falling back to the workspace root.
            var platform = GetCodeOwnersPlatform(SourceRoot ?? WorkspacePath);
            if (TryLoadCodeOwnersFromAncestor(sourceFilePath, platform, WorkspacePath))
            {
                return;
            }

            TryLoadCodeOwnersFromAncestor(WorkspacePath, platform, basePath: null);
        }
    }

    /// <summary>
    /// Walks ancestors from a resolved start directory, loading the first CODEOWNERS file and
    /// stopping at the nearest Git boundary; repeated start locations are cached.
    /// </summary>
    /// <param name="startPath">The source file or directory path from which to start.</param>
    /// <param name="platform">The platform semantics used to locate and parse CODEOWNERS.</param>
    /// <param name="basePath">The absolute base used to resolve a relative <paramref name="startPath"/>.</param>
    /// <returns><c>true</c> when a CODEOWNERS file is found and loaded; otherwise, <c>false</c>.</returns>
    private bool TryLoadCodeOwnersFromAncestor(string? startPath, CodeOwners.Platform platform, string? basePath)
    {
        var startDirectory = GetCodeOwnersSearchStart(startPath, basePath);
        if (StringUtil.IsNullOrEmpty(startDirectory))
        {
            return false;
        }

        DirectoryInfo? directoryInfo;
        try
        {
            directoryInfo = new DirectoryInfo(startDirectory);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS search directory from '{Path}'", startDirectory);
            return false;
        }

        // Limit cache growth to avoid unbounded memory in large test suites.
        if (_codeOwnersSearchStarts.Count >= CodeOwnersSearchCacheLimit)
        {
            _codeOwnersSearchStarts.Clear();
        }

        // Skip repeated lookups for the same starting directory.
        if (!_codeOwnersSearchStarts.Add(directoryInfo.FullName))
        {
            return false;
        }

        // Walk parent directories until we find CODEOWNERS or hit a git boundary.
        while (directoryInfo != null)
        {
            if (TryGetCodeOwnersPath(directoryInfo.FullName, platform, logLookup: false, out var codeOwnersPath))
            {
                Log.Information("CODEOWNERS file found using fallback search: {Path}", codeOwnersPath);
                if (CodeOwners.TryLoad(codeOwnersPath, platform, out var parser))
                {
                    CodeOwners = parser;
                    CodeOwnersRoot = directoryInfo.FullName;
                    return true;
                }

                return false;
            }

            if (HasGitDirectory(directoryInfo.FullName))
            {
                break;
            }

            directoryInfo = directoryInfo.Parent;
        }

        return false;
    }

    /// <summary>
    /// Selects the CODEOWNERS dialect from repository host, CI provider, or platform-specific file
    /// placement, defaulting to GitHub when no reliable GitLab signal exists.
    /// </summary>
    /// <param name="sourceRoot">The repository root used to inspect platform-specific file locations.</param>
    /// <returns>The CODEOWNERS platform whose semantics should be used.</returns>
    private CodeOwners.Platform GetCodeOwnersPlatform(string? sourceRoot)
    {
        if (TryGetCodeOwnersPlatformFromRepository(Repository, out var platform))
        {
            return platform;
        }

        if (string.Equals(Provider, "gitlab", StringComparison.Ordinal))
        {
            return CodeOwners.Platform.GitLab;
        }

        if (string.Equals(Provider, "github", StringComparison.Ordinal))
        {
            return CodeOwners.Platform.GitHub;
        }

        if (!StringUtil.IsNullOrEmpty(sourceRoot) &&
            File.Exists(Path.Combine(sourceRoot, ".gitlab", "CODEOWNERS")) &&
            !File.Exists(Path.Combine(sourceRoot, ".github", "CODEOWNERS")))
        {
            // A platform-specific location is the only reliable signal for self-managed GitLab
            // instances whose host name does not identify the product.
            return CodeOwners.Platform.GitLab;
        }

        return CodeOwners.Platform.GitHub;
    }
}
