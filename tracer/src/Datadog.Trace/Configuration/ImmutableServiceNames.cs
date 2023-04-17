// <copyright file="ImmutableServiceNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    internal class ImmutableServiceNames
    {
        private readonly Dictionary<string, string> _mappings = null;
        private readonly bool _unifyServiceNames;

        public ImmutableServiceNames(IDictionary<string, string> mappings, string metadataSchemaVersion)
        {
            _unifyServiceNames = metadataSchemaVersion == "v0" ? false : true;
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
    }
}
