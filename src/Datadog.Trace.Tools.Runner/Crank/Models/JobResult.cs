// <copyright file="JobResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class JobResult
    {
        public Dictionary<string, object> Results { get; set; } = new();

        public ResultMetadata[] Metadata { get; set; } = Array.Empty<ResultMetadata>();

        public List<Measurement[]> Measurements { get; set; } = new();

        public Dictionary<string, object> Environment { get; set; } = new();
    }
}
