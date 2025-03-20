// <copyright file="TraceExporterConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a configuration for the trace exporter.
/// </summary>
internal class TraceExporterConfiguration : SafeHandle
{
    public TraceExporterConfiguration()
        : base(IntPtr.Zero, true)
    {
        TraceExporterNative.ddog_trace_exporter_config_new(out var ptr);
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public string Url
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_url(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string TraceVersion
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_tracer_version(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string Language
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_language(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string LanguageVersion
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_lang_version(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string LanguageInterpreter
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_lang_interpreter(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Hostname
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_hostname(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Env
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_env(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Version
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_version(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Service
    {
        init
        {
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_service(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    protected override bool ReleaseHandle()
    {
        TraceExporterNative.ddog_trace_exporter_config_free(handle);
        return true;
    }
}
