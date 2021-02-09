using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClientHandler
{
    internal readonly struct HttpHeadersCollection : IHeadersCollection
    {
        private readonly IRequestHeaders _headers;

        public HttpHeadersCollection(IRequestHeaders headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.TryGetValues(name, out IEnumerable<string> values))
            {
                return values;
            }

            return Enumerable.Empty<string>();
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
