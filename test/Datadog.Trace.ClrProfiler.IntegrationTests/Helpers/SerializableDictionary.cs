using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SerializableDictionary : IXunitSerializable, IEnumerable<KeyValuePair<string, string>>
    {
        public Dictionary<string, string> Values { get; private set; } = new Dictionary<string, string>();

        public void Add(string key, string value) => Values.Add(key, value);

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
    }
}
