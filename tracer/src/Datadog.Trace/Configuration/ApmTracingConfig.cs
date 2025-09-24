// <copyright file="ApmTracingConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents an APM_TRACING configuration with targeting information
    /// </summary>
    internal class ApmTracingConfig
    {
        public ApmTracingConfig(string configId, LibConfig libConfig, ServiceTarget? serviceTarget, K8sTargetV2? clusterTarget)
        {
            ConfigId = configId;
            LibConfig = libConfig;
            ServiceTarget = serviceTarget;
            ClusterTarget = clusterTarget;
        }

        public string ConfigId { get; }

        public LibConfig LibConfig { get; }

        public ServiceTarget? ServiceTarget { get; }

        public K8sTargetV2? ClusterTarget { get; }

        /// <summary>
        /// Gets the priority of this configuration based on targeting specificity.
        /// Higher values = higher priority.
        /// </summary>
        public int Priority
        {
            get
            {
                var hasService = !string.IsNullOrEmpty(ServiceTarget?.Service) && ServiceTarget?.Service != "*";
                var hasEnv = !string.IsNullOrEmpty(ServiceTarget?.Env) && ServiceTarget?.Env != "*";
                var hasCluster = ClusterTarget != null;

                return new ConfigurationTarget(hasService, hasEnv, hasCluster) switch
                {
                    (true, true, _) => 5, // Service+env (highest priority)
                    (true, false, _) => 4, // Service only
                    (false, true, _) => 3, // Env only
                    (false, false, true) => 2, // Cluster
                    (false, false, false) => 1 // Org level
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

            var serviceMatches = string.IsNullOrEmpty(ServiceTarget?.Service) ||
                                 ServiceTarget?.Service == "*" ||
                                 ServiceTarget?.Service == serviceName;

            var envMatches = string.IsNullOrEmpty(ServiceTarget?.Env) ||
                             ServiceTarget?.Env == "*" ||
                             ServiceTarget?.Env == environment;

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
                MergeLibConfigs(higherPriority.LibConfig, lowerPriority.LibConfig),
                higherPriority.ServiceTarget,
                higherPriority.ClusterTarget);
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
                DebugEnabled = higher.DebugEnabled ?? lower.DebugEnabled,
                RuntimeMetricsEnabled = higher.RuntimeMetricsEnabled ?? lower.RuntimeMetricsEnabled,
                ServiceMapping = higher.ServiceMapping ?? lower.ServiceMapping,
                DataStreamsEnabled = higher.DataStreamsEnabled ?? lower.DataStreamsEnabled,
                SpanSamplingRules = higher.SpanSamplingRules ?? lower.SpanSamplingRules,
            };
        }

        internal record struct ConfigurationTarget(bool HasService, bool HasEnv, bool HasCluster);
    }

    internal class ApmTracingConfigDto
    {
        [JsonProperty("lib_config")]
        public LibConfig? LibConfig { get; set; }

        [JsonProperty("service_target")]
        public ServiceTarget? ServiceTarget { get; set; }

        [JsonProperty("k8s_target_v2")]
        public K8sTargetV2? K8sTargetV2 { get; set; }
    }

    internal class ServiceTarget
    {
        [JsonProperty("service")]
        public string? Service { get; set; }

        [JsonProperty("env")]
        public string? Env { get; set; }
    }

    internal class K8sTargetV2
    {
        [JsonProperty("cluster_targets")]
        public List<ClusterTarget>? ClusterTargets { get; set; }
    }

    internal class ClusterTarget
    {
        [JsonProperty("cluster_name")]
        public string? ClusterName { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("enabled_namespaces")]
        public List<string>? EnabledNamespaces { get; set; }
    }

    internal class LibConfig
    {
        [JsonProperty("tracing_enabled")]
        public bool? TracingEnabled { get; set; }

        [JsonProperty("log_injection_enabled")]
        public bool? LogInjectionEnabled { get; set; }

        [JsonProperty("tracing_sampling_rate")]
        public double? TracingSamplingRate { get; set; }

        [JsonProperty("tracing_sampling_rules")]
        public object? TracingSamplingRules { get; set; }

        [JsonProperty("tracing_header_tags")]
        public object? TracingHeaderTags { get; set; }

        [JsonProperty("tracing_tags")]
        public object? TracingTags { get; set; }

        [JsonProperty("tracing_debug")]
        public bool? DebugEnabled { get; set; }

        [JsonProperty("runtime_metrics_enabled")]
        public bool? RuntimeMetricsEnabled { get; set; }

        [JsonProperty("tracing_service_mapping")]
        public string? ServiceMapping { get; set; }

        [JsonProperty("data_streams_enabled")]
        public bool? DataStreamsEnabled { get; set; }

        [JsonProperty("span_sampling_rules")]
        public object? SpanSamplingRules { get; set; }
    }
}
