// <copyright file="ConfigurationKeys.CIVisibilityInternal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration;

internal static partial class ConfigurationKeys
{
    /// <summary>
    /// Internal environment marker set when the current process observes at least one test skipped by Intelligent Test Runner while coverage backfill is active.
    /// </summary>
    public const string CIVisibilityItrCoverageBackfillActualSkip = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_ACTUAL_SKIP";

    /// <summary>
    /// Internal environment variable containing the file path used to share backend ITR coverage backfill data between the test session, testhost, and coverage-tool processes.
    /// </summary>
    public const string CIVisibilityItrCoverageBackfillPath = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_PATH";

    /// <summary>
    /// Internal environment variable containing the run-scoped folder used to exchange ITR coverage backfill files and markers between test session, testhost, and coverage-tool processes.
    /// </summary>
    public const string CIVisibilityItrCoverageBackfillRunFolder = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_RUN_FOLDER";

    /// <summary>
    /// VSTest environment variable used to pass a testcase filter to child testhost processes.
    /// </summary>
    public const string VstestTestCaseFilter = "VSTEST_TESTCASEFILTER";
}
