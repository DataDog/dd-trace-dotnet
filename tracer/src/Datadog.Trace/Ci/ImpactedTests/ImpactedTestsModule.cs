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

    public static void Analyze(Test test, MethodSymbolResolver.MethodSymbol methodSymbol, TestSpanTags tags)
    {
        if (IsEnabled)
        {
            Log.Information("Impacted Tests Detection is enabled for {TestName}", test.Name);

            bool modified = false;
            var testFiles = GetTestCoverage(methodSymbol);
            var modifiedFiles = GetModifiedFiles();

            foreach (var testFile in testFiles)
            {
                var modifiedFile = modifiedFiles.FirstOrDefault(x => x.Path == testFile.Path);
                if (modifiedFile is not null)
                {
                    Log.Information("DiffFile found {File} ...", modifiedFile.Path);

                    if (testFile.ExecutedBitmap is null || modifiedFile.ExecutedBitmap is null)
                    {
                        Log.Information(" No line info");
                        modified = true;
                        break;
                    }

                    var testFileBitmap = new FileBitmap(testFile.ExecutedBitmap);
                    var modifiedFileBitmap = new FileBitmap(modifiedFile.ExecutedBitmap);

                    if (testFileBitmap.IntersectsWith(ref modifiedFileBitmap))
                    {
                        Log.Information(" Intersecting lines");
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

    private static FileCoverageInfo[] GetTestCoverage(MethodSymbolResolver.MethodSymbol methodSymbol)
    {
        // Milestone 1 : Return only the test definition file
        var file = new FileCoverageInfo(methodSymbol.File);

        // Milestone 1.5 : Return the test definition lines
        if (CIEnvironmentValues.Instance.PrBaseBranch is { } prBaseBranch)
        {
            var executedBitmap = new FileBitmap(methodSymbol.StartLine, methodSymbol.EndLine);
            file.ExecutedBitmap = executedBitmap.GetInternalArrayOrToArrayAndDispose();
        }

        Log.Information<string, int, int>("TestCoverage for {TestFile}: {Start}..{End}", methodSymbol.File, methodSymbol.StartLine, methodSymbol.EndLine);

        return [file];
    }

    private static FileCoverageInfo[] GetModifiedFiles()
    {
        if (modifiedFiles is null)
        {
            // Milestone 1.5 : Return the test definition lines
            if (CIEnvironmentValues.Instance.PrBaseBranch is not { } prBaseBranch)
            {
                Log.Information("No PR detected. Retrieving only  diff files for {Path}...", CIEnvironmentValues.Instance.WorkspacePath!);
                // TODO : Milestone 1 : Retrieve diff files from Backend
                modifiedFiles = GitCommandManager.GetGitDiffFiles(CIEnvironmentValues.Instance.WorkspacePath!);
            }
            else
            {
                Log.Information("PR detected. Retrieving diff lines from gir CLI for {Path} {BaseCommit}...", CIEnvironmentValues.Instance.WorkspacePath!, CIEnvironmentValues.Instance.PrBaseBranch);
                // Milestone 1.5 : Retrieve diff files and lines from Git Diff CLI
                modifiedFiles = GitCommandManager.GetGitDiffFilesAndLines(CIEnvironmentValues.Instance.WorkspacePath!, CIEnvironmentValues.Instance.PrBaseBranch);
            }
        }

        return modifiedFiles;
    }
}
