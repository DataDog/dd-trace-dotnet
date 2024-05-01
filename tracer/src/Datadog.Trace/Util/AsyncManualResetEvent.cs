// <copyright file="AsyncManualResetEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util;

// Based on: https://github.com/StephenCleary/AsyncEx/blob/master/src/Nito.AsyncEx.Coordination/AsyncManualResetEvent.cs

internal class AsyncManualResetEvent
{
    private readonly object _mutex;
    private TaskCompletionSource<bool> _tcs;

    public AsyncManualResetEvent(bool set)
    {
        _mutex = new object();
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (set)
        {
            _tcs.TrySetResult(true);
        }
    }

    public AsyncManualResetEvent()
        : this(false)
    {
    }

    public bool IsSet
    {
        get
        {
            lock (_mutex)
            {
                return _tcs.Task.IsCompleted;
            }
        }
    }

    public Task WaitAsync() => InternalWaitAsync();

    private Task<bool> InternalWaitAsync()
    {
        lock (_mutex)
        {
            return _tcs.Task;
        }
    }

    public Task<bool> WaitAsync(int millisecondTimeout)
    {
        var waitTask = InternalWaitAsync();
        return waitTask.IsCompleted ? waitTask : InternalWaitWithTimeoutAsync(waitTask, millisecondTimeout);

        static async Task<bool> InternalWaitWithTimeoutAsync(Task task, int timeout)
        {
            using var delayCancellation = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, delayCancellation.Token)).ConfigureAwait(false);
            if (completedTask == task)
            {
                delayCancellation.Cancel();
                await task.ConfigureAwait(false);
                return true;
            }

            return false;
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        var waitTask = InternalWaitAsync();
        if (waitTask.IsCompleted)
        {
            return waitTask;
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return waitTask;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

#if NET6_0_OR_GREATER
        return waitTask.WaitAsync(cancellationToken);
#else
        return DoWaitAsync(waitTask, cancellationToken);

        static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
#if NETCOREAPP2_1_OR_GREATER
            await using var reg = cancellationToken.Register(state => ((TaskCompletionSource<object?>)state!).TrySetResult(true), tcs, false).ConfigureAwait(false);
#else
            using var reg = cancellationToken.Register(state => ((TaskCompletionSource<object?>)state!).TrySetResult(true), tcs, false);
#endif
            await (await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)).ConfigureAwait(false);

            // To avoid a possible race condition we only throw the cancellation token exception if the task completed
            // is the one of the TaskCompletionSource.
            if (tcs.Task.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
#endif
    }

    public void Set()
    {
        lock (_mutex)
        {
            _tcs.TrySetResult(true);
        }
    }

    public void Reset()
    {
        lock (_mutex)
        {
            if (_tcs.Task.IsCompleted)
            {
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
