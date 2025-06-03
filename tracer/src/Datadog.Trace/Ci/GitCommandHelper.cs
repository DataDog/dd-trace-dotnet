// <copyright file="GitCommandHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

internal static class GitCommandHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GitCommandHelper));

    // Regex patterns for parsing the diff output
    private static readonly Regex DiffHeaderRegex = new(@"^diff --git a/(?<fileA>.+) b/(?<fileB>.+)$", RegexOptions.Compiled);
    private static readonly Regex LineChangeRegex = new(@"^@@ -\d+(,\d+)? \+(?<start>\d+)(,(?<count>\d+))? @@", RegexOptions.Compiled);
    private static readonly Regex BranchFilterRegex = new(@"^(main|master|preprod|prod|release/.*|hotfix/.*)$", RegexOptions.Compiled);
    private static readonly string[] PossibleBaseBranches = ["main", "master", "preprod", "prod", "dev", "development", "trunk"];
    private static readonly char[] LineSeparators = ['\n', '\r'];
    private static readonly char[] WhitespaceSeparators = ['\t', ' '];

    public static ProcessHelpers.CommandOutput? RunGitCommand(string? workingDirectory, string arguments, MetricTags.CIVisibilityCommands ciVisibilityCommand, string? input = null)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommand(ciVisibilityCommand);
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var gitOutput = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    "git",
                    arguments,
                    workingDirectory,
                    outputEncoding: Encoding.Default,
                    errorEncoding: Encoding.Default,
                    inputEncoding: Encoding.Default,
                    useWhereIsIfFileNotFound: true,
                    timeout: TimeSpan.FromMinutes(5)),
                input);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitCommandMs(ciVisibilityCommand, sw.Elapsed.TotalMilliseconds);
            if (gitOutput is null)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(ciVisibilityCommand, MetricTags.CIVisibilityExitCodes.Unknown);
                Log.Warning("GitCommandHelper: 'git {Arguments}' command is null", arguments);
            }
            else if (gitOutput.ExitCode != 0)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(MetricTags.CIVisibilityCommands.GetRepository, TelemetryHelper.GetTelemetryExitCodeFromExitCode(gitOutput.ExitCode));
            }

            if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire();
                sb.AppendLine(" -> ");
                sb.AppendLine($"  command : git {arguments}");
                sb.AppendLine($"exit code : {gitOutput?.ExitCode}");
                sb.AppendLine($"   output : {gitOutput?.Output ?? "<NULL>"}");
                if (gitOutput is not null && gitOutput.Error is { Length: > 0 } err)
                {
                    sb.AppendLine($"   error  : {err}");
                }

                var txt = StringBuilderCache.GetStringAndRelease(sb);
                Log.Debug("GitCommandHelper: Git command {Command}", txt);
            }

            return gitOutput;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Warning(ex, "GitCommandHelper: 'git {Arguments}' threw Win32Exception - git is likely not available", arguments);
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(ciVisibilityCommand, MetricTags.CIVisibilityExitCodes.Missing);
            return null;
        }
    }

    public static FileCoverageInfo[] GetGitDiffFilesAndLines(string workingDirectory, string baseCommit, string? headCommit = null)
    {
        try
        {
            // Retrieve the PR list of modified files
            var arguments = string.IsNullOrEmpty(headCommit) ? $"diff -U0 --word-diff=porcelain {baseCommit}" : $"diff -U0 --word-diff=porcelain {baseCommit} {headCommit}";
            var output = RunGitCommand(workingDirectory, arguments, MetricTags.CIVisibilityCommands.Diff);
            if (output is { ExitCode: 0, Output.Length: > 0 })
            {
                return ParseGitDiff(output.Output).ToArray();
            }

            throw new Exception("Empty git diff command output");
        }
        catch (Exception ex)
        {
            Log.Information(ex, "GitCommandHelper: Error calling git diff");
            throw;
        }

        // Parses the Git diff output to extract modified files and their changed lines
        static List<FileCoverageInfo> ParseGitDiff(string diffOutput)
        {
            var fileChanges = new List<FileCoverageInfo>();
            var modifiedLines = new List<LineRange>(25);
            FileCoverageInfo? currentFile = null;

            // Split the diff output into lines for processing
            var lines = SplitLines(diffOutput);

            foreach (var line in lines)
            {
                // Check for the start of a new file diff
                var headerMatch = DiffHeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    // Add the current file to the result and start a new one
                    if (currentFile != null)
                    {
                        currentFile.ExecutedBitmap = ToFileBitmap(modifiedLines);
                        fileChanges.Add(currentFile);
                        modifiedLines.Clear();
                    }

                    currentFile = new FileCoverageInfo(headerMatch.Groups["fileB"].Value);
                    Log.Debug("GitCommandHelper:  Processing {File} ...", currentFile.Path);
                    continue;
                }

                // Check for the line change marker (e.g., @@ -1,2 +3,4 @@)
                // Start tracking new lines
                var lineChangeMatch = LineChangeRegex.Match(line);
                if (lineChangeMatch.Success &&
                    int.TryParse(lineChangeMatch.Groups["start"].Value, out var startLine))
                {
                    var lineCount = 0;
                    if (lineChangeMatch.Groups["count"].Value is { Length: > 0 } countTxt &&
                        int.TryParse(countTxt.Trim(), out var lCount))
                    {
                        lineCount = lCount; // Start tracking new lines
                        if (lineCount > 0)
                        {
                            lineCount -= 1; // Adjust for the start line count
                        }
                    }

                    var range = new LineRange(startLine, startLine + lineCount);
                    modifiedLines.Add(range);
                    Log.Debug<int, int>("GitCommandHelper:    {From}..{To} ...", range.Start, range.End);
                }
            }

            // Add the last file to the result (if any)
            if (currentFile != null)
            {
                currentFile.ExecutedBitmap = ToFileBitmap(modifiedLines);
                fileChanges.Add(currentFile);
            }

            return fileChanges;

            static byte[]? ToFileBitmap(List<LineRange> modifiedLines)
            {
                if (modifiedLines.Count == 0)
                {
                    return null;
                }

                var maxCount = modifiedLines.Max(m => m.End);
                var bitmap = FileBitmap.FromLineCount(maxCount);
                foreach (var tuple in modifiedLines)
                {
                    for (var i = tuple.Start; i <= tuple.End; i++)
                    {
                        bitmap.Set(i);
                    }
                }

                return bitmap.GetInternalArrayOrToArrayAndDispose();
            }
        }
    }

    /// <summary>
    /// Detects the base branch information from the given Git repository working directory.
    /// Based on: https://datadoghq.atlassian.net/wiki/spaces/SDTEST/pages/4516480052/Impacted+tests+detection
    /// </summary>
    /// <param name="workingDirectory">The path to the Git repository working directory.</param>
    /// <param name="targetBranch">The target branch from which the base branch will be determined. Optional.</param>
    /// <param name="remoteName">The name of the remote repository to consult. Optional.</param>
    /// <param name="defaultBranch">The default branch of the repository. This may be used if no explicit base branch can be determined. Optional.</param>
    /// <param name="pullRequestBaseBranch">The base branch of the pull request, if applicable. Optional.</param>
    /// <param name="fetchRemoteBranches">Indicates whether remote branches should be fetched before determining the base branch. Default is true.</param>
    /// <returns>
    /// An instance of <see cref="BaseBranchInfo"/> containing information about the detected base branch,
    /// or null if the base branch could not be determined.
    /// </returns>
    public static BaseBranchInfo? DetectBaseBranch(
        string workingDirectory,
        string? targetBranch = null,
        string? remoteName = null,
        string? defaultBranch = null,
        string? pullRequestBaseBranch = null,
        bool fetchRemoteBranches = true)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            Log.Warning("GitCommandHelper: Cannot detect base branch because working directory is null or empty");
            return null;
        }

        try
        {
            // Step 1a - Get remote name if not provided
            if (string.IsNullOrEmpty(remoteName))
            {
                var originNameOutput = RunGitCommand(workingDirectory, "config --default origin --get clone.defaultRemoteName", MetricTags.CIVisibilityCommands.GetRemote);
                remoteName = originNameOutput?.Output.Replace("\n", string.Empty).Trim() ?? "origin";
                Log.Debug("GitCommandHelper: Auto-detected remote name: {RemoteName}", remoteName);
            }

            if (remoteName is not { Length: > 0 })
            {
                Log.Warning("GitCommandHelper: Cannot detect remote because remoteName is null or empty");
                return null;
            }

            // Step 1b - Get source branch (target branch) if not provided
            if (string.IsNullOrEmpty(targetBranch))
            {
                var gitOutput = RunGitCommand(workingDirectory, "branch --show-current", MetricTags.CIVisibilityCommands.GetBranch);
                targetBranch = gitOutput?.Output.Replace("\n", string.Empty) ?? string.Empty;
                Log.Debug("GitCommandHelper: Auto-detected source branch: {SourceBranch}", targetBranch);
            }

            // Bail out if the target branch is still empty
            if (string.IsNullOrEmpty(targetBranch))
            {
                Log.Warning("GitCommandHelper: Cannot detect base branch because target branch is null or empty");
                return null;
            }

            // Verify branch exists
            var verifyBranchOutput = RunGitCommand(
                workingDirectory,
                $"rev-parse --verify --quiet {targetBranch}",
                MetricTags.CIVisibilityCommands.VerifyBranchExists);

            if (verifyBranchOutput?.ExitCode != 0)
            {
                Log.Warning("GitCommandHelper: Branch '{Branch}' does not exist", targetBranch);
                return null;
            }

            // Check if the target branch is already a main-like branch
            var shortTargetName = targetBranch;
            if (shortTargetName is { Length: > 0 } && shortTargetName.StartsWith($"{remoteName}/"))
            {
                shortTargetName = shortTargetName.Substring(remoteName.Length + 1);
            }

            if (BranchFilterRegex.IsMatch(shortTargetName))
            {
                Log.Debug("GitCommandHelper: Branch '{Branch}' already matches branch filter → no parent needed", targetBranch);
                return null;
            }

            // Step 2 - Build candidate branches list and fetch them from remote
            var candidateBranches = new List<string>();

            if (pullRequestBaseBranch is { Length: > 0 })
            {
                // Step 2b - We have git.pull_request.base_branch
                if (fetchRemoteBranches)
                {
                    CheckAndFetchBranch(workingDirectory, pullRequestBaseBranch, remoteName);
                }

                candidateBranches.Add(pullRequestBaseBranch);
                Log.Debug("GitCommandHelper: Using pull request base branch from CI: {BaseBranch}", pullRequestBaseBranch);
            }
            else
            {
                // Step 2a - We don't have git.pull_request.base_branch
                if (fetchRemoteBranches)
                {
                    foreach (var branch in PossibleBaseBranches)
                    {
                        CheckAndFetchBranch(workingDirectory, branch, remoteName);
                    }
                }

                // Build candidate list
                var branchesOutput = RunGitCommand(
                    workingDirectory,
                    $"for-each-ref --format='%(refname:short)' refs/heads refs/remotes/{remoteName}",
                    MetricTags.CIVisibilityCommands.BuildCandidateList);

                if (branchesOutput?.ExitCode != 0 || string.IsNullOrWhiteSpace(branchesOutput.Output))
                {
                    Log.Warning("GitCommandHelper: Failed to get branch list");
                    return null;
                }

                candidateBranches.AddRange(
                    SplitLines(branchesOutput.Output)
                       .Select(b => b.Trim('\'', ' '))
                       .Where(b =>
                        {
                            if (!string.Equals(b, targetBranch, StringComparison.OrdinalIgnoreCase))
                            {
                                string nameToCheck = b;
                                if (b.StartsWith($"{remoteName}/"))
                                {
                                    nameToCheck = b.Substring(remoteName.Length + 1);
                                }

                                return BranchFilterRegex.IsMatch(nameToCheck);
                            }

                            return false;
                        }));

                Log.Debug(
                    "GitCommandHelper: Found {Count} candidate branches: {Branches}",
                    candidateBranches.Count,
                    string.Join(", ", candidateBranches));
            }

            if (candidateBranches.Count == 0)
            {
                Log.Warning("GitCommandHelper: No candidate branches found");
                return null;
            }

            // Step 3 - Find the best base branch
            var metrics = new List<BranchMetrics>();

            // Compute metrics for each branch
            foreach (var branch in candidateBranches)
            {
                // Find merge-base (common ancestor)
                var mergeBaseOutput = RunGitCommand(
                    workingDirectory,
                    $"merge-base {branch} {targetBranch}",
                    MetricTags.CIVisibilityCommands.MergeBase);

                if (mergeBaseOutput?.ExitCode != 0 || string.IsNullOrWhiteSpace(mergeBaseOutput.Output))
                {
                    continue; // Skip if no common history
                }

                var mergeBaseSha = mergeBaseOutput.Output.Trim();

                // Get ahead/behind counts
                var revListOutput = RunGitCommand(
                    workingDirectory,
                    $"rev-list --left-right --count {branch}...{targetBranch}",
                    MetricTags.CIVisibilityCommands.RevList);

                if (revListOutput?.ExitCode != 0 || string.IsNullOrWhiteSpace(revListOutput.Output))
                {
                    continue;
                }

                var counts = revListOutput.Output.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (counts.Length != 2 ||
                    !int.TryParse(counts[0], out var behind) ||
                    !int.TryParse(counts[1], out var ahead))
                {
                    continue; // Skip if unexpected format or cannot parse counts
                }

                metrics.Add(new BranchMetrics(branch, mergeBaseSha, behind, ahead));
            }

            if (metrics.Count == 0)
            {
                Log.Warning("GitCommandHelper: No metrics could be computed for any candidate branch");
                return null;
            }

            // Sort by "ahead" metric
            metrics.Sort((a, b) => a.Ahead.CompareTo(b.Ahead));

            int bestAhead = metrics[0].Ahead;
            var bestCandidate = metrics[0]; // Default to first

            // Find the best candidate among those with the same bestAhead value in a single pass
            foreach (var candidate in metrics)
            {
                if (candidate.Ahead == bestAhead)
                {
                    bestCandidate = candidate;

                    // If this is the default branch, it's the best choice
                    if (IsDefaultBranch(candidate.Branch))
                    {
                        break; // Found the best possible candidate
                    }
                }
                else
                {
                    // Since metrics are sorted by Ahead, once we hit a different value, we're done
                    break;
                }
            }

            var isDefaultBranch = IsDefaultBranch(bestCandidate.Branch);

            return new BaseBranchInfo(
                bestCandidate.Branch,
                bestCandidate.MergeBaseSha,
                bestCandidate.Behind,
                bestCandidate.Ahead,
                isDefaultBranch);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GitCommandHelper: Error detecting base branch for '{Branch}'", targetBranch);
            return null;
        }

        bool IsDefaultBranch(string candidate) => !string.IsNullOrEmpty(defaultBranch) &&
                                                  (candidate == defaultBranch ||
                                                   candidate == $"{remoteName}/{defaultBranch}");
    }

    private static void CheckAndFetchBranch(string workingDirectory, string branch, string remoteName)
    {
        try
        {
            // Check if branch exists locally
            var localOutput = RunGitCommand(
                workingDirectory,
                $"show-ref --verify --quiet refs/heads/{branch}",
                MetricTags.CIVisibilityCommands.ShowRef);

            if (localOutput?.ExitCode == 0)
            {
                // Branch exists locally
                Log.Debug("GitCommandHelper: Branch {Branch} exists locally", branch);
                return;
            }

            // Check if branch exists in remote
            var remoteOutput = RunGitCommand(
                workingDirectory,
                $"ls-remote --heads {remoteName} {branch}",
                MetricTags.CIVisibilityCommands.LsRemote);

            if (remoteOutput?.ExitCode != 0 || string.IsNullOrWhiteSpace(remoteOutput.Output))
            {
                // Branch doesn't exist in remote
                Log.Debug("GitCommandHelper: Branch {Branch} doesn't exist in remote {Remote}", branch, remoteName);
                return;
            }

            // Fetch the latest commit for this branch from remote
            var fetchOutput = RunGitCommand(
                workingDirectory,
                $"fetch --depth 1 {remoteName} {branch}:{branch}",
                MetricTags.CIVisibilityCommands.Fetch);

            if (fetchOutput?.ExitCode != 0)
            {
                Log.Warning("GitCommandHelper: Failed to fetch branch {Branch} from remote {Remote}", branch, remoteName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GitCommandHelper: Error checking/fetching branch {Branch}", branch);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[] SplitLines(string text, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        return text.Split(LineSeparators, options);
    }

    private readonly record struct LineRange(int Start, int End);

    private readonly record struct BranchMetrics(string Branch, string MergeBaseSha, int Behind, int Ahead);
}
