// <copyright file="ErrorData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry;

/// <summary>
/// An error object for configuration (and other) errors in telemetry
/// </summary>
internal readonly record struct ErrorData
{
    internal ErrorData(TelemetryErrorCode error)
        : this(error, error.ToStringFast())
    {
    }

    internal ErrorData(TelemetryErrorCode code, string message)
    {
        Code = (int)code;
        Message = message;
    }

    /// <summary>
    /// Gets the code of the error
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// Gets the associated message of the error
    /// </summary>
    public string Message { get; }
}
