// <copyright file="DiagnosticLogMessageData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Telemetry.DTOs;

internal class DiagnosticLogMessageData
{
    public DiagnosticLogMessageData(string message, TelemetryLogLevel level, long timestamp)
    {
        Message = message;
        Level = level;
        TracerTime = timestamp.ToUnixTimeSeconds();
    }

    public string Message { get; set; }

    [Vendors.Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public TelemetryLogLevel Level { get; set; }

    public string? Tags { get; set; }

    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets unix timestamp (in seconds) for the log
    /// </summary>
    public long TracerTime { get; set; }
}
