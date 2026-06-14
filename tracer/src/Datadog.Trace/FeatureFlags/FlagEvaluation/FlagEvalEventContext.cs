// <copyright file="FlagEvalEventContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>Holds per-event evaluation context attributes (full tier only).</summary>
internal sealed class FlagEvalEventContext
{
    /// <summary>Gets or sets the evaluation context attributes map.</summary>
    public Dictionary<string, object?>? Evaluation { get; set; }
}
