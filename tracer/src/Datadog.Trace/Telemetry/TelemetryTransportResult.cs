// <copyright file="TelemetryTransportResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Telemetry;

internal enum TelemetryTransportResult
{
    /// <summary>
    /// Data was sent successfully
    /// </summary>
    Success,

    /// <summary>
    /// Data was not sent successfully, and should be sent in next interval
    /// </summary>
    TransientError,

    /// <summary>
    /// Data was not sent. Data should be discarded and no further telemetry requests should be made
    /// </summary>
    FatalError
}
