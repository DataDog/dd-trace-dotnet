// <copyright file="GitCommandGitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GitCommandGitInfoProvider : GitInfoProvider
{
    private GitCommandGitInfoProvider()
    {
    }

    public static IGitInfoProvider Instance { get; } = new GitCommandGitInfoProvider();

    protected override bool TryGetFrom(DirectoryInfo gitDirectory, [NotNullWhen(true)] out IGitInfo? gitInfo)
    {
        using var cd = CodeDurationRef.Create();
        var localGitInfo = new GitInfo
        {
            SourceRoot = gitDirectory.Name == ".git" ? gitDirectory.Parent?.FullName : gitDirectory.FullName
        };

        gitInfo = localGitInfo;

        try
        {
            // Get the repository URL
            var repositoryOutput = GitCommandHelper.RunGitCommand(
                localGitInfo.SourceRoot,
                "ls-remote --get-url",
                MetricTags.CIVisibilityCommands.GetRepository);
            if (repositoryOutput?.ExitCode == 0)
            {
                localGitInfo.Repository = repositoryOutput.Output.Trim();
            }
            else
            {
                localGitInfo.Errors.Add($"Error getting repository URL: {repositoryOutput?.Error}");
            }

            // Get the branch name
            var branchOutput = GitCommandHelper.RunGitCommand(
                localGitInfo.SourceRoot,
                "rev-parse --abbrev-ref HEAD",
                MetricTags.CIVisibilityCommands.GetBranch);
            if (branchOutput?.ExitCode == 0 && branchOutput.Output.Trim() is { Length: > 0 } branchName && branchName != "HEAD")
            {
                localGitInfo.Branch = branchName;
            }
            else
            {
                localGitInfo.Errors.Add($"Error getting branch name: {branchOutput?.Error}");
            }

            if (StringUtil.IsNullOrEmpty(localGitInfo.Branch))
            {
                var tagOutput = GitCommandHelper.RunGitCommand(
                    localGitInfo.SourceRoot,
                    "tag --no-column --points-at HEAD",
                    MetricTags.CIVisibilityCommands.GetBranch);
                if (tagOutput?.ExitCode == 0 && !StringUtil.IsNullOrEmpty(tagOutput.Output))
                {
                    var tag = tagOutput.Output.AsSpan().Trim();
                    localGitInfo.Tag = tag.IsEmpty || tag.IndexOfAny('\r', '\n') >= 0 ? null : tag.ToString();
                }
            }

            // Get the remaining data from the log -1
            var gitLogOutput = GitCommandHelper.RunGitCommand(
                localGitInfo.SourceRoot,
                "-c log.showSignature=false log -1 --pretty=" + GitCommandHelper.CommitFormat,
                MetricTags.CIVisibilityCommands.Fetch);
            if (gitLogOutput?.ExitCode != 0)
            {
                localGitInfo.Errors.Add($"Error getting git log: {gitLogOutput?.Error}");
                return false;
            }

            if (GitCommandHelper.ParseCommitData(gitLogOutput.Output) is not { } commitData)
            {
                localGitInfo.Errors.Add("Error parsing git log commit data");
                return false;
            }

            localGitInfo.Commit = commitData.CommitSha;
            localGitInfo.AuthorDate = commitData.AuthorDate;
            localGitInfo.AuthorName = commitData.AuthorName;
            localGitInfo.AuthorEmail = commitData.AuthorEmail;
            localGitInfo.CommitterDate = commitData.CommitterDate;
            localGitInfo.CommitterName = commitData.CommitterName;
            localGitInfo.CommitterEmail = commitData.CommitterEmail;
            localGitInfo.Message = commitData.CommitMessage;
        }
        catch (Exception ex)
        {
            localGitInfo.Errors.Add($"Error while trying to get git information from the repository: {ex}");
            return false;
        }

        return true;
    }
}
