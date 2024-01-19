// <copyright file="TelemetryConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryConstants
    {
        public const string ApiVersionV2 = "v2";
        public const string TelemetryPath = "api/v2/apmtelemetry";
        public const string TelemetryIntakePrefix = "https://instrumentation-telemetry-intake";
        public const string AgentTelemetryEndpoint = "telemetry/proxy/";

        public const string ApiKeyHeader = "DD-API-KEY";
        public const string ApiVersionHeader = "DD-Telemetry-API-Version";
        public const string DebugHeader = "DD-Telemetry-Debug-Enabled";
        public const string RequestTypeHeader = "DD-Telemetry-Request-Type";
        public const string ClientLibraryLanguageHeader = "DD-Client-Library-Language";
        public const string ClientLibraryVersionHeader = "DD-Client-Library-Version";
        public const string ContainerIdHeader = Datadog.Trace.AgentHttpHeaderNames.ContainerId;
        public const string EntityIdHeader = Datadog.Trace.AgentHttpHeaderNames.EntityId;

        public const string CloudProviderHeader = "DD-Cloud-Provider";
        public const string CloudResourceTypeHeader = "DD-Cloud-Resource-Type";
        public const string CloudResourceIdentifierHeader = "DD-Cloud-Resource-Identifier";

        public const string GcpServiceVariable = "K_SERVICE";
        public const string AzureContainerAppVariable = "CONTAINER_APP_NAME";
        public const string AzureAppServiceVariable1 = "APPSVC_RUN_ZIP";
        public const string AzureAppServiceVariable2 = "WEBSITE_APPSERVICEAPPLOGS_TRACE_ENABLED";
        public const string AzureAppServiceIdentifierVariable = "WEBSITE_SITE_NAME";

        public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMinutes(1);
    }
}
