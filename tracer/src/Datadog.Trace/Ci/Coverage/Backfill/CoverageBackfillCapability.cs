// <copyright file="CoverageBackfillCapability.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Evaluates whether the active coverage setup can be corrected before ITR is allowed to skip tests.
/// </summary>
internal static class CoverageBackfillCapability
{
    /// <summary>
    /// Known MSBuild property that explicitly selects an external report format.
    /// </summary>
    private const string GeneratedLineCoverageXmlFormatProperty = "coverletoutputformat";

    /// <summary>
    /// Known command-line options that identify local test filters which can narrow execution inside a backend coverage scope.
    /// </summary>
    private static readonly string[] UnsupportedTestFilterOptions =
    [
        "--filter",
        "--filter-class",
        "--filter-method",
        "--filter-namespace",
        "--filter-not-class",
        "--filter-not-method",
        "--filter-not-namespace",
        "--filter-not-trait",
        "--filter-query",
        "--filter-trait",
        "--filter-uid",
        "--treenode-filter",
        "/testcasefilter",
        "--testcasefilter",
        "--test",
        "--testlist",
        "/tests:",
        "--tests:",
        "--where",
        "-class",
        "-class-",
        "-filter",
        "-method",
        "-method-",
        "-namespace",
        "-namespace-",
        "-notclass",
        "-notmethod",
        "-notnamespace",
        "-notrait",
        "-test",
        "-testlist",
        "-trait",
        "-trait-",
        "-where"
    ];

    /// <summary>
    /// Known command-line options that identify target-framework selectors which can change the executed code path inside a backend coverage scope.
    /// </summary>
    private static readonly string[] UnsupportedFrameworkFilterOptions = ["--framework", "-framework", "/framework:"];

    /// <summary>
    /// Known MSBuild properties that identify local test filters which can narrow execution inside a backend coverage scope.
    /// </summary>
    private static readonly string[] UnsupportedTestFilterProperties = ["vstesttestcasefilter"];

    /// <summary>
    /// Known MSBuild properties that identify target-framework selectors which can change the executed code path inside a backend coverage scope.
    /// </summary>
    private static readonly string[] UnsupportedFrameworkFilterProperties = ["targetframework", "targetframeworks", "targetframeworkversion"];

    /// <summary>
    /// Known command-line options that identify coverage thresholds evaluated before Datadog can rewrite an external report.
    /// </summary>
    private static readonly string[] UnsupportedExternalThresholdOptions = ["--threshold"];

    /// <summary>
    /// Known external XML coverage formats whose line entries can be reconciled after the coverage command finishes.
    /// </summary>
    private static readonly string[] GeneratedLineCoverageXmlFormats = ["cobertura", "opencover"];

    /// <summary>
    /// Known dotnet-coverage short-format values whose generated reports expose mutable line entries.
    /// </summary>
    private static readonly string[] DotnetCoverageGeneratedLineCoverageXmlFormats = ["xml", "cobertura"];

    /// <summary>
    /// Known Microsoft Testing Platform CodeCoverage formats whose generated reports expose mutable line entries.
    /// </summary>
    private static readonly string[] MicrosoftTestingPlatformGeneratedLineCoverageXmlFormats = ["xml", "cobertura"];

    /// <summary>
    /// Known MSBuild property that identifies an active Coverlet threshold evaluated before Datadog can rewrite an external report.
    /// </summary>
    private static readonly string[] ExternalThresholdProperties = ["threshold"];

    /// <summary>
    /// Known Coverlet threshold types that can be reconciled by line-only coverage backfill.
    /// </summary>
    private static readonly string[] LineThresholdTypes = ["line"];

    /// <summary>
    /// Synchronizes lazy command-line resolution so all capability checks in a process use one stable command.
    /// </summary>
    private static readonly object CommandLineLock = new();

    /// <summary>
    /// Cached command line used for coverage capability decisions in the current process.
    /// </summary>
    private static string? _cachedCommandLine;

