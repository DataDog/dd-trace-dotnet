// <copyright file="IntegrationTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry
{
    internal class IntegrationTelemetryData
    {
        public IntegrationTelemetryData(string name, bool enabled)
        {
            Name = name;
            Enabled = enabled;
        }

        public string Name { get; set; }

        public bool Enabled { get; set; }

        public bool? AutoEnabled { get; set; }

        public bool? Compatible { get; set; }

        public string? Error { get; set; }
    }
}
