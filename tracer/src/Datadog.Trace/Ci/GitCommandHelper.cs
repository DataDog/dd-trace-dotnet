// <copyright file="GitCommandHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

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
    private static readonly SHA256 Hasher = SHA256.Create();

    public static ProcessHelpers.CommandOutput? RunGitCommand(string? workingDirectory, string arguments, MetricTags.CIVisibilityCommands ciVisibilityCommand, string? input = null, bool useCache = true)
    {
        using var cd = CodeDurationRef.Create();
        string? cacheKey = null;
        workingDirectory ??= CIEnvironmentValues.Instance.SourceRoot ?? Environment.CurrentDirectory;
        if (useCache && string.IsNullOrEmpty(input))
        {
            string runId;
            if (TestOptimization.Instance is TestOptimization { } tOpt)
            {
                runId = tOpt.EnsureRunId(workingDirectory);
            }
            else
            {
                runId = TestOptimization.Instance.RunId;
            }

            var cacheFolder = Path.Combine(workingDirectory, ".dd", runId, "git");

            // Try to read from cache
            try
            {
                if (!Directory.Exists(cacheFolder))
                {
                    Directory.CreateDirectory(cacheFolder);
                }

                lock (Hasher)
                {
                    var hash = Hasher.ComputeHash(Encoding.UTF8.GetBytes(arguments));
                    cacheKey = Path.Combine(cacheFolder, BitConverter.ToString(hash).ToLowerInvariant() + ".json");
                }

                if (File.Exists(cacheKey))
                {
                    var jsonValue = File.ReadAllText(cacheKey);
                    if (JsonHelper.DeserializeObject<ProcessHelpers.CommandOutput>(jsonValue) is { } cachedOutput)
                    {
                        cachedOutput.Cached = true;
                        return cachedOutput;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in the git cache.");
            }
        }

        TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommand(ciVisibilityCommand);
        try
        {
            var safeDirectory = QuoteCommandLineArgument(workingDirectory);
            arguments = $"-c safe.directory={safeDirectory} {arguments}";
            var sw = RefStopwatch.Create();
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
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitCommandMs(ciVisibilityCommand, sw.ElapsedMilliseconds);
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
                sb.AppendLine($"       wd : {workingDirectory}");
                sb.AppendLine($"exit code : {gitOutput?.ExitCode}");
                sb.AppendLine($"  elapsed : {sw.ElapsedMilliseconds}ms");
                sb.AppendLine($"   output : {gitOutput?.Output ?? "<NULL>"}");
                if (gitOutput is not null && gitOutput.Error is { Length: > 0 } err)
                {
                    sb.AppendLine($"   error  : {err}");
                }

                var txt = StringBuilderCache.GetStringAndRelease(sb);
                Log.Debug("GitCommandHelper: Git command {Command}", txt);
            }

            // Write the git result to the cache
            try
            {
                if (useCache &&
                        !string.IsNullOrEmpty(cacheKey) &&
                        gitOutput is not null &&
                        JsonHelper.SerializeObject(gitOutput) is { } jsonValue)
                {
                        File.WriteAllText(cacheKey, jsonValue);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error writing git cache.");
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

    private static string QuoteCommandLineArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var needsQuotes = value.IndexOfAny([' ', '\t', '\n', '\r', '"']) >= 0;
        if (!needsQuotes)
        {
            return value;
        }

        var sb = StringBuilderCache.Acquire();
        sb.Append('"');

        var backslashCount = 0;
        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                backslashCount++;
                continue;
            }

            if (ch == '"')
            {
                sb.Append('\\', (backslashCount * 2) + 1);
                sb.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                sb.Append('\\', backslashCount);
                backslashCount = 0;
            }

            sb.Append(ch);
        }

        if (backslashCount > 0)
        {
            sb.Append('\\', backslashCount * 2);
        }

        sb.Append('"');
        return StringBuilderCache.GetStringAndRelease(sb);
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
        if (StringUtil.IsNullOrWhiteSpace(workingDirectory))
        {
            Log.Warning("GitCommandHelper: Cannot detect base branch because working directory is null or empty");
            return null;
        }

        try
        {
            // Step 1a - Get remote name if not provided
            if (StringUtil.IsNullOrWhiteSpace(remoteName))
            {
                var originNameOutput = RunGitCommand(workingDirectory, "config --default origin --get clone.defaultRemoteName", MetricTags.CIVisibilityCommands.GetRemote);
                remoteName = originNameOutput?.Output.Replace(Environment.NewLine, string.Empty).Trim() ?? "origin";
                Log.Debug("GitCommandHelper: Auto-detected remote name: {RemoteName}", remoteName);
            }

            if (StringUtil.IsNullOrWhiteSpace(remoteName))
            {
                Log.Warning("GitCommandHelper: Cannot detect remote because remoteName is null or empty");
                return null;
            }

            // Step 1b - Get source branch (target branch) if not provided
            if (StringUtil.IsNullOrWhiteSpace(targetBranch))
            {
                var gitOutput = RunGitCommand(workingDirectory, "branch --show-current", MetricTags.CIVisibilityCommands.GetBranch);
                targetBranch = gitOutput?.Output.Replace(Environment.NewLine, string.Empty) ?? string.Empty;
                Log.Debug("GitCommandHelper: Auto-detected source branch: {SourceBranch}", targetBranch);
            }

            // Bail out if the target branch is still empty
            if (StringUtil.IsNullOrWhiteSpace(targetBranch))
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

            // Step 2 - Build the candidate branches list and fetch them from remote
            var candidateBranches = new List<string>();

            if (!StringUtil.IsNullOrWhiteSpace(pullRequestBaseBranch))
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

                // Build the candidate list
                var branchesOutput = RunGitCommand(
                    workingDirectory,
                    $"for-each-ref --format='%(refname:short)' refs/remotes/{remoteName}",
                    MetricTags.CIVisibilityCommands.BuildCandidateList);

                if (branchesOutput?.ExitCode != 0 || StringUtil.IsNullOrWhiteSpace(branchesOutput.Output))
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

                if (mergeBaseOutput?.ExitCode != 0 || StringUtil.IsNullOrWhiteSpace(mergeBaseOutput.Output))
                {
                    continue; // Skip if no common history
                }

                var mergeBaseSha = mergeBaseOutput.Output.Trim();

                // Get ahead/behind counts
                var revListOutput = RunGitCommand(
                    workingDirectory,
                    $"rev-list --left-right --count {branch}...{targetBranch}",
                    MetricTags.CIVisibilityCommands.RevList);

                if (revListOutput?.ExitCode != 0 || StringUtil.IsNullOrWhiteSpace(revListOutput.Output))
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

            int bestAhead = int.MaxValue;
            var bestCandidate = metrics[0]; // Default to first
            var isDefaultBranch = false;

            // First pass: find minimum ahead value
            foreach (var candidate in metrics)
            {
                if (candidate.Ahead < bestAhead)
                {
                    bestAhead = candidate.Ahead;
                }
            }

            // Second pass: find the best candidate among those with minimum ahead value
            foreach (var candidate in metrics)
            {
                if (candidate.Ahead == bestAhead)
                {
                    bestCandidate = candidate;
                    isDefaultBranch = IsDefaultBranch(candidate.Branch);

                    // If this is the default branch, it's the best choice
                    if (isDefaultBranch)
                    {
                        break; // Found the best possible candidate
                    }
                }
            }

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

        bool IsDefaultBranch(string candidate) => !StringUtil.IsNullOrWhiteSpace(defaultBranch) &&
                                                  (candidate == defaultBranch ||
                                                   candidate == $"{remoteName}/{defaultBranch}");
    }

    /// <summary>
    /// Retrieves commit metadata for the specified <paramref name="commitSha"/>. If the repository is a shallow clone, the
    /// method attempts to un‑shallow it (requires Git &gt;= 2.27) so that the commit information is available.
    /// </summary>
    /// <param name="workingDirectory">Path to the git working directory.</param>
    /// <param name="commitSha">Commit SHA to retrieve.</param>
    /// <returns>A populated <see cref="CommitData"/> on success; <c>null</c> otherwise.</returns>
    public static CommitData? FetchCommitData(string workingDirectory, string commitSha)
    {
        try
        {
            // 1. Detect shallow repository
            Log.Debug("GitCommandHelper.FetchCommitData: checking if the repository is a shallow clone");
            var isShallowClone = IsShallowCloneRepository(workingDirectory);

            // 2. If shallow, un‑shallow it (git fetch) provided we have a modern git version
            if (isShallowClone)
            {
                Log.Debug("GitCommandHelper.FetchCommitData: checking the git version");
                var (major, minor, patch) = GetGitVersion(workingDirectory);
                Log.Debug<int, int, int>("GitCommandHelper.FetchCommitData: git version detected {Major}.{Minor}.{Patch}", major, minor, patch);
                if (major > 2 || (major == 2 && minor >= 27))
                {
                    // Retrieve remote name, fallback to "origin" if not set
                    var remoteName = GetRemoteName(workingDirectory);
                    Log.Debug("GitCommandHelper.FetchCommitData: remote name: {Remote}", remoteName);

                    // git fetch --update-shallow --filter="blob:none" --recurse-submodules=no --no-write-fetch-head <remoteName> <commitSha>
                    var fetchArgs =
                        $"fetch --update-shallow --filter=\"blob:none\" --recurse-submodules=no --no-write-fetch-head {remoteName} {commitSha}";
                    var fetchOutput = RunGitCommand(
                        workingDirectory,
                        fetchArgs,
                        MetricTags.CIVisibilityCommands.Fetch);

                    if (fetchOutput is null || fetchOutput.ExitCode != 0)
                    {
                        Log.Warning("GitCommandHelper.FetchCommitData: git fetch failed. Exit={ExitCode}, Error={Error}", fetchOutput?.ExitCode, fetchOutput?.Error);
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            // 3. Get commit details via `git show`
            Log.Debug("GitCommandHelper.FetchCommitData: fetching commit details for {Commit}", commitSha);
            // Example output:
            // '1f808ea4e7c068a149975e1851bd905cef56779c|,|1753691341|,|Tony Redondo|,|tony.redondo@datadoghq.com|,|1753691341|,|GitHub|,|noreply@github.com|,|Merge branch 'master' into tony/topt-get-head-commit-info'
            var showArgs = $"""show {commitSha} -s --format='%H|,|%at|,|%an|,|%ae|,|%ct|,|%cn|,|%ce|,|%B'""";
            var showOutput = RunGitCommand(
                workingDirectory,
                showArgs,
                MetricTags.CIVisibilityCommands.GetHead);

            if (showOutput is null || showOutput.ExitCode != 0 || string.IsNullOrWhiteSpace(showOutput.Output))
            {
                Log.Warning("GitCommandHelper.FetchCommitData: git show failed. Exit={ExitCode}, Error={Error}", showOutput?.ExitCode, showOutput?.Error);
                return null;
            }

            // 4. Parse output
            // The delimiter is |,| to avoid issues with commit messages that may contain commas.
            var gitLogDataArray = showOutput.Output.Trim().Split(["|,|"], StringSplitOptions.None);
            if (gitLogDataArray.Length < 8)
            {
                Log.Warning<int>("GitCommandHelper.FetchCommitData: unexpected git show output – expected ≥ 8 tokens, got {Count}", gitLogDataArray.Length);
                return null;
            }

            // Parse author and committer dates from Unix timestamp
            if (!long.TryParse(gitLogDataArray[1], out var authorUnixDate))
            {
                Log.Warning("Error parsing author date from git log output");
                return null;
            }

            if (!long.TryParse(gitLogDataArray[4], out var committerUnixDate))
            {
                Log.Warning("Error parsing committer date from git log output");
                return null;
            }

            var commit = gitLogDataArray[0];
            if (commit.StartsWith("'"))
            {
                commit = commit.Substring(1);
            }

            // The commit message may contain the `|,|` string , so we join the remaining parts.
            var commitMessage = gitLogDataArray.Length > 8 ? string.Join("|,|", gitLogDataArray.Skip(7)).Trim() : gitLogDataArray[7].Trim();
            if (commitMessage.EndsWith("'"))
            {
                commitMessage = commitMessage.Substring(0, commitMessage.Length - 1).Trim();
            }

            var commitData = new CommitData(
                CommitSha: commit,
                AuthorDate: DateTimeOffset.FromUnixTimeSeconds(authorUnixDate),
                AuthorName: gitLogDataArray[2],
                AuthorEmail: gitLogDataArray[3],
                CommitterDate: DateTimeOffset.FromUnixTimeSeconds(committerUnixDate),
                CommitterName: gitLogDataArray[5],
                CommitterEmail: gitLogDataArray[6],
                CommitMessage: commitMessage);

            Log.Debug("GitCommandHelper.FetchCommitData: completed successfully for {Commit}", commitSha);
            return commitData;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GitCommandHelper.FetchCommitData: unexpected error while fetching data for {Commit}", commitSha);
            return null;
        }
    }

    /// <summary>
    /// Determines whether the repository located at <paramref name="workingDirectory"/> is a shallow clone.
    /// </summary>
    /// <param name="workingDirectory">Path to the git working directory.</param>
    /// <returns>True if the repository is a shallow clone; otherwise, false.</returns>
    public static bool IsShallowCloneRepository(string workingDirectory)
    {
        // We need to check if the git clone is a shallow one before uploading anything.
        // In the case is a shallow clone we need to reconfigure it to upload the git tree
        // without blobs so no content will be downloaded.
        var gitRevParseShallowOutput = RunGitCommand(
            workingDirectory,
            "rev-parse --is-shallow-repository",
            MetricTags.CIVisibilityCommands.CheckShallow);
        if (gitRevParseShallowOutput is null || gitRevParseShallowOutput.ExitCode != 0)
        {
            Log.Warning("GitCommandHelper: 'git rev-parse --is-shallow-repository' command is null or exit code is not 0. Exit={ExitCode}", gitRevParseShallowOutput?.ExitCode);
            return false;
        }

        return gitRevParseShallowOutput.Output.IndexOf("true", StringComparison.OrdinalIgnoreCase) > -1;
    }

    /// <summary>
    /// Parses and returns the git version (major, minor, patch) as reported by <c>git --version</c>.
    /// </summary>
    /// <param name="workingDirectory">Path to the git working directory.</param>
    /// <returns>VersionInfo containing the major, minor, and patch version numbers.</returns>
    public static VersionInfo GetGitVersion(string workingDirectory)
    {
        var output = RunGitCommand(
            workingDirectory,
            "--version",
            MetricTags.CIVisibilityCommands.GetBranch);

        if (output is { ExitCode: 0 } && !string.IsNullOrWhiteSpace(output.Output))
        {
            // Expected format: "git version 2.41.0" or similar
            var span = output.Output.AsSpan().Trim();
            var lastSpace = span.LastIndexOf(' ');
            var versionText = span.Slice(lastSpace + 1).ToString();
            var segments = versionText.Split('.');
            int.TryParse(segments.ElementAtOrDefault(0), out var major);
            int.TryParse(segments.ElementAtOrDefault(1), out var minor);
            int.TryParse(segments.ElementAtOrDefault(2), out var patch);
            return new VersionInfo(major, minor, patch);
        }

        return new VersionInfo(0, 0, 0);
    }

    /// <summary>
    /// Attempts to obtain the default remote name for the repository located at <paramref name="workingDirectory"/>.
    /// Falls back to <c>origin</c> when no remote could be detected.
    /// </summary>
    /// <param name="workingDirectory">Path to the git working directory.</param>
    /// <returns>Remote name, or <c>origin</c> if not set.</returns>
    public static string GetRemoteName(string workingDirectory)
    {
        var output = RunGitCommand(
            workingDirectory,
            "config --default origin --get clone.defaultRemoteName",
            MetricTags.CIVisibilityCommands.GetRemote);

        return output?.Output?.Replace("\n", string.Empty).Trim() ?? "origin";
    }

    /// <summary>
    /// Retrieves a list of local commits from the repository located at <paramref name="workingDirectory"/>.
    /// </summary>
    /// <param name="workingDirectory">Path to the git working directory.</param>
    /// <returns>String array containing commit SHAs, or an empty array if no commits are found.</returns>
    public static string[] GetLocalCommits(string workingDirectory)
    {
        var gitLogOutput = RunGitCommand(workingDirectory, "log --format=%H -n 1000 --since=\"1 month ago\"", MetricTags.CIVisibilityCommands.GetLocalCommits);
        if (gitLogOutput is null)
        {
            Log.Warning("TestOptimizationClient: 'git log...' command is null");
            return [];
        }

        return gitLogOutput.Output.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
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

            // Check if the branch exists in remote
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
                $"fetch --depth 1 {remoteName} {branch}",
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

    public readonly record struct VersionInfo(int Major, int Minor, int Patch);

    public readonly record struct CommitData(
        string CommitSha,
        DateTimeOffset AuthorDate,
        string AuthorName,
        string AuthorEmail,
        DateTimeOffset CommitterDate,
        string CommitterName,
        string CommitterEmail,
        string CommitMessage);
}
