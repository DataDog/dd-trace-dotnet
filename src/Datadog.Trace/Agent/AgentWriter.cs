using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private static readonly ILog Log = LogProvider.For<AgentWriter>();

        private readonly int _maximumSpansToFlushAtOneTime = 500;
        private readonly IApi _api;
        private readonly Task _flushTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        public AgentWriter(IApi api)
        {
            _api = api;
            _flushTask = Task.Run(FlushTracesTaskLoopAsync);
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
                Log.Warn("Could not flush all traces before process exit");
            }
        }

        private async Task<IEnumerable<DatadogSpanStagingArea.FlushTask>> AttemptFlush(IEnumerable<DatadogSpanStagingArea.FlushTask> flushTasks)
        {
            await _api.SendTracesAsync(flushTasks.Select(f => f.Span).ToList()).ConfigureAwait(false);
            // TODO: Determine if re-queueing is wanted
            return null;
        }

        private async Task FlushTracesTaskLoopAsync()
        {
            const int maxPollingInterval = 30_000;
            const int defaultBetweenPolling = 1_000;
            var millisecondsBetweenPolling = defaultBetweenPolling;

            void ResetDefault()
            {
                millisecondsBetweenPolling = defaultBetweenPolling;
            }

            // Will continue our lower polling interval when items are added
            DatadogSpanStagingArea.RegisterForWakeup(ResetDefault);

            while (true)
            {
                try
                {
                    await Task.WhenAny(Task.Delay(TimeSpan.FromMilliseconds(millisecondsBetweenPolling)), _processExit.Task)
                              .ConfigureAwait(false);

                    if (DatadogSpanStagingArea.FlushTaskCount > 0)
                    {
                        await DatadogSpanStagingArea.Flush(_maximumSpansToFlushAtOneTime, AttemptFlush).ConfigureAwait(false);
                    }
                    else if (millisecondsBetweenPolling < maxPollingInterval)
                    {
                        millisecondsBetweenPolling += (int)(millisecondsBetweenPolling * .3m);
                    }

                    if (_processExit.Task.IsCompleted)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorException("An unhandled error occurred during the flushing task", ex);
                }
            }
        }
    }
}
