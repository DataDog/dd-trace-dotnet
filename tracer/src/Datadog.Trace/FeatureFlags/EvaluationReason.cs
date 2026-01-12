// <copyright file="EvaluationReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary> Evaluation result reason </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
public enum EvaluationReason
{
    /// <summary> Default value </summary>
    Default,

    /// <summary> Static value </summary>
    Static,

    /// <summary> Targeting match </summary>
    TargetingMatch,

    /// <summary> Split match </summary>
    Split,

    /// <summary> Target disabled </summary>
    Disabled,

    /// <summary> Cached result </summary>
    Cached,

    /// <summary> Unknown reason </summary>
    Unknown,

    /// <summary> Error </summary>
    Error
}
