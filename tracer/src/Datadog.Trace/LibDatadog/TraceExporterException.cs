// <copyright file="TraceExporterException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents an exception thrown by the libdatadog library.
/// </summary>
internal class TraceExporterException : Exception
{
    public TraceExporterException(TraceExporterError exporterError)
        : base(Marshal.PtrToStringAnsi(exporterError.Msg))
    {
        ErrorCode = exporterError.Code;
    }

    public ErrorCode ErrorCode { get; }
}
