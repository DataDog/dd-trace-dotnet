// <copyright file="ApmTracingConfigMerger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Handles merging of multiple APM_TRACING configurations based on priority ordering
    /// </summary>
    internal static class ApmTracingConfigMerger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ApmTracingConfigMerger));

        /// <summary>
        /// Merges multiple APM_TRACING configurations based on priority ordering and returns a JToken.
        /// </summary>
        public static JToken MergeConfigurations(List<RemoteConfiguration> configs, string? serviceName, string? environment)
        {
            if (configs.Count == 0)
            {
                return JToken.Parse("{\"lib_config\":{}}");
            }

            var applicableConfigs = new List<ApmTracingConfig>(configs.Count);

            foreach (var config in configs)
            {
                try
                {
                    var jsonContent = Encoding.UTF8.GetString(config.Contents);
                    var configData = ParseConfiguration(config.Path.Id, jsonContent);

                    if (configData == null)
                    {
                        continue;
                    }

                    // Do not filter un-matched config for now. We will address it in a later PR.
                    applicableConfigs.Add(configData);
                    /*
                    if (configData?.Matches(serviceName, environment) == true)
                    {
                        applicableConfigs.Add(configData);
                    }
                    */
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to merge APM_TRACING configuration {ConfigPath}", config.Path.Path);
                }
            }

            if (applicableConfigs.Count == 0)
            {
                Log.Debug("No APM_TRACING configurations match service '{ServiceName}' and environment '{Environment}'", serviceName, environment);
                return JToken.Parse("{\"lib_config\":{}}");
            }

            // Sort configs by priority (highest first), then by config ID (alphabetically) for deterministic ordering
            applicableConfigs.Sort(CompareConfigsByPriority);

            // Merge all configs in priority order, with higher priority configs overriding lower priority ones
            var mergedLibConfig = MergeConfigsInPriorityOrder(applicableConfigs);

            var result = new { lib_config = mergedLibConfig };
            return JToken.FromObject(result);
        }

        /// <summary>
        /// Compares two configs by priority (highest first), then by config ID (alphabetically)
        /// </summary>
        private static int CompareConfigsByPriority(ApmTracingConfig a, ApmTracingConfig b)
        {
            var priorityComparison = b.Priority.CompareTo(a.Priority); // Descending
            return priorityComparison != 0 ? priorityComparison : string.Compare(a.ConfigId, b.ConfigId, StringComparison.Ordinal); // Ascending
        }

        /// <summary>
        /// Merges configs in priority order, leveraging the fact that they're already sorted.
        /// Stops early when all LibConfig properties are populated (no further merging needed).
        /// </summary>
        private static LibConfig MergeConfigsInPriorityOrder(List<ApmTracingConfig> sortedConfigs)
        {
            if (sortedConfigs.Count == 1)
            {
                return sortedConfigs[0].LibConfig;
            }

            // Since configs are already sorted, start with the highest priority
            var result = sortedConfigs[0].LibConfig;
            for (int i = 1; i < sortedConfigs.Count; i++)
            {
                result = MergeLibConfigs(result, sortedConfigs[i].LibConfig);

                // Early exit if all LibConfig properties are populated
                if (IsLibConfigComplete(result))
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if all LibConfig properties are populated (non-null)
        /// </summary>
        private static bool IsLibConfigComplete(LibConfig libConfig)
        {
            return libConfig is
            {
                TracingEnabled: not null,
                LogInjectionEnabled: not null,
                TracingSamplingRate: not null,
                TracingSamplingRules: not null,
                TracingHeaderTags: not null,
                TracingTags: not null,
                DebugEnabled: not null,
                RuntimeMetricsEnabled: not null,
                ServiceMapping: not null,
                DataStreamsEnabled: not null,
                SpanSamplingRules: not null,
                DynamicInstrumentationEnabled: not null,
                ExceptionReplayEnabled: not null,
                CodeOriginEnabled: not null
            };
        }

        /// <summary>
        /// Merges LibConfig objects, with higher priority config taking precedence
        /// </summary>
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

        /// <summary>
        /// Parses a single APM_TRACING configuration JSON
        /// </summary>
        private static ApmTracingConfig? ParseConfiguration(string configId, string jsonContent)
        {
            try
            {
                var configDto = JsonHelper.DeserializeObject<ApmTracingConfigDto>(jsonContent);

                if (configDto?.LibConfig == null)
                {
                    Log.Warning("APM_TRACING configuration {ConfigId} has no lib_config", configId);
                    return null;
                }

                // ServiceTarget might be null (org-level config)
                return new ApmTracingConfig(configId, configDto.LibConfig, configDto.ServiceTarget, configDto.K8sTargetV2);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to parse APM_TRACING configuration {ConfigId}", configId);
                return null;
            }
        }
    }
}
