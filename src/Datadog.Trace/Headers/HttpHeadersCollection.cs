using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace Datadog.Trace.Headers
{
    internal class HttpHeadersCollection : IHeadersCollection
    {
        private readonly HttpHeaders _headers;

        public HttpHeadersCollection(HttpHeaders headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public IEnumerable<string> GetValues(string name)
        {
            return _headers.TryGetValues(name, out var values)
                       ? values
                       : Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers.Remove(name);
            _headers.Add(name, value);
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
