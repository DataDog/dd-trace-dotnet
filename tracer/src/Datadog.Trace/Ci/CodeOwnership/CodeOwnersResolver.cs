// <copyright file="CodeOwnersResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CodeOwnership;

/// <summary>
/// Finds the repository path for a compiler source file and evaluates it against CODEOWNERS.
/// </summary>
internal sealed class CodeOwnersResolver
{
    private const int SourcePathCacheLimit = 1_024;

    private readonly CodeOwnersFileLocator.LocatedCodeOwners? _locatedCodeOwners;
    private readonly RepositorySourcePathResolver _pathResolver;
    private readonly SmallCacheOrNoCache<SourcePathCacheKey, SourceOwnership> _sourcePathCache;
    private readonly Func<SourcePathCacheKey, SourceOwnership> _resolveCacheMiss;

    internal CodeOwnersResolver(string? sourceRoot, string? workspacePath, string? repository, string? provider)
    {
        _locatedCodeOwners = new CodeOwnersFileLocator(sourceRoot, workspacePath, repository, provider).LocatedFile;
        _pathResolver = new RepositorySourcePathResolver(sourceRoot);
        // The cache disables itself after the limit instead of retaining an unbounded number of source paths.
        _sourcePathCache = new SmallCacheOrNoCache<SourcePathCacheKey, SourceOwnership>(SourcePathCacheLimit, "CODEOWNERS source paths");
        _resolveCacheMiss = ResolveCacheMiss;
    }

    internal bool HasCodeOwners => _locatedCodeOwners is not null;

    /// <summary>
    /// Resolves the source path used for CI tags and returns any owners that match it.
    /// </summary>
    internal SourceOwnership Resolve(string sourceFilePath, bool useOSSeparator)
    {
        var key = new SourcePathCacheKey(sourceFilePath, useOSSeparator);
        return _sourcePathCache.GetOrAdd(key, _resolveCacheMiss);
    }

    private SourceOwnership ResolveCacheMiss(SourcePathCacheKey key)
    {
        var sourceFilePath = key.SourceFilePath;
        var useOSSeparator = key.UseOSSeparator;
        var locatedFile = _locatedCodeOwners;
        if (locatedFile is null)
        {
            var fallbackPath = _pathResolver.MakeRelativeToSourceRoot(sourceFilePath, useOSSeparator);
            return new SourceOwnership(fallbackPath, [], isRepositoryRelative: false);
        }

        _pathResolver.TryMakeRepositoryRelative(sourceFilePath, locatedFile.RepositoryRoot, useOSSeparator, out var repositoryPath);

        var isRepositoryRelative = repositoryPath is not null;
        var resolvedPath = repositoryPath ?? _pathResolver.MakeRelativeToSourceRoot(sourceFilePath, useOSSeparator);
        var matchingOwners = locatedFile.Rules.Match(resolvedPath);

        return new SourceOwnership(resolvedPath, matchingOwners, isRepositoryRelative);
    }

    private readonly struct SourcePathCacheKey : IEquatable<SourcePathCacheKey>
    {
        internal SourcePathCacheKey(string sourceFilePath, bool useOSSeparator)
        {
            SourceFilePath = sourceFilePath;
            UseOSSeparator = useOSSeparator;
        }

        internal string SourceFilePath { get; }

        internal bool UseOSSeparator { get; }

        public bool Equals(SourcePathCacheKey other)
            => UseOSSeparator == other.UseOSSeparator && string.Equals(SourceFilePath, other.SourceFilePath, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is SourcePathCacheKey other && Equals(other);

        public override int GetHashCode()
            => (StringComparer.Ordinal.GetHashCode(SourceFilePath) * 397) ^ UseOSSeparator.GetHashCode();
    }
}
