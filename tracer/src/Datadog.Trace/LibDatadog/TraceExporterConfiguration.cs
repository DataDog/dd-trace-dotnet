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
    private string? _url;
    private string? _traceVersion;
    private string? _language;
    private string? _languageVersion;
    private string? _languageInterpreter;
    private string? _hostname;
    private string? _env;
    private string? _version;
    private string? _service;

    public TraceExporterConfiguration()
        : base(IntPtr.Zero, true)
    {
        TraceExporterNative.ddog_trace_exporter_config_new(out var ptr);
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public string? Url
    {
        get => _url;
        set
        {
            _url = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_url(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? TraceVersion
    {
        get => _traceVersion;
        set
        {
            _traceVersion = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_tracer_version(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Language
    {
        get => _language;
        set
        {
            _language = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_language(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? LanguageVersion
    {
        get => _languageVersion;
        set
        {
            _languageVersion = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_lang_version(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? LanguageInterpreter
    {
        get => _languageInterpreter;
        set
        {
            _languageInterpreter = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_lang_interpreter(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Hostname
    {
        get => _hostname;
        set
        {
            _hostname = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_hostname(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Env
    {
        get => _env;
        set
        {
            _env = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_env(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Version
    {
        get => _version;
        set
        {
            _version = value;
            using var error = TraceExporterNative.ddog_trace_exporter_config_set_version(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Service
    {
        get => _service;
        set
        {
            _service = value;
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
