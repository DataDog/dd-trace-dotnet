// <copyright file="MetricData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.OTelMetrics
{
    internal class MetricData
    {
        public string InstrumentName { get; set; } = string.Empty;

        public string MeterName { get; set; } = string.Empty;

        public string InstrumentType { get; set; } = string.Empty;

        public object Value { get; set; } = 0;

        public Dictionary<string, object?> Tags { get; set; } = new();

        public DateTimeOffset Timestamp { get; set; }
    }
}
#endif
