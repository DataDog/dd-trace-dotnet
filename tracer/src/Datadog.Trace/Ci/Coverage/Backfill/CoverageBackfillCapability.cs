// <copyright file="CoverageBackfillCapability.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Evaluates whether the active coverage setup can be corrected before ITR is allowed to skip tests.
/// </summary>
internal static class CoverageBackfillCapability
{
    /// <summary>
    /// Gets whether the current run has any coverage source that would require ITR coverage backfill.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <returns>True when a coverage result can become inaccurate if ITR skips tests without backfill.</returns>
    public static bool IsCoverageBackfillRequired(TestOptimizationSettings settings)
    {
        var commandLine = GetCommandLine();
        return settings.TestsSkippingEnabled == true &&
               HasSelectedCoverageReportSource(settings, commandLine);
    }

    /// <summary>
    /// Gets whether finalization should wait for a coverage result sent by a child coverage-tool process.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <returns>True when the selected coverage source is expected to arrive through session IPC.</returns>
    public static bool ShouldWaitForCoverageIpc(TestOptimizationSettings settings)
    {
        var commandLine = GetCommandLine();
        return settings.TestsSkippingEnabled == true &&
               HasSelectedCoverageReportSource(settings, commandLine) &&
               (IsCoverletCoverageCommand(commandLine) || IsMicrosoftCodeCoverageCommand(commandLine));
    }

