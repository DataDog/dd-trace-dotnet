using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal readonly struct ReflectionHttpHeadersCollection : IHeadersCollection
    {
        private readonly object _headers;

        public ReflectionHttpHeadersCollection(object headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.CallMethod<string, bool>("Contains", name).Value)
            {
                return _headers.CallMethod<string, IEnumerable<string>>("GetValues", name).Value ?? Enumerable.Empty<string>();
            }

            return Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers.CallMethod<string, bool>("Remove", name);
            _headers.CallVoidMethod<string, string>("Add", name, value);
        }

        public void Add(string name, string value)
        {
            _headers.CallVoidMethod<string, string>("Add", name, value);
        }

        public void Remove(string name)
        {
            _headers.CallMethod<string, bool>("Remove", name);
        }
    }
}
