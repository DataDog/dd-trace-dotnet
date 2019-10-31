using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsD;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private const int TraceBufferSize = 1000;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<AgentWriter>();

        private readonly AgentWriterBuffer<List<Span>> _tracesBuffer = new AgentWriterBuffer<List<Span>>(TraceBufferSize);
        private readonly IApi _api;
        private readonly IDogStatsd _dogStatsdClient;
        private readonly Task _flushTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        public AgentWriter(IApi api, IDogStatsd dogStatsdClient)
        {
            _api = api;
            _dogStatsdClient = dogStatsdClient;
            _flushTask = Task.Run(FlushTracesTaskLoopAsync);
        }

        public void WriteTrace(List<Span> trace)
        {
            var success = _tracesBuffer.Push(trace);

            _dogStatsdClient?.Increment(TracerMetricNames.Queue.PushedTraces);
            _dogStatsdClient?.Increment(TracerMetricNames.Queue.PushedSpans, trace.Count);

            if (!success)
            {
                _dogStatsdClient?.Increment(TracerMetricNames.Queue.DroppedTraces);
                Log.Debug("Trace buffer is full, dropping it.");
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
            var spanCount = traces.Sum(t => t.Count);

            _dogStatsdClient?.Gauge(TracerMetricNames.Queue.PoppedTraces, traces.Count);
            _dogStatsdClient?.Gauge(TracerMetricNames.Queue.PoppedSpans, spanCount);
            _dogStatsdClient?.Gauge(TracerMetricNames.Queue.BufferedTracesLimit, TraceBufferSize);

            if (traces.Any())
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
                    Log.Error(ex, "An unhandled error occurred during the flushing task");
                }
            }
        }
    }
}
