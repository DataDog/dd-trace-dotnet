// <copyright file="IMeterSchemaUrlDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Duck type interface for accessing Meter.TelemetrySchemaUrl (available in .NET 10+).
/// Separate from <see cref="IMeterDuck"/> so that Meter.Tags is still read on .NET 8 and 9.
/// </summary>
internal interface IMeterSchemaUrlDuck
{
    string? TelemetrySchemaUrl { get; }
}

#endif
