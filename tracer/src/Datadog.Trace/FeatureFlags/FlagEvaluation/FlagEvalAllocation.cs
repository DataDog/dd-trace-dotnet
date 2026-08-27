// <copyright file="FlagEvalAllocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>Holds the allocation key in a flag evaluation event.</summary>
internal sealed class FlagEvalAllocation
{
    /// <summary>Gets or sets the allocation key.</summary>
    public string Key { get; set; } = default!;
}
