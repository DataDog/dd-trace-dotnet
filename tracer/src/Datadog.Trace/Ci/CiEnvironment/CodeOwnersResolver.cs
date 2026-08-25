// <copyright file="CodeOwnersResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Linq;

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Finds the repository path for a compiler source file and evaluates it against CODEOWNERS.
/// </summary>
internal sealed class CodeOwnersResolver
{
    private readonly string? _workspacePath;
    private readonly CodeOwnersFileLocator _fileLocator;
    private readonly RepositorySourcePathResolver _pathResolver;

    internal CodeOwnersResolver(string? sourceRoot, string? workspacePath, string? repository, string? provider)
    {
        _workspacePath = workspacePath;
        _fileLocator = new CodeOwnersFileLocator(sourceRoot, workspacePath, repository, provider);
        _pathResolver = new RepositorySourcePathResolver(sourceRoot);
    }

    internal bool HasCodeOwners => _fileLocator.HasCodeOwners;

    /// <summary>
    /// Resolves the source path used for CI tags and returns any owners that match it.
    /// </summary>
    internal SourceOwnership Resolve(string sourceFilePath, bool useOSSeparator)
    {
        var fallbackPath = _pathResolver.MakeRelativeToSourceRoot(sourceFilePath, useOSSeparator);
        var locatedFile = _fileLocator.Find(sourceFilePath);
        if (locatedFile is null)
        {
            return new SourceOwnership(fallbackPath, [], isRepositoryRelative: false);
        }

        string? repositoryPath = null;
        var codeOwnersRoot = GetAbsoluteCodeOwnersRoot(locatedFile);
        if (codeOwnersRoot is not null)
        {
            _pathResolver.TryMakeRepositoryRelative(sourceFilePath, codeOwnersRoot, useOSSeparator, out repositoryPath);
        }

        var isRepositoryRelative = repositoryPath is not null;
        var pathToMatch = repositoryPath ?? fallbackPath;
        var matchingOwners = locatedFile.Rules.Match("/" + pathToMatch)?.ToArray() ?? [];

        return new SourceOwnership(pathToMatch, matchingOwners, isRepositoryRelative);
    }

    private string? GetAbsoluteCodeOwnersRoot(CodeOwnersFileLocator.LocatedCodeOwners locatedFile)
    {
        if (Path.IsPathRooted(locatedFile.Root))
        {
            return locatedFile.Root;
        }

        if (StringUtil.IsNullOrWhiteSpace(_workspacePath) ||
            !RepositorySourcePathResolver.TryResolveWithinRoot(locatedFile.Root, _workspacePath, out var resolvedRoot) ||
            !_fileLocator.HasCodeOwnersFile(resolvedRoot, locatedFile.Dialect))
        {
            return null;
        }

        return resolvedRoot;
    }
}
