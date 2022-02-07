// <copyright file="IHeaderDictionaryHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies;

namespace Datadog.Trace.Headers
{
    internal readonly struct IHeaderDictionaryHeadersCollection : IHeadersCollection
    {
        private readonly IHeaderDictionary _headers;

        public IHeaderDictionaryHeadersCollection(IHeaderDictionary headers)
        {
            _headers = headers;
        }

        public void Add(string name, string value)
        {
            _headers.Add(name, value);
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.TryGetValue(name, out var values))
            {
                return values;
            }

            return Enumerable.Empty<string>();
        }

        public void Remove(string name)
        {
            _headers.Remove(name);
        }

        public void Set(string name, string value)
        {
            _headers.Remove(name);
            _headers.Add(name, value);
        }
    }
}
#endif
