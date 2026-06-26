// <copyright file="FlagEvalDDContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Batch-level Datadog context (service/env/version).
/// </summary>
internal sealed class FlagEvalDDContext
{
    /// <summary>Gets or sets the service name.</summary>
    public string Service { get; set; } = default!;

    /// <summary>Gets or sets the environment.</summary>
    public string? Env { get; set; }

    /// <summary>Gets or sets the service version.</summary>
    public string? Version { get; set; }
}
