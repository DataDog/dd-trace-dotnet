// <copyright file="LogMessageData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Telemetry;

internal class LogMessageData
{
    public LogMessageData(string message, TelemetryLogLevel level)
    {
        Message = message;
        Level = level;
    }

    public string Message { get; set; }

    [Vendors.Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public TelemetryLogLevel Level { get; set; }

    public string? Tags { get; set; }

    public string? StackTrace { get; set; }
}
