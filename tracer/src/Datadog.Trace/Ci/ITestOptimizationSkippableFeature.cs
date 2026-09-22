// <copyright file="ITestOptimizationSkippableFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Ci.Coverage.Backfill;

namespace Datadog.Trace.Ci;

internal interface ITestOptimizationSkippableFeature : ITestOptimizationFeature
{
    void WaitForSkippableTaskToFinish();

    IList<SkippableTest> GetSkippableTestsFromSuiteAndName(string suite, string name, string? moduleName = null);

    bool HasSkippableTests();

    /// <summary>
    /// Gets the backend correlation id for the skippable-tests response used by a local module.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle name, required when skippable requests are scoped.</param>
    /// <returns>The matching backend correlation id, or <c>null</c> when no matching response exists.</returns>
    string? GetCorrelationId(string? moduleName = null);

    /// <summary>
    /// Gets backend coverage data returned with the skippable-tests response.
    /// </summary>
    /// <returns>Decoded backend coverage data, or a missing marker when the response did not include coverage.</returns>
    CoverageBackfillData GetCoverageBackfillData();

    /// <summary>
    /// Gets whether the current run needs backend coverage before applying ITR skips.
    /// </summary>
    /// <returns>True when active coverage requires scoped backend coverage requests.</returns>
    bool IsCoverageBackfillRequired();

    /// <summary>
    /// Gets whether the backend coverage aggregate is available and safe to merge.
    /// </summary>
    /// <returns>True when coverage is present, valid, and has not been invalidated by request-scope filtering.</returns>
    bool IsCoverageBackfillSafe();

    /// <summary>
    /// Checks whether a matched skippable candidate can be skipped when active coverage is enabled.
    /// </summary>
    /// <param name="skippableTest">Backend skippable candidate matched to the current framework test.</param>
    /// <param name="moduleName">Local test module or bundle that is about to skip the test.</param>
    /// <param name="reason">Reason why skipping is unsafe when the method returns false.</param>
    /// <returns>True unless the backend marked this candidate as missing line coverage.</returns>
    bool CanSkipWithCoverageBackfill(SkippableTest skippableTest, string? moduleName, out string reason);

    /// <summary>
    /// Records coverage-backfill state once a framework has committed to skipping a test by ITR.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle that will skip the test.</param>
    void RecordTestSkipCoverageBackfill(string? moduleName = null);

    /// <summary>
    /// Records coverage-backfill state once a framework has committed to skipping a matched backend candidate by ITR.
    /// </summary>
    /// <param name="skippableTest">Backend candidate that was actually skipped.</param>
    /// <param name="moduleName">Local test module or bundle that skipped the test.</param>
    void RecordTestSkipCoverageBackfill(SkippableTest skippableTest, string? moduleName = null);

    /// <summary>
    /// Records that a test was actually skipped by Intelligent Test Runner.
    /// </summary>
    /// <param name="sessionId">Test session span id that owns the skipped test.</param>
    /// <param name="moduleName">Local test module or bundle that skipped the test.</param>
    void RecordTestSkippedByItr(ulong sessionId, string? moduleName = null);

    /// <summary>
    /// Gets whether the supplied session has observed an actual ITR skip.
    /// </summary>
    /// <param name="sessionId">Test session span id to query.</param>
    /// <returns>True when at least one test in the session closed with the ITR skip reason.</returns>
    bool HasSkippedTestsByItr(ulong sessionId);
}
