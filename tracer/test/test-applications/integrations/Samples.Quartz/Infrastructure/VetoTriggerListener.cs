using Quartz;

namespace QuartzSampleApp.Infrastructure;

public class VetoTriggerListener : ITriggerListener
{
    private readonly JobKey _targetJob;
    private readonly TaskCompletionSource<bool> _tcs;

    public VetoTriggerListener(JobKey targetJob, TaskCompletionSource<bool> tcs)
    {
        _targetJob = targetJob;
        _tcs = tcs;
    }

    public string Name => "VetoTriggerListener";

    // Quartz 4x uses ValueTask instead of Task
#if QUARTZ_4_0
    public ValueTask TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Trigger fired for job: {context.JobDetail.Key}");
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Veto if this is our target job
        if (context.JobDetail.Key.Equals(_targetJob))
        {
            Console.WriteLine($"VETOING job execution for: {context.JobDetail.Key}");
            _tcs.TrySetResult(true); // Signal completion
            return ValueTask.FromResult(true); // Return true to veto
        }

        return ValueTask.FromResult(false); // Don't veto other jobs
    }

    public ValueTask TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
#else
    public ValueTask TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Trigger fired for job: {context.JobDetail.Key}");
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Veto if this is our target job
        if (context.JobDetail.Key.Equals(_targetJob))
        {
            Console.WriteLine($"VETOING job execution for: {context.JobDetail.Key}");
            _tcs.TrySetResult(true); // Signal completion
            return ValueTask.FromResult(true); // Return true to veto
        }

        return ValueTask.FromResult(false); // Don't veto other jobs
    }

    public ValueTask TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
#endif
}
