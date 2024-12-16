// <copyright file="TraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.LibDatadog;

internal class TraceExporter : SafeHandle, IApi
{
    private readonly TraceExporterConfiguration _configuration;

    public TraceExporter(TraceExporterConfiguration configuration)
        : base(IntPtr.Zero, true)
    {
        _configuration = configuration;
        var errPtr = TraceExporterNative.ddog_trace_exporter_new(out var ptr, configuration);
        errPtr.ThrowIfError();
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
    {
        // Pin the array to get a pointer to the data
        // This is recommended if using UnsafeAddrOfPinnedArrayElement to avoid the GC moving the array
        var tracesHandle = GCHandle.Alloc(traces.Array, GCHandleType.Pinned);
        var tracesSlice = new ByteSlice
        {
            Ptr = Marshal.UnsafeAddrOfPinnedArrayElement(traces.Array, traces.Offset),
            Len = (UIntPtr)traces.Count
        };

        var responsePtr = IntPtr.Zero;
        using var error = TraceExporterNative.ddog_trace_exporter_send(this, tracesSlice, (UIntPtr)numberOfTraces, ref responsePtr);
        tracesHandle.Free();
        error.ThrowIfError();

        return Task.FromResult(true);
    }

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
    {
        return Task.FromResult(true);
    }

    protected override bool ReleaseHandle()
    {
        TraceExporterNative.ddog_trace_exporter_free(handle);
        return true;
    }
}
