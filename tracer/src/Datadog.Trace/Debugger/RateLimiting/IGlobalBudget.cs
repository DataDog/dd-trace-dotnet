// <copyright file="IGlobalBudget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Tracks global CPU/time budget across all probes to prevent process saturation
    /// </summary>
    internal interface IGlobalBudget
    {
        /// <summary>
        /// Gets a value indicating whether the global budget is currently exhausted
        /// </summary>
        bool IsExhausted { get; }

        /// <summary>
        /// Records CPU time spent on a probe execution
        /// </summary>
        /// <param name="elapsedTicks">The elapsed CPU ticks</param>
        void RecordUsage(long elapsedTicks);

        /// <summary>
        /// Gets the current budget usage percentage (0-100)
        /// </summary>
        double GetUsagePercentage();

        /// <summary>
        /// Gets the number of consecutive windows where budget was exhausted
        /// </summary>
        int GetConsecutiveExhaustedWindows();
    }
}
