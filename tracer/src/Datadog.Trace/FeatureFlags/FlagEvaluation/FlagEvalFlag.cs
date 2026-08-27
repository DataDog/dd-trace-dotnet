// <copyright file="FlagEvalFlag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>Holds the flag key reference in a flag evaluation event.</summary>
internal sealed class FlagEvalFlag
{
    /// <summary>Gets or sets the flag key.</summary>
    public string Key { get; set; } = default!;
}
