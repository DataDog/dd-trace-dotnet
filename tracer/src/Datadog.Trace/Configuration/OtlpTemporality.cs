// <copyright file="OtlpTemporality.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

/// <summary>
/// Defines the OTLP metrics temporality to use when exporting.
/// </summary>
internal enum OtlpTemporality
{
    /// <summary>
    /// Cumulative preference kind
    /// </summary>
    Cumulative = 0,

    /// <summary>
    /// Delta preference kind
    /// </summary>
    Delta = 1,

    /// <summary>
    /// LowMemory preference kind
    /// </summary>
    LowMemory = 2,
}
