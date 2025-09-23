// <copyright file="ApmTracingConfigMerger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Handles merging of multiple APM_TRACING configurations based on priority ordering
    /// </summary>
    internal static class ApmTracingConfigMerger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ApmTracingConfigMerger));

        /// <summary>
        /// Merges multiple APM_TRACING configurations based on priority ordering.
        /// </summary>
        public static string MergeConfigurations(List<RemoteConfiguration> configs, string serviceName, string environment)
        {
            if (configs.Count == 0)
            {
                return "{\"lib_config\":{}}";
            }

            var applicableConfigs = new List<ApmTracingConfig>(configs.Count);

            foreach (var config in configs)
            {
                try
                {
                    var jsonContent = Encoding.UTF8.GetString(config.Contents);
                    var configData = ParseConfiguration(config.Path.Id, jsonContent);

                    // Filter immediately during parsing
                    if (configData?.Matches(serviceName, environment) == true)
                    {
                        applicableConfigs.Add(configData);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to merge APM_TRACING configuration {ConfigPath}", config.Path.Path);
                }
            }

            if (applicableConfigs.Count == 0)
            {
                Log.Debug("No APM_TRACING configurations match service '{ServiceName}' and environment '{Environment}'", serviceName, environment);
                return "{\"lib_config\":{}}";
            }

            applicableConfigs.Sort((a, b) =>
            {
                var priorityComparison = b.Priority.CompareTo(a.Priority); // Descending
                return priorityComparison != 0 ? priorityComparison : string.Compare(a.ConfigId, b.ConfigId, StringComparison.Ordinal); // Ascending
            });

            var mergedConfig = applicableConfigs.Aggregate((current, next) => current.MergeWith(next));

            var result = new { lib_config = mergedConfig.LibConfig };
            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// Parses a single APM_TRACING configuration JSON
        /// </summary>
        private static ApmTracingConfig? ParseConfiguration(string configId, string jsonContent)
        {
            try
            {
                var configDto = JsonConvert.DeserializeObject<ApmTracingConfigDto>(jsonContent);

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
