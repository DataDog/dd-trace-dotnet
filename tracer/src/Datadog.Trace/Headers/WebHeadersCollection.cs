// <copyright file="WebHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

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
