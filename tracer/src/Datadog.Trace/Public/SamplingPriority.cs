// <copyright file="SamplingPriority.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// Sampling "priorities" indicate whether a trace should be kept (sampled) or dropped (not sampled).
    /// Trace statistics are computed based on all traces, even if they are dropped
    /// </summary>
    /// <remarks>
    /// <para>
    /// Currently, all traces are still sent to the Agent (for stats computation, etc),
    /// but this may change in future versions of the tracer.
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
        /// Trace should be dropped (not sampled).
        /// Sampling decision made explicitly by user through
        /// code or configuration (e.g. the rules sampler).
        /// </summary>
        UserReject = SamplingPriorityValues.UserReject,

        /// <summary>
        /// Trace should be dropped (not sampled).
        /// Sampling decision made by the built-in sampler.
        /// </summary>
        AutoReject = SamplingPriorityValues.AutoReject,

        /// <summary>
        /// Trace should be kept (sampled).
        /// Sampling decision made by the built-in sampler.
        /// </summary>
        AutoKeep = SamplingPriorityValues.AutoKeep,

        /// <summary>
        /// Trace should be kept (sampled).
        /// Sampling decision made explicitly by user through
        /// code or configuration (e.g. the rules sampler).
        /// </summary>
        UserKeep = SamplingPriorityValues.UserKeep,
    }
}
