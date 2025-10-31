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

/// <summary>
/// Registry for TestContainers resources. Ensures containers are started once and reused across test classes.
/// Containers are disposed when <see cref="DisposeAll"/> is called (typically at end of test run).
/// </summary>
public static class ContainersRegistry
{
    private static readonly ConcurrentDictionary<Type, Task<IReadOnlyDictionary<string, object>>> Resources = new();
    private static readonly ConcurrentDictionary<Type, int> ReferenceCounts = new();
    private static readonly ConcurrentDictionary<Type, bool> DisposingFixtures = new();
    private static readonly object DisposeLock = new();

    public static async Task<IReadOnlyDictionary<string, object>> GetOrAdd(Type type, Func<Task<IReadOnlyDictionary<string, object>>> createResources)
    {
        // Prevent getting resources that are being disposed to avoid race conditions
        if (DisposingFixtures.TryGetValue(type, out var disposing) && disposing)
        {
            throw new InvalidOperationException($"Cannot acquire {type.Name} - it is being disposed");
        }

        // Increment reference count for this fixture type
        ReferenceCounts.AddOrUpdate(type, 1, (_, count) => count + 1);

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

    /// <summary>
    /// Releases a reference to a fixture type. When the reference count reaches zero, the resources are disposed.
    /// This should be called from ContainerFixture.DisposeAsync() when a test class completes.
    /// </summary>
    public static async Task Release(Type type)
    {
        bool shouldDispose;

        lock (DisposeLock)
        {
            if (!ReferenceCounts.TryGetValue(type, out var currentCount))
            {
                return;
            }

            var newCount = currentCount - 1;

            if (newCount <= 0)
            {
                ReferenceCounts.TryRemove(type, out _);
                // Mark as disposing to prevent new GetOrAdd calls during disposal
                DisposingFixtures[type] = true;
                shouldDispose = true;
            }
            else
            {
                ReferenceCounts[type] = newCount;
                shouldDispose = false;
            }
        }

        // Dispose outside the lock to avoid holding it during async operations
        if (shouldDispose)
        {
            try
            {
                await DisposeFixtureResources(type).ConfigureAwait(false);
            }
            finally
            {
                // Always clear the disposing flag, even if disposal fails
                DisposingFixtures.TryRemove(type, out _);
            }
        }
    }

    /// <summary>
    /// Disposes all containers in the registry.
    /// This should be called at the end of the test run to clean up any remaining containers.
    /// Use xUnit's ICollectionFixture to ensure this is called when all tests complete.
    /// </summary>
    public static async Task DisposeAll()
    {
        var fixtureTypes = Resources.Keys.ToArray();

        // Mark all fixtures as disposing to prevent new GetOrAdd calls
        foreach (var fixtureType in fixtureTypes)
        {
            DisposingFixtures[fixtureType] = true;
        }

        foreach (var fixtureType in fixtureTypes)
        {
            await DisposeFixtureResources(fixtureType).ConfigureAwait(false);
        }

        Resources.Clear();
        ReferenceCounts.Clear();
        DisposingFixtures.Clear();
    }

    private static async Task DisposeFixtureResources(Type fixtureType)
    {
        if (!Resources.TryRemove(fixtureType, out var resourceGroupTask))
        {
            return;
        }

        try
        {
            var resources = await resourceGroupTask.ConfigureAwait(false);

            foreach (var resourceKvp in resources)
            {
                var resource = resourceKvp.Value;

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
                    // Continue disposing other resources even if one fails
                }
            }
        }
        catch
        {
            // Exceptions are expected here, if the container failed to initialize
        }
    }
}
