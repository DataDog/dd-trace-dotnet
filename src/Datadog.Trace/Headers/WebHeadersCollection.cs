#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Datadog.Trace.Headers
{
    internal class WebHeadersCollection : IHeadersCollection
    {
        private readonly WebHeaderCollection _headers;

        public WebHeadersCollection(WebHeaderCollection headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public IEnumerable<string> GetValues(string name)
            => _headers.GetValues(name) ?? Enumerable.Empty<string>();

        public void Set(string name, string value)
            => _headers.Set(name, value);

        public void Add(string name, string value)
            => _headers.Add(name, value);

        public void Remove(string name)
            => _headers.Remove(name);
    }
}

#endif
