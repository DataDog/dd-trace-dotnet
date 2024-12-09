// <copyright file="TraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.LibDatadog;

internal class TraceExporter : IApi, IDisposable
{
    private readonly IntPtr _handle = IntPtr.Zero;
    private readonly CharSlice _url;
    private readonly CharSlice _tracerVersion;
    private readonly CharSlice _language;
    private readonly CharSlice _languageVersion;
    private readonly CharSlice _languageInterpreter;
    private readonly CharSlice _hostname;
    private readonly CharSlice _env;
    private readonly CharSlice _service;
    private readonly CharSlice _serviceVersion;

    public TraceExporter(ImmutableTracerSettings settings)
        : this(
            settings.Exporter.AgentUri,
            settings.ServiceName,
            settings.ServiceVersion,
            settings.Environment,
            TracerConstants.AssemblyVersion,
            ".NET",
            Environment.Version.ToString(),
            ".NET",
            settings.Exporter.AgentUri.ToString(),
            settings.StatsComputationEnabled,
            TraceExporterInputFormat.V04,
            TraceExporterOutputFormat.V04)
    {
    }

    public TraceExporter(
        Uri agentUri,
        string serviceName,
        string serviceVersion,
        string environment,
        string tracerVersion,
        string language,
        string languageVersion,
        string languageInterpreter,
        string hostname,
        bool statsComputationEnabled,
        TraceExporterInputFormat inputFormat,
        TraceExporterOutputFormat outputFormat)
    {
        _url = new CharSlice(agentUri.ToString());
        _tracerVersion = new CharSlice(tracerVersion);
        _language = new CharSlice(language);
        _languageVersion = new CharSlice(languageVersion);
        _languageInterpreter = new CharSlice(languageInterpreter);
        _hostname = new CharSlice(hostname);
        _env = new CharSlice(environment);
        _service = new CharSlice(serviceName);
        _serviceVersion = new CharSlice(serviceVersion);

        var error = Native.ddog_trace_exporter_new(
            outHandle: ref _handle,
            url: _url,
            tracerVersion: _tracerVersion,
            language: _language,
            languageVersion: _languageVersion,
            languageInterpreter: _languageInterpreter,
            hostname: _hostname,
            env: _env,
            version: _serviceVersion,
            service: _service,
            inputFormat: inputFormat,
            outputFormat: outputFormat,
            computeStats: statsComputationEnabled,
            agentResponseCallback: (IntPtr chars) =>
            {
                var response = Marshal.PtrToStringUni(chars);
                Console.WriteLine(response);
            });
        if (error.Tag == ErrorTag.Some)
        {
            throw new LibDatadogException(error);
        }
    }

    ~TraceExporter()
    {
        _url.Free();
        _tracerVersion.Free();
        _language.Free();
        _languageVersion.Free();
        _languageInterpreter.Free();
        _hostname.Free();
        _env.Free();
        _service.Free();
        _serviceVersion.Free();

        ReleaseUnmanagedResources();
    }

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
    {
        var tracesHandle = GCHandle.Alloc(traces.Array, GCHandleType.Pinned);
        var tracesSlice = new ByteSlice { Ptr = tracesHandle.AddrOfPinnedObject(), Len = (UIntPtr)traces.Count };
        try
        {
            var error = Native.ddog_trace_exporter_send(_handle, tracesSlice, (UIntPtr)numberOfTraces);
            if (error.Tag == ErrorTag.Some)
            {
                return Task.FromResult(false);
            }
        }
        finally
        {
            tracesHandle.Free();
        }

        return Task.FromResult(true);
    }

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
    {
        return Task.FromResult(true);
    }

    private void ReleaseUnmanagedResources()
    {
        if (_handle != IntPtr.Zero)
        {
            Native.ddog_trace_exporter_free(_handle);
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

#pragma warning disable SA1300
    internal class Native
    {
        private const string DllName = "datadog_profiling_ffi";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern MaybeError ddog_trace_exporter_new(
            ref IntPtr outHandle,
            CharSlice url,
            CharSlice tracerVersion,
            CharSlice language,
            CharSlice languageVersion,
            CharSlice languageInterpreter,
            CharSlice hostname,
            CharSlice env,
            CharSlice version,
            CharSlice service,
            TraceExporterInputFormat inputFormat,
            TraceExporterOutputFormat outputFormat,
            bool computeStats,
            AgentResponseCallback agentResponseCallback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ddog_MaybeError_drop(MaybeError error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ddog_trace_exporter_free(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern MaybeError ddog_trace_exporter_send(IntPtr handle, ByteSlice trace, UIntPtr traceCount);
    }
#pragma warning restore SA1300
}
