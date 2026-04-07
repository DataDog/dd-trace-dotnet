// <copyright file="OtlpSignalType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer
{
    /// <summary>
    /// Signal type enumeration for OTLP (stub for vendored protobuf serializer use).
    /// This is a stub to avoid vendoring the full OtlpSignalType from OpenTelemetry SDK.
    /// </summary>
    internal enum OtlpSignalType
    {
        /// <summary>
        /// Undefined signal type.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Trace signal type.
        /// </summary>
        Trace = 1,

        /// <summary>
        /// Metrics signal type.
        /// </summary>
        Metrics = 2,

        /// <summary>
        /// Logs signal type.
        /// </summary>
        Logs = 3,
    }
}

#endif

