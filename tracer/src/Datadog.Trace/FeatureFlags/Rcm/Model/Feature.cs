// <copyright file="Feature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

/// <summary>
/// A feature attached to a split, routed to one or more destinations. Only constructed by
/// <see cref="FeatureListJsonConverter"/>, which drops any entry whose key, value or destinations
/// do not match the contract.
/// </summary>
internal sealed class Feature(string key, object value, List<string> destinations)
{
    public string Key { get; } = key;

    /// <summary> Gets the feature value: a <see cref="string"/>, <see cref="double"/> or <see cref="bool"/>; nothing else survives parsing. </summary>
    public object Value { get; } = value;

    public List<string> Destinations { get; } = destinations;
}
