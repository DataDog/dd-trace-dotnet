// <copyright file="EvaluatorParams.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class EvaluatorParams
{
    private object? _maxDependencyDepth;

    public object? MaxDependencyDepth
    {
        get => _maxDependencyDepth;
        set
        {
            MaxDependencyDepthPresent = true;
            _maxDependencyDepth = value;
        }
    }

    internal bool MaxDependencyDepthPresent { get; private set; }
}
