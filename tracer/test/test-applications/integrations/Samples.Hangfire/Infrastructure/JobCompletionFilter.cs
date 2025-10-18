using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace Samples.Hangfire.Infrastructure;

public sealed class JobCompletionFilter : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        var jobId = context.BackgroundJob?.Id ?? context.JobId;

        switch (context.NewState)
        {
            case SucceededState:
                JobCompletion.TryComplete(jobId, new JobResult(jobId, succeeded: true));
                break;

            case FailedState failed:
                JobCompletion.TryComplete(jobId, new JobResult(jobId, succeeded: false, error: failed.Exception));
                break;

            case DeletedState:
                JobCompletion.TryComplete(jobId, new JobResult(jobId, succeeded: false));
                break;
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
}
