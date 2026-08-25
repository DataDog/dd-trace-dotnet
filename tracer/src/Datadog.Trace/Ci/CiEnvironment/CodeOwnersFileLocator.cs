// <copyright file="CodeOwnersFileLocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Finds and loads the CODEOWNERS file that applies to the current repository.
/// </summary>
internal sealed class CodeOwnersFileLocator
{
    private const int SearchCacheLimit = 256;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CodeOwnersFileLocator>();
    private readonly object _lock = new();
    private readonly HashSet<string> _searchedDirectories = new(StringComparer.Ordinal);
    private readonly string? _provider;
    private readonly string? _repository;
    private readonly string? _workspacePath;
    private volatile LocatedCodeOwners? _locatedFile;

    internal CodeOwnersFileLocator(string? sourceRoot, string? workspacePath, string? repository, string? provider)
    {
        _workspacePath = workspacePath;
        _repository = repository;
        _provider = provider;
        _locatedFile = TryLoadFromRoot(sourceRoot, logLookup: true, isFallback: false);
    }

    internal bool HasCodeOwners => _locatedFile is not null;

    /// <summary>
    /// Returns the loaded CODEOWNERS file, searching from the source path and workspace when needed.
    /// </summary>
    internal LocatedCodeOwners? Find(string? sourceFilePath)
    {
        if (_locatedFile is not null)
        {
            return _locatedFile;
        }

        lock (_lock)
        {
            if (_locatedFile is not null)
            {
                return _locatedFile;
            }

            _locatedFile = FindFromAncestor(sourceFilePath, _workspacePath) ??
                           FindFromAncestor(_workspacePath, basePath: null);
            return _locatedFile;
        }
    }

    internal bool HasCodeOwnersFile(string root, CodeOwners.Dialect dialect)
        => TryGetCodeOwnersPath(root, dialect, logLookup: false, out _);

    private LocatedCodeOwners? FindFromAncestor(string? startPath, string? basePath)
    {
        var startDirectory = RepositorySourcePathResolver.GetSearchStart(startPath, basePath);
        if (StringUtil.IsNullOrEmpty(startDirectory))
        {
            return null;
        }

        DirectoryInfo? directory;
        try
        {
            directory = new DirectoryInfo(startDirectory);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resolving CODEOWNERS search directory from '{Path}'", startDirectory);
            return null;
        }

        if (_searchedDirectories.Count >= SearchCacheLimit)
        {
            _searchedDirectories.Clear();
        }

        if (!_searchedDirectories.Add(directory.FullName))
        {
            return null;
        }

        while (directory is not null)
        {
            var locatedFile = TryLoadFromRoot(directory.FullName, logLookup: false, isFallback: true);
            if (locatedFile is not null)
            {
                return locatedFile;
            }

            if (HasGitMarker(directory.FullName))
            {
                return null;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private LocatedCodeOwners? TryLoadFromRoot(string? root, bool logLookup, bool isFallback)
    {
        if (StringUtil.IsNullOrEmpty(root))
        {
            return null;
        }

        var dialect = DetectDialect(root);
        if (!TryGetCodeOwnersPath(root, dialect, logLookup, out var codeOwnersPath))
        {
            return null;
        }

        if (isFallback)
        {
            Log.Information("CODEOWNERS file found using fallback search: {Path}", codeOwnersPath);
        }
        else
        {
            Log.Information("CODEOWNERS file found: {Path}", codeOwnersPath);
        }

        return CodeOwners.TryLoad(codeOwnersPath, dialect, out var rules)
                   ? new LocatedCodeOwners(rules, root, dialect)
                   : null;
    }

    private CodeOwners.Dialect DetectDialect(string? root)
    {
        if (TryGetDialectFromRepository(_repository, out var dialect))
        {
            return dialect;
        }

        if (string.Equals(_provider, "gitlab", StringComparison.Ordinal))
        {
            return CodeOwners.Dialect.GitLab;
        }

        if (string.Equals(_provider, "github", StringComparison.Ordinal))
        {
            return CodeOwners.Dialect.GitHub;
        }

        if (!StringUtil.IsNullOrEmpty(root) &&
            File.Exists(Path.Combine(root, ".gitlab", "CODEOWNERS")) &&
            !File.Exists(Path.Combine(root, ".github", "CODEOWNERS")))
        {
            // The GitLab-only location identifies self-managed instances whose host does not.
            return CodeOwners.Dialect.GitLab;
        }

        return CodeOwners.Dialect.GitHub;
    }

#pragma warning disable SA1204
    private static bool TryGetDialectFromRepository(string? repository, out CodeOwners.Dialect dialect)
    {
        dialect = default;
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
            // SCP-style SSH URLs look like git@gitlab.com:group/project.git.
            var hostStart = repository.IndexOf('@') + 1;
            var hostEnd = repository.IndexOf(':', hostStart);
            if (hostStart > 0 && hostEnd > hostStart)
            {
                host = repository.Substring(hostStart, hostEnd - hostStart);
            }
        }

        if (IsGitLabHost(host))
        {
            dialect = CodeOwners.Dialect.GitLab;
            return true;
        }

        if (string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            dialect = CodeOwners.Dialect.GitHub;
            return true;
        }

        return false;
    }

    private static bool IsGitLabHost(string? host)
        => string.Equals(host, "gitlab.com", StringComparison.OrdinalIgnoreCase) ||
           (host?.StartsWith("gitlab.", StringComparison.OrdinalIgnoreCase) ?? false) ||
           (host?.IndexOf(".gitlab.", StringComparison.OrdinalIgnoreCase) >= 0);

    private static bool HasGitMarker(string path)
    {
        var gitPath = Path.Combine(path, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private static bool TryGetCodeOwnersPath(string root, CodeOwners.Dialect dialect, bool logLookup, [NotNullWhen(true)] out string? codeOwnersPath)
    {
        foreach (var path in GetCodeOwnersPaths(root, dialect))
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

    /// <remarks>
    /// See <see href="https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-file-location">GitHub CODEOWNERS file locations</see>
    /// and <see href="https://docs.gitlab.com/user/project/codeowners/#codeowners-file">GitLab CODEOWNERS file locations</see>.
    /// </remarks>
    private static IEnumerable<string> GetCodeOwnersPaths(string root, CodeOwners.Dialect dialect)
    {
        if (dialect == CodeOwners.Dialect.GitHub)
        {
            yield return Path.Combine(root, ".github", "CODEOWNERS");
            yield return Path.Combine(root, "CODEOWNERS");
            yield return Path.Combine(root, "docs", "CODEOWNERS");
        }
        else
        {
            yield return Path.Combine(root, "CODEOWNERS");
            yield return Path.Combine(root, "docs", "CODEOWNERS");
            yield return Path.Combine(root, ".gitlab", "CODEOWNERS");
        }
    }
#pragma warning restore SA1204

    internal sealed class LocatedCodeOwners
    {
        internal LocatedCodeOwners(CodeOwners rules, string root, CodeOwners.Dialect dialect)
        {
            Rules = rules;
            Root = root;
            Dialect = dialect;
        }

        internal CodeOwners Rules { get; }

        internal string Root { get; }

        internal CodeOwners.Dialect Dialect { get; }
    }
}
