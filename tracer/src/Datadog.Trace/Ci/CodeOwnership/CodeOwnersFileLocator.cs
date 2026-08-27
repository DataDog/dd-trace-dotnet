// <copyright file="CodeOwnersFileLocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CodeOwnership;

/// <summary>
/// Finds and loads the CODEOWNERS file that applies to the current repository.
/// </summary>
internal sealed class CodeOwnersFileLocator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CodeOwnersFileLocator>();
    private readonly string? _provider;
    private readonly string? _repository;

    internal CodeOwnersFileLocator(string? sourceRoot, string? workspacePath, string? repository, string? provider)
    {
        _repository = repository;
        _provider = provider;
        LocatedFile = FindFromAncestors(sourceRoot, workspacePath) ??
                      FindFromAncestors(workspacePath, basePath: null);
    }

    /// <summary>
    /// Gets the CODEOWNERS file found when the test session was initialized.
    /// </summary>
    internal LocatedCodeOwners? LocatedFile { get; }

    private LocatedCodeOwners? FindFromAncestors(string? startPath, string? basePath)
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

        while (directory is not null)
        {
            var locatedFile = TryLoadFromRepositoryRoot(directory.FullName);
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

    private LocatedCodeOwners? TryLoadFromRepositoryRoot(string root)
    {
        var dialect = DetectDialect(root);
        var codeOwnersPath = FindCodeOwnersPath(root, dialect);
        if (codeOwnersPath is null)
        {
            return null;
        }

        Log.Information("CODEOWNERS file found: {Path}", codeOwnersPath);

        return CodeOwners.TryLoad(codeOwnersPath, dialect, out var rules)
                   ? new LocatedCodeOwners(rules, root)
                   : null;
    }

    private CodeOwners.Dialect DetectDialect(string root)
    {
        var repositoryDialect = GetDialectFromRepository(_repository);
        if (repositoryDialect.HasValue)
        {
            return repositoryDialect.Value;
        }

        if (string.Equals(_provider, "gitlab", StringComparison.Ordinal))
        {
            return CodeOwners.Dialect.GitLab;
        }

        if (string.Equals(_provider, "github", StringComparison.Ordinal))
        {
            return CodeOwners.Dialect.GitHub;
        }

        if (File.Exists(Path.Combine(root, ".gitlab", "CODEOWNERS")) &&
            !File.Exists(Path.Combine(root, ".github", "CODEOWNERS")))
        {
            // The GitLab-only location identifies self-managed instances whose host does not.
            return CodeOwners.Dialect.GitLab;
        }

        return CodeOwners.Dialect.GitHub;
    }

#pragma warning disable SA1204
    private static CodeOwners.Dialect? GetDialectFromRepository(string? repository)
    {
        if (StringUtil.IsNullOrWhiteSpace(repository))
        {
            return null;
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
            return CodeOwners.Dialect.GitLab;
        }

        if (string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return CodeOwners.Dialect.GitHub;
        }

        return null;
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

    private static string? FindCodeOwnersPath(string root, CodeOwners.Dialect dialect)
    {
        foreach (var path in GetCodeOwnersPaths(root, dialect))
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
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
        internal LocatedCodeOwners(CodeOwners rules, string repositoryRoot)
        {
            Rules = rules;
            RepositoryRoot = repositoryRoot;
        }

        internal CodeOwners Rules { get; }

        internal string RepositoryRoot { get; }
    }
}
