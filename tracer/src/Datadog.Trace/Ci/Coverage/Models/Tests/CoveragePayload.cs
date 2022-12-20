// <copyright file="CoveragePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Tests
{
    /// <summary>
    /// Coverage payload
    /// </summary>
    internal sealed class CoveragePayload : IEvent
    {
        /// <summary>
        /// Gets or sets the coverages
        /// </summary>
        [JsonProperty("coverages")]
        public List<TestCoverage> Coverages { get; set; } = new();

        /// <summary>
        /// Gets the payload version.
        /// </summary>
        [JsonProperty("version")]
        public int Version { get; } = 2;
    }
}
