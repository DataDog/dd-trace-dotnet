// <copyright file="ContainersRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers.Containers;

public static class ContainersRegistry
{
    private static readonly ConcurrentDictionary<Type, Task<IReadOnlyDictionary<string, object>>> Resources = new();

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
                }
            }
        }

        return await task.ConfigureAwait(false);
    }

    public static async Task DisposeAll()
    {
        foreach (var resourceGroup in Resources.Values)
        {
            try
            {
                var resources = await resourceGroup.ConfigureAwait(false);

                foreach (var resource in resources.Values)
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
            }
            catch
            {
                // Exceptions are expected here, if the container failed to initialize
            }
        }

        Resources.Clear();
    }
}
