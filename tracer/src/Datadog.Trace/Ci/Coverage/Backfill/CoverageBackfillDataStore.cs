// <copyright file="CoverageBackfillDataStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Persists backend ITR coverage data and actual-skip state so coverage adapters running in helper domains or child tool processes can backfill safely.
/// </summary>
internal static class CoverageBackfillDataStore
{
    private const string BackfillFileName = "coverage-backfill.json";
    private const string ActualSkipFileName = "coverage-backfill-actual-skip";
    private const string IpcFailureFileName = "coverage-backfill-ipc-failure";
    private const string IpcResultFolderName = "coverage-backfill-ipc-results";
    private const string ScopedBackfillFolderName = "coverage-backfill-scopes";
    private const string ScopedActualSkipFolderName = "coverage-backfill-actual-skip-scopes";
    private const string ScopedBackfillableSkipFolderName = "coverage-backfill-backfillable-skip-scopes";
    private static readonly TimeSpan ReadRetryTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan ResultFolderQuietPeriod = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ResultSetQuietPeriod = TimeSpan.FromMilliseconds(250);
    // Coverlet XML fallback is produced after direct Coverlet coverage, so give that higher-priority
    // result a separate bounded window without extending unrelated file-read retries.
    private static readonly TimeSpan CoverletXmlFallbackResultTimeout = TimeSpan.FromSeconds(5);
    private static readonly StringComparison PathComparison = FrameworkDescription.Instance.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private enum CoverageBackfillLoadResult
    {
        Missing,
        Invalid,
        Loaded
    }

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
            var contents = JsonHelper.SerializeObject(coverageBackfillData);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            WriteAllTextAtomic(filePath, contents);
            if (!scope.HasFingerprint)
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, filePath);
            }
            else
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, GetRunFolder(testOptimization));
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, string.Empty);
            }

            TryMirrorBackfillDataToCurrentDirectory(testOptimization, scope, contents, filePath);
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
        => TryLoad(TestOptimization.Instance, out coverageBackfillData);

    /// <summary>
    /// Loads persisted backend coverage data for the current test-optimization run and parent session.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped data.</param>
    /// <param name="coverageBackfillData">Decoded backend coverage data when the persisted file is available and valid.</param>
    /// <returns>True when valid backend coverage data was loaded.</returns>
    public static bool TryLoad(ulong sessionId, out CoverageBackfillData coverageBackfillData)
        => TryLoad(TestOptimization.Instance, sessionId, out coverageBackfillData);

    /// <summary>
    /// Loads persisted backend coverage data for the supplied test-optimization instance.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="coverageBackfillData">Decoded backend coverage data when the persisted file is available and valid.</param>
    /// <returns>True when valid backend coverage data was loaded.</returns>
    internal static bool TryLoad(ITestOptimization testOptimization, out CoverageBackfillData coverageBackfillData)
        => TryLoad(testOptimization, sessionId: 0, out coverageBackfillData);

    /// <summary>
    /// Loads persisted backend coverage data for the supplied test-optimization instance and parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped data.</param>
    /// <param name="coverageBackfillData">Decoded backend coverage data when the persisted file is available and valid.</param>
    /// <returns>True when valid backend coverage data was loaded.</returns>
    internal static bool TryLoad(ITestOptimization testOptimization, ulong sessionId, out CoverageBackfillData coverageBackfillData)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
        if (TryLoadScopedActualSkipCoverage(testOptimization, sessionId, out coverageBackfillData, out var hasScopedActualSkips))
        {
            return true;
        }

        if (hasScopedActualSkips)
        {
            return false;
        }

        var unscopedLoadResult = TryLoadUnscopedCoverage(testOptimization, out coverageBackfillData);
        if (unscopedLoadResult == CoverageBackfillLoadResult.Loaded)
        {
            return true;
        }

        if (unscopedLoadResult == CoverageBackfillLoadResult.Invalid)
        {
            return false;
        }

        if (sessionId != 0)
        {
            // Legacy writers persisted scoped actual-skip markers without a parent session id. Preserve that
            // fallback after the current-session scoped and unscoped lookups, but still fail closed if the
            // legacy scoped state is partial.
            if (TryLoadScopedActualSkipCoverage(testOptimization, sessionId: 0, out coverageBackfillData, out hasScopedActualSkips))
            {
                return true;
            }

            if (hasScopedActualSkips)
            {
                return false;
            }
        }

        return false;
    }

    private static CoverageBackfillLoadResult TryLoadUnscopedCoverage(ITestOptimization testOptimization, out CoverageBackfillData coverageBackfillData)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
        var readResult = TryReadUnscopedCoverage(testOptimization, out var contents);
        if (readResult != CoverageBackfillLoadResult.Loaded)
        {
            return readResult;
        }

        try
        {
            var data = JsonHelper.DeserializeObject<CoverageBackfillData>(contents);
            if (data is { IsPresent: true, IsValid: true })
            {
                coverageBackfillData = data;
                return CoverageBackfillLoadResult.Loaded;
            }
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error loading ITR coverage backfill data.");
            return CoverageBackfillLoadResult.Invalid;
        }

        return CoverageBackfillLoadResult.Invalid;
    }

    private static CoverageBackfillLoadResult TryReadUnscopedCoverage(ITestOptimization testOptimization, out string contents)
    {
        var filePath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        if (!StringUtil.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return TryReadAllTextWithRetry(testOptimization, filePath!, waitForFile: false, out contents) ?
                       CoverageBackfillLoadResult.Loaded :
                       CoverageBackfillLoadResult.Invalid;
        }

        return TryReadBackfillDataFromCandidateFolders(testOptimization, out contents);
    }

    /// <summary>
    /// Records in the process environment that a test was actually skipped by ITR.
    /// </summary>
    public static void RecordActualItrSkip()
        => RecordActualItrSkip(TestOptimization.Instance, sessionId: 0, default);

    /// <summary>
    /// Records in the process environment and shared run folder that a scope has actually skipped at least one test by ITR.
    /// </summary>
    /// <param name="scope">Request scope that produced the skip decision.</param>
    public static void RecordActualItrSkip(SkippableTestsRequestScope scope)
        => RecordActualItrSkip(TestOptimization.Instance, sessionId: 0, scope);

    /// <summary>
    /// Records in the process environment and shared run folder that a session has actually skipped at least one test by ITR.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped state.</param>
    public static void RecordActualItrSkip(ulong sessionId)
        => RecordActualItrSkip(TestOptimization.Instance, sessionId, default);

    /// <summary>
    /// Records in the process environment and shared run folder that a session scope has actually skipped at least one test by ITR.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped state.</param>
    /// <param name="scope">Request scope that produced the skip decision.</param>
    public static void RecordActualItrSkip(ulong sessionId, SkippableTestsRequestScope scope)
        => RecordActualItrSkip(TestOptimization.Instance, sessionId, scope);

    /// <summary>
    /// Records that a scoped skippable-tests response has usable backend coverage for backfill.
    /// </summary>
    /// <param name="scope">Request scope that produced an actual skip and usable backend coverage in this run.</param>
    public static void RecordBackfillableItrSkipScope(SkippableTestsRequestScope scope)
        => RecordBackfillableItrSkipScope(TestOptimization.Instance, sessionId: 0, scope);

    /// <summary>
    /// Records that a scoped skippable-tests response for a parent session has usable backend coverage for backfill.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped state.</param>
    /// <param name="scope">Request scope that produced an actual skip and usable backend coverage in this run.</param>
    public static void RecordBackfillableItrSkipScope(ulong sessionId, SkippableTestsRequestScope scope)
        => RecordBackfillableItrSkipScope(TestOptimization.Instance, sessionId, scope);

    /// <summary>
    /// Records in the process environment and shared run folder that the supplied instance actually skipped at least one test by ITR.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scope">Request scope that produced the skip decision.</param>
    internal static void RecordActualItrSkip(ITestOptimization testOptimization, SkippableTestsRequestScope scope)
        => RecordActualItrSkip(testOptimization, sessionId: 0, scope);

    /// <summary>
    /// Records in the process environment and shared run folder that the supplied session actually skipped at least one test by ITR.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped state.</param>
    /// <param name="scope">Request scope that produced the skip decision.</param>
    internal static void RecordActualItrSkip(ITestOptimization testOptimization, ulong sessionId, SkippableTestsRequestScope scope)
    {
        try
        {
            var filePath = GetActualSkipPath(testOptimization, sessionId);
            WriteActualSkipMarkers(filePath, sessionId, scope);
            TryMirrorActualSkipToCurrentDirectory(testOptimization, sessionId, scope, filePath);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting actual ITR skip state.");
        }

        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, "1");
    }

    /// <summary>
    /// Records in the shared run folder that a scoped actual-skip response has usable backend coverage for backfill.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scope">Request scope that produced an actual skip and usable backend coverage.</param>
    internal static void RecordBackfillableItrSkipScope(ITestOptimization testOptimization, SkippableTestsRequestScope scope)
        => RecordBackfillableItrSkipScope(testOptimization, sessionId: 0, scope);

    /// <summary>
    /// Records in the shared run folder that a scoped actual-skip response has usable backend coverage for backfill for a parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped state.</param>
    /// <param name="scope">Request scope that produced an actual skip and usable backend coverage.</param>
    internal static void RecordBackfillableItrSkipScope(ITestOptimization testOptimization, ulong sessionId, SkippableTestsRequestScope scope)
    {
        if (!scope.HasFingerprint)
        {
            return;
        }

        try
        {
            var filePath = GetBackfillableSkipPath(testOptimization, sessionId, scope.Fingerprint);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            WriteAllTextAtomic(filePath, "1");
            TryMirrorBackfillableSkipToCurrentDirectory(testOptimization, sessionId, scope, filePath);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting backfillable ITR skip scope state.");
        }
    }

    /// <summary>
    /// Gets whether the current process environment has observed an actual ITR skip.
    /// </summary>
    /// <returns>True when a prior test closed with the ITR skip reason in this process.</returns>
    public static bool HasActualItrSkip()
        => HasActualItrSkip(TestOptimization.Instance);

    /// <summary>
    /// Gets whether the supplied test-optimization instance has observed an actual ITR skip.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>True when a prior test closed with the ITR skip reason in this process.</returns>
    internal static bool HasActualItrSkip(ITestOptimization testOptimization)
    {
        var actualItrSkip = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        if (string.Equals(actualItrSkip, "1", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            foreach (var filePath in GetActualSkipCandidatePaths(testOptimization))
            {
                if (File.Exists(filePath))
                {
                    return true;
                }
            }

            return HasScopedActualSkipMarker(testOptimization);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error reading actual ITR skip state.");
            return false;
        }
    }

    /// <summary>
    /// Gets whether the shared run folder contains actual ITR skip state written by this run.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>True when a participating process persisted an actual ITR skip marker for this run.</returns>
    internal static bool HasPersistedActualItrSkip(ITestOptimization testOptimization)
        => HasPersistedActualItrSkip(testOptimization, sessionId: 0);

    /// <summary>
    /// Gets whether the shared run folder contains actual ITR skip state written by this run and parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped state.</param>
    /// <returns>True when a participating process persisted an actual ITR skip marker for this session.</returns>
    internal static bool HasPersistedActualItrSkip(ITestOptimization testOptimization, ulong sessionId)
    {
        try
        {
            foreach (var filePath in GetActualSkipCandidatePaths(testOptimization, sessionId))
            {
                if (File.Exists(filePath))
                {
                    return true;
                }
            }

            return HasScopedActualSkipMarker(testOptimization, sessionId);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error reading persisted actual ITR skip state.");
            return false;
        }
    }

    /// <summary>
    /// Gets whether the shared run folder contains actual ITR skip state written for this session or by legacy unscoped writers.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id.</param>
    /// <returns>True when a participating process persisted an actual ITR skip marker for this session or as legacy unscoped state.</returns>
    internal static bool HasPersistedActualItrSkipForSessionOrLegacy(ITestOptimization testOptimization, ulong sessionId)
    {
        if (sessionId != 0 &&
            HasPersistedActualItrSkip(testOptimization, sessionId))
        {
            return true;
        }

        // Legacy adapters and tests can still write unscoped marker files. Only persisted markers are
        // accepted here so a stale process environment variable cannot mark a different session as skipped.
        return HasPersistedActualItrSkip(testOptimization);
    }

    /// <summary>
    /// Persists a compact marker when a selected child coverage source cannot deliver its result to the parent session.
    /// </summary>
    /// <param name="source">Coverage source that failed to send IPC.</param>
    public static void RecordCoverageIpcFailure(string source)
        => RecordCoverageIpcFailure(TestOptimization.Instance, sessionId: 0, source);

    /// <summary>
    /// Persists a compact marker when a selected child coverage source cannot deliver its result to the supplied parent session.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id.</param>
    /// <param name="source">Coverage source that failed to send IPC.</param>
    public static void RecordCoverageIpcFailure(ulong sessionId, string source)
        => RecordCoverageIpcFailure(TestOptimization.Instance, sessionId, source);

    /// <summary>
    /// Persists a coverage result before sending it over IPC so the parent session can recover it if the IPC message races with session close.
    /// </summary>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether backend ITR coverage backfill was used.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    /// <returns>Stable result identity persisted with the result, or null when the result could not be persisted.</returns>
    public static string? RecordCoverageIpcResult(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines = null, double? coveredLines = null, string? diagnostic = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null)
        => RecordCoverageIpcResult(TestOptimization.Instance, sessionId: 0, source, percentage, backfilled, executableLines, coveredLines, diagnostic, backfillValidated: backfillValidated, backfillNotApplicable: backfillNotApplicable, backfillValidation: backfillValidation);

    /// <summary>
    /// Persists a coverage result before sending it over IPC so the supplied parent session can recover it if the IPC message races with session close.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id.</param>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether backend ITR coverage backfill was used.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    /// <returns>Stable result identity persisted with the result, or null when the result could not be persisted.</returns>
    public static string? RecordCoverageIpcResult(ulong sessionId, CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines = null, double? coveredLines = null, string? diagnostic = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null)
        => RecordCoverageIpcResult(TestOptimization.Instance, sessionId, source, percentage, backfilled, executableLines, coveredLines, diagnostic, backfillValidated: backfillValidated, backfillNotApplicable: backfillNotApplicable, backfillValidation: backfillValidation);

    /// <summary>
    /// Persists a coverage result before sending it over IPC so the parent session can recover it if the IPC message races with session close.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped results.</param>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether backend ITR coverage backfill was used.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="resultId">Stable result identity for idempotent artifact-based producers, or null to generate a delivery identity.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    /// <param name="supersededResultIds">Stable identities of partial producer results represented by this merged result.</param>
    /// <returns>Stable result identity persisted with the result, or null when the result could not be persisted.</returns>
    internal static string? RecordCoverageIpcResult(ITestOptimization testOptimization, ulong sessionId, CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines = null, double? coveredLines = null, string? diagnostic = null, string? resultId = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null, string[]? supersededResultIds = null)
    {
        if (percentage < 0 || double.IsNaN(percentage) || double.IsInfinity(percentage))
        {
            return null;
        }

        try
        {
            var callerSuppliedResultId = !StringUtil.IsNullOrEmpty(resultId);
            resultId = callerSuppliedResultId ? resultId : Guid.NewGuid().ToString("N");
            var filePath = GetIpcResultPath(testOptimization, sessionId, source, resultId!);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var contents = JsonHelper.SerializeObject(
                new PersistedCoverageIpcResult
                {
                    ResultId = resultId,
                    SessionId = sessionId,
                    Source = source,
                    Percentage = percentage.ToValidPercentage(),
                    Backfilled = backfilled,
                    ExecutableLines = executableLines,
                    CoveredLines = coveredLines,
                    Diagnostic = diagnostic,
                    BackfillValidated = backfillValidated,
                    BackfillNotApplicable = backfillNotApplicable,
                    BackfillValidation = backfillValidation,
                    SupersededResultIds = supersededResultIds,
                });
            if (callerSuppliedResultId)
            {
                TryWriteAllTextAtomic(filePath, contents, overwriteExisting: false);
                return resultId;
            }

            WriteAllTextAtomic(filePath, contents);
            return resultId;
        }
        catch (Exception ex)
        {
            testOptimization.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting coverage IPC result state.");
            return null;
        }
    }

    /// <summary>
    /// Persists a compact marker when a selected child coverage source cannot deliver its result to the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="source">Coverage source that failed to send IPC.</param>
    internal static void RecordCoverageIpcFailure(ITestOptimization testOptimization, string source)
        => RecordCoverageIpcFailure(testOptimization, sessionId: 0, source);

    /// <summary>
    /// Persists a compact marker when a selected child coverage source cannot deliver its result to the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped markers.</param>
    /// <param name="source">Coverage source that failed to send IPC.</param>
    internal static void RecordCoverageIpcFailure(ITestOptimization testOptimization, ulong sessionId, string source)
    {
        try
        {
            var filePath = GetIpcFailurePath(testOptimization, sessionId);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            WriteAllTextAtomic(filePath, $"{DateTime.UtcNow:o} {source}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            testOptimization.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting coverage IPC failure state.");
        }
    }

    /// <summary>
    /// Reads the compact coverage IPC failure marker, if a selected child coverage source failed to deliver a result.
    /// </summary>
    /// <param name="reason">Failure marker contents.</param>
    /// <returns>True when a coverage IPC failure marker was found.</returns>
    public static bool TryReadCoverageIpcFailure(out string reason)
        => TryReadCoverageIpcFailure(TestOptimization.Instance, sessionId: 0, out reason);

    /// <summary>
    /// Reads the compact coverage IPC failure marker for a parent session, if a selected child coverage source failed to deliver a result.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id.</param>
    /// <param name="reason">Failure marker contents.</param>
    /// <returns>True when a coverage IPC failure marker was found.</returns>
    public static bool TryReadCoverageIpcFailure(ulong sessionId, out string reason)
        => TryReadCoverageIpcFailure(TestOptimization.Instance, sessionId, out reason);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors, if any were written before IPC delivery.
    /// </summary>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    public static bool TryReadCoverageIpcResults(out CodeCoverageAggregationResult[] results)
        => TryReadCoverageIpcResults(TestOptimization.Instance, sessionId: 0, out results);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors for a parent session, if any were written before IPC delivery.
    /// </summary>
    /// <param name="sessionId">Parent test-session span id.</param>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    public static bool TryReadCoverageIpcResults(ulong sessionId, out CodeCoverageAggregationResult[] results)
        => TryReadCoverageIpcResults(TestOptimization.Instance, sessionId, out results);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors, if any were written before IPC delivery.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    internal static bool TryReadCoverageIpcResults(ITestOptimization testOptimization, out CodeCoverageAggregationResult[] results)
        => TryReadCoverageIpcResults(testOptimization, sessionId: 0, out results);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors, if any were written before IPC delivery.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped results.</param>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    internal static bool TryReadCoverageIpcResults(ITestOptimization testOptimization, ulong sessionId, out CodeCoverageAggregationResult[] results)
        => TryReadCoverageIpcResults(testOptimization, sessionId, waitForResultFolder: false, out results);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors, if any were written before IPC delivery.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped results.</param>
    /// <param name="waitForResultFolder">Whether to wait briefly for a producer that is expected to create the result folder during session close.</param>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    internal static bool TryReadCoverageIpcResults(ITestOptimization testOptimization, ulong sessionId, bool waitForResultFolder, out CodeCoverageAggregationResult[] results)
        => TryReadCoverageIpcResults(testOptimization, sessionId, waitForResultFolder, waitForCoverletXmlFallback: true, out results, out _);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors, if any were written before IPC delivery.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped results.</param>
    /// <param name="waitForResultFolder">Whether to wait briefly for a producer that is expected to create the result folder during session close.</param>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <param name="readFailed">True when a result folder was found but could not be read as a complete stable snapshot.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    internal static bool TryReadCoverageIpcResults(ITestOptimization testOptimization, ulong sessionId, bool waitForResultFolder, out CodeCoverageAggregationResult[] results, out bool readFailed)
        => TryReadCoverageIpcResults(testOptimization, sessionId, waitForResultFolder, waitForCoverletXmlFallback: true, out results, out readFailed);

    /// <summary>
    /// Reads persisted coverage results from child coverage collectors, if any were written before IPC delivery.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped results.</param>
    /// <param name="waitForResultFolder">Whether to wait briefly for a producer that is expected to create the result folder during session close.</param>
    /// <param name="waitForCoverletXmlFallback">Whether a direct Coverlet result may still be superseded by a higher-priority XML fallback.</param>
    /// <param name="results">Coverage results recovered from the shared run folder.</param>
    /// <param name="readFailed">True when a result folder was found but could not be read as a complete stable snapshot.</param>
    /// <returns>True when at least one coverage result was recovered.</returns>
    internal static bool TryReadCoverageIpcResults(ITestOptimization testOptimization, ulong sessionId, bool waitForResultFolder, bool waitForCoverletXmlFallback, out CodeCoverageAggregationResult[] results, out bool readFailed)
    {
        readFailed = false;
        var recoveredResults = new List<CodeCoverageAggregationResult>();
        var recoveredResultIds = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var stopwatch = waitForResultFolder ? Stopwatch.StartNew() : null;
            var lastRecoveredResultTime = TimeSpan.Zero;
            while (true)
            {
                var recoveredResultCountBeforePass = recoveredResults.Count;
                foreach (var runFolder in GetRunFolderCandidates(testOptimization))
                {
                    var resultFolder = GetIpcResultFolder(runFolder, sessionId);
                    if (!Directory.Exists(resultFolder))
                    {
                        continue;
                    }

                    var resultFolderReadTimeout = ShouldWaitLongerForCoverletXmlFallback(waitForCoverletXmlFallback, recoveredResults) ?
                                                      CoverletXmlFallbackResultTimeout :
                                                      ReadRetryTimeout;
                    if (!ReadCoverageIpcResultFolder(
                            testOptimization,
                            resultFolder,
                            sessionId,
                            recoveredResults,
                            recoveredResultIds,
                            waitForResultFolder,
                            waitForCoverletXmlFallback,
                            resultFolderReadTimeout,
                            stopwatch))
                    {
                        results = [];
                        readFailed = true;
                        return false;
                    }
                }

                if (waitForResultFolder && recoveredResults.Count > recoveredResultCountBeforePass)
                {
                    lastRecoveredResultTime = stopwatch!.Elapsed;
                }

                var shouldWaitLongerForCoverletXmlFallback = ShouldWaitLongerForCoverletXmlFallback(waitForCoverletXmlFallback, recoveredResults);
                var readTimeout = shouldWaitLongerForCoverletXmlFallback ?
                                      CoverletXmlFallbackResultTimeout :
                                      ReadRetryTimeout;
                if (!waitForResultFolder ||
                    stopwatch!.Elapsed >= readTimeout ||
                    (recoveredResults.Count > 0 &&
                     !shouldWaitLongerForCoverletXmlFallback &&
                     stopwatch.Elapsed - lastRecoveredResultTime >= ResultSetQuietPeriod))
                {
                    break;
                }

                Thread.Sleep(ReadRetryDelay);
            }
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error reading coverage IPC result state.");
            results = [];
            readFailed = true;
            return false;
        }

        results = recoveredResults.ToArray();
        return results.Length > 0;
    }

    /// <summary>
    /// Keeps the close-time recovery window open when a direct Coverlet result may still be superseded by the XML fallback.
    /// </summary>
    private static bool ShouldKeepWaitingForCoverletXmlFallback(List<CodeCoverageAggregationResult> recoveredResults)
    {
        var hasCoverletResult = false;
        foreach (var result in recoveredResults)
        {
            if (result.Source == CodeCoverageReportSource.CoverletXmlFallback)
            {
                return false;
            }

            if (result.Source == CodeCoverageReportSource.Coverlet)
            {
                hasCoverletResult = true;
            }
        }

        return hasCoverletResult;
    }

    private static bool ShouldWaitLongerForCoverletXmlFallback(bool waitForCoverletXmlFallback, List<CodeCoverageAggregationResult> recoveredResults)
        => waitForCoverletXmlFallback && ShouldKeepWaitingForCoverletXmlFallback(recoveredResults);

    /// <summary>
    /// Reads the compact coverage IPC failure marker for the supplied test-optimization instance, if a selected child coverage source failed to deliver a result.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="reason">Failure marker contents.</param>
    /// <returns>True when a coverage IPC failure marker was found.</returns>
    internal static bool TryReadCoverageIpcFailure(ITestOptimization testOptimization, out string reason)
        => TryReadCoverageIpcFailure(testOptimization, sessionId: 0, out reason);

    /// <summary>
    /// Reads the compact coverage IPC failure marker for the supplied test-optimization instance, if a selected child coverage source failed to deliver a result.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped markers.</param>
    /// <param name="reason">Failure marker contents.</param>
    /// <returns>True when a coverage IPC failure marker was found.</returns>
    internal static bool TryReadCoverageIpcFailure(ITestOptimization testOptimization, ulong sessionId, out string reason)
    {
        reason = string.Empty;
        try
        {
            var filePath = GetIpcFailurePath(testOptimization, sessionId);
            var stopwatch = Stopwatch.StartNew();
            var sawTemporaryFile = false;
            while (!File.Exists(filePath))
            {
                sawTemporaryFile |= HasTemporaryAtomicFile(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
                if (!sawTemporaryFile || stopwatch.Elapsed >= ReadRetryTimeout)
                {
                    return false;
                }

                Thread.Sleep(ReadRetryDelay);
            }

            if (!TryReadAllTextWithRetry(testOptimization, filePath, waitForFile: false, out var contents))
            {
                return false;
            }

            reason = contents.Trim();
            return reason.Length > 0;
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error reading coverage IPC failure state.");
            return false;
        }
    }

    /// <summary>
    /// Gets the run-scoped folder used to exchange ITR coverage backfill files and markers across participating processes without mutating the process environment.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the shared run folder.</returns>
    internal static string GetOrCreateRunFolder(ITestOptimization testOptimization)
    {
        var runFolder = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        if (!StringUtil.IsNullOrEmpty(runFolder))
        {
            return runFolder;
        }

        var baseDirectory = GetRunFolderBaseDirectory(testOptimization);
        return Path.Combine(baseDirectory, ".dd", testOptimization.RunId);
    }

    /// <summary>
    /// Gets a fresh run-scoped folder for a new runner-owned Test Optimization run, ignoring inherited internal state from a parent process.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the new run id and workspace.</param>
    /// <returns>Absolute path to the new run folder.</returns>
    internal static string GetNewRunFolder(ITestOptimization testOptimization)
    {
        var baseDirectory = testOptimization.CIValues.WorkspacePath ?? Environment.CurrentDirectory;
        return Path.Combine(baseDirectory, ".dd", testOptimization.RunId);
    }

    /// <summary>
    /// Builds the deterministic backend coverage file path shared by testhost, coverage collectors, and the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the backend coverage file for this run.</returns>
    private static string GetBackfillDataPath(ITestOptimization testOptimization)
    {
        return GetBackfillDataPath(GetRunFolder(testOptimization));
    }

    /// <summary>
    /// Builds the deterministic backend coverage file path for a request scope.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scope">Request scope that produced the backend coverage.</param>
    /// <returns>Absolute path to the backend coverage file for this run and scope.</returns>
    private static string GetBackfillDataPath(ITestOptimization testOptimization, SkippableTestsRequestScope scope)
    {
        return GetBackfillDataPath(GetRunFolder(testOptimization), scope);
    }

    private static string GetBackfillDataPath(string runFolder)
    {
        return Path.Combine(runFolder, BackfillFileName);
    }

    private static string GetBackfillDataPath(string runFolder, SkippableTestsRequestScope scope)
    {
        return scope.HasFingerprint ? Path.Combine(GetScopedBackfillFolder(runFolder), $"{scope.Fingerprint}.json") : GetBackfillDataPath(runFolder);
    }

    /// <summary>
    /// Builds the deterministic marker-file path used to share actual ITR skip state across testhost and coverage collector processes.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the actual-skip marker file for this run.</returns>
    private static string GetActualSkipPath(ITestOptimization testOptimization)
    {
        return GetActualSkipPath(testOptimization, sessionId: 0);
    }

    private static string GetActualSkipPath(ITestOptimization testOptimization, ulong sessionId)
    {
        return GetActualSkipPath(GetRunFolder(testOptimization), sessionId);
    }

    private static string GetActualSkipPath(string runFolder)
    {
        return GetActualSkipPath(runFolder, sessionId: 0);
    }

    private static string GetActualSkipPath(string runFolder, ulong sessionId)
    {
        return sessionId == 0 ?
                   Path.Combine(runFolder, ActualSkipFileName) :
                   Path.Combine(runFolder, $"{ActualSkipFileName}-{sessionId}");
    }

    /// <summary>
    /// Builds the deterministic marker-file path used to share actual ITR skip state for a single request scope.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="scopeFingerprint">Stable request-scope fingerprint.</param>
    /// <returns>Absolute path to the actual-skip marker file for this run and scope.</returns>
    private static string GetScopedActualSkipPath(ITestOptimization testOptimization, string scopeFingerprint)
    {
        return GetScopedActualSkipPath(testOptimization, sessionId: 0, scopeFingerprint);
    }

    private static string GetScopedActualSkipPath(ITestOptimization testOptimization, ulong sessionId, string scopeFingerprint)
    {
        return GetScopedActualSkipPath(GetRunFolder(testOptimization), sessionId, scopeFingerprint);
    }

    private static string GetScopedActualSkipPath(string runFolder, string scopeFingerprint)
    {
        return GetScopedActualSkipPath(runFolder, sessionId: 0, scopeFingerprint);
    }

    private static string GetScopedActualSkipPath(string runFolder, ulong sessionId, string scopeFingerprint)
    {
        return Path.Combine(GetScopedActualSkipFolder(runFolder, sessionId), scopeFingerprint);
    }

    private static string GetBackfillableSkipPath(ITestOptimization testOptimization, string scopeFingerprint)
    {
        return GetBackfillableSkipPath(testOptimization, sessionId: 0, scopeFingerprint);
    }

    private static string GetBackfillableSkipPath(ITestOptimization testOptimization, ulong sessionId, string scopeFingerprint)
    {
        return GetBackfillableSkipPath(GetRunFolder(testOptimization), sessionId, scopeFingerprint);
    }

    private static string GetBackfillableSkipPath(string runFolder, string scopeFingerprint)
    {
        return GetBackfillableSkipPath(runFolder, sessionId: 0, scopeFingerprint);
    }

    private static string GetBackfillableSkipPath(string runFolder, ulong sessionId, string scopeFingerprint)
    {
        return Path.Combine(GetScopedBackfillableSkipFolder(runFolder, sessionId), scopeFingerprint);
    }

    /// <summary>
    /// Builds the deterministic marker-file path used to share selected-source IPC delivery failures with the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped markers.</param>
    /// <returns>Absolute path to the coverage IPC failure marker file.</returns>
    private static string GetIpcFailurePath(ITestOptimization testOptimization, ulong sessionId)
    {
        var runFolder = GetRunFolder(testOptimization);
        return sessionId == 0 ?
                   Path.Combine(runFolder, IpcFailureFileName) :
                   Path.Combine(runFolder, $"{IpcFailureFileName}-{sessionId}");
    }

    /// <summary>
    /// Builds the deterministic file path used to share a selected coverage result with the parent session.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped results.</param>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="resultId">Stable result identity for this producer result.</param>
    /// <returns>Absolute path to the persisted coverage result file.</returns>
    private static string GetIpcResultPath(ITestOptimization testOptimization, ulong sessionId, CodeCoverageReportSource source, string resultId)
    {
        return Path.Combine(GetIpcResultFolder(GetRunFolder(testOptimization), sessionId), GetIpcResultFileName(source, resultId));
    }

    private static string GetIpcResultFileName(CodeCoverageReportSource source, string resultId)
    {
        var bytes = Encoding.UTF8.GetBytes(resultId);
#if NET6_0_OR_GREATER
        var hash = SHA256.HashData(bytes);
#else
        using HashAlgorithm sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
#endif
        return $"{source}-{HexString.ToHexString(hash)}.json";
    }

    private static string GetIpcResultFolder(string runFolder, ulong sessionId)
    {
        return sessionId == 0 ?
                   Path.Combine(runFolder, IpcResultFolderName) :
                   Path.Combine(runFolder, IpcResultFolderName, $"session-{sessionId}");
    }

    /// <summary>
    /// Builds the folder that stores backend coverage files keyed by request-scope fingerprint.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the scoped backend coverage folder.</returns>
    private static string GetScopedBackfillFolder(ITestOptimization testOptimization)
    {
        return GetScopedBackfillFolder(GetRunFolder(testOptimization));
    }

    private static string GetScopedBackfillFolder(string runFolder)
    {
        return Path.Combine(runFolder, ScopedBackfillFolderName);
    }

    /// <summary>
    /// Builds the folder that stores actual-skip markers keyed by request-scope fingerprint.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the scoped actual-skip marker folder.</returns>
    private static string GetScopedActualSkipFolder(ITestOptimization testOptimization)
    {
        return GetScopedActualSkipFolder(GetRunFolder(testOptimization), sessionId: 0);
    }

    private static string GetScopedActualSkipFolder(string runFolder)
    {
        return GetScopedActualSkipFolder(runFolder, sessionId: 0);
    }

    private static string GetScopedActualSkipFolder(string runFolder, ulong sessionId)
    {
        return sessionId == 0 ?
                   Path.Combine(runFolder, ScopedActualSkipFolderName) :
                   Path.Combine(runFolder, ScopedActualSkipFolderName, $"session-{sessionId}");
    }

    private static string GetScopedBackfillableSkipFolder(string runFolder)
    {
        return GetScopedBackfillableSkipFolder(runFolder, sessionId: 0);
    }

    private static string GetScopedBackfillableSkipFolder(string runFolder, ulong sessionId)
    {
        return sessionId == 0 ?
                   Path.Combine(runFolder, ScopedBackfillableSkipFolderName) :
                   Path.Combine(runFolder, ScopedBackfillableSkipFolderName, $"session-{sessionId}");
    }

    private static CoverageBackfillLoadResult TryReadBackfillDataFromCandidateFolders(ITestOptimization testOptimization, out string contents)
    {
        foreach (var runFolder in GetRunFolderCandidates(testOptimization))
        {
            var filePath = GetBackfillDataPath(runFolder);
            if (!File.Exists(filePath))
            {
                continue;
            }

            if (TryReadAllTextWithRetry(testOptimization, filePath, waitForFile: false, out contents))
            {
                return CoverageBackfillLoadResult.Loaded;
            }

            return CoverageBackfillLoadResult.Invalid;
        }

        contents = string.Empty;
        return CoverageBackfillLoadResult.Missing;
    }

    private static IEnumerable<string> GetActualSkipCandidatePaths(ITestOptimization testOptimization)
        => GetActualSkipCandidatePaths(testOptimization, sessionId: 0);

    private static IEnumerable<string> GetActualSkipCandidatePaths(ITestOptimization testOptimization, ulong sessionId)
    {
        foreach (var runFolder in GetRunFolderCandidates(testOptimization))
        {
            yield return GetActualSkipPath(runFolder, sessionId);
        }
    }

    private static IEnumerable<string> GetRunFolderCandidates(ITestOptimization testOptimization)
    {
        var primaryRunFolder = GetRunFolder(testOptimization);
        yield return primaryRunFolder;

        if (StringUtil.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder)))
        {
            yield break;
        }

        var currentDirectoryRunFolder = GetCurrentDirectoryRunFolder(testOptimization);
        if (!PathsEqual(primaryRunFolder, currentDirectoryRunFolder))
        {
            yield return currentDirectoryRunFolder;
        }
    }

    private static string GetCurrentDirectoryRunFolder(ITestOptimization testOptimization)
    {
        return Path.Combine(Environment.CurrentDirectory, ".dd", testOptimization.RunId);
    }

    private static void TryMirrorBackfillDataToCurrentDirectory(ITestOptimization testOptimization, SkippableTestsRequestScope scope, string contents, string primaryFilePath)
    {
        try
        {
            if (StringUtil.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder)))
            {
                return;
            }

            var mirrorFilePath = GetBackfillDataPath(GetCurrentDirectoryRunFolder(testOptimization), scope);
            if (PathsEqual(primaryFilePath, mirrorFilePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(mirrorFilePath)!);
            WriteAllTextAtomic(mirrorFilePath, contents);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error mirroring ITR coverage backfill data to current directory.");
        }
    }

    private static void TryMirrorActualSkipToCurrentDirectory(ITestOptimization testOptimization, ulong sessionId, SkippableTestsRequestScope scope, string primaryFilePath)
    {
        try
        {
            if (StringUtil.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder)))
            {
                return;
            }

            var mirrorFilePath = GetActualSkipPath(GetCurrentDirectoryRunFolder(testOptimization), sessionId);
            if (PathsEqual(primaryFilePath, mirrorFilePath))
            {
                return;
            }

            WriteActualSkipMarkers(mirrorFilePath, sessionId, scope);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error mirroring actual ITR skip state to current directory.");
        }
    }

    private static void TryMirrorBackfillableSkipToCurrentDirectory(ITestOptimization testOptimization, ulong sessionId, SkippableTestsRequestScope scope, string primaryFilePath)
    {
        try
        {
            if (!scope.HasFingerprint ||
                StringUtil.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder)))
            {
                return;
            }

            var mirrorFilePath = GetBackfillableSkipPath(GetCurrentDirectoryRunFolder(testOptimization), sessionId, scope.Fingerprint);
            if (PathsEqual(primaryFilePath, mirrorFilePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(mirrorFilePath)!);
            WriteAllTextAtomic(mirrorFilePath, "1");
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error mirroring backfillable ITR skip scope state to current directory.");
        }
    }

    private static void WriteActualSkipMarkers(string filePath, ulong sessionId, SkippableTestsRequestScope scope)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        if (scope.HasFingerprint)
        {
            var runFolder = Path.GetDirectoryName(filePath)!;
            var scopedFilePath = GetScopedActualSkipPath(runFolder, sessionId, scope.Fingerprint);
            Directory.CreateDirectory(Path.GetDirectoryName(scopedFilePath)!);
            WriteAllTextAtomic(scopedFilePath, "1");
        }

        WriteAllTextAtomic(filePath, "1");
    }

    private static bool HasScopedActualSkipMarker(ITestOptimization testOptimization)
        => HasScopedActualSkipMarker(testOptimization, sessionId: 0);

    private static bool HasScopedActualSkipMarker(ITestOptimization testOptimization, ulong sessionId)
    {
        foreach (var runFolder in GetRunFolderCandidates(testOptimization))
        {
            var actualSkipFolder = GetScopedActualSkipFolder(runFolder, sessionId);
            if (!Directory.Exists(actualSkipFolder))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(actualSkipFolder))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);
    }

    /// <summary>
    /// Loads and merges only scoped backend coverage maps whose scope recorded at least one actual ITR skip.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="sessionId">Parent test-session span id, or 0 for legacy unscoped markers.</param>
    /// <param name="coverageBackfillData">Merged backend coverage for actual skipped scopes.</param>
    /// <param name="hasScopedActualSkips">True when any scoped actual-skip marker was found, even if its coverage map could not be loaded.</param>
    /// <returns>True when at least one scoped coverage map was loaded and merged.</returns>
    private static bool TryLoadScopedActualSkipCoverage(ITestOptimization testOptimization, ulong sessionId, out CoverageBackfillData coverageBackfillData, out bool hasScopedActualSkips)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
        hasScopedActualSkips = false;
        foreach (var runFolder in GetRunFolderCandidates(testOptimization))
        {
            if (TryLoadScopedActualSkipCoverage(testOptimization, runFolder, sessionId, out coverageBackfillData, out hasScopedActualSkips))
            {
                return true;
            }

            if (hasScopedActualSkips)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryLoadScopedActualSkipCoverage(ITestOptimization testOptimization, string runFolder, ulong sessionId, out CoverageBackfillData coverageBackfillData, out bool hasScopedActualSkips)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
        hasScopedActualSkips = false;
        try
        {
            var actualSkipFolder = GetScopedActualSkipFolder(runFolder, sessionId);
            if (!Directory.Exists(actualSkipFolder))
            {
                return false;
            }

            var stopwatch = Stopwatch.StartNew();
            var sawTemporaryMarker = false;
            while (true)
            {
                hasScopedActualSkips = true;
                var sawActualSkipMarker = false;
                var sawTemporaryMarkerInCurrentPass = false;
                List<CoverageBackfillData>? coverageMaps = null;
                foreach (var markerPath in Directory.EnumerateFiles(actualSkipFolder))
                {
                    var scopeFingerprint = Path.GetFileName(markerPath);
                    if (StringUtil.IsNullOrEmpty(scopeFingerprint))
                    {
                        continue;
                    }

                    if (IsTemporaryAtomicFile(markerPath))
                    {
                        sawTemporaryMarkerInCurrentPass = true;
                        continue;
                    }

                    sawActualSkipMarker = true;
                    var backfillableMarkerPath = GetBackfillableSkipPath(runFolder, sessionId, scopeFingerprint);
                    if (!File.Exists(backfillableMarkerPath))
                    {
                        sawTemporaryMarkerInCurrentPass |= HasTemporaryAtomicFileForFinalPath(backfillableMarkerPath);
                        if (stopwatch.Elapsed < ReadRetryTimeout)
                        {
                            sawTemporaryMarkerInCurrentPass = true;
                            continue;
                        }

                        return false;
                    }

                    var backfillPath = Path.Combine(GetScopedBackfillFolder(runFolder), $"{scopeFingerprint}.json");
                    if (!TryReadAllTextWithRetry(testOptimization, backfillPath, waitForFile: true, out var contents))
                    {
                        return false;
                    }

                    var data = JsonHelper.DeserializeObject<CoverageBackfillData>(contents);
                    if (data is { IsPresent: true, IsValid: true })
                    {
                        coverageMaps ??= new List<CoverageBackfillData>();
                        coverageMaps.Add(data);
                        continue;
                    }

                    return false;
                }

                sawTemporaryMarker |= sawTemporaryMarkerInCurrentPass;
                if (!sawActualSkipMarker)
                {
                    if (sawTemporaryMarker)
                    {
                        if (stopwatch.Elapsed < ReadRetryTimeout)
                        {
                            Thread.Sleep(ReadRetryDelay);
                            continue;
                        }

                        return false;
                    }

                    hasScopedActualSkips = false;
                    return false;
                }

                if (sawTemporaryMarkerInCurrentPass)
                {
                    if (stopwatch.Elapsed < ReadRetryTimeout)
                    {
                        Thread.Sleep(ReadRetryDelay);
                        continue;
                    }

                    return false;
                }

                if (coverageMaps is null)
                {
                    return false;
                }

                var mergedCoverage = CoverageBackfillData.Merge(coverageMaps);
                if (mergedCoverage is { IsPresent: true, IsValid: true })
                {
                    coverageBackfillData = mergedCoverage;
                    return true;
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error loading scoped ITR coverage backfill data.");
            return false;
        }
    }

    private static bool IsTemporaryAtomicFile(string filePath)
    {
        return filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTemporaryAtomicFileForFinalPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (StringUtil.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var fileNamePrefix = $"{Path.GetFileName(filePath)}.";
        foreach (var candidatePath in Directory.EnumerateFiles(directory))
        {
            if (Path.GetFileName(candidatePath).StartsWith(fileNamePrefix, StringComparison.Ordinal) &&
                IsTemporaryAtomicFile(candidatePath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadCoverageIpcResultFolder(
        ITestOptimization testOptimization,
        string resultFolder,
        ulong sessionId,
        List<CodeCoverageAggregationResult> recoveredResults,
        HashSet<string> recoveredResultIds,
        bool waitForResultFiles,
        bool waitForCoverletXmlFallback,
        TimeSpan readTimeout,
        Stopwatch? stopwatch = null)
    {
        stopwatch ??= Stopwatch.StartNew();
        var sawTemporaryFile = false;
        string? previousSnapshot = null;
        var previousSnapshotTime = TimeSpan.Zero;
        while (true)
        {
            var sawTemporaryFileInCurrentPass = false;
            var sawCoverletXmlFallbackTemporaryFileInCurrentPass = false;
            var hasCoverletResultFile = false;
            var resultFiles = new List<string>();
            foreach (var filePath in Directory.EnumerateFiles(resultFolder))
            {
                if (IsTemporaryAtomicFile(filePath))
                {
                    sawTemporaryFileInCurrentPass = true;
                    sawCoverletXmlFallbackTemporaryFileInCurrentPass |= IsCoverageIpcResultFileForSource(
                        filePath,
                        CodeCoverageReportSource.CoverletXmlFallback);
                    continue;
                }

                if (string.Equals(Path.GetExtension(filePath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    resultFiles.Add(filePath);
                    hasCoverletResultFile |= IsCoverageIpcResultFileForSource(filePath, CodeCoverageReportSource.Coverlet);
                }
            }

            sawTemporaryFile |= sawTemporaryFileInCurrentPass;
            resultFiles.Sort(StringComparer.Ordinal);
            var effectiveReadTimeout = waitForCoverletXmlFallback &&
                                       hasCoverletResultFile &&
                                       sawCoverletXmlFallbackTemporaryFileInCurrentPass ?
                                           CoverletXmlFallbackResultTimeout :
                                           readTimeout;
            var canReadCurrentSnapshot = !sawTemporaryFileInCurrentPass &&
                                         (!sawTemporaryFile || resultFiles.Count > 0) &&
                                         (!waitForResultFiles || resultFiles.Count > 0);
            if (waitForResultFiles && canReadCurrentSnapshot && resultFiles.Count > 0)
            {
                var snapshot = BuildResultFolderSnapshot(resultFiles);
                if (!string.Equals(snapshot, previousSnapshot, StringComparison.Ordinal))
                {
                    previousSnapshot = snapshot;
                    previousSnapshotTime = stopwatch.Elapsed;
                    canReadCurrentSnapshot = false;
                }
                else if (stopwatch.Elapsed - previousSnapshotTime < ResultFolderQuietPeriod)
                {
                    canReadCurrentSnapshot = false;
                }
            }

            if (sawTemporaryFileInCurrentPass &&
                stopwatch.Elapsed >= effectiveReadTimeout)
            {
                return false;
            }

            if (waitForResultFiles &&
                resultFiles.Count == 0 &&
                stopwatch.Elapsed >= effectiveReadTimeout)
            {
                return false;
            }

            if (canReadCurrentSnapshot ||
                stopwatch.Elapsed >= effectiveReadTimeout)
            {
                foreach (var filePath in resultFiles)
                {
                    if (!TryReadAllTextWithRetry(testOptimization, filePath, waitForFile: false, out var contents) ||
                        !TryDeserializeCoverageIpcResult(testOptimization, contents, sessionId, out var result))
                    {
                        return false;
                    }

                    if (StringUtil.IsNullOrEmpty(result.ResultId) ||
                        recoveredResultIds.Add($"{result.Source}:{result.ResultId}"))
                    {
                        recoveredResults.Add(result);
                    }
                }

                return true;
            }

            Thread.Sleep(ReadRetryDelay);
        }
    }

    /// <summary>
    /// Checks the source prefix used by persisted coverage IPC result files, including atomic temporary files.
    /// </summary>
    private static bool IsCoverageIpcResultFileForSource(string filePath, CodeCoverageReportSource source)
    {
        var fileName = Path.GetFileName(filePath);
        return !StringUtil.IsNullOrEmpty(fileName) &&
               fileName!.StartsWith($"{source}-", StringComparison.Ordinal);
    }

    private static string BuildResultFolderSnapshot(List<string> resultFiles)
    {
        return string.Join("|", resultFiles);
    }

    private static bool HasTemporaryAtomicFile(string? directoryPath, string fileNamePrefix)
    {
        if (StringUtil.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            return false;
        }

        foreach (var filePath in Directory.EnumerateFiles(directoryPath!))
        {
            var fileName = Path.GetFileName(filePath);
            if (IsTemporaryAtomicFile(filePath) &&
                !StringUtil.IsNullOrEmpty(fileName) &&
                fileName!.StartsWith(fileNamePrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryDeserializeCoverageIpcResult(ITestOptimization testOptimization, string contents, ulong sessionId, out CodeCoverageAggregationResult result)
    {
        result = default;
        try
        {
            var persistedResult = JsonHelper.DeserializeObject<PersistedCoverageIpcResult>(contents);
            if (persistedResult is null ||
                (sessionId != 0 && persistedResult.SessionId != 0 && persistedResult.SessionId != sessionId) ||
                persistedResult.Percentage < 0 ||
                double.IsNaN(persistedResult.Percentage) ||
                double.IsInfinity(persistedResult.Percentage))
            {
                return false;
            }

            result = new CodeCoverageAggregationResult(
                persistedResult.Source,
                persistedResult.Percentage.ToValidPercentage(),
                persistedResult.Backfilled,
                persistedResult.ExecutableLines,
                persistedResult.CoveredLines,
                persistedResult.Diagnostic,
                persistedResult.ResultId,
                persistedResult.BackfillValidated,
                persistedResult.BackfillNotApplicable,
                persistedResult.BackfillValidation,
                persistedResult.SupersededResultIds);
            return true;
        }
        catch (Exception ex)
        {
            testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error deserializing coverage IPC result state.");
            return false;
        }
    }

    /// <summary>
    /// Builds the run-scoped `.dd` folder shared by all processes participating in the same test-optimization run.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <returns>Absolute path to the run-scoped folder.</returns>
    private static string GetRunFolder(ITestOptimization testOptimization)
    {
        return GetOrCreateRunFolder(testOptimization);
    }

    /// <summary>
    /// Writes a file through a same-directory temporary file and atomic replacement so readers never observe partial contents.
    /// </summary>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="contents">Complete file contents.</param>
    private static void WriteAllTextAtomic(string filePath, string contents)
        => TryWriteAllTextAtomic(filePath, contents, overwriteExisting: true);

    private static bool TryWriteAllTextAtomic(string filePath, string contents, bool overwriteExisting)
    {
        var directory = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, contents);
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        if (!overwriteExisting)
                        {
                            return false;
                        }

                        File.Replace(tempPath, filePath, null);
                    }
                    else
                    {
                        File.Move(tempPath, filePath);
                    }

                    return true;
                }
                catch (IOException) when (!overwriteExisting && File.Exists(filePath))
                {
                    return false;
                }
                catch (IOException) when (File.Exists(tempPath) && stopwatch.Elapsed < ReadRetryTimeout)
                {
                    Thread.Sleep(ReadRetryDelay);
                }
                catch (UnauthorizedAccessException) when (!overwriteExisting && File.Exists(filePath))
                {
                    return false;
                }
                catch (UnauthorizedAccessException) when (File.Exists(tempPath) && stopwatch.Elapsed < ReadRetryTimeout)
                {
                    Thread.Sleep(ReadRetryDelay);
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup only; a leftover temp file is safer than corrupting the destination.
            }
        }
    }

    /// <summary>
    /// Reads a whole file with a short bounded retry for atomic-replace and cross-process file-lock races.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance used for diagnostic logging.</param>
    /// <param name="filePath">File path to read.</param>
    /// <param name="waitForFile">Whether a missing file should be retried within the bounded budget.</param>
    /// <param name="contents">Complete file contents when the read succeeds.</param>
    /// <returns>True when the file was read successfully.</returns>
    private static bool TryReadAllTextWithRetry(ITestOptimization testOptimization, string filePath, bool waitForFile, out string contents)
    {
        contents = string.Empty;
        Exception? lastTransientException = null;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    if (waitForFile && stopwatch.Elapsed < ReadRetryTimeout)
                    {
                        Thread.Sleep(ReadRetryDelay);
                        continue;
                    }

                    return false;
                }

                contents = File.ReadAllText(filePath);
                return true;
            }
            catch (IOException ex)
            {
                lastTransientException = ex;
                if (stopwatch.Elapsed < ReadRetryTimeout)
                {
                    Thread.Sleep(ReadRetryDelay);
                    continue;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                lastTransientException = ex;
                if (stopwatch.Elapsed < ReadRetryTimeout)
                {
                    Thread.Sleep(ReadRetryDelay);
                    continue;
                }
            }
            catch (Exception ex)
            {
                testOptimization.Log.Debug(ex, "CoverageBackfillDataStore: Error reading ITR coverage backfill file.");
                return false;
            }

            if (lastTransientException is not null)
            {
                testOptimization.Log.Debug(lastTransientException, "CoverageBackfillDataStore: Timed out reading ITR coverage backfill file.");
                return false;
            }
        }
    }

    /// <summary>
    /// Selects a stable base directory before falling back to the current process directory.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance.</param>
    /// <returns>Directory used to create the shared run folder when no explicit run folder was propagated.</returns>
    private static string GetRunFolderBaseDirectory(ITestOptimization testOptimization)
    {
        var sessionWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        if (!StringUtil.IsNullOrEmpty(sessionWorkingDirectory) && Path.IsPathRooted(sessionWorkingDirectory!))
        {
            return sessionWorkingDirectory!;
        }

        return testOptimization.CIValues.WorkspacePath ?? Environment.CurrentDirectory;
    }

    private sealed class PersistedCoverageIpcResult
    {
        public string? ResultId { get; set; }

        public ulong SessionId { get; set; }

        public CodeCoverageReportSource Source { get; set; }

        public double Percentage { get; set; }

        public bool Backfilled { get; set; }

        public bool BackfillValidated { get; set; }

        public bool BackfillNotApplicable { get; set; }

        public CodeCoverageBackfillValidation? BackfillValidation { get; set; }

        public string[]? SupersededResultIds { get; set; }

        public double? ExecutableLines { get; set; }

        public double? CoveredLines { get; set; }

        public string? Diagnostic { get; set; }
    }
}
