using System;
using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    internal class ServiceNames
    {
        private readonly object _lock = new object();
        private Dictionary<string, string> _mappings = null;

        public ServiceNames(IDictionary<string, string> mappings)
        {
            if (mappings is null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            if (mappings.Count > 0)
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
            else
            {
                return $"{applicationName}-{key}";
            }
        }

        public void SetServiceNameMapping(string originalName, string newName)
        {
            lock (_lock)
            {
                var others = _mappings;
                others = others is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(others);
                others[originalName] = newName;
                _mappings = others;
            }
        }
    }
}
