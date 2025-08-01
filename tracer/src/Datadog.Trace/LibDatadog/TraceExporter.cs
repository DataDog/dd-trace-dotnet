// <copyright file="TraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using static Datadog.Trace.Agent.Api;

namespace Datadog.Trace.LibDatadog;

internal class TraceExporter : SafeHandle, IApi
{
    private readonly IDatadogLogger _log;
    private readonly Action<Dictionary<string, float>> _updateSampleRates;
    private string _cachedResponse;

    public TraceExporter(
        TraceExporterConfiguration configuration,
        Action<Dictionary<string, float>> updateSampleRates,
        IDatadogLogger? log = null)
        : base(IntPtr.Zero, true)
    {
        _updateSampleRates = updateSampleRates;
        _log = log ?? DatadogLogging.GetLoggerFor<TraceExporter>();
        _log.Debug("Creating new TraceExporter");
        _cachedResponse = string.Empty;
        using var errPtr = NativeInterop.Exporter.New(out var ptr, configuration);
        errPtr.ThrowIfError();
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
    {
        _log.Debug<int>("Sending {Count} traces to the Datadog Agent.", numberOfTraces);

        var responsePtr = IntPtr.Zero;
        try
        {
            Send(traces, numberOfTraces, ref responsePtr);

            try
            {
                ProcessResponse(responsePtr);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "An error occurred deserializing the response.");
            }

            _log.Debug<int>("Successfully sent {Count} traces to the Datadog Agent.", numberOfTraces);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is not TraceExporterException)
        {
            _log.Error(ex, "An error occurred while sending data to the agent.");
            return Task.FromResult(false);
        }
        finally
        {
            if (responsePtr != IntPtr.Zero)
            {
                NativeInterop.Exporter.FreeResponse(responsePtr);
            }
        }
    }

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
    {
        _log.Debug("No-op: stats computation happens in the data pipeline.");
        return Task.FromResult(true);
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            NativeInterop.Exporter.Free(handle);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "An error occurred while releasing the handle for TraceExporter.");
            return false;
        }

        return true;
    }

    private unsafe int GetBodyLen(IntPtr body)
    {
        if (body == IntPtr.Zero)
        {
            return 0;
        }

        byte* p = (byte*)body;
        int len = 0;
        while (p[len] != 0)
        {
            len++;
        }

        return len;
    }

    private unsafe void Send(ArraySegment<byte> traces, int numberOfTraces, ref IntPtr responsePtr)
    {
        fixed (byte* ptr = traces.Array)
        {
            var traceSlice = new ByteSlice
            {
                Ptr = (IntPtr)ptr,
                Len = (UIntPtr)traces.Count
            };

            using var error = NativeInterop.Exporter.Send(this, traceSlice, (UIntPtr)numberOfTraces, ref responsePtr);
            if (!error.IsInvalid)
            {
                var ex = error.ToException();
                _log.Error(ex, "An error occurred while sending data to the agent. Error Code: {ErrorCode}, Message: {Message}", ex.ErrorCode, ex.Message);
                throw ex;
            }
        }
    }

    private unsafe void ProcessResponse(IntPtr response)
    {
        if (_updateSampleRates is null)
        {
            return;
        }

        if (response == IntPtr.Zero)
        {
            // If response is Null bail out immediately.
            return;
        }

        // TODO: replace GetBodyLen with a native function in order to avoid iterating over the response to get its length.
        var body = NativeInterop.Exporter.GetResponseBody(response);
        var len = GetBodyLen(body);
        if (len <= 0)
        {
            return;
        }

        var json = System.Text.Encoding.UTF8.GetString((byte*)body, len);
        if (json != _cachedResponse)
        {
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(json);
            _updateSampleRates(apiResponse.RateByService);
            _cachedResponse = json;
        }
    }
}
