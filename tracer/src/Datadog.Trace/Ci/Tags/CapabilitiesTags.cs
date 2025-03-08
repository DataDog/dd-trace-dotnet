// <copyright file="CapabilitiesTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Test Optimization capabilities tags
/// </summary>
internal static class CapabilitiesTags
{
    /// <summary>
    /// LibraryCapabilitiesTestImpactAnalysis is a tag used to indicate the test impact analysis capability of the library.
    /// </summary>
    public const string LibraryCapabilitiesTestImpactAnalysis = "_dd.library_capabilities.test_impact_analysis";

    /// <summary>
    /// LibraryCapabilitiesEarlyFlakeDetection is a tag used to indicate the early flake detection capability of the library.
    /// </summary>
    public const string LibraryCapabilitiesEarlyFlakeDetection = "_dd.library_capabilities.early_flake_detection";

    /// <summary>
    /// LibraryCapabilitiesAutoTestRetries is a tag used to indicate the auto test retries capability of the library.
    /// </summary>
    public const string LibraryCapabilitiesAutoTestRetries = "_dd.library_capabilities.auto_test_retries";

    /// <summary>
    /// LibraryCapabilitiesTestManagementQuarantine is a tag used to indicate the quarantine capability of the library.
    /// </summary>
    public const string LibraryCapabilitiesTestManagementQuarantine = "_dd.library_capabilities.test_management.quarantine";

    /// <summary>
    /// LibraryCapabilitiesTestManagementDisable is a tag used to indicate the disable capability of the library.
    /// </summary>
    public const string LibraryCapabilitiesTestManagementDisable = "_dd.library_capabilities.test_management.disable";

    /// <summary>
    /// LibraryCapabilitiesTestManagementAttemptToFix is a tag used to indicate the attempt to fix capability of the library.
    /// </summary>
    public const string LibraryCapabilitiesTestManagementAttemptToFix = "_dd.library_capabilities.test_management.attempt_to_fix";
}
