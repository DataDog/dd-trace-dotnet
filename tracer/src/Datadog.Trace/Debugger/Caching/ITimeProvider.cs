// <copyright file="ITimeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Caching
{
    internal interface ITimeProvider
    {
        DateTime UtcNow { get; }

        Task Delay(TimeSpan delay, CancellationToken token = default);
    }

    internal sealed class DefaultTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public Task Delay(TimeSpan delay, CancellationToken token = default) => Task.Delay(delay, token);
    }
}
