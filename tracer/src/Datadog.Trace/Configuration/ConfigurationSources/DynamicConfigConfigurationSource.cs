// <copyright file="DynamicConfigConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration.ConfigurationSources
{
    internal class DynamicConfigConfigurationSource : JsonConfigurationSource
    {
        private readonly IReadOnlyDictionary<string, string> _mapping;

        internal DynamicConfigConfigurationSource(string json, IReadOnlyDictionary<string, string> mapping, ConfigurationOrigins origin)
            : base(json, origin, j => Deserialize(j))
        {
            _mapping = mapping;
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

        private protected override JToken? SelectToken(string key)
        {
            return base.SelectToken(_mapping.TryGetValue(key, out var newKey) ? newKey : key);
        }
    }
}
