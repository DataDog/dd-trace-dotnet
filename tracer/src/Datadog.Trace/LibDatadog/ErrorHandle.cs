// <copyright file="ErrorHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

internal class ErrorHandle : SafeHandle
{
    public ErrorHandle()
        : base(IntPtr.Zero, true)
    {
    }

    public ErrorHandle(IntPtr handle)
        : base(handle, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeInterop.Exporter.FreeError(handle);
        return true;
    }

    public TraceExporterException ToException()
    {
        return new TraceExporterException(Marshal.PtrToStructure<TraceExporterError>(handle));
    }

    public void ThrowIfError()
    {
        if (!IsInvalid)
        {
            throw ToException();
        }
    }
}
