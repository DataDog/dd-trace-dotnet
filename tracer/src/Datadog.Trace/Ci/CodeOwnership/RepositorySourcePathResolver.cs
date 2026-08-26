// <copyright file="RepositorySourcePathResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CodeOwnership;

/// <summary>
/// Converts compiler-recorded source paths into paths relative to the repository.
/// </summary>
internal sealed class RepositorySourcePathResolver
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RepositorySourcePathResolver>();
    private readonly string? _sourceRoot;

    internal RepositorySourcePathResolver(string? sourceRoot)
    {
        _sourceRoot = sourceRoot;
    }

    internal string MakeRelativeToSourceRoot(string sourceFilePath, bool useOSSeparator)
        => MakeRelativePath(_sourceRoot, sourceFilePath, useOSSeparator);

    /// <summary>
    /// Resolves the source path below the repository root, including relative paths recorded from another CI workspace.
    /// </summary>
    internal bool TryMakeRepositoryRelative(string sourceFilePath, string repositoryRoot, bool useOSSeparator, [NotNullWhen(true)] out string? relativePath)
    {
        relativePath = null;
        if (StringUtil.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }

        if (TryAnchorExistingSuffix(sourceFilePath, repositoryRoot, useOSSeparator, out relativePath))
        {
            return true;
        }

        string? absolutePath;
        if (Path.IsPathRooted(sourceFilePath) || Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            absolutePath = sourceFilePath;
        }
        else if (!TryResolveWithinRoot(sourceFilePath, repositoryRoot, out absolutePath))
        {
            return false;
        }

        var candidate = MakeRelativePath(repositoryRoot, absolutePath, useOSSeparator);
        if (!IsPathInsideRoot(candidate))
        {
            return false;
        }

        relativePath = candidate;
        return true;
    }

