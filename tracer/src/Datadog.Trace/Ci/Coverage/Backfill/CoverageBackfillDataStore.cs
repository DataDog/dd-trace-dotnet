// <copyright file="CoverageBackfillDataStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Persists backend ITR coverage data and actual-skip state so coverage adapters running in helper domains or child tool processes can backfill safely.
/// </summary>
internal static class CoverageBackfillDataStore
{
    /// <summary>
    /// Environment variable that points to the persisted backend coverage map for this test-optimization run.
    /// </summary>
    public const string BackfillDataPathEnvironmentVariable = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_PATH";

    /// <summary>
    /// Environment variable set after the process observes at least one real ITR skip.
    /// </summary>
    public const string ActualItrSkipEnvironmentVariable = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_ACTUAL_SKIP";

    private const string BackfillFileName = "coverage-backfill.json";
    private const string ActualSkipFileName = "coverage-backfill-actual-skip";
    private const string IpcFailureFileName = "coverage-backfill-ipc-failure";
    private const string ScopedBackfillFolderName = "coverage-backfill-scopes";
    private const string ScopedActualSkipFolderName = "coverage-backfill-actual-skip-scopes";

    /// <summary>
    /// Persists backend coverage data for later coverage adapters and propagates the file path through the process environment.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="coverageBackfillData">Decoded backend coverage data returned by the skippable-tests endpoint.</param>
    public static void Persist(ITestOptimization testOptimization, CoverageBackfillData coverageBackfillData)
        => Persist(testOptimization, default, coverageBackfillData);

    /// <summary>
    /// Persists backend coverage data for a specific skippable-tests request scope.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scope">Request scope that produced the backend coverage.</param>
    /// <param name="coverageBackfillData">Decoded backend coverage data returned by the skippable-tests endpoint.</param>
    public static void Persist(ITestOptimization testOptimization, SkippableTestsRequestScope scope, CoverageBackfillData coverageBackfillData)
    {
        if (coverageBackfillData is not { IsPresent: true, IsValid: true })
        {
            return;
        }

        try
        {
            var filePath = GetBackfillDataPath(testOptimization, scope);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonHelper.SerializeObject(coverageBackfillData));
            EnvironmentHelpers.SetEnvironmentVariable(BackfillDataPathEnvironmentVariable, filePath);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting ITR coverage backfill data.");
        }
    }

    /// <summary>
    /// Loads persisted backend coverage data from the path propagated in the environment.
    /// </summary>
    /// <param name="coverageBackfillData">Decoded backend coverage data when the persisted file is available and valid.</param>
    /// <returns>True when valid backend coverage data was loaded.</returns>
    public static bool TryLoad(out CoverageBackfillData coverageBackfillData)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
        if (TryLoadScopedActualSkipCoverage(TestOptimization.Instance, out coverageBackfillData))
        {
            return true;
        }

// TODO temporary, this needs to be addressed
#pragma warning disable DD0012
        var filePath = EnvironmentHelpers.GetEnvironmentVariable(BackfillDataPathEnvironmentVariable);
