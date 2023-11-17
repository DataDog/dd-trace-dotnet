// <copyright file="LogMessageData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Telemetry.DTOs;

internal class LogMessageData
{
    private const int FixedSerializationCharacterCount =
        7 /*message*/ +
        5 /*level*/ +
        5 /*level value*/ +
        4 /*tags*/ +
        11 /*stack_trace*/ +
        11 /* tracer_time */ +
        10 /* tracer_time value */ +
        5 /* count */ +
        3 /* count value (assuming <1000 per message) */ +
        10 /* tracer_time value */ +
        15 /* json ,"{}  etc*/;

    public LogMessageData(string message, TelemetryLogLevel level, DateTimeOffset timestamp)
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

    /// <summary>
    /// Gets or Sets the number of occurrences of the log message
    /// </summary>
    public int? Count { get; set; }

    public int GetApproximateSerializationSize()
        => Encoding.UTF8.GetMaxByteCount(FixedSerializationCharacterCount + Message.Length + (StackTrace?.Length ?? 0) + (Tags?.Length ?? 0));
}
