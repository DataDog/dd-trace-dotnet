// <copyright file="TraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog;

internal class TraceExporter : SafeHandle, IApi
{
    private readonly TraceExporterConfiguration _configuration;
    private readonly IDatadogLogger _log;

    public TraceExporter(
        TraceExporterConfiguration configuration,
        IDatadogLogger? log = null)
        : base(IntPtr.Zero, true)
    {
        _log = log ?? DatadogLogging.GetLoggerFor<TraceExporter>();
        _configuration = configuration;

        _log.Debug("Creating new TraceExporter");
        var errPtr = TraceExporterNative.ddog_trace_exporter_new(out var ptr, _configuration);
        errPtr.ThrowIfError();
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
    {
        _log.Debug<int>("Sending {Count} traces to the Datadog Agent.", numberOfTraces);

        unsafe
        {
            fixed (byte* ptr = traces.Array)
            {
                var tracesSlice = new ByteSlice
                {
                    Ptr = (IntPtr)ptr,
                    Len = (UIntPtr)traces.Count
                };

                var responsePtr = IntPtr.Zero;
                using var error = TraceExporterNative.ddog_trace_exporter_send(this, tracesSlice, (UIntPtr)numberOfTraces, ref responsePtr);
                error.ThrowIfError();
            }
        }

        return Task.FromResult(true);
    }

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
    {
        _log.Debug("No-op: stats computation happens in the data pipeline.");
        return Task.FromResult(true);
    }

    protected override bool ReleaseHandle()
    {
        TraceExporterNative.ddog_trace_exporter_free(handle);
        return true;
    }
}
