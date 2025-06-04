// <copyright file="TelemetryMetric.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class TelemetryMetric
    {
        private List<Tuple<string, string>> _tags;
        private List<Tuple<double, double>> _points;

        public TelemetryMetric(string name, List<Tuple<string, string>> tags, List<Tuple<double, double>> values)
        {
            Name = name;
            _tags = tags;
            _points = values;
        }

        public string Name { get; }
        public List<Tuple<string, string>> Tags { get => _tags; }
        public List<Tuple<double, double>> Points { get => _points; }
    }
}
