using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Hangfire.Infrastructure;

public sealed class JobResult
{
    public string JobId { get; }
    public bool Succeeded { get; }
    public Exception? Error { get; }

    public JobResult(string jobId, bool succeeded, Exception? error = null)
    {
        JobId = jobId;
        Succeeded = succeeded;
        Error = error;
    }
}

public static class JobCompletion
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<JobResult>> _waiters
        = new(StringComparer.Ordinal);

    public static Task<JobResult> Register(string jobId, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<JobResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_waiters.TryAdd(jobId, tcs))
        {
            return _waiters[jobId].Task;
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                if (_waiters.TryRemove(jobId, out var w))
                {
                    w.TrySetCanceled(ct);
                }
            });
        }

        return tcs.Task;
    }

    internal static void TryComplete(string jobId, JobResult result)
    {
        if (_waiters.TryRemove(jobId, out var tcs))
        {
            if (result.Succeeded)
            {
                tcs.TrySetResult(result);
            }
            else if (result.Error is not null)
            {
                tcs.TrySetException(result.Error);
            }
            else
            {
                tcs.TrySetResult(result); // e.g., deleted without an exception
            }
        }
    }
}
