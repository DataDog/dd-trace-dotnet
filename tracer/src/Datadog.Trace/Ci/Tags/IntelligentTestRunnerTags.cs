// <copyright file="IntelligentTestRunnerTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Intelligent test runner related span tags
/// </summary>
internal static class IntelligentTestRunnerTags
{
    /// <summary>
    /// Intelligent Test Runner tests skipped flag
    /// </summary>
    public const string TestsSkipped = "_dd.ci.itr.tests_skipped";

    /// <summary>
    /// Test Session Intelligent Test Runner tests skipping is enabled flag
    /// </summary>
    public const string TestTestsSkippingEnabled = "test.itr.tests_skipping.enabled";

    /// <summary>
    /// Test skipped by intelligent test runner
    /// </summary>
    public const string SkippedBy = "test.skipped_by_itr";

    /// <summary>
    /// Test skipped reason by Intelligent test runner
    /// </summary>
    public const string SkippedByReason = "Skipped by Datadog Intelligent Test Runner";

    /// <summary>
    /// Intelligent Test Runner Skipping type
    /// </summary>
    public const string SkippingType = "test.itr.tests_skipping.type";

    /// <summary>
    /// Intelligent Test Runner by test skipping type
    /// </summary>
    public const string SkippingTypeTest = "test";

    /// <summary>
    /// Intelligent Test Runner skipping count
    /// </summary>
    public const string SkippingCount = "test.itr.tests_skipping.count";

    /// <summary>
    /// Unskippable trait name
    /// </summary>
    public const string UnskippableTraitName = "datadog_itr_unskippable";

    /// <summary>
    /// Unskippable tag name
    /// </summary>
    public const string UnskippableTag = "test.itr.unskippable";

    /// <summary>
    /// Forced run tag name
    /// </summary>
    public const string ForcedRunTag = "test.itr.forced_run";
}
