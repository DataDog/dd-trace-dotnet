// <copyright file="TelemetryRequestTypes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Telemetry
{
    internal static class TelemetryRequestTypes
    {
        public const string AppStarted = "app-started";
        public const string AppDependenciesLoaded = "app-dependencies-loaded";
        public const string AppIntegrationsChanged = "app-integrations-change";
        public const string AppClientConfigurationChanged = "app-client-configuration-change";
        public const string AppProductChanged = "app-product-change";
        public const string AppHeartbeat = "app-heartbeat";
        public const string AppClosing = "app-closing";
        public const string AppExtendedHeartbeat = "app-extended-heartbeat";
        // This payload type for redacted error logs
        public const string RedactedErrorLogs = "logs";

        public const string GenerateMetrics = "generate-metrics";
        public const string Distributions = "distributions";

        public const string MessageBatch = "message-batch";
    }
}
