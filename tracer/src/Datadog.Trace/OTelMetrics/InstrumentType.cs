// <copyright file="InstrumentType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

namespace Datadog.Trace.OTelMetrics;

/// <summary>
/// Represents the type of OpenTelemetry instrument
/// </summary>
internal enum InstrumentType
{
    /// <summary>
    /// Counter instrument - monotonic, additive
    /// </summary>
    Counter = 0,

    /// <summary>
    /// UpDownCounter instrument - non-monotonic, additive
    /// </summary>
    UpDownCounter = 1,

    /// <summary>
    /// Histogram instrument - statistical distribution
    /// </summary>
    Histogram = 2,

    /// <summary>
    /// Asynchronous Counter instrument - monotonic, additive, callback-based
    /// </summary>
    ObservableCounter = 3,

    /// <summary>
    /// Asynchronous UpDownCounter instrument - non-monotonic, additive, callback-based
    /// </summary>
    ObservableUpDownCounter = 4,

    /// <summary>
    /// Gauge instrument - non-additive, last value
    /// </summary>
    Gauge = 5,

    /// <summary>
    /// Asynchronous Gauge instrument - non-additive, last value, callback-based
    /// </summary>
    ObservableGauge = 6
}
#endif
