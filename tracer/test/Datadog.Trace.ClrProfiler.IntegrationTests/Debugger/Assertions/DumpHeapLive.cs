// <copyright file="DumpHeapLive.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger.Assertions;

/// <summary>
/// Performs the equivalent of the windbg/sos command "!DumpHeap -stat -live", which provides statistics regarding objects
/// that are still "live" (meaning there is a GC root which directly or indirectly references them).
/// Based on borrowed code from Alois Krause post: https://stackoverflow.com/a/35286955/292555
/// </summary>
internal static class DumpHeapLive
{
    public static Dictionary<string, (int Live, int Disposed)> GetLiveObjectsByTypes(Process process)
    {
        var objCountByType = new Dictionary<string, (int Live, int Disposed)>();
        if (process.HasExited)
        {
            throw new MemoryAssertionException("Process has exited", string.Empty);
        }

        using var target = DataTarget.CreateSnapshotAndAttach(process.Id);
        var heap = target.ClrVersions[0].CreateRuntime().Heap;
        var considered = new ObjectSet(heap);
        var eval = new Stack<ulong>();

        foreach (var root in heap.EnumerateRoots())
        {
            eval.Push(root.Object);
        }

        while (eval.Count > 0)
        {
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
