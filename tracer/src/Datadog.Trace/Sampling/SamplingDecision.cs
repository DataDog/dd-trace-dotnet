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
    public static SamplingDecision Default = new(SamplingPriorityValues.Default, SamplingMechanism.Default);

    public readonly int Priority;

    public readonly int? Mechanism;

    public SamplingDecision(int priority, int? mechanism)
    {
        Priority = priority;
        Mechanism = mechanism;
    }

    public void Deconstruct(out int priority, out int? mechanism)
    {
        priority = Priority;
        mechanism = Mechanism;
    }
}
