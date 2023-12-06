// <copyright file="ContainersRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public static class ContainersRegistry
{
    private static readonly ConcurrentDictionary<string, Task<IContainer>> Containers = new();

    public static async Task<IContainer> GetOrAdd(string name, Func<ContainerBuilder, Task<IContainer>> createContainer)
    {
        if (!Containers.TryGetValue(name, out var task))
        {
            var tcs = new TaskCompletionSource<IContainer>(TaskCreationOptions.RunContinuationsAsynchronously);

            task = Containers.GetOrAdd(name, tcs.Task);

            if (task == tcs.Task)
            {
                try
                {
                    var container = await createContainer(CreateBuilder()).ConfigureAwait(false);
                    tcs.SetResult(container);
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
        foreach (var container in Containers.Values)
        {
            await (await container).DisposeAsync();
        }

        Containers.Clear();
    }

    private static ContainerBuilder CreateBuilder()
    {
        // If something needs to be added to all containers, do it here
        return new ContainerBuilder();
    }
}
