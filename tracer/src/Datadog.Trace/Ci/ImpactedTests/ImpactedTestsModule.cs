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
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Ci.ImpactedTests;

internal static class ImpactedTestsModule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ImpactedTestsModule));

    private static FileCoverageInfo[]? modifiedFiles = null;

    public static bool IsEnabled => CIVisibility.Settings.ImpactedTestsDetection;

    public static void Analyze(Test test, TestSpanTags tags)
    {
        if (IsEnabled)
        {
            Log.Debug("Impacted Tests Detection is enabled for {TestName}", test.Name);

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
                        Log.Debug(" No line info");
                        modified = true;
                        break;
                    }

                    var testFileBitmap = new FileBitmap(testFile.ExecutedBitmap);
                    var modifiedFileBitmap = new FileBitmap(modifiedFile.ExecutedBitmap);

                    if (testFileBitmap.IntersectsWith(ref modifiedFileBitmap))
                    {
                        Log.Debug(" Intersecting lines");
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
        if (CIEnvironmentValues.Instance.PrBaseBranch is { } prBaseBranch)
        {
            var executedBitmap = new FileBitmap((int)tags.SourceStart, (int)tags.SourceEnd);
            file.ExecutedBitmap = executedBitmap.GetInternalArrayOrToArrayAndDispose();
        }

        Log.Debug<string, int, int>("TestCoverage for {TestFile}: {Start}..{End}", tags.SourceFile, (int)tags.SourceStart, (int)tags.SourceEnd);

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
                    var prBaseBranch = CIEnvironmentValues.Instance.PrBaseBranch;
                    // Milestone 1.5 : Return the test definition lines
                    if (prBaseBranch is null)
                    {
                        Log.Debug("No PR detected. Retrieving only  diff files for {Path}...", CIEnvironmentValues.Instance.WorkspacePath!);
                        // TODO : Milestone 1 : Retrieve diff files from Backend
                        modifiedFiles = GitCommandManager.GetGitDiffFiles(CIEnvironmentValues.Instance.WorkspacePath!);
                    }
                    else
                    {
                        Log.Debug("PR detected. Retrieving diff lines from gir CLI for {Path} {BaseCommit}...", CIEnvironmentValues.Instance.WorkspacePath!, CIEnvironmentValues.Instance.PrBaseBranch);
                        // Milestone 1.5 : Retrieve diff files and lines from Git Diff CLI
                        modifiedFiles = GitCommandManager.GetGitDiffFilesAndLines(CIEnvironmentValues.Instance.WorkspacePath!, prBaseBranch);
                    }
                }
            }
        }

        return modifiedFiles;
    }
}
