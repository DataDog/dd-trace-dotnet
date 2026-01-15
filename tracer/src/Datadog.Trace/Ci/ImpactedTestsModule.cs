// <copyright file="ImpactedTestsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Linq;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Ci;

internal sealed class ImpactedTestsModule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ImpactedTestsModule>();

    private ImpactedTestsModule(string baseCommitSha, string currentCommitSha, FileCoverageInfo[] modifiedFiles)
    {
        BaseCommitSha = baseCommitSha;
        CurrentCommitSha = currentCommitSha;
        ModifiedFiles = modifiedFiles;
    }

    /// <summary>
    /// Gets the modified files.
    /// </summary>
    public FileCoverageInfo[] ModifiedFiles { get; }

    /// <summary>
    /// Gets the current commit SHA.
    /// </summary>
    public string CurrentCommitSha { get; }

    /// <summary>
    /// Gets the base commit SHA.
    /// </summary>
    public string BaseCommitSha { get; }

    /// <summary>
    /// Creates an instance of the <see cref="ImpactedTestsModule"/> class.
    /// </summary>
    /// <param name="environmentValues">CI environment values</param>
    /// <param name="defaultBranch">Repository default branch</param>
    /// <returns>ImpactedTestModule instance</returns>
    public static ImpactedTestsModule CreateInstance(CIEnvironmentValues environmentValues, string? defaultBranch)
    {
        // Get the current commit SHA
        var currentCommitSha = environmentValues.HeadCommit ?? environmentValues.Commit ?? string.Empty;

        // Get the base commit SHA
        var baseCommitSha = environmentValues.PrBaseCommit ?? string.Empty;
        var workspacePath = environmentValues.WorkspacePath ?? string.Empty;
        FileCoverageInfo[] modifiedFiles = [];

        // Check if the base commit SHA is available
        if (!string.IsNullOrEmpty(baseCommitSha))
        {
            Log.Debug("ImpactedTestsModule: PR detected. Retrieving diff lines from Git CLI for {Path} from BaseCommit {BaseCommit} to {HeadCommit} (or recent)", workspacePath, baseCommitSha, currentCommitSha);
            // Retrieve diff files and lines from Git Diff CLI
            try
            {
                modifiedFiles = GitCommandHelper.GetGitDiffFilesAndLines(workspacePath, baseCommitSha);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ImpactedTestsModule: Git command failed.");
            }
        }
        else
        {
            Log.Debug("ImpactedTestsModule: Base commit sha not found in {Path} (current sha: {CurrentSha})", workspacePath, currentCommitSha);
        }

        // We don't have any modified files, let's try to calculate the PR base commit
        if (modifiedFiles.Length == 0)
        {
            // Retrieve diff files from Backend

            // set the new base commit SHA
            baseCommitSha = CalculateBaseCommit(workspacePath, defaultBranch, environmentValues);

            // First, we try to use the calculated base commit SHA for the diff
            if (!string.IsNullOrEmpty(baseCommitSha))
            {
                Log.Debug("ImpactedTestsModule: Retrieving diff lines from Git CLI for {Path} from BaseCommit {BaseCommit} to {HeadCommit} (or recent)", workspacePath, baseCommitSha, currentCommitSha);
                // Retrieve diff files and lines from Git Diff CLI but with the calculated base commit (always try the maximum accuracy)
                try
                {
                    modifiedFiles = GitCommandHelper.GetGitDiffFilesAndLines(workspacePath, baseCommitSha);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ImpactedTestsModule: Git command failed.");
                }
            }
        }

        if (modifiedFiles.Length == 0)
        {
            Log.Information("ImpactedTestsModule: No modified files found.");
        }

        return new ImpactedTestsModule(baseCommitSha, currentCommitSha, modifiedFiles);
    }

    /// <summary>
    /// Creates a NoOp instance of the <see cref="ImpactedTestsModule"/> class.
    /// </summary>
    /// <returns>ImpactedTestModule instance</returns>
    public static ImpactedTestsModule CreateNoOp() => new(string.Empty, string.Empty, []);

    /// <summary>
    /// Analyzes the given test for impacted tests.
    /// </summary>
    /// <param name="test">Test instance</param>
    public void Analyze(Test test)
    {
        if (ModifiedFiles.Length == 0)
        {
            // No modified files, no need to analyze
            return;
        }

        try
        {
            var tags = test.GetTags();
            Log.Debug("ImpactedTestsModule: Impacted Tests Detection is enabled for {TestName}  - {FileName} {From}..{To} ", test.Name, tags.SourceFile, tags.SourceStart, tags.SourceEnd);
            var modified = false;
            var testFiles = GetTestCoverage(tags);

            foreach (var testFile in testFiles)
            {
                if (string.IsNullOrEmpty(testFile.Path))
                {
                    continue;
                }

                var modifiedFile = ModifiedFiles.FirstOrDefault(x => testFile.Path!.EndsWith(x.Path!));
                if (modifiedFile is null)
                {
                    continue;
                }

                Log.Debug("ImpactedTestsModule: DiffFile found {File} ...", modifiedFile.Path);
                if (testFile.ExecutedBitmap is null || modifiedFile.ExecutedBitmap is null)
                {
                    Log.Debug("ImpactedTestsModule:   No line info");
                    modified = true;
                    break;
                }

                var testFileBitmap = new FileBitmap(testFile.ExecutedBitmap);
                var modifiedFileBitmap = new FileBitmap(modifiedFile.ExecutedBitmap);
                if (testFileBitmap.IntersectsWith(ref modifiedFileBitmap))
                {
                    Log.Debug("ImpactedTestsModule:   Intersecting lines. Marking test {TestName} as modified.", test.Name);
                    modified = true;
                    break;
                }
            }

            if (modified)
            {
                tags.IsModified = "true";
                TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsIsModified();
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "ImpactedTestsModule: Error analyzing Impacted Tests for {TestName}", test.Name);
        }
    }

    /// <summary>
    /// Gets the test coverage information.
    /// </summary>
    /// <param name="tags">Test tags</param>
    /// <returns>FileCoverageInfo array</returns>
    private static FileCoverageInfo[] GetTestCoverage(TestSpanTags tags)
    {
        if (string.IsNullOrEmpty(tags.SourceFile))
        {
            Log.Information("ImpactedTestsModule: No test definition file found for {TestName}", tags.Name);
            return [];
        }

        // Milestone 1 : Return only the test definition file
        var file = new FileCoverageInfo(tags.SourceFile);

        // Milestone 1.5 : Return the test definition lines
        if (tags.SourceStart is null or 0 || tags.SourceEnd is null or 0)
        {
            Log.Debug("ImpactedTestsModule: TestCoverage for {TestFile}", tags.SourceFile);
            return [file];
        }

        var executedBitmap = FileBitmap.FromActiveRange((int)tags.SourceStart, (int)tags.SourceEnd);
        file.ExecutedBitmap = executedBitmap.GetInternalArrayOrToArrayAndDispose();
        Log.Debug<string?, int, int>("ImpactedTestsModule: TestCoverage for {TestFile}: {Start}..{End}", tags.SourceFile, (int)tags.SourceStart, (int)tags.SourceEnd);
        return [file];
    }

    /// <summary>
    /// Attempts to calculate the base PR commit.
    /// </summary>
    /// <returns>Calculated commit</returns>
    private static string CalculateBaseCommit(string workingDirectory, string? defaultBranch, CIEnvironmentValues environmentValues)
    {
        Log.Debug("ImpactedTestsModule: Calculating Base Commit with: [DefaultBranch: {DefaultBranch}, TargetBranch: {TargetBranch}, PullRequestBaseBranch: {PullRequestBaseBranch}]", defaultBranch, environmentValues.Branch, environmentValues.PrBaseBranch);
        var baseBranchInfo = GitCommandHelper.DetectBaseBranch(
            workingDirectory,
            defaultBranch: defaultBranch,
            targetBranch: environmentValues.Branch,
            pullRequestBaseBranch: environmentValues.PrBaseBranch);
        var value = baseBranchInfo?.MergeBaseSha ?? string.Empty;
        Log.Debug("ImpactedTestsModule: MergeBaseSha: {MergeBaseSha}", value);
        return value;
    }
}
