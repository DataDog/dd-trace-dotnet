using System;
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
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<AgentWriter>();

        private readonly AgentWriterBuffer<Span[]> _tracesBuffer;
        private readonly IDogStatsd _statsd;
        private readonly Task _flushTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        private readonly IApi _api;

        public AgentWriter(IApi api, IDogStatsd statsd, bool automaticFlush = true, int queueSize = 1000)
        {
            _tracesBuffer = new AgentWriterBuffer<Span[]>(queueSize);
            _api = api;
            _statsd = statsd;

            _flushTask = automaticFlush ? Task.Run(FlushTracesTaskLoopAsync) : Task.FromResult(true);
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
                _statsd.Increment(TracerMetricNames.Queue.EnqueuedTraces);
                _statsd.Increment(TracerMetricNames.Queue.EnqueuedSpans, trace.Length);

                if (!success)
                {
                    _statsd.Increment(TracerMetricNames.Queue.DroppedTraces);
                    _statsd.Increment(TracerMetricNames.Queue.DroppedSpans, trace.Length);
                }
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

                _statsd.Increment(TracerMetricNames.Queue.DequeuedTraces, traces.Length);
                _statsd.Increment(TracerMetricNames.Queue.DequeuedSpans, spanCount);
                _statsd.Gauge(TracerMetricNames.Queue.MaxTraces, _tracesBuffer.MaxSize);
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
