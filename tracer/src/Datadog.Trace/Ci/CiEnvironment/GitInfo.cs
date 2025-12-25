// <copyright file="GitInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GitInfo : IGitInfo
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GitInfo));
    private static IGitInfoProvider[] _gitInfoProviders = [
        ManualParserGitInfoProvider.Instance,
        GitCommandGitInfoProvider.Instance,
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
        List<string>? errors = null;
        foreach (var provider in _gitInfoProviders)
        {
            Log.Debug("Trying to get git info from path {Path} using {Provider}", folder, provider);
            if (provider.TryGetFrom(folder, out var gitInfo) && gitInfo != null)
            {
                Log.Debug("Git info found using {Provider} for path {Path}", provider, folder);
                return gitInfo;
            }

            if (gitInfo != null)
            {
                errors ??= new List<string>();
                errors.AddRange(gitInfo.Errors);
            }
        }

        Log.Debug("No git info found for path {Path}, returning partial info.", folder);
        // Return the partial gitInfo instance with the initial source root
        var value = new GitInfo { SourceRoot = new DirectoryInfo(folder).Parent?.FullName };
        if (errors != null)
        {
            value.Errors.AddRange(errors);
            Log.Warning("{Value}", string.Join(Environment.NewLine, value.Errors));
        }

        return value;
    }

    /// <summary>
    /// Gets a GitInfo from the current folder or assembly attribute
    /// </summary>
    /// <returns>Git info</returns>
    public static IGitInfo GetCurrent()
    {
        List<string>? errors = null;
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var gitDirectory = GetParentGitFolder(baseDirectory);
        Log.Debug("Trying to get git info from base directory: {BaseDirectory} = {GitDirectory}", baseDirectory, gitDirectory);
        if (gitDirectory == null)
        {
            gitDirectory = GetParentGitFolder(Environment.CurrentDirectory);
            Log.Debug("Trying to get git info from Environment.CurrentDirectory: {CurrentDirectory} = {GitDirectory}", Environment.CurrentDirectory, gitDirectory);
        }

        if (gitDirectory != null)
        {
            foreach (var provider in _gitInfoProviders)
            {
                Log.Debug("Trying to get git info from path {Path} using {Provider}", gitDirectory.FullName, provider);
                if (provider.TryGetFrom(gitDirectory, out var gitInfo) && gitInfo != null)
                {
                    Log.Debug("Git info found using {Provider} for path {Path}", provider, gitDirectory.FullName);
                    return gitInfo;
                }

                if (gitInfo != null)
                {
                    errors ??= new List<string>();
                    errors.AddRange(gitInfo.Errors);
                }
            }
        }

        var sourceRoot = string.Empty;
        if (gitDirectory is WorkTreeDirectoryInfo workTreeDirectoryInfo)
        {
            Log.Debug("Found worktree directory: {WorkTreeDirectory}", workTreeDirectoryInfo.WorkTreeDirectory.FullName);
            sourceRoot = workTreeDirectoryInfo.WorkTreeDirectory.FullName;
        }
        else if (gitDirectory is DirectoryInfo { Parent: { } } directoryInfo)
        {
            Log.Debug("Found git directory: {GitDirectory}", directoryInfo.FullName);
            sourceRoot = directoryInfo.Parent.FullName;
        }
        else
        {
            Log.Warning("No git directory found, returning empty GitInfo.");
        }

        Log.Debug("No git info found for path {Path}, returning partial info.", sourceRoot);
        var value = new GitInfo { SourceRoot = sourceRoot };
        if (errors != null)
        {
            value.Errors.AddRange(errors);
            Log.Warning("{Value}", string.Join(Environment.NewLine, value.Errors));
        }

        return value;
    }

    public static FileSystemInfo? GetParentGitFolder(string? innerFolder)
    {
        if (string.IsNullOrEmpty(innerFolder))
        {
            return null;
        }

        DirectoryInfo? dirInfo;

        try
        {
            dirInfo = new DirectoryInfo(innerFolder);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error getting directory info");
            return null;
        }

        while (dirInfo != null)
        {
            try
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

                // worktree support
                var gitFile = Path.Combine(dirInfo.FullName, ".git");
                if (File.Exists(gitFile))
                {
                    var gitFileContent = File.ReadAllText(gitFile);
                    if (gitFileContent.Contains("gitdir: "))
                    {
                        // If the file contains "gitdir: ", it is a git file pointing to another directory
                        var gitDirPath = gitFileContent.Substring(gitFileContent.IndexOf("gitdir: ", StringComparison.Ordinal) + 8).Trim();
                        var workTreeDirectory = new WorkTreeDirectoryInfo(
                            workTreeDirectory: dirInfo,
                            workTreeGitDirectory: new DirectoryInfo(gitDirPath),
                            gitDirectory: GetParentGitFolder(gitDirPath));
                        Log.Debug(
                            "Found worktree directory: {WorkTreeDirectory}, {WorkTreeGitDirectory}, {GitDirectory}",
                            workTreeDirectory.WorkTreeDirectory.FullName,
                            workTreeDirectory.WorkTreeGitDirectory.FullName,
                            workTreeDirectory.GitDirectory?.FullName);
                        return workTreeDirectory;
                    }
                }

                dirInfo = dirInfo.Parent;
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Warning(ex, "Get directories failed with DirectoryNotFoundException");
                return null;
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "Get directories failed with IOException");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warning(ex, "Get directories failed with UnauthorizedAccessException");
                return null;
            }
            catch (SecurityException ex)
            {
                Log.Warning(ex, "Get directories or parent directory failed with SecurityException");
                return null;
            }
        }

        return null;
    }

    internal class WorkTreeDirectoryInfo : FileSystemInfo
    {
        public WorkTreeDirectoryInfo(DirectoryInfo workTreeDirectory, DirectoryInfo workTreeGitDirectory, FileSystemInfo? gitDirectory)
        {
            WorkTreeDirectory = workTreeDirectory ?? throw new ArgumentNullException(nameof(workTreeDirectory));
            WorkTreeGitDirectory = workTreeGitDirectory ?? throw new ArgumentNullException(nameof(workTreeGitDirectory));
            GitDirectory = gitDirectory;
        }

        public DirectoryInfo WorkTreeDirectory { get; }

        public DirectoryInfo WorkTreeGitDirectory { get; }

        public FileSystemInfo? GitDirectory { get; }

        public override string Name => WorkTreeDirectory.Name;

        public override string FullName => WorkTreeDirectory.FullName;

        public override bool Exists => WorkTreeDirectory.Exists;

        public override void Delete() => WorkTreeDirectory.Delete();

        public override bool Equals(object? obj) => WorkTreeDirectory.Equals(obj);

        public override int GetHashCode() => WorkTreeDirectory.GetHashCode();

        public override string ToString() => WorkTreeDirectory.ToString();
    }
}
