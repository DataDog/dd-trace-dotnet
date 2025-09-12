// <copyright file="DumpHeapLive.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.Assertions;

/// <summary>
/// Performs the equivalent of the windbg/sos command "!DumpHeap -stat -live", which provides statistics regarding objects
/// that are still "live" (meaning there is a GC root which directly or indirectly references them).
/// Based on borrowed code from Alois Krause post: https://stackoverflow.com/a/35286955/292555
/// </summary>
internal static class DumpHeapLive
{
    public static async Task<Dictionary<string, (int Live, int Disposed)>> GetLiveObjectsByTypes(Process process, ITestOutputHelper output, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        // Run the task in a background thread, and wait for it to complete or timeout.
        return await Task.Run(() => GetLiveObjectsByTypes(process, output, cts.Token), cts.Token);
    }

    private static Dictionary<string, (int Live, int Disposed)> GetLiveObjectsByTypes(Process process, ITestOutputHelper output, CancellationToken ct)
    {
        var objCountByType = new Dictionary<string, (int Live, int Disposed)>();
        if (process.HasExited)
        {
            throw new MemoryAssertionException("Process has exited", string.Empty);
        }

        output?.WriteLine($"Creating snapshot of process ID {process.Id}");
        using var target = DataTarget.CreateSnapshotAndAttach(process.Id);
        if (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }

        output?.WriteLine("Snapshot complete, analyzing heap...");
        var heap = target.ClrVersions[0].CreateRuntime().Heap;
        var considered = new ObjectSet(heap);
        var eval = new Stack<ulong>();

        var rootCount = 0;
        foreach (var root in heap.EnumerateRoots())
        {
            rootCount++;
            eval.Push(root.Object);
        }

        output?.WriteLine($"Found {rootCount} roots in the heap");
        // var evalsComplete = 0;
        while (eval.Count > 0)
        {
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            // too noisy. uncomment if you want to see the progress
            /*if (evalsComplete % 100 == 0)
            {
                output?.WriteLine($"Evaluated {evalsComplete} objects so far, {eval.Count} remaining.");
            }*/

            var obj = eval.Pop();
            if (considered.Contains(obj))
            {
                continue;
            }

            considered.Add(obj);

            var type = heap.GetObjectType(obj);

            var heapCorrupted = type == null;
            if (heapCorrupted)
            {
                continue;
            }

            bool? disposed = null;
            var disposedFlagField = type.GetFieldByName("_disposed");
            if (disposedFlagField != null)
            {
                disposed = disposedFlagField.Read<bool>(obj, false);
            }

            AddToStats(type.Name, objCountByType, disposed);

            foreach (var child in heap.GetObject(obj)
                                      .EnumerateReferences(carefully: true, considerDependantHandles: true))
            {
                if (!considered.Contains(child.Address))
                {
                    eval.Push(child.Address);
                }
            }
        }

        output?.WriteLine($"Analysis complete, found {objCountByType.Count} object types in the heap");
        return objCountByType;
    }

    private static void AddToStats(string typeName, Dictionary<string, (int Live, int Disposed)> objCountByType, bool? disposed)
    {
        if (typeName != null)
        {
            if (objCountByType.TryGetValue(typeName, out var current))
            {
                objCountByType[typeName] = (current.Live + 1, current.Disposed + (disposed == true ? 1 : 0));
            }
            else
            {
                objCountByType[typeName] = (1, disposed == true ? 1 : 0);
            }
        }
    }
}
