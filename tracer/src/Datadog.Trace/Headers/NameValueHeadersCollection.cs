// <copyright file="NameValueHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Util;

namespace Datadog.Trace.Headers
{
    internal readonly struct NameValueHeadersCollection : IHeadersCollection
    {
        private readonly NameValueCollection _headers;

        public NameValueHeadersCollection(NameValueCollection headers)
        {
            if (headers is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(headers));
            }

            _headers = headers;
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
