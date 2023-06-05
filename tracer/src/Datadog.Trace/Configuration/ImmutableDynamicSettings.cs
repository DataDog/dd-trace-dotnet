// <copyright file="ImmutableDynamicSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    internal record ImmutableDynamicSettings // Declared as record for the equality comparer
    {
        public bool? RuntimeMetricsEnabled { get; init; }

        public bool? DataStreamsMonitoringEnabled { get; init; }

        public string? CustomSamplingRules { get; init; }

        public double? GlobalSamplingRate { get; init; }

        public string? SpanSamplingRules { get; init; }

        public bool? LogsInjectionEnabled { get; init; }

        public IReadOnlyDictionary<string, string>? HeaderTags { get; init; }

        public IDictionary<string, string>? ServiceNameMappings { get; init; }
    }
}
