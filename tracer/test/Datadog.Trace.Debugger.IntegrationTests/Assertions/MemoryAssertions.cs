// <copyright file="MemoryAssertions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit.Abstractions;

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
    /// <param name="output">The test output helper</param>
    /// <returns>MemoryAssertions</returns>
    public static async Task<MemoryAssertions> CaptureSnapshotToAssertOn(Process process, ITestOutputHelper output)
    {
        return await CaptureSnapshotToAssertOn(process, output, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Capture a snapshot of the current state of a process and returns a MemoryAssertions instance
    /// which can perform assertions on said snapshot.
    /// </summary>
    /// <param name="process">Process to capture snapshot of</param>
    /// <param name="output">The test output helper</param>
    /// <param name="timeout">Timeout for the memory snapshot operation</param>
    /// <returns>MemoryAssertions</returns>
    public static async Task<MemoryAssertions> CaptureSnapshotToAssertOn(Process process, ITestOutputHelper output, TimeSpan timeout)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            throw new NotSupportedException("Arm64 is not supported for memory assertions");
        }

        var liveObjectsByTypes = await DumpHeapLive.GetLiveObjectsByTypes(process, output, timeout);
        return new MemoryAssertions(liveObjectsByTypes);
    }

    /// <summary>
    /// Attempts to capture a memory snapshot with timeout handling.
    /// </summary>
    /// <param name="process">Process to capture snapshot of</param>
    /// <param name="output">The test output helper</param>
    /// <param name="timeout">Timeout for the memory snapshot operation</param>
    /// <returns>MemoryAssertions or null if the operation timed out</returns>
    public static async Task<MemoryAssertions?> TryCaptureSnapshotToAssertOn(Process process, ITestOutputHelper output, TimeSpan timeout)
    {
        try
        {
            return await CaptureSnapshotToAssertOn(process, output, timeout);
        }
        catch (OperationCanceledException)
        {
            output?.WriteLine($"Memory assertion operation timed out after {timeout.TotalSeconds} seconds.");
            return null;
        }
        catch (TimeoutException)
        {
            output?.WriteLine($"Memory assertion operation timed out after {timeout.TotalSeconds} seconds.");
            return null;
        }
    }

    public void NoUndisposedObjectsExist<T>()
    {
        if (typeof(T).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance) == null)
        {
            throw new Exception("Type " + typeof(T).Name + " does not have a private field named '_disposed'. " +
                                "Therefore, we cannot verify there are no live un-disposed instances." + GetDumpDetails());
        }

        var typeFullName = typeof(T).FullName;
        if (typeFullName == null)
        {
            return;
        }

        if (_liveObjectsByTypes.TryGetValue(typeFullName, out var result))
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
        var typeFullName = typeof(T).FullName;
        if (typeFullName == null)
        {
            return;
        }

        if (_liveObjectsByTypes.TryGetValue(typeFullName, out var result) && result.Live > 0)
        {
            throw new MemoryAssertionException($"Expected no {typeof(T).Name} objects in memory, but found {result.Live}.", GetDumpDetails());
        }
    }

    public void ObjectsExist<T>()
    {
        var typeFullName = typeof(T).FullName;
        if (typeFullName == null)
        {
            return;
        }

        if (!_liveObjectsByTypes.TryGetValue(typeFullName, out var result) && result.Live > 0)
        {
            throw new MemoryAssertionException($"Expected {typeof(T).Name} objects in memory, but not found.", GetDumpDetails());
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
