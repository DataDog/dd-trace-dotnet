// <copyright file="HttpHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Profiler.IntegrationTests.Helpers.HttpOverStreams
{
    internal class HttpHeaders : IEnumerable<HttpHeaders.HttpHeader>
    {
        private readonly List<HttpHeader> _headers;

        public HttpHeaders()
        {
            _headers = new List<HttpHeader>();
        }

        public HttpHeaders(int initialCapacity)
        {
            _headers = new List<HttpHeader>(initialCapacity);
        }

        public int Count => _headers.Count;

        public bool IsReadOnly { get; }

        public void Add(string name, string value)
        {
            _headers.Add(new HttpHeader(name, value));
        }

        public void Remove(string name)
        {
            _headers.RemoveAll(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public string GetValue(string name)
        {
            foreach (var header in _headers)
            {
                if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        public IEnumerable<string> GetValues(string name)
        {
            foreach (var header in _headers)
            {
                if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    yield return header.Value;
                }
            }
        }

        IEnumerator<HttpHeader> IEnumerable<HttpHeader>.GetEnumerator()
        {
            return _headers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _headers.GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join(", ", _headers.Select(h => $"{h.Name}: {h.Value}"));
        }

        public readonly struct HttpHeader
        {
            public readonly string Name;

            public readonly string Value;

            public HttpHeader(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public override string ToString()
            {
                return $"{Name}: {Value}";
            }
        }
    }
}
