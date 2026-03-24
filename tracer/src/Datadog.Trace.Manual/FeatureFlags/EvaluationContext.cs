// <copyright file="EvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary> Standard implementation of a EvaluationContext </summary>
/// <param name="key"> Targeting Key </param>
/// <param name="values"> Context optional parameters </param>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
public sealed class EvaluationContext(string? key, IDictionary<string, object?>? values = null)
{
    /// <summary> Gets the Context Targeting Key </summary>
    public string? TargetingKey { get; } = key;

    /// <summary> Gets the Context optional Values </summary>
    public IDictionary<string, object?>? Attributes { get; } = values;
}
