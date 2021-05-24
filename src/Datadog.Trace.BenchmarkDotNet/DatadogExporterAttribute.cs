// <copyright file="DatadogExporterAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Datadog.Trace.BenchmarkDotNet
{
    /// <summary>
    /// Datadog BenchmarkDotNet exporter
    /// </summary>
    public class DatadogExporterAttribute : ExporterConfigBaseAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DatadogExporterAttribute"/> class.
        /// </summary>
        public DatadogExporterAttribute()
            : base(DatadogExporter.Default)
        {
        }
    }
}
