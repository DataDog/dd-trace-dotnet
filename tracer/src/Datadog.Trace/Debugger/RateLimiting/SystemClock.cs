// <copyright file="SystemClock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Default implementation that uses system Stopwatch and DateTime
    /// </summary>
    internal sealed class SystemClock : IHighResolutionClock
    {
        public double Frequency => Stopwatch.Frequency;

        public long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }
    }
}
