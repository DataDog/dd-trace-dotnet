// <copyright file="ApmTracingConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents an APM_TRACING configuration with targeting information
    /// </summary>
    internal class ApmTracingConfig
    {
        public ApmTracingConfig(string configId, ServiceTarget? serviceTarget, LibConfig libConfig)
        {
            ConfigId = configId;
            ServiceTarget = serviceTarget;
            LibConfig = libConfig;
        }

        public string ConfigId { get; }

        public ServiceTarget? ServiceTarget { get; }

        public LibConfig LibConfig { get; }

        /// <summary>
        /// Gets the priority of this configuration based on targeting specificity.
        /// Higher values = higher priority.
        /// </summary>
        public int Priority
        {
            get
            {
                if (ServiceTarget == null)
                {
                    return 0; // Org level (lowest priority)
                }

                var hasService = !string.IsNullOrEmpty(ServiceTarget.Service) && ServiceTarget.Service != "*";
                var hasEnv = !string.IsNullOrEmpty(ServiceTarget.Env) && ServiceTarget.Env != "*";

                return new ConfigurationTarget(hasService, hasEnv) switch
                {
                    (true, true) => 4, // Service+env (highest priority)
                    (true, false) => 3, // Service only
                    (false, true) => 2, // Env only
                    (false, false) => 1 // Cluster target or wildcard
                };
            }
        }

        /// <summary>
        /// Checks if this configuration matches the current service and environment
        /// </summary>
        public bool Matches(string serviceName, string environment)
        {
            if (ServiceTarget == null)
            {
                return true; // Org-level config matches everything
            }

            var serviceMatches = string.IsNullOrEmpty(ServiceTarget.Service) ||
                                ServiceTarget.Service == "*" ||
                                ServiceTarget.Service == serviceName;

            var envMatches = string.IsNullOrEmpty(ServiceTarget.Env) ||
                            ServiceTarget.Env == "*" ||
                            ServiceTarget.Env == environment;

            return serviceMatches && envMatches;
        }

        /// <summary>
        /// Merges this configuration with another, giving priority to the higher priority config
        /// </summary>
        public ApmTracingConfig MergeWith(ApmTracingConfig other)
        {
            var higherPriority = this.Priority >= other.Priority ? this : other;
            var lowerPriority = this.Priority >= other.Priority ? other : this;

            return new ApmTracingConfig(
                higherPriority.ConfigId,
                higherPriority.ServiceTarget,
                MergeLibConfigs(higherPriority.LibConfig, lowerPriority.LibConfig));
        }

        private static LibConfig MergeLibConfigs(LibConfig higher, LibConfig lower)
        {
            return new LibConfig
            {
                TracingEnabled = higher.TracingEnabled ?? lower.TracingEnabled,
                LogInjectionEnabled = higher.LogInjectionEnabled ?? lower.LogInjectionEnabled,
                TracingSamplingRate = higher.TracingSamplingRate ?? lower.TracingSamplingRate,
                TracingSamplingRules = higher.TracingSamplingRules ?? lower.TracingSamplingRules,
                TracingHeaderTags = higher.TracingHeaderTags ?? lower.TracingHeaderTags,
                TracingTags = higher.TracingTags ?? lower.TracingTags,
            };
        }

        internal record struct ConfigurationTarget(bool HasService, bool HasEnv);
    }

    internal class ApmTracingConfigDto
    {
        [JsonProperty("service_target")]
        public ServiceTarget? ServiceTarget { get; set; }

        [JsonProperty("lib_config")]
        public LibConfig? LibConfig { get; set; }
    }

    /// <summary>
    /// Represents the service_target field in APM_TRACING configuration
    /// </summary>
    internal class ServiceTarget
    {
        [JsonProperty("service")]
        public string? Service { get; set; }

        [JsonProperty("env")]
        public string? Env { get; set; }
    }

    /// <summary>
    /// Represents the lib_config field in APM_TRACING configuration
    /// </summary>
    internal class LibConfig
    {
        [JsonProperty("tracing_enabled")]
        public bool? TracingEnabled { get; set; }

        [JsonProperty("log_injection_enabled")]
        public bool? LogInjectionEnabled { get; set; }

        [JsonProperty("tracing_sampling_rate")]
        public double? TracingSamplingRate { get; set; }

        [JsonProperty("tracing_sampling_rules")]
        public string? TracingSamplingRules { get; set; }

        [JsonProperty("tracing_header_tags")]
        public string? TracingHeaderTags { get; set; }

        [JsonProperty("tracing_tags")]
        public string? TracingTags { get; set; }
    }
}
