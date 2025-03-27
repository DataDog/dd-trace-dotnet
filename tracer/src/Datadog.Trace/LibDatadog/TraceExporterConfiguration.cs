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
        NativeInterop.TraceExporterConfig.New(out var ptr);
        SetHandle(ptr);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public string Url
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetUrl(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string TraceVersion
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetTracerVersion(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string Language
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetLanguage(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string LanguageVersion
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetLanguageVersion(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string LanguageInterpreter
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetInterpreter(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Hostname
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetHostname(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Env
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetEnv(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Version
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetVersion(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    public string? Service
    {
        init
        {
            using var error = NativeInterop.TraceExporterConfig.SetService(this, new CharSlice(value));
            error.ThrowIfError();
        }
    }

    protected override bool ReleaseHandle()
    {
        NativeInterop.TraceExporterConfig.Free(handle);
        return true;
    }
}
