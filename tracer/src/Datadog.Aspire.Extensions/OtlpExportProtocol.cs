// <copyright file="OtlpExportProtocol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Aspire.Extensions;

/// <summary>
/// Supported by OTLP exporter protocol types according to the specification https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
/// </summary>
public enum OtlpExportProtocol : byte
{
    /// <summary>
    /// OTLP over gRPC (corresponds to 'grpc' Protocol configuration option). Used as default.
    /// </summary>
    Grpc = 0,

    /// <summary>
    /// OTLP over HTTP with protobuf payloads (corresponds to 'http/protobuf' Protocol configuration option).
    /// </summary>
    HttpProtobuf = 1,
}
