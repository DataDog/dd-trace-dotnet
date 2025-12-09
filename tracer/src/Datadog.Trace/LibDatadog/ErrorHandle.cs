// <copyright file="ErrorHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog;

internal sealed class ErrorHandle() : SafeHandle(IntPtr.Zero, true)
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<ErrorHandle>();

    public override bool IsInvalid => handle == IntPtr.Zero;

    public void ThrowIfError()
    {
        if (!IsInvalid)
        {
            var error = Marshal.PtrToStructure<Error>(handle);
            throw error.ToException();
        }
    }

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            try
            {
                NativeInterop.Common.Drop(this);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to drop error handle");
            }
        }

        return true;
    }
}
