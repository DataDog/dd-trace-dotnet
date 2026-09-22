// <copyright file="StatusCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// Status code enum.
/// For the semantics of status codes see
/// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status
/// Corresponds to opentelemetry.proto.trace.v1.Status.StatusCode
/// </summary>
internal enum StatusCode
{
    /// <summary>
    /// The default status.
    /// </summary>
    Unset = 0,

    /// <summary>
    /// The Span has been validated by an Application developer or Operator to
    /// have completed successfully.
    /// </summary>
    Ok = 1,

    /// <summary>
    /// The Span contains an error.
    /// </summary>
    Error = 2
}
