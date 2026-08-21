// <copyright file="MetricsTelemetryCollectorBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry;

internal abstract partial class MetricsTelemetryCollectorBase
{
    private readonly TimeSpan _aggregationInterval;
    private readonly Action? _aggregationNotification;
    private readonly Task _aggregateTask;
    private readonly TaskCompletionSource<bool> _processExit = new();

    protected MetricsTelemetryCollectorBase()
        : this(TimeSpan.FromSeconds(10))
    {
    }

    protected MetricsTelemetryCollectorBase(TimeSpan aggregationInterval, Action? aggregationNotification = null)
    {
        _aggregationInterval = aggregationInterval;
        _aggregationNotification = aggregationNotification;
        _aggregateTask = Task.Run(AggregateMetricsLoopAsync);
        _aggregateTask
           .ContinueWith(
                t =>
                {
                    // There's a complex relationship between metrics and logs initialization, so we don't log anything in this case
                },
                TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    [TestingAndPrivateOnly]
    internal abstract void AggregateMetrics();

    internal abstract void Clear();

    public Task DisposeAsync()
    {
        _processExit.TrySetResult(true);
        return _aggregateTask;
    }

    private async Task AggregateMetricsLoopAsync()
    {
        var tasks = new Task[2];
        tasks[0] = _processExit.Task;
        while (true)
        {
            tasks[1] = Task.Delay(_aggregationInterval);
            await Task.WhenAny(tasks).ConfigureAwait(false);

            // The process may have exited, but we want to do a final aggregation before process end anyway
            AggregateMetrics();
            _aggregationNotification?.Invoke();

            if (_processExit.Task.IsCompleted)
            {
                return;
            }
        }
    }
}
