// <copyright file="ImpactedTestsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

internal static class ImpactedTestsModule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ImpactedTestsModule));

    private static FileCoverageInfo[]? modifiedFiles = null;

    public static bool IsEnabled => CIVisibility.Settings.ImpactedTestsDetectionEnabled ?? false;

    private static string? CurrentCommit => CIEnvironmentValues.Instance.HeadCommit ?? CIEnvironmentValues.Instance.Commit;

    private static string? BaseCommit => CIEnvironmentValues.Instance.PrBaseCommit ?? GetBaseCommitFromBackend();

    public static void Analyze(Test test)
    {
        try
        {
            if (IsEnabled)
            {
                Log.Debug("Impacted Tests Detection is enabled for {TestName}", test.Name);

                var tags = test.GetTags();
                bool modified = false;
                var testFiles = GetTestCoverage(tags);
                var modifiedFiles = GetModifiedFiles();

                foreach (var testFile in testFiles)
                {
                    var modifiedFile = modifiedFiles.FirstOrDefault(x => x.Path == testFile.Path);
                    if (modifiedFile is not null)
                    {
                        Log.Debug("DiffFile found {File} ...", modifiedFile.Path);

                        if (testFile.ExecutedBitmap is null || modifiedFile.ExecutedBitmap is null)
                        {
                            Log.Debug("  No line info");
                            modified = true;
                            break;
                        }

                        var testFileBitmap = new FileBitmap(testFile.ExecutedBitmap);
                        var modifiedFileBitmap = new FileBitmap(modifiedFile.ExecutedBitmap);

                        if (testFileBitmap.IntersectsWith(ref modifiedFileBitmap))
                        {
                            Log.Debug("  Intersecting lines. Marking test {TestName} as modified.", test.Name);
                            modified = true;
                            break;
                        }
                    }
                }

                if (modified)
                {
                    tags.IsModified = "true";
                    TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsIsModified();
                }
            }
            else
            {
                Log.Debug("Impacted Tests Detection is DISABLED for {TestName}", test.Name);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "Error analyzing Impacted Tests for {TestName}", test.Name);
        }
    }

    private static FileCoverageInfo[] GetTestCoverage(TestSpanTags tags)
    {
        if (tags.SourceFile is null || tags.SourceStart is null || tags.SourceEnd is null)
        {
            Log.Warning("No test definition file found for {TestName}", tags.Name);
            return Array.Empty<FileCoverageInfo>();
        }

        // Milestone 1 : Return only the test definition file
        var file = new FileCoverageInfo(tags.SourceFile);

        // Milestone 1.5 : Return the test definition lines
        var prBase = BaseCommit;
        if (prBase is not null)
        {
            var executedBitmap = FileBitmap.FromActiveRange((int)tags.SourceStart, (int)tags.SourceEnd);
            file.ExecutedBitmap = executedBitmap.GetInternalArrayOrToArrayAndDispose();
            Log.Debug<string, int, int>("TestCoverage for {TestFile}: {Start}..{End}", tags.SourceFile, (int)tags.SourceStart, (int)tags.SourceEnd);
        }

        return [file];
    }

    private static FileCoverageInfo[] GetModifiedFiles()
    {
        if (modifiedFiles is null)
        {
            lock (Log)
            {
                if (modifiedFiles is null)
                {
                    var workspacePath = CIEnvironmentValues.Instance.WorkspacePath ?? string.Empty;
                    var prBase = BaseCommit;
                    var commit = CurrentCommit;
                    if (prBase is { Length: > 0 })
                    {
                        Log.Debug("PR detected. Retrieving diff lines from Git CLI for {Path} {BaseCommit}...{CurrentCommit}", workspacePath, prBase, commit);
                        // Milestone 1.5 : Retrieve diff files and lines from Git Diff CLI
                        try
                        {
                            modifiedFiles = AsyncUtil.RunSync(() => GitCommandHelper.GetGitDiffFilesAndLinesAsync(workspacePath, prBase, commit));
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Git command failed.");
                        }
                    }
                    else
                    {
                        Log.Debug("No PR detected.");
                    }

                    if (modifiedFiles is null)
                    {
                        // Milestone 1 : Retrieve diff files from Backend
                        modifiedFiles = GetDiffFilesFromBackend();
                    }
                }
            }
        }

        return modifiedFiles;
    }

    private static FileCoverageInfo[] GetDiffFilesFromBackend()
    {
        Log.Debug("Retrieving diff files from backend...");

        if (CIVisibility.ImpactedTestsDetectionResponse is { } response && response.Files is { Length: > 0 } files)
        {
            var res = new List<FileCoverageInfo>();
            foreach (var file in files)
            {
                res.Add(new FileCoverageInfo(file));
            }

            return res.ToArray();
        }

        return Array.Empty<FileCoverageInfo>();
    }

    private static string? GetBaseCommitFromBackend()
    {
        if (CIVisibility.ImpactedTestsDetectionResponse is { } response && response.BaseSha is { Length: > 0 } baseSha)
        {
            return baseSha;
        }

        return null;
    }
}
