using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Datadog.Trace.Headers
{
    internal class NameValueHeadersCollection : IHeadersCollection
    {
        private readonly NameValueCollection _headers;

        public NameValueHeadersCollection(NameValueCollection headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public IEnumerable<string> GetValues(string name)
        {
            return _headers.GetValues(name) ?? Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers.Set(name, value);
        }

        public void Add(string name, string value)
        {
            _headers.Add(name, value);
        }

        public void Remove(string name)
        {
            _headers.Remove(name);
        }
    }
}
