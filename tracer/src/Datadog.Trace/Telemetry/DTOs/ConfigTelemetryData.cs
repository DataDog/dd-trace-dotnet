// <copyright file="ConfigTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry
{
    internal static class ConfigTelemetryData
    {
        public const string Platform = "platform";
        public const string Enabled = "enabled";
        public const string AgentUrl = "agent_url";
        public const string Debug = "debug";
        public const string AnalyticsEnabled = "analytics_enabled";
        public const string SampleRate = "sample_rate";
        public const string SamplingRules = "sampling_rules";
        public const string LogInjectionEnabled = "logInjection_enabled";
        public const string RuntimeMetricsEnabled = "runtimemetrics_enabled";
        public const string RoutetemplateResourcenamesEnabled = "routetemplate_resourcenames_enabled";
        public const string PartialflushEnabled = "partialflush_enabled";
        public const string PartialflushMinspans = "partialflush_minspans";
        public const string TracerInstanceCount = "tracer_instance_count";
        public const string AasConfigurationError = "aas_configuration_error";
        public const string SecurityEnabled = "security_enabled";
        public const string SecurityBlockingEnabled = "security_blocking_enabled";
        public const string FullTrustAppDomain = "environment_fulltrust_appdomain";

        public const string CloudHosting = "cloud_hosting";
        public const string AasSiteExtensionVersion = "aas_siteextensions_version";
        public const string AasAppType = "aas_app_type";
        public const string AasFunctionsRuntimeVersion = "aas_functions_runtime_version";
    }
}