#pragma warning restore DD0012
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            filePath = GetBackfillDataPath(TestOptimization.Instance);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }
        }

        try
        {
            var data = JsonHelper.DeserializeObject<CoverageBackfillData>(File.ReadAllText(filePath));
            if (data is { IsPresent: true, IsValid: true })
            {
                coverageBackfillData = data;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Records in the process environment that a test was actually skipped by ITR.
    /// </summary>
    public static void RecordActualItrSkip()
        => RecordActualItrSkip(default);

    /// <summary>
    /// Records in the process environment and shared run folder that a scope has actually skipped at least one test by ITR.
    /// </summary>
    /// <param name="scope">Request scope that produced the skip decision.</param>
    public static void RecordActualItrSkip(SkippableTestsRequestScope scope)
    {
        EnvironmentHelpers.SetEnvironmentVariable(ActualItrSkipEnvironmentVariable, "1");
        try
        {
            var filePath = GetActualSkipPath(TestOptimization.Instance);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "1");
            if (scope.HasFingerprint)
            {
                var scopedFilePath = GetScopedActualSkipPath(TestOptimization.Instance, scope.Fingerprint);
                Directory.CreateDirectory(Path.GetDirectoryName(scopedFilePath)!);
                File.WriteAllText(scopedFilePath, "1");
            }
        }
        catch (Exception ex)
        {
            TestOptimization.Instance.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting actual ITR skip state.");
        }
    }

    /// <summary>
    /// Gets whether the current process environment has observed an actual ITR skip.
    /// </summary>
    /// <returns>True when a prior test closed with the ITR skip reason in this process.</returns>
    public static bool HasActualItrSkip()
    {
// TODO temporary, this needs to be addressed
#pragma warning disable DD0012
        var actualItrSkip = EnvironmentHelpers.GetEnvironmentVariable(ActualItrSkipEnvironmentVariable);
#pragma warning restore DD0012
        if (string.Equals(actualItrSkip, "1", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            return File.Exists(GetActualSkipPath(TestOptimization.Instance));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Persists a compact marker when a selected child coverage source cannot deliver its result to the parent session.
    /// </summary>
    /// <param name="source">Coverage source that failed to send IPC.</param>
    public static void RecordCoverageIpcFailure(string source)
    {
        try
        {
            var filePath = GetIpcFailurePath(TestOptimization.Instance);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.AppendAllText(filePath, $"{DateTime.UtcNow:o} {source}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            TestOptimization.Instance.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting coverage IPC failure state.");
        }
    }

    /// <summary>
    /// Reads the compact coverage IPC failure marker, if a selected child coverage source failed to deliver a result.
    /// </summary>
    /// <param name="reason">Failure marker contents.</param>
    /// <returns>True when a coverage IPC failure marker was found.</returns>
    public static bool TryReadCoverageIpcFailure(out string reason)
    {
        reason = string.Empty;
        try
        {
            var filePath = GetIpcFailurePath(TestOptimization.Instance);
            if (!File.Exists(filePath))
            {
                return false;
            }

            reason = File.ReadAllText(filePath).Trim();
            return reason.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the deterministic backend coverage file path shared by testhost, coverage collectors, and the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the backend coverage file for this run.</returns>
    private static string GetBackfillDataPath(ITestOptimization testOptimization)
    {
        return Path.Combine(GetRunFolder(testOptimization), BackfillFileName);
    }

    /// <summary>
    /// Builds the deterministic backend coverage file path for a request scope.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scope">Request scope that produced the backend coverage.</param>
    /// <returns>Absolute path to the backend coverage file for this run and scope.</returns>
    private static string GetBackfillDataPath(ITestOptimization testOptimization, SkippableTestsRequestScope scope)
    {
        if (!scope.HasFingerprint)
        {
            return GetBackfillDataPath(testOptimization);
        }

        return Path.Combine(GetScopedBackfillFolder(testOptimization), $"{scope.Fingerprint}.json");
    }

    /// <summary>
    /// Builds the deterministic marker-file path used to share actual ITR skip state across testhost and coverage collector processes.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the actual-skip marker file for this run.</returns>
    private static string GetActualSkipPath(ITestOptimization testOptimization)
    {
        return Path.Combine(GetRunFolder(testOptimization), ActualSkipFileName);
    }

    /// <summary>
    /// Builds the deterministic marker-file path used to share actual ITR skip state for a single request scope.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scopeFingerprint">Stable request-scope fingerprint.</param>
    /// <returns>Absolute path to the actual-skip marker file for this run and scope.</returns>
    private static string GetScopedActualSkipPath(ITestOptimization testOptimization, string scopeFingerprint)
    {
        return Path.Combine(GetScopedActualSkipFolder(testOptimization), scopeFingerprint);
    }

    /// <summary>
    /// Builds the deterministic marker-file path used to share selected-source IPC delivery failures with the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the coverage IPC failure marker file.</returns>
    private static string GetIpcFailurePath(ITestOptimization testOptimization)
    {
        return Path.Combine(GetRunFolder(testOptimization), IpcFailureFileName);
    }

    /// <summary>
    /// Builds the folder that stores backend coverage files keyed by request-scope fingerprint.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the scoped backend coverage folder.</returns>
    private static string GetScopedBackfillFolder(ITestOptimization testOptimization)
    {
        return Path.Combine(GetRunFolder(testOptimization), ScopedBackfillFolderName);
    }

    /// <summary>
    /// Builds the folder that stores actual-skip markers keyed by request-scope fingerprint.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the scoped actual-skip marker folder.</returns>
    private static string GetScopedActualSkipFolder(ITestOptimization testOptimization)
    {
        return Path.Combine(GetRunFolder(testOptimization), ScopedActualSkipFolderName);
    }

    /// <summary>
    /// Loads and merges only scoped backend coverage maps whose scope recorded at least one actual ITR skip.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="coverageBackfillData">Merged backend coverage for actual skipped scopes.</param>
    /// <returns>True when at least one scoped coverage map was loaded and merged.</returns>
    private static bool TryLoadScopedActualSkipCoverage(ITestOptimization testOptimization, out CoverageBackfillData coverageBackfillData)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
        try
        {
            var actualSkipFolder = GetScopedActualSkipFolder(testOptimization);
            if (!Directory.Exists(actualSkipFolder))
            {
                return false;
            }

            var coverageMaps = new List<CoverageBackfillData>();
            foreach (var markerPath in Directory.EnumerateFiles(actualSkipFolder))
            {
                var scopeFingerprint = Path.GetFileName(markerPath);
                if (StringUtil.IsNullOrEmpty(scopeFingerprint))
                {
                    continue;
                }

                var backfillPath = Path.Combine(GetScopedBackfillFolder(testOptimization), $"{scopeFingerprint}.json");
                if (!File.Exists(backfillPath))
                {
                    continue;
                }

                var data = JsonHelper.DeserializeObject<CoverageBackfillData>(File.ReadAllText(backfillPath));
                if (data is { IsPresent: true, IsValid: true })
                {
                    coverageMaps.Add(data);
                }
            }

            var mergedCoverage = CoverageBackfillData.Merge(coverageMaps);
            if (mergedCoverage is { IsPresent: true, IsValid: true })
            {
                coverageBackfillData = mergedCoverage;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Builds the run-scoped `.dd` folder shared by all processes participating in the same test-optimization run.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the run-scoped folder.</returns>
    private static string GetRunFolder(ITestOptimization testOptimization)
    {
        var baseDirectory = testOptimization.CIValues.WorkspacePath ?? Environment.CurrentDirectory;
        return Path.Combine(baseDirectory, ".dd", testOptimization.RunId);
    }
}
