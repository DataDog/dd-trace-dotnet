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
    internal sealed class ApmTracingConfig
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
        /// Precedence ordering goes from most specific to least specific:
        /// 1. Service (bit 2), 2. Env (bit 1), 3. Cluster target (bit 0)
        /// </summary>
        public int Priority
        {
            get
            {
                var hasService = !string.IsNullOrEmpty(ServiceTarget?.Service) && ServiceTarget?.Service != "*";
                var hasEnv = !string.IsNullOrEmpty(ServiceTarget?.Env) && ServiceTarget?.Env != "*";
                var hasCluster = ClusterTarget is { ClusterTargets.Count: > 0 };

                // This handles all possible combinations
                return ((hasService ? 1 : 0) << 2) |
                       ((hasEnv ? 1 : 0) << 1) |
                       ((hasCluster ? 1 : 0) << 0);
            }
        }

        /// <summary>
        /// Checks if this configuration matches the current service and environment
        /// </summary>
        public bool Matches(string? serviceName, string? environment)
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
                DynamicInstrumentationEnabled = higher.DynamicInstrumentationEnabled ?? lower.DynamicInstrumentationEnabled,
                ExceptionReplayEnabled = higher.ExceptionReplayEnabled ?? lower.ExceptionReplayEnabled,
                CodeOriginEnabled = higher.CodeOriginEnabled ?? lower.CodeOriginEnabled
            };
        }
    }

    internal sealed class ApmTracingConfigDto
    {
        [JsonProperty("lib_config")]
        public LibConfig? LibConfig { get; set; }

        [JsonProperty("service_target")]
        public ServiceTarget? ServiceTarget { get; set; }

        [JsonProperty("k8s_target_v2")]
        public K8sTargetV2? K8sTargetV2 { get; set; }
    }

    internal sealed class ServiceTarget
    {
        [JsonProperty("service")]
        public string? Service { get; set; }

        [JsonProperty("env")]
        public string? Env { get; set; }
    }

    internal sealed class K8sTargetV2
    {
        [JsonProperty("cluster_targets")]
        public List<ClusterTarget>? ClusterTargets { get; set; }
    }

    internal sealed class ClusterTarget
    {
        [JsonProperty("cluster_name")]
        public string? ClusterName { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("enabled_namespaces")]
        public List<string>? EnabledNamespaces { get; set; }
    }

    internal sealed class LibConfig
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

        [JsonProperty("dynamic_instrumentation_enabled")]
        public bool? DynamicInstrumentationEnabled { get; set; }

        [JsonProperty("exception_replay_enabled")]
        public bool? ExceptionReplayEnabled { get; set; }

        [JsonProperty("code_origin_enabled")]
        public bool? CodeOriginEnabled { get; set; }
    }
}
