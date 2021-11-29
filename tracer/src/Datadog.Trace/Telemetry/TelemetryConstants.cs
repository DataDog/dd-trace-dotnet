// <copyright file="TelemetryConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryConstants
    {
        public const string ApiVersion = "v1";
        public const string TelemetryPath = "/api/v2/apmtelemetry";
        public const string AgentTelemetryEndpoint = "telemetry/proxy";

        public const string ApiVersionHeader = "DD-Telemetry-API-Version";
        public const string RequestTypeHeader = "DD-Telemetry-Request-Type";

        public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);
    }
}
