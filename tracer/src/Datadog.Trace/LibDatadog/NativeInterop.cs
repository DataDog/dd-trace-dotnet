// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

internal class NativeInterop
{
    private const string DllName = "LibDatadog";

    internal static class Exporter
    {
        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_new")]
        internal static extern ErrorHandle New(out IntPtr outHandle, SafeHandle config);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_error_free")]
        internal static extern void FreeError(IntPtr error);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_free")]
        internal static extern void Free(IntPtr handle);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_send")]
        internal static extern ErrorHandle Send(SafeHandle handle, ByteSlice trace, UIntPtr traceCount, ref IntPtr response);
    }

    internal static class Config
    {
        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_new")]
        internal static extern void New(out IntPtr outHandle);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_free")]
        internal static extern void Free(IntPtr handle);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_url")]
        internal static extern ErrorHandle SetUrl(SafeHandle config, CharSlice url);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_tracer_version")]
        internal static extern ErrorHandle SetTracerVersion(SafeHandle config, CharSlice version);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_language")]
        internal static extern ErrorHandle SetLanguage(SafeHandle config, CharSlice lang);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_lang_version")]
        internal static extern ErrorHandle SetLanguageVersion(SafeHandle config, CharSlice version);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_lang_interpreter")]
        internal static extern ErrorHandle SetInterpreter(SafeHandle config, CharSlice interpreter);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_hostname")]
        internal static extern ErrorHandle SetHostname(SafeHandle config, CharSlice hostname);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_env")]
        internal static extern ErrorHandle SetEnv(SafeHandle config, CharSlice env);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_version")]
        internal static extern ErrorHandle SetVersion(SafeHandle config, CharSlice version);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_service")]
        internal static extern ErrorHandle SetService(SafeHandle config, CharSlice service);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_compute_stats")]
        internal static extern ErrorHandle SetComputeStats(SafeHandle config, bool isEnabled);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_enable_telemetry")]
        internal static extern ErrorHandle EnableTelemetry(SafeHandle config, IntPtr telemetryConfig);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_client_computed_stats")]
        internal static extern ErrorHandle SetClientComputedStats(SafeHandle config, bool clientComputedStats);
    }
}
