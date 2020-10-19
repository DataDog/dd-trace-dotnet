using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private const int TraceBufferSize = 1000;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<AgentWriter>();

        private readonly AgentWriterBuffer<Span[]> _tracesBuffer = new AgentWriterBuffer<Span[]>(TraceBufferSize);
        private readonly IBatchStatsd _statsd;
        private readonly Task _flushTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        private IApi _api;

        public AgentWriter(IApi api, IBatchStatsd statsd)
            : this(api, statsd, automaticFlush: true)
        {
        }

        internal AgentWriter(IApi api, IBatchStatsd statsd, bool automaticFlush)
        {
            _api = api;
            _statsd = statsd;

            _flushTask = automaticFlush ? Task.Run(FlushTracesTaskLoopAsync) : Task.FromResult(true);
        }

        public void SetApiBaseEndpoint(Uri uri)
        {
            _api.SetBaseEndpoint(uri);
        }

        public Task<bool> Ping()
        {
            return _api.SendTracesAsync(ArrayHelper.Empty<Span[]>());
        }

        public void WriteTrace(Span[] trace)
        {
            var success = _tracesBuffer.Push(trace);

            if (!success)
            {
                Log.Debug("Trace buffer is full. Dropping a trace from the buffer.");
            }

            if (_statsd != null)
            {
                var batch = _statsd.StartBatch(initialCapacity: 2);

                batch.Append(_statsd.GetIncrementCount(TracerMetricNames.Queue.EnqueuedTraces));
                batch.Append(_statsd.GetIncrementCount(TracerMetricNames.Queue.EnqueuedSpans, trace.Length));

                if (!success)
                {
                    batch.Append(_statsd.GetIncrementCount(TracerMetricNames.Queue.DroppedTraces));
                    batch.Append(_statsd.GetIncrementCount(TracerMetricNames.Queue.DroppedSpans, trace.Length));
                }

                batch.Send();
            }
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

        public async Task FlushTracesAsync()
        {
            var traces = _tracesBuffer.Pop();

            if (_statsd != null)
            {
                var spanCount = traces.Sum(t => t.Length);

                var batch = _statsd.StartBatch(initialCapacity: 3);

                batch.Append(_statsd.GetIncrementCount(TracerMetricNames.Queue.DequeuedTraces, traces.Length));
                batch.Append(_statsd.GetIncrementCount(TracerMetricNames.Queue.DequeuedSpans, spanCount));
                batch.Append(_statsd.GetSetGauge(TracerMetricNames.Queue.MaxTraces, TraceBufferSize));
                batch.Send();
            }

            if (traces.Length > 0)
            {
                await _api.SendTracesAsync(traces).ConfigureAwait(false);
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
