// <copyright file="GitCommandManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.StackTraceLeak;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.ImpactedTests;

internal static class GitCommandManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GitCommandManager));

    // Regex patterns for parsing the diff output
    private static readonly Regex DiffHeaderRegex = new Regex(@"^diff --git a/(?<file>.+) b/(?<file2>.+)$");
    private static readonly Regex LineChangeRegex = new Regex(@"^@@ -\d+(,\d+)? \+(?<start>\d+)(,(?<count>\d+))? @@");

    public static List<string> GetGitDiffFiles(string folder, string baseCommit)
    {
        List<string> res = new List<string>();
        try
        {
            // Retrieve PR list of modified files
            var modifiedFiles = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    cmd: "git",
                    arguments: $"diff --name-only {baseCommit}",
                    workingDirectory: folder,
                    useWhereIsIfFileNotFound: true));
            if (modifiedFiles?.ExitCode != 0)
            {
                Log.Information("Error calling git diff");
                return res;
            }

            res = modifiedFiles.Output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error calling git diff");
        }

        return res;
    }

    public static List<FileCoverageInfo> GetGitDiffFilesAndLines(string folder, string baseCommit)
    {
        List<FileCoverageInfo> res = new List<FileCoverageInfo>();
        try
        {
            // Retrieve PR list of modified files
            var modifiedFiles = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    cmd: "git",
                    arguments: $"diff --name-only {baseCommit}",
                    workingDirectory: folder,
                    useWhereIsIfFileNotFound: true));
            if (modifiedFiles?.ExitCode != 0)
            {
                Log.Information("Error calling git diff");
                return res;
            }

            res = ParseGitDiff(modifiedFiles.Output);
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error calling git diff");
        }

        return res;

        // Parses the Git diff output to extract modified files and their changed lines
        static List<FileCoverageInfo> ParseGitDiff(string diffOutput)
        {
            var fileChanges = new List<FileCoverageInfo>();
            FileCoverageInfo? currentFile = null;
            List<Tuple<int, int>> modifiedLines = new List<Tuple<int, int>>(100);

            // Split the diff output into lines for processing
            var lines = diffOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

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
                    continue;
                }

                // Check for the line change marker (e.g., @@ -1,2 +3,4 @@)
                var lineChangeMatch = LineChangeRegex.Match(line);
                if (lineChangeMatch.Success)
                {
                    int startLine = int.Parse(lineChangeMatch.Groups["start"].Value); // Start tracking new lines
                    int lineCount = 0;
                    if (lineChangeMatch.Groups["count"].Value is string countTxt)
                    {
                        lineCount = int.Parse(countTxt); // Start tracking new lines
                    }

                    modifiedLines.Add(new Tuple<int, int>(startLine, startLine + lineCount));
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

                var last = modifiedLines[modifiedLines.Count - 1];
                var maxCount = modifiedLines[modifiedLines.Count - 1].Item2;

                var bitmap = new FileBitmap(maxCount);
                foreach (var tuple in modifiedLines)
                {
                    for (int i = tuple.Item1; i < tuple.Item2; i++)
                    {
                        bitmap.Set(i);
                    }
                }

                return bitmap.GetInternalArrayOrToArrayAndDispose();
            }
        }
    }
}
