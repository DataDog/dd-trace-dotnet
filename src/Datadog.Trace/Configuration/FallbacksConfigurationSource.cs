using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    internal class FallbacksConfigurationSource : IConfigurationSource
    {
        private readonly IConfigurationSource _source;

        public FallbacksConfigurationSource(IConfigurationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public bool? GetBool(string key)
        {
            return GetBool(key, templateValue: null);
        }

        public bool? GetBool(string key, string templateValue)
        {
            return GetAllKeys(key, templateValue)
                  .Select(_source.GetBool)
                  .FirstOrDefault(value => value != null);
        }

        public IDictionary<string, string> GetDictionary(string key)
        {
            return GetDictionary(key, templateValue: null);
        }

        public IDictionary<string, string> GetDictionary(string key, string templateValue)
        {
            return GetAllKeys(key, templateValue)
                  .Select(_source.GetDictionary)
                  .FirstOrDefault(value => value != null);
        }

        public double? GetDouble(string key)
        {
            return GetDouble(key, templateValue: null);
        }

        public double? GetDouble(string key, string templateValue)
        {
            return GetAllKeys(key, templateValue)
                  .Select(_source.GetDouble)
                  .FirstOrDefault(value => value != null);
        }

        public int? GetInt32(string key)
        {
            return GetInt32(key, templateValue: null);
        }

        public int? GetInt32(string key, string templateValue)
        {
            return GetAllKeys(key, templateValue)
                  .Select(_source.GetInt32)
                  .FirstOrDefault(value => value != null);
        }

        public string GetString(string key)
        {
            return GetString(key, templateValue: null);
        }

        public string GetString(string key, string templateValue)
        {
            return GetAllKeys(key, templateValue)
                  .Select(_source.GetString)
                  .FirstOrDefault(value => value != null);
        }

        private static IEnumerable<string> GetAllKeys(string key, string templateValue)
        {
            // try to expand a single key into all its fallbacks
            IEnumerable<string> keys = ConfigurationKeyFallbacks.Instance.GetKeys(key);

            // if specified, try to replace "{0}" in key templates
            return templateValue != null ? keys.Select(s => string.Format(s, templateValue)) : keys;
        }
    }
}
