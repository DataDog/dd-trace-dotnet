// <copyright file="JobResults.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class JobResults
    {
        public Dictionary<string, JobResult> Jobs { get; set; } = new();

        public Dictionary<string, string> Properties { get; set; } = new();
    }
}
