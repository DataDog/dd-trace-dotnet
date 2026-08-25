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

namespace Datadog.Trace.Ci.CiEnvironment;

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
    /// Resolves the source path below the repository root, including paths recorded from another CI workspace.
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
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var fullRoot = Path.GetFullPath(root);
            var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), comparison) ||
                                    fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), comparison)
                                        ? fullRoot
                                        : fullRoot + Path.DirectorySeparatorChar;
            var resolvedPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!resolvedPath.StartsWith(rootWithSeparator, comparison) && !string.Equals(resolvedPath, fullRoot, comparison))
            {
                return false;
            }

            absolutePath = resolvedPath;
            return true;
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
        var normalizedPath = sourceFilePath.Replace('\\', '/');
        if (Path.IsPathRooted(sourceFilePath) || Uri.TryCreate(sourceFilePath, UriKind.Absolute, out _))
        {
            return false;
        }

        var pathWithoutLeadingNavigation = normalizedPath;
        while (pathWithoutLeadingNavigation.StartsWith("../", StringComparison.Ordinal) ||
               pathWithoutLeadingNavigation.StartsWith("./", StringComparison.Ordinal))
        {
            pathWithoutLeadingNavigation = pathWithoutLeadingNavigation.Substring(
                pathWithoutLeadingNavigation.StartsWith("../", StringComparison.Ordinal) ? 3 : 2);
        }

        if (Path.IsPathRooted(pathWithoutLeadingNavigation) || Uri.TryCreate(pathWithoutLeadingNavigation, UriKind.Absolute, out _))
        {
            return false;
        }

        var segments = normalizedPath.Split(Separators.ForwardSlash, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        var firstRepositorySegment = 0;
        while (firstRepositorySegment < segments.Length && (segments[firstRepositorySegment] == "." || segments[firstRepositorySegment] == ".."))
        {
            firstRepositorySegment++;
        }

        for (var i = firstRepositorySegment; i < segments.Length; i++)
        {
            if (segments[i] == "." || segments[i] == "..")
            {
                return false;
            }
        }

        for (var i = firstRepositorySegment; i < segments.Length - 1; i++)
        {
            var candidateSuffix = string.Join(Path.DirectorySeparatorChar.ToString(), segments, i, segments.Length - i);
            if (TryResolveWithinRoot(candidateSuffix, repositoryRoot, out var candidatePath) && File.Exists(candidatePath))
            {
                var separator = useOSSeparator ? Path.DirectorySeparatorChar.ToString() : "/";
                relativePath = string.Join(separator, segments, i, segments.Length - i);
                return true;
            }
        }

        return false;
    }
}
