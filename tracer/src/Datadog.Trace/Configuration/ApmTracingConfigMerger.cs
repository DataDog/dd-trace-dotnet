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
        /// Merges multiple APM_TRACING configurations based on priority ordering.
        /// </summary>
        public static string MergeConfigurations(List<RemoteConfiguration> configs, string serviceName, string environment)
        {
            if (configs.Count == 1)
            {
                // Single config - still need to check if it matches the service/environment
                var singleConfigContent = Encoding.UTF8.GetString(configs[0].Contents);
                var singleConfig = ParseConfiguration(configs[0].Path.Id, singleConfigContent);

                if (singleConfig == null || !singleConfig.Matches(serviceName, environment))
                {
                    return "{\"lib_config\":{}}";
                }

                return singleConfigContent;
            }

            var parsedConfigs = new List<ApmTracingConfig>();
            foreach (var config in configs)
            {
                try
                {
                    var jsonContent = Encoding.UTF8.GetString(config.Contents);
                    var configData = ParseConfiguration(config.Path.Id, jsonContent);
                    if (configData != null)
                    {
                        parsedConfigs.Add(configData);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to parse APM_TRACING configuration {ConfigPath}", config.Path.Path);
                }
            }

            if (parsedConfigs.Count == 0)
            {
                Log.Warning("No valid APM_TRACING configurations found");
                return "{\"lib_config\":{}}";
            }

            // Filter configurations that match the current service and environment
            var applicableConfigs = parsedConfigs
                .Where(c => c.Matches(serviceName, environment))
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.ConfigId) // For deterministic ordering when priorities are equal
                .ToList();

            if (applicableConfigs.Count == 0)
            {
                Log.Debug("No APM_TRACING configurations match service '{ServiceName}' and environment '{Environment}'", serviceName, environment);
                return "{\"lib_config\":{}}";
            }

            // Merge configurations based on priority using the new MergeWith method
            var mergedConfig = applicableConfigs.Aggregate((current, next) => current.MergeWith(next));

            // Wrap in the expected structure for DynamicConfigConfigurationSource
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
                return new ApmTracingConfig(configId, configDto.ServiceTarget, configDto.LibConfig);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to parse APM_TRACING configuration {ConfigId}", configId);
                return null;
            }
        }
    }
}
