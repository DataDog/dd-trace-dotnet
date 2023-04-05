// <copyright file="ServiceNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    internal class ServiceNames
    {
        private readonly object _lock = new object();
        private Dictionary<string, string> _mappings = null;
        private bool _unifyServiceNames;

        public ServiceNames(IDictionary<string, string> mappings, string metadataSchemaVersion)
        {
            _unifyServiceNames = string.Equals(metadataSchemaVersion, "v1", StringComparison.OrdinalIgnoreCase)
                                    ? true
                                    : false;
            if (mappings?.Count > 0)
            {
                _mappings = new Dictionary<string, string>(mappings);
            }
        }

        public string GetServiceName(string applicationName, string key)
        {
            if (_mappings is not null && _mappings.TryGetValue(key, out var name))
            {
                return name;
            }
            else if (_unifyServiceNames)
            {
                return applicationName;
            }
            else
            {
                return $"{applicationName}-{key}";
            }
        }

        public bool TryGetServiceName(string key, out string name)
        {
            if (_mappings is not null && _mappings.TryGetValue(key, out name))
            {
                return true;
            }

            name = null;
            return false;
        }

        public void SetServiceNameMappings(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            lock (_lock)
            {
                _mappings = mappings.ToDictionary(x => x.Key, x => x.Value);
            }
        }
    }
}
