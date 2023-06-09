// <copyright file="JsonConfigurationSourceWithCustomMapping.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration.ConfigurationSources
{
    internal class JsonConfigurationSourceWithCustomMapping : JsonConfigurationSource
    {
        private readonly IReadOnlyDictionary<string, string> _mapping;

        internal JsonConfigurationSourceWithCustomMapping(string json, IReadOnlyDictionary<string, string> mapping, ConfigurationOrigins origin)
            : base(json, origin)
        {
            _mapping = mapping;
        }

        private protected override JToken SelectToken(string key)
        {
            return base.SelectToken(_mapping.TryGetValue(key, out var newKey) ? newKey : key);
        }
    }
}
