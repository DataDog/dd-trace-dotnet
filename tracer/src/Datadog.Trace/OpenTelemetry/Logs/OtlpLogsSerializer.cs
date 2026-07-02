// <copyright file="OtlpLogsSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpCommonFieldNumberConstants;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpLogFieldNumberConstants;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Logs;

/// <summary>
/// Serializes a collection of LogPoints to OTLP protobuf format using vendored OpenTelemetry protobuf utilities.
/// </summary>
internal static class OtlpLogsSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    /// <summary>
    /// Serializes logs to OTLP LogsData binary format into a caller-provided buffer.
    /// </summary>
    /// <remarks>
    /// The caller owns <paramref name="buffer"/> (typically rented from an <see cref="System.Buffers.ArrayPool{T}"/>).
    /// If the batch doesn't fit, this returns <c>false</c> without throwing; the caller should retry
    /// with a larger buffer. It never resizes the buffer itself.
    /// </remarks>
    /// <param name="logs">The batch of logs to serialize.</param>
    /// <param name="buffer">The destination buffer to serialize into.</param>
    /// <param name="settings">Resource-level tags applied to the payload.</param>
    /// <param name="bytesWritten">The number of bytes written (the payload length), or 0 if the batch didn't fit.</param>
    /// <param name="startPosition">Offset at which to start writing (e.g. a reserved gRPC frame header).</param>
    /// <returns><c>true</c> if the batch was serialized into <paramref name="buffer"/>; <c>false</c> if it didn't fit.</returns>
    public static bool TrySerializeLogs(IReadOnlyList<LogPoint> logs, byte[] buffer, ResourceTags settings, out int bytesWritten, int startPosition = 0)
    {
        bytesWritten = 0;

        if (logs.Count == 0)
        {
            return true;
        }

        try
        {
            bytesWritten = SerializeLogs(buffer, logs, settings, startPosition);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            // A span/array write ran past the end of the buffer: it was too small for this batch.
            // Signal the caller to retry with a larger buffer rather than surfacing the exception.
            bytesWritten = 0;
            return false;
        }
    }

    private static int SerializeLogs(byte[] buffer, IReadOnlyList<LogPoint> logs, ResourceTags settings, int startPosition)
    {
        int writePosition = startPosition;

        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, LogsData_Resource_Logs, ProtobufWireType.LEN);
        int resourceLogsLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = WriteResourceLogs(buffer, writePosition, logs, settings);

        ProtobufSerializer.WriteReservedLength(buffer, resourceLogsLengthPosition, writePosition - (resourceLogsLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    private static int WriteResourceLogs(byte[] buffer, int writePosition, IReadOnlyList<LogPoint> logs, ResourceTags settings)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ResourceLogs_Resource, ProtobufWireType.LEN);
        int resourceLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = WriteResource(buffer, writePosition, settings);

        ProtobufSerializer.WriteReservedLength(buffer, resourceLengthPosition, writePosition - (resourceLengthPosition + ReserveSizeForLength));

        var logsByScope = new Dictionary<string, List<LogPoint>>();
        foreach (var log in logs)
        {
            var scopeName = log.CategoryName ?? string.Empty;
            if (!logsByScope.TryGetValue(scopeName, out var scopeLogs))
            {
                scopeLogs = new List<LogPoint>();
                logsByScope[scopeName] = scopeLogs;
            }

            scopeLogs.Add(log);
        }

        foreach (var scope in logsByScope)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ResourceLogs_Scope_Logs, ProtobufWireType.LEN);
            int scopeLogsLengthPosition = writePosition;
            writePosition += ReserveSizeForLength;

            writePosition = WriteScopeLogs(buffer, writePosition, scope.Key, scope.Value);

            ProtobufSerializer.WriteReservedLength(buffer, scopeLogsLengthPosition, writePosition - (scopeLogsLengthPosition + ReserveSizeForLength));
        }

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ResourceLogs_Schema_Url, string.Empty);

        return writePosition;
    }

    private static int WriteResource(byte[] buffer, int writePosition, ResourceTags settings)
    {
        var serviceName = settings.ServiceName ?? "unknown_service:dotnet";
        writePosition = WriteResourceAttribute(buffer, writePosition, "service.name", serviceName);

        if (!StringUtil.IsNullOrEmpty(settings.ServiceVersion))
        {
            writePosition = WriteResourceAttribute(buffer, writePosition, "service.version", settings.ServiceVersion);
        }

        if (!StringUtil.IsNullOrEmpty(settings.Environment))
        {
            writePosition = WriteResourceAttribute(buffer, writePosition, "deployment.environment", settings.Environment);
        }

        // Write telemetry SDK attributes
        writePosition = WriteResourceAttribute(buffer, writePosition, "telemetry.sdk.name", "datadog");
        writePosition = WriteResourceAttribute(buffer, writePosition, "telemetry.sdk.language", "dotnet");
        writePosition = WriteResourceAttribute(buffer, writePosition, "telemetry.sdk.version", TracerConstants.AssemblyVersion);

        if (settings.GlobalTags.Count > 0)
        {
            foreach (var tag in settings.GlobalTags)
            {
                if (IsHandledResourceAttribute(tag.Key))
                {
                    continue;
                }

                writePosition = WriteResourceAttribute(buffer, writePosition, tag.Key, tag.Value);
            }
        }

        return writePosition;
    }

    private static int WriteScopeLogs(byte[] buffer, int writePosition, string scopeName, List<LogPoint> logs)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ScopeLogs_Scope, ProtobufWireType.LEN);
        int scopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, InstrumentationScope_Name, scopeName);
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, InstrumentationScope_Version, string.Empty);

        ProtobufSerializer.WriteReservedLength(buffer, scopeLengthPosition, writePosition - (scopeLengthPosition + ReserveSizeForLength));

        for (int i = 0; i < logs.Count; i++)
        {
            writePosition = WriteLogRecord(buffer, writePosition, logs[i]);
        }

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ScopeLogs_Schema_Url, string.Empty);

        return writePosition;
    }

    private static int WriteLogRecord(byte[] buffer, int writePosition, LogPoint log)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ScopeLogs_Log_Records, ProtobufWireType.LEN);
        int logRecordLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        var timeUnixNano = (ulong)log.Timestamp.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, LogRecord_Time_Unix_Nano, timeUnixNano);
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, LogRecord_Observed_Time_Unix_Nano, timeUnixNano);
        writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, LogRecord_Severity_Number, log.GetSeverityNumber());
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, LogRecord_Severity_Text, log.GetSeverityText());
        writePosition = WriteLogRecordBody(buffer, writePosition, log.Message);

        foreach (var attr in log.Attributes)
        {
            writePosition = WriteKeyValueAttribute(buffer, writePosition, attr.Key, attr.Value?.ToString() ?? string.Empty);
        }

        if (log.TraceId != TraceId.Zero)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, LogRecord_Trace_Id, ProtobufWireType.LEN);
            writePosition = ProtobufSerializer.WriteLength(buffer, writePosition, TraceIdSize);
            writePosition = WriteTraceId(buffer, writePosition, log.TraceId);
        }

        if (log.SpanId != 0)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, LogRecord_Span_Id, ProtobufWireType.LEN);
            writePosition = ProtobufSerializer.WriteLength(buffer, writePosition, SpanIdSize);
            writePosition = WriteSpanId(buffer, writePosition, log.SpanId);
        }

        writePosition = ProtobufSerializer.WriteFixed32WithTag(buffer, writePosition, LogRecord_Flags, (uint)log.Flags);

        if (!StringUtil.IsNullOrEmpty(log.Source))
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, LogRecord_Event_Name, log.Source!);
        }

        ProtobufSerializer.WriteReservedLength(buffer, logRecordLengthPosition, writePosition - (logRecordLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    private static int WriteLogRecordBody(byte[] buffer, int writePosition, string value)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, LogRecord_Body, ProtobufWireType.LEN);
        int bodyLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, AnyValue_String_Value, value);

        ProtobufSerializer.WriteReservedLength(buffer, bodyLengthPosition, writePosition - (bodyLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    private static int WriteResourceAttribute(byte[] buffer, int writePosition, string key, string value)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, Resource_Attributes, ProtobufWireType.LEN);
        int attributeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, KeyValue_Key, key);

        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, KeyValue_Value, ProtobufWireType.LEN);
        int valueLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, AnyValue_String_Value, value);

        ProtobufSerializer.WriteReservedLength(buffer, valueLengthPosition, writePosition - (valueLengthPosition + ReserveSizeForLength));

        ProtobufSerializer.WriteReservedLength(buffer, attributeLengthPosition, writePosition - (attributeLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    private static int WriteKeyValueAttribute(byte[] buffer, int writePosition, string key, string value)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, LogRecord_Attributes, ProtobufWireType.LEN);
        int attributeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, KeyValue_Key, key);

        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, KeyValue_Value, ProtobufWireType.LEN);
        int valueLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, AnyValue_String_Value, value);

        ProtobufSerializer.WriteReservedLength(buffer, valueLengthPosition, writePosition - (valueLengthPosition + ReserveSizeForLength));

        ProtobufSerializer.WriteReservedLength(buffer, attributeLengthPosition, writePosition - (attributeLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    private static int WriteTraceId(byte[] buffer, int writePosition, TraceId traceId)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            new Span<byte>(buffer, writePosition, 8),
            traceId.Upper);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            new Span<byte>(buffer, writePosition + 8, 8),
            traceId.Lower);

        return writePosition + TraceIdSize;
    }

    private static int WriteSpanId(byte[] buffer, int writePosition, ulong spanId)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            new System.Span<byte>(buffer, writePosition, 8),
            spanId);

        return writePosition + SpanIdSize;
    }

    private static bool IsHandledResourceAttribute(string tagKey)
    {
        return tagKey.Equals("service", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("env", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("version", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("service.name", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("deployment.environment.name", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("deployment.environment", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("service.version", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class ResourceTags(string serviceName, string? environment, string? serviceVersion, ReadOnlyDictionary<string, string> globalTags)
    {
        public string ServiceName { get; } = serviceName;

        public string? Environment { get; } = environment;

        public string? ServiceVersion { get; } = serviceVersion;

        public ReadOnlyDictionary<string, string> GlobalTags { get; } = globalTags;
    }
}
#endif
