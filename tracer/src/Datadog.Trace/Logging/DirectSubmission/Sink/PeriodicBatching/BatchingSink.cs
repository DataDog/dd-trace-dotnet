// <copyright file="BatchingSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/src/Serilog.Sinks.PeriodicBatching/Sinks/PeriodicBatching/PeriodicBatchingSink.cs
// Copyright 2013-2020 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching
{
    internal abstract class BatchingSink : IDisposable
    {
        private readonly IDatadogLogger _logger = DatadogLogging.GetLoggerFor<BatchingSink>();
        private readonly int _batchSizeLimit;
        private readonly BoundedConcurrentQueue<DatadogLogEvent> _queue;
        private readonly BatchedConnectionStatus _status;
        private readonly PortableTimer _timer;
        private readonly object _stateLock = new();
        private readonly Queue<DatadogLogEvent> _waitingBatch = new();

        private bool _unloading;
        private bool _started;

        protected BatchingSink(BatchingSinkOptions sinkOptions)
        {
            if (sinkOptions == null)
            {
                throw new ArgumentNullException(nameof(sinkOptions));
            }

            if (sinkOptions.BatchSizeLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sinkOptions), "The batch size limit must be greater than zero.");
            }

            if (sinkOptions.Period <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(sinkOptions), "The period must be greater than zero.");
            }

            _batchSizeLimit = sinkOptions.BatchSizeLimit;
            _queue = new BoundedConcurrentQueue<DatadogLogEvent>(sinkOptions.QueueLimit);
            _status = new BatchedConnectionStatus(sinkOptions.Period);
            _timer = new PortableTimer(_ => OnTick());
        }

        public void Dispose() => Dispose(true);

        /// <summary>
        /// Emit the provided log event to the sink. If the sink is being disposed or
        /// the app domain unloaded, then the event is ignored.
        /// </summary>
        /// <param name="logEvent">Log event to emit.</param>
        /// <exception cref="ArgumentNullException">The event is null.</exception>
        public void EnqueueLog(DatadogLogEvent logEvent)
        {
            if (logEvent == null)
            {
                throw new ArgumentNullException(nameof(logEvent));
            }

            if (_unloading)
            {
                return;
            }

            if (!_started)
            {
                lock (_stateLock)
                {
                    if (_unloading)
                    {
                        return;
                    }

                    if (!_started)
                    {
                        _queue.TryEnqueue(logEvent);
                        _started = true;

                        // Special handling to try to get the first event across as quickly
                        // as possible to show we're alive!
                        SetTimer(TimeSpan.Zero);

                        return;
                    }
                }
            }

            _queue.TryEnqueue(logEvent);
        }

        private static void ResetSyncContextAndWait(Func<Task> taskFactory)
        {
            var prevContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                taskFactory().Wait();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        /// <summary>
        /// Emit a batch of log events, running to completion synchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        protected abstract Task EmitBatch(Queue<DatadogLogEvent> events);

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        /// <param name="disposing">If true, called because the object is being disposed; if false,
        /// the object is being disposed from the finalizer.</param>
        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            CloseAndFlush();
        }

        private async Task OnTick()
        {
            try
            {
                bool batchWasFull;
                do
                {
                    while (_waitingBatch.Count < _batchSizeLimit &&
                           _queue.TryDequeue(out var next))
                    {
                        _waitingBatch.Enqueue(next);
                    }

                    if (_waitingBatch.Count == 0)
                    {
                        return;
                    }

                    await EmitBatch(_waitingBatch).ConfigureAwait(false);

                    batchWasFull = _waitingBatch.Count >= _batchSizeLimit;
                    _waitingBatch.Clear();
                    _status.MarkSuccess();
                }
                while (batchWasFull); // Otherwise, allow the period to elapse
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception while emitting periodic batch");
                _status.MarkFailure();
            }
            finally
            {
                if (_status.ShouldDropBatch)
                {
                    _waitingBatch.Clear();
                }

                if (_status.ShouldDropQueue)
                {
                    while (_queue.TryDequeue(out _)) { }
                }

                lock (_stateLock)
                {
                    if (!_unloading)
                    {
                        SetTimer(_status.NextInterval);
                    }
                }
            }
        }

        private void SetTimer(TimeSpan interval)
        {
            _timer.Start(interval);
        }

        private void CloseAndFlush()
        {
            lock (_stateLock)
            {
                if (!_started || _unloading)
                {
                    return;
                }

                _unloading = true;
            }

            _timer.Dispose();

            // This is the place where SynchronizationContext.Current is unknown and can be != null
            // so we prevent possible deadlocks here for sync-over-async downstream implementations
            ResetSyncContextAndWait(OnTick);

            // Dispose anything used in child implementations
            AdditionalDispose();
        }

        protected abstract void AdditionalDispose();
    }
}
