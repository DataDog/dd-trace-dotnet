// <copyright file="ExternalCoverageXmlBackfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Reads and rewrites line-capable XML coverage reports with backend ITR skipped-test coverage.
/// </summary>
internal static class ExternalCoverageXmlBackfill
{
    /// <summary>
    /// Reads an XML coverage report and applies backend ITR line coverage when requested and supported by the format.
    /// </summary>
    /// <param name="filePath">Coverage XML file path.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="applyBackfill">Whether the caller requires backend-aware coverage.</param>
    /// <param name="result">Coverage result after parsing and optional mutation.</param>
    /// <returns>True when the file was parsed with enough information to publish line coverage.</returns>
    public static bool TryProcess(string filePath, CoverageBackfillData? backfillData, bool applyBackfill, out ExternalCoverageXmlResult result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        xmlDoc.Load(filePath);

        var canBackfill = applyBackfill && backfillData is { IsPresent: true, IsValid: true };
        if (xmlDoc.SelectSingleNode("/coverage") is not null)
        {
            return TryProcessCobertura(xmlDoc, filePath, backfillData, canBackfill, out result);
        }

        if (xmlDoc.SelectSingleNode("/CoverageSession") is not null)
        {
            return TryProcessOpenCover(xmlDoc, filePath, backfillData, canBackfill, out result);
        }

        if (TryProcessMicrosoftLineXml(xmlDoc, filePath, backfillData, canBackfill, out result))
        {
            return true;
        }

        if (applyBackfill)
        {
            return false;
        }

        return TryReadMicrosoftAggregateXml(xmlDoc, out result);
    }

    private static bool TryProcessCobertura(XmlDocument xmlDoc, string filePath, CoverageBackfillData? backfillData, bool canBackfill, out ExternalCoverageXmlResult result)
    {
        result = default;
        var classNodes = xmlDoc.SelectNodes("//class[lines/line]");
        if (classNodes is null || classNodes.Count == 0)
        {
            return false;
        }

        var rewritten = false;
        foreach (XmlNode? classNode in classNodes)
        {
            if (classNode is null)
            {
                continue;
            }

            var filename = classNode.Attributes?["filename"]?.Value;
            var backendBitmap = GetBackendBitmap(backfillData, filename);
            if (canBackfill && backendBitmap is not null)
            {
                rewritten |= BackfillCoberturaClass(classNode, backendBitmap);
            }

            UpdateCoberturaLineRate(classNode);
        }

        var packageNodes = xmlDoc.SelectNodes("//package");
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

        var coverageNode = xmlDoc.SelectSingleNode("/coverage");
        if (coverageNode is null)
        {
            return false;
        }

        var counts = UpdateCoberturaLineRate(coverageNode);
        if (counts.Total <= 0)
        {
            return false;
        }

        if (rewritten)
        {
            xmlDoc.Save(filePath);
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(counts.Covered, counts.Total),
            counts.Total,
            counts.Covered,
            backfilled: canBackfill,
            rewritten,
            diagnostic: "cobertura");
        return true;
    }

    private static bool TryProcessOpenCover(XmlDocument xmlDoc, string filePath, CoverageBackfillData? backfillData, bool canBackfill, out ExternalCoverageXmlResult result)
    {
        result = default;
        var fileMap = GetOpenCoverFileMap(xmlDoc);
        var sequencePointNodes = xmlDoc.SelectNodes("//SequencePoint[@sl and @vc]");
        if (sequencePointNodes is null || sequencePointNodes.Count == 0)
        {
            return false;
        }

        var rewritten = false;
        foreach (XmlNode? sequencePointNode in sequencePointNodes)
        {
            if (sequencePointNode is null)
            {
                continue;
            }

            if (!canBackfill ||
                !TryGetIntAttribute(sequencePointNode, "fileid", out var fileId) ||
                !fileMap.TryGetValue(fileId, out var sourcePath) ||
                GetBackendBitmap(backfillData, sourcePath) is not { } backendBitmap ||
                !TryGetIntAttribute(sequencePointNode, "sl", out var line) ||
                !IsBackendLineCovered(backendBitmap, line) ||
                !TryGetIntAttribute(sequencePointNode, "vc", out var visits) ||
                visits > 0)
            {
                continue;
            }

            SetAttribute(sequencePointNode, "vc", "1");
            rewritten = true;
        }

        UpdateOpenCoverSummaries(xmlDoc);
        var rootSummary = xmlDoc.SelectSingleNode("/CoverageSession/Summary");
        if (rootSummary is null ||
            !TryGetDoubleAttribute(rootSummary, "numSequencePoints", out var total) ||
            !TryGetDoubleAttribute(rootSummary, "visitedSequencePoints", out var covered) ||
            total <= 0)
        {
            return false;
        }

        if (rewritten)
        {
            xmlDoc.Save(filePath);
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(covered, total),
            total,
            covered,
            backfilled: canBackfill,
            rewritten,
            diagnostic: "opencover");
        return true;
    }

    private static bool TryProcessMicrosoftLineXml(XmlDocument xmlDoc, string filePath, CoverageBackfillData? backfillData, bool canBackfill, out ExternalCoverageXmlResult result)
    {
        result = default;
        var lineNodes = xmlDoc.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='line'][@number or @num or @line]");
        if (lineNodes is null || lineNodes.Count == 0)
        {
            return false;
        }

