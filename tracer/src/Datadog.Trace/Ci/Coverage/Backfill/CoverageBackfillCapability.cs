// <copyright file="CoverageBackfillCapability.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
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
        return HasSelectedCoverageReportSource(settings, commandLine) &&
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
    /// <returns>True when the external source is handled in-process, already verified as line-capable, or configured as a post-command XML report.</returns>
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

        if (File.Exists(externalCoveragePathValue))
        {
            if (ExternalCoverageXmlBackfill.IsLineBackfillableReport(externalCoveragePathValue, out reason))
            {
                reason = string.Empty;
                return true;
            }

            return false;
        }

        if (Path.GetExtension(externalCoveragePathValue).Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            if (IsGeneratedLineCoverageXmlCommand(commandLine))
            {
                reason = string.Empty;
                return true;
            }

            reason = "External coverage XML report must exist before skipping unless the command declares a supported line-capable XML format.";
            return false;
        }

        if (string.IsNullOrEmpty(reason))
        {
            reason = "External coverage path must point to an XML report when the report will be generated after the test command.";
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

        // Explicit project, solution, and assembly targets are safe here because coverage-active skipping uses
        // a testhost-scoped skippable request keyed by test.bundle once backfill is required. Local filters and
        // framework selectors are still rejected because they can narrow execution within the same bundle.

        var vstestTestCaseFilter = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.VstestTestCaseFilter);
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
    /// Detects threshold modes that execute after the external tool computes coverage but before Datadog can rewrite an XML file.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <returns>True when an unsupported external threshold mode was detected.</returns>
    private static bool HasUnsupportedExternalThreshold(string commandLine)
    {
        return ContainsAny(commandLine, "--threshold", "/p:threshold", "threshold=", "threshold%3d", "thresholdtype", "thresholdstat");
    }

    /// <summary>
    /// Detects generated XML report modes whose command line explicitly selects a mutable line-capable format.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <returns>True when the generated XML format is known to expose line entries that can be rewritten after the command.</returns>
    private static bool IsGeneratedLineCoverageXmlCommand(string commandLine)
    {
        return ContainsAny(commandLine, "cobertura", "opencover");
    }

    /// <summary>
    /// Checks whether a command line contains any known unsafe selector fragment.
    /// </summary>
    /// <param name="value">Command line to inspect.</param>
    /// <param name="fragments">Case-insensitive fragments to search for.</param>
    /// <returns>True when at least one fragment appears in the command line.</returns>
    private static bool ContainsAny(string value, params string[] fragments)
    {
        return fragments.Any(fragment => value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
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
