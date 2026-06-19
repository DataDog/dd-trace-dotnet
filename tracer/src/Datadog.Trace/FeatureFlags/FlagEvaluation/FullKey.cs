// <copyright file="FullKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Full-tier bucket identity: all dims plus a comparable canonical-context key.
/// No hash — distinct contexts always produce distinct buckets (no digest, so no collisions).
/// </summary>
internal readonly struct FullKey : IEquatable<FullKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullKey"/> struct.
    /// </summary>
    public FullKey(string flagKey, string variant, string allocationKey, string errorMessage, bool runtimeDefault, string targetingKey, string contextKey)
    {
        FlagKey = flagKey;
        Variant = variant;
        AllocationKey = allocationKey;
        ErrorMessage = errorMessage;
        RuntimeDefault = runtimeDefault;
        TargetingKey = targetingKey;
        ContextKey = contextKey;
    }

    /// <summary>Gets the flag key.</summary>
    public string FlagKey { get; }

    /// <summary>Gets the variant.</summary>
    public string Variant { get; }

    /// <summary>Gets the allocation key.</summary>
    public string AllocationKey { get; }

    /// <summary>Gets the schema-visible error message.</summary>
    public string ErrorMessage { get; }

    /// <summary>Gets a value indicating whether the runtime default was used.</summary>
    public bool RuntimeDefault { get; }

    /// <summary>Gets the targeting key.</summary>
    public string TargetingKey { get; }

    /// <summary>Gets the exact canonical encoding of the pruned context — comparable, not a digest.</summary>
    public string ContextKey { get; }

    /// <inheritdoc/>
    public bool Equals(FullKey other) =>
        FlagKey == other.FlagKey &&
        Variant == other.Variant &&
        AllocationKey == other.AllocationKey &&
        ErrorMessage == other.ErrorMessage &&
        RuntimeDefault == other.RuntimeDefault &&
        TargetingKey == other.TargetingKey &&
        ContextKey == other.ContextKey;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FullKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int h = FlagKey?.GetHashCode() ?? 0;
            h = (h * 397) ^ (Variant?.GetHashCode() ?? 0);
            h = (h * 397) ^ (AllocationKey?.GetHashCode() ?? 0);
            h = (h * 397) ^ (ErrorMessage?.GetHashCode() ?? 0);
            h = (h * 397) ^ RuntimeDefault.GetHashCode();
            h = (h * 397) ^ (TargetingKey?.GetHashCode() ?? 0);
            h = (h * 397) ^ (ContextKey?.GetHashCode() ?? 0);
            return h;
        }
    }
}
