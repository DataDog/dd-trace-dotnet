// <copyright file="ConfigTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry
{
    internal static class ConfigTelemetryData
    {
        public const string AgentTraceTransport = "agent_transport";
        public const string NativeTracerVersion = "native_tracer_version";
        public const string ManagedTracerTfm = "managed_tracer_framework";
        public const string AasConfigurationError = "aas_configuration_error";
        public const string FullTrustAppDomain = "environment_fulltrust_appdomain";

        public const string CloudHosting = "cloud_hosting";
        public const string AasAppType = "aas_app_type";

        public const string ProfilerLoaded = "profiler_loaded";
        public const string CodeHotspotsEnabled = "code_hotspots_enabled";

        public const string SsiInjectionEnabled = "ssi_injection_enabled";
        public const string SsiAllowUnsupportedRuntimesEnabled = "ssi_forced_injection_enabled";

        // We intentionally are using specific values here, not OR_GREATER_THAN
#if NET6_0
        public const string ManagedTracerTfmValue = "net6.0";
#elif NETCOREAPP3_1
        public const string ManagedTracerTfmValue = "netcoreapp3.1";
#elif NETSTANDARD2_0
        public const string ManagedTracerTfmValue = "netstandard2.0";
#elif NETFRAMEWORK
        public const string ManagedTracerTfmValue = "net461";
#else
#error Unexpected TFM
#endif
    }
}
