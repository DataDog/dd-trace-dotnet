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
        limiterRate: null,
        sample: null);

    public readonly int Priority;

    public readonly string? Mechanism;

    public readonly float? Rate;

    public readonly float? LimiterRate;

    /// <summary>
    /// The raw probability keep/drop outcome (before any rate-limiter demotion), or null
    /// when no probability mechanism made this decision (e.g. <see cref="SamplingDecision.Default"/>).
    /// Used to derive the OTel "ot.rv"/"ot.th" tracestate values in
    /// <see cref="TraceContext.SetSamplingPriority"/> — never affects <see cref="Priority"/>.
    /// </summary>
    public readonly bool? KeptByProbabilitySampling;

    public SamplingDecision(int priority, string? mechanism, float? rate, float? limiterRate, bool? sample = null)
    {
        Priority = priority;
        Mechanism = mechanism;
        Rate = rate;
        LimiterRate = limiterRate;
        KeptByProbabilitySampling = sample;
    }

    public void Deconstruct(out int priority, out string? mechanism, out float? rate, out float? limiterRate)
    {
        priority = Priority;
        mechanism = Mechanism;
        rate = Rate;
        limiterRate = LimiterRate;
    }
}
