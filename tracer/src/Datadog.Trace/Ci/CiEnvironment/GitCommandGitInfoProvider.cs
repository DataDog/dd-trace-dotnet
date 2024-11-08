// <copyright file="GitCommandGitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GitCommandGitInfoProvider : GitInfoProvider
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GitCommandGitInfoProvider));

    private GitCommandGitInfoProvider()
    {
    }

    public static IGitInfoProvider Instance { get; } = new GitCommandGitInfoProvider();

    private static DateTimeOffset UnixTimeStampToDateTime(long unixTimeStamp)
    {
        const long unixEpochTicks = TimeSpan.TicksPerDay * 719162; // 621,355,968,000,000,000
        return new DateTimeOffset((unixTimeStamp * TimeSpan.TicksPerSecond) + unixEpochTicks, TimeSpan.Zero);
    }

    protected override bool TryGetFrom(DirectoryInfo gitDirectory, out IGitInfo? gitInfo)
    {
        var localGitInfo = new GitInfo
        {
            SourceRoot = gitDirectory.Parent?.FullName
        };

        gitInfo = localGitInfo;

        try
        {
            // Get the repository URL
            var repositoryOutput = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    cmd: "git",
                    arguments: "ls-remote --get-url",
                    workingDirectory: gitDirectory.FullName,
                    useWhereIsIfFileNotFound: true));
            if (repositoryOutput?.ExitCode == 0)
            {
                localGitInfo.Repository = repositoryOutput.Output.Trim();
            }
            else
            {
                localGitInfo.Errors.Add($"Error getting repository URL: {repositoryOutput?.Error}");
            }

            // Get the branch name
            var branchOutput = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    cmd: "git",
                    arguments: "rev-parse --abbrev-ref HEAD",
                    workingDirectory: gitDirectory.FullName,
                    useWhereIsIfFileNotFound: true));
            if (branchOutput?.ExitCode == 0 && branchOutput.Output.Trim() is { Length: > 0 } branchName && branchName != "HEAD")
            {
                localGitInfo.Branch = branchName;
            }
            else
            {
                localGitInfo.Errors.Add($"Error getting branch name: {branchOutput?.Error}");
            }

            // Get the remaining data from the log -1
            var gitLogOutput = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    cmd: "git",
                    arguments: """log -1 --pretty='%H|,|%at|,|%an|,|%ae|,|%ct|,|%cn|,|%ce|,|%B'""",
                    workingDirectory: gitDirectory.FullName,
                    useWhereIsIfFileNotFound: true));
            if (gitLogOutput?.ExitCode != 0)
            {
                localGitInfo.Errors.Add($"Error getting git log: {gitLogOutput?.Error}");
                return false;
            }

            var gitLogDataArray = gitLogOutput.Output.Split(["|,|"], StringSplitOptions.None);
            if (gitLogDataArray.Length < 8)
            {
                localGitInfo.Errors.Add($"Git log output does not contain the expected number of fields: {gitLogOutput.Output}");
                return false;
            }

            // Parse author and committer dates from Unix timestamp
            if (!long.TryParse(gitLogDataArray[1], out var authorUnixDate))
            {
                localGitInfo.Errors.Add("Error parsing author date from git log output");
                return false;
            }

            if (!long.TryParse(gitLogDataArray[4], out var committerUnixDate))
            {
                localGitInfo.Errors.Add("Error parsing committer date from git log output");
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
        }
        catch (Exception ex)
        {
            localGitInfo.Errors.Add($"Error while trying to get git information from the repository: {ex}");
            return false;
        }

        return true;
    }
}
