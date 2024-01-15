// <copyright file="SerializableDictionary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class SerializableDictionary : IXunitSerializable, IEnumerable<KeyValuePair<string, string>>
    {
        public Dictionary<string, string> Values { get; private set; } = new();

        public void Add(string key, string value) => Values.Add(key, value);

        public bool TryGetValue(string key, out string value) => Values.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Values.GetEnumerator();

        public void Deserialize(IXunitSerializationInfo info)
        {
            Values = JsonConvert.DeserializeObject<Dictionary<string, string>>(info.GetValue<string>(nameof(Values)));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Values), JsonConvert.SerializeObject(Values));
        }

        public Dictionary<string, string> ToDictionary() => new(Values);
    }
}
