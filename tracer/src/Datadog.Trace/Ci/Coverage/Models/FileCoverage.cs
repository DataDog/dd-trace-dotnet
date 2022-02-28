// <copyright file="FileCoverage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Ci.Coverage.Models
{
    /// <summary>
    /// Source file with executable code
    /// </summary>
    internal sealed class FileCoverage
    {
        /// <summary>
        /// Gets or sets path/name of the file
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the limits of regions with executable code, where region begin/ends or changes count
        /// </summary>
        public List<uint[]> Boundaries { get; set; } = new();
    }
}
