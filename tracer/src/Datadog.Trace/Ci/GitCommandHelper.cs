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
using System.Threading.Tasks;
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
    private static readonly char[] LineSeparators = ['\n', '\r'];

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

    public static BaseBranchInfo? DetectBaseBranch(
        string workingDirectory,
        string? targetBranch = null,
        string remoteName = "origin",
        string branchFilterPattern = @"^(main|master|preprod|prod|release/.*|hotfix/.*)$")
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            Log.Warning("GitCommandHelper: Cannot detect base branch because working directory is null or empty");
            return null;
        }

        try
        {
            // 1. Get the target branch if not specified
            if (string.IsNullOrEmpty(targetBranch))
            {
                var branchOutput = RunGitCommand(
                    workingDirectory,
                    "rev-parse --abbrev-ref HEAD",
                    MetricTags.CIVisibilityCommands.GetHead);

                if (branchOutput is not { ExitCode: 0 } || string.IsNullOrWhiteSpace(branchOutput?.Output))
                {
                    Log.Warning("GitCommandHelper: Failed to get current branch");
                    return null;
                }

                targetBranch = branchOutput!.Output.Trim();
            }

            // Verify branch exists
            var verifyBranchOutput = RunGitCommand(
                workingDirectory,
                $"rev-parse --verify --quiet {targetBranch}",
                MetricTags.CIVisibilityCommands.VerifyBranchExists);

            if (verifyBranchOutput is not { ExitCode: 0 })
            {
                Log.Warning("GitCommandHelper: Branch '{Branch}' does not exist", targetBranch);
                return null;
            }

            // 2. Check if the target is already a main-like branch
            string shortTargetName = targetBranch!;
            if (shortTargetName.StartsWith($"{remoteName}/"))
            {
                shortTargetName = shortTargetName.Substring(remoteName.Length + 1);
            }

            if (Regex.IsMatch(shortTargetName, branchFilterPattern))
            {
                Log.Debug("GitCommandHelper: Branch '{Branch}' already matches branch filter → no parent needed", targetBranch);
                return null;
            }

            // 3. Detect default branch
            string? defaultBranch = null;

            // Try to get the symbolic reference for origin/HEAD
            var symbolicRefOutput = RunGitCommand(
                workingDirectory,
                $"symbolic-ref --quiet --short refs/remotes/{remoteName}/HEAD",
                MetricTags.CIVisibilityCommands.GetSymbolicRef);

            if (symbolicRefOutput is { ExitCode: 0 } && !string.IsNullOrWhiteSpace(symbolicRefOutput.Output))
            {
                var symbolicRef = symbolicRefOutput.Output.Trim();
                string prefix = $"{remoteName}/";
                if (symbolicRef.StartsWith(prefix))
                {
                    defaultBranch = symbolicRef.Substring(prefix.Length);
                }
                else
                {
                    defaultBranch = symbolicRef;
                }
            }
            else
            {
                // Fallback to main or master
                foreach (var fallback in new[] { "main", "master" })
                {
                    var fallbackOutput = RunGitCommand(
                        workingDirectory,
                        $"show-ref --verify --quiet refs/remotes/{remoteName}/{fallback}",
                        MetricTags.CIVisibilityCommands.ShowRef);

                    if (fallbackOutput is { ExitCode: 0 })
                    {
                        defaultBranch = fallback;
                        break;
                    }
                }
            }

            // 4. Build candidate list
            var branchesOutput = RunGitCommand(
                workingDirectory,
                $"for-each-ref --format='%(refname:short)' refs/heads refs/remotes/{remoteName}",
                MetricTags.CIVisibilityCommands.BuildCandidateList);

            if (branchesOutput is not { ExitCode: 0 } || string.IsNullOrWhiteSpace(branchesOutput?.Output))
            {
                Log.Warning("GitCommandHelper: Failed to get branch list");
                return null;
            }

            var regex = new Regex(branchFilterPattern);
            var branches = SplitLines(branchesOutput!.Output)
                          .Select(b => b.Trim('\'', ' '))
                          .Where(b => !string.Equals(b, targetBranch, StringComparison.OrdinalIgnoreCase))
                          .Where(b =>
                           {
                               string nameToCheck = b;
                               if (b.StartsWith($"{remoteName}/"))
                               {
                                   nameToCheck = b.Substring(remoteName.Length + 1);
                               }

                               return regex.IsMatch(nameToCheck);
                           })
                          .ToList();

            if (branches.Count == 0)
            {
                Log.Warning("GitCommandHelper: No candidate branches found");
                return null;
            }

            // 5. Compute metrics for each branch
            var metrics = new List<Tuple<string, string, int, int>>(); // Branch, MergeBase, Behind, Ahead

            foreach (var branch in branches)
            {
                // Find merge-base (common ancestor)
                var mergeBaseOutput = RunGitCommand(
                    workingDirectory,
                    $"merge-base {branch} {targetBranch}",
                    MetricTags.CIVisibilityCommands.MergeBase);

                if (mergeBaseOutput is not { ExitCode: 0 } || string.IsNullOrWhiteSpace(mergeBaseOutput?.Output))
                {
                    continue; // Skip if no common history
                }

                var mergeBaseSha = mergeBaseOutput!.Output.Trim();

                // Count commits ahead and behind
                var revListOutput = RunGitCommand(
                    workingDirectory,
                    $"rev-list --left-right --count {branch}...{targetBranch}",
                    MetricTags.CIVisibilityCommands.RevList);

                if (revListOutput is not { ExitCode: 0 } || string.IsNullOrWhiteSpace(revListOutput?.Output))
                {
                    continue; // Skip if it cannot get commit counts
                }

                var counts = revListOutput!.Output.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (counts.Length != 2 ||
                    !int.TryParse(counts[0], out var behind) ||
                    !int.TryParse(counts[1], out var ahead))
                {
                    continue; // Skip if unexpected format or cannot parse counts
                }

                metrics.Add(Tuple.Create(branch, mergeBaseSha, behind, ahead));
            }

            if (metrics.Count == 0)
            {
                Log.Warning("GitCommandHelper: No metrics could be computed for any candidate branch");
                return null;
            }

            // 6. Sort by the "behind" metric (ascending) to find the best base branch
            metrics.Sort((a, b) => a.Item3.CompareTo(b.Item3)); // Sort by Behind (Item3)

            // Find candidates with the minimum "behind" value
            int bestBehind = metrics[0].Item3;
            var bestCandidates = metrics.Where(m => m.Item3 == bestBehind).ToList();

            // If multiple candidates with same "behind" value, prioritize default branch
            var bestCandidate = bestCandidates[0];

            if (bestCandidates.Count > 1 && !string.IsNullOrEmpty(defaultBranch))
            {
                foreach (var candidate in bestCandidates)
                {
                    if (candidate.Item1 == defaultBranch || candidate.Item1 == $"{remoteName}/{defaultBranch}")
                    {
                        bestCandidate = candidate;
                        break;
                    }
                }
            }

            bool isDefaultBranch = !string.IsNullOrEmpty(defaultBranch) &&
                                   (bestCandidate.Item1 == defaultBranch ||
                                    bestCandidate.Item1 == $"{remoteName}/{defaultBranch}");

            return new BaseBranchInfo(
                bestCandidate.Item1, // Branch
                bestCandidate.Item2, // MergeBase
                bestCandidate.Item3, // Behind
                bestCandidate.Item4, // Ahead
                isDefaultBranch);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GitCommandHelper: Error detecting base branch for '{Branch}'", targetBranch);
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[] SplitLines(string text, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        return text.Split(LineSeparators, options);
    }

    private readonly record struct LineRange(int Start, int End);
}
