// <copyright file="SamplingDecision.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling;

internal readonly struct SamplingDecision
{
    public readonly int Priority;

    public readonly int? Mechanism;

    public readonly double? Rate;

    public SamplingDecision(int priority, int? mechanism = null, double? rate = null)
    {
        Priority = priority;
        Mechanism = mechanism;
        Rate = rate;
    }

    public void Deconstruct(out int priority, out int? mechanism, out double? rate)
    {
        priority = Priority;
        mechanism = Mechanism;
        rate = Rate;
    }
}
