// <copyright file="SamplingDecision.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling;

internal readonly struct SamplingDecision
{
    /// <summary>
    /// The default sampling decision used when there is no sampler available
    /// or no sampling rules match. For example, this value is used if the tracer has not yet
    /// received any sampling rates from agent and there are no configured sampling rates.
    /// </summary>
    public static SamplingDecision Default = new(
        priority: SamplingPriorityValues.Default,
        mechanism: SamplingMechanism.Default,
        rate: null,
        limiterRate: null);

    public readonly int Priority;

    public readonly string? Mechanism;

    public readonly float? Rate;

    public readonly float? LimiterRate;

    public SamplingDecision(int priority, string? mechanism, float? rate, float? limiterRate)
    {
        Priority = priority;
        Mechanism = mechanism;
        Rate = rate;
        LimiterRate = limiterRate;
    }

    public void Deconstruct(out int priority, out string? mechanism, out float? rate, out float? limiterRate)
    {
        priority = Priority;
        mechanism = Mechanism;
        rate = Rate;
        limiterRate = LimiterRate;
    }
}
