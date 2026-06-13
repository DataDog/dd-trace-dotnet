// <copyright file="FlagEvaluationsRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// EVP request payload: batch-level context + list of flag evaluation events.
/// Schema: batchedflagevaluations.json (flageval-worker). Serialized with NullValueHandling.Ignore.
/// </summary>
internal sealed class FlagEvaluationsRequest
{
    /// <summary>Gets or sets the batch-level Datadog context (service/env/version).</summary>
    public FlagEvalDDContext Context { get; set; } = default!;

    /// <summary>Gets or sets the list of flag evaluation events in this batch.</summary>
    // Batch wrapper key is camelCase ("flagEvaluations") per the flageval-worker schema
    // (batchedflagevaluations.json) and the Go reference, while inner event fields stay
    // snake_case; the explicit name overrides the snake_case naming strategy for this property.
    [JsonProperty("flagEvaluations")]
    public List<FlagEvaluationEvent> FlagEvaluations { get; set; } = default!;
}
