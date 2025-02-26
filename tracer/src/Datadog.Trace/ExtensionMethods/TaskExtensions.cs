// <copyright file="TaskExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods;

internal static class TaskExtensions
{
    public static void SafeWait(this Task task)
    {
        if (task.IsCompleted)
        {
            return;
        }

        var originalContext = SynchronizationContext.Current;
        try
        {
            // Set the synchronization context to null to avoid deadlocks.
            SynchronizationContext.SetSynchronizationContext(null);

            // Wait synchronously for the task to complete.
            task.GetAwaiter().GetResult();
        }
        finally
        {
            // Restore the original synchronization context.
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    public static T SafeGetResult<T>(this Task<T> task)
    {
        if (task.IsCompleted)
        {
            return task.Result;
        }

        var originalContext = SynchronizationContext.Current;
        try
        {
            // Set the synchronization context to null to avoid deadlocks.
            SynchronizationContext.SetSynchronizationContext(null);

            // Wait synchronously for the task to complete.
            return task.GetAwaiter().GetResult();
        }
        finally
        {
            // Restore the original synchronization context.
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    public static bool SafeWait(this Func<Task> funcTask, int millisecondsTimeout)
    {
        var originalContext = SynchronizationContext.Current;
        using var cts = new CancellationTokenSource(millisecondsTimeout);
        try
        {
            // Set the synchronization context to null to avoid deadlocks.
            SynchronizationContext.SetSynchronizationContext(null);

            // Wait synchronously for the task to complete.
            AsyncUtil.RunSync(funcTask, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return false;
            }
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        finally
        {
            // Restore the original synchronization context.
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        return true;
    }
}
