using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Headers
{
    internal class DictionaryHeadersCollection : IHeadersCollection
    {
        private readonly IDictionary<string, IList<string>> _headers;

        public DictionaryHeadersCollection()
        {
            _headers = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public DictionaryHeadersCollection(int capacity)
        {
            _headers = new Dictionary<string, IList<string>>(capacity, StringComparer.OrdinalIgnoreCase);
        }

        public DictionaryHeadersCollection(IDictionary<string, IList<string>> dictionary)
        {
            _headers = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        public IEnumerable<string> GetValues(string name)
        {
            return _headers.TryGetValue(name, out var values)
                       ? values
                       : Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers.Remove(name);
            _headers.Add(name, new List<string> { value });
        }

        public void Add(string name, string value)
        {
            Add(name, new[] { value });
        }

        public void Add(string name, IEnumerable<string> values)
        {
            if (!_headers.TryGetValue(name, out var list))
            {
                list = new List<string>();
            }

            foreach (var value in values)
            {
                list.Add(value);
            }
        }

        public void Remove(string name)
        {
            _headers.Remove(name);
        }
    }
}
