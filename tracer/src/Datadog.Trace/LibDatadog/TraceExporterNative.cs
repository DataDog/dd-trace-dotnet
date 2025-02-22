// <copyright file="TraceExporterNative.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

#pragma warning disable SA1300
internal class TraceExporterNative
{
    private const string DllName = "datadog_profiling_ffi";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_new(out IntPtr outHandle, SafeHandle config);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_error_free(IntPtr error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_free(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_send(SafeHandle handle, ByteSlice trace, UIntPtr traceCount, ref IntPtr response);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_config_new(out IntPtr outHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_config_free(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_url(SafeHandle config, CharSlice url);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_tracer_version(SafeHandle config, CharSlice version);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_language(SafeHandle config, CharSlice lang);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_lang_version(SafeHandle config, CharSlice version);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_lang_interpreter(SafeHandle config, CharSlice interpreter);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_hostname(SafeHandle config, CharSlice hostname);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_env(SafeHandle config, CharSlice env);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_version(SafeHandle config, CharSlice version);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_service(SafeHandle config, CharSlice service);
}
#pragma warning restore SA1300
