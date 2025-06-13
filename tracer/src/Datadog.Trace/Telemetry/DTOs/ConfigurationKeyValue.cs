// <copyright file="ConfigurationKeyValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry;

/// <summary>
/// V2 implementation of <c>conf_key_value</c>
/// </summary>
internal readonly struct ConfigurationKeyValue
{
    internal ConfigurationKeyValue(string name, object? value, string origin, long seqId, TelemetryErrorCode? error)
        : this(name, value, origin, seqId, error is { } err ? new ErrorData(err) : null)
    {
    }

    private ConfigurationKeyValue(string name, object? value, string origin, long seqId, ErrorData? error)
    {
        Name = name;
        Value = value;
        Origin = origin;
        SeqId = seqId;
        Error = error;
    }

    /// <summary>
    /// Gets the name of the configuration
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the value of the configuration
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the origin of the configuration
    /// </summary>
    public string Origin { get; }

    /// <summary>
    /// Gets the seq ID of the configuration
    /// </summary>
    public long SeqId { get; }

    /// <summary>
    /// Gets the error of the configuration if there was one
    /// </summary>
    public ErrorData? Error { get; }

    // primarily for test purposes
    internal static ConfigurationKeyValue Create(string name, object? value, string origin, long seqId, ErrorData? error)
        => new(name, value, origin, seqId, error);
}
