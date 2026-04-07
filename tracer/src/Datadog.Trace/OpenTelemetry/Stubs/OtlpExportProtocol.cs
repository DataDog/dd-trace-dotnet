// <copyright file="OtlpExportProtocol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// OtlpExportProtocol enum from OpenTelemetry.Exporter.OpenTelemetryProtocol.
    /// We keep this in our stub because the original file is in the parent namespace
    /// OpenTelemetry.Exporter which would require additional namespace rewriting rules.
    /// Values match OpenTelemetry's enum (Grpc=0, HttpProtobuf=1).
    /// </summary>
    internal enum OtlpExportProtocol : byte
    {
        Grpc = 0,
        HttpProtobuf = 1,
    }
}
#endif
