// <copyright file="MetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Telemetry;

internal partial class MetricsTelemetryCollector : MetricsTelemetryCollectorBase, IMetricsTelemetryCollector
{
    public MetricsTelemetryCollector()
        : base()
    {
    }

    internal MetricsTelemetryCollector(TimeSpan aggregationInterval, Action? aggregationNotification = null)
        : base(aggregationInterval, aggregationNotification)
    {
    }
}
