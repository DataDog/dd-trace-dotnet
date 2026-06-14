// <copyright file="DegradedKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Degraded-tier bucket identity: drops targeting_key and context relative to FullKey.
/// OTel <c>feature_flag.evaluations</c> cardinality. Terminal tier — overflow is drop-counted.
/// </summary>
internal readonly struct DegradedKey : IEquatable<DegradedKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DegradedKey"/> struct.
    /// </summary>
    public DegradedKey(string flagKey, string variant, string allocationKey, string reason)
    {
        FlagKey = flagKey;
        Variant = variant;
        AllocationKey = allocationKey;
        Reason = reason;
    }

    /// <summary>Gets the flag key.</summary>
    public string FlagKey { get; }

    /// <summary>Gets the variant.</summary>
    public string Variant { get; }

    /// <summary>Gets the allocation key.</summary>
    public string AllocationKey { get; }

    /// <summary>Gets the reason.</summary>
    public string Reason { get; }

    /// <inheritdoc/>
    public bool Equals(DegradedKey other) =>
        FlagKey == other.FlagKey &&
        Variant == other.Variant &&
        AllocationKey == other.AllocationKey &&
        Reason == other.Reason;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DegradedKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int h = FlagKey?.GetHashCode() ?? 0;
            h = (h * 397) ^ (Variant?.GetHashCode() ?? 0);
            h = (h * 397) ^ (AllocationKey?.GetHashCode() ?? 0);
            h = (h * 397) ^ (Reason?.GetHashCode() ?? 0);
            return h;
        }
    }
}
