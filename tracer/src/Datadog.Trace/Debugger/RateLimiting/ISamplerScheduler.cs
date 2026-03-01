// <copyright file="ISamplerScheduler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Shared scheduler for sampler window rolls to reduce timer overhead
    /// </summary>
    internal interface ISamplerScheduler
    {
        /// <summary>
        /// Schedules a callback to be invoked at regular intervals
        /// </summary>
        /// <param name="callback">The callback to invoke</param>
        /// <param name="interval">The interval at which to invoke the callback</param>
        /// <returns>A token that can be used to unschedule the callback</returns>
        IDisposable Schedule(Action callback, TimeSpan interval);
    }
}
