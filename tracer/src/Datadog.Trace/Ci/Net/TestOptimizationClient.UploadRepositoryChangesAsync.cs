// <copyright file="TestOptimizationClient.UploadRepositoryChangesAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry.Metrics;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Datadog.Trace.Ci.Net;

internal sealed partial class TestOptimizationClient
{
    public async Task<long> UploadRepositoryChangesAsync()
    {
        Log.Debug("TestOptimizationClient: Uploading Repository Changes...");

        // Let's first try get the commit data from local and remote
        var initialCommitData = await GetCommitsAsync().ConfigureAwait(false);

        // Missing selected history may be recovered from the configured remote below.
        if (!initialCommitData.IsOk && initialCommitData.HasCommits)
        {
            return 0;
        }

        // If:
        //   - We have local commits
        //   - There are not missing commits (backend has the total number of local commits already)
        // Then we are good to go with it, we don't need to check if we need to unshallow or anything and just go with that.
        if (initialCommitData is { HasCommits: true, MissingCommits.Length: 0 })
        {
            Log.Debug("TestOptimizationClient: Initial commit data has everything already, we don't need to upload anything.");
            return 0;
        }

        // There's some missing commits on the backend, first we need to check if we need to unshallow before sending anything...

        try
        {
            var isShallow = GitCommandHelper.IsShallowCloneRepository(_workingDirectory);
            var selectedShaIsValid = GitCommandHelper.IsValidCommitSha(_commitSha);
            var recoverSelected = !initialCommitData.HasCommits && selectedShaIsValid;
            if (!isShallow && !recoverSelected)
            {
                if (!initialCommitData.HasCommits)
                {
                    return 0;
                }

                // Repo is not in a shallow state, we continue with the pack files upload with the initial commit data we retrieved earlier.
                Log.Debug("TestOptimizationClient: Repository is not in a shallow state, uploading changes...");
                return await SendPackFilesAsync(initialCommitData.LocalCommits[0], initialCommitData.MissingCommits, initialCommitData.RemoteCommits).ConfigureAwait(false);
            }

            Log.Debug("TestOptimizationClient: Fetching repository history...");

            // Check the selected root; another checkout commit can have a different shallow boundary.
            var historyRoot = selectedShaIsValid ? _commitSha : "HEAD";
            var gitShallowLogOutput = recoverSelected ? null : GitCommandHelper.RunGitCommand(_workingDirectory, $"log --format=oneline -n 2 {historyRoot} --", MetricTags.CIVisibilityCommands.CheckShallow, useCache: false);
            if (!recoverSelected && gitShallowLogOutput is null)
            {
                Log.Warning("TestOptimizationClient: 'git log --format=oneline -n 2' command is null");
                return 0;
            }

            var shallowLogArray = gitShallowLogOutput?.Output.Split(["\n"], StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (shallowLogArray.Length == 1 || recoverSelected)
            {
                var originName = GitCommandHelper.GetRemoteName(_workingDirectory);

                // git rev-parse HEAD
                var headOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "rev-parse HEAD", MetricTags.CIVisibilityCommands.GetHead);
                var checkoutHead = headOutput?.Output?.Replace("\n", string.Empty).Trim() ?? _branchName;
                var head = selectedShaIsValid ? _commitSha : checkoutHead;
                var targetedRecovery = recoverSelected || !string.Equals(head, checkoutHead, StringComparison.OrdinalIgnoreCase);
                var depth = isShallow ? "--shallow-since=\"1 month ago\"" : string.Empty;

                Log.Information("TestOptimizationClient: Refetching Git data for {OriginName}|{Head}", originName, head);
                var gitUnshallowOutput = GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch {depth} --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow, useCache: false);

                if (targetedRecovery && isShallow && (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0))
                {
                    gitUnshallowOutput = GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch --depth=1 --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow, useCache: false);
                }

                if (recoverSelected && (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0))
                {
                    return 0;
                }

                if (!targetedRecovery && (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0))
                {
                    // ***
                    // The previous command has a drawback: if the local HEAD is a commit that has not been pushed to the remote, it will fail.
                    // If this is the case, we fallback to: `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse --abbrev-ref --symbolic-full-name @{upstream})`
                    // This command will attempt to use the tracked branch for the current branch in order to unshallow.
                    // ***

                    // originName = git config --default origin --get clone.defaultRemoteName
                    // git rev-parse --abbrev-ref --symbolic-full-name @{upstream}
                    headOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "rev-parse --abbrev-ref --symbolic-full-name \"@{upstream}\"", MetricTags.CIVisibilityCommands.GetHead);
                    head = headOutput?.Output?.Replace("\n", string.Empty).Trim() ?? _branchName;

                    // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse --abbrev-ref --symbolic-full-name @{upstream})
                    Log.Information("TestOptimizationClient: Previous unshallow command failed, refetching data with fallback 1 for {OriginName}|{Head}", originName, head);
                    gitUnshallowOutput = GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow, useCache: false);
                }

                if (!targetedRecovery && (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0))
                {
                    // ***
                    // It could be that the CI is working on a detached HEAD or maybe branch tracking hasn’t been set up.
                    // In that case, this command will also fail, and we will finally fallback to we just unshallow all the things:
                    // `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName)`
                    // ***

                    // originName = git config --default origin --get clone.defaultRemoteName
                    // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName)
                    Log.Information("TestOptimizationClient: Previous unshallow command failed, refetching data with fallback 2 for {OriginName}", originName);
                    GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName}", MetricTags.CIVisibilityCommands.Unshallow, useCache: false);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting and reconfiguring git repository for shallow clone.");
        }

        var commitsData = await GetCommitsAsync().ConfigureAwait(false);
        if (!commitsData.IsOk)
        {
            return 0;
        }

        return await SendPackFilesAsync(commitsData.LocalCommits[0], commitsData.MissingCommits, commitsData.RemoteCommits).ConfigureAwait(false);
    }
}
