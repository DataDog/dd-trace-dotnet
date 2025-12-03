// <copyright file="LogPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Logs;

/// <summary>
/// Represents a single log entry that can be exported via OTLP.
/// This is similar to MetricPoint but for logs data.
/// Uses Datadog's native TraceId (128-bit) and SpanId (64-bit) for trace correlation.
/// </summary>
internal class LogPoint
{
    public string Message { get; set; } = string.Empty;

    public int LogLevel { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string? LoggerName { get; set; }

    public string? CategoryName { get; set; }

    public Dictionary<string, object?> Attributes { get; set; } = new();

    public int Flags { get; set; }

    public TraceId TraceId { get; set; }

    public ulong SpanId { get; set; }

    public Exception? Exception { get; set; }

    public string? Source { get; set; }

    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets the OTLP severity number from Microsoft.Extensions.Logging.LogLevel.
    /// Maps to OpenTelemetry SeverityNumber enum values.
    /// </summary>
    public int GetSeverityNumber()
    {
        // Microsoft.Extensions.Logging.LogLevel → OTLP SeverityNumber
        // https://opentelemetry.io/docs/specs/otel/logs/data-model/#field-severitynumber
        return LogLevel switch
        {
            0 => 1,  // Trace → TRACE
            1 => 5,  // Debug → DEBUG
            2 => 9,  // Information → INFO
            3 => 13, // Warning → WARN
            4 => 17, // Error → ERROR
            5 => 21, // Critical → FATAL
            6 => 0,  // None → UNSPECIFIED
            _ => 9,  // Default to INFO
        };
    }

    /// <summary>
    /// Gets the OTLP severity text from Microsoft.Extensions.Logging.LogLevel.
    /// Returns human-readable log level names matching .NET naming conventions.
    /// </summary>
    public string GetSeverityText()
    {
        // Microsoft.Extensions.Logging.LogLevel → OTLP SeverityText
        return LogLevel switch
        {
            0 => "Trace",
            1 => "Debug",
            2 => "Information",
            3 => "Warning",
            4 => "Error",
            5 => "Critical",
            6 => "None",
            _ => "Information",
        };
    }
}
#endif