    /// <summary>
    /// Gets whether the active coverage setup is line-capable and can be corrected safely.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <param name="reason">Reason why the setup is not safe for coverage-active skipping.</param>
    /// <returns>True when ITR may skip tests and later backfill the selected coverage source.</returns>
    public static bool IsActiveCoverageModeBackfillable(TestOptimizationSettings settings, out string reason)
    {
        var commandLine = GetCommandLine();

        if (!HasSelectedCoverageReportSource(settings, commandLine))
        {
            reason = string.Empty;
            return true;
        }

        if (HasUnsupportedSelection(commandLine, out reason))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath)))
        {
            return IsExternalCoveragePathBackfillable(commandLine, out reason);
        }

        if (IsMicrosoftCodeCoverageCommand(commandLine))
        {
            reason = "Microsoft CodeCoverage was detected without a pre-verified line-capable XML report, so coverage-active skipping is disabled.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Gets whether the command line enables a Coverlet mode that Datadog can mutate before Coverlet calculates summaries and thresholds.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <returns>True when a supported Coverlet collector or MSBuild mode is detected.</returns>
    public static bool IsCoverletCoverageCommand(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        var commandLineValue = commandLine!;
        return commandLineValue.IndexOf("xplat code coverage", StringComparison.OrdinalIgnoreCase) >= 0 ||
               commandLineValue.IndexOf("coverlet.collector", StringComparison.OrdinalIgnoreCase) >= 0 ||
               commandLineValue.IndexOf("coverlet.msbuild", StringComparison.OrdinalIgnoreCase) >= 0 ||
               commandLineValue.IndexOf("collectcoverage=true", StringComparison.OrdinalIgnoreCase) >= 0 ||
               commandLineValue.IndexOf("collectcoverage%3dtrue", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Gets whether the command line enables Microsoft CodeCoverage collection.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <returns>True when Microsoft CodeCoverage collection is detected.</returns>
    public static bool IsMicrosoftCodeCoverageCommand(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        var commandLineValue = commandLine!;
        return commandLineValue.IndexOf("code coverage", StringComparison.OrdinalIgnoreCase) >= 0 &&
               commandLineValue.IndexOf("xplat code coverage", StringComparison.OrdinalIgnoreCase) < 0;
    }

    /// <summary>
    /// Checks whether an external coverage path is safe to use before any ITR skip is applied.
    /// </summary>
    /// <param name="commandLine">Command line used to identify known in-process coverage hooks.</param>
    /// <param name="reason">Reason why the external path is not safe for coverage-active skipping.</param>
    /// <returns>True when the external source is either handled in-process or already verified as line-capable.</returns>
    private static bool IsExternalCoveragePathBackfillable(string commandLine, out string reason)
    {
        reason = string.Empty;
        var externalCoveragePath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath);
        if (IsCoverletCoverageCommand(commandLine))
        {
            reason = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(externalCoveragePath))
        {
            reason = "External coverage path is not configured.";
            return false;
        }

        var externalCoveragePathValue = externalCoveragePath!;
        if (HasUnsupportedExternalThreshold(commandLine))
        {
            reason = "An external coverage threshold was detected before Datadog can mutate the XML report.";
            return false;
        }

        if (File.Exists(externalCoveragePathValue) &&
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(externalCoveragePathValue, out reason))
        {
            reason = string.Empty;
            return true;
        }

        if (string.IsNullOrEmpty(reason))
        {
            reason = "External coverage XML was not available as a verified line-capable report before skipping.";
        }

        return false;
    }

    /// <summary>
    /// Detects local test subsetting that can make the backend aggregate broader than the current execution.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <param name="reason">Reason why the command scope is unsafe.</param>
    /// <returns>True when coverage-active skipping must be disabled for aggregate safety.</returns>
    private static bool HasUnsupportedSelection(string commandLine, out string reason)
    {
        if (ContainsAny(commandLine, "--filter", "/testcasefilter", "--testcasefilter", "/tests:", "--tests:"))
        {
            reason = "A test filter was detected; backend coverage may include candidates outside the executed subset.";
            return true;
        }

        if (ContainsAny(commandLine, "--framework", "/framework:", " -f "))
        {
            reason = "A target-framework subset was detected; backend coverage is not scoped to the selected framework.";
            return true;
        }

        if (HasExplicitTestTarget(commandLine))
        {
            reason = "A targeted project, solution, or test assembly was detected; backend coverage may include candidates outside the executed subset.";
            return true;
        }

// TODO temporary, this needs to be addressed
#pragma warning disable DD0011
        var vstestTestCaseFilter = EnvironmentHelpers.GetEnvironmentVariable("VSTEST_TESTCASEFILTER");
#pragma warning restore DD0011
        if (!string.IsNullOrWhiteSpace(vstestTestCaseFilter))
        {
            reason = "A VSTest testcase filter was detected in the environment.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Gets whether the current process has a customer-visible coverage report source, not just ITR line collection.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <param name="commandLine">Command line to inspect for coverage tool activation.</param>
    /// <returns>True when ITR skips could make a published coverage report inaccurate.</returns>
    private static bool HasSelectedCoverageReportSource(TestOptimizationSettings settings, string commandLine)
    {
        return !string.IsNullOrWhiteSpace(settings.CodeCoveragePath) ||
               !string.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath)) ||
               IsCoverletCoverageCommand(commandLine) ||
               IsMicrosoftCodeCoverageCommand(commandLine);
    }

    /// <summary>
    /// Detects explicit test project, solution, or assembly targets that can narrow the local execution scope.
    /// </summary>
    /// <param name="commandLine">Propagated test-session command line.</param>
    /// <returns>True when the command appears to target only part of the repository.</returns>
    private static bool HasExplicitTestTarget(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand)))
        {
            return false;
        }

        return ContainsAny(commandLine, ".csproj", ".fsproj", ".vbproj", ".sln", ".dll");
    }

    /// <summary>
    /// Detects threshold modes that execute after the external tool computes coverage but before Datadog can rewrite an XML file.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <returns>True when an unsupported external threshold mode was detected.</returns>
    private static bool HasUnsupportedExternalThreshold(string commandLine)
    {
        return ContainsAny(commandLine, "--threshold", "/p:threshold", "threshold=", "threshold%3d", "thresholdtype", "thresholdstat");
    }

    /// <summary>
    /// Checks whether a command line contains any known unsafe selector fragment.
    /// </summary>
    /// <param name="value">Command line to inspect.</param>
    /// <param name="fragments">Case-insensitive fragments to search for.</param>
    /// <returns>True when at least one fragment appears in the command line.</returns>
    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the propagated test-session command line, falling back to the current process command line.
    /// </summary>
    /// <returns>Command line used for coverage capability decisions.</returns>
    private static string GetCommandLine()
    {
        return EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand) ??
               Environment.CommandLine;
    }
}
