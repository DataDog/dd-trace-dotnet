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
    private const int TagsCharacterCount = 10; // "tags":"",
    private const int StackTraceCharacterCount = 17; // "stack_trace":"",
    private const int CountCharacterCount = 12; // "count":123, (assuming <1000 count per message)

    private const int FixedSerializationCharacterCount =
        13 /* "message":"", */ +
        16 /* "level":"ERROR", */ +
        25 /* "tracer_time":1700220630, */ +
        1 /* {} (-1 for trailing comma) */;

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
        => Encoding.UTF8.GetMaxByteCount(
            FixedSerializationCharacterCount
          + Message.Length
          + (StackTrace?.Length is { } s ? s + StackTraceCharacterCount : 0)
          + (Tags?.Length is { } t ? t + TagsCharacterCount : 0)
          + (Count.HasValue ? CountCharacterCount : 0));
}
