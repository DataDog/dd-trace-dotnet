// <copyright file="EvaluationReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags;

/// <summary> Evaluation result reason </summary>
public enum EvaluationReason
{
    /// <summary> Default value </summary>
    DEFAULT,

    /// <summary> Static value </summary>
    STATIC,

    /// <summary> Targeting match </summary>
    TARGETING_MATCH,

    /// <summary> Split match </summary>
    SPLIT,

    /// <summary> Target disabled </summary>
    DISABLED,

    /// <summary> Cached result </summary>
    CACHED,

    /// <summary> Unknown reason </summary>
    UNKNOWN,

    /// <summary> Error </summary>
    ERROR
}
