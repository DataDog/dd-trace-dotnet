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

    string? GetCorrelationId();

    /// <summary>
    /// Gets backend coverage data returned with the skippable-tests response.
    /// </summary>
    /// <returns>Decoded backend coverage data, or a missing marker when the response did not include coverage.</returns>
    CoverageBackfillData GetCoverageBackfillData();

    /// <summary>
    /// Gets whether the current run needs backend coverage before applying ITR skips.
    /// </summary>
    /// <returns>True when skipping without backfill can make the selected coverage result inaccurate.</returns>
    bool IsCoverageBackfillRequired();

    /// <summary>
    /// Gets whether the backend coverage aggregate is safe to use with the local skippable-test list.
    /// </summary>
    /// <returns>True when coverage is present, valid, and has not been invalidated by local filtering.</returns>
    bool IsCoverageBackfillSafe();

    /// <summary>
    /// Checks whether a matched skippable candidate can be skipped without making active coverage reports inaccurate.
    /// </summary>
    /// <param name="skippableTest">Backend skippable candidate matched to the current framework test.</param>
    /// <param name="reason">Reason why skipping is unsafe when the method returns false.</param>
    /// <returns>True when coverage-active skipping is safe for this candidate.</returns>
    bool CanSkipWithCoverageBackfill(SkippableTest skippableTest, out string reason);

    /// <summary>
    /// Records that a test was actually skipped by Intelligent Test Runner.
    /// </summary>
    void RecordTestSkippedByItr();

    /// <summary>
    /// Gets whether this process has observed an actual ITR skip.
    /// </summary>
    /// <returns>True when at least one test closed with the ITR skip reason.</returns>
    bool HasSkippedTestsByItr();
}
