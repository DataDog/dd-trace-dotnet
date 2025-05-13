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
            // Retrieve PR list of modified files
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[] SplitLines(string text, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        return text.Split(LineSeparators, options);
    }

    private readonly record struct LineRange(int Start, int End);
}
