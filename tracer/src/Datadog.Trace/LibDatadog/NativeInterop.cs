// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.LibDatadog.ServiceDiscovery;

namespace Datadog.Trace.LibDatadog;

internal class NativeInterop
{
    private const string DllName = "LibDatadog";

    // This will never change, so we use a lazy to cache the result.
    // This confirms that we are in an automatic instrumentation environment (and so PInvokes have been re-written)
    // and that the libdatadog library has been deployed (which is not the case in many serverless environments).
    // We should add or remove conditions from here as our deployment requirements change.
    private static readonly Lazy<bool> LibDatadogAvailable = new(() => !Util.EnvironmentHelpers.IsServerlessEnvironment() && Instrumentation.ProfilerAttached);

    public static bool IsLibDatadogAvailable => LibDatadogAvailable.Value;

    internal static class Exporter
    {
        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_new")]
        internal static extern TraceExporterErrorHandle New(out IntPtr outHandle, SafeHandle config);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_error_free")]
        internal static extern void FreeError(IntPtr error);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_free")]
        internal static extern void Free(IntPtr handle);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_send")]
        internal static extern TraceExporterErrorHandle Send(SafeHandle handle, ByteSlice trace, UIntPtr traceCount, ref IntPtr response);
    }

    internal static class Config
    {
        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_new")]
        internal static extern void New(out IntPtr outHandle);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_free")]
        internal static extern void Free(IntPtr handle);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_url")]
        internal static extern TraceExporterErrorHandle SetUrl(SafeHandle config, CharSlice url);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_tracer_version")]
        internal static extern TraceExporterErrorHandle SetTracerVersion(SafeHandle config, CharSlice version);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_language")]
        internal static extern TraceExporterErrorHandle SetLanguage(SafeHandle config, CharSlice lang);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_lang_version")]
        internal static extern TraceExporterErrorHandle SetLanguageVersion(SafeHandle config, CharSlice version);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_lang_interpreter")]
        internal static extern TraceExporterErrorHandle SetInterpreter(SafeHandle config, CharSlice interpreter);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_hostname")]
        internal static extern TraceExporterErrorHandle SetHostname(SafeHandle config, CharSlice hostname);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_env")]
        internal static extern TraceExporterErrorHandle SetEnv(SafeHandle config, CharSlice env);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_version")]
        internal static extern TraceExporterErrorHandle SetVersion(SafeHandle config, CharSlice version);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_service")]
        internal static extern TraceExporterErrorHandle SetService(SafeHandle config, CharSlice service);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_compute_stats")]
        internal static extern TraceExporterErrorHandle SetComputeStats(SafeHandle config, bool isEnabled);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_enable_telemetry")]
        internal static extern TraceExporterErrorHandle EnableTelemetry(SafeHandle config, IntPtr telemetryConfig);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_client_computed_stats")]
        internal static extern TraceExporterErrorHandle SetClientComputedStats(SafeHandle config, bool clientComputedStats);
    }

    internal static class Logger
    {
        [DllImport(DllName, EntryPoint = "ddog_logger_configure_std")]
        internal static extern ErrorHandle ConfigureStd(StdConfig config);

        [DllImport(DllName, EntryPoint = "ddog_logger_configure_file")]
        internal static extern ErrorHandle ConfigureFile(FileConfig config);

        [DllImport(DllName, EntryPoint = "ddog_logger_disable_file")]
        internal static extern ErrorHandle DisableFile();

        [DllImport(DllName, EntryPoint = "ddog_logger_disable_std")]
        internal static extern ErrorHandle DisableStd();

        [DllImport(DllName, EntryPoint = "ddog_logger_set_log_level")]
        internal static extern ErrorHandle SetLogLevel(LogEventLevel logLevel);
    }

    internal static class Common
    {
        [DllImport(DllName, EntryPoint = "ddog_Error_drop")]
        internal static extern void Drop(ErrorHandle error);

        [DllImport(DllName, EntryPoint = "ddog_store_tracer_metadata")]
        internal static extern TracerMemfdHandleResult StoreTracerMetadata(
            byte schemaVersion,
            CharSlice runtimeId,
            CharSlice tracerLanguage,
            CharSlice tracerVersion,
            CharSlice hostname,
            CharSlice serviceName,
            CharSlice serviceEnv,
            CharSlice serviceVersion);

        [DllImport(DllName, EntryPoint = "ddog_Error_drop")]
        internal static extern void DropError(ref Error errorHandle);
    }
}
