// <copyright file="ICircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Circuit breaker for probe execution protection
    /// </summary>
    internal interface ICircuitBreaker
    {
        /// <summary>
        /// Gets the current state of the circuit
        /// </summary>
        CircuitState State { get; }

        /// <summary>
        /// Checks if a request should be allowed through
        /// </summary>
        /// <returns>True if request should proceed, false otherwise</returns>
        bool ShouldAllow();

        /// <summary>
        /// Records a successful execution
        /// </summary>
        /// <param name="elapsedTicks">The elapsed time in ticks</param>
        void RecordSuccess(long elapsedTicks);

        /// <summary>
        /// Records a failed execution or rejected request
        /// </summary>
        void RecordFailure();

        /// <summary>
        /// Records that the probe is in a hot loop (high frequency hits)
        /// </summary>
        void RecordHotLoop();

        /// <summary>
        /// Records that memory pressure is causing issues with this probe
        /// </summary>
        void RecordMemoryPressure();
    }
}
