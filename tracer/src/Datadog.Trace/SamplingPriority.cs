// <copyright file="SamplingPriority.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// Sampling "priorities" indicate whether a trace should be kept (sampled) or dropped (not sampled).
    /// Trace statistics are computed based on all traces, even if they are sampled out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Currently, all traces are still sent to the Agent (for stats computation, etc),
    /// but this will change in future versions of the tracer if the Agent supports it.
    /// </para>
    /// <para>
    /// Despite the name, there is no relative priority between the different values.
    /// All the "keep" and "reject" values have the same weight, they only indicate where
    /// the decision originated from.
    /// </para>
    /// </remarks>
    public enum SamplingPriority
    {
        /// <summary>
        /// Trace should be dropped (not sampled) due to a user request through code or configuration (e.g. the rules sampler).
        /// </summary>
        UserReject = -1,

        /// <summary>
        /// Trace should be dropped (not sampled) due according to the built-in sampler.
        /// </summary>
        AutoReject = 0,

        /// <summary>
        /// Trace should be kept (sampled) due according to the built-in sampler.
        /// </summary>
        AutoKeep = 1,

        /// <summary>
        /// Trace should be kept (sampled) due to a user request through code or configuration (e.g. the rules sampler).
        /// </summary>
        UserKeep = 2,

        /// <summary>
        /// Trace should be kept (sampled) due to an application security event.
        /// </summary>
        AppSecKeep = 4,
    }
}
