// <copyright file="ProbeConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal class ProbeConfiguration
    {
        public LogProbe[] LogProbes { get; set; } = Array.Empty<LogProbe>();

        public MetricProbe[] MetricProbes { get; set; } = Array.Empty<MetricProbe>();

        public SpanDecorationProbe[] SpanDecorationProbes { get; set; } = Array.Empty<SpanDecorationProbe>();

        public SpanProbe[] SpanProbes { get; set; } = Array.Empty<SpanProbe>();

        public ServiceConfiguration? ServiceConfiguration { get; set; }
    }
}
