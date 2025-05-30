// <copyright file="TraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog;

internal class TraceExporter : SafeHandle, IApi
{
    private readonly IDatadogLogger _log;

    public TraceExporter(
        TraceExporterConfiguration configuration,
        IDatadogLogger? log = null)
        : base(IntPtr.Zero, true)
    {
        _log = log ?? DatadogLogging.GetLoggerFor<TraceExporter>();

        _log.Debug("Creating new TraceExporter");
        using var errPtr = NativeInterop.Exporter.New(out var ptr, configuration);
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
                try
                {
                    using var error = NativeInterop.Exporter.Send(this, tracesSlice, (UIntPtr)numberOfTraces, ref responsePtr);
                    if (!error.IsInvalid)
                    {
                        var ex = error.ToException();
#pragma warning disable DDLOG004
                        _log.Error(ex, "An error occurred while sending data to the agent. Error Code: " + ex.ErrorCode + ", message: {Message}", ex.Message);
#pragma warning restore DDLOG004
                        throw ex;
                    }
                }
                catch (Exception ex) when (ex is not TraceExporterException)
                {
                    _log.Error(ex, "An error occurred while sending data to the agent.");
                }
            }
        }

        _log.Debug<int>("Successfully sent {Count} traces to the Datadog Agent.", numberOfTraces);

        return Task.FromResult(true);
    }

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
    {
        _log.Debug("No-op: stats computation happens in the data pipeline.");
        return Task.FromResult(true);
    }

    protected override bool ReleaseHandle()
    {
        NativeInterop.Exporter.Free(handle);
        return true;
    }
}
