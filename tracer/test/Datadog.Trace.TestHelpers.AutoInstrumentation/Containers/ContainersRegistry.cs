// <copyright file="ContainersRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public static class ContainersRegistry
{
    private static readonly ConcurrentDictionary<Type, Task<IReadOnlyDictionary<string, object>>> Resources = new();
    private static readonly ConcurrentDictionary<Type, int> ReferenceCounts = new();
    private static readonly object LockObject = new();

    public static async Task<IReadOnlyDictionary<string, object>> GetOrAdd(Type type, Func<Task<IReadOnlyDictionary<string, object>>> createResources)
    {
        if (!Resources.TryGetValue(type, out var task))
        {
            var tcs = new TaskCompletionSource<IReadOnlyDictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);

            task = Resources.GetOrAdd(type, tcs.Task);

            if (task == tcs.Task)
            {
                try
                {
                    var resources = await createResources().ConfigureAwait(false);
                    tcs.SetResult(resources);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                    throw;
                }
            }
        }

        // Increment reference count
        lock (LockObject)
        {
            ReferenceCounts.AddOrUpdate(type, 1, (_, count) => count + 1);
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes resources for a specific fixture type if no other tests are using it.
    /// </summary>
    public static async Task DisposeFixture(Type type)
    {
        Task<IReadOnlyDictionary<string, object>>? taskToDispose = null;

        lock (LockObject)
        {
            if (ReferenceCounts.TryGetValue(type, out var count))
            {
                count--;
                if (count <= 0)
                {
                    ReferenceCounts.TryRemove(type, out _);
                    Resources.TryRemove(type, out taskToDispose);
                }
                else
                {
                    ReferenceCounts[type] = count;
                }
            }
        }

        if (taskToDispose != null)
        {
            await DisposeResources(taskToDispose).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes all resources. Should be called at the end of the test run.
    /// </summary>
    public static async Task DisposeAll()
    {
        var tasksToDispose = Resources.Values.ToList();

        foreach (var resourceGroup in tasksToDispose)
        {
            await DisposeResources(resourceGroup).ConfigureAwait(false);
        }

        Resources.Clear();
        ReferenceCounts.Clear();
    }

    private static async Task DisposeResources(Task<IReadOnlyDictionary<string, object>> resourceGroup)
    {
        try
        {
            var resources = await resourceGroup.ConfigureAwait(false);

            foreach (var resource in resources.Values)
            {
                try
                {
                    if (resource is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else if (resource is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch
                {
                    // Log but don't throw - allow other resources to be disposed
                }
            }
        }
        catch
        {
            // Exceptions are expected here, if the container failed to initialize
        }
    }
}
