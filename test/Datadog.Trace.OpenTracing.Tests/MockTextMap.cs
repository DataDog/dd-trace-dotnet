using System.Collections.Generic;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class MockTextMap : ITextMap
    {
        private Dictionary<string, string> _dictionary = new Dictionary<string, string>();

        public string Get(string key)
        {
            _dictionary.TryGetValue(key, out string value);
            return value;
        }

        public IEnumerable<KeyValuePair<string, string>> GetEntries()
        {
            return _dictionary;
        }

        public void Set(string key, string value)
        {
            _dictionary[key] = value;
        }
    }
}
