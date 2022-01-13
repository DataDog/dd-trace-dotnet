// <copyright file="BatchingSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching
{
    internal abstract class BatchingSink : IDisposable
    {
        internal const int FailuresBeforeCircuitBreak = 10;

        private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor<BatchingSink>();
        private readonly int _batchSizeLimit;
        private readonly TimeSpan _flushPeriod;
        private readonly TimeSpan _circuitBreakPeriod;
        private readonly BoundedConcurrentQueue<DatadogLogEvent> _queue;
        private readonly Queue<DatadogLogEvent> _waitingBatch = new();
        private readonly CircuitBreaker _circuitBreaker;
        private readonly Task _flushTask;
        private readonly Action? _disableSinkAction;
        private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _enqueueLogEnabled = true;

        protected BatchingSink(BatchingSinkOptions sinkOptions, Action? disableSinkAction)
        {
            if (sinkOptions == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(sinkOptions));
            }

            if (sinkOptions.BatchSizeLimit <= 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(sinkOptions), "The batch size limit must be greater than zero.");
            }

            if (sinkOptions.Period <= TimeSpan.Zero)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(sinkOptions), "The period must be greater than zero.");
            }

            _disableSinkAction = disableSinkAction;

            _batchSizeLimit = sinkOptions.BatchSizeLimit;
            _flushPeriod = sinkOptions.Period;
            _circuitBreakPeriod = sinkOptions.CircuitBreakPeriod;

            _queue = new BoundedConcurrentQueue<DatadogLogEvent>(sinkOptions.QueueLimit);
            _circuitBreaker = new CircuitBreaker(FailuresBeforeCircuitBreak);

            _flushTask = Task.Run(FlushBuffersTaskLoopAsync);
            _flushTask.ContinueWith(
                t =>
                {
                    _log.Error(t.Exception, "Error in flush task");
                    _disableSinkAction?.Invoke();
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

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
                ThrowHelper.ThrowArgumentNullException(nameof(logEvent));
            }

            if (_processExit.Task.IsCompleted || !_enqueueLogEnabled)
            {
                return;
            }

            _queue.TryEnqueue(logEvent);
        }

        public virtual void Dispose()
        {
            Dispose(finalFlush: true);
        }

        public void Dispose(bool finalFlush)
        {
            _processExit.TrySetResult(finalFlush);
            _flushTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Emit a batch of log events, running to completion synchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <returns><c>true</c> if the batch was emitted successfully. <c>false</c> if there was an error</returns>
        protected abstract Task<bool> EmitBatch(Queue<DatadogLogEvent> events);

        private async Task FlushBuffersTaskLoopAsync()
        {
            while (!_processExit.Task.IsCompleted)
            {
                var circuitStatus = await FlushLogs().ConfigureAwait(false);

                await HandleCircuitStatus(circuitStatus).ConfigureAwait(false);
            }

            _log.Debug("Terminating Log submission loop");
            if (_processExit.Task.Result)
            {
                var maxShutDownDelay = Task.Delay(20_000);
                var finalFlushTask = FlushLogs();
                var completed = await Task.WhenAny(finalFlushTask, maxShutDownDelay).ConfigureAwait(false);

                if (completed != finalFlushTask)
                {
                    _log.Warning("Could not finish flushing all logs before process end");
                }
            }
        }

        private async Task<CircuitStatus> FlushLogs()
        {
            try
            {
                var status = CircuitStatus.Closed;
                var haveMultipleBatchesToSend = false;
                do
                {
                    while (_waitingBatch.Count < _batchSizeLimit &&
                           _queue.TryDequeue(out var next))
                    {
                        _waitingBatch.Enqueue(next);
                    }

                    if (_waitingBatch.Count == 0)
                    {
                        // If the first batch was full, then use that status.
                        // If this is the first batch in this loop, then there were no logs to send
                        // So can't say anything about the status of the API.
                        return haveMultipleBatchesToSend ? status : _circuitBreaker.MarkSkipped();
                    }

                    var success = await EmitBatch(_waitingBatch).ConfigureAwait(false);
                    if (success)
                    {
                        status = _circuitBreaker.MarkSuccess();
                        haveMultipleBatchesToSend = _waitingBatch.Count >= _batchSizeLimit;
                        if (haveMultipleBatchesToSend)
                        {
                            _waitingBatch.Clear();
                        }
                    }
                    else
                    {
                        return _circuitBreaker.MarkFailure();
                    }
                }
                while (haveMultipleBatchesToSend);

                return status;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception while emitting periodic batch");
                return _circuitBreaker.MarkFailure();
            }
            finally
            {
                _waitingBatch.Clear();
            }
        }

        private Task HandleCircuitStatus(CircuitStatus status)
        {
            var delayTillNextEmit = _flushPeriod;
            switch (status)
            {
                case CircuitStatus.PermanentlyBroken:
                    _processExit.TrySetResult(false);
                    _enqueueLogEnabled = false;
                    _disableSinkAction?.Invoke();

                    // clear the queue
                    while (_queue.TryDequeue(out _)) { }

                    return _processExit.Task;

                case CircuitStatus.Broken:
                    // circuit breaker is broken, so stop queuing more logs
                    // for now but don't disable log shipping entirely
                    _enqueueLogEnabled = false;
                    // Wait a while before trying again
                    delayTillNextEmit = _circuitBreakPeriod;
                    break;

                case CircuitStatus.HalfBroken:
                    // circuit breaker is tentatively open. Start queuing logs again
                    _enqueueLogEnabled = true;
                    break;

                case CircuitStatus.Closed:
                default:
                    _enqueueLogEnabled = true;
                    break;
            }

            return Task.WhenAny(
                Task.Delay(delayTillNextEmit),
                _processExit.Task);
        }
    }
}
