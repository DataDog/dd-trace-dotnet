using Quartz;

namespace QuartzSampleApp.Infrastructure;

public sealed class FinalJobListener : IJobListener
{
    private readonly JobKey _target;
    private readonly TaskCompletionSource<bool> _tcs;

    public FinalJobListener(JobKey target, TaskCompletionSource<bool> tcs)
    {
        _target = target;
        _tcs = tcs;
    }

    public string Name => $"FinalJobListener({_target})";

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken ct = default)
    {
        // Only care about the specific job we're waiting on
        if (!context.JobDetail.Key.Equals(_target))
            return Task.CompletedTask;

        // If Quartz plans to refire immediately, this run isn't "final" yet.
        if (jobException?.RefireImmediately == true)
            return Task.CompletedTask;

        // Final success or failure -> signal completion
        _tcs.TrySetResult(true);
        return Task.CompletedTask;
    }
}
