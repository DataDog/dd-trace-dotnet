// <copyright file="DynamicConfigConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration.ConfigurationSources
{
    internal class DynamicConfigConfigurationSource : JsonConfigurationSource
    {
        private static readonly Dictionary<string, string> Mapping = new()
        {
            { ConfigurationKeys.TraceEnabled, "tracing_enabled" },
            // { ConfigurationKeys.DebugEnabled, "tracing_debug" },
            // { ConfigurationKeys.RuntimeMetricsEnabled, "runtime_metrics_enabled" },
            { ConfigurationKeys.HeaderTags, "tracing_header_tags" },
            // { ConfigurationKeys.ServiceNameMappings, "tracing_service_mapping" },
            { ConfigurationKeys.LogsInjectionEnabled, "log_injection_enabled" },
            { ConfigurationKeys.GlobalSamplingRate, "tracing_sampling_rate" },
            { ConfigurationKeys.CustomSamplingRules, "tracing_sampling_rules" },
            // { ConfigurationKeys.SpanSamplingRules, "span_sampling_rules" },
            // { ConfigurationKeys.DataStreamsMonitoring.Enabled, "data_streams_enabled" },
            { ConfigurationKeys.GlobalTags, "tracing_tags" }
        };

        internal DynamicConfigConfigurationSource(string json, ConfigurationOrigins origin)
            : base(json, origin, j => Deserialize(j))
        {
            TreatNullDictionaryAsEmpty = false;
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

        private static Dictionary<string, string> ReadHeaderTags(JToken token)
        {
            return ((JArray)token).ToDictionary(t => t["header"]!.Value<string>()!, t => t["tag_name"]!.Value<string>()!);
        }

        private static Dictionary<string, string> ReadServiceMapping(JToken token)
        {
            return ((JArray)token).ToDictionary(t => t["from_key"]!.Value<string>()!, t => t["to_name"]!.Value<string>()!);
        }

        private static Dictionary<string, string> ReadGlobalTags(JToken token)
        {
            var result = new Dictionary<string, string>();

            foreach (var item in (JArray)token)
            {
                var rawValue = item?.Value<string>();

                if (rawValue == null)
                {
                    continue;
                }

                var values = rawValue.Split(':');

                if (values.Length != 2 || values[0].Length == 0 || values[1].Length == 0)
                {
                    continue;
                }

                result[values[0]] = values[1];
            }

            return result;
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

            return mappedKey switch
            {
                "tracing_service_mapping" => ReadServiceMapping(token),
                "tracing_header_tags" => ReadHeaderTags(token),
                "tracing_tags" => ReadGlobalTags(token),
                _ => base.ConvertToDictionary(mappedKey, token)
            };
        }
    }
}
