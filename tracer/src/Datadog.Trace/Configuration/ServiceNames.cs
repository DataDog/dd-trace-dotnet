// <copyright file="ServiceNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    internal class ServiceNames
    {
        private readonly object _lock = new object();
        private Dictionary<string, string> _mappings = null;

        private ServiceNames()
        {
        }

        public ServiceNames(IDictionary<string, string> mappings)
        {
            if (mappings?.Count > 0)
            {
                _mappings = new Dictionary<string, string>(mappings);
            }
        }

        public static ServiceNames FromDictionary(Dictionary<string, string> mappings)
        {
            return new ServiceNames
            {
                _mappings = mappings
            };
        }

        public string GetServiceName(string applicationName, string key)
        {
            if (_mappings is not null && _mappings.TryGetValue(key, out var name))
            {
                return name;
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
                var mappingsDictionary = new Dictionary<string, string>();
                foreach (var item in mappings)
                {
                    mappingsDictionary[item.Key] = item.Value;
                }

                _mappings = mappingsDictionary;
            }
        }
    }
}