        var rewritten = false;
        double total = 0;
        double covered = 0;
        foreach (XmlNode? lineNode in lineNodes)
        {
            if (lineNode is null || !TryGetLineNumber(lineNode, out var line))
            {
                continue;
            }

            total++;
            var hitAttribute = GetHitAttribute(lineNode);
            var hits = hitAttribute is null ? 0 : ParseCoverageHits(hitAttribute.Value);
            if (canBackfill &&
                hits <= 0 &&
                TryGetSourcePathFromNode(lineNode, out var sourcePath) &&
                GetBackendBitmap(backfillData, sourcePath) is { } backendBitmap &&
                IsBackendLineCovered(backendBitmap, line))
            {
                SetAttribute(lineNode, hitAttribute?.Name ?? "hits", "1");
                hits = 1;
                rewritten = true;
            }

            if (hits > 0)
            {
                covered++;
            }
        }

        if (total <= 0)
        {
            return false;
        }

        if (rewritten)
        {
            xmlDoc.Save(filePath);
        }

        result = new ExternalCoverageXmlResult(
            CalculatePercentage(covered, total),
            total,
            covered,
            backfilled: canBackfill,
            rewritten,
            diagnostic: "microsoft-line");
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

    private static bool BackfillCoberturaClass(XmlNode classNode, byte[] backendBitmap)
    {
        var rewritten = false;
        var lineNodes = classNode.SelectNodes("lines/line");
        if (lineNodes is null)
        {
            return false;
        }

        foreach (XmlNode? lineNode in lineNodes)
        {
            if (lineNode is null ||
                !TryGetIntAttribute(lineNode, "number", out var line) ||
                !IsBackendLineCovered(backendBitmap, line) ||
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
        if (string.Equals(scopeNode.Name, "coverage", StringComparison.Ordinal))
        {
            SetAttribute(scopeNode, "lines-valid", FormatNumber(counts.Total));
            SetAttribute(scopeNode, "lines-covered", FormatNumber(counts.Covered));
        }

        return counts;
    }

    private static LineCounts CountCoberturaLines(XmlNode scopeNode)
    {
        var lineNodes = scopeNode.SelectNodes(".//line[@number]");
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

            total++;
            if (TryGetDoubleAttribute(lineNode, "hits", out var hits) && hits > 0)
            {
                covered++;
            }
        }

        return new LineCounts(total, covered);
    }

    private static Dictionary<int, string> GetOpenCoverFileMap(XmlDocument xmlDoc)
    {
        var fileMap = new Dictionary<int, string>();
        var fileNodes = xmlDoc.SelectNodes("//File[@uid and @fullPath]");
        if (fileNodes is null)
        {
            return fileMap;
        }

        foreach (XmlNode? fileNode in fileNodes)
        {
            if (fileNode is not null && TryGetIntAttribute(fileNode, "uid", out var uid))
            {
                fileMap[uid] = fileNode.Attributes?["fullPath"]?.Value ?? string.Empty;
            }
        }

        return fileMap;
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

            total++;
            if (TryGetDoubleAttribute(sequencePointNode, "vc", out var visits) && visits > 0)
            {
                covered++;
            }
        }

        SetAttribute(summaryNode, "numSequencePoints", FormatNumber(total));
        SetAttribute(summaryNode, "visitedSequencePoints", FormatNumber(covered));
        SetAttribute(summaryNode, "sequenceCoverage", FormatPercentage(CalculatePercentage(covered, total)));
    }

    private static byte[]? GetBackendBitmap(CoverageBackfillData? backfillData, string? sourcePath)
    {
        if (backfillData is not { IsPresent: true, IsValid: true } || string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        var relativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourcePath!, false);
        try
        {
            return backfillData.ExecutedLinesByRelativePath.TryGetValue(CoverageBackfillData.NormalizePath(relativePath), out var bitmap) ? bitmap : null;
        }
        catch
        {
            return null;
        }
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

    private static double ParseCoverageHits(string value)
    {
        if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hits))
        {
            return hits;
        }

        if (bool.TryParse(value, out var covered))
        {
            return covered ? 1 : 0;
        }

        return string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static bool TryGetSourcePathFromNode(XmlNode node, out string sourcePath)
    {
        sourcePath = node.Attributes?["path"]?.Value ??
                     node.Attributes?["file"]?.Value ??
                     node.Attributes?["filename"]?.Value ??
                     node.ParentNode?.Attributes?["path"]?.Value ??
                     node.ParentNode?.Attributes?["file"]?.Value ??
                     node.ParentNode?.Attributes?["filename"]?.Value ??
                     string.Empty;
        return sourcePath.Length > 0;
    }

    private static bool TryGetIntAttribute(XmlNode node, string attributeName, out int value)
    {
        value = 0;
        return node.Attributes?[attributeName] is { Value: { } attributeValue } &&
               int.TryParse(attributeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
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

    private readonly struct LineCounts
    {
        public LineCounts(double total, double covered)
        {
            Total = total;
            Covered = covered;
        }

        public double Total { get; }

        public double Covered { get; }
    }
}
