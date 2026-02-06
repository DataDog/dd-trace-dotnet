// <copyright file="SpanStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// The Status type defines a logical error model that is suitable for different
/// programming environments, including REST APIs and RPC APIs.
/// Corresponds to opentelemetry.proto.trace.v1.Status
/// </summary>
internal readonly struct SpanStatus
{
    /// <summary>
    /// A developer-facing human readable error message.
    /// Field 2: string message
    /// </summary>
    public readonly string? Message;

    /// <summary>
    /// The status code.
    /// Field 3: StatusCode code
    /// </summary>
    public readonly StatusCode Code;

    public SpanStatus(StatusCode code, string? message = null)
    {
        Code = code;
        Message = message;
    }
}
