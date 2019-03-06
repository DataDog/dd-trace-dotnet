using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private static readonly ILog _log = LogProvider.For<AgentWriter>();

        private readonly AgentWriterBuffer<List<Span>> _tracesBuffer = new AgentWriterBuffer<List<Span>>(1000);
        private readonly IApi _api;
        private readonly Task _flushTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        public AgentWriter(IApi api)
        {
            _api = api;
            _flushTask = Task.Run(FlushTracesTaskLoopAsync);
        }

        public void WriteTrace(List<Span> trace)
        {
            var success = _tracesBuffer.Push(trace);
            if (!success)
            {
                _log.Debug("Trace buffer is full, dropping it.");
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
                _log.Warn("Could not flush all traces before process exit");
            }
        }

        private async Task FlushTracesAsync()
        {
            var traces = _tracesBuffer.Pop();
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
                    _log.ErrorException("An unhandled error occurred during the flushing task", ex);
                }
            }
        }
    }
}
