// <copyright file="TraceExporterResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.LibDatadog.DataPipeline;

/// <summary>
/// SafeHandle wrapper for trace exporter response from libdatadog
/// </summary>
internal sealed class TraceExporterResponse(IntPtr handle) : SafeHandle(handle, true)
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<TraceExporterResponse>();

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            try
            {
                NativeInterop.Exporter.FreeResponse(handle);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to free TraceExporterResponse handle");
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the response body as a UTF-8 string from the native response.
    /// </summary>
    /// <returns>The response body as a string, or null if the response is invalid or empty</returns>
    public unsafe string? ReadAsString()
    {
        if (IsInvalid)
        {
            return null;
        }

        try
        {
            var len = UIntPtr.Zero;
            var body = NativeInterop.Exporter.GetResponseBody(this, ref len);
            var bodyLen = (ulong)len;

            if (body == IntPtr.Zero || bodyLen == 0)
            {
                return null;
            }

            if (bodyLen > int.MaxValue)
            {
                Logger.Warning("Agent response is too large");
                return null;
            }

            return StringEncoding.UTF8.GetString((byte*)body, (int)bodyLen);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read response body as string");
            return null;
        }
    }
}
