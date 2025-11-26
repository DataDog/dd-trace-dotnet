// <copyright file="TraceExporterConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.DataPipeline;

/// <summary>
/// Represents a configuration for the trace exporter.
/// </summary>
internal class TraceExporterConfiguration : SafeHandle
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<TraceExporterConfiguration>();

    private IntPtr _telemetryConfigPtr;
    private string _url = string.Empty;
    private string _traceVersion = string.Empty;
    private string _language = string.Empty;
    private string _languageVersion = string.Empty;
    private string _languageInterpreter = string.Empty;
    private string? _hostname;
    private string? _env;
    private string? _version;
    private string? _service;
    private TelemetryClientConfiguration? _telemetryClientConfiguration;
    private bool _computeStats;
    private bool _clientComputedStats;
    private ulong _connectionTimeoutMs;

    public TraceExporterConfiguration()
        : base(IntPtr.Zero, true)
    {
        NativeInterop.Config.New(out var ptr);
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public string Url
    {
        init
        {
            _url = value;
            using var url = new CharSlice(value);
            using var error = NativeInterop.Config.SetUrl(this, url);
            error.ThrowIfError();
        }
    }

    public string TraceVersion
    {
        init
        {
            _traceVersion = value;
            using var tracerVersion = new CharSlice(value);
            using var error = NativeInterop.Config.SetTracerVersion(this, tracerVersion);
            error.ThrowIfError();
        }
    }

    public string Language
    {
        init
        {
            _language = value;
            using var language = new CharSlice(value);
            using var error = NativeInterop.Config.SetLanguage(this, language);
            error.ThrowIfError();
        }
    }

    public string LanguageVersion
    {
        init
        {
            _languageVersion = value;
            using var languageVersion = new CharSlice(value);
            using var error = NativeInterop.Config.SetLanguageVersion(this, languageVersion);
            error.ThrowIfError();
        }
    }

    public string LanguageInterpreter
    {
        init
        {
            _languageInterpreter = value;
            using var interpreter = new CharSlice(value);
            using var error = NativeInterop.Config.SetInterpreter(this, interpreter);
            error.ThrowIfError();
        }
    }

    public string? Hostname
    {
        init
        {
            _hostname = value;
            using var hostname = new CharSlice(value);
            using var error = NativeInterop.Config.SetHostname(this, hostname);
            error.ThrowIfError();
        }
    }

    public string? Env
    {
        init
        {
            _env = value;
            using var env = new CharSlice(value);
            using var error = NativeInterop.Config.SetEnv(this, env);
            error.ThrowIfError();
        }
    }

    public string? Version
    {
        init
        {
            _version = value;
            using var version = new CharSlice(value);
            using var error = NativeInterop.Config.SetVersion(this, version);
            error.ThrowIfError();
        }
    }

    public string? Service
    {
        init
        {
            _service = value;
            using var service = new CharSlice(value);
            using var error = NativeInterop.Config.SetService(this, service);
            error.ThrowIfError();
        }
    }

    public TelemetryClientConfiguration? TelemetryClientConfiguration
    {
        init
        {
            _telemetryClientConfiguration = value;
            if (value.HasValue)
            {
                _telemetryConfigPtr = Marshal.AllocHGlobal(Marshal.SizeOf(value.Value));
                Marshal.StructureToPtr(value.Value, _telemetryConfigPtr, false);
                using var error = NativeInterop.Config.EnableTelemetry(this, _telemetryConfigPtr);
                error.ThrowIfError();
            }
        }
    }

    public bool ComputeStats
    {
        init
        {
            _computeStats = value;
            using var error = NativeInterop.Config.SetComputeStats(this, value);
            error.ThrowIfError();
        }
    }

    public bool ClientComputedStats
    {
        init
        {
            _clientComputedStats = value;
            using var error = NativeInterop.Config.SetClientComputedStats(this, value);
            error.ThrowIfError();
        }
    }

    public ulong ConnectionTimeoutMs
    {
        init
        {
            _connectionTimeoutMs = value;
            using var error = NativeInterop.Config.SetConnectionTimeout(this, value);
            error.ThrowIfError();
        }
    }

    protected override bool ReleaseHandle()
    {
        if (_telemetryConfigPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_telemetryConfigPtr);
            _telemetryConfigPtr = IntPtr.Zero;
        }

        try
        {
            NativeInterop.Config.Free(handle);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while releasing the handle for TraceExporterConfiguration.");
        }

        return true;
    }

    public override string ToString()
    {
        return $"Url: {_url}, " +
               $"TraceVersion: {_traceVersion}, " +
               $"Language: {_language}, " +
               $"LanguageVersion: {_languageVersion}, " +
               $"LanguageInterpreter: {_languageInterpreter}, " +
               $"Hostname: {_hostname}, " +
               $"Env: {_env}, " +
               $"Version: {_version}, " +
               $"Service: {_service}, " +
               $"TelemetryClientConfiguration: {_telemetryClientConfiguration}, " +
               $"ComputeStats: {_computeStats}, " +
               $"ClientComputedStats: {_clientComputedStats}, " +
               $"ConnectionTimeoutMs: {_connectionTimeoutMs}";
    }
}
