// <copyright file="IntegrationTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry
{
    internal readonly record struct IntegrationTelemetryData
    {
        public IntegrationTelemetryData(string name, bool enabled, bool? autoEnabled, string? error)
        {
            Name = name;
            Enabled = enabled;
            AutoEnabled = autoEnabled;
            Error = error;
        }

        public string Name { get; }

        public bool Enabled { get; }

        public bool? AutoEnabled { get; }

        public string? Error { get; }
    }
}
