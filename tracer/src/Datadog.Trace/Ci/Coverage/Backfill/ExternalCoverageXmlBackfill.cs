// <copyright file="ExternalCoverageXmlBackfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Reads and rewrites line-capable XML coverage reports with backend ITR skipped-test coverage.
/// </summary>
internal static class ExternalCoverageXmlBackfill
{
    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
    private static readonly StringComparer ResolvedPathComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly StringComparison ResolvedPathComparison = FrameworkDescription.Instance.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private enum ExpectedXmlReportFormat
    {
        Auto,
        Cobertura,
        OpenCover,
        Microsoft
    }

    private enum MicrosoftCoverageStatus
    {
        Unknown,
        NotCovered,
        PartiallyCovered,
        Covered
    }

    private enum MicrosoftCoverageValueKind
    {
        GenericAttribute,
        MicrosoftCoveredAttribute,
        CoverageXmlElement
    }

    /// <summary>
    /// Gets or sets a callback invoked after the replacement XML has been saved to a temporary file but before it replaces the original report.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static Action<string>? BeforeReplaceXmlDocumentForTests { get; set; }

    /// <summary>
    /// Gets or sets a callback that replaces the temporary XML file with the original report during tests.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static Action<string, string, string>? ReplaceXmlDocumentForTests { get; set; }

    /// <summary>
    /// Reads an XML coverage report and applies backend ITR line coverage when requested and supported by the format.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed with enough information to publish line coverage.</returns>
    public static bool TryProcess(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.Auto, validationState: null, out result);

    /// <summary>
    /// Reads an XML coverage report and records validation data for a larger report set.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="validationState">Report-set validation data to update.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed with enough information to publish line coverage.</returns>
    internal static bool TryProcess(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.Auto, validationState, out result);

    /// <summary>
    /// Reads a Cobertura XML coverage report and applies backend ITR line coverage when requested.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed as Cobertura with enough information to publish line coverage.</returns>
    public static bool TryProcessCobertura(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.Cobertura, validationState: null, out result);

    /// <summary>
    /// Reads a Cobertura XML coverage report and records validation data for a larger report set.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="validationState">Report-set validation data to update.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed as Cobertura with enough information to publish line coverage.</returns>
    internal static bool TryProcessCobertura(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.Cobertura, validationState, out result);

    /// <summary>
    /// Reads an OpenCover XML coverage report and applies backend ITR line coverage when requested.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed as OpenCover with enough information to publish line coverage.</returns>
    public static bool TryProcessOpenCover(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.OpenCover, validationState: null, out result);

    /// <summary>
    /// Reads an OpenCover XML coverage report and records validation data for a larger report set.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="validationState">Report-set validation data to update.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed as OpenCover with enough information to publish line coverage.</returns>
    internal static bool TryProcessOpenCover(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.OpenCover, validationState, out result);

    /// <summary>
    /// Reads a Microsoft line XML coverage report and applies backend ITR line coverage when requested.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed as Microsoft line XML with enough information to publish line coverage.</returns>
    public static bool TryProcessMicrosoft(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.Microsoft, validationState: null, out result);

