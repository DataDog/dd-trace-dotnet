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
    private readonly IntPtr _telemetryClientConfigurationHandle;

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
            using var error = NativeInterop.Config.SetUrl(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string TraceVersion
    {
        init
        {
            using var error = NativeInterop.Config.SetTracerVersion(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string Language
    {
        init
        {
            using var error = NativeInterop.Config.SetLanguage(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string LanguageVersion
    {
        init
        {
            using var error = NativeInterop.Config.SetLanguageVersion(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string LanguageInterpreter
    {
        init
        {
            using var error = NativeInterop.Config.SetInterpreter(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Hostname
    {
        init
        {
            using var error = NativeInterop.Config.SetHostname(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Env
    {
        init
        {
            using var error = NativeInterop.Config.SetEnv(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Version
    {
        init
        {
            using var error = NativeInterop.Config.SetVersion(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Service
    {
        init
        {
            using var error = NativeInterop.Config.SetService(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public TelemetryClientConfiguration? TelemetryClientConfiguration
    {
        init
        {
            if (value.HasValue)
            {
                _telemetryClientConfigurationHandle = Marshal.AllocHGlobal(Marshal.SizeOf(value.Value));
                Marshal.StructureToPtr(value.Value, _telemetryClientConfigurationHandle, true);

                using var error = NativeInterop.Config.EnableTelemetry(this, _telemetryClientConfigurationHandle);
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

    protected override bool ReleaseHandle()
    {
        NativeInterop.Config.Free(handle);
        Marshal.FreeHGlobal(_telemetryClientConfigurationHandle);
        return true;
    }
}
