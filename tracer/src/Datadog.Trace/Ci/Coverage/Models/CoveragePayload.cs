// <copyright file="CoveragePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models
{
    /// <summary>
    /// Coverage payload
    /// </summary>
    internal sealed class CoveragePayload : IEvent
    {
        /// <summary>
        /// Gets or sets the trace's unique identifier.
        /// </summary>
        [JsonProperty("trace_id")]
        public ulong TraceId { get; set; }

        /// <summary>
        /// Gets or sets the span's unique identifier.
        /// </summary>
        [JsonProperty("span_id")]
        public ulong SpanId { get; set; }

        /// <summary>
        /// Gets or sets the files with coverage information
        /// </summary>
        [JsonProperty("files")]
        public List<FileCoverage> Files { get; set; } = new();

        /// <summary>
        /// Gets the payload version.
        /// </summary>
        [JsonProperty("version")]
        public int Version { get; } = 1;
    }
}