    /// <summary>
    /// Reads a Microsoft line XML coverage report and records validation data for a larger report set.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="validationState">Report-set validation data to update.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed as Microsoft line XML with enough information to publish line coverage.</returns>
    internal static bool TryProcessMicrosoft(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
        => TryProcess(filePath, backfillData, applyBackfill, ExpectedXmlReportFormat.Microsoft, validationState, out result);

    /// <summary>
    /// Reads line-level coverage from a supported XML report without mutating the report.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="executableLines">Unique executable source line count.</param>
    /// <param name="coveredLines">Unique covered source line count.</param>
    /// <returns>True when line-level coverage could be read safely.</returns>
    internal static bool TryReadLineCoverage(string filePath, out double executableLines, out double coveredLines)
        => TryReadMergedLineCoverage([filePath], validationState: null, out executableLines, out coveredLines);

    /// <summary>
    /// Reads and merges line-level coverage from supported XML reports without mutating the reports.
    /// </summary>
    /// <param name="filePaths">Coverage XML file paths.</param>
    /// <param name="executableLines">Unique executable source line count.</param>
    /// <param name="coveredLines">Unique covered source line count.</param>
    /// <returns>True when line-level coverage could be read safely from every report.</returns>
    internal static bool TryReadMergedLineCoverage(IReadOnlyList<string> filePaths, out double executableLines, out double coveredLines)
        => TryReadMergedLineCoverage(filePaths, validationState: null, out executableLines, out coveredLines);

    /// <summary>
    /// Reads and merges line-level coverage from supported XML reports without mutating the reports.
    /// </summary>
    /// <param name="filePaths">Coverage XML file paths.</param>
    /// <param name="validationState">Optional validated backfill state used to canonicalize local XML source paths to backend coverage keys.</param>
    /// <param name="executableLines">Unique executable source line count.</param>
    /// <param name="coveredLines">Unique covered source line count.</param>
    /// <returns>True when line-level coverage could be read safely from every report.</returns>
    internal static bool TryReadMergedLineCoverage(IReadOnlyList<string> filePaths, CoverageBackfillValidationState? validationState, out double executableLines, out double coveredLines)
    {
        executableLines = 0;
        coveredLines = 0;
        if (filePaths.Count == 0)
        {
            return false;
        }

        var executableLineKeys = new HashSet<CoverageLineKey>();
        var coveredLineKeys = new HashSet<CoverageLineKey>();
        foreach (var filePath in filePaths)
        {
            if (StringUtil.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            if (!TryLoadXmlDocument(filePath, out var xmlDoc))
            {
                return false;
            }

            var reportExecutableLineKeys = new HashSet<CoverageLineKey>();
            var reportCoveredLineKeys = new HashSet<CoverageLineKey>();
            var read = DocumentElementHasLocalName(xmlDoc, "coverage") ?
                           TryAddCoberturaCoverageLineKeys(xmlDoc, filePath, reportExecutableLineKeys, reportCoveredLineKeys, validationState) :
                           xmlDoc.SelectSingleNode("/CoverageSession") is not null ?
                               TryAddOpenCoverCoverageLineKeys(xmlDoc, filePath, reportExecutableLineKeys, reportCoveredLineKeys, validationState) :
                               TryAddMicrosoftCoverageLineKeys(xmlDoc, filePath, reportExecutableLineKeys, reportCoveredLineKeys, validationState);
            if (!read || reportExecutableLineKeys.Count == 0)
            {
                return false;
            }

            executableLineKeys.UnionWith(reportExecutableLineKeys);
            coveredLineKeys.UnionWith(reportCoveredLineKeys);
        }

        if (executableLineKeys.Count > 0)
        {
            executableLines = executableLineKeys.Count;
            coveredLines = coveredLineKeys.Count;
            return true;
        }

        return false;
    }

    private static bool TryProcess(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, ExpectedXmlReportFormat expectedFormat, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
    {
        result = default;
        if (StringUtil.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        XmlDocument xmlDoc;
        if (!TryLoadXmlDocument(filePath, out xmlDoc))
        {
            return false;
        }

        var canBackfill = applyBackfill && backfillData is { IsPresent: true, IsValid: true };
        if (canBackfill)
        {
            validationState?.MarkBackfillAttempted(backfillData!);
        }

        if (DocumentElementHasLocalName(xmlDoc, "coverage"))
        {
            if (expectedFormat is not ExpectedXmlReportFormat.Auto and not ExpectedXmlReportFormat.Cobertura)
            {
                return false;
            }

            if (TryProcessCobertura(xmlDoc, filePath, backfillData, canBackfill, validationState, out result))
            {
                return true;
            }

            return !applyBackfill && TryReadCoberturaAggregateXml(xmlDoc, out result);
        }

        if (xmlDoc.SelectSingleNode("/CoverageSession") is not null)
        {
            if (expectedFormat is not ExpectedXmlReportFormat.Auto and not ExpectedXmlReportFormat.OpenCover)
            {
                return false;
            }

            if (TryProcessOpenCover(xmlDoc, filePath, backfillData, canBackfill, validationState, out result))
            {
                return true;
            }

            return !applyBackfill && TryReadOpenCoverAggregateXml(xmlDoc, out result);
        }

        if (expectedFormat is ExpectedXmlReportFormat.Auto or ExpectedXmlReportFormat.Microsoft &&
            TryProcessMicrosoftLineXml(xmlDoc, filePath, backfillData, canBackfill, validationState, out result))
        {
            return true;
        }

        if (applyBackfill)
        {
            return false;
        }

        return expectedFormat is ExpectedXmlReportFormat.Auto or ExpectedXmlReportFormat.Microsoft &&
               TryReadMicrosoftAggregateXml(xmlDoc, out result);
    }

    /// <summary>
    /// Checks whether an existing XML report has line entries that can be rewritten for ITR coverage backfill.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="reason">Reason why the report cannot be used for line backfill.</param>
    /// <returns>True when the report format exposes mutable line-level coverage data.</returns>
    public static bool IsLineBackfillableReport(string filePath, out string reason)
    {
        reason = string.Empty;
        if (StringUtil.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            reason = "Coverage XML report does not exist yet.";
            return false;
        }

        try
        {
            if (!TryLoadXmlDocument(filePath, out var xmlDoc))
            {
                reason = "Coverage XML report could not be inspected.";
                return false;
            }

            if (DocumentElementHasLocalName(xmlDoc, "coverage"))
            {
                if (HasBackfillableCoberturaLine(xmlDoc, filePath, out var hasUnsafeSourcePath))
                {
                    return true;
                }

                if (hasUnsafeSourcePath)
                {
                    reason = "Coverage XML report contains source paths that cannot be safely matched.";
                    return false;
                }
            }
            else if (xmlDoc.SelectSingleNode("/CoverageSession") is not null)
            {
                if (HasBackfillableOpenCoverSequencePoint(xmlDoc, out var hasUnsafeSourcePath))
                {
                    return true;
                }

                if (hasUnsafeSourcePath)
                {
                    reason = "Coverage XML report contains source paths that cannot be safely matched.";
                    return false;
                }
            }
            else
            {
                if (HasBackfillableMicrosoftLine(xmlDoc, out var hasUnsafeSourcePath))
                {
                    return true;
                }

                if (hasUnsafeSourcePath)
                {
                    reason = "Coverage XML report contains source paths that cannot be safely matched.";
                    return false;
                }
            }

            reason = "Coverage XML report is aggregate-only or unsupported.";
            return false;
        }
        catch (Exception ex)
        {
            reason = $"Coverage XML report could not be inspected: {ex.Message}";
            return false;
        }
    }

    private static bool TryProcessCobertura(XmlDocument xmlDoc, string filePath, CoverageBackfillData? backfillData, bool canBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
    {
        result = default;
        var classNodes = xmlDoc.SelectNodes($"//*[{LocalNameEquals("class")}][*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}]]");
        if (classNodes is null || classNodes.Count == 0)
        {
            return false;
        }

        var rewritten = false;
        var unsafePathMatch = false;
        var duplicateRepresentedBackendLine = false;
        var pathMatchTracker = new CoverageBackfillPathMatchTracker();
        var matchedBackendPaths = new HashSet<string>(StringComparer.Ordinal);
        var representedBackendLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var sourceRoots = canBackfill ? GetCoberturaSourceRoots(xmlDoc) : [];
        foreach (XmlNode? classNode in classNodes)
        {
            if (classNode is null)
            {
                continue;
            }

            var filename = classNode.Attributes?["filename"]?.Value;
            if (canBackfill)
            {
                if (TryGetCoberturaBackendCoverage(backfillData, filename, filePath, sourceRoots, pathMatchTracker, validationState, out var backendKey, out var backendBitmap, out var rejectedUnsafeMatch))
                {
                    if (HasActiveBits(backendBitmap))
                    {
                        matchedBackendPaths.Add(backendKey);
                        validationState?.RecordMatchedBackendPath(backendKey, backendBitmap);
                    }

                    var representedLines = GetOrCreateLineSet(representedBackendLines, backendKey);
                    rewritten |= BackfillCoberturaClass(classNode, backendBitmap, representedLines, out var newlyRepresentedLines, out var duplicateClassBackendLine);
                    duplicateRepresentedBackendLine |= duplicateClassBackendLine ||
                                                       !RecordRepresentedBackendLines(validationState, backendKey, newlyRepresentedLines);
                }
                else
                {
                    unsafePathMatch |= rejectedUnsafeMatch;
                    if (rejectedUnsafeMatch)
                    {
                        validationState?.RecordUnsafePathMatch();
                    }
                }
            }

            UpdateCoberturaLineRate(classNode);
        }

        var packageNodes = xmlDoc.SelectNodes($"//*[{LocalNameEquals("package")}]");
        if (packageNodes is not null)
        {
            foreach (XmlNode? packageNode in packageNodes)
            {
                if (packageNode is not null)
                {
                    UpdateCoberturaLineRate(packageNode);
                }
            }
        }

        var methodNodes = xmlDoc.SelectNodes($"//*[{LocalNameEquals("method")}][*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}]]");
        if (methodNodes is not null)
        {
            foreach (XmlNode? methodNode in methodNodes)
            {
                if (methodNode is not null)
                {
                    UpdateCoberturaLineRate(methodNode);
                }
            }
        }

        var coverageNode = xmlDoc.SelectSingleNode($"/*[{LocalNameEquals("coverage")}]");
        if (coverageNode is null)
        {
            return false;
        }

        var counts = UpdateCoberturaLineRate(coverageNode);
        if (counts.Total <= 0)
        {
            return false;
        }

        if (canBackfill &&
            (unsafePathMatch ||
             duplicateRepresentedBackendLine ||
             (validationState is null && !CanPublishBackfilledCoverage(backfillData, matchedBackendPaths, representedBackendLines))))
        {
            return false;
        }

        if (rewritten)
        {
            if (!TrySaveXmlDocument(xmlDoc, filePath))
            {
                return false;
            }
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(counts.Covered, counts.Total),
            counts.Total,
            counts.Covered,
            HasValidatedBackfillCoverage(canBackfill, backfillData, matchedBackendPaths, representedBackendLines),
            rewritten,
            diagnostic: "cobertura");
        return true;
    }

    private static bool TryProcessOpenCover(XmlDocument xmlDoc, string filePath, CoverageBackfillData? backfillData, bool canBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
    {
        result = default;
        var moduleNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module");
        if (moduleNodes is null || moduleNodes.Count == 0)
        {
            return false;
        }

        var rewritten = false;
        var unsafePathMatch = false;
        var duplicateBackendCoveredSequencePoint = false;
        var partialBackfilledSequencePointRange = false;
        var hasSequencePoints = false;
        var pathMatchTracker = new CoverageBackfillPathMatchTracker();
        var matchedBackendPaths = new HashSet<string>(StringComparer.Ordinal);
        var backendCoveredSequencePointLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var rangedBackendCoveredSequencePointLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var representedBackendLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (XmlNode? moduleNode in moduleNodes)
        {
            if (moduleNode is null)
            {
                continue;
            }

            if (!TryGetOpenCoverFileMap(moduleNode, out var fileMap))
            {
                return false;
            }

            var sequencePointNodes = moduleNode.SelectNodes(".//SequencePoint[@sl and @vc]");
            if (sequencePointNodes is null)
            {
                continue;
            }

            foreach (XmlNode? sequencePointNode in sequencePointNodes)
            {
                if (sequencePointNode is null)
                {
                    continue;
                }

                hasSequencePoints = true;
                if (!canBackfill ||
                    !TryGetIntAttribute(sequencePointNode, "fileid", out var fileId) ||
                    !fileMap.TryGetValue(fileId, out var sourcePath))
                {
                    continue;
                }

                if (!TryGetBackendCoverage(backfillData, sourcePath, filePath, pathMatchTracker, validationState, out var backendKey, out var backendBitmap, out var rejectedUnsafeMatch))
                {
                    unsafePathMatch |= rejectedUnsafeMatch;
                    if (rejectedUnsafeMatch)
                    {
                        validationState?.RecordUnsafePathMatch();
                    }

                    continue;
                }

                if (HasActiveBits(backendBitmap))
                {
                    matchedBackendPaths.Add(backendKey);
                    validationState?.RecordMatchedBackendPath(backendKey, backendBitmap);
                }

                if (!TryGetIntAttribute(sequencePointNode, "vc", out var visits))
                {
                    continue;
                }

                if (!TryGetOpenCoverSequencePointRange(sequencePointNode, out var startLine, out var endLine))
                {
                    continue;
                }

                var maxBackendLine = backendBitmap.Length * 8;
                if (startLine > maxBackendLine)
                {
                    continue;
                }

                var representedAnyBackendLine = false;
                var rangeFullyCoveredByBackend = endLine <= maxBackendLine;
                var isSingleLineSequencePoint = startLine == endLine;
                var cappedEndLine = Math.Min(endLine, maxBackendLine);
                for (var line = startLine; line <= cappedEndLine; line++)
                {
                    if (!IsBackendLineCovered(backendBitmap, line))
                    {
                        rangeFullyCoveredByBackend = false;
                        continue;
                    }

                    var representedByPreviousSequencePoint = !GetOrCreateLineSet(backendCoveredSequencePointLines, backendKey).Add(line);
                    var representedByPreviousRange = rangedBackendCoveredSequencePointLines.TryGetValue(backendKey, out var rangedLines) && rangedLines.Contains(line);
                    if (!isSingleLineSequencePoint)
                    {
                        GetOrCreateLineSet(rangedBackendCoveredSequencePointLines, backendKey).Add(line);
                    }

                    if (representedByPreviousSequencePoint && (!isSingleLineSequencePoint || representedByPreviousRange))
                    {
                        duplicateBackendCoveredSequencePoint = true;
                    }

                    GetOrCreateLineSet(representedBackendLines, backendKey).Add(line);
                    if (validationState is not null)
                    {
                        validationState.RecordRepresentedBackendLine(backendKey, line);
                    }

                    representedAnyBackendLine = true;
                }

                if (!representedAnyBackendLine)
                {
                    continue;
                }

                if (visits > 0)
                {
                    continue;
                }

                if (!rangeFullyCoveredByBackend)
                {
                    partialBackfilledSequencePointRange = true;
                    continue;
                }

                SetAttribute(sequencePointNode, "vc", "1");
                rewritten = true;
            }
        }

        if (!hasSequencePoints)
        {
            return false;
        }

        if (rewritten)
        {
            if (HasInvalidOpenCoverSequencePointRange(xmlDoc))
            {
                return false;
            }

            UpdateOpenCoverMethodVisitMetadata(xmlDoc);
            UpdateOpenCoverSummaries(xmlDoc);
        }

        var rootSummary = xmlDoc.SelectSingleNode("/CoverageSession/Summary");
        if (rootSummary is null ||
            !TryGetDoubleAttribute(rootSummary, "numSequencePoints", out var total) ||
            !TryGetDoubleAttribute(rootSummary, "visitedSequencePoints", out var covered) ||
            total <= 0)
        {
            return false;
        }

        if (TryGetOpenCoverLineCoverage(xmlDoc, filePath, out var executableLines, out var coveredLines))
        {
            total = executableLines;
            covered = coveredLines;
        }

        if (canBackfill &&
            (unsafePathMatch ||
             duplicateBackendCoveredSequencePoint ||
             partialBackfilledSequencePointRange ||
             (validationState is null && !CanPublishBackfilledCoverage(backfillData, matchedBackendPaths, representedBackendLines))))
        {
            return false;
        }

        if (rewritten)
        {
            if (!TrySaveXmlDocument(xmlDoc, filePath))
            {
                return false;
            }
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(covered, total),
            total,
            covered,
            HasValidatedBackfillCoverage(canBackfill, backfillData, matchedBackendPaths, representedBackendLines),
            rewritten,
            diagnostic: "opencover");
        return true;
    }

    private static bool TryProcessMicrosoftLineXml(XmlDocument xmlDoc, string filePath, CoverageBackfillData? backfillData, bool canBackfill, CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
    {
        result = default;
        if (!TryGetMicrosoftLineEntries(xmlDoc, out var lineEntries))
        {
            return false;
        }

        if (lineEntries.Count == 0)
        {
            return false;
        }

        var rewritten = false;
        var unsafePathMatch = false;
        var duplicateRepresentedBackendLine = false;
        var unsupportedPartialBackfill = false;
        var pathMatchTracker = new CoverageBackfillPathMatchTracker();
        var matchedBackendPaths = new HashSet<string>(StringComparer.Ordinal);
        var representedBackendLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var lineEntry in lineEntries)
        {
            if (!lineEntry.HasValidLineRange)
            {
                continue;
            }

            var hitsCanBeRewritten = lineEntry.TryGetHits(out var hits);
            var isPartiallyCovered = lineEntry.TryGetStatus(out var status) &&
                                     status == MicrosoftCoverageStatus.PartiallyCovered;
            byte[]? backendBitmap = null;
            string? backendKey = null;
            var hasCoveredBackendLine = false;
            var hasUncoveredBackendLineInRange = false;
            if (canBackfill)
            {
                if (TryGetBackendCoverage(backfillData, lineEntry.SourcePath, filePath, pathMatchTracker, validationState, out backendKey, out backendBitmap, out var rejectedUnsafeMatch))
                {
                    if (HasActiveBits(backendBitmap))
                    {
                        matchedBackendPaths.Add(backendKey);
                        validationState?.RecordMatchedBackendPath(backendKey, backendBitmap);
                    }

                    for (var line = lineEntry.StartLine; line <= lineEntry.EndLine; line++)
                    {
                        if (IsBackendLineCovered(backendBitmap, line))
                        {
                            hasCoveredBackendLine = true;
                            continue;
                        }

                        hasUncoveredBackendLineInRange = true;
                    }
                }
                else
                {
                    unsafePathMatch |= rejectedUnsafeMatch;
                    if (rejectedUnsafeMatch)
                    {
                        validationState?.RecordUnsafePathMatch();
                    }
                }
            }

            if (canBackfill &&
                isPartiallyCovered &&
                backendBitmap is not null &&
                hasCoveredBackendLine)
            {
                unsupportedPartialBackfill = true;
                validationState?.RecordUnsupportedBackfill();
            }

            if (canBackfill &&
                !isPartiallyCovered &&
                hitsCanBeRewritten &&
                hits <= 0 &&
                backendBitmap is not null &&
                hasCoveredBackendLine &&
                hasUncoveredBackendLineInRange)
            {
                unsupportedPartialBackfill = true;
                validationState?.RecordUnsupportedBackfill();
            }

            if (canBackfill &&
                !isPartiallyCovered &&
                hitsCanBeRewritten &&
                hits <= 0 &&
                backendBitmap is not null &&
                hasCoveredBackendLine &&
                !hasUncoveredBackendLineInRange)
            {
                lineEntry.SetCovered();
                hits = 1;
                rewritten = true;
            }

            if (canBackfill &&
                hitsCanBeRewritten &&
                hits > 0 &&
                backendBitmap is not null &&
                backendKey is not null)
            {
                for (var line = lineEntry.StartLine; line <= lineEntry.EndLine; line++)
                {
                    if (!IsBackendLineCovered(backendBitmap, line))
                    {
                        continue;
                    }

                    if (GetOrCreateLineSet(representedBackendLines, backendKey).Add(line) &&
                        validationState is not null &&
                        !validationState.RecordRepresentedBackendLine(backendKey, line))
                    {
                        duplicateRepresentedBackendLine = true;
                    }
                }
            }
        }

        var counts = CountMicrosoftLines(lineEntries);
        if (counts.Total <= 0)
        {
            return false;
        }

        if (canBackfill &&
            (unsafePathMatch ||
             duplicateRepresentedBackendLine ||
             unsupportedPartialBackfill ||
             (validationState is null && !CanPublishBackfilledCoverage(backfillData, matchedBackendPaths, representedBackendLines))))
        {
            return false;
        }

        if (rewritten)
        {
            if (!UpdateMicrosoftAggregateSummaries(xmlDoc))
            {
                return false;
            }

            if (!TrySaveXmlDocument(xmlDoc, filePath))
            {
                return false;
            }
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(counts.Covered, counts.Total),
            counts.Total,
            counts.Covered,
            HasValidatedBackfillCoverage(canBackfill, backfillData, matchedBackendPaths, representedBackendLines),
            rewritten,
            diagnostic: "microsoft-line");
        return true;
    }

    private static bool TryGetOpenCoverLineCoverage(XmlDocument xmlDoc, string filePath, out double executableLines, out double coveredLines)
    {
        executableLines = 0;
        coveredLines = 0;
        var executableLineKeys = new HashSet<CoverageLineKey>();
        var coveredLineKeys = new HashSet<CoverageLineKey>();
        if (!TryAddOpenCoverCoverageLineKeys(xmlDoc, filePath, executableLineKeys, coveredLineKeys, validationState: null) ||
            executableLineKeys.Count == 0)
        {
            return false;
        }

        executableLines = executableLineKeys.Count;
        coveredLines = coveredLineKeys.Count;
        return true;
    }

    private static bool TryReadMicrosoftAggregateXml(XmlDocument xmlDoc, out ExternalCoverageXmlResult result)
    {
        result = default;
        var linesCovered = xmlDoc.SelectNodes("/results/modules/module/@lines_covered");
        var linesPartiallyCovered = xmlDoc.SelectNodes("/results/modules/module/@lines_partially_covered");
        var linesNotCovered = xmlDoc.SelectNodes("/results/modules/module/@lines_not_covered");

        if (linesCovered is null ||
            linesPartiallyCovered is null ||
            linesNotCovered is null ||
            linesCovered.Count != linesPartiallyCovered.Count ||
            linesCovered.Count != linesNotCovered.Count)
        {
            return false;
        }

        var totalLinesCovered = SumAttributes(linesCovered);
        var totalLinesPartiallyCovered = SumAttributes(linesPartiallyCovered);
        var totalLinesNotCovered = SumAttributes(linesNotCovered);
        if (totalLinesCovered is null || totalLinesPartiallyCovered is null || totalLinesNotCovered is null)
        {
            return false;
        }

        var totalLines = totalLinesCovered.Value + totalLinesPartiallyCovered.Value + totalLinesNotCovered.Value;
        if (totalLines <= 0)
        {
            return false;
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(totalLinesCovered.Value, totalLines),
            totalLines,
            totalLinesCovered.Value,
            backfilled: false,
            rewritten: false,
            diagnostic: "microsoft-aggregate");
        return true;
    }

    private static bool UpdateMicrosoftAggregateSummaries(XmlDocument xmlDoc)
    {
        var microsoftSummaryNodes = xmlDoc.SelectNodes("//*[@lines_covered or @lines_partially_covered or @lines_not_covered]");
        if (microsoftSummaryNodes is not null)
        {
            foreach (XmlNode? summaryNode in microsoftSummaryNodes)
            {
                if (summaryNode is null)
                {
                    continue;
                }

                if (!TryGetMicrosoftLineEntries(summaryNode, out var lineEntries))
                {
                    return false;
                }

                var counts = CountMicrosoftLines(lineEntries);
                if (counts.Total <= 0)
                {
                    continue;
                }

                SetAttribute(summaryNode, "lines_covered", FormatNumber(counts.Covered));
                SetAttribute(summaryNode, "lines_partially_covered", FormatNumber(counts.PartiallyCovered));
                SetAttribute(summaryNode, "lines_not_covered", FormatNumber(counts.Total - counts.Covered - counts.PartiallyCovered));
            }
        }

        var coverageXmlAggregateNodes = xmlDoc.SelectNodes(
            $"//*[{LocalNameEquals("module")} or {LocalNameEquals("namespacetable")} or {LocalNameEquals("class")} or {LocalNameEquals("method")}]");
        if (coverageXmlAggregateNodes is not null)
        {
            foreach (XmlNode? aggregateNode in coverageXmlAggregateNodes)
            {
                if (aggregateNode is null ||
                    (GetChildElement(aggregateNode, "LinesCovered") is null &&
                     GetChildElement(aggregateNode, "LinesPartiallyCovered") is null &&
                     GetChildElement(aggregateNode, "LinesNotCovered") is null))
                {
                    continue;
                }

                if (!TryGetMicrosoftLineEntries(aggregateNode, out var lineEntries))
                {
                    return false;
                }

                var counts = CountMicrosoftLines(lineEntries);
                if (counts.Total <= 0)
                {
                    continue;
                }

                SetChildElementText(aggregateNode, "LinesCovered", FormatNumber(counts.Covered));
                SetChildElementText(aggregateNode, "LinesPartiallyCovered", FormatNumber(counts.PartiallyCovered));
                SetChildElementText(aggregateNode, "LinesNotCovered", FormatNumber(counts.Total - counts.Covered - counts.PartiallyCovered));
            }
        }

        return true;
    }

    private static LineCounts CountMicrosoftLines(IEnumerable<MicrosoftLineEntry> lineEntries)
    {
        var statusByLine = new Dictionary<MicrosoftSourceLineKey, MicrosoftCoverageStatus>();
        foreach (var lineEntry in lineEntries)
        {
            if (!lineEntry.HasValidLineRange ||
                !lineEntry.TryGetStatus(out var status))
            {
                continue;
            }

            for (var line = lineEntry.StartLine; line <= lineEntry.EndLine; line++)
            {
                var key = new MicrosoftSourceLineKey(lineEntry.SourcePath, line);
                statusByLine[key] = statusByLine.TryGetValue(key, out var existingStatus) ?
                                        MergeMicrosoftCoverageStatus(existingStatus, status) :
                                        status;
            }
        }

        double total = 0;
        double covered = 0;
        double partiallyCovered = 0;
        foreach (var status in statusByLine.Values)
        {
            total++;
            if (status == MicrosoftCoverageStatus.Covered)
            {
                covered++;
            }
            else if (status == MicrosoftCoverageStatus.PartiallyCovered)
            {
                partiallyCovered++;
            }
        }

        return new LineCounts(total, covered, partiallyCovered);
    }

    private static MicrosoftCoverageStatus MergeMicrosoftCoverageStatus(MicrosoftCoverageStatus existingStatus, MicrosoftCoverageStatus newStatus)
    {
        if (existingStatus == newStatus)
        {
            return existingStatus;
        }

        if (existingStatus == MicrosoftCoverageStatus.Unknown)
        {
            return newStatus;
        }

        if (newStatus == MicrosoftCoverageStatus.Unknown)
        {
            return existingStatus;
        }

        if (existingStatus == MicrosoftCoverageStatus.PartiallyCovered ||
            newStatus == MicrosoftCoverageStatus.PartiallyCovered)
        {
            return MicrosoftCoverageStatus.PartiallyCovered;
        }

        return MicrosoftCoverageStatus.PartiallyCovered;
    }

    private static bool HasBackfillableCoberturaLine(XmlDocument xmlDoc, string reportPath, out bool hasUnsafeSourcePath)
    {
        hasUnsafeSourcePath = false;
        var hasBackfillableLine = false;
        var sourceRoots = GetCoberturaSourceRoots(xmlDoc);
        var classNodes = xmlDoc.SelectNodes($"/*[{LocalNameEquals("coverage")}]//*[{LocalNameEquals("class")}][@filename][*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}]]");
        if (classNodes is null)
        {
            return false;
        }

        foreach (XmlNode? classNode in classNodes)
        {
            if (classNode is null ||
                StringUtil.IsNullOrWhiteSpace(classNode.Attributes?["filename"]?.Value))
            {
                continue;
            }

            var filename = classNode.Attributes?["filename"]?.Value;
            var lineNodes = classNode.SelectNodes($"*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}][@number and @hits]");
            if (lineNodes is null)
            {
                continue;
            }

            foreach (XmlNode? lineNode in lineNodes)
            {
                if (lineNode is not null &&
                    TryGetIntAttribute(lineNode, "number", out var line) &&
                    line > 0 &&
                    TryGetDoubleAttribute(lineNode, "hits", out _))
                {
                    if (IsUnsafeBackfillSourcePath(filename!))
                    {
                        hasUnsafeSourcePath = true;
                        continue;
                    }

                    if (sourceRoots.Length > 0 &&
                        !IsPathRootedCrossPlatform(filename!) &&
                        !TryGetCoberturaSourceRootRawPathCandidates(filename!, reportPath, sourceRoots, out _))
                    {
                        hasUnsafeSourcePath = true;
                        continue;
                    }

                    hasBackfillableLine = true;
                }
            }
        }

        return hasBackfillableLine && !hasUnsafeSourcePath;
    }

    private static bool HasBackfillableOpenCoverSequencePoint(XmlDocument xmlDoc, out bool hasUnsafeSourcePath)
    {
        hasUnsafeSourcePath = false;
        var hasBackfillableSequencePoint = false;
        var moduleNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module");
        if (moduleNodes is null)
        {
            return false;
        }

        foreach (XmlNode? moduleNode in moduleNodes)
        {
            if (moduleNode is null || !TryGetOpenCoverFileMap(moduleNode, out var fileMap))
            {
                return false;
            }

            var sequencePointNodes = moduleNode.SelectNodes(".//SequencePoint[@sl and @vc]");
            if (sequencePointNodes is null)
            {
                continue;
            }

            foreach (XmlNode? sequencePointNode in sequencePointNodes)
            {
                if (sequencePointNode is not null &&
                    TryGetIntAttribute(sequencePointNode, "fileid", out var fileId) &&
                    fileMap.TryGetValue(fileId, out var sourcePath) &&
                    !StringUtil.IsNullOrWhiteSpace(sourcePath) &&
                    TryGetOpenCoverSequencePointRange(sequencePointNode, out _, out _) &&
                    TryGetDoubleAttribute(sequencePointNode, "vc", out _))
                {
                    if (IsUnsafeBackfillSourcePath(sourcePath))
                    {
                        hasUnsafeSourcePath = true;
                        continue;
                    }

                    hasBackfillableSequencePoint = true;
                }
            }
        }

        return hasBackfillableSequencePoint && !hasUnsafeSourcePath;
    }

    private static bool HasBackfillableMicrosoftLine(XmlDocument xmlDoc, out bool hasUnsafeSourcePath)
    {
        hasUnsafeSourcePath = false;
        var hasBackfillableLine = false;
        if (!TryGetMicrosoftLineEntries(xmlDoc, out var lineEntries))
        {
            hasUnsafeSourcePath = true;
            return false;
        }

        foreach (var lineEntry in lineEntries)
        {
            if (!StringUtil.IsNullOrWhiteSpace(lineEntry.SourcePath) &&
                lineEntry.HasValidLineRange &&
                lineEntry.TryGetHits(out _))
            {
                if (IsUnsafeBackfillSourcePath(lineEntry.SourcePath))
                {
                    hasUnsafeSourcePath = true;
                    continue;
                }

                hasBackfillableLine = true;
            }
        }

        return hasBackfillableLine && !hasUnsafeSourcePath;
    }

    private static bool TryReadCoberturaAggregateXml(XmlDocument xmlDoc, out ExternalCoverageXmlResult result)
    {
        result = default;
        var coverageNode = xmlDoc.SelectSingleNode($"/*[{LocalNameEquals("coverage")}]");
        if (coverageNode is null)
        {
            return false;
        }

        if (TryGetDoubleAttribute(coverageNode, "lines-valid", out var total) &&
            TryGetDoubleAttribute(coverageNode, "lines-covered", out var covered) &&
            total > 0)
        {
            result = new ExternalCoverageXmlResult(
                CalculatePercentage(covered, total),
                total,
                covered,
                backfilled: false,
                rewritten: false,
                diagnostic: "cobertura-aggregate");
            return true;
        }

        if (TryGetDoubleAttribute(coverageNode, "line-rate", out var lineRate))
        {
            result = new ExternalCoverageXmlResult(
                Math.Round(lineRate * 100, 2).ToValidPercentage(),
                executableLines: null,
                coveredLines: null,
                backfilled: false,
                rewritten: false,
                diagnostic: "cobertura-aggregate");
            return true;
        }

        return false;
    }

    private static bool TryReadOpenCoverAggregateXml(XmlDocument xmlDoc, out ExternalCoverageXmlResult result)
    {
        result = default;
        var rootSummary = xmlDoc.SelectSingleNode("/CoverageSession/Summary");
        if (rootSummary is null)
        {
            return false;
        }

        if (TryGetDoubleAttribute(rootSummary, "numSequencePoints", out var total) &&
            TryGetDoubleAttribute(rootSummary, "visitedSequencePoints", out var covered) &&
            total > 0)
        {
            result = new ExternalCoverageXmlResult(
                CalculatePercentage(covered, total),
                total,
                covered,
                backfilled: false,
                rewritten: false,
                diagnostic: "opencover-aggregate");
            return true;
        }

        if (TryGetDoubleAttribute(rootSummary, "sequenceCoverage", out var sequenceCoverage))
        {
            result = new ExternalCoverageXmlResult(
                sequenceCoverage.ToValidPercentage(),
                executableLines: null,
                coveredLines: null,
                backfilled: false,
                rewritten: false,
                diagnostic: "opencover-aggregate");
            return true;
        }

        return false;
    }

    private static bool TryAddCoberturaCoverageLineKeys(XmlDocument xmlDoc, string reportPath, HashSet<CoverageLineKey> executableLineKeys, HashSet<CoverageLineKey> coveredLineKeys, CoverageBackfillValidationState? validationState)
    {
        var sourceRoots = GetCoberturaSourceRoots(xmlDoc);
        var classNodes = xmlDoc.SelectNodes($"/*[{LocalNameEquals("coverage")}]//*[{LocalNameEquals("class")}][@filename][*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}]]");
        if (classNodes is null)
        {
            return false;
        }

        foreach (XmlNode? classNode in classNodes)
        {
            if (classNode is null)
            {
                continue;
            }

            var filename = classNode.Attributes?["filename"]?.Value;
            if (StringUtil.IsNullOrWhiteSpace(filename))
            {
                continue;
            }

            if (!TryGetCoberturaLineCoverageSourcePath(filename!, reportPath, sourceRoots, out var sourcePath))
            {
                return false;
            }

            var lineNodes = classNode.SelectNodes($"*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}]");
            if (lineNodes is null)
            {
                continue;
            }

            foreach (XmlNode? lineNode in lineNodes)
            {
                if (lineNode is null ||
                    !TryGetIntAttribute(lineNode, "number", out var line) ||
                    line <= 0 ||
                    !TryGetDoubleAttribute(lineNode, "hits", out var hits))
                {
                    continue;
                }

                if (!TryCreateCoverageLineKey(sourcePath, reportPath, line, line, validationState, out var lineKey))
                {
                    return false;
                }

                executableLineKeys.Add(lineKey);
                if (hits > 0)
                {
                    coveredLineKeys.Add(lineKey);
                }
            }
        }

        return true;
    }

    private static bool TryGetCoberturaLineCoverageSourcePath(string filename, string reportPath, string[] sourceRoots, out string sourcePath)
    {
        sourcePath = filename;
        if (sourceRoots.Length == 0 || IsPathRootedCrossPlatform(filename))
        {
            return true;
        }

        if (!TryGetCoberturaSourceRootRawPathCandidates(filename, reportPath, sourceRoots, out var rawCandidates))
        {
            return false;
        }

        if (rawCandidates.Length == 1)
        {
            sourcePath = rawCandidates[0];
        }

        return true;
    }

    private static bool TryAddOpenCoverCoverageLineKeys(XmlDocument xmlDoc, string reportPath, HashSet<CoverageLineKey> executableLineKeys, HashSet<CoverageLineKey> coveredLineKeys, CoverageBackfillValidationState? validationState)
    {
        var moduleNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module");
        if (moduleNodes is null)
        {
            return false;
        }

        foreach (XmlNode? moduleNode in moduleNodes)
        {
            if (moduleNode is null || !TryGetOpenCoverFileMap(moduleNode, out var fileMap))
            {
                return false;
            }

            var sequencePointNodes = moduleNode.SelectNodes(".//SequencePoint[@sl and @vc and @fileid]");
            if (sequencePointNodes is null)
            {
                continue;
            }

            foreach (XmlNode? sequencePointNode in sequencePointNodes)
            {
                if (sequencePointNode is null ||
                    !TryGetIntAttribute(sequencePointNode, "fileid", out var fileId) ||
                    !fileMap.TryGetValue(fileId, out var sourcePath) ||
                    !TryGetOpenCoverSequencePointRange(sequencePointNode, out var startLine, out var endLine) ||
                    !TryGetDoubleAttribute(sequencePointNode, "vc", out var visits))
                {
                    return false;
                }

                for (var line = startLine; line <= endLine; line++)
                {
                    if (!TryCreateCoverageLineKey(sourcePath, reportPath, line, line, validationState, out var lineKey))
                    {
                        return false;
                    }

                    executableLineKeys.Add(lineKey);
                    if (visits > 0)
                    {
                        coveredLineKeys.Add(lineKey);
                    }
                }
            }
        }

        return true;
    }

    private static bool TryAddMicrosoftCoverageLineKeys(XmlDocument xmlDoc, string reportPath, HashSet<CoverageLineKey> executableLineKeys, HashSet<CoverageLineKey> coveredLineKeys, CoverageBackfillValidationState? validationState)
    {
        if (!TryGetMicrosoftLineEntries(xmlDoc, out var lineEntries))
        {
            return false;
        }

        foreach (var lineEntry in lineEntries)
        {
            if (!lineEntry.HasValidLineRange ||
                !lineEntry.TryGetStatus(out var status))
            {
                continue;
            }

            for (var line = lineEntry.StartLine; line <= lineEntry.EndLine; line++)
            {
                if (!TryCreateCoverageLineKey(lineEntry.SourcePath, reportPath, line, line, validationState, out var lineKey))
                {
                    return false;
                }

                executableLineKeys.Add(lineKey);
                if (status == MicrosoftCoverageStatus.Covered)
                {
                    coveredLineKeys.Add(lineKey);
                }
            }
        }

        return true;
    }

    private static bool BackfillCoberturaClass(XmlNode classNode, byte[] backendBitmap, HashSet<int> representedBackendLines, out List<int> newlyRepresentedBackendLines, out bool duplicateRepresentedBackendLine)
    {
        newlyRepresentedBackendLines = [];
        duplicateRepresentedBackendLine = false;
        var rewritten = false;
        var lineNodes = classNode.SelectNodes($"*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}]");
        if (lineNodes is null)
        {
            return false;
        }

        foreach (XmlNode? lineNode in lineNodes)
        {
            if (lineNode is null ||
                !TryGetIntAttribute(lineNode, "number", out var line) ||
                !IsBackendLineCovered(backendBitmap, line))
            {
                continue;
            }

            if (!TryGetDoubleAttribute(lineNode, "hits", out var hits))
            {
                continue;
            }

            if (representedBackendLines.Add(line))
            {
                newlyRepresentedBackendLines.Add(line);
            }
            else
            {
                duplicateRepresentedBackendLine = true;
            }

            if (hits > 0)
            {
                continue;
            }

            SetAttribute(lineNode, "hits", "1");
            rewritten = true;
        }

        var duplicateLineNodes = classNode.SelectNodes($"*[{LocalNameEquals("methods")}]//*[{LocalNameEquals("line")}]");
        if (duplicateLineNodes is null)
        {
            return rewritten;
        }

        foreach (XmlNode? lineNode in duplicateLineNodes)
        {
            if (lineNode is null ||
                !TryGetIntAttribute(lineNode, "number", out var line) ||
                !newlyRepresentedBackendLines.Contains(line) ||
                !TryGetDoubleAttribute(lineNode, "hits", out var hits) ||
                hits > 0)
            {
                continue;
            }

            SetAttribute(lineNode, "hits", "1");
            rewritten = true;
        }

        return rewritten;
    }

    private static LineCounts UpdateCoberturaLineRate(XmlNode scopeNode)
    {
        var counts = CountCoberturaLines(scopeNode);
        var rate = counts.Total <= 0 ? 0 : counts.Covered / counts.Total;
        SetAttribute(scopeNode, "line-rate", FormatRate(rate));
        if (string.Equals(scopeNode.LocalName, "coverage", StringComparison.OrdinalIgnoreCase))
        {
            SetAttribute(scopeNode, "lines-valid", FormatNumber(counts.Total));
            SetAttribute(scopeNode, "lines-covered", FormatNumber(counts.Covered));
        }

        return counts;
    }

    private static LineCounts CountCoberturaLines(XmlNode scopeNode)
    {
        var lineNodes = string.Equals(scopeNode.LocalName, "class", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(scopeNode.LocalName, "method", StringComparison.OrdinalIgnoreCase) ?
                            scopeNode.SelectNodes($"*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}][@number]") :
                            scopeNode.SelectNodes($".//*[{LocalNameEquals("class")}]/*[{LocalNameEquals("lines")}]/*[{LocalNameEquals("line")}][@number]");
        if (lineNodes is null)
        {
            return default;
        }

        double total = 0;
        double covered = 0;
        foreach (XmlNode? lineNode in lineNodes)
        {
            if (lineNode is null)
            {
                continue;
            }

            if (!TryGetIntAttribute(lineNode, "number", out var line) || line <= 0)
            {
                continue;
            }

            if (TryGetDoubleAttribute(lineNode, "hits", out var hits) && hits > 0)
            {
                covered++;
            }

            total++;
        }

        return new LineCounts(total, covered);
    }

    private static bool TryGetOpenCoverFileMap(XmlNode moduleNode, out Dictionary<int, string> fileMap)
    {
        fileMap = new Dictionary<int, string>();
        var fileNodes = moduleNode.SelectNodes(".//Files/File[@uid and @fullPath]");
        if (fileNodes is null)
        {
            return true;
        }

        foreach (XmlNode? fileNode in fileNodes)
        {
            if (fileNode is not null && TryGetIntAttribute(fileNode, "uid", out var uid))
            {
                var fullPath = fileNode.Attributes?["fullPath"]?.Value ?? string.Empty;
                if (fileMap.TryGetValue(uid, out var existingPath) &&
                    !string.Equals(existingPath, fullPath, ResolvedPathComparison))
                {
                    return false;
                }

                fileMap[uid] = fullPath;
            }
        }

        return true;
    }

    private static void UpdateOpenCoverSummaries(XmlDocument xmlDoc)
    {
        UpdateOpenCoverSummary(xmlDoc.SelectSingleNode("/CoverageSession"), recursivePath: ".//SequencePoint");
        var moduleNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module");
        UpdateOpenCoverSummaryNodes(moduleNodes);
        var classNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module/Classes/Class");
        UpdateOpenCoverSummaryNodes(classNodes);
        var methodNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module/Classes/Class/Methods/Method");
        UpdateOpenCoverSummaryNodes(methodNodes);
    }

    private static void UpdateOpenCoverMethodVisitMetadata(XmlDocument xmlDoc)
    {
        var methodNodes = xmlDoc.SelectNodes("/CoverageSession/Modules/Module/Classes/Class/Methods/Method");
        if (methodNodes is null)
        {
            return;
        }

        foreach (XmlNode? methodNode in methodNodes)
        {
            if (methodNode is null)
            {
                continue;
            }

            var sequencePointNodes = methodNode.SelectNodes(".//SequencePoint");
            var methodVisited = sequencePointNodes is not null && HasVisitedOpenCoverSequencePoint(sequencePointNodes);
            if (methodNode.Attributes?["visited"] is not null)
            {
                SetAttribute(methodNode, "visited", methodVisited ? "true" : "false");
            }

            if (!methodVisited)
            {
                continue;
            }

            var methodPointNodes = methodNode.SelectNodes(".//MethodPoint[@vc]");
            if (methodPointNodes is null)
            {
                continue;
            }

            foreach (XmlNode? methodPointNode in methodPointNodes)
            {
                if (methodPointNode is not null &&
                    TryGetDoubleAttribute(methodPointNode, "vc", out var visits) &&
                    visits <= 0)
                {
                    SetAttribute(methodPointNode, "vc", "1");
                }
            }
        }
    }

    private static void UpdateOpenCoverSummaryNodes(XmlNodeList? nodes)
    {
        if (nodes is null)
        {
            return;
        }

        foreach (XmlNode? node in nodes)
        {
            UpdateOpenCoverSummary(node, recursivePath: ".//SequencePoint");
        }
    }

    private static void UpdateOpenCoverSummary(XmlNode? scopeNode, string recursivePath)
    {
        if (scopeNode is null)
        {
            return;
        }

        var summaryNode = scopeNode.SelectSingleNode("Summary");
        if (summaryNode is null)
        {
            return;
        }

        var sequencePoints = scopeNode.SelectNodes(recursivePath);
        if (sequencePoints is null)
        {
            return;
        }

        double total = 0;
        double covered = 0;
        foreach (XmlNode? sequencePointNode in sequencePoints)
        {
            if (sequencePointNode is null)
            {
                continue;
            }

            if (!TryGetOpenCoverSequencePointRange(sequencePointNode, out _, out _))
            {
                continue;
            }

            total++;
            if (TryGetDoubleAttribute(sequencePointNode, "vc", out var visits) && visits > 0)
            {
                covered++;
            }
        }

        SetAttribute(summaryNode, "numSequencePoints", FormatNumber(total));
        SetAttribute(summaryNode, "visitedSequencePoints", FormatNumber(covered));
        var sequenceCoverage = FormatPercentage(CalculatePercentage(covered, total));
        SetAttribute(summaryNode, "sequenceCoverage", sequenceCoverage);
        UpdateOpenCoverVisitedSummaryCounts(scopeNode, summaryNode);
        if (string.Equals(scopeNode.LocalName, "Method", StringComparison.OrdinalIgnoreCase) &&
            scopeNode.Attributes?["sequenceCoverage"] is not null)
        {
            SetAttribute(scopeNode, "sequenceCoverage", sequenceCoverage);
        }
    }

    private static void UpdateOpenCoverVisitedSummaryCounts(XmlNode scopeNode, XmlNode summaryNode)
    {
        if (summaryNode.Attributes?["numMethods"] is not null ||
            summaryNode.Attributes?["visitedMethods"] is not null)
        {
            UpdateOpenCoverVisitedNodeCounts(summaryNode, scopeNode, "Method", "numMethods", "visitedMethods");
        }

        if (summaryNode.Attributes?["numClasses"] is not null ||
            summaryNode.Attributes?["visitedClasses"] is not null)
        {
            UpdateOpenCoverVisitedNodeCounts(summaryNode, scopeNode, "Class", "numClasses", "visitedClasses");
        }
    }

    private static void UpdateOpenCoverVisitedNodeCounts(XmlNode summaryNode, XmlNode scopeNode, string nodeName, string totalAttributeName, string visitedAttributeName)
    {
        double total = 0;
        double visited = 0;
        CountOpenCoverVisitedNode(scopeNode, nodeName, ref total, ref visited);
        var nodes = scopeNode.SelectNodes(".//" + nodeName);
        if (nodes is null)
        {
            SetAttribute(summaryNode, totalAttributeName, FormatNumber(total));
            SetAttribute(summaryNode, visitedAttributeName, FormatNumber(visited));
            return;
        }

        foreach (XmlNode? node in nodes)
        {
            CountOpenCoverVisitedNode(node, nodeName, ref total, ref visited);
        }

        SetAttribute(summaryNode, totalAttributeName, FormatNumber(total));
        SetAttribute(summaryNode, visitedAttributeName, FormatNumber(visited));
    }

    private static void CountOpenCoverVisitedNode(XmlNode? node, string nodeName, ref double total, ref double visited)
    {
        if (node is null || !string.Equals(node.LocalName, nodeName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sequencePointNodes = node.SelectNodes(".//SequencePoint");
        if (sequencePointNodes is null || sequencePointNodes.Count == 0)
        {
            return;
        }

        total++;
        if (HasVisitedOpenCoverSequencePoint(sequencePointNodes))
        {
            visited++;
        }
    }

    private static bool HasVisitedOpenCoverSequencePoint(XmlNodeList sequencePointNodes)
    {
        foreach (XmlNode? sequencePointNode in sequencePointNodes)
        {
            if (sequencePointNode is null ||
                !TryGetOpenCoverSequencePointRange(sequencePointNode, out _, out _))
            {
                continue;
            }

            if (TryGetDoubleAttribute(sequencePointNode, "vc", out var visits) && visits > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasInvalidOpenCoverSequencePointRange(XmlDocument xmlDoc)
    {
        var sequencePointNodes = xmlDoc.SelectNodes("//SequencePoint");
        if (sequencePointNodes is null)
        {
            return false;
        }

        foreach (XmlNode? sequencePointNode in sequencePointNodes)
        {
            if (sequencePointNode is null)
            {
                continue;
            }

            if (!TryGetOpenCoverSequencePointRange(sequencePointNode, out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetOpenCoverSequencePointRange(XmlNode sequencePointNode, out int startLine, out int endLine)
    {
        if (!TryGetIntAttribute(sequencePointNode, "sl", out startLine))
        {
            endLine = 0;
            return false;
        }

        endLine = startLine;
        if (!TryGetOptionalIntAttribute(sequencePointNode, "el", out var candidateEndLine, out var hasEndLine))
        {
            return false;
        }

        if (hasEndLine)
        {
            endLine = candidateEndLine;
        }

        return startLine > 0 && endLine >= startLine;
    }

    private static bool TryGetBackendCoverage(CoverageBackfillData? backfillData, string? sourcePath, string reportPath, CoverageBackfillPathMatchTracker pathMatchTracker, CoverageBackfillValidationState? validationState, out string backendKey, out byte[] backendBitmap, out bool rejectedUnsafeMatch)
    {
        if (StringUtil.IsNullOrWhiteSpace(sourcePath))
        {
            rejectedUnsafeMatch = false;
            backendKey = string.Empty;
            backendBitmap = [];
            return false;
        }

        if (!IsPathRootedCrossPlatform(sourcePath!) && ContainsRelativeDotDirectoryPathSegment(sourcePath!))
        {
            rejectedUnsafeMatch = true;
            backendKey = string.Empty;
            backendBitmap = [];
            return false;
        }

        if (IsPathRootedCrossPlatform(sourcePath!))
        {
            return TryGetRootedBackendCoverage(backfillData, sourcePath!, pathMatchTracker, validationState, out backendKey, out backendBitmap, out rejectedUnsafeMatch);
        }

        if (IsAbsoluteUriPath(sourcePath!))
        {
            rejectedUnsafeMatch = true;
            backendKey = string.Empty;
            backendBitmap = [];
            return false;
        }

        return TryGetBackendCoverage(backfillData, GetRawPathCandidates(sourcePath!, reportPath), pathMatchTracker, validationState, allowExactMatch: true, allowSuffixMatch: false, TryNormalizeLocalCandidate(sourcePath!), out backendKey, out backendBitmap, out rejectedUnsafeMatch);
    }

    private static bool TryGetRootedBackendCoverage(CoverageBackfillData? backfillData, string sourcePath, CoverageBackfillPathMatchTracker pathMatchTracker, CoverageBackfillValidationState? validationState, out string backendKey, out byte[] backendBitmap, out bool rejectedUnsafeMatch)
    {
        backendKey = string.Empty;
        backendBitmap = [];
        rejectedUnsafeMatch = false;

        var sourceRootRelativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourcePath, false);
        if (IsSafeRelativePathCandidate(sourceRootRelativePath, sourcePath) &&
            TryGetBackendCoverage(backfillData, [sourceRootRelativePath], pathMatchTracker, validationState, allowExactMatch: true, allowSuffixMatch: false, normalizedLocalCandidate: null, out backendKey, out backendBitmap, out rejectedUnsafeMatch))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetBackendCoverage(CoverageBackfillData? backfillData, IEnumerable<string> rawCandidates, CoverageBackfillPathMatchTracker pathMatchTracker, CoverageBackfillValidationState? validationState, bool allowExactMatch, bool allowSuffixMatch, string? normalizedLocalCandidate, out string backendKey, out byte[] backendBitmap, out bool rejectedUnsafeMatch)
    {
        rejectedUnsafeMatch = false;
        if (backfillData is not { IsPresent: true, IsValid: true })
        {
            backendKey = string.Empty;
            backendBitmap = [];
            return false;
        }

        if (CoverageBackfillPathMatcher.TryGetBackendCoverage(backfillData, rawCandidates, allowSuffixMatch, out var match, out var hasAmbiguousActiveMatch))
        {
            if (!allowExactMatch && match.Kind != CoverageBackfillPathMatchKind.Suffix)
            {
                backendKey = string.Empty;
                backendBitmap = [];
                return false;
            }

            if (!allowSuffixMatch && match.Kind == CoverageBackfillPathMatchKind.Suffix)
            {
                backendKey = string.Empty;
                backendBitmap = [];
                return false;
            }

            var validationMatch = normalizedLocalCandidate is { Length: > 0 } ?
                                      match.WithNormalizedLocalCandidate(normalizedLocalCandidate) :
                                      match;
            if (validationMatch.HasActiveBits &&
                (!pathMatchTracker.TryRecord(validationMatch) ||
                 (validationState is not null && !validationState.TryRecordPathMatch(validationMatch))))
            {
                rejectedUnsafeMatch = true;
                backendKey = string.Empty;
                backendBitmap = [];
                return false;
            }

            backendKey = match.BackendKey;
            backendBitmap = match.Bitmap;
            return true;
        }

        if (hasAmbiguousActiveMatch)
        {
            rejectedUnsafeMatch = true;
        }

        backendKey = string.Empty;
        backendBitmap = [];
        return false;
    }

    private static string? TryNormalizeLocalCandidate(string sourcePath)
    {
        try
        {
            return CoverageBackfillData.NormalizePath(sourcePath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string[] GetCoberturaSourceRoots(XmlDocument xmlDoc)
    {
        var sourceNodes = xmlDoc.SelectNodes($"/*[{LocalNameEquals("coverage")}]/*[{LocalNameEquals("sources")}]/*[{LocalNameEquals("source")}]");
        if (sourceNodes is null || sourceNodes.Count == 0)
        {
            return [];
        }

        var sourceRoots = new List<string>(sourceNodes.Count);
        foreach (XmlNode? sourceNode in sourceNodes)
        {
            var sourceRoot = sourceNode?.InnerText;
            if (!StringUtil.IsNullOrWhiteSpace(sourceRoot))
            {
                sourceRoots.Add(sourceRoot!.Trim());
            }
        }

        return sourceRoots.ToArray();
    }

    private static bool TryGetCoberturaBackendCoverage(
        CoverageBackfillData? backfillData,
        string? filename,
        string reportPath,
        string[] sourceRoots,
        CoverageBackfillPathMatchTracker pathMatchTracker,
        CoverageBackfillValidationState? validationState,
        out string backendKey,
        out byte[] backendBitmap,
        out bool rejectedUnsafeMatch)
    {
        rejectedUnsafeMatch = false;
        backendKey = string.Empty;
        backendBitmap = [];
        if (StringUtil.IsNullOrWhiteSpace(filename))
        {
            return false;
        }

        if (!IsPathRootedCrossPlatform(filename!) && IsAbsoluteUriPath(filename!))
        {
            rejectedUnsafeMatch = true;
            return false;
        }

        if (sourceRoots.Length == 0 || IsPathRootedCrossPlatform(filename!))
        {
            return TryGetBackendCoverage(backfillData, filename!, reportPath, pathMatchTracker, validationState, out backendKey, out backendBitmap, out rejectedUnsafeMatch);
        }

        if (TryGetCoberturaSourceRootRawPathCandidates(filename!, reportPath, sourceRoots, out var sourceRootCandidates))
        {
            foreach (var sourceRootCandidate in sourceRootCandidates)
            {
                if (TryGetBackendCoverage(backfillData, sourceRootCandidate, reportPath, pathMatchTracker, validationState, out backendKey, out backendBitmap, out rejectedUnsafeMatch))
                {
                    return true;
                }

                if (rejectedUnsafeMatch)
                {
                    return false;
                }
            }

            return false;
        }

        rejectedUnsafeMatch = true;
        return false;
    }

    private static bool TryGetCoberturaSourceRootRawPathCandidates(string filename, string reportPath, string[] sourceRoots, out string[] rawCandidates)
    {
        rawCandidates = [];
        if (sourceRoots.Length == 0)
        {
            return true;
        }

        var candidateByNormalizedResolvedPath = new Dictionary<string, CoberturaSourceRootRawPathCandidate>(ResolvedPathComparer);
        var hasUnsafeCandidate = false;
        foreach (var sourceRoot in sourceRoots)
        {
            if (StringUtil.IsNullOrWhiteSpace(sourceRoot))
            {
                continue;
            }

            var sourceRootPath = sourceRoot.Trim();
            if (!IsPathRootedCrossPlatform(sourceRootPath) && IsAbsoluteUriPath(sourceRootPath))
            {
                hasUnsafeCandidate = true;
                continue;
            }

            var sourceRootRelativePath = IsCurrentDirectorySourceRoot(sourceRootPath) ?
                                             filename :
                                             Path.Combine(sourceRootPath, filename);
            if (IsUnsafeBackfillSourcePath(sourceRootRelativePath))
            {
                hasUnsafeCandidate = true;
                continue;
            }

            if (!TryResolvePathForComparison(sourceRootPath, reportPath, out _, out var normalizedSourceRoot, allowRoot: true) ||
                !TryResolvePathForComparison(sourceRootRelativePath, reportPath, out var resolvedPath, out var normalizedResolvedPath))
            {
                continue;
            }

            if (normalizedSourceRoot.Equals("/", ResolvedPathComparison) &&
                ContainsRelativeDotDirectoryPathSegment(filename))
            {
                hasUnsafeCandidate = true;
                continue;
            }

            if (!IsResolvedPathWithinBase(normalizedResolvedPath, normalizedSourceRoot))
            {
                hasUnsafeCandidate = true;
                continue;
            }

            if (!candidateByNormalizedResolvedPath.ContainsKey(normalizedResolvedPath))
            {
                candidateByNormalizedResolvedPath[normalizedResolvedPath] = new CoberturaSourceRootRawPathCandidate(sourceRootRelativePath, resolvedPath);
            }
        }

        if (hasUnsafeCandidate)
        {
            return false;
        }

        if (candidateByNormalizedResolvedPath.Count == 0)
        {
            return true;
        }

        if (candidateByNormalizedResolvedPath.Count == 1)
        {
            rawCandidates = [GetFirstValue(candidateByNormalizedResolvedPath).RawPath];
            return true;
        }

        var existingRawPath = string.Empty;
        var existingRawPathCount = 0;
        foreach (var candidate in candidateByNormalizedResolvedPath.Values)
        {
            if (!File.Exists(candidate.ResolvedPath))
            {
                continue;
            }

            existingRawPath = candidate.RawPath;
            existingRawPathCount++;
        }

        if (existingRawPathCount == 1)
        {
            rawCandidates = [existingRawPath];
            return true;
        }

        return false;
    }

    private static bool IsCurrentDirectorySourceRoot(string sourceRoot)
    {
        var trimmedSourceRoot = sourceRoot.Trim();
        if (trimmedSourceRoot.Equals(".", StringComparison.Ordinal))
        {
            return true;
        }

        var withoutTrailingSeparators = trimmedSourceRoot.TrimEnd('/', '\\');
        return withoutTrailingSeparators.Equals(".", StringComparison.Ordinal);
    }

    private static bool IsResolvedPathWithinBase(string normalizedPath, string normalizedBasePath)
    {
        if (normalizedBasePath.Equals("/", ResolvedPathComparison))
        {
            return !normalizedPath.Equals("/", ResolvedPathComparison) &&
                   normalizedPath.Length > 0;
        }

        var basePath = normalizedBasePath.TrimEnd('/');
        return basePath.Length > 0 &&
               (normalizedPath.Equals(basePath, ResolvedPathComparison) ||
                (normalizedPath.Length > basePath.Length &&
                 normalizedPath.StartsWith(basePath, ResolvedPathComparison) &&
                 normalizedPath[basePath.Length] == '/'));
    }

    private static bool TryResolvePathForComparison(string path, string reportPath, out string resolvedPath, out string normalizedPath, bool allowRoot = false)
    {
        resolvedPath = string.Empty;
        normalizedPath = string.Empty;
        try
        {
            var comparisonPath = path;
            if (!IsPathRootedCrossPlatform(comparisonPath))
            {
                var reportDirectory = Path.GetDirectoryName(reportPath);
                if (!StringUtil.IsNullOrWhiteSpace(reportDirectory))
                {
                    comparisonPath = Path.Combine(reportDirectory!, comparisonPath);
                }
            }

            resolvedPath = Path.GetFullPath(comparisonPath);
            if (allowRoot && IsFilesystemRootPath(resolvedPath))
            {
                normalizedPath = "/";
                return true;
            }

            normalizedPath = CoverageBackfillData.NormalizePath(resolvedPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsFilesystemRootPath(string path)
    {
        var root = Path.GetPathRoot(path);
        return !StringUtil.IsNullOrEmpty(root) &&
               path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   .Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), ResolvedPathComparison);
    }

    private static bool IsAbsoluteUriPath(string path)
    {
        return Uri.TryCreate(path, UriKind.Absolute, out _);
    }

    private static bool IsUnsafeAbsoluteUriPath(string path)
    {
        return !IsPathRootedCrossPlatform(path) &&
               IsAbsoluteUriPath(path);
    }

    private static bool IsUnsafeBackfillSourcePath(string path)
    {
        if (IsUnsafeAbsoluteUriPath(path))
        {
            return true;
        }

        if (!IsPathRootedCrossPlatform(path))
        {
            return ContainsRelativeDotDirectoryPathSegment(path);
        }

        var sourceRootRelativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(path, false);
        return !IsSafeRelativePathCandidate(sourceRootRelativePath, path);
    }

    private static bool IsPathRootedCrossPlatform(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return true;
        }

        if (path.StartsWith("\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        return path.Length >= 3 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               path[2] is '\\' or '/';
    }

    private static bool IsSafeRelativePathCandidate(string candidate, string originalPath)
    {
        if (StringUtil.IsNullOrWhiteSpace(candidate) ||
            candidate.Equals(originalPath, StringComparison.Ordinal) ||
            IsPathRootedCrossPlatform(candidate) ||
            Uri.TryCreate(candidate, UriKind.Absolute, out _) ||
            candidate.Equals("..", StringComparison.Ordinal) ||
            candidate.StartsWith("../", StringComparison.Ordinal) ||
            candidate.StartsWith("..\\", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsRelativeDotDirectoryPathSegment(string path)
    {
        var segmentStart = 0;
        for (var i = 0; i <= path.Length; i++)
        {
            if (i < path.Length && path[i] is not '/' and not '\\')
            {
                continue;
            }

            var segmentLength = i - segmentStart;
            if ((segmentLength == 1 && path[segmentStart] == '.') ||
                (segmentLength == 2 && path[segmentStart] == '.' && path[segmentStart + 1] == '.'))
            {
                return true;
            }

            segmentStart = i + 1;
        }

        return false;
    }

    /// <summary>
    /// Produces raw path forms that are commonly emitted by external XML coverage tools.
    /// </summary>
    /// <param name="sourcePath">Source path read from the coverage report.</param>
    /// <param name="reportPath">Coverage report path used for report-directory-relative source paths.</param>
    /// <returns>Raw absolute, source-root-relative, and report-directory-relative path candidates.</returns>
    private static IEnumerable<string> GetRawPathCandidates(string sourcePath, string reportPath)
    {
        yield return sourcePath;
        yield return CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourcePath, false);

        if (!IsPathRootedCrossPlatform(sourcePath))
        {
            var reportDirectory = Path.GetDirectoryName(reportPath);
            if (!StringUtil.IsNullOrWhiteSpace(reportDirectory))
            {
                var reportRelativePath = Path.Combine(reportDirectory!, sourcePath);
                yield return reportRelativePath;
                yield return CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(reportRelativePath, false);
            }
        }
    }

    private static bool CanPublishBackfilledCoverage(CoverageBackfillData? backfillData, HashSet<string> matchedBackendPaths, Dictionary<string, HashSet<int>> representedBackendLines)
    {
        if (backfillData is not { IsPresent: true, IsValid: true })
        {
            return matchedBackendPaths.Count == 0;
        }

        var backendFileCount = 0;
        foreach (var item in backfillData.ExecutedLinesByRelativePath)
        {
            if (!HasActiveBits(item.Value))
            {
                continue;
            }

            backendFileCount++;
            if (!matchedBackendPaths.Contains(item.Key) ||
                !representedBackendLines.TryGetValue(item.Key, out var representedLines) ||
                !RepresentsAllActiveBackendLines(item.Value, representedLines))
            {
                return false;
            }
        }

        return backendFileCount == matchedBackendPaths.Count;
    }

    private static bool RepresentsAllActiveBackendLines(byte[] bitmap, HashSet<int> representedLines)
    {
        for (var byteIndex = 0; byteIndex < bitmap.Length; byteIndex++)
        {
            var value = bitmap[byteIndex];
            if (value == 0)
            {
                continue;
            }

            for (var bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                if ((value & (128 >> bitIndex)) == 0)
                {
                    continue;
                }

                var line = (byteIndex * 8) + bitIndex + 1;
                if (!representedLines.Contains(line))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasValidatedBackfillCoverage(bool canBackfill, CoverageBackfillData? backfillData, HashSet<string> matchedBackendPaths, Dictionary<string, HashSet<int>> representedBackendLines)
        => canBackfill &&
           backfillData is { IsPresent: true, IsValid: true } &&
           matchedBackendPaths.Count > 0 &&
           CountBackendFilesWithCoverage(backfillData) > 0 &&
           HasRepresentedBackendLines(matchedBackendPaths, representedBackendLines);

    private static bool HasRepresentedBackendLines(HashSet<string> matchedBackendPaths, Dictionary<string, HashSet<int>> representedBackendLines)
    {
        foreach (var backendPath in matchedBackendPaths)
        {
            if (representedBackendLines.TryGetValue(backendPath, out var representedLines) &&
                representedLines.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RecordRepresentedBackendLines(CoverageBackfillValidationState? validationState, string backendKey, IEnumerable<int> representedLines)
    {
        if (validationState is null)
        {
            return true;
        }

        var recorded = true;
        foreach (var line in representedLines)
        {
            recorded &= validationState.RecordRepresentedBackendLine(backendKey, line);
        }

        return recorded;
    }

    private static HashSet<int> GetOrCreateLineSet(Dictionary<string, HashSet<int>> representedBackendLines, string backendKey)
    {
        if (!representedBackendLines.TryGetValue(backendKey, out var lineSet))
        {
            lineSet = new HashSet<int>();
            representedBackendLines[backendKey] = lineSet;
        }

        return lineSet;
    }

    private static CoberturaSourceRootRawPathCandidate GetFirstValue(Dictionary<string, CoberturaSourceRootRawPathCandidate> values)
    {
        foreach (var value in values.Values)
        {
            return value;
        }

        return default;
    }

    private static int CountBackendFilesWithCoverage(CoverageBackfillData? backfillData)
    {
        if (backfillData is not { IsPresent: true, IsValid: true })
        {
            return 0;
        }

        var count = 0;
        foreach (var bitmap in backfillData.ExecutedLinesByRelativePath.Values)
        {
            if (HasActiveBits(bitmap))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasActiveBits(byte[]? bitmap)
    {
        if (bitmap is null)
        {
            return false;
        }

        foreach (var value in bitmap)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountCoveredLines(byte[]? bitmap)
    {
        if (bitmap is null)
        {
            return 0;
        }

        var coveredLines = 0;
        for (var line = 1; line <= bitmap.Length * 8; line++)
        {
            if (IsBackendLineCovered(bitmap, line))
            {
                coveredLines++;
            }
        }

        return coveredLines;
    }

    private static bool IsBackendLineCovered(byte[] bitmap, int line)
    {
        if (line <= 0)
        {
            return false;
        }

        var index = line - 1;
        var byteIndex = index >> 3;
        if (byteIndex >= bitmap.Length)
        {
            return false;
        }

        var bitMask = (byte)(128 >> (index & 7));
        return (bitmap[byteIndex] & bitMask) != 0;
    }

    private static bool TryCreateCoverageLineKey(string sourcePath, string reportPath, int startLine, int endLine, CoverageBackfillValidationState? validationState, out CoverageLineKey lineKey)
    {
        lineKey = default;
        if (StringUtil.IsNullOrWhiteSpace(sourcePath) ||
            startLine <= 0 ||
            endLine < startLine)
        {
            return false;
        }

        try
        {
            var lineKeySourcePath = validationState is not null && validationState.TryGetCanonicalBackendPath(sourcePath, reportPath, out var canonicalBackendPath) ?
                                        canonicalBackendPath :
                                        GetMergedLineKeySourcePath(sourcePath, reportPath);
            lineKey = new CoverageLineKey(CoverageBackfillData.NormalizePath(lineKeySourcePath), startLine, endLine);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string GetMergedLineKeySourcePath(string sourcePath, string reportPath)
    {
        if (IsPathRootedCrossPlatform(sourcePath))
        {
            var sourceRootRelativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourcePath, false);
            if (IsSafeRelativePathCandidate(sourceRootRelativePath, sourcePath))
            {
                return sourceRootRelativePath;
            }
        }
        else
        {
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!StringUtil.IsNullOrWhiteSpace(reportDirectory))
            {
                var reportRelativePath = Path.Combine(reportDirectory!, sourcePath);
                var sourceRootRelativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(reportRelativePath, false);
                return IsSafeRelativePathCandidate(sourceRootRelativePath, reportRelativePath) ? sourceRootRelativePath : reportRelativePath;
            }
        }

        return sourcePath;
    }

    private static bool TryGetLineNumber(XmlNode lineNode, out int line)
    {
        return TryGetIntAttribute(lineNode, "number", out line) ||
               TryGetIntAttribute(lineNode, "num", out line) ||
               TryGetIntAttribute(lineNode, "line", out line);
    }

    private static XmlAttribute? GetHitAttribute(XmlNode lineNode)
    {
        return lineNode.Attributes?["hits"] ??
               lineNode.Attributes?["hit"] ??
               lineNode.Attributes?["visits"] ??
               lineNode.Attributes?["count"] ??
               lineNode.Attributes?["covered"];
    }

    private static bool TryParseCoverageHits(string value, out double hits)
    {
        if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out hits))
        {
            return true;
        }

        if (bool.TryParse(value, out var covered))
        {
            hits = covered ? 1 : 0;
            return true;
        }

        if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            hits = 1;
            return true;
        }

        if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            hits = 0;
            return true;
        }

        hits = 0;
        return false;
    }

    private static string GetBackfilledHitValue(XmlAttribute hitAttribute)
    {
        if (bool.TryParse(hitAttribute.Value, out _))
        {
            return "true";
        }

        if (string.Equals(hitAttribute.Value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hitAttribute.Value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return "yes";
        }

        return "1";
    }

    private static bool TryGetSourcePathFromNode(XmlNode node, out string sourcePath)
    {
        for (var current = node; current is not null; current = current.ParentNode)
        {
            sourcePath = current.Attributes?["path"]?.Value ??
                         current.Attributes?["file"]?.Value ??
                         current.Attributes?["filename"]?.Value ??
                         string.Empty;
            if (sourcePath.Length > 0)
            {
                return true;
            }
        }

        sourcePath = string.Empty;
        return false;
    }

    private static bool TryGetIntAttribute(XmlNode node, string attributeName, out int value)
    {
        value = 0;
        return node.Attributes?[attributeName] is { Value: { } attributeValue } &&
               int.TryParse(attributeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetOptionalIntAttribute(XmlNode node, string attributeName, out int value, out bool hasValue)
    {
        value = 0;
        hasValue = false;
        if (node.Attributes?[attributeName] is not { Value: { } attributeValue })
        {
            return true;
        }

        hasValue = true;
        return int.TryParse(attributeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDoubleAttribute(XmlNode node, string attributeName, out double value)
    {
        value = 0;
        return node.Attributes?[attributeName] is { Value: { } attributeValue } &&
               double.TryParse(attributeValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static double? SumAttributes(XmlNodeList attributes)
    {
        double total = 0;
        foreach (XmlNode? attribute in attributes)
        {
            if (attribute is null)
            {
                continue;
            }

            if (!double.TryParse(attribute.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            total += value;
        }

        return total;
    }

    private static double CalculatePercentage(double covered, double total)
    {
        return total <= 0 ? 0 : Math.Round((covered / total) * 100, 2).ToValidPercentage();
    }

    private static void SetAttribute(XmlNode node, string attributeName, string value)
    {
        if (node.Attributes is null)
        {
            return;
        }

        if (node.Attributes[attributeName] is { } attribute)
        {
            attribute.Value = value;
            return;
        }

        var newAttribute = node.OwnerDocument?.CreateAttribute(attributeName);
        if (newAttribute is null)
        {
            return;
        }

        newAttribute.Value = value;
        node.Attributes.Append(newAttribute);
    }

    private static bool TryLoadXmlDocument(string filePath, out XmlDocument xmlDoc)
    {
        xmlDoc = new XmlDocument
        {
            PreserveWhitespace = true,
            XmlResolver = null
        };

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var reader = XmlReader.Create(filePath, settings);
            xmlDoc.Load(reader);
            return true;
        }
        catch (Exception)
        {
            xmlDoc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            return false;
        }
    }

    private static bool TrySaveXmlDocument(XmlDocument xmlDoc, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (StringUtil.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        var temporaryPath = Path.Combine(directoryPath!, $".{fileName}.{Guid.NewGuid():N}.tmp");
        var backupPath = temporaryPath + ".bak";
        try
        {
            using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                xmlDoc.Save(fileStream);
            }

            BeforeReplaceXmlDocumentForTests?.Invoke(fullPath);
            if (ReplaceXmlDocumentForTests is { } replaceXmlDocument)
            {
                replaceXmlDocument(temporaryPath, fullPath, backupPath);
            }
            else
            {
                File.Replace(temporaryPath, fullPath, backupPath);
            }

            TryDeleteFile(backupPath);
            return true;
        }
        catch (Exception)
        {
            TryRestoreReplacementBackup(fullPath, backupPath);
            TryDeleteFile(temporaryPath);
            return false;
        }
    }

    private static void TryRestoreReplacementBackup(string fullPath, string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath) || File.Exists(fullPath))
            {
                return;
            }

            File.Move(backupPath, fullPath);
        }
        catch
        {
            // Leave the replacement backup on disk rather than deleting the only preserved copy of the original report.
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup only.
        }
    }

    private static bool DocumentElementHasLocalName(XmlDocument xmlDoc, string localName)
    {
        return string.Equals(xmlDoc.DocumentElement?.LocalName, localName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<MicrosoftLineEntry> GetMicrosoftLineEntries(XmlNode scopeNode)
        => TryGetMicrosoftLineEntries(scopeNode, out var lineEntries) ? lineEntries : [];

    private static bool TryGetMicrosoftLineEntries(XmlNode scopeNode, out List<MicrosoftLineEntry> lineEntries)
    {
        lineEntries = new List<MicrosoftLineEntry>();
        AddMicrosoftNestedLineEntries(scopeNode, lineEntries);
        return TryAddMicrosoftRangeLineEntries(scopeNode, lineEntries) &&
               TryAddMicrosoftCoverageXmlLineEntries(scopeNode, lineEntries);
    }

    private static void AddMicrosoftNestedLineEntries(XmlNode scopeNode, List<MicrosoftLineEntry> lineEntries)
    {
        var lineNodes = new List<XmlNode>();
        var seenNodes = new HashSet<XmlNode>();
        if (IsMicrosoftSourceContainer(scopeNode))
        {
            AddMicrosoftLineNodes(scopeNode.SelectNodes($".//*[{MicrosoftExecutableLinePredicate()}]"), lineNodes, seenNodes);
        }

        var descendantPrefix = scopeNode.NodeType == XmlNodeType.Document ? "//*" : ".//*";
        var sourceContainers = scopeNode.SelectNodes(
            $"{descendantPrefix}[({LocalNameEquals("file")} or {LocalNameEquals("sourcefile")} or {LocalNameEquals("source_file")} or {LocalNameEquals("document")}) and (@path or @file or @filename) and .//*[{MicrosoftExecutableLinePredicate()}]]");

        if (sourceContainers is { Count: > 0 })
        {
            foreach (XmlNode? sourceContainer in sourceContainers)
            {
                AddMicrosoftLineNodes(sourceContainer?.SelectNodes($".//*[{MicrosoftExecutableLinePredicate()}]"), lineNodes, seenNodes);
            }
        }

        foreach (var lineNode in lineNodes)
        {
            if (TryGetLineNumber(lineNode, out var line) &&
                GetHitAttribute(lineNode) is { } hitAttribute &&
                TryGetSourcePathFromNode(lineNode, out var sourcePath))
            {
                var valueKind = string.Equals(hitAttribute.Name, "covered", StringComparison.OrdinalIgnoreCase) ?
                                    MicrosoftCoverageValueKind.MicrosoftCoveredAttribute :
                                    MicrosoftCoverageValueKind.GenericAttribute;
                lineEntries.Add(new MicrosoftLineEntry(sourcePath, line, hitAttribute, valueKind));
            }
        }
    }

    private static bool IsMicrosoftSourceContainer(XmlNode node)
    {
        return (string.Equals(node.LocalName, "file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.LocalName, "sourcefile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.LocalName, "source_file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.LocalName, "document", StringComparison.OrdinalIgnoreCase)) &&
               (node.Attributes?["path"] is not null ||
                node.Attributes?["file"] is not null ||
                node.Attributes?["filename"] is not null) &&
               node.SelectSingleNode($".//*[{MicrosoftExecutableLinePredicate()}]") is not null;
    }

    private static void AddMicrosoftLineNodes(XmlNodeList? candidateNodes, List<XmlNode> lineNodes, HashSet<XmlNode> seenNodes)
    {
        if (candidateNodes is null)
        {
            return;
        }

        foreach (XmlNode? candidateNode in candidateNodes)
        {
            if (candidateNode is not null && seenNodes.Add(candidateNode))
            {
                lineNodes.Add(candidateNode);
            }
        }
    }

    private static bool TryAddMicrosoftRangeLineEntries(XmlNode scopeNode, List<MicrosoftLineEntry> lineEntries)
    {
        var ancestorModuleNode = GetSelfOrAncestor(scopeNode, "module");
        if (ancestorModuleNode is not null)
        {
            return TryAddMicrosoftRangeLineEntries(scopeNode, ancestorModuleNode, lineEntries);
        }

        foreach (var moduleNode in GetMicrosoftModuleNodes(scopeNode))
        {
            if (!TryAddMicrosoftRangeLineEntries(moduleNode, moduleNode, lineEntries))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAddMicrosoftRangeLineEntries(XmlNode rangeScopeNode, XmlNode sourceMapScopeNode, List<MicrosoftLineEntry> lineEntries)
    {
        var rangeNodes = rangeScopeNode.SelectNodes($".//*[{LocalNameEquals("range")}][@source_id and @start_line and @covered]");
        if (rangeNodes is null)
        {
            return true;
        }

        if (rangeNodes.Count == 0)
        {
            return true;
        }

        if (!TryGetMicrosoftRangeSourceFileMap(sourceMapScopeNode, out var sourceFileMap))
        {
            return false;
        }

        foreach (XmlNode? rangeNode in rangeNodes)
        {
            if (rangeNode is null ||
                rangeNode.Attributes?["source_id"] is not { Value: { } sourceId } ||
                !sourceFileMap.TryGetValue(sourceId, out var sourcePath) ||
                !TryGetIntAttribute(rangeNode, "start_line", out var line) ||
                rangeNode.Attributes?["covered"] is not { } coveredAttribute)
            {
                continue;
            }

            var endLine = line;
            if (!TryGetOptionalIntAttribute(rangeNode, "end_line", out var candidateEndLine, out var hasEndLine))
            {
                return false;
            }

            if (hasEndLine)
            {
                endLine = candidateEndLine;
            }

            lineEntries.Add(new MicrosoftLineEntry(sourcePath, line, endLine, coveredAttribute, MicrosoftCoverageValueKind.MicrosoftCoveredAttribute));
        }

        return true;
    }

    private static List<XmlNode> GetMicrosoftModuleNodes(XmlNode scopeNode)
    {
        if (HasLocalName(scopeNode, "module"))
        {
            return [scopeNode];
        }

        var moduleNodes = new List<XmlNode>();
        var descendantPrefix = scopeNode.NodeType == XmlNodeType.Document ? "//*" : ".//*";
        var selectedNodes = scopeNode.SelectNodes($"{descendantPrefix}[{LocalNameEquals("module")}]");
        if (selectedNodes is null)
        {
            return moduleNodes;
        }

        foreach (XmlNode? moduleNode in selectedNodes)
        {
            if (moduleNode is not null)
            {
                moduleNodes.Add(moduleNode);
            }
        }

        return moduleNodes;
    }

    private static XmlNode? GetSelfOrAncestor(XmlNode node, string localName)
    {
        for (var current = node; current is not null; current = current.ParentNode)
        {
            if (HasLocalName(current, localName))
            {
                return current;
            }
        }

        return null;
    }

    private static bool TryGetMicrosoftRangeSourceFileMap(XmlNode moduleNode, out Dictionary<string, string> sourceFileMap)
    {
        sourceFileMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceFileNodes = moduleNode.SelectNodes($".//*[{LocalNameEquals("source_file")}][@id and @path]");
        if (sourceFileNodes is null)
        {
            return true;
        }

        foreach (XmlNode? sourceFileNode in sourceFileNodes)
        {
            if (sourceFileNode is null ||
                sourceFileNode.Attributes?["id"] is not { Value: { } sourceId })
            {
                continue;
            }

            var sourcePath = sourceFileNode.Attributes?["path"]?.Value ?? string.Empty;
            if (sourceFileMap.TryGetValue(sourceId, out var existingPath) &&
                !string.Equals(existingPath, sourcePath, ResolvedPathComparison))
            {
                return false;
            }

            sourceFileMap[sourceId] = sourcePath;
        }

        return true;
    }

    private static bool TryAddMicrosoftCoverageXmlLineEntries(XmlNode scopeNode, List<MicrosoftLineEntry> lineEntries)
    {
        var ancestorModuleNode = GetSelfOrAncestor(scopeNode, "module");
        if (ancestorModuleNode is not null)
        {
            return TryAddMicrosoftCoverageXmlLineEntries(scopeNode, ancestorModuleNode, lineEntries);
        }

        var moduleNodes = GetMicrosoftModuleNodes(scopeNode);
        if (moduleNodes.Count == 0)
        {
            return TryAddMicrosoftCoverageXmlLineEntries(scopeNode, scopeNode, lineEntries);
        }

        foreach (var moduleNode in moduleNodes)
        {
            if (!TryAddMicrosoftCoverageXmlLineEntries(moduleNode, moduleNode, lineEntries))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAddMicrosoftCoverageXmlLineEntries(XmlNode lineScopeNode, XmlNode sourceMapScopeNode, List<MicrosoftLineEntry> lineEntries)
    {
        var descendantPrefix = lineScopeNode.NodeType == XmlNodeType.Document ? "//*" : ".//*";
        var lineNodes = lineScopeNode.SelectNodes(
            $"{descendantPrefix}[{LocalNameEquals("lines")} and *[{LocalNameEquals("lnstart")}] and *[{LocalNameEquals("coverage")}] and *[{LocalNameEquals("sourcefileid")}]]");
        if (lineNodes is null)
        {
            return true;
        }

        if (lineNodes.Count == 0)
        {
            return true;
        }

        if (!TryGetMicrosoftCoverageXmlSourceFileMap(sourceMapScopeNode, out var sourceFileMap))
        {
            return false;
        }

        foreach (XmlNode? lineNode in lineNodes)
        {
            if (lineNode is null ||
                !TryGetIntChildElement(lineNode, "LnStart", out var line) ||
                !TryGetChildElementText(lineNode, "SourceFileID", out var sourceFileId) ||
                !sourceFileMap.TryGetValue(sourceFileId, out var sourcePath) ||
                GetChildElement(lineNode, "Coverage") is not { } coverageNode)
            {
                continue;
            }

            var endLine = line;
            if (!TryGetOptionalIntChildElement(lineNode, "LnEnd", out var candidateEndLine, out var hasEndLine))
            {
                return false;
            }

            if (hasEndLine)
            {
                endLine = candidateEndLine;
            }

            lineEntries.Add(new MicrosoftLineEntry(sourcePath, line, endLine, coverageNode, MicrosoftCoverageValueKind.CoverageXmlElement));
        }

        return true;
    }

    private static bool TryGetMicrosoftCoverageXmlSourceFileMap(XmlNode scopeNode, out Dictionary<string, string> sourceFileMap)
    {
        sourceFileMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var descendantPrefix = scopeNode.NodeType == XmlNodeType.Document ? "//*" : ".//*";
        var sourceFileNodes = scopeNode.SelectNodes($"{descendantPrefix}[{LocalNameEquals("sourcefilenames")}]");
        if (sourceFileNodes is null)
        {
            return true;
        }

        foreach (XmlNode? sourceFileNode in sourceFileNodes)
        {
            if (sourceFileNode is null ||
                !TryGetChildElementText(sourceFileNode, "SourceFileID", out var sourceFileId) ||
                !TryGetChildElementText(sourceFileNode, "SourceFileName", out var sourcePath))
            {
                continue;
            }

            if (sourceFileMap.TryGetValue(sourceFileId, out var existingPath) &&
                !string.Equals(existingPath, sourcePath, ResolvedPathComparison))
            {
                return false;
            }

            sourceFileMap[sourceFileId] = sourcePath;
        }

        return true;
    }

    private static XmlNode? GetChildElement(XmlNode node, string localName)
    {
        foreach (XmlNode? childNode in node.ChildNodes)
        {
            if (childNode is not null &&
                childNode.NodeType == XmlNodeType.Element &&
                HasLocalName(childNode, localName))
            {
                return childNode;
            }
        }

        return null;
    }

    private static bool TryGetChildElementText(XmlNode node, string localName, out string value)
    {
        value = GetChildElement(node, localName)?.InnerText ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetIntChildElement(XmlNode node, string localName, out int value)
    {
        value = 0;
        return TryGetChildElementText(node, localName, out var text) &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetOptionalIntChildElement(XmlNode node, string localName, out int value, out bool hasValue)
    {
        value = 0;
        hasValue = false;
        if (!TryGetChildElementText(node, localName, out var text))
        {
            return true;
        }

        hasValue = true;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void SetChildElementText(XmlNode node, string localName, string value)
    {
        var childElement = GetChildElement(node, localName);
        if (childElement is not null)
        {
            childElement.InnerText = value;
        }
    }

    private static string MicrosoftExecutableLinePredicate()
    {
        return $"{LocalNameEquals("line")} and (@number or @num or @line) and (@hits or @hit or @visits or @count or @covered)";
    }

    private static bool HasLocalName(XmlNode node, string localName)
    {
        return string.Equals(node.LocalName, localName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBackfilledMicrosoftCoveredValue(XmlNode valueNode)
    {
        return valueNode is XmlAttribute attribute ? GetBackfilledHitValue(attribute) : "0";
    }

    private static bool TryParseMicrosoftCoverageStatus(string value, MicrosoftCoverageValueKind valueKind, out MicrosoftCoverageStatus status)
    {
        if (valueKind == MicrosoftCoverageValueKind.CoverageXmlElement)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var coverage))
            {
                status = coverage switch
                {
                    0 => MicrosoftCoverageStatus.Covered,
                    1 => MicrosoftCoverageStatus.PartiallyCovered,
                    2 => MicrosoftCoverageStatus.NotCovered,
                    _ => MicrosoftCoverageStatus.Unknown,
                };

                return status != MicrosoftCoverageStatus.Unknown;
            }

            status = MicrosoftCoverageStatus.Unknown;
            return false;
        }

        if (valueKind == MicrosoftCoverageValueKind.MicrosoftCoveredAttribute &&
            string.Equals(value, "partial", StringComparison.OrdinalIgnoreCase))
        {
            status = MicrosoftCoverageStatus.PartiallyCovered;
            return true;
        }

        if (TryParseCoverageHits(value, out var hits))
        {
            status = hits > 0 ? MicrosoftCoverageStatus.Covered : MicrosoftCoverageStatus.NotCovered;
            return true;
        }

        status = MicrosoftCoverageStatus.Unknown;
        return false;
    }

    private static string LocalNameEquals(string localName)
    {
        return $"translate(local-name(), '{UppercaseLetters}', '{LowercaseLetters}')='{localName}'";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatRate(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string FormatPercentage(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private readonly struct CoverageLineKey : IEquatable<CoverageLineKey>
    {
        public CoverageLineKey(string sourcePath, int startLine, int endLine)
        {
            SourcePath = sourcePath;
            StartLine = startLine;
            EndLine = endLine;
        }

        private string SourcePath { get; }

        private int StartLine { get; }

        private int EndLine { get; }

        public bool Equals(CoverageLineKey other)
            => ResolvedPathComparer.Equals(SourcePath, other.SourcePath) &&
               StartLine == other.StartLine &&
               EndLine == other.EndLine;

        public override bool Equals(object? obj)
            => obj is CoverageLineKey other && Equals(other);

        public override int GetHashCode()
        {
#if NETCOREAPP3_1_OR_GREATER
            return HashCode.Combine(ResolvedPathComparer.GetHashCode(SourcePath), StartLine, EndLine);
#else
            unchecked
            {
                var hashCode = ResolvedPathComparer.GetHashCode(SourcePath);
                hashCode = (hashCode * 397) ^ StartLine;
                hashCode = (hashCode * 397) ^ EndLine;
                return hashCode;
            }
#endif
        }
    }

    private readonly struct MicrosoftSourceLineKey : IEquatable<MicrosoftSourceLineKey>
    {
        public MicrosoftSourceLineKey(string sourcePath, int line)
        {
            SourcePath = sourcePath;
            Line = line;
        }

        private string SourcePath { get; }

        private int Line { get; }

        public bool Equals(MicrosoftSourceLineKey other)
            => ResolvedPathComparer.Equals(SourcePath, other.SourcePath) &&
               Line == other.Line;

        public override bool Equals(object? obj)
            => obj is MicrosoftSourceLineKey other && Equals(other);

        public override int GetHashCode()
        {
#if NETCOREAPP3_1_OR_GREATER
            return HashCode.Combine(ResolvedPathComparer.GetHashCode(SourcePath), Line);
#else
            unchecked
            {
                return (ResolvedPathComparer.GetHashCode(SourcePath) * 397) ^ Line;
            }
#endif
        }
    }

    private readonly struct MicrosoftLineEntry
    {
        public MicrosoftLineEntry(string sourcePath, int line, XmlNode valueNode, MicrosoftCoverageValueKind valueKind)
            : this(sourcePath, line, line, valueNode, valueKind)
        {
        }

        public MicrosoftLineEntry(string sourcePath, int startLine, int endLine, XmlNode valueNode, MicrosoftCoverageValueKind valueKind)
        {
            SourcePath = sourcePath;
            StartLine = startLine;
            EndLine = endLine;
            ValueNode = valueNode;
            ValueKind = valueKind;
        }

        public string SourcePath { get; }

        public int StartLine { get; }

        public int EndLine { get; }

        public bool HasValidLineRange => StartLine > 0 && EndLine >= StartLine;

        public int LineCount => HasValidLineRange ? EndLine - StartLine + 1 : 0;

        private XmlNode ValueNode { get; }

        private MicrosoftCoverageValueKind ValueKind { get; }

        public bool TryGetHits(out double hits)
        {
            if (TryGetStatus(out var status))
            {
                hits = status == MicrosoftCoverageStatus.Covered ? 1 : 0;
                return true;
            }

            hits = 0;
            return false;
        }

        public bool TryGetStatus(out MicrosoftCoverageStatus status)
            => TryParseMicrosoftCoverageStatus(ValueNode.Value ?? ValueNode.InnerText, ValueKind, out status);

        public void SetCovered()
        {
            if (ValueNode is XmlAttribute attribute)
            {
                attribute.Value = GetBackfilledMicrosoftCoveredValue(attribute);
                return;
            }

            ValueNode.InnerText = GetBackfilledMicrosoftCoveredValue(ValueNode);
        }
    }

    private readonly struct LineCounts
    {
        public LineCounts(double total, double covered)
            : this(total, covered, partiallyCovered: 0)
        {
        }

        public LineCounts(double total, double covered, double partiallyCovered)
        {
            Total = total;
            Covered = covered;
            PartiallyCovered = partiallyCovered;
        }

        public double Total { get; }

        public double Covered { get; }

        public double PartiallyCovered { get; }
    }

    private readonly struct CoberturaSourceRootRawPathCandidate
    {
        public CoberturaSourceRootRawPathCandidate(string rawPath, string resolvedPath)
        {
            RawPath = rawPath;
            ResolvedPath = resolvedPath;
        }

        public string RawPath { get; }

        public string ResolvedPath { get; }
    }

    /// <summary>
    /// Accumulates backend path matches across the XML reports that will contribute to one published coverage result.
    /// </summary>
    internal sealed class CoverageBackfillValidationState
    {
        private static readonly StringComparer LocalCandidateComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private readonly HashSet<string> _matchedBackendPaths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _localCandidateByBackendPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _backendPathByLocalCandidate = new(LocalCandidateComparer);
        private readonly Dictionary<string, HashSet<int>> _representedBackendLines = new(StringComparer.Ordinal);
        private readonly bool _rejectDuplicateRepresentedBackendLines;
        private CoverageBackfillData? _backfillData;
        private bool _backfillAttempted;
        private bool _unsafePathMatch;
        private bool _duplicateRepresentedBackendLine;
        private bool _unsupportedBackfill;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoverageBackfillValidationState"/> class.
        /// </summary>
        /// <param name="rejectDuplicateRepresentedBackendLines">True when the merged report set must fail if two reports represent the same backend line.</param>
        internal CoverageBackfillValidationState(bool rejectDuplicateRepresentedBackendLines = false)
        {
            _rejectDuplicateRepresentedBackendLines = rejectDuplicateRepresentedBackendLines;
        }

        /// <summary>
        /// Gets a value indicating whether backend ITR coverage was evaluated without unsafe XML report matches.
        /// </summary>
        internal bool BackfillValidated => _backfillAttempted && CanPublish();

        /// <summary>
        /// Gets a value indicating whether the processed report represented the same backend line more than once.
        /// </summary>
        internal bool HasDuplicateRepresentedBackendLine => _duplicateRepresentedBackendLine;

        /// <summary>
        /// Gets the number of backend covered lines represented by this report set.
        /// </summary>
        internal int RepresentedBackendLineCount
        {
            get
            {
                var count = 0;
                foreach (var item in _representedBackendLines)
                {
                    count += item.Value.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// Merges validation data from one processed XML report into this validation set.
        /// </summary>
        /// <param name="other">Processed report validation data.</param>
        internal void Merge(CoverageBackfillValidationState other)
        {
            if (other._backfillAttempted)
            {
                _backfillAttempted = true;
                _backfillData ??= other._backfillData;
            }

            _unsafePathMatch |= other._unsafePathMatch;
            if (_rejectDuplicateRepresentedBackendLines)
            {
                _duplicateRepresentedBackendLine |= other._duplicateRepresentedBackendLine;
            }

            _unsupportedBackfill |= other._unsupportedBackfill;
            foreach (var backendPath in other._matchedBackendPaths)
            {
                _matchedBackendPaths.Add(backendPath);
            }

            foreach (var item in other._localCandidateByBackendPath)
            {
                if (!TryRecordPathIdentity(item.Key, item.Value))
                {
                    continue;
                }
            }

            foreach (var item in other._representedBackendLines)
            {
                var representedLines = GetOrCreateLineSet(_representedBackendLines, item.Key);
                foreach (var line in item.Value)
                {
                    var added = representedLines.Add(line);
                    if (_rejectDuplicateRepresentedBackendLines && !added)
                    {
                        _duplicateRepresentedBackendLine = true;
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether the accumulated report set can publish local coverage after any safe backend merge.
        /// </summary>
        /// <returns>True when either no backfill was attempted or matched backend coverage was processed without unsafe paths, duplicate represented lines, or unsupported XML data.</returns>
        internal bool CanPublish()
            => !_backfillAttempted ||
               (!_unsafePathMatch &&
                !_duplicateRepresentedBackendLine &&
                !_unsupportedBackfill &&
                CanPublishBackfilledCoverage(_backfillData, _matchedBackendPaths, _representedBackendLines));

        /// <summary>
        /// Gets whether this report set can be kept after local XML backfill when it will not be used for coverage publication.
        /// </summary>
        /// <returns>True when backfill either was not attempted or represented at least one backend line without unsafe matches.</returns>
        internal bool CanKeepUnpublishedBackfill()
            => !_backfillAttempted ||
               (!_unsafePathMatch &&
                !_duplicateRepresentedBackendLine &&
                !_unsupportedBackfill &&
                RepresentedBackendLineCount > 0);

        internal void MarkBackfillAttempted(CoverageBackfillData backfillData)
        {
            _backfillAttempted = true;
            _backfillData ??= backfillData;
        }

        internal void RecordMatchedBackendPath(string backendKey, byte[] backendBitmap)
        {
            if (HasActiveBits(backendBitmap))
            {
                _matchedBackendPaths.Add(backendKey);
            }
        }

        internal void RecordUnsupportedBackfill()
        {
            _unsupportedBackfill = true;
        }

        internal bool TryRecordPathMatch(CoverageBackfillPathMatch match)
        {
            if (!match.HasActiveBits)
            {
                return true;
            }

            if (_localCandidateByBackendPath.TryGetValue(match.BackendKey, out var existingCandidate))
            {
                if (LocalCandidateComparer.Equals(existingCandidate, match.NormalizedLocalCandidate))
                {
                    return true;
                }

                _unsafePathMatch = true;
                return false;
            }

            return TryRecordPathIdentity(match.BackendKey, match.NormalizedLocalCandidate);
        }

        internal bool TryGetCanonicalBackendPath(string sourcePath, string reportPath, out string backendPath)
        {
            if (TryGetBackendPathByNormalizedLocalCandidate(sourcePath, out backendPath))
            {
                return true;
            }

            var mergedLineKeySourcePath = GetMergedLineKeySourcePath(sourcePath, reportPath);
            return !LocalCandidateComparer.Equals(sourcePath, mergedLineKeySourcePath) &&
                   TryGetBackendPathByNormalizedLocalCandidate(mergedLineKeySourcePath, out backendPath);
        }

        internal bool RecordRepresentedBackendLine(string backendKey, int line)
        {
            if (GetOrCreateLineSet(_representedBackendLines, backendKey).Add(line))
            {
                return true;
            }

            if (_rejectDuplicateRepresentedBackendLines)
            {
                _duplicateRepresentedBackendLine = true;
                return false;
            }

            return true;
        }

        internal void RecordUnsafePathMatch()
            => _unsafePathMatch = true;

        private bool TryRecordPathIdentity(string backendKey, string normalizedLocalCandidate)
        {
            if (_localCandidateByBackendPath.TryGetValue(backendKey, out var existingCandidate) &&
                !LocalCandidateComparer.Equals(existingCandidate, normalizedLocalCandidate))
            {
                _unsafePathMatch = true;
                return false;
            }

            if (_backendPathByLocalCandidate.TryGetValue(normalizedLocalCandidate, out var existingBackendKey) &&
                !StringComparer.Ordinal.Equals(existingBackendKey, backendKey))
            {
                _unsafePathMatch = true;
                return false;
            }

            _localCandidateByBackendPath[backendKey] = normalizedLocalCandidate;
            _backendPathByLocalCandidate[normalizedLocalCandidate] = backendKey;
            return true;
        }

        private bool TryGetBackendPathByNormalizedLocalCandidate(string sourcePath, out string backendPath)
        {
            backendPath = string.Empty;
            var normalizedSourcePath = TryNormalizeLocalCandidate(sourcePath);
            if (normalizedSourcePath is null ||
                !_backendPathByLocalCandidate.TryGetValue(normalizedSourcePath, out var matchedBackendPath))
            {
                return false;
            }

            backendPath = matchedBackendPath;
            return true;
        }
    }
}
