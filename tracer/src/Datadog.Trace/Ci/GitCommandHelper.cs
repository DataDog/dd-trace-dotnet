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
    private static readonly Regex DiffHeaderRegex = new Regex(@"^diff --git a/(?<file>.+) b/(?<file2>.+)$");
    private static readonly Regex LineChangeRegex = new Regex(@"^@@ -\d+(,\d+)? \+(?<start>\d+)(,(?<count>\d+))? @@");
    private static char[] lineSeparators = { '\n', '\r' };

    public static async Task<ProcessHelpers.CommandOutput?> RunGitCommandAsync(string? workingDirectory, string arguments, MetricTags.CIVisibilityCommands ciVisibilityCommand, string? input = null)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommand(ciVisibilityCommand);
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var gitOutput = await ProcessHelpers.RunCommandAsync(
                                new ProcessHelpers.Command(
                                    "git",
                                    arguments,
                                    workingDirectory,
                                    outputEncoding: Encoding.Default,
                                    errorEncoding: Encoding.Default,
                                    inputEncoding: Encoding.Default,
                                    useWhereIsIfFileNotFound: true),
                                input).ConfigureAwait(false);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitCommandMs(ciVisibilityCommand, sw.Elapsed.TotalMilliseconds);
            if (gitOutput is null)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(ciVisibilityCommand, MetricTags.CIVisibilityExitCodes.Unknown);
                Log.Warning("GitCommand: 'git {Arguments}' command is null", arguments);
            }
            else if (gitOutput.ExitCode != 0)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(MetricTags.CIVisibilityCommands.GetRepository, TelemetryHelper.GetTelemetryExitCodeFromExitCode(gitOutput.ExitCode));
            }

            return gitOutput;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Warning(ex, "GitCommand: 'git {Arguments}' threw Win32Exception - git is likely not available", arguments);
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(ciVisibilityCommand, MetricTags.CIVisibilityExitCodes.Missing);
            return null;
        }
    }

    public static async Task<FileCoverageInfo[]> GetGitDiffFilesAndLinesAsync(string workingDirectory, string baseCommit, string? headCommit = null)
    {
        try
        {
            // Retrieve PR list of modified files
            var arguments = $"diff -U0 --word-diff=porcelain {baseCommit}";
            if (!string.IsNullOrEmpty(headCommit))
            {
                arguments += $" {headCommit}";
            }

            var output = await RunGitCommandAsync(workingDirectory, arguments, MetricTags.CIVisibilityCommands.Diff).ConfigureAwait(false);
            if (output is not null)
            {
                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug("Git command : {Command}", $"git {arguments}");
                    Log.Debug("     output : {Output}", output.Output);
                }

                return ParseGitDiff(output.Output).ToArray();
            }
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error calling git diff");
            throw;
        }

        return Array.Empty<FileCoverageInfo>();

        // Parses the Git diff output to extract modified files and their changed lines
        static List<FileCoverageInfo> ParseGitDiff(string diffOutput)
        {
            var fileChanges = new List<FileCoverageInfo>();
            FileCoverageInfo? currentFile = null;
            var modifiedLines = new List<Tuple<int, int>>(100);

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

                    currentFile = new FileCoverageInfo(headerMatch.Groups["file"].Value);
                    Log.Debug("  Processing {File} ...", currentFile.Path);

                    continue;
                }

                // Check for the line change marker (e.g., @@ -1,2 +3,4 @@)
                var lineChangeMatch = LineChangeRegex.Match(line);
                if (lineChangeMatch.Success)
                {
                    int startLine = int.Parse(lineChangeMatch.Groups["start"].Value); // Start tracking new lines
                    int lineCount = 0;
                    if (lineChangeMatch.Groups["count"].Value is string countTxt && countTxt.Length > 0)
                    {
                        lineCount = int.Parse(countTxt); // Start tracking new lines
                    }

                    modifiedLines.Add(new Tuple<int, int>(startLine, startLine + lineCount));
                    var range = modifiedLines[modifiedLines.Count - 1];
                    Log.Debug<int, int>("    {From}..{To} ...", range.Item1, range.Item2);
                    continue;
                }
            }

            // Add the last file to the result (if any)
            if (currentFile != null)
            {
                currentFile.ExecutedBitmap = ToFileBitmap(modifiedLines);
                fileChanges.Add(currentFile);
            }

            return fileChanges;

            static byte[]? ToFileBitmap(List<Tuple<int, int>> modifiedLines)
            {
                if (modifiedLines.Count == 0)
                {
                    return null;
                }

                var maxCount = modifiedLines[modifiedLines.Count - 1].Item2;

                var bitmap = FileBitmap.FromLineCount(maxCount);
                foreach (var tuple in modifiedLines)
                {
                    for (int i = tuple.Item1; i <= tuple.Item2; i++)
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
        return text.Split(lineSeparators, options);
    }
}
