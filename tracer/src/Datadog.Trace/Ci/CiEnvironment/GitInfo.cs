// <copyright file="GitInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Datadog.Trace.Ci.CiEnvironment;

internal class GitInfo : IGitInfo
{
    private static IGitInfoProvider[] _gitInfoProviders = [
        GitCommandGitInfoProvider.Instance,
        ManualParserGitInfoProvider.Instance,
    ];

    /// <summary>
    /// Gets or sets Source root
    /// </summary>
    public string? SourceRoot { get; internal set; }

    /// <summary>
    /// Gets or sets Repository
    /// </summary>
    public string? Repository { get; internal set; }

    /// <summary>
    /// Gets or sets Branch
    /// </summary>
    public string? Branch { get; internal set; }

    /// <summary>
    /// Gets or sets Commit
    /// </summary>
    public string? Commit { get; internal set; }

    /// <summary>
    /// Gets or sets Author Name
    /// </summary>
    public string? AuthorName { get; internal set; }

    /// <summary>
    /// Gets or sets Author Email
    /// </summary>
    public string? AuthorEmail { get; internal set; }

    /// <summary>
    /// Gets or sets Author Date
    /// </summary>
    public DateTimeOffset? AuthorDate { get; internal set; }

    /// <summary>
    /// Gets or sets Committer Name
    /// </summary>
    public string? CommitterName { get; internal set; }

    /// <summary>
    /// Gets or sets Committer Email
    /// </summary>
    public string? CommitterEmail { get; internal set; }

    /// <summary>
    /// Gets or sets Committer Date
    /// </summary>
    public DateTimeOffset? CommitterDate { get; internal set; }

    /// <summary>
    /// Gets or sets Commit Message
    /// </summary>
    public string? Message { get; internal set; }

    /// <summary>
    /// Gets parsing errors
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Gets a GitInfo from a folder
    /// </summary>
    /// <param name="folder">Target folder to retrieve the git info</param>
    /// <returns>Git info</returns>
    public static IGitInfo GetFrom(string folder)
    {
        foreach (var provider in _gitInfoProviders)
        {
            // Try to load git metadata from the folder
            if (provider.TryGetFrom(folder, out var gitInfo) && gitInfo != null)
            {
                return gitInfo;
            }
        }

        // Return the partial gitInfo instance with the initial source root
        return new GitInfo { SourceRoot = new DirectoryInfo(folder).Parent?.FullName };
    }

    /// <summary>
    /// Gets a GitInfo from the current folder or assembly attribute
    /// </summary>
    /// <returns>Git info</returns>
    public static IGitInfo GetCurrent()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var gitDirectory = GetParentGitFolder(baseDirectory) ?? GetParentGitFolder(Environment.CurrentDirectory);
        if (gitDirectory != null)
        {
            foreach (var provider in _gitInfoProviders)
            {
                if (provider.TryGetFrom(gitDirectory.FullName, out var gitInfo) && gitInfo != null)
                {
                    return gitInfo;
                }
            }
        }

        return new GitInfo { SourceRoot = gitDirectory?.Parent?.FullName };
    }

    public static DirectoryInfo? GetParentGitFolder(string? innerFolder)
    {
        if (string.IsNullOrEmpty(innerFolder))
        {
            return null;
        }

        var dirInfo = new DirectoryInfo(innerFolder);
        while (dirInfo != null)
        {
            var gitDirectories = dirInfo.GetDirectories(".git");
            if (gitDirectories.Length > 0)
            {
                foreach (var gitDir in gitDirectories)
                {
                    if (gitDir.Name == ".git")
                    {
                        return gitDir;
                    }
                }
            }

            dirInfo = dirInfo.Parent;
        }

        return null;
    }

    internal static void SetGitInfoProviders(params IGitInfoProvider[] providers)
    {
        _gitInfoProviders = providers;
    }
}
