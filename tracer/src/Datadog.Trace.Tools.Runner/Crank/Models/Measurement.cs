// <copyright file="Measurement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class Measurement
    {
        public const string Delimiter = "$$Delimiter$$";

        public DateTimeOffset Timestamp { get; set; }

        public string Name { get; set; }

        public object Value { get; set; }

        public bool IsDelimiter => string.Equals(Name, Delimiter, StringComparison.OrdinalIgnoreCase);
    }
}
