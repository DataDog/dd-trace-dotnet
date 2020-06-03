using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private const int TraceBufferSize = 1000;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<AgentWriter>();

        private readonly AgentWriterBuffer<Span[]> _tracesBuffer = new AgentWriterBuffer<Span[]>(TraceBufferSize);
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

        public void WriteTrace(Span[] trace)
        {
            var success = _tracesBuffer.Push(trace);

            if (!success)
            {
                Log.Debug("Trace buffer is full. Dropping a trace from the buffer.");
            }

            if (_statsd != null)
            {
                _statsd.AppendIncrementCount(TracerMetricNames.Queue.EnqueuedTraces);
                _statsd.AppendIncrementCount(TracerMetricNames.Queue.EnqueuedSpans, trace.Length);

                if (!success)
                {
                    _statsd.AppendIncrementCount(TracerMetricNames.Queue.DroppedTraces);
                    _statsd.AppendIncrementCount(TracerMetricNames.Queue.DroppedSpans, trace.Length);
                }

                _statsd.Send();
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

        private async Task FlushTracesAsync()
        {
            var traces = _tracesBuffer.Pop();

            if (_statsd != null)
            {
                var spanCount = traces.Sum(t => t.Length);

                _statsd.AppendIncrementCount(TracerMetricNames.Queue.DequeuedTraces, traces.Length);
                _statsd.AppendIncrementCount(TracerMetricNames.Queue.DequeuedSpans, spanCount);
                _statsd.AppendSetGauge(TracerMetricNames.Queue.MaxTraces, TraceBufferSize);
                _statsd.Send();
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
