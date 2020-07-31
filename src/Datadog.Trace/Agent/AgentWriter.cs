using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private const int TraceBufferSize = 1000;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<AgentWriter>();

        private readonly AgentWriterBuffer<IReadOnlyList<Span>> _tracesBuffer = new AgentWriterBuffer<IReadOnlyList<Span>>(TraceBufferSize);
        private readonly IStatsd _statsd;
        private readonly Task _flushTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        private IApi _api;

        public AgentWriter(IApi api, IStatsd statsd)
        {
            _api = api;
            _statsd = statsd;
            _flushTask = Task.Run(FlushTracesTaskLoopAsync);
        }

        public void OverrideApi(IApi api)
        {
            _api = api;
        }

        public Task<bool> Ping()
        {
            return _api.SendTracesAsync(new Span[0][]);
        }

        public void WriteTrace(IReadOnlyList<Span> trace)
        {
            var success = _tracesBuffer.Push(trace);

            if (!success)
            {
                Log.Debug("Trace buffer is full. Dropping a trace from the buffer.");
            }

            if (_statsd != null)
            {
                _statsd.AppendIncrementCount(TracerMetricNames.Queue.EnqueuedTraces);
                _statsd.AppendIncrementCount(TracerMetricNames.Queue.EnqueuedSpans, trace.Count);

                if (!success)
                {
                    _statsd.AppendIncrementCount(TracerMetricNames.Queue.DroppedTraces);
                    _statsd.AppendIncrementCount(TracerMetricNames.Queue.DroppedSpans, trace.Count);
                }

                _statsd.Send();
            }
        }

        public Task FlushAsync()
        {
            return FlushTracesAsync();
        }

        public async Task FlushAndCloseAsync()
        {
            if (!_processExit.TrySetResult(true))
            {
                return;
            }

            await Task.WhenAny(_flushTask, Task.Delay(TimeSpan.FromSeconds(20)))
                      .ConfigureAwait(false);

            if (!_flushTask.IsCompleted)
            {
                Log.Warning("Could not flush all traces before process exit");
            }
        }

        private async Task FlushTracesAsync()
        {
            var pool = DefaultObjectPool<List<IReadOnlyList<Span>>>.Shared;
            var traces = pool.Get();
            try
            {
                _tracesBuffer.Fill(traces);

                if (_statsd != null)
                {
                    var spanCount = traces.Sum(t => t.Count);

                    _statsd.AppendIncrementCount(TracerMetricNames.Queue.DequeuedTraces, traces.Count);
                    _statsd.AppendIncrementCount(TracerMetricNames.Queue.DequeuedSpans, spanCount);
                    _statsd.AppendSetGauge(TracerMetricNames.Queue.MaxTraces, TraceBufferSize);
                    _statsd.Send();
                }

                if (traces.Count > 0)
                {
                    await _api.SendTracesAsync(traces).ConfigureAwait(false);

                    // Returns the recyclable spans to the pool
                    foreach (var trace in traces)
                    {
                        foreach (var span in trace)
                        {
                            if (span is RecyclableSpan rSpan)
                            {
                                RecyclableSpan.Return(rSpan);
                            }
                        }
                    }
                }
            }
            finally
            {
                traces.Clear();
                pool.Return(traces);
            }
        }

        private async Task FlushTracesTaskLoopAsync()
        {
            while (true)
            {
                try
                {
                    await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(1)), _processExit.Task)
                              .ConfigureAwait(false);

                    if (_processExit.Task.IsCompleted)
                    {
                        await FlushTracesAsync().ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        await FlushTracesAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Log.SafeLogError(ex, "An unhandled error occurred during the flushing task");
                }
            }
        }
    }
}
