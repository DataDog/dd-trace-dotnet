// <copyright file="HttpHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient
{
    internal readonly struct HttpHeadersCollection : IHeadersCollection
    {
#if NETCOREAPP
        private readonly System.Net.Http.Headers.HttpRequestHeaders _headers;
#else
        private readonly IRequestHeaders _headers;
#endif

#if NETCOREAPP
        public HttpHeadersCollection(System.Net.Http.Headers.HttpRequestHeaders headers)
#else
        public HttpHeadersCollection(IRequestHeaders headers)
#endif
        {
            if (headers is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(headers));
            }

            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.TryGetValues(name, out IEnumerable<string> values))
            {
                return values;
            }

            return [];
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
