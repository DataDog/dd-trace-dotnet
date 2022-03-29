// <copyright file="MockTextMap.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
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

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        public void Set(string key, string value)
        {
            _dictionary[key] = value;
        }
    }
}
