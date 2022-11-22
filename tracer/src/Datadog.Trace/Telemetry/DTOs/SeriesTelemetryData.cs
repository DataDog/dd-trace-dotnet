// <copyright file="SeriesTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Telemetry
{
    /// <summary>
    /// Using a record as used as dictionary key so getting equality comparison for free
    /// </summary>
    internal record SeriesTelemetryData
    {
        public SeriesTelemetryData(string @namespace, string metric, ICollection<double[]> points, ICollection<string> tags, string type, bool common)
        {
            Namespace = @namespace;
            Metric = metric;
            Points = points;
            Tags = tags;
            Type = type;
            Common = common;
        }

        public string Namespace { get; set; }

        public string Metric { get; set; }

        public ICollection<double[]> Points { get; set; }

        public ICollection<string> Tags { get; set; }

        public string Type { get; set; }

        public bool Common { get; set; }
    }
}
