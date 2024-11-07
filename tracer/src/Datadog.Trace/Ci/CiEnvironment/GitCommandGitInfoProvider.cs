// <copyright file="GitCommandGitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GitCommandGitInfoProvider : IGitInfoProvider
{
    private GitCommandGitInfoProvider()
    {
    }

    public static IGitInfoProvider Instance { get; } = new GitCommandGitInfoProvider();

    public bool TryGetFrom(DirectoryInfo gitDirectory, out IGitInfo gitInfo)
    {
        var localGitInfo = new GitInfo
        {
            SourceRoot = gitDirectory.Parent?.FullName
        };

        gitInfo = localGitInfo;

        // Get the repository URL
        var repositoryOutput = ProcessHelpers.RunCommand(new ProcessHelpers.Command(
                                                             cmd: "git",
                                                             arguments: "ls-remote --get-url",
                                                             workingDirectory: gitDirectory.FullName));
        if (repositoryOutput?.ExitCode == 0)
        {
            localGitInfo.Repository = repositoryOutput.Output.Trim();
        }

        // Get the branch name
        var branchOutput = ProcessHelpers.RunCommand(new ProcessHelpers.Command(
                                                             cmd: "git",
                                                             arguments: "rev-parse --abbrev-ref HEAD",
                                                             workingDirectory: gitDirectory.FullName));
        if (branchOutput?.ExitCode == 0 && branchOutput.Output.Trim() is { Length: > 0 } branchName && branchName != "HEAD")
        {
            localGitInfo.Branch = branchName;
        }

        // Get the remaining data from the log -1
        var gitLogOutput = ProcessHelpers.RunCommand(new ProcessHelpers.Command(
                                                   cmd: "git",
                                                   arguments: """log -1 --pretty='%H|,|%at|,|%an|,|%ae|,|%ct|,|%cn|,|%ce|,|%B'""",
                                                   workingDirectory: gitDirectory.FullName));
        if (gitLogOutput?.ExitCode != 0)
        {
            return false;
        }

        var gitLogDataArray = gitLogOutput.Output.Split(["|,|"], StringSplitOptions.None);
        if (gitLogDataArray.Length < 8)
        {
            return false;
        }

        // Parse author and committer dates from Unix timestamp
        if (!double.TryParse(gitLogDataArray[1], out var authorUnixDate))
        {
            return false;
        }

        if (!double.TryParse(gitLogDataArray[4], out var committerUnixDate))
        {
            return false;
        }

        // Populate the localGitData struct with the parsed information
        localGitInfo.Commit = gitLogDataArray[0];
        localGitInfo.AuthorDate = UnixTimeStampToDateTime(authorUnixDate);
        localGitInfo.AuthorName = gitLogDataArray[2];
        localGitInfo.AuthorEmail = gitLogDataArray[3];
        localGitInfo.CommitterDate = UnixTimeStampToDateTime(committerUnixDate);
        localGitInfo.CommitterName = gitLogDataArray[5];
        localGitInfo.CommitterEmail = gitLogDataArray[6];
        localGitInfo.Message = string.Join("|,|", gitLogDataArray.Skip(7)).Trim();
        if (localGitInfo.Commit.StartsWith("'"))
        {
            localGitInfo.Commit = localGitInfo.Commit.Substring(1);
        }

        if (localGitInfo.Message.EndsWith("'"))
        {
            localGitInfo.Message = localGitInfo.Message.Substring(0, localGitInfo.Message.Length - 1);
        }

        return true;
    }

    private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp);
        return dateTime;
    }
}
