// <copyright file="SessionCodeCoverageMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Ipc.Messages;

internal sealed class SessionCodeCoverageMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCodeCoverageMessage"/> class for IPC deserialization.
    /// </summary>
    public SessionCodeCoverageMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCodeCoverageMessage"/> class with a legacy source.
    /// </summary>
    /// <param name="value">Line coverage percentage.</param>
    public SessionCodeCoverageMessage(double value)
    {
        Source = CodeCoverageReportSource.Unknown;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCodeCoverageMessage"/> class.
    /// </summary>
    /// <param name="source">Coverage source that produced the percentage.</param>
    /// <param name="value">Line coverage percentage.</param>
    /// <param name="backfilled">Whether backend ITR coverage was used to compute the result.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="resultId">Stable result identity used to deduplicate IPC delivery and persisted fallback copies of the same producer result.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    /// <param name="supersededResultIds">Stable identities of partial producer results represented by this merged result.</param>
    public SessionCodeCoverageMessage(CodeCoverageReportSource source, double value, bool backfilled, double? executableLines = null, double? coveredLines = null, string? diagnostic = null, string? resultId = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null, string[]? supersededResultIds = null)
    {
        Source = source;
        Value = value;
        Backfilled = backfilled;
        ExecutableLines = executableLines;
        CoveredLines = coveredLines;
        Diagnostic = diagnostic;
        ResultId = resultId;
        BackfillValidated = backfillValidated;
        BackfillNotApplicable = backfillNotApplicable;
        BackfillValidation = backfillValidation;
        SupersededResultIds = supersededResultIds;
    }

    /// <summary>
    /// Gets or sets the coverage source that produced the percentage.
    /// </summary>
    [JsonProperty("source")]
    public CodeCoverageReportSource Source { get; set; }

    /// <summary>
    /// Gets or sets the line coverage percentage.
    /// </summary>
    [JsonProperty("value")]
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether backend ITR coverage was used to compute the result.
    /// </summary>
    [JsonProperty("backfilled")]
    public bool Backfilled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.
    /// </summary>
    [JsonProperty("backfill_validated")]
    public bool BackfillValidated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether backend ITR coverage was evaluated and did not apply to this producer result.
    /// </summary>
    [JsonProperty("backfill_not_applicable")]
    public bool BackfillNotApplicable { get; set; }

    /// <summary>
    /// Gets or sets backend ITR coverage validation data that can be merged with other same-source results.
    /// </summary>
    [JsonProperty("backfill_validation")]
    public CodeCoverageBackfillValidation? BackfillValidation { get; set; }

    /// <summary>
    /// Gets or sets the executable-line count, when available.
    /// </summary>
    [JsonProperty("executable_lines")]
    public double? ExecutableLines { get; set; }

    /// <summary>
    /// Gets or sets the covered-line count, when available.
    /// </summary>
    [JsonProperty("covered_lines")]
    public double? CoveredLines { get; set; }

    /// <summary>
    /// Gets or sets compact diagnostic text, when available.
    /// </summary>
    [JsonProperty("diagnostic")]
    public string? Diagnostic { get; set; }

    /// <summary>
    /// Gets or sets the stable result identity used to deduplicate IPC delivery and persisted fallback copies of the same producer result.
    /// </summary>
    [JsonProperty("result_id")]
    public string? ResultId { get; set; }

    /// <summary>
    /// Gets or sets stable identities of partial producer results represented by this merged result.
    /// </summary>
    [JsonProperty("superseded_result_ids")]
    public string[]? SupersededResultIds { get; set; }
}
