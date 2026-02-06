// <copyright file="TraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using static Datadog.Trace.Agent.Api;

namespace Datadog.Trace.LibDatadog.DataPipeline;

internal sealed class TraceExporter : SafeHandle, IApi
{
    private static readonly ArraySegment<byte> EmptyPayload = new([0x90]);

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

    public TracesEncoding TracesEncoding => TracesEncoding.DatadogV0;

    public Task<bool> Ping() => SendTracesAsync(EmptyPayload, 0, false, 0, 0, true);

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled)
    {
        _log.Debug<int>("Sending {Count} traces to the Datadog Agent.", numberOfTraces);

        try
        {
            using var response = Send(traces, numberOfTraces);

            if (response.IsInvalid)
            {
                _log.Warning("Traces sent successfully to the Agent, but the response is invalid");
            }
            else
            {
                var json = response.ReadAsString();

                if (StringUtil.IsNullOrEmpty(json))
                {
                    _log.Warning("Traces sent successfully to the Agent, but the response is empty");
                }
                else if (json != _cachedResponse)
                {
                    try
                    {
                        var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(json);
                        _updateSampleRates(apiResponse.RateByService);
                        _cachedResponse = json;
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Traces sent successfully to the Agent, but an error occurred deserializing the response.");
                    }
                }
            }

            _log.Debug<int>("Successfully sent {Count} traces to the Datadog Agent.", numberOfTraces);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is not TraceExporterException)
        {
            _log.Error(ex, "An error occurred while sending data to the agent.");
            return Task.FromResult(false);
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
        }

        return true;
    }

    private unsafe TraceExporterResponse Send(ArraySegment<byte> traces, int numberOfTraces)
    {
        fixed (byte* ptr = traces.Array)
        {
            var traceSlice = new ByteSlice
            {
                Ptr = (IntPtr)ptr,
                Len = (UIntPtr)traces.Count
            };

            var responsePtr = IntPtr.Zero;
            try
            {
                using var error = NativeInterop.Exporter.Send(this, traceSlice, (UIntPtr)numberOfTraces, ref responsePtr);
                if (!error.IsInvalid)
                {
                    var ex = error.ToException();
                    _log.Error(ex, "An error occurred while sending data to the agent. Error Code: {ErrorCode}, Message: {Message}", ex.ErrorCode, ex.Message);
                    throw ex;
                }
            }
            catch (Exception ex) when (ex is not TraceExporterException)
            {
                _log.Error(ex, "An error occurred while sending data to the agent.");
            }

            return new TraceExporterResponse(responsePtr);
        }
    }
}
