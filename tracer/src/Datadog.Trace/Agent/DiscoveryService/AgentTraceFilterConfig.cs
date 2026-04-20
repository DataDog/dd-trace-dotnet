// <copyright file="AgentTraceFilterConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Agent.DiscoveryService;

/// <summary>
/// Trace-level filtering configuration received from the agent's /info endpoint.
/// Filters are applied to the root span before stats computation.
/// </summary>
internal sealed record AgentTraceFilterConfig(
    List<string>? FilterTagsRequire,
    List<string>? FilterTagsReject,
    List<string>? FilterTagsRegexRequire,
    List<string>? FilterTagsRegexReject,
    List<string>? IgnoreResources)
{
    public static readonly AgentTraceFilterConfig Empty = new(null, null, null, null, null);

    public bool HasFilters =>
        FilterTagsRequire is { Count: > 0 } ||
        FilterTagsReject is { Count: > 0 } ||
        FilterTagsRegexRequire is { Count: > 0 } ||
        FilterTagsRegexReject is { Count: > 0 } ||
        IgnoreResources is { Count: > 0 };
}
