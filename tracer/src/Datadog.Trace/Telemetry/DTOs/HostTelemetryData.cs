// <copyright file="HostTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry
{
    internal class HostTelemetryData
    {
        public string? ContainerId { get; set; }

        public string? Hostname { get; set; }

        public string? Os { get; set; }

        public string? OsVersion { get; set; }

        public string? KernelName { get; set; }

        public string? KernelRelease { get; set; }

        public string? KernelVersion { get; set; }
    }
}
