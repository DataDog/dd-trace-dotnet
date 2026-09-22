// <copyright file="ExternalCoverageXmlResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Describes the result of reading and optionally backfilling an external XML coverage report.
/// </summary>
internal readonly struct ExternalCoverageXmlResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalCoverageXmlResult"/> struct.
    /// </summary>
    /// <param name="percentage">Line coverage percentage after any mutation.</param>
    /// <param name="executableLines">Optional executable-line or sequence-point count used by the report format.</param>
    /// <param name="coveredLines">Optional covered-line or visited sequence-point count used by the report format.</param>
    /// <param name="backfilled">Whether backend ITR coverage was safely reconciled with this XML coverage result.</param>
    /// <param name="rewritten">Whether the XML file was modified on disk.</param>
    /// <param name="diagnostic">Compact diagnostic text for logs.</param>
    public ExternalCoverageXmlResult(double percentage, double? executableLines, double? coveredLines, bool backfilled, bool rewritten, string? diagnostic)
    {
        Percentage = percentage;
        ExecutableLines = executableLines;
        CoveredLines = coveredLines;
        Backfilled = backfilled;
        Rewritten = rewritten;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the line coverage percentage after any mutation.
    /// </summary>
    public double Percentage { get; }

    /// <summary>
    /// Gets the executable-line or sequence-point count used by the report format, when the report exposes one.
    /// </summary>
    public double? ExecutableLines { get; }

    /// <summary>
    /// Gets the covered-line or visited sequence-point count used by the report format, when the report exposes one.
    /// </summary>
    public double? CoveredLines { get; }

    /// <summary>
    /// Gets a value indicating whether backend ITR coverage was safely reconciled with this XML coverage result.
    /// </summary>
    public bool Backfilled { get; }

    /// <summary>
    /// Gets a value indicating whether the XML file was modified on disk.
    /// </summary>
    public bool Rewritten { get; }

    /// <summary>
    /// Gets compact diagnostic text for logs.
    /// </summary>
    public string? Diagnostic { get; }
}
