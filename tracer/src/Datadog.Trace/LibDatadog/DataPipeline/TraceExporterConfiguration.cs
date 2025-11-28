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
internal sealed class TraceExporterConfiguration : SafeHandle
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<TraceExporterConfiguration>();

    private IntPtr _telemetryConfigPtr;

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
            using var url = new CharSlice(value);
            using var error = NativeInterop.Config.SetUrl(this, url);
            error.ThrowIfError();
        }
    }

    public string TraceVersion
    {
        init
        {
            using var tracerVersion = new CharSlice(value);
            using var error = NativeInterop.Config.SetTracerVersion(this, tracerVersion);
            error.ThrowIfError();
        }
    }

    public string Language
    {
        init
        {
            using var language = new CharSlice(value);
            using var error = NativeInterop.Config.SetLanguage(this, language);
            error.ThrowIfError();
        }
    }

    public string LanguageVersion
    {
        init
        {
            using var languageVersion = new CharSlice(value);
            using var error = NativeInterop.Config.SetLanguageVersion(this, languageVersion);
            error.ThrowIfError();
        }
    }

    public string LanguageInterpreter
    {
        init
        {
            using var interpreter = new CharSlice(value);
            using var error = NativeInterop.Config.SetInterpreter(this, interpreter);
            error.ThrowIfError();
        }
    }

    public string? Hostname
    {
        init
        {
            using var hostname = new CharSlice(value);
            using var error = NativeInterop.Config.SetHostname(this, hostname);
            error.ThrowIfError();
        }
    }

    public string? Env
    {
        init
        {
            using var env = new CharSlice(value);
            using var error = NativeInterop.Config.SetEnv(this, env);
            error.ThrowIfError();
        }
    }

    public string? Version
    {
        init
        {
            using var version = new CharSlice(value);
            using var error = NativeInterop.Config.SetVersion(this, version);
            error.ThrowIfError();
        }
    }

    public string? Service
    {
        init
        {
            using var service = new CharSlice(value);
            using var error = NativeInterop.Config.SetService(this, service);
            error.ThrowIfError();
        }
    }

    public TelemetryClientConfiguration? TelemetryClientConfiguration
    {
        init
        {
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
            using var error = NativeInterop.Config.SetComputeStats(this, value);
            error.ThrowIfError();
        }
    }

    public bool ClientComputedStats
    {
        init
        {
            using var error = NativeInterop.Config.SetClientComputedStats(this, value);
            error.ThrowIfError();
        }
    }

    public ulong ConnectionTimeoutMs
    {
        init
        {
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
}
