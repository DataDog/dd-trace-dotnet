using System.Collections.Generic;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public interface IDictioDuckType
    {
        public ICollection<string> Keys { get; }

        public ICollection<string> Values { get; }

        int Count { get; }

        public string this[string key] { get; set; }

        void Add(string key, string value);

        bool ContainsKey(string key);

        bool Remove(string key);

        bool TryGetValue(string key, out string value);

        [Duck(Name = "TryGetValue")]
        bool TryGetValueInObject(string key, out object value);

        [Duck(Name = "TryGetValue")]
        bool TryGetValueInDuckChaining(string key, out IDictioValue value);

        IEnumerator<KeyValuePair<string, string>> GetEnumerator();
    }
}
