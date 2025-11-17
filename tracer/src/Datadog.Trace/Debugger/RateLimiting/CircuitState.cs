// <copyright file="CircuitState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Represents the state of a circuit breaker
    /// </summary>
    internal enum CircuitState : int
    {
        /// <summary>
        /// Circuit is closed, requests flow normally
        /// </summary>
        Closed = 0,

        /// <summary>
        /// Circuit is open, requests are rejected
        /// </summary>
        Open = 1,

        /// <summary>
        /// Circuit is half-open, allowing limited trial requests
        /// </summary>
        HalfOpen = 2
    }
}
