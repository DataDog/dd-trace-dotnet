using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    public class AggregateConfigurationSource : IConfigurationSource
    {
        private readonly List<IConfigurationSource> _sources = new List<IConfigurationSource>();

        public void AddSource(IConfigurationSource source)
        {
            _sources.Add(source);
        }

        public string GetString(string key)
        {
            return _sources.Select(source => source.GetString(key))
                           .FirstOrDefault(value => value != null);
        }

        public int? GetInt32(string key)
        {
            return _sources.Select(source => source.GetInt32(key))
                           .FirstOrDefault(value => value != null);
        }

        public bool? GetBool(string key)
        {
            return _sources.Select(source => source.GetBool(key))
                           .FirstOrDefault(value => value != null);
        }
    }
}
