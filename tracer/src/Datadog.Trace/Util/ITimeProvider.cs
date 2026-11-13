// <copyright file="ITimeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util
{
    internal interface ITimeProvider
    {
        DateTime UtcNow { get; }

        /// <summary>
        /// Gets monotonic elapsed time since this provider was created, unaffected by wall-clock adjustments.
        /// </summary>
        TimeSpan Elapsed { get; }

        void Sleep(TimeSpan delay);

        Task Delay(TimeSpan delay, CancellationToken token = default);
    }

    internal sealed class DefaultTimeProvider : ITimeProvider
    {
        private readonly long _started = Stopwatch.GetTimestamp();

        public TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _started);

        public DateTime UtcNow => DateTime.UtcNow;

        public void Sleep(TimeSpan delay) => Thread.Sleep(delay);

        public Task Delay(TimeSpan delay, CancellationToken token = default) => Task.Delay(delay, token);
    }
}