    /// <summary>
    /// Gets whether the current run has any coverage source that would require ITR coverage backfill.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <returns>True when a coverage result can become inaccurate if ITR skips tests without backfill.</returns>
    public static bool IsCoverageBackfillRequired(TestOptimizationSettings settings)
    {
        var command = CoverageBackfillCommandLine.Parse(GetCommandLine(), GetCommandWorkingDirectory());
        return settings.TestsSkippingEnabled == true &&
               HasSelectedCoverageReportSource(settings, command);
    }

    /// <summary>
    /// Gets whether finalization should wait for a coverage result sent by a child coverage-tool process.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <returns>True when the selected coverage source is expected to arrive through session IPC.</returns>
    public static bool ShouldWaitForCoverageIpc(TestOptimizationSettings settings)
    {
        var command = CoverageBackfillCommandLine.Parse(GetCommandLine(), GetCommandWorkingDirectory());
        return HasSelectedCoverageReportSource(settings, command) &&
               ShouldWaitForCoverageIpc(command);
    }

    /// <summary>
    /// Gets whether persisted coverage recovery should wait for a Coverlet collector XML fallback.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <returns>True when a selected Coverlet collector source can produce a higher-priority XML fallback.</returns>
    internal static bool ShouldWaitForCoverletXmlFallback(TestOptimizationSettings settings)
    {
        var workingDirectory = GetCommandWorkingDirectory();
        var command = CoverageBackfillCommandLine.Parse(GetCommandLine(), workingDirectory);
        return HasSelectedCoverageReportSource(settings, command) &&
               ShouldWaitForCoverletXmlFallback(command, workingDirectory);
    }

