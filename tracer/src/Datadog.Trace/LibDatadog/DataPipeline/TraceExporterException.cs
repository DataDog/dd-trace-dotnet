// <copyright file="TraceExporterException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.DataPipeline;

/// <summary>
/// Represents an exception thrown by the libdatadog library.
/// </summary>
internal sealed class TraceExporterException : Exception
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<TraceExporterException>();

    public TraceExporterException(TraceExporterError exporterError)
        : base(Marshal.PtrToStringAnsi(exporterError.Msg))
    {
        if (!Enum.IsDefined(typeof(TraceExporterErrorCode), exporterError.Code))
        {
            Logger.Warning("Invalid TraceExporterErrorCode: {ErrorCode}", exporterError.Code);
        }

        ErrorCode = exporterError.Code;
    }

    public TraceExporterErrorCode ErrorCode { get; }
}
