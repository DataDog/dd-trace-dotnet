// <copyright file="CaptureBehaviour.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Defines the level of data capture for a probe execution
    /// </summary>
    internal enum CaptureBehaviour
    {
        /// <summary>
        /// Skip capture entirely (circuit open or global budget exhausted)
        /// </summary>
        Skip,

        /// <summary>
        /// Light capture: minimal data, shallow depth, short strings
        /// Used when under resource pressure
        /// </summary>
        Light,

        /// <summary>
        /// Full capture: complete snapshot with normal depth and size limits
        /// </summary>
        Full
    }
}
