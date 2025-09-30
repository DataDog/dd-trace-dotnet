// <copyright file="OtlpProtocol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

/// <summary>
/// Defines the available OTLP protocol types available for exporting.
/// </summary>
internal enum OtlpProtocol
{
    /// <summary>
    /// gRPC with Protocol Buffers encoding
    /// </summary>
    Grpc = 0,

    /// <summary>
    /// HTTP with Protocol Buffers encoding
    /// </summary>
    HttpProtobuf = 1,

    /// <summary>
    /// HTTP with JSON encoding
    /// </summary>
    HttpJson = 2,
}
