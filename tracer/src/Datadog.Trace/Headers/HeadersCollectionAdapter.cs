// <copyright file="HeadersCollectionAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.Headers
{
    internal readonly struct HeadersCollectionAdapter : IHeadersCollection
    {
        private readonly IHeaderDictionary _headers;

        public HeadersCollectionAdapter(IHeaderDictionary headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.TryGetValue(name, out var values))
            {
                return values;
            }

            return Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers[name] = value;
        }
    }
}
#endif
