// <copyright file="TelemetryConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryConstants
    {
        public const string DefaultEndpoint = "https://tracer-telemetry-edge.datadoghq.com/api/v1/apm-app-env";
        public const string TimestampHeader = "DD-Tracer-Timestamp";

        public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);
    }
}