    /// <summary>
    /// Gets whether the active coverage setup is line-capable and can be corrected safely.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <param name="reason">Reason why the setup is not safe for coverage-active skipping.</param>
    /// <returns>True when ITR may skip tests and later backfill the selected coverage source.</returns>
    public static bool IsActiveCoverageModeBackfillable(TestOptimizationSettings settings, out string reason)
    {
        var command = CoverageBackfillCommandLine.Parse(GetCommandLine(), GetCommandWorkingDirectory());

        if (!HasSelectedCoverageReportSource(settings, command))
        {
            reason = string.Empty;
            return true;
        }

        if (HasUnsupportedSelection(command, out reason))
        {
            return false;
        }

        if (HasUnsupportedCoverletThreshold(command))
        {
            reason = "A non-line Coverlet coverage threshold was detected before Datadog can reconcile line coverage.";
            return false;
        }

        if (!StringUtil.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath)))
        {
            return IsExternalCoveragePathBackfillable(command, out reason);
        }

        if (IsCoverletTestingPlatformCoverageCommand(command) ||
            IsMicrosoftTestingPlatformCoverageCommand(command) ||
            IsDotnetCoverageCommand(command))
        {
            reason = "The selected coverage tool requires a supported external XML report path for ITR coverage backfill.";
            return false;
        }

        if (IsMicrosoftCodeCoverageCommand(command) && !command.UsesMicrosoftCodeCoverageXmlRunSettings(GetCommandWorkingDirectory()))
        {
            reason = "Microsoft CodeCoverage requires collect format or runsettings that declare XML output for ITR coverage backfill.";
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
        return IsCoverletCoverageCommand(CoverageBackfillCommandLine.Parse(commandLine));
    }

    /// <summary>
    /// Gets whether the command line enables Microsoft CodeCoverage collection.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <returns>True when Microsoft CodeCoverage collection is detected.</returns>
    public static bool IsMicrosoftCodeCoverageCommand(string? commandLine)
    {
        return IsMicrosoftCodeCoverageCommand(CoverageBackfillCommandLine.Parse(commandLine));
    }

    /// <summary>
    /// Gets whether the configured external XML coverage path should be read for the current command.
    /// </summary>
    /// <param name="externalCoveragePath">Configured external coverage report path.</param>
    /// <param name="reason">Reason why the path should not be processed.</param>
    /// <returns>True when the current command can safely own or reuse the configured external path.</returns>
    internal static bool CanProcessExternalCoveragePathForCurrentCommand(string externalCoveragePath, out string reason)
    {
        reason = string.Empty;
        var command = CoverageBackfillCommandLine.Parse(GetCommandLine(), GetCommandWorkingDirectory());
        if (!TryIsXmlPath(externalCoveragePath, out var isXmlPath))
        {
            reason = "External coverage path could not be parsed as a valid file path.";
            return false;
        }

        if (!isXmlPath)
        {
            if (IsCoverletCoverageCommand(command) ||
                RequiresExternalXmlWrittenByCurrentCoverageCommand(command))
            {
                reason = "External coverage XML report must be written by the current coverage command.";
                return false;
            }

            if (File.Exists(externalCoveragePath) &&
                ExternalCoverageXmlBackfill.IsLineBackfillableReport(externalCoveragePath, out reason))
            {
                reason = string.Empty;
                return true;
            }

            if (StringUtil.IsNullOrEmpty(reason))
            {
                reason = "External coverage path must point to an XML report.";
            }

            return false;
        }

        if (isXmlPath && command.WritesDotnetCoverageReportPath(externalCoveragePath))
        {
            reason = "dotnet-coverage collect writes the external coverage XML report after the instrumented test command exits.";
            return false;
        }

        if (TryGetCoverageReportPathWriterStatus(command, externalCoveragePath, out var hasReportPathWriter, out _) &&
            hasReportPathWriter)
        {
            return true;
        }

        if (IsCoverletCoverageCommand(command) ||
            RequiresExternalXmlWrittenByCurrentCoverageCommand(command))
        {
            reason = "External coverage XML report must be written by the current coverage command.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets whether the current coverage command declares the configured external report path as one of its outputs.
    /// </summary>
    /// <param name="externalCoveragePath">Configured external coverage report path.</param>
    /// <returns>True when a known writer in the current command writes the supplied path.</returns>
    internal static bool IsExternalCoveragePathWrittenByCurrentCommand(string externalCoveragePath)
    {
        var command = CoverageBackfillCommandLine.Parse(GetCommandLine(), GetCommandWorkingDirectory());
        return TryGetCoverageReportPathWriterStatus(command, externalCoveragePath, out var hasReportPathWriter, out _) &&
               hasReportPathWriter;
    }

    /// <summary>
    /// Gets whether the command line enables a Microsoft Testing Platform coverage mode.
    /// </summary>
    /// <param name="commandLine">Command line to inspect.</param>
    /// <param name="workingDirectory">Optional base directory for response files.</param>
    /// <returns>True when Microsoft Testing Platform coverage is detected.</returns>
    public static bool IsTestingPlatformCoverageCommand(string? commandLine, string? workingDirectory = null)
    {
        var command = CoverageBackfillCommandLine.Parse(commandLine, workingDirectory);
        return IsCoverletTestingPlatformCoverageCommand(command) ||
               IsMicrosoftTestingPlatformCoverageCommand(command);
    }

    /// <summary>
    /// Clears the cached command line so tests that mutate command-line environment variables remain isolated.
    /// </summary>
    [TestingOnly]
    internal static void ResetCommandLineCacheForTests()
    {
        lock (CommandLineLock)
        {
            _cachedCommandLine = null;
        }
    }

    /// <summary>
    /// Checks whether an external coverage path is safe to use before any ITR skip is applied.
    /// </summary>
    /// <param name="command">Parsed command used to identify known in-process coverage hooks.</param>
    /// <param name="reason">Reason why the external path is not safe for coverage-active skipping.</param>
    /// <returns>True when the external source is handled in-process, already verified as line-capable, or configured as a post-command XML report.</returns>
    private static bool IsExternalCoveragePathBackfillable(CoverageBackfillCommandLine command, out string reason)
    {
        reason = string.Empty;
        var externalCoveragePath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath);
        var isCoverletCoverageCommand = IsCoverletCoverageCommand(command);

        if (StringUtil.IsNullOrWhiteSpace(externalCoveragePath))
        {
            reason = "External coverage path is not configured.";
            return false;
        }

        if (HasUnsupportedExternalThreshold(command, externalCoveragePath))
        {
            reason = "An external coverage threshold was detected before Datadog can mutate the XML report.";
            return false;
        }

        if (!TryIsXmlPath(externalCoveragePath, out var isXmlPath))
        {
            reason = "External coverage path could not be parsed as a valid file path.";
            return false;
        }

        if (!isXmlPath)
        {
            if (isCoverletCoverageCommand ||
                RequiresExternalXmlWrittenByCurrentCoverageCommand(command))
            {
                reason = "External coverage XML report must be written by the current coverage command.";
                return false;
            }

            if (File.Exists(externalCoveragePath) &&
                ExternalCoverageXmlBackfill.IsLineBackfillableReport(externalCoveragePath, out reason))
            {
                reason = string.Empty;
                return true;
            }

            if (StringUtil.IsNullOrEmpty(reason))
            {
                reason = "External coverage path must point to an XML report.";
            }

            return false;
        }

        if (isXmlPath && command.WritesDotnetCoverageReportPath(externalCoveragePath))
        {
            reason = "dotnet-coverage collect writes the external coverage XML report after the instrumented test command exits.";
            return false;
        }

        if (isXmlPath &&
            TryGetCoverageReportPathWriterStatus(command, externalCoveragePath, out var hasReportPathWriter, out var allReportPathWritersAreLineCapable) &&
            hasReportPathWriter)
        {
            if (HasExplicitCoverageReportPathOtherThanIncludingDotnetCoverageChildCommand(command, externalCoveragePath))
            {
                reason = "External coverage XML report generation must write the configured external coverage path and must not write additional explicit coverage report paths.";
                return false;
            }

            if (HasCoverageReportPathWriterWithUnverifiableOutputPathIncludingDotnetCoverageChildCommand(command))
            {
                reason = "External coverage XML report generation must not include coverage writers with unverifiable output paths.";
                return false;
            }

            if (allReportPathWritersAreLineCapable)
            {
                reason = string.Empty;
                return true;
            }

            reason = "External coverage XML report must declare a supported line-capable XML format when the current command writes the report.";
            return false;
        }

        if (isXmlPath && command.ReferencesPath(externalCoveragePath))
        {
            reason = "External coverage XML report generation must use a supported coverage output option for the configured external coverage path.";
            return false;
        }

        if (isXmlPath && IsGeneratedLineCoverageXmlCommandIncludingDotnetCoverageChildCommand(command))
        {
            reason = "External coverage XML report generation must write the configured external coverage path.";
            return false;
        }

        if (isCoverletCoverageCommand)
        {
            if (isXmlPath && File.Exists(externalCoveragePath))
            {
                reason = "External coverage XML report must be written by the current command or be absent when using Coverlet XML fallback.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        if (isXmlPath && RequiresExternalXmlWrittenByCurrentCoverageCommand(command))
        {
            reason = "External coverage XML report must be written by the current coverage command.";
            return false;
        }

        if (File.Exists(externalCoveragePath))
        {
            if (ExternalCoverageXmlBackfill.IsLineBackfillableReport(externalCoveragePath, out reason))
            {
                reason = string.Empty;
                return true;
            }

            if (StringUtil.IsNullOrEmpty(reason))
            {
                reason = "External coverage report is not line-backfillable.";
            }

            return false;
        }

        if (isXmlPath)
        {
            if (IsGeneratedLineCoverageXmlCommand(command))
            {
                reason = "External coverage XML report generation must write the configured external coverage path.";
                return false;
            }

            reason = "External coverage XML report must exist before skipping unless the command declares a supported line-capable XML format.";
            return false;
        }

        reason = "External coverage path must point to an XML report.";
        return false;
    }

    private static bool HasExplicitCoverageReportPathOtherThanIncludingDotnetCoverageChildCommand(CoverageBackfillCommandLine command, string externalCoveragePath)
    {
        if (command.WritesCoverageReportPathOtherThan(externalCoveragePath))
        {
            return true;
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            if (HasExplicitCoverageReportPathOtherThanIncludingDotnetCoverageChildCommand(childCommand, externalCoveragePath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCoverageReportPathWriterWithUnverifiableOutputPathIncludingDotnetCoverageChildCommand(CoverageBackfillCommandLine command)
    {
        if (command.HasCoverageReportPathWriterWithUnverifiableOutputPath())
        {
            return true;
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            if (HasCoverageReportPathWriterWithUnverifiableOutputPathIncludingDotnetCoverageChildCommand(childCommand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCoverageReportPathWriterStatus(CoverageBackfillCommandLine command, string externalCoveragePath, out bool hasWriter, out bool allWritersAreLineCapable)
    {
        hasWriter = false;
        allWritersAreLineCapable = true;
        AddCoverageReportPathWriterStatus(command, externalCoveragePath, ref hasWriter, ref allWritersAreLineCapable);
        return true;
    }

    private static void AddCoverageReportPathWriterStatus(CoverageBackfillCommandLine command, string externalCoveragePath, ref bool hasWriter, ref bool allWritersAreLineCapable)
    {
        if (command.WritesCoverageReportPath(externalCoveragePath) &&
            !command.WritesDotnetCoverageReportPath(externalCoveragePath))
        {
            hasWriter = true;
            allWritersAreLineCapable &= IsGeneratedLineCoverageXmlCommandForReportPath(command, externalCoveragePath);
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            AddCoverageReportPathWriterStatus(childCommand, externalCoveragePath, ref hasWriter, ref allWritersAreLineCapable);
        }
    }

    private static bool ShouldWaitForCoverageIpc(CoverageBackfillCommandLine command)
    {
        if (IsCoverletCoverageCommand(command) || IsMicrosoftCodeCoverageCommand(command))
        {
            return true;
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            if (ShouldWaitForCoverageIpc(childCommand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldWaitForCoverletXmlFallback(CoverageBackfillCommandLine command, string? runSettingsBaseDirectory)
    {
        if (command.UsesCoverletCollectorCoverage(runSettingsBaseDirectory))
        {
            return true;
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            if (ShouldWaitForCoverletXmlFallback(childCommand, runSettingsBaseDirectory))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects local test subsetting that can make the backend aggregate broader than the current execution.
    /// </summary>
    /// <param name="command">Parsed command to inspect.</param>
    /// <param name="reason">Reason why the command scope is unsafe.</param>
    /// <returns>True when coverage-active skipping must be disabled for aggregate safety.</returns>
    private static bool HasUnsupportedSelection(CoverageBackfillCommandLine command, out string reason)
    {
        if (command.HasUnexpandedResponseFileReferenceIncludingDotnetCoverageChildCommand() ||
            command.HasUnexpandedTestingPlatformCommandLineArgumentResponseFileReferenceIncludingDotnetCoverageChildCommand())
        {
            reason = "A command response file could not be expanded; backend coverage may include candidates outside the executed subset.";
            return true;
        }

        if (command.HasOptionIncludingDotnetCoverageChildCommand(UnsupportedTestFilterOptions) ||
            command.HasTestingPlatformCommandLineArgumentOptionIncludingDotnetCoverageChildCommand(UnsupportedTestFilterOptions) ||
            command.HasAnyNonEmptyEffectiveMsBuildPropertyIncludingDotnetCoverageChildCommand(UnsupportedTestFilterProperties, requireActiveCoverletMsBuildProject: false))
        {
            reason = "A test filter was detected; backend coverage may include candidates outside the executed subset.";
            return true;
        }

        if (command.HasOptionIncludingDotnetCoverageChildCommand(UnsupportedFrameworkFilterOptions) ||
            command.ContainsShortFrameworkOptionIncludingDotnetCoverageChildCommand() ||
            command.HasAnyNonEmptyMsBuildPropertyIncludingDotnetCoverageChildCommand(UnsupportedFrameworkFilterProperties) ||
            command.HasRunSettingsTargetFrameworkIncludingDotnetCoverageChildCommand())
        {
            reason = "A target-framework subset was detected; backend coverage is not scoped to the selected framework.";
            return true;
        }

        // Explicit project, solution, and assembly targets are safe here because coverage-active skipping uses
        // a testhost-scoped skippable request keyed by test.bundle once backfill is required. Local filters and
        // framework selectors are still rejected because they can narrow execution within the same bundle.

        var vstestTestCaseFilter = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.VstestTestCaseFilter);
        if (!StringUtil.IsNullOrWhiteSpace(vstestTestCaseFilter))
        {
            reason = "A VSTest testcase filter was detected in the environment.";
            return true;
        }

        if (command.HasRunSettingsTestCaseFilterIncludingDotnetCoverageChildCommand())
        {
            reason = "A runsettings testcase filter was detected.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Gets whether the current process has a customer-visible coverage report source, not just ITR line collection.
    /// </summary>
    /// <param name="settings">Resolved Test Optimization settings for the current process.</param>
    /// <param name="command">Parsed command to inspect for coverage tool activation.</param>
    /// <returns>True when ITR skips could make a published coverage report inaccurate.</returns>
    private static bool HasSelectedCoverageReportSource(TestOptimizationSettings settings, CoverageBackfillCommandLine command)
    {
        return !StringUtil.IsNullOrWhiteSpace(settings.CodeCoveragePath) ||
               !StringUtil.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath)) ||
               IsDotnetCoverageCommand(command) ||
               IsCoverletCoverageCommand(command) ||
               IsCoverletTestingPlatformCoverageCommand(command) ||
               IsMicrosoftTestingPlatformCoverageCommand(command) ||
               IsMicrosoftCodeCoverageCommand(command);
    }

    /// <summary>
    /// Detects threshold modes that execute after the external tool computes coverage but before Datadog can rewrite an XML file.
    /// </summary>
    /// <param name="command">Parsed command to inspect.</param>
    /// <param name="externalCoveragePath">Configured external coverage report path.</param>
    /// <returns>True when an unsupported external threshold mode was detected.</returns>
    private static bool HasUnsupportedExternalThreshold(CoverageBackfillCommandLine command, string externalCoveragePath)
    {
        return command.HasUnsupportedExternalThresholdIncludingDotnetCoverageChildCommand(
            UnsupportedExternalThresholdOptions,
            ExternalThresholdProperties,
            "thresholdtype",
            LineThresholdTypes,
            externalCoveragePath);
    }

    /// <summary>
    /// Detects unsupported Coverlet MSBuild threshold modes even when Datadog is not using an external report path.
    /// </summary>
    /// <param name="command">Parsed command to inspect.</param>
    /// <returns>True when a Coverlet threshold includes non-line coverage dimensions.</returns>
    private static bool HasUnsupportedCoverletThreshold(CoverageBackfillCommandLine command)
    {
        return command.HasUnsupportedCoverletThresholdIncludingDotnetCoverageChildCommand(
            ExternalThresholdProperties,
            "thresholdtype",
            LineThresholdTypes);
    }

    /// <summary>
    /// Detects generated XML report modes whose command line explicitly selects a mutable line-capable format.
    /// </summary>
    /// <param name="command">Parsed command to inspect.</param>
    /// <returns>True when the generated XML format is known to expose line entries that can be rewritten after the command.</returns>
    private static bool IsGeneratedLineCoverageXmlCommand(CoverageBackfillCommandLine command)
    {
        return IsDotnetCoverageGeneratedLineCoverageXmlCommand(command) ||
               IsCoverletMsBuildGeneratedLineCoverageXmlCommand(command) ||
               IsMicrosoftTestingPlatformGeneratedLineCoverageXmlCommand(command) ||
               IsCoverletTestingPlatformGeneratedLineCoverageXmlCommand(command);
    }

    private static bool IsGeneratedLineCoverageXmlCommandIncludingDotnetCoverageChildCommand(CoverageBackfillCommandLine command)
    {
        if (IsGeneratedLineCoverageXmlCommand(command))
        {
            return true;
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            if (IsGeneratedLineCoverageXmlCommandIncludingDotnetCoverageChildCommand(childCommand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RequiresExternalXmlWrittenByCurrentCoverageCommand(CoverageBackfillCommandLine command)
    {
        if (IsDotnetCoverageCommand(command) ||
            IsMicrosoftCodeCoverageCommand(command) ||
            IsCoverletTestingPlatformCoverageCommand(command) ||
            IsMicrosoftTestingPlatformCoverageCommand(command))
        {
            return true;
        }

        foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
        {
            if (RequiresExternalXmlWrittenByCurrentCoverageCommand(childCommand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedLineCoverageXmlCommandForReportPath(CoverageBackfillCommandLine command, string externalCoveragePath)
    {
        if (command.WritesDotnetCoverageReportPath(externalCoveragePath))
        {
            return IsDotnetCoverageGeneratedLineCoverageXmlCommand(command);
        }

        if (command.WritesLineCapableCoverletMsBuildCoverageReportPath(externalCoveragePath))
        {
            return true;
        }

        if (command.WritesMicrosoftTestingPlatformCoverageReportPath(externalCoveragePath))
        {
            return IsMicrosoftTestingPlatformGeneratedLineCoverageXmlCommand(command);
        }

        if (command.WritesCoverletTestingPlatformCoverageReportPath(externalCoveragePath))
        {
            return IsCoverletTestingPlatformGeneratedLineCoverageXmlCommand(command);
        }

        return false;
    }

    private static bool IsDotnetCoverageGeneratedLineCoverageXmlCommand(CoverageBackfillCommandLine command)
    {
        return command.DotnetCoverageOutputFormatOptionValueContainsAny(DotnetCoverageGeneratedLineCoverageXmlFormats);
    }

    private static bool IsCoverletMsBuildGeneratedLineCoverageXmlCommand(CoverageBackfillCommandLine command)
    {
        return command.MsBuildPropertyValueContainsAny(GeneratedLineCoverageXmlFormatProperty, GeneratedLineCoverageXmlFormats);
    }

    private static bool IsMicrosoftTestingPlatformGeneratedLineCoverageXmlCommand(CoverageBackfillCommandLine command)
    {
        return command.MicrosoftTestingPlatformCoverageOutputFormatContainsAny(MicrosoftTestingPlatformGeneratedLineCoverageXmlFormats);
    }

    private static bool IsCoverletTestingPlatformGeneratedLineCoverageXmlCommand(CoverageBackfillCommandLine command)
    {
        return command.CoverletTestingPlatformCoverageOutputFormatContainsAny(GeneratedLineCoverageXmlFormats);
    }

    /// <summary>
    /// Checks whether a configured path has an XML extension without allowing malformed paths to escape the capability gate.
    /// </summary>
    /// <param name="path">Configured external coverage path.</param>
    /// <param name="isXmlPath">True when the path extension is <c>.xml</c>.</param>
    /// <returns>True when the path could be parsed.</returns>
    private static bool TryIsXmlPath(string path, out bool isXmlPath)
    {
        try
        {
            isXmlPath = Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception)
        {
            isXmlPath = false;
            return false;
        }
    }

    private static bool IsCoverletCoverageCommand(CoverageBackfillCommandLine command)
    {
        return command.UsesCoverletCoverage();
    }

    private static bool IsMicrosoftCodeCoverageCommand(CoverageBackfillCommandLine command)
    {
        return command.UsesMicrosoftCodeCoverage();
    }

    private static bool IsDotnetCoverageCommand(CoverageBackfillCommandLine command)
    {
        return command.UsesDotnetCoverage();
    }

    private static bool IsCoverletTestingPlatformCoverageCommand(CoverageBackfillCommandLine command)
    {
        return command.UsesCoverletTestingPlatformCoverage();
    }

    private static bool IsMicrosoftTestingPlatformCoverageCommand(CoverageBackfillCommandLine command)
    {
        return command.UsesMicrosoftTestingPlatformCoverage();
    }

    /// <summary>
    /// Gets the coverage backfill command line, falling back to the public test-session command and then the current process command line.
    /// </summary>
    /// <returns>Command line used for coverage capability decisions.</returns>
    private static string GetCommandLine()
    {
        var commandLine = _cachedCommandLine;
        if (commandLine is not null)
        {
            return commandLine;
        }

        lock (CommandLineLock)
        {
            _cachedCommandLine ??= EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand) ??
                                   EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand) ??
                                   Environment.CommandLine;
            return _cachedCommandLine;
        }
    }

    private static string? GetCommandWorkingDirectory()
    {
        return EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
    }
}