#pragma warning disable SA1204
    internal static string? GetSearchStart(string? path, string? basePath)
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
                TryResolveWithinRoot(path, basePath, out resolvedPath);
            }

            if (StringUtil.IsNullOrWhiteSpace(resolvedPath))
            {
                return null;
            }

            return Directory.Exists(resolvedPath) ? resolvedPath : Path.GetDirectoryName(resolvedPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS search start for '{Path}'", resolvedPath ?? path);
            return null;
        }
    }

    internal static bool TryResolveWithinRoot(string relativePath, string root, [NotNullWhen(true)] out string? absolutePath)
    {
        absolutePath = null;
        if (StringUtil.IsNullOrWhiteSpace(relativePath) ||
            StringUtil.IsNullOrWhiteSpace(root) ||
            Path.IsPathRooted(relativePath) ||
            Uri.TryCreate(relativePath, UriKind.Absolute, out _) ||
            !Path.IsPathRooted(root))
        {
            return false;
        }

        try
        {
            var rootInfo = new RootPathInfo(root);
            return TryResolveWithinRoot(relativePath, rootInfo, out absolutePath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving relative path '{Path}' within root '{Root}'", relativePath, root);
            return false;
        }
    }

    internal static string MakeRelativePath(string? root, string absolutePath, bool useOSSeparator)
    {
        if (StringUtil.IsNullOrEmpty(root))
        {
            return absolutePath;
        }

        if (StringUtil.IsNullOrEmpty(absolutePath))
        {
            return root;
        }

        var rootWithSeparator = root;
        try
        {
            if (rootWithSeparator![rootWithSeparator.Length - 1] != Path.DirectorySeparatorChar)
            {
                rootWithSeparator += Path.DirectorySeparatorChar;
            }

            var relativeUri = new Uri(rootWithSeparator).MakeRelativeUri(new Uri(absolutePath));
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            return useOSSeparator ? relativePath.Replace('/', Path.DirectorySeparatorChar) : relativePath;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error creating a relative path for '{AbsolutePath}' from '{Root}'", absolutePath, rootWithSeparator);
            return absolutePath;
        }
    }
#pragma warning restore SA1204

    private static bool IsPathInsideRoot(string path)
        => !StringUtil.IsNullOrWhiteSpace(path) &&
           !Path.IsPathRooted(path) &&
           !Uri.TryCreate(path, UriKind.Absolute, out _) &&
           !path.Equals("..", StringComparison.Ordinal) &&
           !path.StartsWith("../", StringComparison.Ordinal) &&
           !path.StartsWith("..\\", StringComparison.Ordinal);

    private static bool TryAnchorExistingSuffix(string sourceFilePath, string repositoryRoot, bool useOSSeparator, [NotNullWhen(true)] out string? relativePath)
    {
        relativePath = null;
        if (Path.IsPathRooted(sourceFilePath) || Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            return false;
        }

        var normalizedPath = sourceFilePath.IndexOf('\\') >= 0 ? sourceFilePath.Replace('\\', '/') : sourceFilePath;
        var repositoryPathStart = SkipLeadingNavigationSegments(normalizedPath);
        var pathWithoutLeadingNavigation = repositoryPathStart == 0 ? normalizedPath : normalizedPath.Substring(repositoryPathStart);
        if (Path.IsPathRooted(pathWithoutLeadingNavigation) || Uri.TryCreate(pathWithoutLeadingNavigation, UriKind.Absolute, out _))
        {
            return false;
        }

        if (!TryInspectRepositoryPath(normalizedPath, repositoryPathStart, out var repositorySegmentCount, out var hasEmptySegments))
        {
            return false;
        }

        try
        {
            var rootInfo = new RootPathInfo(repositoryRoot);
            var remainingSegments = repositorySegmentCount;
            // Try each suffix from longest to shortest until one names an existing repository file.
            foreach (var segment in normalizedPath.SplitIntoSpans('/'))
            {
                if (segment.Length == 0 || segment.StartIndex < repositoryPathStart)
                {
                    continue;
                }

                if (remainingSegments == 1)
                {
                    break;
                }

                remainingSegments--;
                var candidateSuffix = hasEmptySegments
                                          ? CreateSuffixWithoutEmptySegments(normalizedPath, segment.StartIndex)
                                          : normalizedPath.Substring(segment.StartIndex);
                if (TryResolveWithinRoot(candidateSuffix, rootInfo, out var candidatePath) && File.Exists(candidatePath))
                {
                    relativePath = useOSSeparator && Path.DirectorySeparatorChar != '/'
                                       ? candidateSuffix.Replace('/', Path.DirectorySeparatorChar)
                                       : candidateSuffix;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving a suffix from relative path '{Path}' within root '{Root}'", sourceFilePath, repositoryRoot);
        }

        return false;
    }

    private static int SkipLeadingNavigationSegments(string path)
    {
        var pathStart = 0;
        while (pathStart + 1 < path.Length && path[pathStart] == '.')
        {
            if (pathStart + 2 < path.Length && path[pathStart + 1] == '.' && path[pathStart + 2] == '/')
            {
                pathStart += 3;
            }
            else if (path[pathStart + 1] == '/')
            {
                pathStart += 2;
            }
            else
            {
                break;
            }
        }

        return pathStart;
    }

    private static bool TryInspectRepositoryPath(string path, int pathStart, out int segmentCount, out bool hasEmptySegments)
    {
        segmentCount = 0;
        hasEmptySegments = false;

        // SplitIntoSpans lets us validate the path without allocating a string for every segment.
        foreach (var segment in path.SplitIntoSpans('/'))
        {
            if (segment.Length == 0)
            {
                hasEmptySegments |= segment.StartIndex >= pathStart;
                continue;
            }

            if (segment.StartIndex < pathStart)
            {
                continue;
            }

            if (IsNavigationSegment(segment.AsSpan()))
            {
                return false;
            }

            segmentCount++;
        }

        // A bare filename is not enough to identify its repository location.
        return segmentCount >= 2;
    }

    private static bool IsNavigationSegment(ReadOnlySpan<char> segment)
        => (segment.Length == 1 && segment[0] == '.') ||
           (segment.Length == 2 && segment[0] == '.' && segment[1] == '.');

    private static string CreateSuffixWithoutEmptySegments(string path, int startIndex)
    {
        var builder = StringBuilderCache.Acquire(path.Length - startIndex);
        foreach (var segment in path.SplitIntoSpans('/'))
        {
            if (segment.Length == 0 || segment.StartIndex < startIndex)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('/');
            }

            builder.Append(path, segment.StartIndex, segment.Length);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    private static bool TryResolveWithinRoot(
        string relativePath,
        RootPathInfo root,
        [NotNullWhen(true)] out string? absolutePath)
    {
        var resolvedPath = Path.GetFullPath(Path.Combine(root.FullPath, relativePath));
        if (!resolvedPath.StartsWith(root.FullPathWithSeparator, root.PathComparison) &&
            !string.Equals(resolvedPath, root.FullPath, root.PathComparison))
        {
            absolutePath = null;
            return false;
        }

        absolutePath = resolvedPath;
        return true;
    }

    private readonly struct RootPathInfo
    {
        internal RootPathInfo(string root)
        {
            PathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            FullPath = Path.GetFullPath(root);
            var lastCharacter = FullPath[FullPath.Length - 1];
            FullPathWithSeparator = lastCharacter == Path.DirectorySeparatorChar || lastCharacter == Path.AltDirectorySeparatorChar
                                        ? FullPath
                                        : FullPath + Path.DirectorySeparatorChar;
        }

        internal string FullPath { get; }

        internal string FullPathWithSeparator { get; }

        internal StringComparison PathComparison { get; }
    }
}
