// <copyright file="AsyncManualResetEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util;

// Stripped down version of: https://github.com/StephenCleary/AsyncEx/blob/master/src/Nito.AsyncEx.Coordination/AsyncManualResetEvent.cs

internal class AsyncManualResetEvent
{
    private readonly object _mutex;
    private TaskCompletionSource<object?> _tcs;

    public AsyncManualResetEvent(bool set)
    {
        _mutex = new object();
        _tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (set)
        {
            _tcs.TrySetResult(null);
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

    public Task WaitAsync()
    {
        lock (_mutex)
        {
            return _tcs.Task;
        }
    }

    public Task WaitAsync(int millisecondTimeout)
    {
        var waitTask = WaitAsync();
        if (waitTask.IsCompleted)
        {
            return waitTask;
        }

        return InternalWaitAsync();

        async Task InternalWaitAsync()
        {
            var delayCancellation = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(waitTask, Task.Delay(millisecondTimeout, delayCancellation.Token)).ConfigureAwait(false);
            if (completedTask == waitTask)
            {
                delayCancellation.Cancel();
            }
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        var waitTask = WaitAsync();
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

        return DoWaitAsync(waitTask, cancellationToken);

        static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object?>();
#if NETCOREAPP2_1_OR_GREATER
            await using var reg = cancellationToken.Register(state => ((TaskCompletionSource<object?>)state!).TrySetResult(true), tcs, false).ConfigureAwait(false);
#else
            using var reg = cancellationToken.Register(state => ((TaskCompletionSource<object?>)state!).TrySetResult(true), tcs, false);
#endif
            await (await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }

    public void Set()
    {
        lock (_mutex)
        {
            _tcs.TrySetResult(null);
        }
    }

    public void Reset()
    {
        lock (_mutex)
        {
            if (_tcs.Task.IsCompleted)
            {
                _tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
