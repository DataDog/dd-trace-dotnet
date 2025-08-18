namespace QuartzSampleApp.Infrastructure;

public static class JobCompletion
{
    public static readonly TaskCompletionSource<bool> Tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
