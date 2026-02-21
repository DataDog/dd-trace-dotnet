// <copyright file="IHighResolutionClock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal interface IHighResolutionClock
    {
        /// <summary>Gets the frequency of the timestamp (ticks per second).</summary>
        double Frequency { get; }

        /// <summary>Gets the current timestamp in ticks.</summary>
        long GetTimestamp();
    }
}
