// <copyright file="TraceExporterErrorHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.DataPipeline;

internal sealed class TraceExporterErrorHandle : SafeHandle
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<TraceExporterErrorHandle>();

    public TraceExporterErrorHandle()
        : base(IntPtr.Zero, true)
    {
    }

    public TraceExporterErrorHandle(IntPtr handle)
        : base(handle, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        try
        {
            NativeInterop.Exporter.FreeError(handle);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while releasing the handle for TraceExporterErrorHandle.");
        }

        return true;
    }

    public TraceExporterError ToError()
    {
        return Marshal.PtrToStructure<TraceExporterError>(handle);
    }

    public void ThrowIfError()
    {
        if (!IsInvalid)
        {
            var error = ToError();
            throw new TraceExporterException(error);
        }
    }
}
