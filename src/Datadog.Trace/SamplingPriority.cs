// <copyright file="SamplingPriority.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// A traces sampling priority determines whether is should be kept and stored.
    /// </summary>
    public enum SamplingPriority
    {
        /// <summary>
        /// Explicitly ask the backend to not store a trace.
        /// </summary>
        UserReject = -1,

        /// <summary>
        /// Used by the built-in sampler to inform the backend that a trace should be rejected and not stored.
        /// </summary>
        AutoReject = 0,

        /// <summary>
        /// Used by the built-in sampler to inform the backend that a trace should be kept and stored.
        /// </summary>
        AutoKeep = 1,

        /// <summary>
        /// Explicitly ask the backend to keep a trace.
        /// </summary>
        UserKeep = 2
    }
}
