// <copyright file="ConfigTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Telemetry
{
    internal class ConfigTelemetryData
    {
        public string OsName { get; set; }

        public string OsVersion { get; set; }

        public string Platform { get; set; }

        public bool? Enabled { get; set; }

        public string AgentUrl { get; set; }

        public bool? Debug { get; set; }

        public bool? AnalyticsEnabled { get; set; }

        public double? SampleRate { get; set; }

        public string SamplingRules { get; set; }

        public bool? LogInjectionEnabled { get; set; }

        public bool? RuntimeMetricsEnabled { get; set; }

        public bool? RoutetemplateResourcenamesEnabled { get; set; }

        public bool? PartialflushEnabled { get; set; }

        public int? PartialflushMinspans { get; set; }

        public int? TracerInstanceCount { get; set; }

        public string CloudHosting { get; set; }

        public bool? AasConfigurationError { get; set; }

        public string AasSiteExtensionVersion { get; set; }

        public string AasAppType { get; set; }

        public string AasFunctionsRuntimeVersion { get; set; }

        public bool? SecurityEnabled { get; set; }

        public bool? SecurityBlockingEnabled { get; set; }
    }
}
