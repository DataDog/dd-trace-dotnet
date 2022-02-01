// <copyright file="SamplingPriorityValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

internal static class SamplingPriorityValues
{
    /// <summary>
    /// Trace should be dropped (not sampled).
    /// Sampling decision made explicitly by user through
    /// code or configuration (e.g. the rules sampler).
    /// </summary>
    public const int UserReject = -1;

    /// <summary>
    /// Trace should be dropped (not sampled).
    /// Sampling decision made by the built-in sampler.
    /// </summary>
    public const int AutoReject = 0;

    /// <summary>
    /// Trace should be kept (sampled).
    /// Sampling decision made by the built-in sampler.
    /// </summary>
    public const int AutoKeep = 1;

    /// <summary>
    /// Trace should be kept (sampled).
    /// Sampling decision made explicitly by user through
    /// code or configuration (e.g. the rules sampler).
    /// </summary>
    public const int UserKeep = 2;
}
