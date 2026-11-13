// <copyright file="EvaluatorParams.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class EvaluatorParams
{
    internal static readonly EvaluatorParams Invalid = new() { IsInvalid = true };

    private OptionalField<object> _maxDependencyDepth;

    public object? MaxDependencyDepth
    {
        get => _maxDependencyDepth.Value;
        set => _maxDependencyDepth = new OptionalField<object>(value);
    }

    internal OptionalField<object> MaxDependencyDepthField => _maxDependencyDepth;

    internal bool IsInvalid { get; private set; }

    public bool ShouldSerializeMaxDependencyDepth() => _maxDependencyDepth.IsPresent;
}
