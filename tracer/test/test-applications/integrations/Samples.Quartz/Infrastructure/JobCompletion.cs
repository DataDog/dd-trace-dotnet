namespace QuartzSampleApp.Infrastructure;

public static class JobCompletion
{
    public static readonly TaskCompletionSource<bool> HelloTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly TaskCompletionSource<bool> ExceptionTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly TaskCompletionSource<bool> VetoTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
