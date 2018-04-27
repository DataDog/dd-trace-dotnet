using System;
using System.Collections;
using System.Collections.Generic;
using OpenTracing.Propagation;

namespace Datadog.Trace.Tests
{
    public class MockTextMap : ITextMap
    {
        private Dictionary<string, string> _dictionary = new Dictionary<string, string>();

        public string Get(string key)
        {
            _dictionary.TryGetValue(key, out string value);
            return value;
        }

        public void Set(string key, string value)
        {
            _dictionary[key] = value;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (KeyValuePair<string, string> pair in _dictionary)
            {
                yield return pair;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
