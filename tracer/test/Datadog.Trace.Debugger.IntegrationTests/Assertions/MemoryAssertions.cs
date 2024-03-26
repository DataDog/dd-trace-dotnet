// <copyright file="MemoryAssertions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Debugger.IntegrationTests.Assertions;

#if !NETCOREAPP2_1
/// <summary>
/// Perform assertions to validate there are no memory leaks.
/// NOTE: This uses ClrMD instead of the SciTech MemProfiler API or JetBrains dotMemoryUnit because in our case,
/// there is already a profiler attached to the process (and we need it to work in Linux as well).
/// IMPORTANT: Not supported on .net core 2.1 and ARM64 architecture
/// </summary>
internal class MemoryAssertions
{
    private readonly Dictionary<string, (int Live, int Disposed)> _liveObjectsByTypes;

    private MemoryAssertions(Dictionary<string, (int Live, int Disposed)> liveObjectsByTypes)
    {
        _liveObjectsByTypes = liveObjectsByTypes;
    }

    /// <summary>
    /// Capture a snapshot of the current state of a process and returns a MemoryAssertions instance
    /// which can perform assertions on said snapshot.
    /// </summary>
    /// <param name="process">Process to capture snapshot of</param>
    /// <returns>MemoryAssertions</returns>
    public static MemoryAssertions CaptureSnapshotToAssertOn(Process process)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            throw new NotSupportedException("Arm64 is not supported for memory assertions");
        }

        var liveObjectsByTypes = DumpHeapLive.GetLiveObjectsByTypes(process);
        return new MemoryAssertions(liveObjectsByTypes);
    }

    public void NoUndisposedObjectsExist<T>()
    {
        if (typeof(T).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance) == null)
        {
            throw new Exception("Type " + typeof(T).Name + " does not have a private field named '_disposed'. " +
                                "Therefore, we cannot verify there are no live un-disposed instances." + GetDumpDetails());
        }

        if (_liveObjectsByTypes.TryGetValue(typeof(T).FullName, out var result))
        {
            var undisposed = result.Live - result.Disposed;
            if (undisposed > 0)
            {
                throw new MemoryAssertionException($"Expected no {typeof(T).Name} objects in memory with (_disposed = false), but found {undisposed}. ", GetDumpDetails());
            }
        }
    }

    public void NoObjectsExist<T>()
    {
        if (_liveObjectsByTypes.TryGetValue(typeof(T).FullName, out var result) && result.Live > 0)
        {
            throw new MemoryAssertionException($"Expected no {typeof(T).Name} objects in memory, but found {result.Live}.", GetDumpDetails());
        }
    }

    private string GetDumpDetails()
    {
        return string.Empty;
        // re-use TaskHelper.TakeMemoryDump after rebasing from datadog upstream
        // https://github.com/DataDog/dd-trace-dotnet/pull/2655/files#diff-3eeffff2f0a958d283a96355b666dcfa121a911332c526139121f7f4260af89c
    }
}
#endif
