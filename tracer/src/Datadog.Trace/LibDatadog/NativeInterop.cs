// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.DataPipeline;
using Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;
using Datadog.Trace.LibDatadog.Logging;
using Datadog.Trace.LibDatadog.ServiceDiscovery;

namespace Datadog.Trace.LibDatadog;

internal static class NativeInterop
{
    private const string DllName = "LibDatadog";

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

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_response_get_body")]
        internal static extern IntPtr GetResponseBody(SafeHandle outHandle, ref UIntPtr len);

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_response_free")]
        internal static extern void FreeResponse(IntPtr handle);
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

        [DllImport(DllName, EntryPoint = "ddog_trace_exporter_config_set_connection_timeout")]
        internal static extern TraceExporterErrorHandle SetConnectionTimeout(SafeHandle config, ulong timeout_ms);
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

        [DllImport(DllName, EntryPoint = "ddog_Error_drop")]
        internal static extern void DropError(ref Error errorHandle);
    }

    internal static class LibraryConfig
    {
        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_new")]
        internal static extern IntPtr TracerMetadataNew();

        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_free")]
        internal static extern void TracerMetadataFree(IntPtr metadata);

        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_set")]
        internal static extern void TracerMetadataSet(IntPtr metadata, MetadataKind kind, CString value);

        [DllImport(DllName, EntryPoint = "ddog_tracer_metadata_store")]
        internal static extern TracerMemfdHandleResult StoreTracerMetadata(IntPtr metadata);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_new")]
        internal static extern IntPtr ConfiguratorNew(byte debugLogs, CharSlice language);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_with_local_path")]
        internal static extern IntPtr ConfiguratorWithLocalPath(IntPtr configurator, CString localPath);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_with_fleet_path")]
        internal static extern IntPtr ConfiguratorWithFleetPath(IntPtr configurator, CString fleetPath);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_get")]
        internal static extern LibraryConfigResult ConfiguratorGet(IntPtr configurator);

        [DllImport(DllName, EntryPoint = "ddog_library_configurator_drop")]
        internal static extern void ConfiguratorDrop(IntPtr configurator);

        [DllImport(DllName, EntryPoint = "ddog_library_config_drop")]
        internal static extern void LibraryConfigDrop(LibraryConfigResult configs);
    }
}
