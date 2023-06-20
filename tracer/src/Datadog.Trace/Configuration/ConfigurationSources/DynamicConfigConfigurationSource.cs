// <copyright file="DynamicConfigConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration.ConfigurationSources
{
    internal class DynamicConfigConfigurationSource : JsonConfigurationSource
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DynamicConfigConfigurationSource));

        private static readonly IReadOnlyDictionary<string, string> Mapping = new Dictionary<string, string>
        {
            { ConfigurationKeys.DebugEnabled, "tracing_debug" },
            { ConfigurationKeys.RuntimeMetricsEnabled, "runtime_metrics_enabled" },
            { ConfigurationKeys.HeaderTags, "tracing_header_tags" },
            { ConfigurationKeys.ServiceNameMappings, "tracing_service_mapping" },
            { ConfigurationKeys.LogsInjectionEnabled, "log_injection_enabled" },
            { ConfigurationKeys.GlobalSamplingRate, "tracing_sample_rate" },
            { ConfigurationKeys.CustomSamplingRules, "tracing_sampling_rules" },
            { ConfigurationKeys.SpanSamplingRules, "span_sampling_rules" },
            { ConfigurationKeys.DataStreamsMonitoring.Enabled, "data_streams_enabled" }
        };

        internal DynamicConfigConfigurationSource(string json, ConfigurationOrigins origin)
            : base(json, origin, j => Deserialize(j))
        {
        }

        private static JToken? Deserialize(string config)
        {
            var jobject = JsonConvert.DeserializeObject(config) as JObject;

            if (jobject != null)
            {
                return jobject["lib_config"];
            }

            return jobject;
        }

        private static IDictionary<string, string> ReadHeaderTags(JToken token)
        {
            return ((JArray)token).ToDictionary(t => t["header"]!.Value<string>()!, t => t["tag_name"]!.Value<string>()!);
        }

        private static IDictionary<string, string> ReadServiceMapping(JToken token)
        {
            return ((JArray)token).ToDictionary(t => t["from_key"]!.Value<string>()!, t => t["to_name"]!.Value<string>()!);
        }

        private protected override JToken? SelectToken(string key)
        {
            return base.SelectToken(Mapping.TryGetValue(key, out var newKey) ? newKey : key);
        }

        private protected override IDictionary<string, string>? ConvertToDictionary(string key, JToken token)
        {
            if (!Mapping.TryGetValue(key, out var mappedKey))
            {
                mappedKey = key;
            }

            if (mappedKey == "tracing_service_mapping")
            {
                return ReadServiceMapping(token);
            }

            if (mappedKey == "tracing_header_tags")
            {
                return ReadHeaderTags(token);
            }

            return base.ConvertToDictionary(key, token);
        }
    }
}
