// <copyright file="TriggerSamplingDecision.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Sampling;

internal enum TriggerSamplingDecision
{
    /// <summary>
    /// Do not trigger a sampling decision.
    /// </summary>
    None,

    /// <summary>
    /// Trigger a sampling decision only if the sampling priority is not set.
    /// </summary>
    IfNotSet,

    /// <summary>
    /// Always trigger a sampling decision.
    /// </summary>
    Always
}
