// <copyright file="AsyncMutex.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Simple wrapper around TaskCompletionSource
/// </summary>
public class AsyncMutex
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Set() => _tcs.TrySetResult(true);

    public Task WaitAsync() => WaitAsync(Timeout.InfiniteTimeSpan);

    public async Task<bool> WaitAsync(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await _tcs.Task;
            return true;
        }

#if NET8_0_OR_GREATER
        await _tcs.Task.WaitAsync(timeout);
        return _tcs.Task.IsCompleted;
#else
        var delay = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(_tcs.Task, delay);

        return completedTask == _tcs.Task;
#endif
    }
}
