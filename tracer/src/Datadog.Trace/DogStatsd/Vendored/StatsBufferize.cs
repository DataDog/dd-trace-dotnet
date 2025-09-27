﻿// <copyright file="StatsBufferize.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.StatsdClient.Statistic;
using Datadog.Trace.Vendors.StatsdClient.Worker;

// ReSharper disable once CheckNamespace
namespace Datadog.Trace.Vendors.StatsdClient.Bufferize
{
    /// <summary>
    /// StatsBufferize bufferizes metrics before sending them.
    /// </summary>
    internal class StatsBufferize
    {
        private readonly AsynchronousWorker<Stats> _worker;

        public StatsBufferize(
            StatsRouter statsRouter,
            int workerMaxItemCount,
            TimeSpan? blockingQueueTimeout,
            TimeSpan maxIdleWaitBeforeSending)
        {
            var handler = new WorkerHandler(statsRouter, maxIdleWaitBeforeSending);

            // `handler` (and also `statsRouter`) do not need to be thread safe as long as `workerThreadCount` is 1.
            this._worker = new AsynchronousWorker<Stats>(
                handler,
                new Waiter(),
                workerThreadCount: 1,
                workerMaxItemCount,
                blockingQueueTimeout);
        }

        public bool Send(Stats serializedMetric)
        {
            if (!this._worker.TryEnqueue(serializedMetric))
            {
                serializedMetric.Dispose();
                return false;
            }

            return true;
        }

        public void Flush()
        {
            this._worker.Flush();
        }

        public Task DisposeAsync() => this._worker.DisposeAsync();

        private class WorkerHandler : IAsynchronousWorkerHandler<Stats>
        {
            private readonly StatsRouter _statsRouter;
            private readonly TimeSpan _maxIdleWaitBeforeSending;
            private System.Diagnostics.Stopwatch _stopwatch;

            public WorkerHandler(StatsRouter statsRouter, TimeSpan maxIdleWaitBeforeSending)
            {
                _statsRouter = statsRouter;
                _maxIdleWaitBeforeSending = maxIdleWaitBeforeSending;
            }

            public void OnNewValue(Stats stats)
            {
                using (stats)
                {
                    _statsRouter.Route(stats);
                    _stopwatch = null;
                }
            }

            public bool OnIdle()
            {
                if (_stopwatch == null)
                {
                    _stopwatch = System.Diagnostics.Stopwatch.StartNew();
                }

                if (_stopwatch.ElapsedMilliseconds > _maxIdleWaitBeforeSending.TotalMilliseconds)
                {
                    this._statsRouter.OnIdle();

                    return true;
                }

                return true;
            }

            public void Flush()
            {
                this._statsRouter.Flush();
            }
        }
    }
}
